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
/// Módulo Jurídico: fila de encaminhadas, casos, atendimentos com protocolo do servidor,
/// adendos, responsáveis, visibilidade e acesso de emergência.
/// A coordenação (ADM) enxerga apenas metadados; o conteúdo é exclusivo do Jurídico.
/// </summary>
[ApiController]
[Route("api/juridico")]
[Authorize(Policy = PoliticasAcesso.AcessoJuridico)]
public class JuridicoController : ControllerBase
{
    private static readonly TimeSpan DuracaoAcessoEmergencia = TimeSpan.FromHours(12);

    private readonly AppDbContext _dbContext;
    private readonly AssistidasService _assistidas;
    private readonly IAuditoriaService _auditoria;
    private readonly SecuritySnapshotPersistenceService _snapshot;
    private readonly IEmailService _emailService;
    private readonly ILogger<JuridicoController> _logger;

    public JuridicoController(
        AppDbContext dbContext,
        AssistidasService assistidas,
        IAuditoriaService auditoria,
        SecuritySnapshotPersistenceService snapshot,
        IEmailService emailService,
        ILogger<JuridicoController> logger)
    {
        _dbContext = dbContext;
        _assistidas = assistidas;
        _auditoria = auditoria;
        _snapshot = snapshot;
        _emailService = emailService;
        _logger = logger;
    }

    /// <summary>
    /// escopo: fila (encaminhadas sem atendimento), minhas (sou responsável),
    /// casos (padrão: com caso ou encaminhadas). Com busca, procura em todo o cadastro.
    /// </summary>
    [HttpGet("assistidas")]
    public async Task<ActionResult<List<JuridicoAssistidaResumoResponse>>> Listar(
        [FromQuery] string? escopo,
        [FromQuery] string? busca,
        CancellationToken cancellationToken)
    {
        var usuario = await _assistidas.ObterUsuarioAsync(User, cancellationToken);
        if (usuario is null) return Unauthorized();

        var assistidas = await _dbContext.Assistidas.AsNoTracking()
            .Where(a => a.Ativa)
            .ToListAsync(cancellationToken);

        var casos = await CarregarCasosAsync(cancellationToken);
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
                "fila" => filtradas.Where(a => a.EncaminhadaJuridicoEm is not null
                    && (!casos.TryGetValue(a.Id, out var c) || c.Atendimentos.Count == 0)),
                "minhas" => filtradas.Where(a => casos.TryGetValue(a.Id, out var c)
                    && c.Responsaveis.Any(r => r.UserId == usuario.UserId)),
                _ => filtradas.Where(a => a.EncaminhadaJuridicoEm is not null || casos.ContainsKey(a.Id))
            };
        }

        var resultado = filtradas
            .Select(a => MontarResumo(a, casos.GetValueOrDefault(a.Id), usuario))
            .OrderBy(r => escopo == "fila" ? (r.EncaminhadaJuridicoEm ?? DateTime.MaxValue).Ticks : 0)
            .ThenBy(r => r.Nome, StringComparer.Create(new CultureInfo("pt-BR"), true))
            .ToList();

        return Ok(resultado);
    }

    [HttpGet("casos/{assistidaId:int}")]
    public async Task<ActionResult<CasoJuridicoDetalheResponse>> ObterCaso(int assistidaId, CancellationToken cancellationToken)
    {
        var usuario = await _assistidas.ObterUsuarioAsync(User, cancellationToken);
        if (usuario is null) return Unauthorized();

        var assistida = await _dbContext.Assistidas.AsNoTracking()
            .SingleOrDefaultAsync(a => a.Id == assistidaId && a.Ativa, cancellationToken);
        if (assistida is null) return NotFound(new { mensagem = "Assistida não encontrada." });

        var caso = await _dbContext.CasosJuridicos.AsNoTracking()
            .Include(c => c.Responsaveis)
            .Include(c => c.Atendimentos).ThenInclude(at => at.Adendos)
            .SingleOrDefaultAsync(c => c.AssistidaId == assistidaId, cancellationToken);

        var acesso = await AvaliarAcessoAsync(caso, usuario, cancellationToken);

        var resposta = new CasoJuridicoDetalheResponse
        {
            Assistida = AssistidasController.MapearResposta(assistida, null, usuario.Perfil),
            CasoId = caso?.Id,
            Visibilidade = caso?.Visibilidade ?? CasoJuridicoVisibilidade.Equipe,
            Situacao = caso?.Situacao ?? CasoJuridicoSituacao.EmAndamento,
            Responsaveis = caso?.Responsaveis.OrderBy(r => r.AtribuidoEm).Select(MapearResponsavel).ToList() ?? [],
            SouResponsavel = acesso.SouResponsavel,
            PodeVerConteudo = acesso.PodeVerConteudo,
            PodeRegistrar = acesso.PodeVerConteudo,
            PodeAtribuir = acesso.PodeAtribuir,
            PodeAlterarVisibilidade = acesso.SouResponsavel,
            PodePedirAcessoEmergencia = acesso.AtuaComoJuridico && !acesso.PodeVerConteudo,
            AcessoEmergenciaExpiraEm = acesso.EmergenciaExpiraEm,
            Atendimentos = caso?.Atendimentos
                .OrderByDescending(at => at.CriadoEm)
                .Select(at => MapearAtendimento(at, acesso.PodeVerConteudo))
                .ToList() ?? []
        };

        if (acesso.AtuaComoJuridico)
        {
            var psicologico = await _dbContext.ProntuariosPsicologicos.AsNoTracking()
                .Where(p => p.AssistidaId == assistidaId && p.Situacao == ProntuarioSituacao.EmAcompanhamento && p.Sessoes.Any())
                .Select(p => new { p.RiscoAtual })
                .FirstOrDefaultAsync(cancellationToken);
            resposta.AcompanhamentoPsicologico = psicologico is not null;
            resposta.AlertaRiscoPsicologico = psicologico is not null && PsicologiaCatalogo.RiscoElevado(psicologico.RiscoAtual);
            resposta.EncaminhadaPsicologia = assistida.EncaminhadaPsicologiaEm is not null;
        }

        // Remove o que é do Jurídico para quem só pode ver metadados.
        if (!acesso.PodeVerConteudo)
        {
            resposta.Assistida.Observacoes = null;
        }

        if (acesso.PodeVerConteudo && resposta.Atendimentos.Count > 0)
        {
            await _auditoria.RegistrarAsync(
                "JURIDICO_CASO_VISUALIZADO",
                "CasoJuridico",
                caso!.Id.ToString(CultureInfo.InvariantCulture),
                $"{usuario.Identificador} abriu os atendimentos jurídicos da assistida #{assistida.Id}"
                    + (acesso.EmergenciaExpiraEm is not null && !acesso.SouResponsavel && caso.Visibilidade == CasoJuridicoVisibilidade.Responsaveis
                        ? " (acesso de emergência)."
                        : "."));
        }

        return Ok(resposta);
    }

    [HttpPost("casos/{assistidaId:int}/atendimentos")]
    public async Task<ActionResult<AtendimentoRegistradoResponse>> RegistrarAtendimento(
        int assistidaId,
        NovoAtendimentoJuridicoRequest request,
        CancellationToken cancellationToken)
    {
        var usuario = await _assistidas.ObterUsuarioAsync(User, cancellationToken);
        if (usuario is null) return Unauthorized();

        if (!AssistidasService.AtuaComoJuridico(usuario.Perfil))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { mensagem = "Somente o Jurídico registra atendimentos jurídicos." });
        }

        var assistida = await _dbContext.Assistidas.SingleOrDefaultAsync(a => a.Id == assistidaId && a.Ativa, cancellationToken);
        if (assistida is null) return NotFound(new { mensagem = "Assistida não encontrada." });

        var erro = ValidarAtendimento(request);
        if (erro is not null) return BadRequest(new { mensagem = erro });

        var caso = await _dbContext.CasosJuridicos
            .Include(c => c.Responsaveis)
            .SingleOrDefaultAsync(c => c.AssistidaId == assistidaId, cancellationToken);

        var acesso = await AvaliarAcessoAsync(caso, usuario, cancellationToken);
        if (!acesso.PodeVerConteudo)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { mensagem = "Este caso está visível somente para as responsáveis. Peça acesso de emergência se precisar atender." });
        }

        var agora = DateTime.UtcNow;
        if (caso is null)
        {
            caso = new CasoJuridico { AssistidaId = assistidaId, CriadoEm = agora };
            caso.Responsaveis.Add(new CasoJuridicoResponsavel
            {
                UserId = usuario.UserId,
                Identificador = usuario.Identificador,
                Nome = usuario.Nome,
                AtribuidoPorIdentificador = usuario.Identificador,
                AtribuidoEm = agora
            });
            _dbContext.CasosJuridicos.Add(caso);
        }

        var situacao = request.Situacao ?? CasoJuridicoSituacao.EmAndamento;
        caso.Situacao = situacao;
        caso.AtualizadoEm = agora;
        assistida.EncaminhadaJuridicoEm ??= agora;

        var ano = ObterAnoLocal(agora);
        var atendimento = new AtendimentoJuridico
        {
            Caso = caso,
            Ano = ano,
            ProfissionalUserId = usuario.UserId,
            ProfissionalIdentificador = usuario.Identificador,
            ProfissionalNome = usuario.Nome,
            TipoDemanda = request.TipoDemanda,
            TipoDemandaOutro = request.TipoDemanda == "outro" ? Limitar(request.TipoDemandaOutro?.Trim(), 120) : null,
            TemMedidaProtetiva = request.TemMedidaProtetiva,
            NumeroProcesso = Limitar(request.NumeroProcesso?.Trim(), 40),
            Vara = Limitar(request.Vara?.Trim(), 120),
            Encaminhamentos = request.Encaminhamentos.Count == 0 ? null : string.Join(',', request.Encaminhamentos.Distinct()),
            EncaminhamentoOutro = request.Encaminhamentos.Contains("outro") ? Limitar(request.EncaminhamentoOutro?.Trim(), 120) : null,
            Relato = request.Relato.Trim(),
            ProximoPasso = Limitar(request.ProximoPasso?.Trim(), 500),
            DataRetorno = DateOnly.TryParse(request.DataRetorno, CultureInfo.InvariantCulture, out var retorno) ? retorno : null,
            Situacao = situacao,
            CriadoEm = agora
        };
        _dbContext.AtendimentosJuridicos.Add(atendimento);

        // Protocolo sequencial por ano, gerado no servidor. Repete se outra gravação pegar o mesmo número.
        for (var tentativa = 1; ; tentativa++)
        {
            var ultimo = await _dbContext.AtendimentosJuridicos
                .Where(at => at.Ano == ano)
                .MaxAsync(at => (int?)at.Sequencial, cancellationToken) ?? 0;
            atendimento.Sequencial = ultimo + 1;
            atendimento.Protocolo = $"CJ-{ano}-{atendimento.Sequencial:D6}";

            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
                break;
            }
            catch (DbUpdateException) when (tentativa < 3)
            {
                _logger.LogWarning("Conflito ao gerar protocolo {Protocolo}; tentando o próximo número.", atendimento.Protocolo);
            }
        }

        await _auditoria.RegistrarAsync(
            "JURIDICO_ATENDIMENTO_REGISTRADO",
            "AtendimentoJuridico",
            atendimento.Id.ToString(CultureInfo.InvariantCulture),
            $"{usuario.Identificador} registrou o atendimento {atendimento.Protocolo} da assistida #{assistida.Id}.");

        var snapshot = await _snapshot.PersistAsync("juridico_atendimento", CancellationToken.None);

        return Ok(new AtendimentoRegistradoResponse
        {
            AtendimentoId = atendimento.Id,
            Protocolo = atendimento.Protocolo,
            CriadoEm = atendimento.CriadoEm,
            AssistidaNome = assistida.Nome,
            ProfissionalNome = usuario.Nome,
            DataRetorno = atendimento.DataRetorno?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            AvisoSnapshot = snapshot.AvisoSnapshot
        });
    }

    [HttpPost("atendimentos/{atendimentoId:int}/adendos")]
    public async Task<ActionResult<AdendoJuridicoResponse>> RegistrarAdendo(
        int atendimentoId,
        NovoAdendoJuridicoRequest request,
        CancellationToken cancellationToken)
    {
        var usuario = await _assistidas.ObterUsuarioAsync(User, cancellationToken);
        if (usuario is null) return Unauthorized();

        var texto = request.Texto?.Trim() ?? string.Empty;
        if (texto.Length < 3 || texto.Length > 2000)
        {
            return BadRequest(new { mensagem = "O adendo deve ter entre 3 e 2000 caracteres." });
        }

        var atendimento = await _dbContext.AtendimentosJuridicos
            .Include(at => at.Caso).ThenInclude(c => c!.Responsaveis)
            .SingleOrDefaultAsync(at => at.Id == atendimentoId, cancellationToken);
        if (atendimento?.Caso is null) return NotFound(new { mensagem = "Atendimento não encontrado." });

        var acesso = await AvaliarAcessoAsync(atendimento.Caso, usuario, cancellationToken);
        if (!acesso.PodeVerConteudo)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { mensagem = "Você não tem acesso ao conteúdo deste caso." });
        }

        var adendo = new AtendimentoJuridicoAdendo
        {
            AtendimentoJuridicoId = atendimento.Id,
            Texto = texto,
            AutorUserId = usuario.UserId,
            AutorIdentificador = usuario.Identificador,
            AutorNome = usuario.Nome,
            CriadoEm = DateTime.UtcNow
        };
        _dbContext.AtendimentosJuridicosAdendos.Add(adendo);
        atendimento.Caso.AtualizadoEm = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditoria.RegistrarAsync(
            "JURIDICO_ADENDO_REGISTRADO",
            "AtendimentoJuridico",
            atendimento.Id.ToString(CultureInfo.InvariantCulture),
            $"{usuario.Identificador} acrescentou um adendo ao atendimento {atendimento.Protocolo}.");

        await _snapshot.PersistAsync("juridico_adendo", CancellationToken.None);
        return Ok(MapearAdendo(adendo));
    }

    [HttpPost("casos/{assistidaId:int}/responsaveis")]
    public async Task<IActionResult> AtribuirResponsavel(
        int assistidaId,
        AtribuirResponsavelJuridicoRequest request,
        CancellationToken cancellationToken)
    {
        var usuario = await _assistidas.ObterUsuarioAsync(User, cancellationToken);
        if (usuario is null) return Unauthorized();

        var assistida = await _dbContext.Assistidas.AsNoTracking()
            .SingleOrDefaultAsync(a => a.Id == assistidaId && a.Ativa, cancellationToken);
        if (assistida is null) return NotFound(new { mensagem = "Assistida não encontrada." });

        var caso = await _dbContext.CasosJuridicos
            .Include(c => c.Responsaveis)
            .SingleOrDefaultAsync(c => c.AssistidaId == assistidaId, cancellationToken);

        var acesso = await AvaliarAcessoAsync(caso, usuario, cancellationToken);
        if (!acesso.PodeAtribuir)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { mensagem = "Você não pode alterar as responsáveis deste caso." });
        }

        var profissional = await _dbContext.Users.AsNoTracking()
            .SingleOrDefaultAsync(u => u.Id == request.UserId && u.Ativo, cancellationToken);
        if (profissional is null || !AssistidasService.AtuaComoJuridico(profissional.Perfil.ToLowerInvariant()))
        {
            return BadRequest(new { mensagem = "Escolha uma profissional ativa do Jurídico." });
        }

        if (caso is null)
        {
            caso = new CasoJuridico { AssistidaId = assistidaId };
            _dbContext.CasosJuridicos.Add(caso);
        }

        if (caso.Responsaveis.Any(r => r.UserId == profissional.Id))
        {
            return Ok(new { mensagem = "Esta profissional já é responsável pelo caso." });
        }

        caso.Responsaveis.Add(new CasoJuridicoResponsavel
        {
            UserId = profissional.Id,
            Identificador = profissional.IdentificadorFuncionario,
            Nome = profissional.NomeCompleto,
            AtribuidoPorIdentificador = usuario.Identificador,
            AtribuidoEm = DateTime.UtcNow
        });
        caso.AtualizadoEm = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditoria.RegistrarAsync(
            "JURIDICO_RESPONSAVEL_ATRIBUIDO",
            "CasoJuridico",
            caso.Id.ToString(CultureInfo.InvariantCulture),
            $"{usuario.Identificador} atribuiu {profissional.IdentificadorFuncionario} como responsável pelo caso da assistida #{assistidaId}.");

        var snapshot = await _snapshot.PersistAsync("juridico_responsavel", CancellationToken.None);
        return Ok(new { mensagem = "Responsável atribuída.", avisoSnapshot = snapshot.AvisoSnapshot });
    }

    [HttpDelete("casos/{assistidaId:int}/responsaveis/{userId}")]
    public async Task<IActionResult> RemoverResponsavel(int assistidaId, string userId, CancellationToken cancellationToken)
    {
        var usuario = await _assistidas.ObterUsuarioAsync(User, cancellationToken);
        if (usuario is null) return Unauthorized();

        var caso = await _dbContext.CasosJuridicos
            .Include(c => c.Responsaveis)
            .SingleOrDefaultAsync(c => c.AssistidaId == assistidaId, cancellationToken);
        if (caso is null) return NotFound(new { mensagem = "Caso não encontrado." });

        var acesso = await AvaliarAcessoAsync(caso, usuario, cancellationToken);
        if (!acesso.PodeAtribuir)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { mensagem = "Você não pode alterar as responsáveis deste caso." });
        }

        var responsavel = caso.Responsaveis.SingleOrDefault(r => r.UserId == userId);
        if (responsavel is null) return NotFound(new { mensagem = "Responsável não encontrada." });

        caso.Responsaveis.Remove(responsavel);
        caso.AtualizadoEm = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditoria.RegistrarAsync(
            "JURIDICO_RESPONSAVEL_REMOVIDO",
            "CasoJuridico",
            caso.Id.ToString(CultureInfo.InvariantCulture),
            $"{usuario.Identificador} removeu {responsavel.Identificador} das responsáveis pelo caso da assistida #{assistidaId}.");

        var snapshot = await _snapshot.PersistAsync("juridico_responsavel", CancellationToken.None);
        return Ok(new { mensagem = "Responsável removida.", avisoSnapshot = snapshot.AvisoSnapshot });
    }

    [HttpPut("casos/{assistidaId:int}/visibilidade")]
    public async Task<IActionResult> AlterarVisibilidade(
        int assistidaId,
        AlterarVisibilidadeCasoRequest request,
        CancellationToken cancellationToken)
    {
        var usuario = await _assistidas.ObterUsuarioAsync(User, cancellationToken);
        if (usuario is null) return Unauthorized();

        if (!CasoJuridicoVisibilidade.Todos.Contains(request.Visibilidade))
        {
            return BadRequest(new { mensagem = "Visibilidade inválida." });
        }

        var caso = await _dbContext.CasosJuridicos
            .Include(c => c.Responsaveis)
            .SingleOrDefaultAsync(c => c.AssistidaId == assistidaId, cancellationToken);
        if (caso is null) return NotFound(new { mensagem = "Atribua uma responsável ou registre um atendimento antes de definir a visibilidade." });

        if (!caso.Responsaveis.Any(r => r.UserId == usuario.UserId))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { mensagem = "Somente as responsáveis pelo caso escolhem a visibilidade." });
        }

        caso.Visibilidade = request.Visibilidade;
        caso.AtualizadoEm = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditoria.RegistrarAsync(
            "JURIDICO_VISIBILIDADE_ALTERADA",
            "CasoJuridico",
            caso.Id.ToString(CultureInfo.InvariantCulture),
            $"{usuario.Identificador} definiu a visibilidade do caso da assistida #{assistidaId} como \"{request.Visibilidade}\".");

        var snapshot = await _snapshot.PersistAsync("juridico_visibilidade", CancellationToken.None);
        return Ok(new { mensagem = "Visibilidade atualizada.", avisoSnapshot = snapshot.AvisoSnapshot });
    }

    [HttpPost("casos/{assistidaId:int}/acesso-emergencia")]
    public async Task<IActionResult> PedirAcessoEmergencia(
        int assistidaId,
        AcessoEmergenciaJuridicoRequest request,
        CancellationToken cancellationToken)
    {
        var usuario = await _assistidas.ObterUsuarioAsync(User, cancellationToken);
        if (usuario is null) return Unauthorized();

        if (!AssistidasService.AtuaComoJuridico(usuario.Perfil))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { mensagem = "Somente o Jurídico pode pedir acesso de emergência." });
        }

        var motivo = request.Motivo?.Trim() ?? string.Empty;
        if (motivo.Length < 10 || motivo.Length > 500)
        {
            return BadRequest(new { mensagem = "Explique o motivo do acesso (entre 10 e 500 caracteres)." });
        }

        var caso = await _dbContext.CasosJuridicos
            .Include(c => c.Responsaveis)
            .SingleOrDefaultAsync(c => c.AssistidaId == assistidaId, cancellationToken);
        if (caso is null) return NotFound(new { mensagem = "Caso não encontrado." });

        var acesso = await AvaliarAcessoAsync(caso, usuario, cancellationToken);
        if (acesso.PodeVerConteudo)
        {
            return Ok(new { mensagem = "Você já tem acesso a este caso." });
        }

        var registro = new CasoJuridicoAcessoEmergencia
        {
            CasoJuridicoId = caso.Id,
            UserId = usuario.UserId,
            Identificador = usuario.Identificador,
            Nome = usuario.Nome,
            Motivo = motivo,
            CriadoEm = DateTime.UtcNow,
            ExpiraEm = DateTime.UtcNow.Add(DuracaoAcessoEmergencia)
        };
        _dbContext.CasosJuridicosAcessosEmergencia.Add(registro);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditoria.RegistrarAsync(
            "JURIDICO_ACESSO_EMERGENCIA",
            "CasoJuridico",
            caso.Id.ToString(CultureInfo.InvariantCulture),
            $"{usuario.Identificador} usou acesso de emergência ao caso da assistida #{assistidaId}. Motivo: {Limitar(motivo, 300)}");

        await AvisarResponsaveisAsync(caso, usuario);
        var snapshot = await _snapshot.PersistAsync("juridico_acesso_emergencia", CancellationToken.None);

        return Ok(new
        {
            mensagem = $"Acesso liberado até {registro.ExpiraEm:O}. As responsáveis foram avisadas.",
            expiraEm = registro.ExpiraEm,
            avisoSnapshot = snapshot.AvisoSnapshot
        });
    }

    [HttpGet("profissionais")]
    public async Task<ActionResult<List<ResponsavelJuridicoResponse>>> ListarProfissionais(CancellationToken cancellationToken)
    {
        var usuario = await _assistidas.ObterUsuarioAsync(User, cancellationToken);
        if (usuario is null) return Unauthorized();

        var perfis = new[] { PerfisAcesso.Juridico, PerfisAcesso.Equipe };
        var profissionais = await _dbContext.Users.AsNoTracking()
            .Where(u => u.Ativo && perfis.Contains(u.Perfil.ToLower()))
            .OrderBy(u => u.NomeCompleto)
            .Select(u => new ResponsavelJuridicoResponse
            {
                UserId = u.Id,
                Identificador = u.IdentificadorFuncionario,
                Nome = u.NomeCompleto
            })
            .ToListAsync(cancellationToken);

        return Ok(profissionais);
    }

    [HttpGet("indicadores")]
    public async Task<ActionResult<IndicadoresJuridicoResponse>> Indicadores(CancellationToken cancellationToken)
    {
        var usuario = await _assistidas.ObterUsuarioAsync(User, cancellationToken);
        if (usuario is null) return Unauthorized();

        var agora = DateTime.UtcNow;
        var inicioMes = new DateTime(agora.Year, agora.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var hoje = DateOnly.FromDateTime(agora);
        var limiteRetorno = hoje.AddDays(7);

        var casos = await CarregarCasosAsync(cancellationToken);
        var encaminhadas = await _dbContext.Assistidas.AsNoTracking()
            .Where(a => a.Ativa && a.EncaminhadaJuridicoEm != null)
            .Select(a => a.Id)
            .ToListAsync(cancellationToken);

        var atendimentosMes = await _dbContext.AtendimentosJuridicos.AsNoTracking()
            .Where(at => at.CriadoEm >= inicioMes)
            .Select(at => at.TipoDemanda)
            .ToListAsync(cancellationToken);

        // Contagens anônimas: nenhum nome sai deste endpoint.
        return Ok(new IndicadoresJuridicoResponse
        {
            Fila = encaminhadas.Count(id => !casos.TryGetValue(id, out var c) || c.Atendimentos.Count == 0),
            CasosEmAndamento = casos.Values.Count(c => c.Atendimentos.Count > 0 && c.Situacao != CasoJuridicoSituacao.Encerrado),
            CasosEncerrados = casos.Values.Count(c => c.Situacao == CasoJuridicoSituacao.Encerrado),
            AtendimentosNoMes = atendimentosMes.Count,
            RetornosProximos7Dias = casos.Values
                .Where(c => c.Situacao != CasoJuridicoSituacao.Encerrado)
                .Select(c => c.Atendimentos.OrderByDescending(at => at.CriadoEm).FirstOrDefault()?.DataRetorno)
                .Count(data => data is not null && data >= hoje && data <= limiteRetorno),
            AtendimentosPorTipo = atendimentosMes
                .GroupBy(tipo => tipo)
                .ToDictionary(g => g.Key, g => g.Count())
        });
    }

    private sealed record AcessoCaso(
        bool AtuaComoJuridico,
        bool SouResponsavel,
        bool PodeVerConteudo,
        bool PodeAtribuir,
        DateTime? EmergenciaExpiraEm);

    private async Task<AcessoCaso> AvaliarAcessoAsync(CasoJuridico? caso, UsuarioAtuante usuario, CancellationToken cancellationToken)
    {
        var juridico = AssistidasService.AtuaComoJuridico(usuario.Perfil);
        var adm = usuario.Perfil == PerfisAcesso.Adm;
        var souResponsavel = caso?.Responsaveis.Any(r => r.UserId == usuario.UserId) == true;

        DateTime? emergencia = null;
        if (caso is not null && juridico)
        {
            var agora = DateTime.UtcNow;
            emergencia = await _dbContext.CasosJuridicosAcessosEmergencia.AsNoTracking()
                .Where(a => a.CasoJuridicoId == caso.Id && a.UserId == usuario.UserId && a.ExpiraEm > agora)
                .MaxAsync(a => (DateTime?)a.ExpiraEm, cancellationToken);
        }

        var podeVer = juridico && (caso is null
            || caso.Visibilidade == CasoJuridicoVisibilidade.Equipe
            || souResponsavel
            || emergencia is not null);

        return new AcessoCaso(juridico, souResponsavel, podeVer, adm || podeVer, emergencia);
    }

    private async Task<Dictionary<int, CasoJuridico>> CarregarCasosAsync(CancellationToken cancellationToken)
    {
        var casos = await _dbContext.CasosJuridicos.AsNoTracking()
            .Include(c => c.Responsaveis)
            .Include(c => c.Atendimentos)
            .ToListAsync(cancellationToken);

        return casos.ToDictionary(c => c.AssistidaId);
    }

    private static JuridicoAssistidaResumoResponse MontarResumo(Assistida a, CasoJuridico? caso, UsuarioAtuante usuario)
    {
        var ultimo = caso?.Atendimentos.OrderByDescending(at => at.CriadoEm).FirstOrDefault();
        var souResponsavel = caso?.Responsaveis.Any(r => r.UserId == usuario.UserId) == true;
        var podeVerDatas = AssistidasService.AtuaComoJuridico(usuario.Perfil)
            && (caso is null || caso.Visibilidade == CasoJuridicoVisibilidade.Equipe || souResponsavel);

        return new JuridicoAssistidaResumoResponse
        {
            AssistidaId = a.Id,
            Nome = a.Nome,
            CpfMascarado = AssistidasService.MascararCpf(a.Cpf),
            Telefone = a.Telefone,
            SemTelefoneSeguro = a.SemTelefoneSeguro,
            ContatoSeguro = a.ContatoSeguro,
            AcessoRestrito = a.AcessoRestrito,
            MotivoRestricao = a.MotivoRestricao,
            Origem = a.Origem,
            EncaminhadaJuridicoEm = a.EncaminhadaJuridicoEm,
            CasoId = caso?.Id,
            Situacao = caso is null || caso.Atendimentos.Count == 0 ? null : caso.Situacao,
            Visibilidade = caso?.Visibilidade,
            Responsaveis = caso?.Responsaveis.OrderBy(r => r.AtribuidoEm).Select(MapearResponsavel).ToList() ?? [],
            SouResponsavel = souResponsavel,
            TotalAtendimentos = caso?.Atendimentos.Count ?? 0,
            UltimoAtendimentoEm = ultimo?.CriadoEm,
            ProximoRetorno = podeVerDatas ? ultimo?.DataRetorno?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : null
        };
    }

    private static ResponsavelJuridicoResponse MapearResponsavel(CasoJuridicoResponsavel r)
    {
        return new ResponsavelJuridicoResponse { UserId = r.UserId, Identificador = r.Identificador, Nome = r.Nome };
    }

    private static AdendoJuridicoResponse MapearAdendo(AtendimentoJuridicoAdendo adendo)
    {
        return new AdendoJuridicoResponse
        {
            Id = adendo.Id,
            Texto = adendo.Texto,
            AutorNome = adendo.AutorNome,
            AutorIdentificador = adendo.AutorIdentificador,
            CriadoEm = adendo.CriadoEm
        };
    }

    private static AtendimentoJuridicoResponse MapearAtendimento(AtendimentoJuridico at, bool conteudo)
    {
        var resposta = new AtendimentoJuridicoResponse
        {
            Id = at.Id,
            Protocolo = at.Protocolo,
            CriadoEm = at.CriadoEm,
            ProfissionalNome = at.ProfissionalNome,
            ProfissionalIdentificador = at.ProfissionalIdentificador,
            Situacao = at.Situacao,
            ConteudoOculto = !conteudo
        };

        if (!conteudo)
        {
            return resposta;
        }

        resposta.TipoDemanda = at.TipoDemanda;
        resposta.TipoDemandaOutro = at.TipoDemandaOutro;
        resposta.TemMedidaProtetiva = at.TemMedidaProtetiva;
        resposta.NumeroProcesso = at.NumeroProcesso;
        resposta.Vara = at.Vara;
        resposta.Encaminhamentos = string.IsNullOrWhiteSpace(at.Encaminhamentos)
            ? []
            : at.Encaminhamentos.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
        resposta.EncaminhamentoOutro = at.EncaminhamentoOutro;
        resposta.Relato = at.Relato;
        resposta.ProximoPasso = at.ProximoPasso;
        resposta.DataRetorno = at.DataRetorno?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        resposta.Adendos = at.Adendos.OrderBy(a => a.CriadoEm).Select(MapearAdendo).ToList();
        return resposta;
    }

    private static string? ValidarAtendimento(NovoAtendimentoJuridicoRequest request)
    {
        if (!AtendimentoJuridicoCatalogo.TiposDemanda.Contains(request.TipoDemanda))
        {
            return "Escolha o tipo de demanda.";
        }

        if (request.TipoDemanda == "outro" && string.IsNullOrWhiteSpace(request.TipoDemandaOutro))
        {
            return "Descreva o tipo de demanda.";
        }

        if (request.Encaminhamentos.Any(e => !AtendimentoJuridicoCatalogo.Encaminhamentos.Contains(e)))
        {
            return "Encaminhamento inválido.";
        }

        var relato = request.Relato?.Trim() ?? string.Empty;
        if (relato.Length < 15) return "Descreva o relato com mais detalhes (mínimo de 15 caracteres).";
        if (relato.Length > 5000) return "O relato excedeu o limite de 5000 caracteres.";

        if (request.Situacao is not null && !CasoJuridicoSituacao.Todos.Contains(request.Situacao))
        {
            return "Situação do caso inválida.";
        }

        if (!string.IsNullOrWhiteSpace(request.DataRetorno)
            && !DateOnly.TryParse(request.DataRetorno, CultureInfo.InvariantCulture, out _))
        {
            return "Data de retorno inválida.";
        }

        return null;
    }

    private async Task AvisarResponsaveisAsync(CasoJuridico caso, UsuarioAtuante usuario)
    {
        var ids = caso.Responsaveis.Select(r => r.UserId).ToList();
        var emails = await _dbContext.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id) && u.Email != null)
            .Select(u => u.Email!)
            .ToListAsync();

        // O e-mail não leva nome da assistida nem o motivo: os detalhes ficam na auditoria do sistema.
        var corpo = $"<p>A profissional de ID {System.Net.WebUtility.HtmlEncode(usuario.Identificador)} usou o acesso de emergência "
            + $"em um caso jurídico sob sua responsabilidade (caso nº {caso.Id}).</p>"
            + "<p>O acesso vale por 12 horas e ficou registrado no histórico de ações. Entre no sistema para ver os detalhes.</p>";

        foreach (var email in emails)
        {
            try
            {
                await _emailService.EnviarAsync(email, "Acesso de emergência a um caso jurídico - Casa da Mulher", corpo, "JuridicoAcessoEmergencia");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Não foi possível avisar uma responsável sobre o acesso de emergência ao caso {CasoId}.", caso.Id);
            }
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
