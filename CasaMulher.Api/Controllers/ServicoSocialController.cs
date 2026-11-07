using System.Globalization;
using CasaMulher.Api.Data;
using CasaMulher.Api.DTOs;
using CasaMulher.Api.Models;
using CasaMulher.Api.Security;
using CasaMulher.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CasaMulher.Api.Controllers;

/// <summary>
/// Módulo Serviço Social: fila de encaminhadas, prontuário social com número do servidor,
/// atendimentos numerados, adendos, responsáveis, visibilidade, acesso de emergência,
/// encerramento e declaração de comparecimento. Estudo social, rede e plano ficam em
/// ServicoSocialController.Acompanhamento.cs; anexos em AnexosSociaisController.
/// Uma nova área do Serviço Social entra como outra parte (partial) desta classe, usando
/// <see cref="CarregarProntuarioComAcessoAsync"/> para seguir as mesmas regras de sigilo.
/// Sigilo: por padrão só as responsáveis veem o conteúdo; a coordenação (ADM) vê só números.
/// </summary>
[ApiController]
[Route("api/servico-social")]
[Authorize(Policy = PoliticasAcesso.AcessoProntuarioSocial)]
public partial class ServicoSocialController : ControllerBase
{
    private const string MensagemCoordenacao = "A coordenação acompanha o Serviço Social apenas pelos indicadores.";

    private readonly AppDbContext _dbContext;
    private readonly AssistidasService _assistidas;
    private readonly ServicoSocialAcessoService _acesso;
    private readonly AnexosSociaisArquivos _arquivos;
    private readonly IAuditoriaService _auditoria;
    private readonly SecuritySnapshotPersistenceService _snapshot;
    private readonly IEmailService _emailService;
    private readonly ILogger<ServicoSocialController> _logger;

    public ServicoSocialController(
        AppDbContext dbContext,
        AssistidasService assistidas,
        ServicoSocialAcessoService acesso,
        AnexosSociaisArquivos arquivos,
        IAuditoriaService auditoria,
        SecuritySnapshotPersistenceService snapshot,
        IEmailService emailService,
        ILogger<ServicoSocialController> logger)
    {
        _dbContext = dbContext;
        _assistidas = assistidas;
        _acesso = acesso;
        _arquivos = arquivos;
        _auditoria = auditoria;
        _snapshot = snapshot;
        _emailService = emailService;
        _logger = logger;
    }

    /// <summary>
    /// escopo: fila (encaminhadas sem atendimento), minhas (sou responsável),
    /// prontuarios (padrão: com prontuário ou encaminhadas). Com busca, procura em todo o cadastro.
    /// </summary>
    [HttpGet("assistidas")]
    public async Task<ActionResult<List<SocialAssistidaResumoResponse>>> Listar(
        [FromQuery] string? escopo,
        [FromQuery] string? busca,
        CancellationToken cancellationToken)
    {
        var usuario = await _assistidas.ObterUsuarioAsync(User, cancellationToken);
        if (usuario is null) return Unauthorized();
        if (!AssistidasService.AtuaComoServicoSocial(usuario.Perfil)) return Proibido(MensagemCoordenacao);

        var assistidas = await _dbContext.Assistidas.AsNoTracking().Where(a => a.Ativa).ToListAsync(cancellationToken);
        var prontuarios = await CarregarProntuariosAsync(cancellationToken);
        var termo = AssistidasService.NormalizarTexto(busca);
        var digitos = AssistidasService.SomenteDigitos(termo);
        var temLetras = termo.Any(char.IsLetter);

        IEnumerable<Assistida> filtradas = assistidas;

        if (termo.Length > 0)
        {
            filtradas = filtradas.Where(a => temLetras
                ? AssistidasService.NormalizarTexto(a.Nome).Contains(termo)
                : digitos.Length >= 3 && (a.Cpf.Contains(digitos) || AssistidasService.SomenteDigitos(a.Telefone).Contains(digitos)));
        }
        else
        {
            filtradas = escopo switch
            {
                "fila" => filtradas.Where(a => a.EncaminhadaAssistenciaSocialEm is not null
                    && (!prontuarios.TryGetValue(a.Id, out var p) || p.Atendimentos.Count == 0)),
                "minhas" => filtradas.Where(a => prontuarios.TryGetValue(a.Id, out var p)
                    && p.Responsaveis.Any(r => r.UserId == usuario.UserId)),
                _ => filtradas.Where(a => a.EncaminhadaAssistenciaSocialEm is not null || prontuarios.ContainsKey(a.Id))
            };
        }

        var resultado = filtradas
            .Select(a => MontarResumo(a, prontuarios.GetValueOrDefault(a.Id), usuario))
            .OrderByDescending(r => r.Vulnerabilidade)
            .ThenBy(r => escopo == "fila" ? (r.EncaminhadaServicoSocialEm ?? DateTime.MaxValue).Ticks : 0)
            .ThenBy(r => r.Nome, StringComparer.Create(new CultureInfo("pt-BR"), true))
            .ToList();

        return Ok(resultado);
    }

    [HttpGet("prontuarios/{assistidaId:int}")]
    public async Task<ActionResult<ProntuarioSocialDetalheResponse>> ObterProntuario(int assistidaId, CancellationToken cancellationToken)
    {
        var usuario = await _assistidas.ObterUsuarioAsync(User, cancellationToken);
        if (usuario is null) return Unauthorized();
        if (!AssistidasService.AtuaComoServicoSocial(usuario.Perfil)) return Proibido(MensagemCoordenacao);

        var assistida = await _dbContext.Assistidas.AsNoTracking()
            .Include(a => a.Cursos).ThenInclude(c => c.Curso)
            .SingleOrDefaultAsync(a => a.Id == assistidaId && a.Ativa, cancellationToken);
        if (assistida is null) return NotFound(new { mensagem = "Assistida não encontrada." });

        var prontuario = await _dbContext.ProntuariosSociais.AsNoTracking()
            .Include(p => p.Responsaveis)
            .Include(p => p.Atendimentos).ThenInclude(a => a.Adendos)
            .Include(p => p.Estudos)
            .Include(p => p.EncaminhamentosRede)
            .Include(p => p.Metas)
            .Include(p => p.Anexos)
            .AsSplitQuery()
            .SingleOrDefaultAsync(p => p.AssistidaId == assistidaId, cancellationToken);

        var acesso = await _acesso.AvaliarAsync(prontuario, usuario, cancellationToken);
        var ver = acesso.PodeVerConteudo;
        var atendimentos = prontuario?.Atendimentos.OrderByDescending(a => a.Numero).ToList() ?? [];
        var anexosAtivos = prontuario?.Anexos.Where(a => !a.Excluido).ToList() ?? [];

        var emJuridico = await _dbContext.CasosJuridicos.AsNoTracking()
            .AnyAsync(c => c.AssistidaId == assistidaId && c.Atendimentos.Any() && c.Situacao != CasoJuridicoSituacao.Encerrado, cancellationToken);
        var emPsicologia = await _dbContext.ProntuariosPsicologicos.AsNoTracking()
            .AnyAsync(p => p.AssistidaId == assistidaId && p.Sessoes.Any() && p.Situacao == ProntuarioSituacao.EmAcompanhamento, cancellationToken);
        var hoje = DataLocal(DateTime.UtcNow);
        var estudos = prontuario?.Estudos.OrderByDescending(e => e.Versao).ToList() ?? [];
        var numeros = atendimentos.ToDictionary(a => a.Id, a => a.Numero);

        var resposta = new ProntuarioSocialDetalheResponse
        {
            Assistida = AssistidasController.MapearResposta(assistida, null, usuario.Perfil),
            ProntuarioId = prontuario?.Id,
            NumeroProntuario = prontuario?.Numero,
            Visibilidade = prontuario?.Visibilidade ?? ProntuarioVisibilidade.Responsaveis,
            Situacao = prontuario?.Situacao ?? ProntuarioSituacao.EmAcompanhamento,
            MotivoEncerramento = prontuario?.MotivoEncerramento,
            EncerradoEm = prontuario?.EncerradoEm,
            Responsaveis = prontuario?.Responsaveis.OrderBy(r => r.AtribuidoEm).Select(MapearResponsavel).ToList() ?? [],
            SouResponsavel = acesso.SouResponsavel,
            PodeVerConteudo = ver,
            PodeRegistrar = ver,
            PodeAtribuir = acesso.PodeAtribuir,
            PodeAlterarVisibilidade = acesso.SouResponsavel,
            PodePedirAcessoEmergencia = prontuario is not null && !ver,
            AcessoEmergenciaExpiraEm = acesso.EmergenciaExpiraEm,
            Vulnerabilidades = Separar(prontuario?.VulnerabilidadesAtuais),
            EmAtendimentoJuridico = emJuridico,
            EmAcompanhamentoPsicologia = emPsicologia,
            AnexosDisponiveis = _arquivos.Disponivel,
            MeuCress = await CressAsync(usuario.UserId, cancellationToken),
            TotalAtendimentos = atendimentos.Count,
            Atendimentos = atendimentos.Select(a => MapearAtendimento(a, ver, anexosAtivos.Count(x => x.AtendimentoSocialId == a.Id))).ToList(),
            Estudo = ver && estudos.Count > 0 ? MapearEstudo(estudos[0]) : null,
            HistoricoEstudo = ver ? estudos.Skip(1).Select(MapearEstudo).ToList() : [],
            EncaminhamentosRede = ver && prontuario is not null ? prontuario.EncaminhamentosRede.OrderByDescending(e => e.Data).ThenByDescending(e => e.Id).Select(MapearEncaminhamento).ToList() : [],
            Metas = ver && prontuario is not null ? prontuario.Metas.OrderBy(m => m.Situacao == MetaSocialSituacao.Concluida).ThenBy(m => m.Prazo ?? DateOnly.MaxValue).Select(m => MapearMeta(m, hoje)).ToList() : [],
            Anexos = ver ? anexosAtivos.OrderByDescending(a => a.CriadoEm).Select(a => MapearAnexo(a, numeros, ver)).ToList() : []
        };

        if (!ver)
        {
            resposta.Assistida.Observacoes = null;
        }

        if (ver && prontuario is not null && (atendimentos.Count > 0 || estudos.Count > 0))
        {
            await _auditoria.RegistrarAsync(
                "SOCIAL_PRONTUARIO_VISUALIZADO",
                "ProntuarioSocial",
                prontuario.Id.ToString(CultureInfo.InvariantCulture),
                $"{usuario.Identificador} abriu o prontuário social {prontuario.Numero}"
                    + (acesso.EmergenciaExpiraEm is not null && !acesso.SouResponsavel && prontuario.Visibilidade == ProntuarioVisibilidade.Responsaveis
                        ? " (acesso de emergência)."
                        : "."));
        }

        return Ok(resposta);
    }

    /// <summary>Abre o prontuário (número do servidor) sem precisar registrar um atendimento.</summary>
    [HttpPost("prontuarios/{assistidaId:int}/abrir")]
    public async Task<IActionResult> AbrirProntuario(int assistidaId, CancellationToken cancellationToken)
    {
        var usuario = await _assistidas.ObterUsuarioAsync(User, cancellationToken);
        if (usuario is null) return Unauthorized();
        if (!AssistidasService.AtuaComoServicoSocial(usuario.Perfil)) return Proibido("Somente o Serviço Social abre prontuários sociais.");

        var assistida = await _dbContext.Assistidas.SingleOrDefaultAsync(a => a.Id == assistidaId && a.Ativa, cancellationToken);
        if (assistida is null) return NotFound(new { mensagem = "Assistida não encontrada." });

        if (await _dbContext.ProntuariosSociais.AnyAsync(p => p.AssistidaId == assistidaId, cancellationToken))
        {
            return Ok(new { mensagem = "O prontuário já está aberto." });
        }

        var prontuario = NovoProntuario(assistida, usuario, DateTime.UtcNow, await CressAsync(usuario.UserId, cancellationToken));
        await SalvarComNumeroAsync(prontuario, cancellationToken);

        await _auditoria.RegistrarAsync(
            "SOCIAL_PRONTUARIO_ABERTO",
            "ProntuarioSocial",
            prontuario.Id.ToString(CultureInfo.InvariantCulture),
            $"{usuario.Identificador} abriu o prontuário social {prontuario.Numero} da assistida #{assistida.Id}.");

        var snapshot = await _snapshot.PersistAsync("social_prontuario", CancellationToken.None);
        return Ok(new { mensagem = $"Prontuário {prontuario.Numero} aberto.", numeroProntuario = prontuario.Numero, avisoSnapshot = snapshot.AvisoSnapshot });
    }

    [HttpPost("prontuarios/{assistidaId:int}/atendimentos")]
    public async Task<ActionResult<AtendimentoSocialRegistradoResponse>> RegistrarAtendimento(
        int assistidaId,
        NovoAtendimentoSocialRequest request,
        CancellationToken cancellationToken)
    {
        var usuario = await _assistidas.ObterUsuarioAsync(User, cancellationToken);
        if (usuario is null) return Unauthorized();
        if (!AssistidasService.AtuaComoServicoSocial(usuario.Perfil)) return Proibido("Somente o Serviço Social registra atendimentos.");

        var assistida = await _dbContext.Assistidas.SingleOrDefaultAsync(a => a.Id == assistidaId && a.Ativa, cancellationToken);
        if (assistida is null) return NotFound(new { mensagem = "Assistida não encontrada." });

        var agora = DateTime.UtcNow;
        var (realizadoEm, erro) = ValidarAtendimento(request, agora);
        if (erro is not null) return BadRequest(new { mensagem = erro });

        var prontuario = await _dbContext.ProntuariosSociais
            .Include(p => p.Responsaveis)
            .SingleOrDefaultAsync(p => p.AssistidaId == assistidaId, cancellationToken);

        var acesso = await _acesso.AvaliarAsync(prontuario, usuario, cancellationToken);
        if (!acesso.PodeVerConteudo)
        {
            return Proibido("Este prontuário está visível somente para as responsáveis. Peça acesso de emergência se precisar atender.");
        }

        var cress = await CressAsync(usuario.UserId, cancellationToken);
        var novo = prontuario is null;
        prontuario ??= NovoProntuario(assistida, usuario, agora, cress);

        // Um novo atendimento reabre um acompanhamento encerrado.
        if (prontuario.Situacao == ProntuarioSituacao.Encerrado)
        {
            prontuario.Situacao = ProntuarioSituacao.EmAcompanhamento;
            prontuario.MotivoEncerramento = null;
            prontuario.EncerradoEm = null;
        }

        prontuario.AtualizadoEm = agora;
        assistida.EncaminhadaAssistenciaSocialEm ??= agora;
        if (request.EncaminharJuridico) assistida.EncaminhadaJuridicoEm ??= agora;
        if (request.EncaminharPsicologia) assistida.EncaminhadaPsicologiaEm ??= agora;

        var numero = novo
            ? 1
            : (await _dbContext.AtendimentosSociais.Where(a => a.ProntuarioSocialId == prontuario.Id).MaxAsync(a => (int?)a.Numero, cancellationToken) ?? 0) + 1;

        var atendimento = new AtendimentoSocial
        {
            Prontuario = prontuario,
            Numero = numero,
            RealizadoEm = realizadoEm,
            ProfissionalUserId = usuario.UserId,
            ProfissionalIdentificador = usuario.Identificador,
            ProfissionalNome = usuario.Nome,
            ProfissionalCress = cress,
            Tipo = request.Tipo,
            TipoOutro = request.Tipo == "outro" ? Limitar(request.TipoOutro?.Trim(), 120) : null,
            Relato = request.Relato.Trim(),
            Providencias = Limitar(request.Providencias?.Trim(), 1000),
            CriadoEm = agora
        };
        _dbContext.AtendimentosSociais.Add(atendimento);

        await SalvarComNumeroAsync(prontuario, cancellationToken);

        await _auditoria.RegistrarAsync(
            "SOCIAL_ATENDIMENTO_REGISTRADO",
            "ProntuarioSocial",
            prontuario.Id.ToString(CultureInfo.InvariantCulture),
            $"{usuario.Identificador} registrou o atendimento {atendimento.Numero} do prontuário social {prontuario.Numero}.");

        foreach (var area in new[] { (request.EncaminharJuridico, "Jurídico"), (request.EncaminharPsicologia, "Psicologia") }.Where(x => x.Item1))
        {
            await _auditoria.RegistrarAsync(
                "ASSISTIDA_ENCAMINHADA",
                "Assistida",
                assistida.Id.ToString(CultureInfo.InvariantCulture),
                $"{usuario.Identificador} encaminhou a assistida #{assistida.Id} para {area.Item2}.");
        }

        var snapshot = await _snapshot.PersistAsync("social_atendimento", CancellationToken.None);

        return Ok(new AtendimentoSocialRegistradoResponse
        {
            AtendimentoId = atendimento.Id,
            NumeroProntuario = prontuario.Numero,
            NumeroAtendimento = atendimento.Numero,
            RealizadoEm = atendimento.RealizadoEm,
            PodeDeclaracao = ServicoSocialCatalogo.TiposComComparecimento.Contains(atendimento.Tipo),
            AvisoSnapshot = snapshot.AvisoSnapshot
        });
    }

    [HttpPost("atendimentos/{atendimentoId:int}/adendos")]
    public async Task<ActionResult<AdendoSocialResponse>> RegistrarAdendo(
        int atendimentoId,
        NovoAdendoPsicologiaRequest request,
        CancellationToken cancellationToken)
    {
        var usuario = await _assistidas.ObterUsuarioAsync(User, cancellationToken);
        if (usuario is null) return Unauthorized();

        var texto = request.Texto?.Trim() ?? string.Empty;
        if (texto.Length < 3 || texto.Length > 2000)
        {
            return BadRequest(new { mensagem = "O adendo deve ter entre 3 e 2000 caracteres." });
        }

        var atendimento = await _dbContext.AtendimentosSociais
            .Include(a => a.Prontuario).ThenInclude(p => p!.Responsaveis)
            .SingleOrDefaultAsync(a => a.Id == atendimentoId, cancellationToken);
        if (atendimento?.Prontuario is null) return NotFound(new { mensagem = "Atendimento não encontrado." });

        var acesso = await _acesso.AvaliarAsync(atendimento.Prontuario, usuario, cancellationToken);
        if (!acesso.PodeVerConteudo) return Proibido("Você não tem acesso ao conteúdo deste prontuário.");

        var adendo = new AtendimentoSocialAdendo
        {
            AtendimentoSocialId = atendimento.Id,
            Texto = texto,
            AutorUserId = usuario.UserId,
            AutorIdentificador = usuario.Identificador,
            AutorNome = usuario.Nome,
            CriadoEm = DateTime.UtcNow
        };
        _dbContext.AtendimentosSociaisAdendos.Add(adendo);
        atendimento.Prontuario.AtualizadoEm = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditoria.RegistrarAsync(
            "SOCIAL_ADENDO_REGISTRADO",
            "ProntuarioSocial",
            atendimento.Prontuario.Id.ToString(CultureInfo.InvariantCulture),
            $"{usuario.Identificador} acrescentou um adendo ao atendimento {atendimento.Numero} do prontuário social {atendimento.Prontuario.Numero}.");

        await _snapshot.PersistAsync("social_adendo", CancellationToken.None);
        return Ok(MapearAdendo(adendo));
    }

    [HttpGet("atendimentos/{atendimentoId:int}/declaracao")]
    public async Task<ActionResult<DeclaracaoComparecimentoSocialResponse>> Declaracao(int atendimentoId, CancellationToken cancellationToken)
    {
        var usuario = await _assistidas.ObterUsuarioAsync(User, cancellationToken);
        if (usuario is null) return Unauthorized();

        var atendimento = await _dbContext.AtendimentosSociais.AsNoTracking()
            .Include(a => a.Prontuario).ThenInclude(p => p!.Responsaveis)
            .Include(a => a.Prontuario).ThenInclude(p => p!.Assistida)
            .SingleOrDefaultAsync(a => a.Id == atendimentoId, cancellationToken);
        if (atendimento?.Prontuario?.Assistida is null) return NotFound(new { mensagem = "Atendimento não encontrado." });

        var acesso = await _acesso.AvaliarAsync(atendimento.Prontuario, usuario, cancellationToken);
        if (!acesso.PodeVerConteudo) return Proibido("Você não tem acesso a este prontuário.");

        if (!ServicoSocialCatalogo.TiposComComparecimento.Contains(atendimento.Tipo))
        {
            return BadRequest(new { mensagem = "A declaração é emitida só para atendimentos em que a assistida esteve na Casa." });
        }

        await _auditoria.RegistrarAsync(
            "SOCIAL_DECLARACAO_EMITIDA",
            "ProntuarioSocial",
            atendimento.Prontuario.Id.ToString(CultureInfo.InvariantCulture),
            $"{usuario.Identificador} emitiu declaração de comparecimento do atendimento {atendimento.Numero} do prontuário social {atendimento.Prontuario.Numero}.");

        return Ok(new DeclaracaoComparecimentoSocialResponse
        {
            AssistidaNome = atendimento.Prontuario.Assistida.Nome,
            Data = atendimento.RealizadoEm,
            ProfissionalNome = atendimento.ProfissionalNome,
            ProfissionalIdentificador = atendimento.ProfissionalIdentificador,
            ProfissionalCress = atendimento.ProfissionalCress
        });
    }

    [HttpPost("prontuarios/{assistidaId:int}/encerrar")]
    public async Task<IActionResult> Encerrar(int assistidaId, EncerrarProntuarioRequest request, CancellationToken cancellationToken)
    {
        var usuario = await _assistidas.ObterUsuarioAsync(User, cancellationToken);
        if (usuario is null) return Unauthorized();

        if (!ServicoSocialCatalogo.MotivosEncerramento.Contains(request.Motivo))
        {
            return BadRequest(new { mensagem = "Escolha o motivo do encerramento." });
        }

        var (prontuario, acesso) = await CarregarProntuarioComAcessoAsync(assistidaId, usuario, rastrear: true, cancellationToken);
        if (prontuario is null) return NotFound(new { mensagem = "Prontuário não encontrado." });
        if (!acesso.PodeVerConteudo) return Proibido("Somente quem cuida do caso pode encerrar o acompanhamento.");

        prontuario.Situacao = ProntuarioSituacao.Encerrado;
        prontuario.MotivoEncerramento = request.Motivo;
        prontuario.EncerradoEm = DateTime.UtcNow;
        prontuario.AtualizadoEm = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditoria.RegistrarAsync(
            "SOCIAL_ACOMPANHAMENTO_ENCERRADO",
            "ProntuarioSocial",
            prontuario.Id.ToString(CultureInfo.InvariantCulture),
            $"{usuario.Identificador} encerrou o acompanhamento do prontuário social {prontuario.Numero} (motivo: {request.Motivo}).");

        var snapshot = await _snapshot.PersistAsync("social_encerramento", CancellationToken.None);
        return Ok(new { mensagem = "Acompanhamento encerrado.", avisoSnapshot = snapshot.AvisoSnapshot });
    }

    [HttpPost("prontuarios/{assistidaId:int}/responsaveis")]
    public async Task<IActionResult> AtribuirResponsavel(
        int assistidaId,
        AtribuirResponsavelPsicologiaRequest request,
        CancellationToken cancellationToken)
    {
        var usuario = await _assistidas.ObterUsuarioAsync(User, cancellationToken);
        if (usuario is null) return Unauthorized();

        var (prontuario, acesso) = await CarregarProntuarioComAcessoAsync(assistidaId, usuario, rastrear: true, cancellationToken);
        if (prontuario is null) return NotFound(new { mensagem = "Abra o prontuário antes de atribuir responsáveis." });
        if (!acesso.PodeAtribuir) return Proibido("Você não pode alterar as responsáveis deste prontuário.");

        var profissional = await _dbContext.Users.AsNoTracking()
            .SingleOrDefaultAsync(u => u.Id == request.UserId && u.Ativo, cancellationToken);
        if (profissional is null || !AssistidasService.AtuaComoServicoSocial(profissional.Perfil.ToLowerInvariant()))
        {
            return BadRequest(new { mensagem = "Escolha uma profissional ativa do Serviço Social." });
        }

        if (prontuario.Responsaveis.Any(r => r.UserId == profissional.Id))
        {
            return Ok(new { mensagem = "Esta profissional já é responsável." });
        }

        prontuario.Responsaveis.Add(new ProntuarioSocialResponsavel
        {
            UserId = profissional.Id,
            Identificador = profissional.IdentificadorFuncionario,
            Nome = profissional.NomeCompleto,
            Cress = profissional.Cress,
            AtribuidoPorIdentificador = usuario.Identificador,
            AtribuidoEm = DateTime.UtcNow
        });
        prontuario.AtualizadoEm = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditoria.RegistrarAsync(
            "SOCIAL_RESPONSAVEL_ATRIBUIDO",
            "ProntuarioSocial",
            prontuario.Id.ToString(CultureInfo.InvariantCulture),
            $"{usuario.Identificador} atribuiu {profissional.IdentificadorFuncionario} como responsável pelo prontuário social {prontuario.Numero}.");

        var snapshot = await _snapshot.PersistAsync("social_responsavel", CancellationToken.None);
        return Ok(new { mensagem = "Responsável atribuída.", avisoSnapshot = snapshot.AvisoSnapshot });
    }

    [HttpDelete("prontuarios/{assistidaId:int}/responsaveis/{userId}")]
    public async Task<IActionResult> RemoverResponsavel(int assistidaId, string userId, CancellationToken cancellationToken)
    {
        var usuario = await _assistidas.ObterUsuarioAsync(User, cancellationToken);
        if (usuario is null) return Unauthorized();

        var (prontuario, acesso) = await CarregarProntuarioComAcessoAsync(assistidaId, usuario, rastrear: true, cancellationToken);
        if (prontuario is null) return NotFound(new { mensagem = "Prontuário não encontrado." });
        if (!acesso.PodeAtribuir) return Proibido("Você não pode alterar as responsáveis deste prontuário.");

        var responsavel = prontuario.Responsaveis.SingleOrDefault(r => r.UserId == userId);
        if (responsavel is null) return NotFound(new { mensagem = "Responsável não encontrada." });

        prontuario.Responsaveis.Remove(responsavel);
        prontuario.AtualizadoEm = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditoria.RegistrarAsync(
            "SOCIAL_RESPONSAVEL_REMOVIDO",
            "ProntuarioSocial",
            prontuario.Id.ToString(CultureInfo.InvariantCulture),
            $"{usuario.Identificador} removeu {responsavel.Identificador} das responsáveis pelo prontuário social {prontuario.Numero}.");

        var snapshot = await _snapshot.PersistAsync("social_responsavel", CancellationToken.None);
        return Ok(new { mensagem = "Responsável removida.", avisoSnapshot = snapshot.AvisoSnapshot });
    }

    [HttpPut("prontuarios/{assistidaId:int}/visibilidade")]
    public async Task<IActionResult> AlterarVisibilidade(
        int assistidaId,
        AlterarVisibilidadeProntuarioRequest request,
        CancellationToken cancellationToken)
    {
        var usuario = await _assistidas.ObterUsuarioAsync(User, cancellationToken);
        if (usuario is null) return Unauthorized();

        if (!ProntuarioVisibilidade.Todos.Contains(request.Visibilidade))
        {
            return BadRequest(new { mensagem = "Visibilidade inválida." });
        }

        var (prontuario, acesso) = await CarregarProntuarioComAcessoAsync(assistidaId, usuario, rastrear: true, cancellationToken);
        if (prontuario is null) return NotFound(new { mensagem = "Prontuário não encontrado." });
        if (!acesso.SouResponsavel) return Proibido("Somente as responsáveis escolhem a visibilidade.");

        prontuario.Visibilidade = request.Visibilidade;
        prontuario.AtualizadoEm = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditoria.RegistrarAsync(
            "SOCIAL_VISIBILIDADE_ALTERADA",
            "ProntuarioSocial",
            prontuario.Id.ToString(CultureInfo.InvariantCulture),
            $"{usuario.Identificador} definiu a visibilidade do prontuário social {prontuario.Numero} como \"{request.Visibilidade}\".");

        var snapshot = await _snapshot.PersistAsync("social_visibilidade", CancellationToken.None);
        return Ok(new { mensagem = "Visibilidade atualizada.", avisoSnapshot = snapshot.AvisoSnapshot });
    }

    [HttpPost("prontuarios/{assistidaId:int}/acesso-emergencia")]
    public async Task<IActionResult> PedirAcessoEmergencia(
        int assistidaId,
        AcessoEmergenciaPsicologiaRequest request,
        CancellationToken cancellationToken)
    {
        var usuario = await _assistidas.ObterUsuarioAsync(User, cancellationToken);
        if (usuario is null) return Unauthorized();
        if (!AssistidasService.AtuaComoServicoSocial(usuario.Perfil)) return Proibido("Somente o Serviço Social pode pedir acesso de emergência.");

        var motivo = request.Motivo?.Trim() ?? string.Empty;
        if (motivo.Length < 10 || motivo.Length > 500)
        {
            return BadRequest(new { mensagem = "Explique o motivo do acesso (entre 10 e 500 caracteres)." });
        }

        var (prontuario, acesso) = await CarregarProntuarioComAcessoAsync(assistidaId, usuario, rastrear: false, cancellationToken);
        if (prontuario is null) return NotFound(new { mensagem = "Prontuário não encontrado." });
        if (acesso.PodeVerConteudo) return Ok(new { mensagem = "Você já tem acesso a este prontuário." });

        var registro = new ProntuarioSocialAcessoEmergencia
        {
            ProntuarioSocialId = prontuario.Id,
            UserId = usuario.UserId,
            Identificador = usuario.Identificador,
            Nome = usuario.Nome,
            Motivo = motivo,
            CriadoEm = DateTime.UtcNow,
            ExpiraEm = DateTime.UtcNow.Add(ServicoSocialAcessoService.DuracaoAcessoEmergencia)
        };
        _dbContext.ProntuariosSociaisAcessosEmergencia.Add(registro);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditoria.RegistrarAsync(
            "SOCIAL_ACESSO_EMERGENCIA",
            "ProntuarioSocial",
            prontuario.Id.ToString(CultureInfo.InvariantCulture),
            $"{usuario.Identificador} usou acesso de emergência ao prontuário social {prontuario.Numero}. Motivo: {Limitar(motivo, 300)}");

        await AvisarResponsaveisAsync(prontuario, usuario);
        var snapshot = await _snapshot.PersistAsync("social_acesso_emergencia", CancellationToken.None);

        return Ok(new { mensagem = "Acesso liberado por 12 horas. As responsáveis foram avisadas.", expiraEm = registro.ExpiraEm, avisoSnapshot = snapshot.AvisoSnapshot });
    }

    [HttpGet("profissionais")]
    public async Task<ActionResult<List<ResponsavelSocialResponse>>> ListarProfissionais(CancellationToken cancellationToken)
    {
        var perfis = new[] { PerfisAcesso.AssistenteSocial, PerfisAcesso.Equipe };
        var profissionais = await _dbContext.Users.AsNoTracking()
            .Where(u => u.Ativo && perfis.Contains(u.Perfil.ToLower()))
            .OrderBy(u => u.NomeCompleto)
            .Select(u => new ResponsavelSocialResponse { UserId = u.Id, Identificador = u.IdentificadorFuncionario, Nome = u.NomeCompleto, Cress = u.Cress })
            .ToListAsync(cancellationToken);

        return Ok(profissionais);
    }

    /// <summary>
    /// A própria assistente social informa ou corrige o CRESS, que sai na declaração e nos documentos.
    /// Não bloqueia o trabalho: sem CRESS a tela só mostra um aviso.
    /// </summary>
    [HttpPut("meu-cress")]
    public async Task<IActionResult> AtualizarMeuCress(MeuCressRequest request, CancellationToken cancellationToken)
    {
        var usuario = await _assistidas.ObterUsuarioAsync(User, cancellationToken);
        if (usuario is null) return Unauthorized();
        if (!AssistidasService.AtuaComoServicoSocial(usuario.Perfil)) return Proibido("Somente o Serviço Social informa CRESS.");

        var cress = request.Cress?.Trim() ?? string.Empty;
        if (cress.Length < 4 || cress.Length > 60 || !cress.Any(char.IsDigit))
        {
            return BadRequest(new { mensagem = "Informe o CRESS com região e número (ex.: CRESS 9ª Região nº 00000)." });
        }

        var conta = await _dbContext.Users.SingleAsync(u => u.Id == usuario.UserId, cancellationToken);
        conta.Cress = cress;

        // As responsabilidades atuais passam a mostrar o CRESS novo; atendimentos já salvos mantêm o que tinham.
        var responsavel = await _dbContext.ProntuariosSociaisResponsaveis.Where(r => r.UserId == usuario.UserId).ToListAsync(cancellationToken);
        responsavel.ForEach(r => r.Cress = cress);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditoria.RegistrarAsync("SOCIAL_CRESS_ATUALIZADO", "ApplicationUser", usuario.UserId, $"{usuario.Identificador} atualizou o próprio CRESS.");
        var snapshot = await _snapshot.PersistAsync("social_cress", CancellationToken.None);
        return Ok(new { mensagem = "CRESS salvo.", cress, avisoSnapshot = snapshot.AvisoSnapshot });
    }

    /// <summary>Contagens anônimas: nenhum nome sai deste endpoint. É o que a coordenação vê.</summary>
    [HttpGet("indicadores")]
    public async Task<ActionResult<IndicadoresServicoSocialResponse>> Indicadores(CancellationToken cancellationToken)
    {
        var agora = DateTime.UtcNow;
        var inicioMes = new DateTime(agora.Year, agora.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var prontuarios = await CarregarProntuariosAsync(cancellationToken);
        var encaminhadas = await _dbContext.Assistidas.AsNoTracking()
            .Where(a => a.Ativa && a.EncaminhadaAssistenciaSocialEm != null)
            .Select(a => a.Id)
            .ToListAsync(cancellationToken);
        var atendimentosMes = await _dbContext.AtendimentosSociais.AsNoTracking()
            .Where(a => a.RealizadoEm >= inicioMes)
            .Select(a => a.Tipo)
            .ToListAsync(cancellationToken);
        var rede = await _dbContext.EncaminhamentosRede.AsNoTracking()
            .Select(e => new { e.CriadoEm, e.Status })
            .ToListAsync(cancellationToken);

        return Ok(new IndicadoresServicoSocialResponse
        {
            Fila = encaminhadas.Count(id => !prontuarios.TryGetValue(id, out var p) || p.Atendimentos.Count == 0),
            EmAcompanhamento = prontuarios.Values.Count(p => p.Atendimentos.Count > 0 && p.Situacao == ProntuarioSituacao.EmAcompanhamento),
            Encerrados = prontuarios.Values.Count(p => p.Situacao == ProntuarioSituacao.Encerrado),
            AtendimentosNoMes = atendimentosMes.Count,
            ComVulnerabilidade = prontuarios.Values.Count(p => p.Situacao == ProntuarioSituacao.EmAcompanhamento && !string.IsNullOrWhiteSpace(p.VulnerabilidadesAtuais)),
            EncaminhamentosRedeNoMes = rede.Count(e => e.CriadoEm >= inicioMes),
            EncaminhamentosSemRetorno = rede.Count(e => e.Status is EncaminhamentoRedeStatus.Enviado or EncaminhamentoRedeStatus.SemRetorno),
            AtendimentosPorTipoNoMes = atendimentosMes.GroupBy(t => t).ToDictionary(g => g.Key, g => g.Count())
        });
    }

    // ------------------------------------------------------------------ apoio

    /// <summary>Carrega o prontuário da assistida (com responsáveis) e avalia o acesso de quem pede.</summary>
    private async Task<(ProntuarioSocial? Prontuario, AcessoProntuarioSocial Acesso)> CarregarProntuarioComAcessoAsync(
        int assistidaId,
        UsuarioAtuante usuario,
        bool rastrear,
        CancellationToken cancellationToken)
    {
        var consulta = _dbContext.ProntuariosSociais.Include(p => p.Responsaveis).Where(p => p.AssistidaId == assistidaId && p.Assistida!.Ativa);
        var prontuario = await (rastrear ? consulta : consulta.AsNoTracking()).SingleOrDefaultAsync(cancellationToken);
        return (prontuario, await _acesso.AvaliarAsync(prontuario, usuario, cancellationToken));
    }

    private Task<string?> CressAsync(string userId, CancellationToken cancellationToken) =>
        _dbContext.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.Cress).SingleOrDefaultAsync(cancellationToken);

    private static ProntuarioSocial NovoProntuario(Assistida assistida, UsuarioAtuante usuario, DateTime agora, string? cress)
    {
        var prontuario = new ProntuarioSocial { AssistidaId = assistida.Id, Ano = DataLocal(agora).Year, CriadoEm = agora, AtualizadoEm = agora };
        prontuario.Responsaveis.Add(new ProntuarioSocialResponsavel
        {
            UserId = usuario.UserId,
            Identificador = usuario.Identificador,
            Nome = usuario.Nome,
            Cress = cress,
            AtribuidoPorIdentificador = usuario.Identificador,
            AtribuidoEm = agora
        });
        return prontuario;
    }

    /// <summary>Salva; se o prontuário é novo, gera o número SOC-AAAA-NNNNNN sequencial por ano no servidor.</summary>
    private async Task SalvarComNumeroAsync(ProntuarioSocial prontuario, CancellationToken cancellationToken)
    {
        var novo = prontuario.Id == 0;
        if (novo && _dbContext.Entry(prontuario).State == EntityState.Detached)
        {
            _dbContext.ProntuariosSociais.Add(prontuario);
        }

        for (var tentativa = 1; ; tentativa++)
        {
            if (novo)
            {
                var ultimo = await _dbContext.ProntuariosSociais
                    .Where(p => p.Ano == prontuario.Ano && p.Id != prontuario.Id)
                    .MaxAsync(p => (int?)p.Sequencial, cancellationToken) ?? 0;
                prontuario.Sequencial = ultimo + 1;
                prontuario.Numero = $"SOC-{prontuario.Ano}-{prontuario.Sequencial:D6}";
            }

            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (DbUpdateException) when (novo && tentativa < 3)
            {
                _logger.LogWarning("Conflito ao numerar o prontuário social; tentando novamente.");
            }
        }
    }

    private async Task<Dictionary<int, ProntuarioSocial>> CarregarProntuariosAsync(CancellationToken cancellationToken)
    {
        var prontuarios = await _dbContext.ProntuariosSociais.AsNoTracking()
            .Include(p => p.Responsaveis)
            .Include(p => p.Atendimentos)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        return prontuarios.ToDictionary(p => p.AssistidaId);
    }

    private static SocialAssistidaResumoResponse MontarResumo(Assistida a, ProntuarioSocial? prontuario, UsuarioAtuante usuario)
    {
        var ultimo = prontuario?.Atendimentos.OrderByDescending(x => x.Numero).FirstOrDefault();
        var souResponsavel = prontuario?.Responsaveis.Any(r => r.UserId == usuario.UserId) == true;
        var podeVerDatas = prontuario is null || prontuario.Visibilidade == ProntuarioVisibilidade.Equipe || souResponsavel;

        return new SocialAssistidaResumoResponse
        {
            AssistidaId = a.Id,
            Nome = a.Nome,
            CpfMascarado = AssistidasService.MascararCpf(a.Cpf),
            Telefone = a.Telefone,
            SemTelefoneSeguro = a.SemTelefoneSeguro,
            ContatoSeguro = a.ContatoSeguro,
            AcessoRestrito = a.AcessoRestrito,
            Origem = a.Origem == AssistidaOrigem.Psicologia ? "equipe_tecnica" : a.Origem,
            EncaminhadaServicoSocialEm = a.EncaminhadaAssistenciaSocialEm,
            ProntuarioId = prontuario?.Id,
            NumeroProntuario = prontuario?.Numero,
            Situacao = prontuario is null || prontuario.Atendimentos.Count == 0 ? null : prontuario.Situacao,
            Visibilidade = prontuario?.Visibilidade,
            Responsaveis = prontuario?.Responsaveis.OrderBy(r => r.AtribuidoEm).Select(MapearResponsavel).ToList() ?? [],
            SouResponsavel = souResponsavel,
            TotalAtendimentos = prontuario?.Atendimentos.Count ?? 0,
            UltimoAtendimentoEm = podeVerDatas ? ultimo?.RealizadoEm : null,
            Vulnerabilidade = prontuario?.Situacao == ProntuarioSituacao.EmAcompanhamento && !string.IsNullOrWhiteSpace(prontuario.VulnerabilidadesAtuais)
        };
    }

    internal static ResponsavelSocialResponse MapearResponsavel(ProntuarioSocialResponsavel r) =>
        new() { UserId = r.UserId, Identificador = r.Identificador, Nome = r.Nome, Cress = r.Cress };

    private static AdendoSocialResponse MapearAdendo(AtendimentoSocialAdendo adendo) => new()
    {
        Id = adendo.Id,
        Texto = adendo.Texto,
        AutorNome = adendo.AutorNome,
        AutorIdentificador = adendo.AutorIdentificador,
        CriadoEm = adendo.CriadoEm
    };

    private static AtendimentoSocialResponse MapearAtendimento(AtendimentoSocial a, bool conteudo, int totalAnexos)
    {
        var resposta = new AtendimentoSocialResponse
        {
            Id = a.Id,
            Numero = a.Numero,
            RealizadoEm = a.RealizadoEm,
            CriadoEm = a.CriadoEm,
            ProfissionalNome = a.ProfissionalNome,
            ProfissionalIdentificador = a.ProfissionalIdentificador,
            ProfissionalCress = a.ProfissionalCress,
            ConteudoOculto = !conteudo
        };

        if (!conteudo) return resposta;

        resposta.Tipo = a.Tipo;
        resposta.TipoOutro = a.TipoOutro;
        resposta.Relato = a.Relato;
        resposta.Providencias = a.Providencias;
        resposta.PodeDeclaracao = ServicoSocialCatalogo.TiposComComparecimento.Contains(a.Tipo);
        resposta.TotalAnexos = totalAnexos;
        resposta.Adendos = a.Adendos.OrderBy(x => x.CriadoEm).Select(MapearAdendo).ToList();
        return resposta;
    }

    internal static AnexoSocialResponse MapearAnexo(AnexoSocial a, IReadOnlyDictionary<int, int> numerosAtendimento, bool podeExcluir) => new()
    {
        Id = a.Id,
        Tipo = a.Tipo,
        Descricao = a.Descricao,
        NomeArquivo = a.NomeArquivo,
        ContentType = a.ContentType,
        TamanhoBytes = a.TamanhoBytes,
        Visibilidade = a.Visibilidade,
        AtendimentoNumero = a.AtendimentoSocialId is { } id && numerosAtendimento.TryGetValue(id, out var numero) ? numero : null,
        AutorNome = a.AutorNome,
        AutorIdentificador = a.AutorIdentificador,
        CriadoEm = a.CriadoEm,
        PodeExcluir = podeExcluir
    };

    private static (DateTime RealizadoEm, string? Erro) ValidarAtendimento(NovoAtendimentoSocialRequest request, DateTime agora)
    {
        if (!ServicoSocialCatalogo.TiposAtendimento.Contains(request.Tipo)) return (default, "Escolha o tipo de atendimento.");
        if (request.Tipo == "outro" && string.IsNullOrWhiteSpace(request.TipoOutro)) return (default, "Descreva o tipo de atendimento.");

        var relato = request.Relato?.Trim() ?? string.Empty;
        if (relato.Length < 15) return (default, "Descreva o atendimento com mais detalhes (mínimo de 15 caracteres).");
        if (relato.Length > 5000) return (default, "O relato excedeu 5000 caracteres.");
        if ((request.Providencias?.Trim().Length ?? 0) > 1000) return (default, "As providências excederam 1000 caracteres.");

        if (string.IsNullOrWhiteSpace(request.RealizadoEm)) return (agora, null);

        if (!DateTime.TryParse(request.RealizadoEm, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
        {
            return (default, "Data e hora do atendimento inválidas.");
        }

        var utc = LocalParaUtc(local);
        if (utc > agora.AddMinutes(10)) return (default, "A data do atendimento não pode estar no futuro.");
        if (utc < agora.AddYears(-1)) return (default, "A data do atendimento é antiga demais (mais de 1 ano).");
        return (utc, null);
    }

    private async Task AvisarResponsaveisAsync(ProntuarioSocial prontuario, UsuarioAtuante usuario)
    {
        var ids = prontuario.Responsaveis.Select(r => r.UserId).ToList();
        var emails = await _dbContext.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id) && u.Email != null)
            .Select(u => u.Email!)
            .ToListAsync();

        // O e-mail não leva nome da assistida nem o motivo: os detalhes ficam na auditoria.
        var corpo = $"<p>A profissional de ID {System.Net.WebUtility.HtmlEncode(usuario.Identificador)} usou o acesso de emergência "
            + $"no prontuário social {System.Net.WebUtility.HtmlEncode(prontuario.Numero)}, que está sob sua responsabilidade.</p>"
            + "<p>O acesso vale por 12 horas e ficou registrado no histórico de ações. Entre no sistema para ver os detalhes.</p>";

        foreach (var email in emails)
        {
            try
            {
                await _emailService.EnviarAsync(email, "Acesso de emergência a um prontuário social - Casa da Mulher", corpo, "ServicoSocialAcessoEmergencia");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Não foi possível avisar uma responsável sobre o acesso de emergência ao prontuário social {ProntuarioId}.", prontuario.Id);
            }
        }
    }

    private ObjectResult Proibido(string mensagem) => StatusCode(StatusCodes.Status403Forbidden, new { mensagem });

    private static List<string> Separar(string? csv) =>
        string.IsNullOrWhiteSpace(csv) ? [] : csv.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();

    private static string? Juntar(IEnumerable<string> valores)
    {
        var lista = valores.Distinct().ToList();
        return lista.Count == 0 ? null : string.Join(',', lista);
    }

    /// <summary>O navegador envia datas no horário de Brasília; o banco guarda em UTC.</summary>
    private static DateTime LocalParaUtc(DateTime local) => RelatoriosService.ParaUtc(local);

    private static DateOnly DataLocal(DateTime utc) => RelatoriosService.Local(utc);

    private static string? Limitar(string? valor, int maximo)
    {
        if (string.IsNullOrEmpty(valor)) return null;
        return valor.Length <= maximo ? valor : valor[..maximo];
    }
}
