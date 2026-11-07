using System.Security.Claims;
using System.Text.Json;
using CasaMulher.Api.Controllers;
using CasaMulher.Api.Data;
using CasaMulher.Api.DTOs;
using CasaMulher.Api.Models;
using CasaMulher.Api.Security;
using CasaMulher.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;

namespace CasaMulher.Api.Tests.Services;

/// <summary>
/// Agenda e Calendário: as regras do protótipo aprovado (plano v3) rodando no servidor.
/// Hora simulada: terça 10/11/2026 13:40 (Brasília). Dados fictícios.
/// </summary>
public sealed class AgendaTests : IAsyncLifetime
{
    private static readonly UsuarioAtuante Ana = new("u-ana", PerfisAcesso.Psicologia, "PSI-000001", "Ana Psicóloga", null);
    private static readonly UsuarioAtuante Bruna = new("u-bruna", PerfisAcesso.Psicologia, "PSI-000002", "Bruna Psicóloga", null);
    private static readonly UsuarioAtuante Jur1 = new("u-jur1", PerfisAcesso.Juridico, "JUR-000001", "Carla Advogada", null);
    private static readonly UsuarioAtuante Jur2 = new("u-jur2", PerfisAcesso.Juridico, "JUR-000002", "Diana Advogada", null);
    private static readonly UsuarioAtuante Rec = new("u-rec", PerfisAcesso.Recepcao, "REC-000001", "Elisa Recepção", null);
    private static readonly UsuarioAtuante Adm = new("u-adm", PerfisAcesso.Adm, "ADM-000001", "Sônia Coordenação", null);
    private static readonly UsuarioAtuante Prof1 = new("u-prof1", PerfisAcesso.Professor, "PRF-000001", "Fernanda Professora", null);
    private static readonly UsuarioAtuante Prof2 = new("u-prof2", PerfisAcesso.Professor, "PRF-000002", "Gabriela Professora", null);

    private static readonly DateTime Agora = new(2026, 11, 10, 16, 40, 0, DateTimeKind.Utc); // 13:40 em Brasília

    private SqliteConnection _conn = null!;
    private AppDbContext _db = null!;
    private readonly List<string> _auditoria = [];
    private int _maria;
    private int _cursoInfo;
    private int _cursoCul;

    public async Task InitializeAsync()
    {
        _conn = new SqliteConnection("Data Source=:memory:");
        await _conn.OpenAsync();
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();

        foreach (var u in new[] { Ana, Bruna, Jur1, Jur2, Rec, Adm, Prof1, Prof2 })
            _db.Users.Add(new ApplicationUser { Id = u.UserId, UserName = u.Identificador, NomeCompleto = u.Nome, Perfil = u.Perfil, IdentificadorFuncionario = u.Identificador, Ativo = true });

        var maria = new Assistida
        {
            Nome = "Maria Ficticia", Cpf = "52998224725", Origem = AssistidaOrigem.Recepcao, CadastradaPorUserId = Rec.UserId,
            CadastradaPorIdentificador = Rec.Identificador, CadastradaPorNome = Rec.Nome, EncaminhadaPsicologiaEm = Agora.AddDays(-20), EncaminhadaJuridicoEm = Agora.AddDays(-20)
        };
        _db.Assistidas.Add(maria);
        await _db.SaveChangesAsync();
        _maria = maria.Id;

        var psi = new ProntuarioPsicologico { AssistidaId = maria.Id, Numero = "PSI-2026-000001", Ano = 2026, Sequencial = 1, Visibilidade = ProntuarioVisibilidade.Responsaveis };
        psi.Responsaveis.Add(new ProntuarioPsicologicoResponsavel { UserId = Ana.UserId, Identificador = Ana.Identificador, Nome = Ana.Nome, AtribuidoPorIdentificador = "X" });
        _db.ProntuariosPsicologicos.Add(psi);
        var caso = new CasoJuridico { AssistidaId = maria.Id, Visibilidade = CasoJuridicoVisibilidade.Responsaveis };
        caso.Responsaveis.Add(new CasoJuridicoResponsavel { UserId = Jur1.UserId, Identificador = Jur1.Identificador, Nome = Jur1.Nome, AtribuidoPorIdentificador = "X" });
        _db.CasosJuridicos.Add(caso);

        var info = new Curso { Nome = "Informática" };
        var cul = new Curso { Nome = "Culinária" };
        _db.Cursos.AddRange(info, cul);
        await _db.SaveChangesAsync();
        _cursoInfo = info.Id;
        _cursoCul = cul.Id;
        _db.ProfessoresCursos.AddRange(new ProfessorCurso { UserId = Prof1.UserId, CursoId = info.Id }, new ProfessorCurso { UserId = Prof2.UserId, CursoId = cul.Id });
        _db.AssistidasCursos.Add(new AssistidaCurso { AssistidaId = maria.Id, CursoId = info.Id });
        // Informática: terça 18–20 e quinta 18–20. Culinária: quarta 9–11.
        _db.CursosHorarios.AddRange(
            new CursoHorario { CursoId = info.Id, DiaSemana = 2, Inicio = new(18, 0), Fim = new(20, 0), Sala = "Sala 2", ValidoDesde = new(2026, 11, 1) },
            new CursoHorario { CursoId = info.Id, DiaSemana = 4, Inicio = new(18, 0), Fim = new(20, 0), Sala = "Sala 2", ValidoDesde = new(2026, 11, 1) },
            new CursoHorario { CursoId = cul.Id, DiaSemana = 3, Inicio = new(9, 0), Fim = new(11, 0), Sala = "Cozinha", ValidoDesde = new(2026, 11, 1) });
        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    // ------------------------------------------------------------------ apoio

    private sealed class AuditoriaFalsa(List<string> acoes) : IAuditoriaService
    {
        public Task RegistrarAsync(string acao, string entidade, string? entidadeId, string descricao, string? identificadorReferencia = null, string? escopo = null)
        {
            acoes.Add(acao + " | " + descricao);
            return Task.CompletedTask;
        }
    }

    private sealed class Ambiente : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "CasaMulher.Api.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = Path.GetTempPath();
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private SecuritySnapshotPersistenceService Snapshot()
    {
        var config = new ConfigurationBuilder().Build();
        var hml = new HmlDbSnapshotService(new GitHubPrivateFileService(new HttpClient(), config), config, new Ambiente(),
            new HmlDbStorageInfo(Path.Combine(Path.GetTempPath(), "x.db"), true), new HmlDbSnapshotState(), NullLogger<HmlDbSnapshotService>.Instance);
        return new SecuritySnapshotPersistenceService(hml, new Ambiente(), NullLogger<SecuritySnapshotPersistenceService>.Instance);
    }

    private AgendaController C(UsuarioAtuante u)
    {
        _db.ChangeTracker.Clear();
        var agenda = new AgendaService(_db) { RelogioUtc = () => Agora };
        return new AgendaController(_db, agenda, new AssistidasService(_db, NullLogger<AssistidasService>.Instance), new AuditoriaFalsa(_auditoria), Snapshot())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, u.UserId), new Claim("perfil", u.Perfil)], "teste"))
                }
            }
        };
    }

    private static int Status(IActionResult? r) => r switch
    {
        null => 200,
        ObjectResult o => o.StatusCode ?? 200,
        StatusCodeResult s => s.StatusCode,
        _ => 0
    };

    private static int Status<T>(ActionResult<T> r) => Status(r.Result);

    private static T Valor<T>(ActionResult<T> r) => r.Value ?? (T)((ObjectResult)r.Result!).Value!;

    private static string Json(object? o) => JsonSerializer.Serialize(o, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

    private static string Corpo(IActionResult r) => Json(((ObjectResult)r).Value);

    private async Task<List<AgendaItemResponse>> Lista(UsuarioAtuante u, string de = "2026-11-09", string ate = "2026-11-15") =>
        Valor(await C(u).Listar(de, ate, default)).Itens;

    private async Task<string> AgendarAsync(UsuarioAtuante u, string inicio, int dur = 50, bool presencial = true, bool avisar = true, string tipo = "sessao", string? justificativa = null, int? remarcar = null)
    {
        var r = await C(u).Agendar(new AgendaAgendamentoRequest
        {
            AssistidaId = _maria, Tipo = tipo, Inicio = inicio, DuracaoMinutos = dur, Presencial = presencial, AvisarRecepcao = avisar,
            Observacao = "Trazer documento", Justificativa = justificativa, RemarcarDeId = remarcar
        }, default);
        Assert.True(Status(r) == 200, Corpo(r));
        return JsonDocument.Parse(Corpo(r)).RootElement.GetProperty("id").GetString()!;
    }

    private static int Num(string id) => int.Parse(id.Split(':')[1]);

    private static AgendaCompromissoRequest Reuniao(string ini, string fim, params string[] participantes) => new()
    {
        Tipo = AgendaTipoManual.Reuniao, Titulo = "Reunião de equipe", Visibilidade = AgendaVisibilidade.Eu, Participantes = participantes.ToList(),
        Ocorrencias = [new AgendaOcorrenciaRequest { Inicio = ini, Fim = fim }]
    };

    private async Task TirarAcessoDaAnaAsync()
    {
        _db.ChangeTracker.Clear();
        var r = await _db.ProntuariosPsicologicosResponsaveis.SingleAsync(x => x.UserId == Ana.UserId);
        r.UserId = Bruna.UserId;
        await _db.SaveChangesAsync();
    }

    // ------------------------------------------------------------------ sigilo

    [Fact]
    public async Task Responsavel_ve_detalhe_e_outra_psicologa_nao_ve_nada()
    {
        var id = await AgendarAsync(Ana, "2026-11-10T15:00");

        var daAna = Assert.Single(await Lista(Ana), i => i.Id == id);
        Assert.Equal(AgendaService.NivelDetalhe, daAna.Nivel);
        Assert.Contains("Maria Ficticia", daAna.Sub);

        Assert.DoesNotContain(await Lista(Bruna), i => i.Id == id);
        Assert.Equal(404, Status(await C(Bruna).Detalhe(id, default)));
        Assert.DoesNotContain(await Lista(Jur1), i => i.Id == id);
    }

    [Fact]
    public async Task Oculto_nao_e_livre_e_conflito_nao_revela_o_motivo()
    {
        await AgendarAsync(Ana, "2026-11-10T15:00");

        var sem = await C(Bruna).CriarCompromisso(Reuniao("2026-11-10T15:00", "2026-11-10T15:30", Ana.UserId), default);
        Assert.Equal(409, Status(sem));
        var corpo = Corpo(sem);
        Assert.Contains("Há uma indisponibilidade na agenda de Ana", corpo);
        Assert.DoesNotContain("Maria", corpo);
        Assert.DoesNotContain("Sessão", corpo);

        var req = Reuniao("2026-11-10T15:00", "2026-11-10T15:30", Ana.UserId);
        req.Ocorrencias[0].Justificativa = "Única hora possível";
        Assert.Equal(200, Status(await C(Bruna).CriarCompromisso(req, default)));
        Assert.Contains(_auditoria, a => a.Contains("confirmou por cima de conflito"));
    }

    [Fact]
    public async Task Disponibilidade_mostra_indisponivel_sem_motivo()
    {
        await AgendarAsync(Ana, "2026-11-10T15:00");
        var faixas = Valor(await C(Bruna).Disponibilidade(new AgendaDisponibilidadeRequest { Pessoas = [Ana.UserId], De = "2026-11-10", Ate = "2026-11-10" }, default));
        var f = Assert.Single(faixas, x => x.Inicio == "2026-11-10T15:00:00");
        Assert.Equal("Indisponível", f.Rotulo.Split(" · ")[0]);
        Assert.False(f.Revelado);
        Assert.Contains(_auditoria, a => a.StartsWith("AGENDA_DISPONIBILIDADE"));
    }

    [Fact]
    public async Task Recepcao_ve_so_esperada_do_dia_sem_area_nem_motivo_e_marca_chegada()
    {
        var id = await AgendarAsync(Ana, "2026-11-10T15:00");
        var semAviso = await AgendarAsync(Ana, "2026-11-10T16:30", avisar: false);
        var amanha = await AgendarAsync(Ana, "2026-11-11T15:00");

        var lista = await Lista(Rec);
        var item = Assert.Single(lista, i => i.Id == id);
        Assert.Equal(AgendaService.NivelRecepcao, item.Nivel);
        Assert.Equal("Atendimento com a equipe técnica", item.Sub);
        Assert.Contains("Maria Ficticia", item.Titulo);
        var json = Json(item);
        Assert.DoesNotContain("Psicologia", json);
        Assert.DoesNotContain("Sessão", json);
        Assert.DoesNotContain("documento", json);
        Assert.DoesNotContain(lista, i => i.Id == semAviso);
        Assert.DoesNotContain(lista, i => i.Id == amanha);

        var det = Valor(await C(Rec).Detalhe(id, default));
        Assert.Contains("chegou", det.Acoes);
        Assert.Contains(det.Linhas, l => l[0] == "Situação" && l[1] == "Esperada");

        Assert.Equal(200, Status(await C(Rec).Chegou(Num(id), default)));
        Assert.Equal(400, Status(await C(Rec).Chegou(Num(id), default)));
        Assert.Equal(404, Status(await C(Rec).Chegou(Num(semAviso), default)));
        Assert.Equal(403, Status(await C(Bruna).Chegou(Num(id), default)));

        var pend = Valor(await C(Ana).Pendencias(default));
        Assert.Contains(pend, p => p.Tipo == "chegada" && p.Id == id);
        Assert.Equal(200, Status(await C(Ana).Recebida(Num(id), default)));
        det = Valor(await C(Rec).Detalhe(id, default));
        Assert.Contains(det.Linhas, l => l[0] == "Situação" && l[1].StartsWith("Recebida pela equipe"));
    }

    [Fact]
    public async Task Remarcado_sai_das_esperadas_e_guarda_historico()
    {
        var id = await AgendarAsync(Ana, "2026-11-10T15:00");
        var novo = await AgendarAsync(Ana, "2026-11-12T15:00", remarcar: Num(id));

        Assert.DoesNotContain(await Lista(Rec), i => i.Id == id);
        var antigo = await _db.AgendaAgendamentos.AsNoTracking().SingleAsync(a => a.Id == Num(id));
        Assert.Equal(AgendaSituacao.Remarcado, antigo.Situacao);
        Assert.Null(antigo.ChegadaEstado);
        var det = Valor(await C(Ana).Detalhe(novo, default));
        Assert.Contains(det.Linhas, l => l[0] == "Histórico" && l[1].Contains("Remarcado por Ana"));
    }

    [Fact]
    public async Task Adm_ve_atendimentos_so_como_numero()
    {
        var id = await AgendarAsync(Ana, "2026-11-10T15:00");
        var resp = Valor(await C(Adm).Listar("2026-11-09", "2026-11-15", default));
        Assert.DoesNotContain(resp.Itens, i => i.Id == id);
        Assert.Equal(1, resp.Contagens["2026-11-10"].Psicologia);
        Assert.Equal(404, Status(await C(Adm).Detalhe(id, default)));
    }

    [Fact]
    public async Task Juridico_sem_permissao_no_caso_nao_agenda_nem_ve_a_assistida_nas_origens()
    {
        var r = await C(Jur2).Agendar(new AgendaAgendamentoRequest { AssistidaId = _maria, Tipo = "atendimento", Inicio = "2026-11-12T10:00", DuracaoMinutos = 60 }, default);
        Assert.Equal(403, Status(r));
        Assert.DoesNotContain(Valor(await C(Jur2).Origens(default)), o => o.AssistidaId == _maria);
        Assert.Contains(Valor(await C(Jur1).Origens(default)), o => o.AssistidaId == _maria && o.Restrito);
    }

    [Fact]
    public async Task Compromisso_com_caso_que_ela_nao_acessa_mais_vira_sem_acesso()
    {
        var req = Reuniao("2026-11-12T10:00", "2026-11-12T11:00");
        req.AssistidaId = _maria;
        req.Titulo = "Discutir caso da Maria";
        var r = await C(Ana).CriarCompromisso(req, default);
        Assert.Equal(200, Status(r));
        var id = JsonDocument.Parse(Corpo(r)).RootElement.GetProperty("ids")[0].GetString()!;

        await TirarAcessoDaAnaAsync();

        var item = Assert.Single(await Lista(Ana), i => i.Id == id);
        Assert.Equal(AgendaService.NivelSemAcesso, item.Nivel);
        var det = Json(Valor(await C(Ana).Detalhe(id, default)));
        Assert.DoesNotContain("Maria", det);
        Assert.Contains("não acessa mais", det);
    }

    [Fact]
    public async Task Toda_a_equipe_nao_pode_com_assistida_vinculada()
    {
        var req = Reuniao("2026-11-12T10:00", "2026-11-12T11:00");
        req.AssistidaId = _maria;
        req.Visibilidade = AgendaVisibilidade.Equipe;
        Assert.Equal(400, Status(await C(Ana).CriarCompromisso(req, default)));
    }

    [Fact]
    public async Task Compartilhado_para_ver_nao_ocupa_a_agenda()
    {
        var req = Reuniao("2026-11-12T10:00", "2026-11-12T11:00");
        req.Visibilidade = AgendaVisibilidade.Pessoas;
        req.PodemVer = [Bruna.UserId];
        Assert.Equal(200, Status(await C(Ana).CriarCompromisso(req, default)));

        var item = Assert.Single(await Lista(Bruna), i => i.Titulo == "Reunião de equipe");
        Assert.Equal("compartilhados", item.Grupo);
        var conf = Valor(await C(Bruna).Conferir(new AgendaConferirRequest { Ocorrencias = [new AgendaOcorrenciaRequest { Inicio = "2026-11-12T10:00", Fim = "2026-11-12T11:00" }] }, default));
        Assert.DoesNotContain(conf[0].Conflitos, c => c.Nivel == AgendaService.Confirmar);
    }

    // ------------------------------------------------------------------ conflitos

    [Fact]
    public async Task Unidade_fechada_bloqueia_e_excecao_da_adm_vale_so_para_aquele_compromisso()
    {
        Assert.Equal(200, Status(await C(Adm).CriarDia(new AgendaDiaEspecialRequest { Data = "2026-11-27", Tipo = AgendaTipoDia.Outra, Funcionamento = AgendaFuncionamento.Fechada, Titulo = "Dedetização" }, default)));

        var r = await C(Ana).Agendar(new AgendaAgendamentoRequest { AssistidaId = _maria, Tipo = "sessao", Inicio = "2026-11-27T10:00", DuracaoMinutos = 50, Justificativa = "preciso", ExcecaoMotivo = "tentativa" }, default);
        Assert.Equal(409, Status(r));
        Assert.Contains("Casa fechada neste dia (Dedetização)", Corpo(r));

        var vistoria = Reuniao("2026-11-27T10:00", "2026-11-27T11:00", Rec.UserId);
        Assert.Equal(409, Status(await C(Adm).CriarCompromisso(vistoria, default)));
        vistoria.Ocorrencias[0].ExcecaoMotivo = "Vistoria do imóvel";
        Assert.Equal(200, Status(await C(Adm).CriarCompromisso(vistoria, default)));

        // continua fechada para as demais
        r = await C(Ana).Agendar(new AgendaAgendamentoRequest { AssistidaId = _maria, Tipo = "sessao", Inicio = "2026-11-27T14:00", DuracaoMinutos = 50, Justificativa = "x" }, default);
        Assert.Equal(409, Status(r));
        var cfg = Valor(await C(Adm).Configuracao(default));
        Assert.Contains(cfg.Excecoes, e => e.Contains("Vistoria do imóvel") && e.Contains("Elisa"));
    }

    [Fact]
    public async Task Feriado_e_so_aviso()
    {
        await C(Adm).CriarDia(new AgendaDiaEspecialRequest { Data = "2026-11-20", Tipo = "feriado", Titulo = "Consciência Negra" }, default);
        var conf = Valor(await C(Ana).Conferir(new AgendaConferirRequest { AssistidaId = _maria, Ocorrencias = [new AgendaOcorrenciaRequest { Inicio = "2026-11-20T10:00", Fim = "2026-11-20T10:50" }] }, default));
        Assert.Contains(conf[0].Conflitos, c => c.Nivel == AgendaService.Aviso && c.Mensagem.Contains("Feriado"));
        await AgendarAsync(Ana, "2026-11-20T10:00");
    }

    [Fact]
    public async Task Feriado_com_a_Casa_fechada_continua_identificado_como_feriado()
    {
        Assert.Equal(200, Status(await C(Adm).CriarDia(new AgendaDiaEspecialRequest { Data = "2026-11-12", Tipo = AgendaTipoDia.Feriado, Funcionamento = AgendaFuncionamento.Fechada, Titulo = "Feriado municipal" }, default)));
        var casa = (await Lista(Ana)).Single(i => i.Fonte == "casa");
        Assert.Equal("Feriado municipal", casa.Titulo);
        Assert.Equal("k-fec", casa.Cor);
        Assert.Contains("Casa fechada", casa.Tags);
        var r = await C(Ana).Agendar(new AgendaAgendamentoRequest { AssistidaId = _maria, Tipo = "sessao", Inicio = "2026-11-12T10:00", DuracaoMinutos = 50, Justificativa = "x" }, default);
        Assert.Equal(409, Status(r));
        Assert.True(Corpo(r).Contains("Casa fechada neste dia (Feriado: Feriado municipal)"), Corpo(r));
        var cfg = Valor(await C(Adm).Configuracao(default));
        var d = Assert.Single(cfg.Dias);
        Assert.Equal(AgendaTipoDia.Feriado, d.Tipo);
        Assert.Equal("Fechada", d.TextoFuncionamento);
    }

    [Fact]
    public async Task Horario_especial_so_aceita_dentro_do_intervalo()
    {
        Assert.Equal(200, Status(await C(Adm).CriarDia(new AgendaDiaEspecialRequest { Data = "2026-11-13", Tipo = AgendaTipoDia.Feriado, Funcionamento = AgendaFuncionamento.Especial, HorarioInicio = "08:00", HorarioFim = "12:00", Titulo = "Ponto facultativo" }, default)));
        var conf = Valor(await C(Ana).Conferir(new AgendaConferirRequest { AssistidaId = _maria, Ocorrencias = [new AgendaOcorrenciaRequest { Inicio = "2026-11-13T10:00", Fim = "2026-11-13T10:50" }] }, default));
        Assert.DoesNotContain(conf[0].Conflitos, c => c.Nivel == AgendaService.Bloqueio);
        Assert.Contains(conf[0].Conflitos, c => c.Nivel == AgendaService.Aviso && c.Mensagem.Contains("08:00") && c.Mensagem.Contains("12:00"));
        await AgendarAsync(Ana, "2026-11-13T10:00");

        foreach (var ini in new[] { "2026-11-13T13:00", "2026-11-13T11:30", "2026-11-13T07:30" })
        {
            var r = await C(Ana).Agendar(new AgendaAgendamentoRequest { AssistidaId = _maria, Tipo = "sessao", Inicio = ini, DuracaoMinutos = 50, Justificativa = "x" }, default);
            Assert.Equal(409, Status(r));
            Assert.Contains("a Casa funciona só das 08:00 às 12:00", Corpo(r));
        }
        // dia inteiro também não cabe no horário especial
        var aus = Reuniao("2026-11-13T00:00", "2026-11-13T00:00");
        aus.DiaInteiro = true; aus.Ocorrencias = [new AgendaOcorrenciaRequest { Inicio = "2026-11-13" }];
        Assert.Equal(409, Status(await C(Bruna).CriarCompromisso(aus, default)));
    }

    [Fact]
    public async Task Dia_da_Casa_valida_o_pedido_e_so_a_adm_cadastra()
    {
        Assert.Equal(403, Status(await C(Ana).CriarDia(new AgendaDiaEspecialRequest { Data = "2026-11-13", Tipo = AgendaTipoDia.Feriado, Titulo = "X" }, default)));
        Assert.Equal(403, Status(await C(Ana).PreviaDia(new AgendaDiaEspecialRequest { Data = "2026-11-13", Tipo = AgendaTipoDia.Feriado, Titulo = "X" }, default)));
        Assert.Equal(400, Status(await C(Adm).CriarDia(new AgendaDiaEspecialRequest { Data = "2026-11-13", Tipo = "fechada", Titulo = "X" }, default)));
        Assert.Equal(400, Status(await C(Adm).CriarDia(new AgendaDiaEspecialRequest { Data = "2026-11-13", Tipo = AgendaTipoDia.Feriado, Funcionamento = AgendaFuncionamento.Especial, Titulo = "X" }, default)));
        Assert.Equal(400, Status(await C(Adm).CriarDia(new AgendaDiaEspecialRequest { Data = "2026-11-13", Tipo = AgendaTipoDia.Feriado, Funcionamento = AgendaFuncionamento.Especial, HorarioInicio = "12:00", HorarioFim = "08:00", Titulo = "X" }, default)));
        Assert.Equal(400, Status(await C(Adm).CriarDia(new AgendaDiaEspecialRequest { Data = "2026-11-13", Tipo = AgendaTipoDia.Outra, Funcionamento = AgendaFuncionamento.Normal, Titulo = "X" }, default)));
        // feriado com funcionamento normal: só identifica a data
        Assert.Equal(200, Status(await C(Adm).CriarDia(new AgendaDiaEspecialRequest { Data = "2026-11-13", Tipo = AgendaTipoDia.Feriado, Funcionamento = AgendaFuncionamento.Normal, Titulo = "X" }, default)));
    }

    [Fact]
    public async Task Fechar_data_com_compromissos_mostra_os_afetados_sem_cancelar_e_avisa_cada_responsavel()
    {
        // terça 24/11: sessão da Ana às 10h, atendimento jurídico da Carla às 14h e aula de Informática às 18h
        var sessao = await AgendarAsync(Ana, "2026-11-24T10:00");
        var juridico = await AgendarAsync(Jur1, "2026-11-24T14:00", tipo: "atendimento");
        var fechar = new AgendaDiaEspecialRequest { Data = "2026-11-24", Tipo = AgendaTipoDia.Outra, Funcionamento = AgendaFuncionamento.Fechada, Titulo = "Manutenção elétrica" };

        var previa = Valor(await C(Adm).PreviaDia(fechar, default));
        Assert.Equal(3, previa.Total);
        Assert.Equal(1, previa.Atendimentos["Psicologia"]);
        Assert.Equal(1, previa.Atendimentos["Jurídico"]);
        Assert.Equal(1, previa.Aulas);
        var jsonPrevia = Json(previa);
        Assert.DoesNotContain("Maria", jsonPrevia);
        Assert.DoesNotContain("Sessão", jsonPrevia);

        var criado = await C(Adm).CriarDia(fechar, default);
        Assert.Equal(200, Status(criado));
        Assert.Contains("\"Total\":3", Corpo(criado));
        Assert.DoesNotContain("Maria", Corpo(criado));

        // nada foi cancelado sozinho
        Assert.Equal(AgendaSituacao.Agendado, (await _db.AgendaAgendamentos.AsNoTracking().SingleAsync(a => a.Id == Num(sessao))).Situacao);
        Assert.Equal(AgendaSituacao.Agendado, (await _db.AgendaAgendamentos.AsNoTracking().SingleAsync(a => a.Id == Num(juridico))).Situacao);

        // cada responsável recebe a sua pendência, e só a sua
        var pAna = Valor(await C(Ana).Pendencias(default)).Single(p => p.Tipo == "foraFuncionamento");
        Assert.Equal(sessao, pAna.Id);
        Assert.Contains("Casa fechada neste dia (Manutenção elétrica)", pAna.Texto);
        Assert.Contains("remarcar", pAna.Acoes);
        Assert.Contains("cancelarAgendamento", pAna.Acoes);
        Assert.Single(Valor(await C(Jur1).Pendencias(default)), p => p.Tipo == "foraFuncionamento" && p.Id == juridico);
        Assert.DoesNotContain(Valor(await C(Bruna).Pendencias(default)), p => p.Tipo == "foraFuncionamento");
        Assert.Contains(Valor(await C(Prof1).Pendencias(default)), p => p.Tipo == "fechada" && p.Texto.Contains("Informática"));
        Assert.DoesNotContain(Valor(await C(Prof2).Pendencias(default)), p => p.Tipo == "fechada");

        // a ADM vê quantos faltam tratar, sem nomes
        var dia = Valor(await C(Adm).Configuracao(default)).Dias.Single();
        Assert.Equal(3, dia.Afetados);
        Assert.DoesNotContain("Maria", dia.AfetadosTexto);

        // a ADM muda para horário especial 8h–12h: a sessão das 10h deixa de ser afetada
        var editar = new AgendaDiaEspecialRequest { Data = "2026-11-24", Tipo = AgendaTipoDia.Outra, Funcionamento = AgendaFuncionamento.Especial, HorarioInicio = "08:00", HorarioFim = "12:00", Titulo = "Manutenção elétrica (tarde)" };
        Assert.Equal(200, Status(await C(Adm).EditarDia(dia.Id, editar, default)));
        Assert.DoesNotContain(Valor(await C(Ana).Pendencias(default)), p => p.Tipo == "foraFuncionamento");
        Assert.Contains(Valor(await C(Jur1).Pendencias(default)), p => p.Tipo == "foraFuncionamento" && p.Texto.Contains("08:00 às 12:00"));
        Assert.Equal(2, Valor(await C(Adm).Configuracao(default)).Dias.Single().Afetados);

        // voltar ao normal para todos resolve o resto
        Assert.Equal(200, Status(await C(Adm).Reabrir(dia.Id, new AgendaMotivoRequest { Motivo = "Manutenção adiada" }, default)));
        Assert.DoesNotContain(Valor(await C(Jur1).Pendencias(default)), p => p.Tipo == "foraFuncionamento");
        Assert.Equal(0, Valor(await C(Adm).Configuracao(default)).Dias.Single().Afetados);
        Assert.Contains(_auditoria, a => a.Contains("AGENDA_DIA_EDITADO"));
    }

    [Fact]
    public async Task Ausencia_de_dia_inteiro_confirmada_ocupa_o_dia()
    {
        var aus = new AgendaCompromissoRequest
        {
            Tipo = AgendaTipoManual.Ausencia, Titulo = "Folga", DiaInteiro = true, Visibilidade = AgendaVisibilidade.Equipe,
            Ocorrencias = [new AgendaOcorrenciaRequest { Inicio = "2026-11-13" }]
        };
        Assert.Equal(200, Status(await C(Ana).CriarCompromisso(aus, default)));
        var r = await C(Bruna).CriarCompromisso(Reuniao("2026-11-13T16:00", "2026-11-13T16:30", Ana.UserId), default);
        Assert.Equal(409, Status(r));
        Assert.Contains("Ana está ausente o dia inteiro (ausência confirmada)", Corpo(r));
    }

    [Fact]
    public async Task Ausencia_informada_por_colega_e_so_aviso_ate_ser_confirmada()
    {
        var aus = new AgendaCompromissoRequest
        {
            Tipo = AgendaTipoManual.Ausencia, DonoId = Ana.UserId, Titulo = "Consulta médica", Visibilidade = AgendaVisibilidade.Equipe,
            Ocorrencias = [new AgendaOcorrenciaRequest { Inicio = "2026-11-11T09:00", Fim = "2026-11-11T12:00" }]
        };
        var criada = await C(Bruna).CriarCompromisso(aus, default);
        Assert.Equal(200, Status(criada));
        var id = JsonDocument.Parse(Corpo(criada)).RootElement.GetProperty("ids")[0].GetString()!;

        // só aviso: o horário continua selecionável e o agendamento passa sem justificativa
        var faixas = Valor(await C(Ana).Disponibilidade(new AgendaDisponibilidadeRequest { De = "2026-11-11", Ate = "2026-11-11", AssistidaId = _maria }, default));
        Assert.Contains(faixas, f => f.Aviso && f.Cor == "k-aus");
        await AgendarAsync(Ana, "2026-11-11T10:00");

        Assert.Contains(Valor(await C(Ana).Pendencias(default)), p => p.Tipo == "ausencia" && p.Id == id);
        Assert.Equal(403, Status(await C(Jur1).DecidirAusencia(Num(id), "confirmar", default)));
        Assert.Equal(200, Status(await C(Ana).DecidirAusencia(Num(id), "confirmar", default)));

        var r = await C(Ana).Agendar(new AgendaAgendamentoRequest { AssistidaId = _maria, Tipo = "sessao", Inicio = "2026-11-11T11:00", DuracaoMinutos = 50 }, default);
        Assert.Equal(409, Status(r));
        Assert.Contains("ausência confirmada", Corpo(r));
    }

    [Fact]
    public async Task Recorrencia_e_conferida_data_a_data()
    {
        await AgendarAsync(Ana, "2026-11-19T10:00");
        var req = Reuniao("2026-11-12T10:00", "2026-11-12T11:00");
        req.Ocorrencias.Add(new AgendaOcorrenciaRequest { Inicio = "2026-11-19T10:00", Fim = "2026-11-19T11:00" });
        req.Ocorrencias.Add(new AgendaOcorrenciaRequest { Inicio = "2026-11-26T10:00", Fim = "2026-11-26T11:00" });

        var r = await C(Ana).CriarCompromisso(req, default);
        Assert.Equal(409, Status(r));
        var probs = JsonDocument.Parse(Corpo(r)).RootElement.GetProperty("ocorrencias");
        Assert.Equal(1, probs.GetArrayLength());
        Assert.Equal("2026-11-19T10:00:00", probs[0].GetProperty("Inicio").GetString());

        req.Ocorrencias.RemoveAt(1);
        Assert.Equal(200, Status(await C(Ana).CriarCompromisso(req, default)));
        Assert.Equal(2, await _db.AgendaCompromissos.CountAsync(c => c.SerieId != null));
    }

    [Fact]
    public async Task Aula_da_assistida_aparece_pelo_nome_so_para_quem_tem_permissao_no_caso()
    {
        var conf = Valor(await C(Jur1).Conferir(new AgendaConferirRequest { AssistidaId = _maria, Ocorrencias = [new AgendaOcorrenciaRequest { Inicio = "2026-11-12T18:30", Fim = "2026-11-12T19:00" }] }, default));
        // quem tem permissão no caso já vê curso e situação da assistida (regra dos relatórios)
        Assert.Contains(conf[0].Conflitos, c => c.Mensagem == "A assistida tem aula de Informática das 18:00 às 20:00.");
        // sem permissão no caso, nem dá para conferir com a assistida
        Assert.Equal(403, Status(await C(Jur2).Conferir(new AgendaConferirRequest { AssistidaId = _maria, Ocorrencias = [new AgendaOcorrenciaRequest { Inicio = "2026-11-12T18:30", Fim = "2026-11-12T19:00" }] }, default)));
    }

    // ------------------------------------------------------------------ aulas

    [Fact]
    public async Task Professora_ve_a_sua_turma_e_so_dia_hora_e_sala_das_outras()
    {
        var info = (await Lista(Prof1)).Where(i => i.Fonte == "aula").ToList();
        Assert.Contains(info, i => i.Nivel == AgendaService.NivelDetalhe && i.Titulo == "Aula: Informática");
        var cul = Assert.Single(info, i => i.Titulo == "Aula: Culinária");
        Assert.Equal(AgendaService.NivelCurso, cul.Nivel);
        var det = Json(Valor(await C(Prof1).Detalhe(cul.Id, default)));
        Assert.DoesNotContain("Alunas", det);
    }

    [Fact]
    public async Task Cancelar_aulas_seguintes_nao_encerra_a_turma_e_respeita_chamada()
    {
        _db.Chamadas.Add(new Chamada { CursoId = _cursoInfo, Data = new(2026, 11, 10) });
        await _db.SaveChangesAsync();

        Assert.Equal(403, Status(await C(Prof2).CancelarAula(new AgendaAulaCancelarRequest { CursoId = _cursoInfo, Data = "2026-11-12", Escopo = "seguintes", Motivo = "x" }, default)));
        Assert.Equal(400, Status(await C(Prof1).CancelarAula(new AgendaAulaCancelarRequest { CursoId = _cursoInfo, Data = "2026-11-10", Escopo = "esta", Motivo = "x" }, default)));
        Assert.Equal(200, Status(await C(Prof1).CancelarAula(new AgendaAulaCancelarRequest { CursoId = _cursoInfo, Data = "2026-11-12", Escopo = "seguintes", Motivo = "Reforma da sala" }, default)));

        var aulas = (await Lista(Prof1, "2026-11-09", "2026-11-20")).Where(i => i.Titulo == "Aula: Informática").ToList();
        Assert.False(aulas.Single(a => a.Inicio.StartsWith("2026-11-10")).Cancelado);
        Assert.True(aulas.Where(a => string.CompareOrdinal(a.Inicio, "2026-11-12") > 0).All(a => a.Cancelado));
        Assert.True((await _db.Cursos.SingleAsync(c => c.Id == _cursoInfo)).Ativo);

        Assert.Equal(200, Status(await C(Prof1).DesfazerCancelamentoAula(new AgendaAulaCancelarRequest { CursoId = _cursoInfo, Data = "2026-11-12" }, default)));
        Assert.Equal(1, await _db.AulasCancelamentos.CountAsync(c => c.DesfeitoEm != null));
    }

    [Fact]
    public async Task Comunicacao_de_falta_aparece_para_a_professora_e_nao_marca_chamada()
    {
        var aulaId = $"aula:{_cursoInfo}:2026-11-12:" + (await _db.CursosHorarios.SingleAsync(h => h.CursoId == _cursoInfo && h.DiaSemana == 4)).Id;
        var opcoes = Valor(await C(Rec).OpcoesComunicacao(default));
        Assert.Contains(opcoes.Single(o => o.AssistidaId == _maria).Aulas, a => a[0] == aulaId);

        Assert.Equal(403, Status(await C(Prof2).Comunicar(new AgendaComunicacaoRequest { AssistidaId = _maria, AulaId = aulaId }, default)));
        Assert.Equal(200, Status(await C(Rec).Comunicar(new AgendaComunicacaoRequest { AssistidaId = _maria, AulaId = aulaId, Comunicacao = "falta" }, default)));

        var det = Valor(await C(Prof1).Detalhe(aulaId, default));
        Assert.Contains(det.Linhas, l => l[0] == "Comunicações" && l[1].Contains("Maria Ficticia") && l[1].Contains("vai faltar"));
        Assert.Equal(0, await _db.Chamadas.CountAsync());
    }

    // ------------------------------------------------------------------ pendências e permissões atuais

    [Fact]
    public async Task Pendencia_some_e_acao_e_recusada_quando_perde_acesso_a_origem()
    {
        _db.AgendaAgendamentos.Add(new AgendaAgendamento
        {
            Area = AgendaArea.Psicologia, AssistidaId = _maria, Tipo = "sessao", ResponsavelUserId = Ana.UserId,
            InicioUtc = Agora.AddHours(-3), FimUtc = Agora.AddHours(-2), CriadoPorUserId = Ana.UserId, CriadoPorIdentificador = Ana.Identificador
        });
        await _db.SaveChangesAsync();
        var id = (await _db.AgendaAgendamentos.SingleAsync()).Id;
        Assert.Contains(Valor(await C(Ana).Pendencias(default)), p => p.Tipo == "resultado");

        await TirarAcessoDaAnaAsync();

        Assert.DoesNotContain(Valor(await C(Ana).Pendencias(default)), p => p.Tipo == "resultado");
        Assert.Equal(403, Status(await C(Ana).Resultado(id, new AgendaResultadoRequest { Situacao = AgendaSituacao.Realizado }, default)));
        var item = Assert.Single(await Lista(Ana), i => i.Id == "ag:" + id);
        Assert.Equal(AgendaService.NivelSemAcesso, item.Nivel);
    }

    [Fact]
    public async Task Reavaliacao_resolve_a_pendencia_e_so_abre_com_acesso_atual()
    {
        var p = await _db.ProntuariosPsicologicos.SingleAsync();
        p.RiscoAtual = "alto";
        p.RiscoAtualizadoEm = Agora.AddDays(-2);
        await _db.SaveChangesAsync();

        Assert.Contains(Valor(await C(Ana).Pendencias(default)), x => x.Tipo == "reavaliacao");
        Assert.Equal(403, Status(await C(Bruna).Reavaliar(new ReavaliacaoRiscoRequest { AssistidaId = _maria, Classificacao = "alto", ProximaAcao = "x" }, default)));
        Assert.Equal(200, Status(await C(Ana).Reavaliar(new ReavaliacaoRiscoRequest { AssistidaId = _maria, Classificacao = "alto", ProximaAcao = "Sessão extra na sexta" }, default)));
        Assert.DoesNotContain(Valor(await C(Ana).Pendencias(default)), x => x.Tipo == "reavaliacao");
        Assert.Equal("alto", (await _db.ProntuariosPsicologicos.AsNoTracking().SingleAsync()).RiscoAtual);
        Assert.Equal("Sessão extra na sexta", Valor(await C(Ana).Reavaliacoes(_maria, default)).Single().ProximaAcao);

        await TirarAcessoDaAnaAsync();
        Assert.Equal(403, Status(await C(Ana).Reavaliacoes(_maria, default)));
    }

    [Fact]
    public async Task Sucessora_revisa_e_assume_com_conferencia_da_agenda_dela()
    {
        var id = Num(await AgendarAsync(Ana, "2026-11-12T15:00"));
        // simula "Passar responsabilidades" Ana -> Bruna
        await TirarAcessoDaAnaAsync();
        _db.AgendaRevisoes.Add(new AgendaRevisao { AgendaAgendamentoId = id, DeUserId = Ana.UserId, ParaUserId = Bruna.UserId });
        await _db.SaveChangesAsync();
        await C(Bruna).CriarCompromisso(Reuniao("2026-11-12T15:00", "2026-11-12T15:30"), default);

        Assert.Contains(Valor(await C(Bruna).Pendencias(default)), p => p.Tipo == "revisar" && p.Id == "ag:" + id);
        Assert.Equal(409, Status(await C(Bruna).Assumir(id, new AgendaJustificativaRequest(), default)));
        Assert.Equal(200, Status(await C(Bruna).Assumir(id, new AgendaJustificativaRequest { Justificativa = "Encaixo a reunião depois" }, default)));
        Assert.Equal(Bruna.UserId, (await _db.AgendaAgendamentos.AsNoTracking().SingleAsync(a => a.Id == id)).ResponsavelUserId);
        Assert.DoesNotContain(Valor(await C(Bruna).Pendencias(default)), p => p.Tipo == "revisar");
    }

    [Fact]
    public async Task Configuracao_e_horarios_so_pela_adm()
    {
        Assert.Equal(403, Status(await C(Ana).Configuracao(default)));
        Assert.Equal(403, Status(await C(Prof1).SalvarHorarios(_cursoInfo, [], default)));
        Assert.Equal(200, Status(await C(Adm).SalvarConfiguracao(new AgendaConfiguracaoRequest { LimiteCompromissosDia = 2, LimiteHorasDia = 6 }, default)));
        await AgendarAsync(Ana, "2026-11-16T09:00");
        await AgendarAsync(Ana, "2026-11-16T10:00");
        var conf = Valor(await C(Ana).Conferir(new AgendaConferirRequest { AssistidaId = _maria, Ocorrencias = [new AgendaOcorrenciaRequest { Inicio = "2026-11-16T14:00", Fim = "2026-11-16T14:50" }] }, default));
        Assert.Contains(conf[0].Conflitos, c => c.Nivel == AgendaService.Aviso && c.Mensagem.Contains("3 compromissos"));
    }
}
