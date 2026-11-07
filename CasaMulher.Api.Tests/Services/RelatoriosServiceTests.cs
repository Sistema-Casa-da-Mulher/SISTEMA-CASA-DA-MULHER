using System.Text.Json;
using CasaMulher.Api.Data;
using CasaMulher.Api.Models;
using CasaMulher.Api.Security;
using CasaMulher.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CasaMulher.Api.Tests.Services;

/// <summary>
/// Regras de sigilo dos relatórios: cada perfil só recebe o que já pode ver nos módulos.
/// Os testes procuram no JSON inteiro da resposta, para pegar qualquer vazamento.
/// </summary>
public sealed class RelatoriosServiceTests : IAsyncLifetime
{
    private const string Relato = "RELATO-SECRETO-123";
    private const string Evolucao = "EVOLUCAO-SECRETA-456";
    private const string NomeComum = "Ana Ficticia";
    private const string NomeRestrita = "Bia Restrita";
    private const string NumeroProntuario = "PSI-2026-000001";

    private static readonly UsuarioAtuante Adm = new("u-adm", PerfisAcesso.Adm, "ADM-000001", "Coordenação", null);
    private static readonly UsuarioAtuante Recepcao = new("u-rec", PerfisAcesso.Recepcao, "REC-000001", "Recepcionista", null);
    private static readonly UsuarioAtuante JurResponsavel = new("u-jur1", PerfisAcesso.Juridico, "JUR-000001", "Advogada Responsável", null);
    private static readonly UsuarioAtuante JurOutra = new("u-jur2", PerfisAcesso.Juridico, "JUR-000002", "Advogada Outra", null);
    private static readonly UsuarioAtuante PsiResponsavel = new("u-psi1", PerfisAcesso.Psicologia, "PSI-000001", "Psicóloga Responsável", null);
    private static readonly UsuarioAtuante PsiOutra = new("u-psi2", PerfisAcesso.Psicologia, "PSI-000002", "Psicóloga Outra", null);
    private static readonly UsuarioAtuante Professora = new("u-prof", PerfisAcesso.Professor, "PRO-000001", "Professora", null);

    private SqliteConnection _connection = null!;
    private AppDbContext _db = null!;
    private int _comumId;
    private int _restritaId;
    private int _atendimentoId;
    private int _sessaoId;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        await _db.Database.EnsureCreatedAsync();

        foreach (var u in new[] { Adm, Recepcao, JurResponsavel, JurOutra, PsiResponsavel, PsiOutra, Professora })
        {
            _db.Users.Add(new ApplicationUser { Id = u.UserId, UserName = u.Identificador, NomeCompleto = u.Nome, Perfil = u.Perfil, IdentificadorFuncionario = u.Identificador, Ativo = true });
        }

        var agora = DateTime.UtcNow;
        var curso = new Curso { Nome = "Culinária e Autonomia", Vagas = 10 };
        _db.Cursos.Add(curso);
        await _db.SaveChangesAsync();
        _db.ProfessoresCursos.Add(new ProfessorCurso { UserId = Professora.UserId, CursoId = curso.Id });

        var comum = new Assistida
        {
            Nome = NomeComum, Cpf = "52998224725", Telefone = "11999998888", Origem = AssistidaOrigem.Recepcao,
            CadastradaPorUserId = Recepcao.UserId, CadastradaPorIdentificador = Recepcao.Identificador, CadastradaPorNome = Recepcao.Nome,
            EncaminhadaJuridicoEm = agora.AddDays(-20), EncaminhadaPsicologiaEm = agora.AddDays(-20),
            CriadoEm = agora.AddDays(-20), AtualizadoEm = agora
        };
        comum.Cursos.Add(new AssistidaCurso
        {
            CursoId = curso.Id, CriadoEm = agora.AddDays(-20), Situacao = AssistidaCursoSituacao.Interrompido,
            TerminoEm = DateOnly.FromDateTime(agora.AddDays(-2)), MotivoSaida = "Saúde"
        });
        var restrita = new Assistida
        {
            Nome = NomeRestrita, Cpf = "11144477735", Origem = AssistidaOrigem.Juridico, AcessoRestrito = true, MotivoRestricao = AssistidaMotivoRestricao.CasaAbrigo,
            CadastradaPorUserId = JurResponsavel.UserId, CadastradaPorIdentificador = JurResponsavel.Identificador, CadastradaPorNome = JurResponsavel.Nome,
            EncaminhadaJuridicoEm = agora.AddDays(-5), CriadoEm = agora.AddDays(-5), AtualizadoEm = agora
        };
        _db.Assistidas.AddRange(comum, restrita);
        await _db.SaveChangesAsync();
        _comumId = comum.Id;
        _restritaId = restrita.Id;

        var caso = new CasoJuridico { AssistidaId = comum.Id, Visibilidade = CasoJuridicoVisibilidade.Responsaveis, CriadoEm = agora.AddDays(-10) };
        caso.Responsaveis.Add(new CasoJuridicoResponsavel { UserId = JurResponsavel.UserId, Identificador = JurResponsavel.Identificador, Nome = JurResponsavel.Nome, AtribuidoPorIdentificador = JurResponsavel.Identificador });
        var atendimento = new AtendimentoJuridico
        {
            Caso = caso, Protocolo = "CJ-2026-000001", Ano = 2026, Sequencial = 1, ProfissionalUserId = JurResponsavel.UserId,
            ProfissionalIdentificador = JurResponsavel.Identificador, ProfissionalNome = JurResponsavel.Nome, TipoDemanda = "guarda",
            TemMedidaProtetiva = true, Relato = Relato, CriadoEm = agora.AddDays(-10)
        };
        _db.CasosJuridicos.Add(caso);
        _db.AtendimentosJuridicos.Add(atendimento);

        var pront = new ProntuarioPsicologico { AssistidaId = comum.Id, Numero = NumeroProntuario, Ano = 2026, Sequencial = 1, RiscoAtual = "alto", CriadoEm = agora.AddDays(-9) };
        pront.Responsaveis.Add(new ProntuarioPsicologicoResponsavel { UserId = PsiResponsavel.UserId, Identificador = PsiResponsavel.Identificador, Nome = PsiResponsavel.Nome, AtribuidoPorIdentificador = PsiResponsavel.Identificador });
        var sessao = new SessaoPsicologica
        {
            Prontuario = pront, Numero = 1, ProfissionalUserId = PsiResponsavel.UserId, ProfissionalIdentificador = PsiResponsavel.Identificador,
            ProfissionalNome = PsiResponsavel.Nome, TipoAtendimento = "acolhimento", Presenca = "compareceu", Risco = "alto", Evolucao = Evolucao, CriadoEm = agora.AddDays(-9)
        };
        _db.ProntuariosPsicologicos.Add(pront);
        _db.SessoesPsicologicas.Add(sessao);
        await _db.SaveChangesAsync();
        _atendimentoId = atendimento.Id;
        _sessaoId = sessao.Id;
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private RelatoriosService Servico() => new(_db);

    private static readonly JsonSerializerOptions JsonOpcoes = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private static string Json(object o) => JsonSerializer.Serialize(o, JsonOpcoes);

    [Fact]
    public async Task RecepcaoNaoRecebeNadaDaPsicologiaNemCasosRestritos()
    {
        var s = Servico();
        var lista = await s.ListarAssistidasAsync(Recepcao, PerfisAcesso.Recepcao, default);
        Assert.DoesNotContain(lista, a => a.Id == _restritaId);

        var geral = Json(await s.GerarGeralAsync(Recepcao, PerfisAcesso.Recepcao, "geral", null, null, new CriteriosAtencao(), default));
        var individual = Json((await s.GerarIndividualAsync(Recepcao, PerfisAcesso.Recepcao, _comumId, "geral", null, default))!);

        foreach (var json in new[] { geral, individual })
        {
            Assert.DoesNotContain(NomeRestrita, json);
            Assert.DoesNotContain(NumeroProntuario, json);
            Assert.DoesNotContain("Encaminhada à Psicologia", json);
            Assert.DoesNotContain("Risco", json);
            Assert.DoesNotContain(Evolucao, json);
            Assert.DoesNotContain(Relato, json);
            Assert.DoesNotContain("Saúde", json);
        }

        Assert.Null(await s.GerarIndividualAsync(Recepcao, PerfisAcesso.Recepcao, _restritaId, "geral", null, default));
        Assert.Null(await s.ConteudoSigilosoAsync(Recepcao, PerfisAcesso.Recepcao, _comumId, [RelatoriosService.PrefixoJuridico + _atendimentoId], default));
    }

    [Fact]
    public async Task RecepcaoVeSituacaoOperacionalDoJuridicoSemMotivoDeCancelamento()
    {
        var restrita = await _db.Assistidas.SingleAsync(a => a.Id == _restritaId);
        var nova = new Assistida
        {
            Nome = "Cida Cancelada", Cpf = "39053344705", Origem = AssistidaOrigem.Recepcao, CadastradaPorUserId = Recepcao.UserId,
            CadastradaPorIdentificador = Recepcao.Identificador, CadastradaPorNome = Recepcao.Nome,
            EncaminhadaJuridicoEm = DateTime.UtcNow.AddDays(-3), JuridicoEncaminhamentoCanceladoEm = DateTime.UtcNow.AddDays(-1),
            JuridicoMotivoCancelamento = "MOTIVO-INTERNO-789", CriadoEm = DateTime.UtcNow.AddDays(-3)
        };
        _db.Assistidas.Add(nova);
        await _db.SaveChangesAsync();

        var s = Servico();
        var json = Json((await s.GerarIndividualAsync(Recepcao, PerfisAcesso.Recepcao, nova.Id, "geral", null, default))!);
        Assert.Contains("Cancelado em", json);
        Assert.DoesNotContain("MOTIVO-INTERNO-789", json);

        var comum = Json((await s.GerarIndividualAsync(Recepcao, PerfisAcesso.Recepcao, _comumId, "geral", null, default))!);
        Assert.Contains("Primeiro atendimento realizado em", comum);
        Assert.DoesNotContain("Guarda", comum);
    }

    [Fact]
    public async Task JuridicoNaoResponsavelNaoRecebeRelatoDeCasoSomenteResponsaveis()
    {
        var s = Servico();
        var outra = (await s.GerarIndividualAsync(JurOutra, PerfisAcesso.Juridico, _comumId, "geral", null, default))!;
        var json = Json(outra);

        Assert.DoesNotContain(Relato, json);
        Assert.DoesNotContain("Guarda", json);
        Assert.False(outra.PodeIncluirSigiloso);
        Assert.Empty(outra.RegistrosSigilosos);
        Assert.False(outra.PodeDocumentoExterno);
        Assert.Contains("CJ-2026-000001", json);
        Assert.Null(await s.ConteudoSigilosoAsync(JurOutra, PerfisAcesso.Juridico, _comumId, [RelatoriosService.PrefixoJuridico + _atendimentoId], default));
        Assert.Null(await s.DocumentoExternoAsync(JurOutra, PerfisAcesso.Juridico, _comumId, "geral", null, default));

        // No geral, a demanda dos casos que ela não vê não entra nas contagens.
        var geral = Json(await s.GerarGeralAsync(JurOutra, PerfisAcesso.Juridico, "geral", null, null, new CriteriosAtencao(), default));
        Assert.DoesNotContain("Guarda", geral);
        Assert.DoesNotContain(Relato, geral);
        // O Jurídico nunca recebe o conteúdo da Psicologia, só o alerta de risco.
        Assert.DoesNotContain(Evolucao, Json(outra));
        Assert.DoesNotContain(NumeroProntuario, Json(outra));
    }

    [Fact]
    public async Task SoAResponsavelDoJuridicoRecebeORelatoEscolhido()
    {
        var s = Servico();
        var resp = (await s.GerarIndividualAsync(JurResponsavel, PerfisAcesso.Juridico, _comumId, "geral", null, default))!;
        Assert.True(resp.PodeIncluirSigiloso);
        Assert.Contains("Guarda", Json(resp));
        Assert.DoesNotContain(Relato, Json(resp));

        var conteudo = await s.ConteudoSigilosoAsync(JurResponsavel, PerfisAcesso.Juridico, _comumId, [RelatoriosService.PrefixoJuridico + _atendimentoId], default);
        Assert.NotNull(conteudo);
        Assert.Contains(Relato, Json(conteudo!));

        // Chave de registro de outro módulo ou inexistente é recusada.
        Assert.Null(await s.ConteudoSigilosoAsync(JurResponsavel, PerfisAcesso.Juridico, _comumId, [RelatoriosService.PrefixoPsicologia + _sessaoId], default));
        Assert.Null(await s.ConteudoSigilosoAsync(JurResponsavel, PerfisAcesso.Juridico, _comumId, [RelatoriosService.PrefixoJuridico + "99999"], default));
    }

    [Fact]
    public async Task ProntuarioSoDaPsicologaResponsavel()
    {
        var s = Servico();
        var outra = (await s.GerarIndividualAsync(PsiOutra, PerfisAcesso.Psicologia, _comumId, "geral", null, default))!;
        Assert.DoesNotContain(Evolucao, Json(outra));
        Assert.False(outra.PodeIncluirSigiloso);
        Assert.Null(await s.ConteudoSigilosoAsync(PsiOutra, PerfisAcesso.Psicologia, _comumId, [RelatoriosService.PrefixoPsicologia + _sessaoId], default));
        Assert.Null(await s.DocumentoExternoAsync(PsiOutra, PerfisAcesso.Psicologia, _comumId, "geral", null, default));

        var responsavel = Json((await s.GerarIndividualAsync(PsiResponsavel, PerfisAcesso.Psicologia, _comumId, "geral", null, default))!);
        Assert.Contains(NumeroProntuario, responsavel);
        Assert.DoesNotContain(Evolucao, responsavel);

        var conteudo = await s.ConteudoSigilosoAsync(PsiResponsavel, PerfisAcesso.Psicologia, _comumId, [RelatoriosService.PrefixoPsicologia + _sessaoId], default);
        Assert.Contains(Evolucao, Json(conteudo!));
        Assert.NotNull(await s.DocumentoExternoAsync(PsiResponsavel, PerfisAcesso.Psicologia, _comumId, "geral", null, default));
    }

    [Fact]
    public async Task CoordenacaoNaoRecebeNomesNosPontosDeAtencaoNemNadaDaPsicologiaPorAssistida()
    {
        var s = Servico();
        var geral = await s.GerarGeralAsync(Adm, PerfisAcesso.Adm, "geral", null, null, new CriteriosAtencao(PrazoFila: 1, FaltasSeguidas: 1, DiasRetornoVencido: 1, OcupacaoBaixa: 100), default);
        var pontos = Json(geral.Avisos);
        Assert.NotEmpty(geral.Avisos.SelectMany(a => a.Pontos));
        Assert.True(geral.Avisos.SelectMany(a => a.Pontos).Count() <= 5);

        var tudo = Json(geral);
        foreach (var nome in new[] { NomeComum, NomeRestrita })
        {
            Assert.DoesNotContain(nome, pontos);
            Assert.DoesNotContain(nome, tudo);
        }

        Assert.DoesNotContain(Relato, tudo);
        Assert.DoesNotContain(Evolucao, tudo);
        Assert.DoesNotContain("Iminente", tudo);

        var individual = Json((await s.GerarIndividualAsync(Adm, PerfisAcesso.Adm, _comumId, "geral", null, default))!);
        Assert.DoesNotContain(NumeroProntuario, individual);
        Assert.DoesNotContain(Evolucao, individual);
        Assert.DoesNotContain(Relato, individual);
        Assert.DoesNotContain("Encaminhada à Psicologia", individual);
        Assert.DoesNotContain("Saúde", individual);
        Assert.DoesNotContain("Risco alto", individual);
    }

    [Fact]
    public async Task MotivoDaSaidaDoCursoSoParaAProfessoraECoordenacaoSoContagem()
    {
        var s = Servico();
        var prof = Json((await s.GerarIndividualAsync(Professora, PerfisAcesso.Professor, _comumId, "geral", null, default))!);
        Assert.Contains("Saúde", prof);
        Assert.DoesNotContain(Relato, prof);
        Assert.DoesNotContain(NumeroProntuario, prof);
        Assert.DoesNotContain("CJ-2026", prof);

        var jur = Json((await s.GerarIndividualAsync(JurResponsavel, PerfisAcesso.Juridico, _comumId, "geral", null, default))!);
        Assert.Contains("Interrompido", jur);
        Assert.DoesNotContain("Saúde", jur);

        // Coordenação: contagem por motivo (categoria), sem nome da aluna.
        var adm = await s.GerarGeralAsync(Adm, PerfisAcesso.Adm, "geral", null, null, new CriteriosAtencao(), default);
        var motivos = adm.Secoes.SelectMany(x => x.Blocos).Single(b => b.Titulo == "Motivos de interrupção");
        Assert.Contains(motivos.Itens, i => i.Rotulo == "Saúde" && i.Valor == 1);

        // A professora não recebe alunas com acesso restrito.
        var lista = await s.ListarAssistidasAsync(Professora, PerfisAcesso.Professor, default);
        Assert.DoesNotContain(lista, a => a.Id == _restritaId);
    }

    [Theory]
    [InlineData("mensal", "2026-02", "2026-02-01", "2026-02-28")]
    [InlineData("semestral", "2026-1", "2026-01-01", "2026-06-30")]
    [InlineData("anual", "2025", "2025-01-01", "2025-12-31")]
    public void PeriodosRespeitamOMesSemestreEAno(string tipo, string referencia, string ini, string fim)
    {
        var s = new RelatoriosService(_db) { HojeFixo = new DateOnly(2026, 11, 6) };
        var p = s.ResolverPeriodo(tipo, referencia, new DateOnly(2025, 9, 1));
        Assert.Equal(DateOnly.Parse(ini), p.Ini);
        Assert.Equal(DateOnly.Parse(fim), p.Fim);
        Assert.NotNull(s.Anterior(p));
    }

    [Fact]
    public void PeriodoNaoPassaDeHoje()
    {
        var s = new RelatoriosService(_db) { HojeFixo = new DateOnly(2026, 11, 6) };
        Assert.Equal(new DateOnly(2026, 11, 6), s.ResolverPeriodo("mensal", "2026-11", new DateOnly(2025, 9, 1)).Fim);
        Assert.Equal(new DateOnly(2026, 11, 6), s.ResolverPeriodo("anual", "2026", new DateOnly(2025, 9, 1)).Fim);
        Assert.Null(s.Anterior(s.ResolverPeriodo("geral", null, new DateOnly(2025, 9, 1))));
    }

    [Fact]
    public void EquipeEscolheAVisaoEOsDemaisNao()
    {
        var eqp = new UsuarioAtuante("u-eqp", PerfisAcesso.Equipe, "EQP-000001", "Dev", null);
        Assert.Equal(PerfisAcesso.Juridico, RelatoriosService.PerfilRelatorio(eqp, PerfisAcesso.Juridico));
        Assert.Equal(PerfisAcesso.Adm, RelatoriosService.PerfilRelatorio(eqp, null));
        Assert.Equal(PerfisAcesso.Recepcao, RelatoriosService.PerfilRelatorio(Recepcao, PerfisAcesso.Adm));
    }
}
