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
/// Módulo Psicologia: fila de encaminhadas, prontuário com número do servidor, sessões,
/// classificação de risco, adendos, responsáveis, visibilidade, acesso de emergência,
/// encerramento e declaração de comparecimento.
/// Sigilo: por padrão só a psicóloga responsável vê o conteúdo; a coordenação (ADM)
/// vê apenas números, nunca o conteúdo nem os metadados das sessões.
/// </summary>
[ApiController]
[Route("api/psicologia")]
[Authorize(Policy = PoliticasAcesso.AcessoPsicologia)]
public class PsicologiaController : ControllerBase
{
    private static readonly TimeSpan DuracaoAcessoEmergencia = TimeSpan.FromHours(12);

    private readonly AppDbContext _dbContext;
    private readonly AssistidasService _assistidas;
    private readonly IAuditoriaService _auditoria;
    private readonly SecuritySnapshotPersistenceService _snapshot;
    private readonly IEmailService _emailService;
    private readonly ILogger<PsicologiaController> _logger;

    public PsicologiaController(
        AppDbContext dbContext,
        AssistidasService assistidas,
        IAuditoriaService auditoria,
        SecuritySnapshotPersistenceService snapshot,
        IEmailService emailService,
        ILogger<PsicologiaController> logger)
    {
        _dbContext = dbContext;
        _assistidas = assistidas;
        _auditoria = auditoria;
        _snapshot = snapshot;
        _emailService = emailService;
        _logger = logger;
    }

    /// <summary>
    /// escopo: fila (encaminhadas sem sessão), minhas (sou responsável),
    /// prontuarios (padrão: com prontuário ou encaminhadas). Com busca, procura em todo o cadastro.
    /// </summary>
    [HttpGet("assistidas")]
    public async Task<ActionResult<List<PsicologiaAssistidaResumoResponse>>> Listar(
        [FromQuery] string? escopo,
        [FromQuery] string? busca,
        CancellationToken cancellationToken)
    {
        var usuario = await _assistidas.ObterUsuarioAsync(User, cancellationToken);
        if (usuario is null) return Unauthorized();

        if (!AssistidasService.AtuaComoPsicologia(usuario.Perfil))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { mensagem = "A coordenação acompanha a Psicologia apenas pelos indicadores." });
        }

        var assistidas = await _dbContext.Assistidas.AsNoTracking()
            .Where(a => a.Ativa)
            .ToListAsync(cancellationToken);

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
                "fila" => filtradas.Where(a => a.EncaminhadaPsicologiaEm is not null
                    && (!prontuarios.TryGetValue(a.Id, out var p) || p.Sessoes.Count == 0)),
                "minhas" => filtradas.Where(a => prontuarios.TryGetValue(a.Id, out var p)
                    && p.Responsaveis.Any(r => r.UserId == usuario.UserId)),
                _ => filtradas.Where(a => a.EncaminhadaPsicologiaEm is not null || prontuarios.ContainsKey(a.Id))
            };
        }

        var resultado = filtradas
            .Select(a => MontarResumo(a, prontuarios.GetValueOrDefault(a.Id), usuario))
            .OrderByDescending(r => r.RiscoElevado)
            .ThenBy(r => escopo == "fila" ? (r.EncaminhadaPsicologiaEm ?? DateTime.MaxValue).Ticks : 0)
            .ThenBy(r => r.Nome, StringComparer.Create(new CultureInfo("pt-BR"), true))
            .ToList();

        return Ok(resultado);
    }

    [HttpGet("prontuarios/{assistidaId:int}")]
    public async Task<ActionResult<ProntuarioPsicologicoDetalheResponse>> ObterProntuario(int assistidaId, CancellationToken cancellationToken)
    {
        var usuario = await _assistidas.ObterUsuarioAsync(User, cancellationToken);
        if (usuario is null) return Unauthorized();

        if (!AssistidasService.AtuaComoPsicologia(usuario.Perfil))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { mensagem = "A coordenação acompanha a Psicologia apenas pelos indicadores." });
        }

        var assistida = await _dbContext.Assistidas.AsNoTracking()
            .SingleOrDefaultAsync(a => a.Id == assistidaId && a.Ativa, cancellationToken);
        if (assistida is null) return NotFound(new { mensagem = "Assistida não encontrada." });

        var prontuario = await _dbContext.ProntuariosPsicologicos.AsNoTracking()
            .Include(p => p.Responsaveis)
            .Include(p => p.Sessoes).ThenInclude(s => s.Adendos)
            .SingleOrDefaultAsync(p => p.AssistidaId == assistidaId, cancellationToken);

        var acesso = await AvaliarAcessoAsync(prontuario, usuario, cancellationToken);
        var sessoes = prontuario?.Sessoes.OrderByDescending(s => s.Numero).ToList() ?? [];

        var emJuridico = await _dbContext.CasosJuridicos.AsNoTracking()
            .AnyAsync(c => c.AssistidaId == assistidaId && c.Atendimentos.Any() && c.Situacao != CasoJuridicoSituacao.Encerrado, cancellationToken);

        var resposta = new ProntuarioPsicologicoDetalheResponse
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
            PodeVerConteudo = acesso.PodeVerConteudo,
            PodeRegistrar = acesso.PodeVerConteudo,
            PodeAtribuir = acesso.PodeAtribuir,
            PodeAlterarVisibilidade = acesso.SouResponsavel,
            PodePedirAcessoEmergencia = prontuario is not null && !acesso.PodeVerConteudo,
            AcessoEmergenciaExpiraEm = acesso.EmergenciaExpiraEm,
            // O alerta de risco aparece para toda a equipe de Psicologia, sem o conteúdo.
            RiscoAtual = prontuario?.RiscoAtual,
            RiscoElevado = PsicologiaCatalogo.RiscoElevado(prontuario?.RiscoAtual),
            ConsentimentoRegistrado = prontuario?.ConsentimentoSigiloEm is not null,
            EmAtendimentoJuridico = emJuridico,
            TotalSessoes = sessoes.Count,
            TotalFaltas = acesso.PodeVerConteudo ? sessoes.Count(s => s.Presenca is "faltou" or "faltou_justificou") : 0,
            FaltasSeguidas = acesso.PodeVerConteudo ? sessoes.TakeWhile(s => s.Presenca is "faltou" or "faltou_justificou").Count() : 0,
            Sessoes = sessoes.Select(s => MapearSessao(s, acesso.PodeVerConteudo)).ToList()
        };

        if (!acesso.PodeVerConteudo)
        {
            resposta.Assistida.Observacoes = null;
        }

        if (acesso.PodeVerConteudo && sessoes.Count > 0)
        {
            await _auditoria.RegistrarAsync(
                "PSICOLOGIA_PRONTUARIO_VISUALIZADO",
                "ProntuarioPsicologico",
                prontuario!.Id.ToString(CultureInfo.InvariantCulture),
                $"{usuario.Identificador} abriu o prontuário psicológico {prontuario.Numero}"
                    + (acesso.EmergenciaExpiraEm is not null && !acesso.SouResponsavel && prontuario.Visibilidade == ProntuarioVisibilidade.Responsaveis
                        ? " (acesso de emergência)."
                        : "."));
        }

        return Ok(resposta);
    }

    [HttpPost("prontuarios/{assistidaId:int}/sessoes")]
    public async Task<ActionResult<SessaoRegistradaResponse>> RegistrarSessao(
        int assistidaId,
        NovaSessaoPsicologicaRequest request,
        CancellationToken cancellationToken)
    {
        var usuario = await _assistidas.ObterUsuarioAsync(User, cancellationToken);
        if (usuario is null) return Unauthorized();

        if (!AssistidasService.AtuaComoPsicologia(usuario.Perfil))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { mensagem = "Somente a Psicologia registra sessões." });
        }

        var assistida = await _dbContext.Assistidas.SingleOrDefaultAsync(a => a.Id == assistidaId && a.Ativa, cancellationToken);
        if (assistida is null) return NotFound(new { mensagem = "Assistida não encontrada." });

        var prontuario = await _dbContext.ProntuariosPsicologicos
            .Include(p => p.Responsaveis)
            .SingleOrDefaultAsync(p => p.AssistidaId == assistidaId, cancellationToken);

        var erro = ValidarSessao(request, prontuario);
        if (erro is not null) return BadRequest(new { mensagem = erro });

        var acesso = await AvaliarAcessoAsync(prontuario, usuario, cancellationToken);
        if (!acesso.PodeVerConteudo)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { mensagem = "Este prontuário está visível somente para a psicóloga responsável. Peça acesso de emergência se precisar atender." });
        }

        var crp = await _dbContext.Users.AsNoTracking()
            .Where(u => u.Id == usuario.UserId)
            .Select(u => u.Crp)
            .SingleOrDefaultAsync(cancellationToken);

        var agora = DateTime.UtcNow;
        var novo = prontuario is null;
        if (prontuario is null)
        {
            prontuario = new ProntuarioPsicologico { AssistidaId = assistidaId, Ano = ObterAnoLocal(agora), CriadoEm = agora };
            prontuario.Responsaveis.Add(new ProntuarioPsicologicoResponsavel
            {
                UserId = usuario.UserId,
                Identificador = usuario.Identificador,
                Nome = usuario.Nome,
                Crp = crp,
                AtribuidoPorIdentificador = usuario.Identificador,
                AtribuidoEm = agora
            });
            _dbContext.ProntuariosPsicologicos.Add(prontuario);
        }

        var compareceu = request.Presenca == "compareceu";

        // Uma nova sessão reabre um acompanhamento encerrado.
        if (prontuario.Situacao == ProntuarioSituacao.Encerrado)
        {
            prontuario.Situacao = ProntuarioSituacao.EmAcompanhamento;
            prontuario.MotivoEncerramento = null;
            prontuario.EncerradoEm = null;
        }

        if (compareceu && request.ConsentimentoSigilo && prontuario.ConsentimentoSigiloEm is null)
        {
            prontuario.ConsentimentoSigiloEm = agora;
            prontuario.ConsentimentoPorIdentificador = usuario.Identificador;
        }

        if (!string.IsNullOrWhiteSpace(request.Risco))
        {
            prontuario.RiscoAtual = request.Risco;
            prontuario.RiscoAtualizadoEm = agora;
        }

        prontuario.AtualizadoEm = agora;
        assistida.EncaminhadaPsicologiaEm ??= agora;

        if (request.EncaminharJuridico)
        {
            assistida.EncaminhadaJuridicoEm ??= agora;
        }

        var numeroSessao = novo
            ? 1
            : (await _dbContext.SessoesPsicologicas.Where(s => s.ProntuarioPsicologicoId == prontuario.Id).MaxAsync(s => (int?)s.Numero, cancellationToken) ?? 0) + 1;

        var sessao = new SessaoPsicologica
        {
            Prontuario = prontuario,
            Numero = numeroSessao,
            ProfissionalUserId = usuario.UserId,
            ProfissionalIdentificador = usuario.Identificador,
            ProfissionalNome = usuario.Nome,
            ProfissionalCrp = crp,
            TipoAtendimento = request.TipoAtendimento,
            Presenca = request.Presenca,
            Modalidade = compareceu ? request.Modalidade : null,
            Demanda = Limitar(request.Demanda?.Trim(), 500),
            Evolucao = Limitar(request.Evolucao?.Trim(), 5000),
            Plano = Limitar(request.Plano?.Trim(), 1000),
            ProximaSessao = DateTime.TryParse(request.ProximaSessao, CultureInfo.InvariantCulture, DateTimeStyles.None, out var proxima) ? LocalParaUtc(proxima) : null,
            Risco = string.IsNullOrWhiteSpace(request.Risco) ? null : request.Risco,
            EncaminhamentosExternos = request.EncaminhamentosExternos.Count == 0 ? null : string.Join(',', request.EncaminhamentosExternos.Distinct()),
            EncaminhamentoExternoOutro = request.EncaminhamentosExternos.Contains("outro") ? Limitar(request.EncaminhamentoExternoOutro?.Trim(), 120) : null,
            CriadoEm = agora
        };
        _dbContext.SessoesPsicologicas.Add(sessao);

        // Número do prontuário sequencial por ano, gerado no servidor.
        for (var tentativa = 1; ; tentativa++)
        {
            if (novo)
            {
                var ultimo = await _dbContext.ProntuariosPsicologicos
                    .Where(p => p.Ano == prontuario.Ano)
                    .MaxAsync(p => (int?)p.Sequencial, cancellationToken) ?? 0;
                prontuario.Sequencial = ultimo + 1;
                prontuario.Numero = $"PSI-{prontuario.Ano}-{prontuario.Sequencial:D6}";
            }

            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
                break;
            }
            catch (DbUpdateException) when (tentativa < 3)
            {
                _logger.LogWarning("Conflito ao numerar o prontuário psicológico; tentando novamente.");
            }
        }

        await _auditoria.RegistrarAsync(
            "PSICOLOGIA_SESSAO_REGISTRADA",
            "ProntuarioPsicologico",
            prontuario.Id.ToString(CultureInfo.InvariantCulture),
            $"{usuario.Identificador} registrou a sessão {sessao.Numero} do prontuário {prontuario.Numero}.");

        if (request.EncaminharJuridico)
        {
            await _auditoria.RegistrarAsync(
                "ASSISTIDA_ENCAMINHADA",
                "Assistida",
                assistida.Id.ToString(CultureInfo.InvariantCulture),
                $"{usuario.Identificador} encaminhou a assistida #{assistida.Id} para Jurídico.");
        }

        var snapshot = await _snapshot.PersistAsync("psicologia_sessao", CancellationToken.None);

        return Ok(new SessaoRegistradaResponse
        {
            SessaoId = sessao.Id,
            NumeroProntuario = prontuario.Numero,
            NumeroSessao = sessao.Numero,
            CriadoEm = sessao.CriadoEm,
            Presenca = sessao.Presenca,
            AvisoSnapshot = snapshot.AvisoSnapshot
        });
    }

    [HttpPost("sessoes/{sessaoId:int}/adendos")]
    public async Task<ActionResult<AdendoPsicologiaResponse>> RegistrarAdendo(
        int sessaoId,
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

        var sessao = await _dbContext.SessoesPsicologicas
            .Include(s => s.Prontuario).ThenInclude(p => p!.Responsaveis)
            .SingleOrDefaultAsync(s => s.Id == sessaoId, cancellationToken);
        if (sessao?.Prontuario is null) return NotFound(new { mensagem = "Sessão não encontrada." });

        var acesso = await AvaliarAcessoAsync(sessao.Prontuario, usuario, cancellationToken);
        if (!acesso.PodeVerConteudo)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { mensagem = "Você não tem acesso ao conteúdo deste prontuário." });
        }

        var adendo = new SessaoPsicologicaAdendo
        {
            SessaoPsicologicaId = sessao.Id,
            Texto = texto,
            AutorUserId = usuario.UserId,
            AutorIdentificador = usuario.Identificador,
            AutorNome = usuario.Nome,
            CriadoEm = DateTime.UtcNow
        };
        _dbContext.SessoesPsicologicasAdendos.Add(adendo);
        sessao.Prontuario.AtualizadoEm = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditoria.RegistrarAsync(
            "PSICOLOGIA_ADENDO_REGISTRADO",
            "ProntuarioPsicologico",
            sessao.Prontuario.Id.ToString(CultureInfo.InvariantCulture),
            $"{usuario.Identificador} acrescentou um adendo à sessão {sessao.Numero} do prontuário {sessao.Prontuario.Numero}.");

        await _snapshot.PersistAsync("psicologia_adendo", CancellationToken.None);
        return Ok(MapearAdendo(adendo));
    }

    [HttpGet("sessoes/{sessaoId:int}/declaracao")]
    public async Task<ActionResult<DeclaracaoComparecimentoResponse>> Declaracao(int sessaoId, CancellationToken cancellationToken)
    {
        var usuario = await _assistidas.ObterUsuarioAsync(User, cancellationToken);
        if (usuario is null) return Unauthorized();

        var sessao = await _dbContext.SessoesPsicologicas.AsNoTracking()
            .Include(s => s.Prontuario).ThenInclude(p => p!.Responsaveis)
            .Include(s => s.Prontuario).ThenInclude(p => p!.Assistida)
            .SingleOrDefaultAsync(s => s.Id == sessaoId, cancellationToken);
        if (sessao?.Prontuario?.Assistida is null) return NotFound(new { mensagem = "Sessão não encontrada." });

        var acesso = await AvaliarAcessoAsync(sessao.Prontuario, usuario, cancellationToken);
        if (!acesso.PodeVerConteudo)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { mensagem = "Você não tem acesso a este prontuário." });
        }

        if (sessao.Presenca != "compareceu")
        {
            return BadRequest(new { mensagem = "A declaração só pode ser emitida para sessões em que a assistida compareceu." });
        }

        await _auditoria.RegistrarAsync(
            "PSICOLOGIA_DECLARACAO_EMITIDA",
            "ProntuarioPsicologico",
            sessao.Prontuario.Id.ToString(CultureInfo.InvariantCulture),
            $"{usuario.Identificador} emitiu declaração de comparecimento da sessão {sessao.Numero} do prontuário {sessao.Prontuario.Numero}.");

        return Ok(new DeclaracaoComparecimentoResponse
        {
            AssistidaNome = sessao.Prontuario.Assistida.Nome,
            Data = sessao.CriadoEm,
            ProfissionalNome = sessao.ProfissionalNome,
            ProfissionalCrp = sessao.ProfissionalCrp
        });
    }

    [HttpPost("prontuarios/{assistidaId:int}/encerrar")]
    public async Task<IActionResult> Encerrar(int assistidaId, EncerrarProntuarioRequest request, CancellationToken cancellationToken)
    {
        var usuario = await _assistidas.ObterUsuarioAsync(User, cancellationToken);
        if (usuario is null) return Unauthorized();

        if (!PsicologiaCatalogo.MotivosEncerramento.Contains(request.Motivo))
        {
            return BadRequest(new { mensagem = "Escolha o motivo do encerramento." });
        }

        var prontuario = await _dbContext.ProntuariosPsicologicos
            .Include(p => p.Responsaveis)
            .SingleOrDefaultAsync(p => p.AssistidaId == assistidaId, cancellationToken);
        if (prontuario is null) return NotFound(new { mensagem = "Prontuário não encontrado." });

        var acesso = await AvaliarAcessoAsync(prontuario, usuario, cancellationToken);
        if (!acesso.PodeVerConteudo)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { mensagem = "Somente quem cuida do caso pode encerrar o acompanhamento." });
        }

        prontuario.Situacao = ProntuarioSituacao.Encerrado;
        prontuario.MotivoEncerramento = request.Motivo;
        prontuario.EncerradoEm = DateTime.UtcNow;
        prontuario.AtualizadoEm = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditoria.RegistrarAsync(
            "PSICOLOGIA_ACOMPANHAMENTO_ENCERRADO",
            "ProntuarioPsicologico",
            prontuario.Id.ToString(CultureInfo.InvariantCulture),
            $"{usuario.Identificador} encerrou o acompanhamento do prontuário {prontuario.Numero} (motivo: {request.Motivo}).");

        var snapshot = await _snapshot.PersistAsync("psicologia_encerramento", CancellationToken.None);
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

        var prontuario = await _dbContext.ProntuariosPsicologicos
            .Include(p => p.Responsaveis)
            .SingleOrDefaultAsync(p => p.AssistidaId == assistidaId, cancellationToken);
        if (prontuario is null) return NotFound(new { mensagem = "Registre a primeira sessão antes de atribuir responsáveis." });

        var acesso = await AvaliarAcessoAsync(prontuario, usuario, cancellationToken);
        if (!acesso.PodeAtribuir)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { mensagem = "Você não pode alterar as responsáveis deste prontuário." });
        }

        var profissional = await _dbContext.Users.AsNoTracking()
            .SingleOrDefaultAsync(u => u.Id == request.UserId && u.Ativo, cancellationToken);
        if (profissional is null || !AssistidasService.AtuaComoPsicologia(profissional.Perfil.ToLowerInvariant()))
        {
            return BadRequest(new { mensagem = "Escolha uma profissional ativa da Psicologia." });
        }

        if (prontuario.Responsaveis.Any(r => r.UserId == profissional.Id))
        {
            return Ok(new { mensagem = "Esta profissional já é responsável." });
        }

        prontuario.Responsaveis.Add(new ProntuarioPsicologicoResponsavel
        {
            UserId = profissional.Id,
            Identificador = profissional.IdentificadorFuncionario,
            Nome = profissional.NomeCompleto,
            Crp = profissional.Crp,
            AtribuidoPorIdentificador = usuario.Identificador,
            AtribuidoEm = DateTime.UtcNow
        });
        prontuario.AtualizadoEm = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditoria.RegistrarAsync(
            "PSICOLOGIA_RESPONSAVEL_ATRIBUIDO",
            "ProntuarioPsicologico",
            prontuario.Id.ToString(CultureInfo.InvariantCulture),
            $"{usuario.Identificador} atribuiu {profissional.IdentificadorFuncionario} como responsável pelo prontuário {prontuario.Numero}.");

        var snapshot = await _snapshot.PersistAsync("psicologia_responsavel", CancellationToken.None);
        return Ok(new { mensagem = "Responsável atribuída.", avisoSnapshot = snapshot.AvisoSnapshot });
    }

    [HttpDelete("prontuarios/{assistidaId:int}/responsaveis/{userId}")]
    public async Task<IActionResult> RemoverResponsavel(int assistidaId, string userId, CancellationToken cancellationToken)
    {
        var usuario = await _assistidas.ObterUsuarioAsync(User, cancellationToken);
        if (usuario is null) return Unauthorized();

        var prontuario = await _dbContext.ProntuariosPsicologicos
            .Include(p => p.Responsaveis)
            .SingleOrDefaultAsync(p => p.AssistidaId == assistidaId, cancellationToken);
        if (prontuario is null) return NotFound(new { mensagem = "Prontuário não encontrado." });

        var acesso = await AvaliarAcessoAsync(prontuario, usuario, cancellationToken);
        if (!acesso.PodeAtribuir)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { mensagem = "Você não pode alterar as responsáveis deste prontuário." });
        }

        var responsavel = prontuario.Responsaveis.SingleOrDefault(r => r.UserId == userId);
        if (responsavel is null) return NotFound(new { mensagem = "Responsável não encontrada." });

        prontuario.Responsaveis.Remove(responsavel);
        prontuario.AtualizadoEm = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditoria.RegistrarAsync(
            "PSICOLOGIA_RESPONSAVEL_REMOVIDO",
            "ProntuarioPsicologico",
            prontuario.Id.ToString(CultureInfo.InvariantCulture),
            $"{usuario.Identificador} removeu {responsavel.Identificador} das responsáveis pelo prontuário {prontuario.Numero}.");

        var snapshot = await _snapshot.PersistAsync("psicologia_responsavel", CancellationToken.None);
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

        var prontuario = await _dbContext.ProntuariosPsicologicos
            .Include(p => p.Responsaveis)
            .SingleOrDefaultAsync(p => p.AssistidaId == assistidaId, cancellationToken);
        if (prontuario is null) return NotFound(new { mensagem = "Prontuário não encontrado." });

        if (!prontuario.Responsaveis.Any(r => r.UserId == usuario.UserId))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { mensagem = "Somente as responsáveis escolhem a visibilidade." });
        }

        prontuario.Visibilidade = request.Visibilidade;
        prontuario.AtualizadoEm = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditoria.RegistrarAsync(
            "PSICOLOGIA_VISIBILIDADE_ALTERADA",
            "ProntuarioPsicologico",
            prontuario.Id.ToString(CultureInfo.InvariantCulture),
            $"{usuario.Identificador} definiu a visibilidade do prontuário {prontuario.Numero} como \"{request.Visibilidade}\".");

        var snapshot = await _snapshot.PersistAsync("psicologia_visibilidade", CancellationToken.None);
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

        if (!AssistidasService.AtuaComoPsicologia(usuario.Perfil))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { mensagem = "Somente a Psicologia pode pedir acesso de emergência." });
        }

        var motivo = request.Motivo?.Trim() ?? string.Empty;
        if (motivo.Length < 10 || motivo.Length > 500)
        {
            return BadRequest(new { mensagem = "Explique o motivo do acesso (entre 10 e 500 caracteres)." });
        }

        var prontuario = await _dbContext.ProntuariosPsicologicos
            .Include(p => p.Responsaveis)
            .SingleOrDefaultAsync(p => p.AssistidaId == assistidaId, cancellationToken);
        if (prontuario is null) return NotFound(new { mensagem = "Prontuário não encontrado." });

        var acesso = await AvaliarAcessoAsync(prontuario, usuario, cancellationToken);
        if (acesso.PodeVerConteudo)
        {
            return Ok(new { mensagem = "Você já tem acesso a este prontuário." });
        }

        var registro = new ProntuarioPsicologicoAcessoEmergencia
        {
            ProntuarioPsicologicoId = prontuario.Id,
            UserId = usuario.UserId,
            Identificador = usuario.Identificador,
            Nome = usuario.Nome,
            Motivo = motivo,
            CriadoEm = DateTime.UtcNow,
            ExpiraEm = DateTime.UtcNow.Add(DuracaoAcessoEmergencia)
        };
        _dbContext.ProntuariosPsicologicosAcessosEmergencia.Add(registro);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditoria.RegistrarAsync(
            "PSICOLOGIA_ACESSO_EMERGENCIA",
            "ProntuarioPsicologico",
            prontuario.Id.ToString(CultureInfo.InvariantCulture),
            $"{usuario.Identificador} usou acesso de emergência ao prontuário {prontuario.Numero}. Motivo: {Limitar(motivo, 300)}");

        await AvisarResponsaveisAsync(prontuario, usuario);
        var snapshot = await _snapshot.PersistAsync("psicologia_acesso_emergencia", CancellationToken.None);

        return Ok(new { mensagem = "Acesso liberado por 12 horas. As responsáveis foram avisadas.", expiraEm = registro.ExpiraEm, avisoSnapshot = snapshot.AvisoSnapshot });
    }

    [HttpGet("profissionais")]
    public async Task<ActionResult<List<ResponsavelPsicologiaResponse>>> ListarProfissionais(CancellationToken cancellationToken)
    {
        var perfis = new[] { PerfisAcesso.Psicologia, PerfisAcesso.Equipe };
        var profissionais = await _dbContext.Users.AsNoTracking()
            .Where(u => u.Ativo && perfis.Contains(u.Perfil.ToLower()))
            .OrderBy(u => u.NomeCompleto)
            .Select(u => new ResponsavelPsicologiaResponse
            {
                UserId = u.Id,
                Identificador = u.IdentificadorFuncionario,
                Nome = u.NomeCompleto,
                Crp = u.Crp
            })
            .ToListAsync(cancellationToken);

        return Ok(profissionais);
    }

    /// <summary>Contagens anônimas: nenhum nome sai deste endpoint. É o que a coordenação vê.</summary>
    [HttpGet("indicadores")]
    public async Task<ActionResult<IndicadoresPsicologiaResponse>> Indicadores(CancellationToken cancellationToken)
    {
        var agora = DateTime.UtcNow;
        var inicioMes = new DateTime(agora.Year, agora.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var prontuarios = await CarregarProntuariosAsync(cancellationToken);
        var encaminhadas = await _dbContext.Assistidas.AsNoTracking()
            .Where(a => a.Ativa && a.EncaminhadaPsicologiaEm != null)
            .Select(a => a.Id)
            .ToListAsync(cancellationToken);

        var sessoesMes = await _dbContext.SessoesPsicologicas.AsNoTracking()
            .Where(s => s.CriadoEm >= inicioMes)
            .Select(s => new { s.Presenca, s.EncaminhamentosExternos })
            .ToListAsync(cancellationToken);

        return Ok(new IndicadoresPsicologiaResponse
        {
            Fila = encaminhadas.Count(id => !prontuarios.TryGetValue(id, out var p) || p.Sessoes.Count == 0),
            EmAcompanhamento = prontuarios.Values.Count(p => p.Sessoes.Count > 0 && p.Situacao == ProntuarioSituacao.EmAcompanhamento),
            Encerrados = prontuarios.Values.Count(p => p.Situacao == ProntuarioSituacao.Encerrado),
            SessoesNoMes = sessoesMes.Count(s => s.Presenca == "compareceu"),
            FaltasNoMes = sessoesMes.Count(s => s.Presenca is "faltou" or "faltou_justificou"),
            RiscoElevado = prontuarios.Values.Count(p => p.Situacao == ProntuarioSituacao.EmAcompanhamento && PsicologiaCatalogo.RiscoElevado(p.RiscoAtual)),
            EncaminhamentosNoMes = sessoesMes
                .Where(s => !string.IsNullOrWhiteSpace(s.EncaminhamentosExternos))
                .SelectMany(s => s.EncaminhamentosExternos!.Split(',', StringSplitOptions.RemoveEmptyEntries))
                .GroupBy(e => e)
                .ToDictionary(g => g.Key, g => g.Count())
        });
    }

    private sealed record AcessoProntuario(
        bool SouResponsavel,
        bool PodeVerConteudo,
        bool PodeAtribuir,
        DateTime? EmergenciaExpiraEm);

    private async Task<AcessoProntuario> AvaliarAcessoAsync(ProntuarioPsicologico? prontuario, UsuarioAtuante usuario, CancellationToken cancellationToken)
    {
        var psicologia = AssistidasService.AtuaComoPsicologia(usuario.Perfil);
        var souResponsavel = prontuario?.Responsaveis.Any(r => r.UserId == usuario.UserId) == true;

        DateTime? emergencia = null;
        if (prontuario is not null && psicologia)
        {
            var agora = DateTime.UtcNow;
            emergencia = await _dbContext.ProntuariosPsicologicosAcessosEmergencia.AsNoTracking()
                .Where(a => a.ProntuarioPsicologicoId == prontuario.Id && a.UserId == usuario.UserId && a.ExpiraEm > agora)
                .MaxAsync(a => (DateTime?)a.ExpiraEm, cancellationToken);
        }

        // Prontuário ainda inexistente: qualquer psicóloga pode abrir com a primeira sessão.
        var podeVer = psicologia && (prontuario is null
            || prontuario.Visibilidade == ProntuarioVisibilidade.Equipe
            || souResponsavel
            || emergencia is not null);

        var semResponsavel = prontuario is not null && prontuario.Responsaveis.Count == 0;
        return new AcessoProntuario(souResponsavel, podeVer, podeVer || (psicologia && semResponsavel), emergencia);
    }

    private async Task<Dictionary<int, ProntuarioPsicologico>> CarregarProntuariosAsync(CancellationToken cancellationToken)
    {
        var prontuarios = await _dbContext.ProntuariosPsicologicos.AsNoTracking()
            .Include(p => p.Responsaveis)
            .Include(p => p.Sessoes)
            .ToListAsync(cancellationToken);

        return prontuarios.ToDictionary(p => p.AssistidaId);
    }

    private static PsicologiaAssistidaResumoResponse MontarResumo(Assistida a, ProntuarioPsicologico? prontuario, UsuarioAtuante usuario)
    {
        var ultima = prontuario?.Sessoes.OrderByDescending(s => s.Numero).FirstOrDefault();
        var souResponsavel = prontuario?.Responsaveis.Any(r => r.UserId == usuario.UserId) == true;
        var podeVerDatas = prontuario is null || prontuario.Visibilidade == ProntuarioVisibilidade.Equipe || souResponsavel;

        return new PsicologiaAssistidaResumoResponse
        {
            AssistidaId = a.Id,
            Nome = a.Nome,
            CpfMascarado = AssistidasService.MascararCpf(a.Cpf),
            Telefone = a.Telefone,
            SemTelefoneSeguro = a.SemTelefoneSeguro,
            ContatoSeguro = a.ContatoSeguro,
            AcessoRestrito = a.AcessoRestrito,
            Origem = a.Origem,
            EncaminhadaPsicologiaEm = a.EncaminhadaPsicologiaEm,
            ProntuarioId = prontuario?.Id,
            NumeroProntuario = prontuario?.Numero,
            Situacao = prontuario is null || prontuario.Sessoes.Count == 0 ? null : prontuario.Situacao,
            Visibilidade = prontuario?.Visibilidade,
            Responsaveis = prontuario?.Responsaveis.OrderBy(r => r.AtribuidoEm).Select(MapearResponsavel).ToList() ?? [],
            SouResponsavel = souResponsavel,
            TotalSessoes = prontuario?.Sessoes.Count ?? 0,
            UltimaSessaoEm = podeVerDatas ? ultima?.CriadoEm : null,
            ProximaSessao = podeVerDatas ? ultima?.ProximaSessao : null,
            RiscoElevado = prontuario?.Situacao == ProntuarioSituacao.EmAcompanhamento && PsicologiaCatalogo.RiscoElevado(prontuario.RiscoAtual)
        };
    }

    private static ResponsavelPsicologiaResponse MapearResponsavel(ProntuarioPsicologicoResponsavel r)
    {
        return new ResponsavelPsicologiaResponse { UserId = r.UserId, Identificador = r.Identificador, Nome = r.Nome, Crp = r.Crp };
    }

    private static AdendoPsicologiaResponse MapearAdendo(SessaoPsicologicaAdendo adendo)
    {
        return new AdendoPsicologiaResponse
        {
            Id = adendo.Id,
            Texto = adendo.Texto,
            AutorNome = adendo.AutorNome,
            AutorIdentificador = adendo.AutorIdentificador,
            CriadoEm = adendo.CriadoEm
        };
    }

    private static SessaoPsicologicaResponse MapearSessao(SessaoPsicologica s, bool conteudo)
    {
        var resposta = new SessaoPsicologicaResponse
        {
            Id = s.Id,
            Numero = s.Numero,
            CriadoEm = s.CriadoEm,
            ProfissionalNome = s.ProfissionalNome,
            ProfissionalIdentificador = s.ProfissionalIdentificador,
            ProfissionalCrp = s.ProfissionalCrp,
            ConteudoOculto = !conteudo
        };

        if (!conteudo)
        {
            return resposta;
        }

        resposta.TipoAtendimento = s.TipoAtendimento;
        resposta.Presenca = s.Presenca;
        resposta.Modalidade = s.Modalidade;
        resposta.Demanda = s.Demanda;
        resposta.Evolucao = s.Evolucao;
        resposta.Plano = s.Plano;
        resposta.ProximaSessao = s.ProximaSessao;
        resposta.Risco = s.Risco;
        resposta.EncaminhamentosExternos = string.IsNullOrWhiteSpace(s.EncaminhamentosExternos)
            ? []
            : s.EncaminhamentosExternos.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
        resposta.EncaminhamentoExternoOutro = s.EncaminhamentoExternoOutro;
        resposta.Adendos = s.Adendos.OrderBy(a => a.CriadoEm).Select(MapearAdendo).ToList();
        return resposta;
    }

    private static string? ValidarSessao(NovaSessaoPsicologicaRequest request, ProntuarioPsicologico? prontuario)
    {
        if (!PsicologiaCatalogo.TiposAtendimento.Contains(request.TipoAtendimento)) return "Escolha o tipo de atendimento.";
        if (!PsicologiaCatalogo.Presencas.Contains(request.Presenca)) return "Informe a presença.";

        var compareceu = request.Presenca == "compareceu";

        if (compareceu)
        {
            if (!PsicologiaCatalogo.Modalidades.Contains(request.Modalidade)) return "Escolha a modalidade.";
            if (!PsicologiaCatalogo.Riscos.Contains(request.Risco)) return "Classifique o risco.";

            var evolucao = request.Evolucao?.Trim() ?? string.Empty;
            if (evolucao.Length < 15) return "Descreva a evolução com mais detalhes (mínimo de 15 caracteres).";

            if (prontuario?.ConsentimentoSigiloEm is null && !request.ConsentimentoSigilo)
            {
                return "No primeiro atendimento, confirme que a assistida foi informada sobre o sigilo e seus limites.";
            }
        }
        else if (!string.IsNullOrWhiteSpace(request.Risco) && !PsicologiaCatalogo.Riscos.Contains(request.Risco))
        {
            return "Classificação de risco inválida.";
        }

        if ((request.Evolucao?.Trim().Length ?? 0) > 5000) return "A evolução excedeu 5000 caracteres.";

        if (request.EncaminhamentosExternos.Any(e => !PsicologiaCatalogo.EncaminhamentosExternos.Contains(e)))
        {
            return "Encaminhamento inválido.";
        }

        if (!string.IsNullOrWhiteSpace(request.ProximaSessao)
            && !DateTime.TryParse(request.ProximaSessao, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        {
            return "Data da próxima sessão inválida.";
        }

        return null;
    }

    private async Task AvisarResponsaveisAsync(ProntuarioPsicologico prontuario, UsuarioAtuante usuario)
    {
        var ids = prontuario.Responsaveis.Select(r => r.UserId).ToList();
        var emails = await _dbContext.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id) && u.Email != null)
            .Select(u => u.Email!)
            .ToListAsync();

        // O e-mail não leva nome da assistida nem o motivo: os detalhes ficam na auditoria.
        var corpo = $"<p>A profissional de ID {System.Net.WebUtility.HtmlEncode(usuario.Identificador)} usou o acesso de emergência "
            + $"no prontuário {System.Net.WebUtility.HtmlEncode(prontuario.Numero)}, que está sob sua responsabilidade.</p>"
            + "<p>O acesso vale por 12 horas e ficou registrado no histórico de ações. Entre no sistema para ver os detalhes.</p>";

        foreach (var email in emails)
        {
            try
            {
                await _emailService.EnviarAsync(email, "Acesso de emergência a um prontuário psicológico - Casa da Mulher", corpo, "PsicologiaAcessoEmergencia");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Não foi possível avisar uma responsável sobre o acesso de emergência ao prontuário {ProntuarioId}.", prontuario.Id);
            }
        }
    }

    /// <summary>O navegador envia a próxima sessão no horário de Brasília; o banco guarda em UTC.</summary>
    private static DateTime LocalParaUtc(DateTime local)
    {
        try
        {
            var fuso = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
            return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), fuso);
        }
        catch (TimeZoneNotFoundException)
        {
            return DateTime.SpecifyKind(local.AddHours(3), DateTimeKind.Utc);
        }
    }

    private static int ObterAnoLocal(DateTime utc)
    {
        try
        {
            var fuso = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
            return TimeZoneInfo.ConvertTimeFromUtc(utc, fuso).Year;
        }
        catch (TimeZoneNotFoundException)
        {
            return utc.AddHours(-3).Year;
        }
    }

    private static string? Limitar(string? valor, int maximo)
    {
        if (string.IsNullOrEmpty(valor)) return null;
        return valor.Length <= maximo ? valor : valor[..maximo];
    }
}
