using System.Globalization;
using CasaMulher.Api.Data;
using CasaMulher.Api.DTOs;
using CasaMulher.Api.Security;
using CasaMulher.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CasaMulher.Api.Controllers;

/// <summary>
/// Aba "Relatórios" de cada perfil: Relatório Estatístico/Geral e relatório individual da assistida.
/// Os números são calculados no servidor com as regras de sigilo dos módulos; relatos e evoluções
/// só saem para a responsável, registro a registro, e a impressão fica no histórico de ações.
/// </summary>
[ApiController]
[Route("api/relatorios")]
[Authorize(Policy = PoliticasAcesso.AcessoRelatorios)]
public class RelatoriosController : ControllerBase
{
    private static readonly string[] Finalidades =
    [
        "Encaminhamento para a rede de proteção",
        "Solicitação do Poder Judiciário",
        "Continuidade do cuidado em saúde",
        "Outra"
    ];

    private readonly AppDbContext _dbContext;
    private readonly AssistidasService _assistidas;
    private readonly RelatoriosService _relatorios;
    private readonly IAuditoriaService _auditoria;

    public RelatoriosController(AppDbContext dbContext, AssistidasService assistidas, RelatoriosService relatorios, IAuditoriaService auditoria)
    {
        _dbContext = dbContext;
        _assistidas = assistidas;
        _relatorios = relatorios;
        _auditoria = auditoria;
    }

    [HttpGet("contexto")]
    public async Task<ActionResult<RelatorioContextoResponse>> Contexto([FromQuery] string? visao, CancellationToken ct)
    {
        var (usuario, perfil, erro) = await ResolverAsync(visao, ct);
        if (erro is not null) return erro;

        var contexto = new RelatorioContextoResponse
        {
            Perfil = perfil,
            InicioDados = (await _relatorios.InicioDadosAsync(ct)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Hoje = _relatorios.Hoje.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            VisoesDisponiveis = usuario!.Perfil == PerfisAcesso.Equipe
                ? [PerfisAcesso.Adm, PerfisAcesso.Recepcao, PerfisAcesso.Juridico, PerfisAcesso.Psicologia, PerfisAcesso.AssistenteSocial, PerfisAcesso.Professor]
                : []
        };

        switch (perfil)
        {
            case PerfisAcesso.Adm:
                contexto.PerfilNome = "ADM / Coordenação";
                contexto.TituloArea = "Relatório Estatístico/Geral da Casa";
                contexto.Intro = "Números da Casa inteira, sem identificar assistidas. No relatório individual a Coordenação vê metadados (datas, situação, protocolo), nunca relatos nem nada da Psicologia ou do Serviço Social por assistida.";
                contexto.VoltarUrl = "painel.html";
                contexto.VoltarRotulo = "Painel";
                break;
            case PerfisAcesso.Recepcao:
                contexto.PerfilNome = "Recepção";
                contexto.TituloArea = "Relatório Estatístico/Geral da Recepção";
                contexto.Intro = "Acolhimentos, encaminhamentos e cursos escolhidos. Casos com acesso restrito e qualquer informação da Psicologia ficam fora.";
                contexto.VoltarUrl = "recepcao.html";
                contexto.VoltarRotulo = "Recepção";
                break;
            case PerfisAcesso.Juridico:
                contexto.PerfilNome = "Jurídico";
                contexto.TituloArea = "Relatório Estatístico/Geral do Jurídico";
                contexto.Intro = "Estatísticas do Jurídico e o histórico jurídico de cada assistida. Casos “somente as responsáveis” mostram só metadados para quem não é responsável.";
                contexto.FiltroRotulo = "Casos";
                contexto.FiltroOpcoes = [new() { Valor = "todos", Rotulo = "Todos os casos" }, new() { Valor = "meus", Rotulo = "Minhas assistidas" }];
                contexto.RotuloSigiloso = "Incluir relatos (escolher quais)";
                contexto.DocumentoExterno = true;
                contexto.VoltarUrl = "juridico.html";
                contexto.VoltarRotulo = "Jurídico";
                break;
            case PerfisAcesso.Psicologia:
                contexto.PerfilNome = "Psicologia";
                contexto.TituloArea = "Relatório Estatístico/Geral da Psicologia";
                contexto.Intro = "Estatísticas da Psicologia e o prontuário de cada assistida. As anotações seguem o sigilo: só a psicóloga responsável vê e só ela pode incluir evoluções no PDF.";
                contexto.FiltroRotulo = "Prontuários";
                contexto.FiltroOpcoes = [new() { Valor = "todos", Rotulo = "Todos os prontuários" }, new() { Valor = "meus", Rotulo = "Minhas assistidas" }];
                contexto.RotuloSigiloso = "Incluir evoluções (escolher quais)";
                contexto.DocumentoExterno = true;
                contexto.VoltarUrl = "psicologia.html";
                contexto.VoltarRotulo = "Psicologia";
                break;
            case PerfisAcesso.AssistenteSocial:
                contexto.PerfilNome = "Serviço Social";
                contexto.TituloArea = "Relatório Estatístico/Geral do Serviço Social";
                contexto.Intro = "Atendimentos, rede, benefícios, vulnerabilidades e plano de acompanhamento. Os relatos seguem o sigilo: só a assistente social responsável vê e só ela pode incluí-los no PDF. Documentos anexados nunca entram no PDF.";
                contexto.FiltroRotulo = "Prontuários";
                contexto.FiltroOpcoes = [new() { Valor = "todos", Rotulo = "Todos os prontuários" }, new() { Valor = "meus", Rotulo = "Minhas assistidas" }];
                contexto.RotuloSigiloso = "Incluir relatos (escolher quais)";
                contexto.DocumentoExterno = true;
                contexto.VoltarUrl = "assistente-social.html";
                contexto.VoltarRotulo = "Serviço Social";
                break;
            case PerfisAcesso.Professor:
                contexto.PerfilNome = "Professora";
                contexto.TituloArea = "Relatório Estatístico/Geral das turmas";
                contexto.Intro = "Frequência, entradas, conclusões e interrupções das suas turmas. Alunas com acesso restrito não aparecem.";
                contexto.FiltroRotulo = "Curso";
                var cursos = await _dbContext.ProfessoresCursos.AsNoTracking()
                    .Where(p => p.UserId == usuario.UserId)
                    .OrderBy(p => p.Curso!.Nome)
                    .Select(p => new RelatorioOpcaoDto { Valor = p.CursoId.ToString(), Rotulo = p.Curso!.Nome })
                    .ToListAsync(ct);
                contexto.FiltroOpcoes = [new() { Valor = "todos", Rotulo = "Todos os meus cursos" }, .. cursos];
                contexto.VoltarUrl = "professor.html";
                contexto.VoltarRotulo = "Aulas e chamada";
                break;
        }

        return Ok(contexto);
    }

    [HttpGet("geral")]
    public async Task<ActionResult<RelatorioResponse>> Geral(
        [FromQuery] string? periodo,
        [FromQuery(Name = "ref")] string? referencia,
        [FromQuery] string? filtro,
        [FromQuery] string? visao,
        [FromQuery] int? prazoFila,
        [FromQuery] int? faltasSeguidas,
        [FromQuery] int? diasRetorno,
        [FromQuery] int? ocupacaoBaixa,
        CancellationToken ct)
    {
        var (usuario, perfil, erro) = await ResolverAsync(visao, ct);
        if (erro is not null) return erro;

        var criterios = CriteriosAtencao.De(prazoFila, faltasSeguidas, diasRetorno, ocupacaoBaixa);
        return Ok(await _relatorios.GerarGeralAsync(usuario!, perfil, periodo, referencia, filtro, criterios, ct));
    }

    [HttpGet("assistidas")]
    public async Task<ActionResult<List<RelatorioAssistidaOpcaoDto>>> Assistidas([FromQuery] string? visao, CancellationToken ct)
    {
        var (usuario, perfil, erro) = await ResolverAsync(visao, ct);
        if (erro is not null) return erro;

        return Ok(await _relatorios.ListarAssistidasAsync(usuario!, perfil, ct));
    }

    [HttpGet("assistidas/{id:int}")]
    public async Task<ActionResult<RelatorioIndividualResponse>> Individual(
        int id,
        [FromQuery] string? periodo,
        [FromQuery(Name = "ref")] string? referencia,
        [FromQuery] string? visao,
        CancellationToken ct)
    {
        var (usuario, perfil, erro) = await ResolverAsync(visao, ct);
        if (erro is not null) return erro;

        var relatorio = await _relatorios.GerarIndividualAsync(usuario!, perfil, id, periodo, referencia, ct);
        if (relatorio is null) return NotFound(new { mensagem = "Assistida não encontrada entre as que você pode ver." });

        await _auditoria.RegistrarAsync(
            "RELATORIO_INDIVIDUAL_GERADO",
            "Assistida",
            id.ToString(CultureInfo.InvariantCulture),
            $"{usuario!.Identificador} gerou o relatório individual da assistida #{id} ({perfil}, {relatorio.Periodo.Rotulo}).");

        return Ok(relatorio);
    }

    /// <summary>Prévia: devolve o texto só dos registros escolhidos, e só para a responsável.</summary>
    [HttpPost("assistidas/{id:int}/sigilosos")]
    public async Task<ActionResult<List<RelatorioConteudoSigilosoDto>>> Sigilosos(int id, RelatorioSigilososRequest request, [FromQuery] string? visao, CancellationToken ct)
    {
        var (usuario, perfil, erro) = await ResolverAsync(visao, ct);
        if (erro is not null) return erro;

        var conteudo = await _relatorios.ConteudoSigilosoAsync(usuario!, perfil, id, request.Registros ?? [], ct);
        if (conteudo is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { mensagem = "Só a responsável pelo caso ou prontuário pode incluir este conteúdo no PDF." });
        }

        return Ok(conteudo);
    }

    /// <summary>Registra a impressão no histórico de ações: quem, quando, qual assistida e quais registros (sem copiar textos).</summary>
    [HttpPost("impressao")]
    public async Task<ActionResult<RelatorioImpressaoResponse>> Impressao(RelatorioImpressaoRequest request, CancellationToken ct)
    {
        var (usuario, perfil, erro) = await ResolverAsync(request.Visao, ct);
        if (erro is not null) return erro;

        var periodo = _relatorios.ResolverPeriodo(request.Periodo, request.Ref, await _relatorios.InicioDadosAsync(ct));
        var rotulos = new List<string>();

        if (request.Tipo == "individual")
        {
            if (request.AssistidaId is not { } assistidaId) return BadRequest(new { mensagem = "Informe a assistida." });

            var visiveis = await _relatorios.ListarAssistidasAsync(usuario!, perfil, ct);
            if (visiveis.All(a => a.Id != assistidaId)) return NotFound(new { mensagem = "Assistida não encontrada entre as que você pode ver." });

            if (request.Registros.Count > 0)
            {
                var conteudo = await _relatorios.ConteudoSigilosoAsync(usuario!, perfil, assistidaId, request.Registros, ct);
                if (conteudo is null)
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new { mensagem = "Só a responsável pode imprimir relatos ou evoluções." });
                }

                rotulos = conteudo.Select(c => c.Rotulo).ToList();
            }

            var tipoSig = perfil == PerfisAcesso.Psicologia ? "evolução(ões)" : "relato(s)";
            await _auditoria.RegistrarAsync(
                "RELATORIO_IMPRESSO",
                "Assistida",
                assistidaId.ToString(CultureInfo.InvariantCulture),
                $"{usuario!.Identificador} imprimiu o relatório individual da assistida #{assistidaId} ({perfil}, {periodo.Rotulo}) "
                    + (rotulos.Count > 0 ? $"com {rotulos.Count} {tipoSig} sigiloso(s): {string.Join(", ", rotulos)}." : "sem relatos nem evoluções."));
        }
        else
        {
            await _auditoria.RegistrarAsync(
                "RELATORIO_IMPRESSO",
                "Relatorio",
                perfil,
                $"{usuario!.Identificador} imprimiu o Relatório Estatístico/Geral ({perfil}, {periodo.Rotulo}).");
        }

        return Ok(new RelatorioImpressaoResponse
        {
            Mensagem = "Impressão registrada no histórico de ações.",
            RegistrosSigilosos = rotulos.Count,
            Rotulos = rotulos,
            RegistradoEm = DateTime.UtcNow
        });
    }

    /// <summary>Documento separado do relatório: a profissional escreve o resumo (que não vem ao servidor).</summary>
    [HttpPost("assistidas/{id:int}/documento-externo")]
    public async Task<ActionResult<DocumentoExternoResponse>> DocumentoExterno(int id, DocumentoExternoRequest request, [FromQuery] string? visao, CancellationToken ct)
    {
        var (usuario, perfil, erro) = await ResolverAsync(visao, ct);
        if (erro is not null) return erro;

        var destinatario = request.Destinatario?.Trim() ?? string.Empty;
        var finalidade = request.Finalidade?.Trim() ?? string.Empty;
        if (request.Confirmar && (destinatario.Length < 3 || destinatario.Length > 160))
        {
            return BadRequest(new { mensagem = "Informe o destinatário (entre 3 e 160 caracteres)." });
        }

        if (!Finalidades.Contains(finalidade))
        {
            return BadRequest(new { mensagem = "Escolha a finalidade." });
        }

        var documento = await _relatorios.DocumentoExternoAsync(usuario!, perfil, id, request.Periodo, request.Ref, ct);
        if (documento is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                mensagem = perfil == PerfisAcesso.Psicologia
                    ? "Só a psicóloga responsável pode emitir documento para outra instituição."
                    : perfil == PerfisAcesso.AssistenteSocial
                    ? "Só a assistente social responsável pode emitir documento para outra instituição."
                    : "Só quem pode ver o conteúdo do caso pode emitir documento para outra instituição."
            });
        }

        if (request.Confirmar)
        {
            await _auditoria.RegistrarAsync(
                "RELATORIO_DOCUMENTO_EXTERNO",
                "Assistida",
                id.ToString(CultureInfo.InvariantCulture),
                $"{usuario!.Identificador} gerou documento para outra instituição ({documento.Area}) da assistida #{id}. Destinatário: {destinatario}. Finalidade: {finalidade}. O resumo escrito não é guardado.");
            documento.Registrado = true;
        }

        return Ok(documento);
    }

    private async Task<(UsuarioAtuante? Usuario, string Perfil, ActionResult? Erro)> ResolverAsync(string? visao, CancellationToken ct)
    {
        var usuario = await _assistidas.ObterUsuarioAsync(User, ct);
        if (usuario is null) return (null, string.Empty, Unauthorized());

        var perfil = RelatoriosService.PerfilRelatorio(usuario, visao);
        if (!RelatoriosService.PerfilComRelatorio(perfil))
        {
            return (usuario, perfil, StatusCode(StatusCodes.Status403Forbidden, new { mensagem = "Este perfil ainda não tem relatórios." }));
        }

        return (usuario, perfil, null);
    }
}
