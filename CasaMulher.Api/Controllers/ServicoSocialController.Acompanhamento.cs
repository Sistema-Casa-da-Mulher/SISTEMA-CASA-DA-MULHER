using System.Globalization;
using System.Text.Json;
using CasaMulher.Api.DTOs;
using CasaMulher.Api.Models;
using CasaMulher.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CasaMulher.Api.Controllers;

/// <summary>
/// Estudo social (versões com histórico), encaminhamentos para a rede e plano de acompanhamento.
/// Tudo isso é conteúdo do prontuário: só quem pode ver o conteúdo registra e consulta.
/// </summary>
public partial class ServicoSocialController
{
    private static readonly JsonSerializerOptions JsonDependentes = new(JsonSerializerDefaults.Web);

    // ------------------------------------------------------------------ estudo social

    /// <summary>Grava uma nova versão do estudo social. As anteriores ficam no histórico.</summary>
    [HttpPost("prontuarios/{assistidaId:int}/estudo")]
    public async Task<ActionResult<EstudoSocialResponse>> AtualizarEstudo(int assistidaId, EstudoSocialRequest request, CancellationToken cancellationToken)
    {
        var usuario = await _assistidas.ObterUsuarioAsync(User, cancellationToken);
        if (usuario is null) return Unauthorized();

        var erro = ValidarEstudo(request);
        if (erro is not null) return BadRequest(new { mensagem = erro });

        var (prontuario, acesso) = await CarregarProntuarioComAcessoAsync(assistidaId, usuario, rastrear: true, cancellationToken);
        if (prontuario is null) return NotFound(new { mensagem = "Abra o prontuário antes de preencher o estudo social." });
        if (!acesso.PodeVerConteudo) return Proibido("Você não tem acesso ao conteúdo deste prontuário.");

        var versao = (await _dbContext.EstudosSociais.Where(e => e.ProntuarioSocialId == prontuario.Id).MaxAsync(e => (int?)e.Versao, cancellationToken) ?? 0) + 1;
        var vulnerabilidades = ServicoSocialCatalogo.VulnerabilidadesComDerivadas(request.Vulnerabilidades, request.FaixaRenda, request.Moradia, request.ConviveComAgressor);
        var agora = DateTime.UtcNow;

        var estudo = new EstudoSocial
        {
            ProntuarioSocialId = prontuario.Id,
            Versao = versao,
            PessoasNoDomicilio = request.PessoasNoDomicilio,
            ComposicaoFamiliar = Limitar(request.ComposicaoFamiliar?.Trim(), 1000),
            DependentesJson = JsonSerializer.Serialize(
                request.Dependentes.Take(20).Select(d => new DependenteDto { Parentesco = d.Parentesco, Idade = d.Idade }),
                JsonDependentes),
            FaixaRenda = Vazio(request.FaixaRenda),
            SituacaoTrabalho = Vazio(request.SituacaoTrabalho),
            Moradia = Vazio(request.Moradia),
            ConviveComAgressor = request.ConviveComAgressor,
            Escolaridade = Vazio(request.Escolaridade),
            CadUnico = Vazio(request.CadUnico),
            Nis = AssistidasService.SomenteDigitos(request.Nis) is { Length: 11 } nis ? nis : null,
            Beneficios = Juntar(request.Beneficios),
            BeneficioOutro = request.Beneficios.Contains("outro") ? Limitar(request.BeneficioOutro?.Trim(), 120) : null,
            RedeApoio = Limitar(request.RedeApoio?.Trim(), 1000),
            Vulnerabilidades = Juntar(vulnerabilidades),
            Observacoes = Limitar(request.Observacoes?.Trim(), 2000),
            ProfissionalUserId = usuario.UserId,
            ProfissionalIdentificador = usuario.Identificador,
            ProfissionalNome = usuario.Nome,
            CriadoEm = agora
        };
        _dbContext.EstudosSociais.Add(estudo);

        var antes = prontuario.VulnerabilidadesAtuais;
        prontuario.VulnerabilidadesAtuais = estudo.Vulnerabilidades;
        prontuario.AtualizadoEm = agora;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditoria.RegistrarAsync(
            "SOCIAL_ESTUDO_ATUALIZADO",
            "ProntuarioSocial",
            prontuario.Id.ToString(CultureInfo.InvariantCulture),
            $"{usuario.Identificador} gravou a versão {versao} do estudo social do prontuário {prontuario.Numero}"
                + (antes != estudo.Vulnerabilidades ? $" (indicadores de vulnerabilidade: {vulnerabilidades.Count})." : "."));

        await _snapshot.PersistAsync("social_estudo", CancellationToken.None);
        return Ok(MapearEstudo(estudo));
    }

    private static string? ValidarEstudo(EstudoSocialRequest r)
    {
        if (r.PessoasNoDomicilio is < 1 or > 30) return "Pessoas no domicílio deve estar entre 1 e 30.";
        if (!Opcional(r.FaixaRenda, ServicoSocialCatalogo.FaixasRenda)) return "Faixa de renda inválida.";
        if (!Opcional(r.SituacaoTrabalho, ServicoSocialCatalogo.SituacoesTrabalho)) return "Situação de trabalho inválida.";
        if (!Opcional(r.Moradia, ServicoSocialCatalogo.Moradias)) return "Moradia inválida.";
        if (!Opcional(r.Escolaridade, ServicoSocialCatalogo.Escolaridades)) return "Escolaridade inválida.";
        if (!Opcional(r.CadUnico, ServicoSocialCatalogo.CadUnico)) return "Resposta do CadÚnico inválida.";
        if (r.Beneficios.Any(b => !ServicoSocialCatalogo.Beneficios.Contains(b))) return "Benefício inválido.";
        if (r.Vulnerabilidades.Any(v => !ServicoSocialCatalogo.Vulnerabilidades.Contains(v))) return "Indicador de vulnerabilidade inválido.";
        if (r.Dependentes.Count > 20) return "Informe no máximo 20 dependentes.";
        if (r.Dependentes.Any(d => !ServicoSocialCatalogo.Parentescos.Contains(d.Parentesco) || d.Idade is < 0 or > 120)) return "Confira parentesco e idade dos dependentes.";

        var nis = AssistidasService.SomenteDigitos(r.Nis);
        if (nis.Length > 0 && nis.Length != 11) return "O NIS tem 11 dígitos.";
        return null;
    }

    private static bool Opcional(string? valor, string[] permitidos) => string.IsNullOrWhiteSpace(valor) || permitidos.Contains(valor);

    private static string? Vazio(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor;

    internal static EstudoSocialResponse MapearEstudo(EstudoSocial e)
    {
        List<DependenteDto> dependentes;
        try
        {
            dependentes = JsonSerializer.Deserialize<List<DependenteDto>>(e.DependentesJson, JsonDependentes) ?? [];
        }
        catch (JsonException)
        {
            dependentes = [];
        }

        return new EstudoSocialResponse
        {
            Id = e.Id,
            Versao = e.Versao,
            PessoasNoDomicilio = e.PessoasNoDomicilio,
            ComposicaoFamiliar = e.ComposicaoFamiliar,
            Dependentes = dependentes,
            FaixaRenda = e.FaixaRenda,
            SituacaoTrabalho = e.SituacaoTrabalho,
            Moradia = e.Moradia,
            ConviveComAgressor = e.ConviveComAgressor,
            Escolaridade = e.Escolaridade,
            CadUnico = e.CadUnico,
            Nis = e.Nis,
            Beneficios = Separar(e.Beneficios),
            BeneficioOutro = e.BeneficioOutro,
            RedeApoio = e.RedeApoio,
            Vulnerabilidades = Separar(e.Vulnerabilidades),
            Observacoes = e.Observacoes,
            ProfissionalNome = e.ProfissionalNome,
            ProfissionalIdentificador = e.ProfissionalIdentificador,
            CriadoEm = e.CriadoEm
        };
    }

    // ------------------------------------------------------------------ encaminhamentos para a rede

    [HttpPost("prontuarios/{assistidaId:int}/encaminhamentos-rede")]
    public async Task<ActionResult<EncaminhamentoRedeResponse>> RegistrarEncaminhamentoRede(int assistidaId, EncaminhamentoRedeRequest request, CancellationToken cancellationToken)
    {
        var usuario = await _assistidas.ObterUsuarioAsync(User, cancellationToken);
        if (usuario is null) return Unauthorized();

        if (!ServicoSocialCatalogo.DestinosRede.Contains(request.Destino)) return BadRequest(new { mensagem = "Escolha o destino do encaminhamento." });
        if (request.Destino == "outro" && string.IsNullOrWhiteSpace(request.DestinoOutro)) return BadRequest(new { mensagem = "Informe para onde foi encaminhada." });

        var motivo = request.Motivo?.Trim() ?? string.Empty;
        if (motivo.Length < 5 || motivo.Length > 1000) return BadRequest(new { mensagem = "Descreva o motivo (entre 5 e 1000 caracteres)." });

        var hoje = DataLocal(DateTime.UtcNow);
        var data = hoje;
        if (!string.IsNullOrWhiteSpace(request.Data)
            && !DateOnly.TryParseExact(request.Data, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out data))
        {
            return BadRequest(new { mensagem = "Data do encaminhamento inválida." });
        }

        if (data > hoje) return BadRequest(new { mensagem = "A data do encaminhamento não pode estar no futuro." });

        var (prontuario, acesso) = await CarregarProntuarioComAcessoAsync(assistidaId, usuario, rastrear: true, cancellationToken);
        if (prontuario is null) return NotFound(new { mensagem = "Abra o prontuário antes de encaminhar para a rede." });
        if (!acesso.PodeVerConteudo) return Proibido("Você não tem acesso ao conteúdo deste prontuário.");

        var encaminhamento = new EncaminhamentoRede
        {
            ProntuarioSocialId = prontuario.Id,
            Destino = request.Destino,
            DestinoOutro = request.Destino == "outro" ? Limitar(request.DestinoOutro?.Trim(), 120) : null,
            Data = data,
            Motivo = motivo,
            Status = EncaminhamentoRedeStatus.Enviado,
            ProfissionalUserId = usuario.UserId,
            ProfissionalIdentificador = usuario.Identificador,
            ProfissionalNome = usuario.Nome,
            CriadoEm = DateTime.UtcNow
        };
        _dbContext.EncaminhamentosRede.Add(encaminhamento);
        prontuario.AtualizadoEm = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditoria.RegistrarAsync(
            "SOCIAL_ENCAMINHAMENTO_REDE",
            "ProntuarioSocial",
            prontuario.Id.ToString(CultureInfo.InvariantCulture),
            $"{usuario.Identificador} encaminhou a assistida do prontuário social {prontuario.Numero} para a rede ({request.Destino}).");

        await _snapshot.PersistAsync("social_rede", CancellationToken.None);
        return Ok(MapearEncaminhamento(encaminhamento));
    }

    [HttpPut("encaminhamentos-rede/{encaminhamentoId:int}")]
    public async Task<ActionResult<EncaminhamentoRedeResponse>> AtualizarEncaminhamentoRede(int encaminhamentoId, AtualizarEncaminhamentoRedeRequest request, CancellationToken cancellationToken)
    {
        var usuario = await _assistidas.ObterUsuarioAsync(User, cancellationToken);
        if (usuario is null) return Unauthorized();

        if (!EncaminhamentoRedeStatus.Todos.Contains(request.Status)) return BadRequest(new { mensagem = "Situação inválida." });
        if ((request.Observacao?.Trim().Length ?? 0) > 500) return BadRequest(new { mensagem = "A observação passou de 500 caracteres." });

        var encaminhamento = await _dbContext.EncaminhamentosRede
            .Include(e => e.Prontuario).ThenInclude(p => p!.Responsaveis)
            .SingleOrDefaultAsync(e => e.Id == encaminhamentoId, cancellationToken);
        if (encaminhamento?.Prontuario is null) return NotFound(new { mensagem = "Encaminhamento não encontrado." });

        var acesso = await _acesso.AvaliarAsync(encaminhamento.Prontuario, usuario, cancellationToken);
        if (!acesso.PodeVerConteudo) return Proibido("Você não tem acesso ao conteúdo deste prontuário.");

        var anterior = encaminhamento.Status;
        encaminhamento.Status = request.Status;
        encaminhamento.ObservacaoStatus = Limitar(request.Observacao?.Trim(), 500);
        encaminhamento.StatusAtualizadoEm = DateTime.UtcNow;
        encaminhamento.StatusAtualizadoPorIdentificador = usuario.Identificador;
        encaminhamento.Prontuario.AtualizadoEm = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditoria.RegistrarAsync(
            "SOCIAL_ENCAMINHAMENTO_REDE_STATUS",
            "ProntuarioSocial",
            encaminhamento.ProntuarioSocialId.ToString(CultureInfo.InvariantCulture),
            $"{usuario.Identificador} mudou o encaminhamento à rede #{encaminhamento.Id} do prontuário social {encaminhamento.Prontuario.Numero} de \"{anterior}\" para \"{request.Status}\".");

        await _snapshot.PersistAsync("social_rede", CancellationToken.None);
        return Ok(MapearEncaminhamento(encaminhamento));
    }

    internal static EncaminhamentoRedeResponse MapearEncaminhamento(EncaminhamentoRede e) => new()
    {
        Id = e.Id,
        Destino = e.Destino,
        DestinoOutro = e.DestinoOutro,
        Data = e.Data.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        Motivo = e.Motivo,
        Status = e.Status,
        ObservacaoStatus = e.ObservacaoStatus,
        StatusAtualizadoEm = e.StatusAtualizadoEm,
        ProfissionalNome = e.ProfissionalNome,
        ProfissionalIdentificador = e.ProfissionalIdentificador,
        CriadoEm = e.CriadoEm
    };

    // ------------------------------------------------------------------ plano de acompanhamento

    [HttpPost("prontuarios/{assistidaId:int}/metas")]
    public async Task<ActionResult<MetaSocialResponse>> RegistrarMeta(int assistidaId, MetaSocialRequest request, CancellationToken cancellationToken)
    {
        var usuario = await _assistidas.ObterUsuarioAsync(User, cancellationToken);
        if (usuario is null) return Unauthorized();

        var descricao = request.Descricao?.Trim() ?? string.Empty;
        if (descricao.Length < 3 || descricao.Length > 500) return BadRequest(new { mensagem = "Descreva a meta ou ação (entre 3 e 500 caracteres)." });

        DateOnly? prazo = null;
        if (!string.IsNullOrWhiteSpace(request.Prazo))
        {
            if (!DateOnly.TryParseExact(request.Prazo, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var p))
            {
                return BadRequest(new { mensagem = "Prazo inválido." });
            }

            prazo = p;
        }

        var (prontuario, acesso) = await CarregarProntuarioComAcessoAsync(assistidaId, usuario, rastrear: true, cancellationToken);
        if (prontuario is null) return NotFound(new { mensagem = "Abra o prontuário antes de montar o plano." });
        if (!acesso.PodeVerConteudo) return Proibido("Você não tem acesso ao conteúdo deste prontuário.");

        var agora = DateTime.UtcNow;
        var meta = new MetaAcompanhamentoSocial
        {
            ProntuarioSocialId = prontuario.Id,
            Descricao = descricao,
            Prazo = prazo,
            Situacao = MetaSocialSituacao.Pendente,
            CriadaPorUserId = usuario.UserId,
            CriadaPorIdentificador = usuario.Identificador,
            CriadaPorNome = usuario.Nome,
            CriadoEm = agora,
            AtualizadoEm = agora
        };
        _dbContext.MetasAcompanhamentoSocial.Add(meta);
        prontuario.AtualizadoEm = agora;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditoria.RegistrarAsync(
            "SOCIAL_META_CRIADA",
            "ProntuarioSocial",
            prontuario.Id.ToString(CultureInfo.InvariantCulture),
            $"{usuario.Identificador} incluiu a meta #{meta.Id} no plano do prontuário social {prontuario.Numero}.");

        await _snapshot.PersistAsync("social_meta", CancellationToken.None);
        return Ok(MapearMeta(meta, DataLocal(agora)));
    }

    [HttpPut("metas/{metaId:int}")]
    public async Task<ActionResult<MetaSocialResponse>> AtualizarMeta(int metaId, AtualizarMetaSocialRequest request, CancellationToken cancellationToken)
    {
        var usuario = await _assistidas.ObterUsuarioAsync(User, cancellationToken);
        if (usuario is null) return Unauthorized();

        if (!MetaSocialSituacao.Todos.Contains(request.Situacao)) return BadRequest(new { mensagem = "Situação inválida." });

        var meta = await _dbContext.MetasAcompanhamentoSocial
            .Include(m => m.Prontuario).ThenInclude(p => p!.Responsaveis)
            .SingleOrDefaultAsync(m => m.Id == metaId, cancellationToken);
        if (meta?.Prontuario is null) return NotFound(new { mensagem = "Meta não encontrada." });

        var acesso = await _acesso.AvaliarAsync(meta.Prontuario, usuario, cancellationToken);
        if (!acesso.PodeVerConteudo) return Proibido("Você não tem acesso ao conteúdo deste prontuário.");

        var agora = DateTime.UtcNow;
        var anterior = meta.Situacao;
        meta.Situacao = request.Situacao;
        meta.ConcluidaEm = request.Situacao == MetaSocialSituacao.Concluida ? meta.ConcluidaEm ?? agora : null;
        meta.AtualizadaPorIdentificador = usuario.Identificador;
        meta.AtualizadoEm = agora;
        meta.Prontuario.AtualizadoEm = agora;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditoria.RegistrarAsync(
            "SOCIAL_META_ATUALIZADA",
            "ProntuarioSocial",
            meta.ProntuarioSocialId.ToString(CultureInfo.InvariantCulture),
            $"{usuario.Identificador} mudou a meta #{meta.Id} do prontuário social {meta.Prontuario.Numero} de \"{anterior}\" para \"{request.Situacao}\".");

        await _snapshot.PersistAsync("social_meta", CancellationToken.None);
        return Ok(MapearMeta(meta, DataLocal(agora)));
    }

    internal static MetaSocialResponse MapearMeta(MetaAcompanhamentoSocial m, DateOnly hoje) => new()
    {
        Id = m.Id,
        Descricao = m.Descricao,
        Prazo = m.Prazo?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        Situacao = m.Situacao,
        Atrasada = m.Situacao != MetaSocialSituacao.Concluida && m.Prazo is not null && m.Prazo < hoje,
        ConcluidaEm = m.ConcluidaEm,
        CriadaPorNome = m.CriadaPorNome,
        CriadoEm = m.CriadoEm,
        AtualizadoEm = m.AtualizadoEm
    };
}
