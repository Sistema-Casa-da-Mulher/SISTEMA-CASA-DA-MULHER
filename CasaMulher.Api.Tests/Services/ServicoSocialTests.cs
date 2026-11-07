using System.Security.Claims;
using System.Text;
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
/// Serviço Social: numeração, sigilo do prontuário, anexos (tipo, tamanho, limite, quem baixa),
/// encaminhamentos, CRESS e relatórios. Os controllers são chamados direto, com o usuário no contexto.
/// </summary>
public sealed class ServicoSocialTests : IAsyncLifetime
{
    private const string Relato = "RELATO-SOCIAL-SECRETO-789";
    private const string Cress = "CRESS 9ª Região nº 12345";

    private static readonly UsuarioAtuante Social1 = new("u-soc1", PerfisAcesso.AssistenteSocial, "SOC-000001", "Assistente Um", null);
    private static readonly UsuarioAtuante Social2 = new("u-soc2", PerfisAcesso.AssistenteSocial, "SOC-000002", "Assistente Dois", null);
    private static readonly UsuarioAtuante Recepcao = new("u-rec", PerfisAcesso.Recepcao, "REC-000001", "Recepcionista", null);
    private static readonly UsuarioAtuante Adm = new("u-adm", PerfisAcesso.Adm, "ADM-000001", "Coordenação", null);
    private static readonly UsuarioAtuante JurResponsavel = new("u-jur1", PerfisAcesso.Juridico, "JUR-000001", "Advogada Responsável", null);
    private static readonly UsuarioAtuante JurOutra = new("u-jur2", PerfisAcesso.Juridico, "JUR-000002", "Advogada Outra", null);

    private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.4\n% documento ficticio\n%%EOF");
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0x0D];

    private SqliteConnection _connection = null!;
    private AppDbContext _db = null!;
    private readonly List<string> _auditoria = [];
    private int _assistidaId;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        await _db.Database.EnsureCreatedAsync();

        foreach (var u in new[] { Social1, Social2, Recepcao, Adm, JurResponsavel, JurOutra })
        {
            _db.Users.Add(new ApplicationUser
            {
                Id = u.UserId, UserName = u.Identificador, NomeCompleto = u.Nome, Perfil = u.Perfil, IdentificadorFuncionario = u.Identificador,
                Ativo = true, Cress = u == Social1 ? Cress : null
            });
        }

        var assistida = new Assistida
        {
            Nome = "Ana Ficticia", Cpf = "52998224725", Origem = AssistidaOrigem.Recepcao, CadastradaPorUserId = Recepcao.UserId,
            CadastradaPorIdentificador = Recepcao.Identificador, CadastradaPorNome = Recepcao.Nome,
            EncaminhadaAssistenciaSocialEm = DateTime.UtcNow.AddDays(-3), EncaminhadaJuridicoEm = DateTime.UtcNow.AddDays(-3), CriadoEm = DateTime.UtcNow.AddDays(-3)
        };
        _db.Assistidas.Add(assistida);
        await _db.SaveChangesAsync();
        _assistidaId = assistida.Id;

        var caso = new CasoJuridico { AssistidaId = assistida.Id, Visibilidade = CasoJuridicoVisibilidade.Responsaveis };
        caso.Responsaveis.Add(new CasoJuridicoResponsavel { UserId = JurResponsavel.UserId, Identificador = JurResponsavel.Identificador, Nome = JurResponsavel.Nome, AtribuidoPorIdentificador = "X" });
        _db.CasosJuridicos.Add(caso);
        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    // ------------------------------------------------------------------ apoio

    private static readonly JsonSerializerOptions JsonOpcoes = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private static string Json(object? o) => JsonSerializer.Serialize(o, JsonOpcoes);

    private sealed class AuditoriaFalsa(List<string> acoes) : IAuditoriaService
    {
        public Task RegistrarAsync(string acao, string entidade, string? entidadeId, string descricao, string? identificadorReferencia = null, string? escopo = null)
        {
            acoes.Add(acao);
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

    private sealed class EmailFalso : IEmailService
    {
        public Task<ResultadoEnvioEmail> EnviarAsync(string destinatario, string assunto, string corpoHtml, string tipo) => Task.FromResult(ResultadoEnvioEmail.Simulado());
    }

    private SecuritySnapshotPersistenceService Snapshot()
    {
        var config = new ConfigurationBuilder().Build();
        var hml = new HmlDbSnapshotService(new GitHubPrivateFileService(new HttpClient(), config), config, new Ambiente(),
            new HmlDbStorageInfo(Path.Combine(Path.GetTempPath(), "x.db"), true), new HmlDbSnapshotState(), NullLogger<HmlDbSnapshotService>.Instance);
        return new SecuritySnapshotPersistenceService(hml, new Ambiente(), NullLogger<SecuritySnapshotPersistenceService>.Instance);
    }

    private static ControllerContext Contexto(UsuarioAtuante u) => new()
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, u.UserId),
                new Claim("perfil", u.Perfil),
                new Claim("identificadorFuncionario", u.Identificador)
            ], "teste"))
        }
    };

    private AnexosSociaisArquivos Arquivos(long limite = long.MaxValue) => new(new byte[32], limite);

    private ServicoSocialController Social(UsuarioAtuante u) => new(
        _db, new AssistidasService(_db, NullLogger<AssistidasService>.Instance), new ServicoSocialAcessoService(_db), Arquivos(),
        new AuditoriaFalsa(_auditoria), Snapshot(), new EmailFalso(), NullLogger<ServicoSocialController>.Instance)
    { ControllerContext = Contexto(u) };

    private AnexosSociaisController Anexos(UsuarioAtuante u, long limite = long.MaxValue) => new(
        _db, new AssistidasService(_db, NullLogger<AssistidasService>.Instance), new ServicoSocialAcessoService(_db), Arquivos(limite),
        new AuditoriaFalsa(_auditoria), Snapshot())
    { ControllerContext = Contexto(u) };

    private static int Status(IActionResult r) => r switch
    {
        ObjectResult o => o.StatusCode ?? 200,
        StatusCodeResult s => s.StatusCode,
        FileContentResult => 200,
        _ => 0
    };

    private static int Status<T>(ActionResult<T> r) => r.Result is null ? 200 : Status(r.Result);

    private static IFormFile Arquivo(string nome, byte[] dados) =>
        new FormFile(new MemoryStream(dados), 0, dados.Length, "arquivo", nome);

    private async Task<int> AtendimentoAsync(UsuarioAtuante u, string tipo = "individual")
    {
        var r = await Social(u).RegistrarAtendimento(_assistidaId, new NovoAtendimentoSocialRequest { Tipo = tipo, Relato = Relato + " com detalhes suficientes" }, default);
        Assert.Equal(200, Status(r));
        return ((AtendimentoSocialRegistradoResponse)((OkObjectResult)r.Result!).Value!).AtendimentoId;
    }

    private async Task<int> AnexarAsync(string visibilidade = AnexoSocialVisibilidade.ServicoSocial)
    {
        var r = await Anexos(Social1).Enviar(_assistidaId, Arquivo("rg.pdf", Pdf), "rg", "frente", visibilidade, null, default);
        Assert.Equal(200, Status(r));
        return ((AnexoSocialResponse)((OkObjectResult)r.Result!).Value!).Id;
    }

    // ------------------------------------------------------------------ testes

    [Fact]
    public async Task ProntuarioRecebeNumeroSocSequencialEAtendimentosNumerados()
    {
        await AtendimentoAsync(Social1);
        await AtendimentoAsync(Social1, "visita_domiciliar");

        var pront = await _db.ProntuariosSociais.Include(p => p.Atendimentos).Include(p => p.Responsaveis).SingleAsync();
        Assert.Matches(@"^SOC-\d{4}-000001$", pront.Numero);
        Assert.Equal([1, 2], pront.Atendimentos.Select(a => a.Numero).OrderBy(n => n));
        Assert.Equal(Social1.UserId, Assert.Single(pront.Responsaveis).UserId);
        Assert.Equal(Cress, pront.Atendimentos.First().ProfissionalCress);
        Assert.Contains("SOCIAL_ATENDIMENTO_REGISTRADO", _auditoria);
    }

    [Fact]
    public async Task FichaSemProntuarioAbreSemErro()
    {
        var r = await Social(Social1).ObterProntuario(_assistidaId, default);
        var ficha = (ProntuarioSocialDetalheResponse)((OkObjectResult)r.Result!).Value!;
        Assert.Null(ficha.ProntuarioId);
        Assert.True(ficha.PodeRegistrar);
        Assert.Empty(ficha.EncaminhamentosRede);
    }

    [Fact]
    public async Task OutraAssistenteSocialSoVeQueOAtendimentoExistiuAteUsarEmergencia()
    {
        await AtendimentoAsync(Social1);

        var outra = (ProntuarioSocialDetalheResponse)((OkObjectResult)(await Social(Social2).ObterProntuario(_assistidaId, default)).Result!).Value!;
        Assert.False(outra.PodeVerConteudo);
        Assert.True(Assert.Single(outra.Atendimentos).ConteudoOculto);
        Assert.DoesNotContain(Relato, Json(outra));
        Assert.Equal(403, Status(await Social(Social2).RegistrarAtendimento(_assistidaId, new NovoAtendimentoSocialRequest { Tipo = "individual", Relato = "Tentativa de registro sem acesso" }, default)));

        Assert.Equal(200, Status(await Social(Social2).PedirAcessoEmergencia(_assistidaId, new AcessoEmergenciaPsicologiaRequest { Motivo = "Responsável de férias e assistida na Casa" }, default)));
        var comEmergencia = (ProntuarioSocialDetalheResponse)((OkObjectResult)(await Social(Social2).ObterProntuario(_assistidaId, default)).Result!).Value!;
        Assert.True(comEmergencia.PodeVerConteudo);
        Assert.Contains("SOCIAL_ACESSO_EMERGENCIA", _auditoria);
    }

    [Fact]
    public async Task CoordenacaoNaoAbreProntuarioSocial()
    {
        await AtendimentoAsync(Social1);
        Assert.Equal(403, Status(await Social(Adm).ObterProntuario(_assistidaId, default)));
        Assert.Equal(403, Status(await Social(Adm).Listar(null, null, default)));
        Assert.Equal(200, Status(await Social(Adm).Indicadores(default)));
    }

    [Theory]
    [InlineData("documento.exe", "pdf")]
    [InlineData("foto.png", "pdf")]
    [InlineData("texto.pdf", "texto")]
    public async Task AnexoComTipoInvalidoEhRecusado(string nome, string conteudo)
    {
        await AtendimentoAsync(Social1);
        var dados = conteudo == "pdf" ? Pdf : Encoding.UTF8.GetBytes("isto não é um PDF");
        var r = await Anexos(Social1).Enviar(_assistidaId, Arquivo(nome, dados), "rg", null, null, null, default);
        Assert.Equal(400, Status(r));
        Assert.Empty(_db.AnexosSociais);
    }

    [Fact]
    public async Task AnexoAcimaDe5MbEhRecusado()
    {
        await AtendimentoAsync(Social1);
        var grande = new byte[AnexosSociaisArquivos.TamanhoMaximoBytes + 1];
        Pdf.CopyTo(grande, 0);
        Assert.Equal(400, Status(await Anexos(Social1).Enviar(_assistidaId, Arquivo("grande.pdf", grande), "rg", null, null, null, default)));
    }

    [Fact]
    public async Task LimiteTotalDosAnexosEhRespeitado()
    {
        await AtendimentoAsync(Social1);
        Assert.Equal(200, Status(await Anexos(Social1, Pdf.Length + 5).Enviar(_assistidaId, Arquivo("a.pdf", Pdf), "rg", null, null, null, default)));
        Assert.Equal(400, Status(await Anexos(Social1, Pdf.Length + 5).Enviar(_assistidaId, Arquivo("b.pdf", Pdf), "cpf", null, null, null, default)));
    }

    [Fact]
    public async Task AnexoFicaCriptografadoEVoltaIgualNoDownload()
    {
        await AtendimentoAsync(Social1);
        var id = await AnexarAsync();
        var guardado = await _db.AnexosSociaisConteudos.SingleAsync();
        Assert.False(guardado.Dados.AsSpan().IndexOf(Pdf.AsSpan(0, 8)) >= 0);

        var download = await Anexos(Social1).Baixar(id, false, default);
        Assert.Equal(Pdf, Assert.IsType<FileContentResult>(download).FileContents);
        Assert.Contains("SOCIAL_ANEXO_BAIXADO", _auditoria);

        var png = await Anexos(Social1).Enviar(_assistidaId, Arquivo("foto.png", Png), "outro", null, null, null, default);
        Assert.Equal(200, Status(png));
    }

    [Fact]
    public async Task RecepcaoCoordenacaoEJuridicoNaoResponsavelNaoBaixamAnexos()
    {
        await AtendimentoAsync(Social1);
        var id = await AnexarAsync(AnexoSocialVisibilidade.EquipeTecnica);

        foreach (var u in new[] { Recepcao, Adm, JurOutra })
        {
            Assert.Equal(403, Status(await Anexos(u).Baixar(id, false, default)));
            Assert.Equal(403, Status(await Anexos(u).Listar(_assistidaId, default)));
        }

        Assert.Contains("SOCIAL_ANEXO_ACESSO_NEGADO", _auditoria);
        Assert.Equal(403, Status(await Anexos(Recepcao).Enviar(_assistidaId, Arquivo("x.pdf", Pdf), "rg", null, null, null, default)));
    }

    [Fact]
    public async Task JuridicoResponsavelSoVeAnexosAbertosParaAEquipeTecnica()
    {
        await AtendimentoAsync(Social1);
        var soSocial = await AnexarAsync(AnexoSocialVisibilidade.ServicoSocial);
        var equipe = await AnexarAsync(AnexoSocialVisibilidade.EquipeTecnica);

        Assert.Equal(403, Status(await Anexos(JurResponsavel).Baixar(soSocial, false, default)));
        Assert.Equal(200, Status(await Anexos(JurResponsavel).Baixar(equipe, false, default)));

        var lista = (AnexosCompartilhadosResponse)((OkObjectResult)(await Anexos(JurResponsavel).Listar(_assistidaId, default)).Result!).Value!;
        Assert.Equal(equipe, Assert.Single(lista.Anexos).Id);
        Assert.Null(lista.NumeroProntuario);
    }

    [Fact]
    public async Task ExclusaoDeAnexoELogicaComMotivo()
    {
        await AtendimentoAsync(Social1);
        var id = await AnexarAsync();
        Assert.Equal(400, Status(await Anexos(Social1).Excluir(id, new ExcluirAnexoRequest { Motivo = "x" }, default)));
        Assert.Equal(403, Status(await Anexos(Social2).Excluir(id, new ExcluirAnexoRequest { Motivo = "enviado por engano" }, default)));
        Assert.Equal(200, Status(await Anexos(Social1).Excluir(id, new ExcluirAnexoRequest { Motivo = "enviado por engano" }, default)));

        var anexo = await _db.AnexosSociais.SingleAsync();
        Assert.True(anexo.Excluido);
        Assert.Equal("enviado por engano", anexo.MotivoExclusao);
        Assert.Single(_db.AnexosSociaisConteudos);
        Assert.Equal(404, Status(await Anexos(Social1).Baixar(id, false, default)));
    }

    [Fact]
    public async Task EncaminhamentoARedeEPlanoSoComAcessoAoConteudo()
    {
        await AtendimentoAsync(Social1);
        var r = await Social(Social1).RegistrarEncaminhamentoRede(_assistidaId, new EncaminhamentoRedeRequest { Destino = "cras", Motivo = "Inclusão no CadÚnico" }, default);
        var enc = (EncaminhamentoRedeResponse)((OkObjectResult)r.Result!).Value!;
        Assert.Equal(EncaminhamentoRedeStatus.Enviado, enc.Status);

        Assert.Equal(400, Status(await Social(Social1).RegistrarEncaminhamentoRede(_assistidaId, new EncaminhamentoRedeRequest { Destino = "lugar_inventado", Motivo = "Motivo qualquer" }, default)));
        Assert.Equal(403, Status(await Social(Social2).AtualizarEncaminhamentoRede(enc.Id, new AtualizarEncaminhamentoRedeRequest { Status = "retorno_recebido" }, default)));
        var atualizado = (EncaminhamentoRedeResponse)((OkObjectResult)(await Social(Social1).AtualizarEncaminhamentoRede(enc.Id, new AtualizarEncaminhamentoRedeRequest { Status = "retorno_recebido", Observacao = "Inscrita" }, default)).Result!).Value!;
        Assert.Equal("retorno_recebido", atualizado.Status);

        var meta = (MetaSocialResponse)((OkObjectResult)(await Social(Social1).RegistrarMeta(_assistidaId, new MetaSocialRequest { Descricao = "Tirar 2ª via do RG" }, default)).Result!).Value!;
        var concluida = (MetaSocialResponse)((OkObjectResult)(await Social(Social1).AtualizarMeta(meta.Id, new AtualizarMetaSocialRequest { Situacao = "concluida" }, default)).Result!).Value!;
        Assert.NotNull(concluida.ConcluidaEm);
    }

    [Fact]
    public async Task EstudoSocialGeraAvisoDeVulnerabilidadeSemDetalhesParaOJuridico()
    {
        await AtendimentoAsync(Social1);
        var estudo = await Social(Social1).AtualizarEstudo(_assistidaId, new EstudoSocialRequest { FaixaRenda = "sem_renda", Moradia = "alugada", ConviveComAgressor = true, Beneficios = ["bpc"] }, default);
        var e = (EstudoSocialResponse)((OkObjectResult)estudo.Result!).Value!;
        Assert.Equal(["sem_renda", "sem_moradia_segura"], e.Vulnerabilidades);

        await Social(Social1).AtualizarEstudo(_assistidaId, new EstudoSocialRequest { FaixaRenda = "1_a_2_sm" }, default);
        var ficha = (ProntuarioSocialDetalheResponse)((OkObjectResult)(await Social(Social1).ObterProntuario(_assistidaId, default)).Result!).Value!;
        Assert.Equal(2, ficha.Estudo!.Versao);
        Assert.Single(ficha.HistoricoEstudo);

        // A outra assistente social vê o aviso, mas não o estudo.
        await Social(Social1).AtualizarEstudo(_assistidaId, new EstudoSocialRequest { Vulnerabilidades = ["inseguranca_alimentar"] }, default);
        var outra = (ProntuarioSocialDetalheResponse)((OkObjectResult)(await Social(Social2).ObterProntuario(_assistidaId, default)).Result!).Value!;
        Assert.Contains("inseguranca_alimentar", outra.Vulnerabilidades);
        Assert.Null(outra.Estudo);
    }

    [Fact]
    public async Task DeclaracaoLevaCressESoParaAtendimentoPresencial()
    {
        var presencial = await AtendimentoAsync(Social1);
        var telefone = await AtendimentoAsync(Social1, "contato_telefonico");

        var d = (DeclaracaoComparecimentoSocialResponse)((OkObjectResult)(await Social(Social1).Declaracao(presencial, default)).Result!).Value!;
        Assert.Equal(Cress, d.ProfissionalCress);
        Assert.DoesNotContain(Relato, Json(d));
        Assert.Equal(400, Status(await Social(Social1).Declaracao(telefone, default)));
    }

    [Fact]
    public async Task AssistenteSocialSemCressRecebeAvisoEPodePreencher()
    {
        await AtendimentoAsync(Social2);
        var ficha = (ProntuarioSocialDetalheResponse)((OkObjectResult)(await Social(Social2).ObterProntuario(_assistidaId, default)).Result!).Value!;
        Assert.Null(ficha.MeuCress);

        Assert.Equal(400, Status(await Social(Social2).AtualizarMeuCress(new MeuCressRequest { Cress = "abc" }, default)));
        Assert.Equal(200, Status(await Social(Social2).AtualizarMeuCress(new MeuCressRequest { Cress = "CRESS 9ª Região nº 54321" }, default)));
        Assert.Equal("CRESS 9ª Região nº 54321", (await _db.ProntuariosSociaisResponsaveis.SingleAsync()).Cress);
    }

    [Fact]
    public async Task RelatoriosSeguemOSigiloDoServicoSocial()
    {
        await AtendimentoAsync(Social1);
        await Social(Social1).RegistrarEncaminhamentoRede(_assistidaId, new EncaminhamentoRedeRequest { Destino = "creas", Motivo = "Acompanhamento PAEFI" }, default);
        var s = new RelatoriosService(_db);
        var atendimentoId = (await _db.AtendimentosSociais.SingleAsync()).Id;

        // ADM: números do Serviço Social, sem nomes e sem relatos.
        var adm = Json(await s.GerarGeralAsync(Adm, PerfisAcesso.Adm, "geral", null, null, new CriteriosAtencao(), default));
        Assert.Contains("Serviço Social em números", adm);
        Assert.DoesNotContain("Ana Ficticia", adm);
        Assert.DoesNotContain(Relato, adm);

        // Recepção: nada do Serviço Social além do encaminhamento.
        var rec = Json((await s.GerarIndividualAsync(Recepcao, PerfisAcesso.Recepcao, _assistidaId, "geral", null, default))!);
        Assert.DoesNotContain("SOC-", rec);
        Assert.DoesNotContain(Relato, rec);

        // Jurídico: só "em acompanhamento".
        var jur = Json((await s.GerarIndividualAsync(JurResponsavel, PerfisAcesso.Juridico, _assistidaId, "geral", null, default))!);
        Assert.Contains("Em acompanhamento no Serviço Social", jur);
        Assert.DoesNotContain("SOC-", jur);
        Assert.DoesNotContain("CREAS", jur);

        // Serviço Social: relato só para a responsável, só se pedido.
        var resp = (await s.GerarIndividualAsync(Social1, PerfisAcesso.AssistenteSocial, _assistidaId, "geral", null, default))!;
        Assert.True(resp.PodeIncluirSigiloso);
        Assert.DoesNotContain(Relato, Json(resp));
        Assert.Contains(Cress, resp.Rodape);
        var conteudo = await s.ConteudoSigilosoAsync(Social1, PerfisAcesso.AssistenteSocial, _assistidaId, [RelatoriosService.PrefixoServicoSocial + atendimentoId], default);
        Assert.Contains(Relato, Json(conteudo));
        Assert.Null(await s.ConteudoSigilosoAsync(Social2, PerfisAcesso.AssistenteSocial, _assistidaId, [RelatoriosService.PrefixoServicoSocial + atendimentoId], default));

        var geral = Json(await s.GerarGeralAsync(Social1, PerfisAcesso.AssistenteSocial, "geral", null, null, new CriteriosAtencao(), default));
        Assert.Contains("CREAS", geral);
        Assert.DoesNotContain(Relato, geral);
    }

    [Fact]
    public async Task EncaminhamentoInternoParaOServicoSocialEntraNaFila()
    {
        var nova = new Assistida { Nome = "Bia Ficticia", Cpf = "11144477735", Origem = AssistidaOrigem.Juridico, CadastradaPorUserId = "x", CadastradaPorIdentificador = "x", CadastradaPorNome = "x" };
        _db.Assistidas.Add(nova);
        await _db.SaveChangesAsync();

        var controller = new AssistidasController(_db, new AssistidasService(_db, NullLogger<AssistidasService>.Instance), new AuditoriaFalsa(_auditoria), Snapshot(),
            new CursosService(_db, NullLogger<CursosService>.Instance)) { ControllerContext = Contexto(JurResponsavel) };
        Assert.Equal(200, Status(await controller.Encaminhar(nova.Id, new EncaminharAssistidaRequest { Area = AssistidaOrigem.ServicoSocial }, default)));

        var fila = (List<SocialAssistidaResumoResponse>)((OkObjectResult)(await Social(Social1).Listar("fila", null, default)).Result!).Value!;
        Assert.Contains(fila, a => a.AssistidaId == nova.Id);
    }
}
