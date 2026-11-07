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
/// Agenda e Calendário. Toda regra de sigilo e de conflito roda AQUI no servidor:
/// a tela só recebe o que a pessoa pode ver, e cada gravação refaz a conferência.
/// Ações de agendamentos, aulas, configuração e reavaliação ficam em AgendaController.Acoes.cs.
/// </summary>
[ApiController]
[Route("api/agenda")]
[Authorize(Policy = PoliticasAcesso.AcessoAgenda)]
public partial class AgendaController : ControllerBase
{
    private const int MaxDiasLista = 62;
    private const int MaxDiasDisponibilidade = 21;
    private const int MaxOcorrencias = 12;

    private readonly AppDbContext _db;
    private readonly AgendaService _agenda;
    private readonly AssistidasService _assistidas;
    private readonly IAuditoriaService _auditoria;
    private readonly SecuritySnapshotPersistenceService _snapshot;

    public AgendaController(
        AppDbContext db,
        AgendaService agenda,
        AssistidasService assistidas,
        IAuditoriaService auditoria,
        SecuritySnapshotPersistenceService snapshot)
    {
        _db = db;
        _agenda = agenda;
        _assistidas = assistidas;
        _auditoria = auditoria;
        _snapshot = snapshot;
    }

    // ------------------------------------------------------------------ leitura

    [HttpGet("me")]
    public async Task<ActionResult<AgendaMeResponse>> Me(CancellationToken ct)
    {
        var v = await _assistidas.ObterUsuarioAsync(User, ct);
        if (v is null) return Unauthorized();
        var ctx = await _agenda.CarregarContextoAsync(ct);
        var area = AgendaService.AreaDoPerfil(v.Perfil);

        var filtrosDisp = new List<string> { "meus", "equipe", "compartilhados", "casa" };
        if (area is not null) filtrosDisp.Insert(1, "area");
        if (v.Perfil == PerfisAcesso.Professor) filtrosDisp.Add("turmas");
        filtrosDisp.Add("outrasTurmas");
        if (v.Perfil == PerfisAcesso.Recepcao) filtrosDisp.Add("esperadas");
        if (v.Perfil == PerfisAcesso.Adm) filtrosDisp.Add("contagem");

        var padrao = v.Perfil switch
        {
            PerfisAcesso.Recepcao => new List<string> { "meus", "esperadas", "equipe", "casa" },
            PerfisAcesso.Professor => new List<string> { "meus", "turmas", "equipe", "casa" },
            PerfisAcesso.Adm => new List<string> { "meus", "equipe", "casa", "contagem" },
            _ => new List<string> { "meus", "area", "equipe", "compartilhados", "casa" }
        };

        return Ok(new AgendaMeResponse
        {
            Id = v.UserId, Nome = v.Nome, Perfil = v.Perfil, Area = area, Agora = AgendaService.Iso(_agenda.AgoraUtc),
            FiltrosDisponiveis = filtrosDisp, FiltrosPadrao = padrao.Where(filtrosDisp.Contains).ToList(),
            Pessoas = Pessoas(ctx).Where(p => p.Id != v.UserId).ToList(),
            TiposManuais = TiposManuais(v.Perfil).Select(t => new[] { t.Key, t.Value }).ToList(),
            TiposAgendamento = TiposAgendamento(area).Select(t => new[] { t.Key, t.Value }).ToList(),
            DuracaoPadrao = area == AgendaArea.Psicologia ? 50 : 60,
            DeslocamentoPadrao = area == AgendaArea.ServicoSocial ? 30 : 0,
            ExpedienteInicio = ctx.Config.ExpedienteInicio.ToString("HH:mm", CultureInfo.InvariantCulture),
            ExpedienteFim = ctx.Config.ExpedienteFim.ToString("HH:mm", CultureInfo.InvariantCulture)
        });
    }

    /// <summary>Itens do período já filtrados. "Ocupado" e "oculto" nunca vêm na lista: disponibilidade só ao agendar.</summary>
    [HttpGet]
    public async Task<ActionResult<AgendaListaResponse>> Listar([FromQuery] string de, [FromQuery] string ate, CancellationToken ct)
    {
        var v = await _assistidas.ObterUsuarioAsync(User, ct);
        if (v is null) return Unauthorized();
        if (!LerDia(de, out var d0) || !LerDia(ate, out var d1) || d1 < d0 || d1.DayNumber - d0.DayNumber > MaxDiasLista)
            return BadRequest(new { mensagem = "Período inválido." });

        var ctx = await _agenda.CarregarContextoAsync(ct);
        var itens = await _agenda.ItensAsync(ctx, AgendaService.InicioDiaUtc(d0), AgendaService.InicioDiaUtc(d1.AddDays(1)), ct);
        var resp = new AgendaListaResponse { Agora = AgendaService.Iso(_agenda.AgoraUtc) };
        foreach (var it in itens.OrderBy(i => i.IniUtc))
        {
            var nivel = _agenda.Revelar(ctx, it, v);
            if (nivel is AgendaService.NivelOcupado or AgendaService.NivelOculto or AgendaService.NivelContagem) continue;
            resp.Itens.Add(_agenda.Montar(ctx, it, v, nivel));
        }
        if (v.Perfil == PerfisAcesso.Adm) resp.Contagens = AgendaService.Contagens(itens);
        return Ok(resp);
    }

    /// <summary>Detalhe de um item. Com assistida vinculada, abrir fica registrado no histórico de ações.</summary>
    [HttpGet("itens/{id}")]
    public async Task<ActionResult<AgendaDetalheResponse>> Detalhe(string id, CancellationToken ct)
    {
        var v = await _assistidas.ObterUsuarioAsync(User, ct);
        if (v is null) return Unauthorized();
        var ctx = await _agenda.CarregarContextoAsync(ct);
        var it = await AcharItemAsync(ctx, id, ct);
        if (it is null) return NaoEncontrado();
        var nivel = _agenda.Revelar(ctx, it, v);
        if (nivel is AgendaService.NivelOcupado or AgendaService.NivelOculto or AgendaService.NivelContagem) return NaoEncontrado();

        var resp = await _agenda.DetalheAsync(ctx, it, v, nivel, ct);
        if (v.Perfil == PerfisAcesso.Recepcao && nivel == AgendaService.NivelDetalhe) resp.Acoes.Remove("chegou");
        if (it.AssistidaId is int aid && nivel is AgendaService.NivelDetalhe or AgendaService.NivelRecepcao)
        {
            await _auditoria.RegistrarAsync("AGENDA_DETALHE_ABERTO", "Agenda", id, $"{v.Identificador} abriu o detalhe de um item da agenda vinculado a {AgendaService.CodigoAssistida(aid)}.", v.Identificador);
        }
        return Ok(resp);
    }

    [HttpGet("pendencias")]
    public async Task<ActionResult<List<AgendaPendenciaResponse>>> Pendencias(CancellationToken ct)
    {
        var v = await _assistidas.ObterUsuarioAsync(User, ct);
        if (v is null) return Unauthorized();
        var ctx = await _agenda.CarregarContextoAsync(ct);
        return Ok(await _agenda.PendenciasAsync(ctx, v, ct));
    }

    /// <summary>Assistidas que a pessoa pode vincular/agendar na área dela (permissão na origem, não na ficha).</summary>
    [HttpGet("origens")]
    public async Task<ActionResult<List<AgendaOrigemResponse>>> Origens(CancellationToken ct)
    {
        var v = await _assistidas.ObterUsuarioAsync(User, ct);
        if (v is null) return Unauthorized();
        var area = AgendaService.AreaDoPerfil(v.Perfil);
        if (area is null) return Ok(new List<AgendaOrigemResponse>());
        var ctx = await _agenda.CarregarContextoAsync(ct);
        var lista = ctx.Assistidas.Values
            .Where(a => a.Ativa && PodeVincular(ctx, v, a.Id))
            .OrderBy(a => a.Nome)
            .Select(a => new AgendaOrigemResponse
            {
                AssistidaId = a.Id, Identificacao = ctx.Identificacao(a.Id),
                Referencia = area switch
                {
                    AgendaArea.Psicologia => ctx.ProntuariosPsi.TryGetValue(a.Id, out var p) ? p.Numero : "encaminhada",
                    AgendaArea.ServicoSocial => ctx.ProntuariosSoc.TryGetValue(a.Id, out var s) ? s.Numero : "encaminhada",
                    _ => ctx.Casos.ContainsKey(a.Id) ? "caso jurídico" : "encaminhada"
                },
                Restrito = area switch
                {
                    AgendaArea.Psicologia => ctx.ProntuariosPsi.TryGetValue(a.Id, out var p) && p.Visibilidade != ProntuarioVisibilidade.Equipe,
                    AgendaArea.ServicoSocial => ctx.ProntuariosSoc.TryGetValue(a.Id, out var s) && s.Visibilidade != ProntuarioVisibilidade.Equipe,
                    _ => ctx.Casos.TryGetValue(a.Id, out var c) && c.Visibilidade != CasoJuridicoVisibilidade.Equipe
                }
            })
            .ToList();
        return Ok(lista);
    }

    // ------------------------------------------------------------------ conferência e disponibilidade

    [HttpPost("conferir")]
    public async Task<ActionResult<List<AgendaConferenciaOcorrenciaResponse>>> Conferir(AgendaConferirRequest req, CancellationToken ct)
    {
        var v = await _assistidas.ObterUsuarioAsync(User, ct);
        if (v is null) return Unauthorized();
        if (req.Ocorrencias.Count is 0 or > MaxOcorrencias) return BadRequest(new { mensagem = "Informe de 1 a 12 datas." });
        var ctx = await _agenda.CarregarContextoAsync(ct);
        var dono = string.IsNullOrEmpty(req.DonoId) ? v.UserId : req.DonoId;
        var pessoasErro = ValidarPessoas(ctx, [dono, .. req.Participantes]);
        if (pessoasErro is not null) return BadRequest(new { mensagem = pessoasErro });
        if (req.AssistidaId is int aid && !PodeVincular(ctx, v, aid)) return Proibido("Você não tem permissão no caso/prontuário dessa assistida.");

        var ocs = new List<(DateTime Ini, DateTime Fim, AgendaOcorrenciaRequest O)>();
        foreach (var o in req.Ocorrencias)
        {
            if (!LerOcorrencia(o, req.DiaInteiro, out var ini, out var fim, out var erro)) return BadRequest(new { mensagem = erro });
            ocs.Add((ini, fim, o));
        }
        var itens = await ItensDoPeriodoAsync(ctx, ocs.Min(o => o.Ini), ocs.Max(o => o.Fim), ct);
        var saida = new List<AgendaConferenciaOcorrenciaResponse>();
        foreach (var o in ocs)
        {
            var cf = Conferencia(ctx, itens, v, dono, req.Participantes, req.AssistidaId, o.Ini, o.Fim, req.DiaInteiro, req.DeslocamentoMinutos, req.IgnorarItemId, false);
            saida.Add(new AgendaConferenciaOcorrenciaResponse { Inicio = AgendaService.Iso(o.Ini), Rotulo = Rotulo(o.Ini), Conflitos = cf.Select(Dto).ToList() });
        }
        return Ok(saida);
    }

    /// <summary>"Ver disponibilidade": só dentro de um formulário, para as pessoas do compromisso. O motivo só aparece se a pessoa puder vê-lo.</summary>
    [HttpPost("disponibilidade")]
    public async Task<ActionResult<List<AgendaFaixaResponse>>> Disponibilidade(AgendaDisponibilidadeRequest req, CancellationToken ct)
    {
        var v = await _assistidas.ObterUsuarioAsync(User, ct);
        if (v is null) return Unauthorized();
        if (!LerDia(req.De, out var d0) || !LerDia(req.Ate, out var d1) || d1 < d0 || d1.DayNumber - d0.DayNumber > MaxDiasDisponibilidade)
            return BadRequest(new { mensagem = "Período inválido." });
        var ctx = await _agenda.CarregarContextoAsync(ct);
        var pessoas = req.Pessoas.Where(p => !string.IsNullOrEmpty(p)).Distinct().ToList();
        if (!pessoas.Contains(v.UserId)) pessoas.Insert(0, v.UserId);
        if (pessoas.Count > 8) return BadRequest(new { mensagem = "Escolha até 8 pessoas." });
        var erro = ValidarPessoas(ctx, pessoas);
        if (erro is not null) return BadRequest(new { mensagem = erro });
        if (req.AssistidaId is int aid && !PodeVincular(ctx, v, aid)) return Proibido("Você não tem permissão no caso/prontuário dessa assistida.");

        var itens = await _agenda.ItensAsync(ctx, AgendaService.InicioDiaUtc(d0), AgendaService.InicioDiaUtc(d1.AddDays(1)), ct);
        var faixas = _agenda.Faixas(ctx, itens, v, pessoas, req.AssistidaId, req.IgnorarItemId);
        await _auditoria.RegistrarAsync("AGENDA_DISPONIBILIDADE", "Agenda", null,
            $"{v.Identificador} consultou disponibilidade ({req.De} a {req.Ate}) de {string.Join(", ", pessoas.Select(ctx.Curto))}{(req.AssistidaId is int a2 ? " e de " + AgendaService.CodigoAssistida(a2) : "")}.", v.Identificador);
        return Ok(faixas.Select(f => new AgendaFaixaResponse
        {
            Inicio = AgendaService.Iso(f.IniUtc), Fim = AgendaService.Iso(f.FimUtc), Rotulo = f.Rotulo, Revelado = f.Revelado, Aviso = f.Aviso, Cor = f.Cor
        }).ToList());
    }

    // ------------------------------------------------------------------ compromissos manuais

    [HttpPost("compromissos")]
    public async Task<ActionResult> CriarCompromisso(AgendaCompromissoRequest req, CancellationToken ct)
    {
        var v = await _assistidas.ObterUsuarioAsync(User, ct);
        if (v is null) return Unauthorized();
        var tipos = TiposManuais(v.Perfil);
        if (!tipos.ContainsKey(req.Tipo) || req.Tipo == AgendaTipoManual.Comunicacao) return BadRequest(new { mensagem = "Tipo de compromisso inválido para o seu perfil." });
        if (!AgendaVisibilidade.Todas.Contains(req.Visibilidade)) return BadRequest(new { mensagem = "Escolha quem pode ver." });
        var ausencia = req.Tipo == AgendaTipoManual.Ausencia;
        var titulo = (req.Titulo ?? "").Trim();
        if (titulo.Length == 0) titulo = ausencia ? "Ausência" : tipos[req.Tipo];
        if (titulo.Length > 120) return BadRequest(new { mensagem = "Título muito longo (até 120 caracteres)." });
        var local = string.IsNullOrWhiteSpace(req.Local) ? null : req.Local.Trim();
        if (local is { Length: > 120 }) return BadRequest(new { mensagem = "Local muito longo." });

        var ctx = await _agenda.CarregarContextoAsync(ct);
        var dono = ausencia && !string.IsNullOrEmpty(req.DonoId) ? req.DonoId : v.UserId;
        var participantes = ausencia || AgendaTipoManual.NuncaOcupa(req.Tipo)
            ? []
            : req.Participantes.Where(p => !string.IsNullOrEmpty(p) && p != dono).Distinct().ToList();
        var podemVer = req.Visibilidade == AgendaVisibilidade.Pessoas ? req.PodemVer.Where(p => !string.IsNullOrEmpty(p) && p != dono).Distinct().ToList() : [];
        var erroPessoas = ValidarPessoas(ctx, [dono, .. participantes, .. podemVer]);
        if (erroPessoas is not null) return BadRequest(new { mensagem = erroPessoas });

        int? assistidaId = ausencia ? null : req.AssistidaId;
        string? origemArea = null;
        if (assistidaId is int aid)
        {
            if (!PodeVincular(ctx, v, aid)) return Proibido("Você não tem permissão no caso/prontuário dessa assistida.");
            if (req.Visibilidade == AgendaVisibilidade.Equipe) return BadRequest(new { mensagem = "Com assistida vinculada, “Toda a equipe” não pode ser usado." });
            origemArea = AgendaService.AreaDoPerfil(v.Perfil);
        }

        if (req.Ocorrencias.Count == 0 || req.Ocorrencias.Count > (ausencia ? 1 : MaxOcorrencias))
            return BadRequest(new { mensagem = ausencia ? "Ausência é registrada uma data por vez." : "Informe de 1 a 12 datas." });
        var ocs = new List<(DateTime Ini, DateTime Fim, AgendaOcorrenciaRequest O)>();
        foreach (var o in req.Ocorrencias)
        {
            if (!LerOcorrencia(o, req.DiaInteiro, out var ini, out var fim, out var erro)) return BadRequest(new { mensagem = erro });
            ocs.Add((ini, fim, o));
        }

        var ocupa = !AgendaTipoManual.NuncaOcupa(req.Tipo);
        // conferência refeita no servidor, data a data (ausência não é conferida: ela é o aviso)
        if (ocupa && !ausencia)
        {
            var itens = await ItensDoPeriodoAsync(ctx, ocs.Min(o => o.Ini), ocs.Max(o => o.Fim), ct);
            var problemas = new List<AgendaConferenciaOcorrenciaResponse>();
            foreach (var o in ocs)
            {
                var falha = Decidir(ctx, itens, v, dono, participantes, assistidaId, o.Ini, o.Fim, req.DiaInteiro, 0, null, o.O);
                if (falha is not null) problemas.Add(falha);
            }
            if (problemas.Count > 0) return Conflict(new { mensagem = "Há conflitos sem confirmação. Nada foi salvo.", ocorrencias = problemas });
        }

        var serie = ocs.Count > 1 ? Guid.NewGuid().ToString("N") : null;
        var criados = new List<AgendaCompromisso>();
        foreach (var o in ocs)
        {
            var c = new AgendaCompromisso
            {
                Tipo = req.Tipo, Titulo = titulo, DonoUserId = dono, RegistradoPorUserId = v.UserId,
                InicioUtc = o.Ini, FimUtc = req.DiaInteiro ? null : o.Fim, DiaInteiro = req.DiaInteiro, OcupaHorario = ocupa,
                Local = local, OrigemArea = origemArea, AssistidaId = assistidaId, Visibilidade = req.Visibilidade,
                SituacaoAusencia = ausencia ? (dono == v.UserId ? "confirmada" : "informada") : null,
                AusenciaConfirmadaPorIdentificador = ausencia && dono == v.UserId ? v.Identificador : null,
                SerieId = serie, Justificativa = Limpo(o.O.Justificativa),
                ExcecaoMotivo = v.Perfil == PerfisAcesso.Adm ? Limpo(o.O.ExcecaoMotivo) : null,
                ExcecaoPorIdentificador = v.Perfil == PerfisAcesso.Adm && Limpo(o.O.ExcecaoMotivo) is not null ? v.Identificador : null,
                CriadoEm = _agenda.AgoraUtc
            };
            foreach (var p in participantes) c.Pessoas.Add(new AgendaCompromissoPessoa { UserId = p, Papel = AgendaPapel.Participante });
            foreach (var p in podemVer.Where(p => !participantes.Contains(p))) c.Pessoas.Add(new AgendaCompromissoPessoa { UserId = p, Papel = AgendaPapel.Ver });
            _db.AgendaCompromissos.Add(c);
            criados.Add(c);
        }
        await _db.SaveChangesAsync(ct);

        var extras = new List<string>();
        if (dono != v.UserId) extras.Add("em nome de " + ctx.Curto(dono) + " (informada)");
        if (ocs.Count > 1) extras.Add($"{ocs.Count} datas");
        var just = criados.Where(c => c.Justificativa is not null).Select(c => AgendaService.Dm(c.InicioUtc) + " “" + c.Justificativa + "”").ToList();
        if (just.Count > 0) extras.Add("confirmou por cima de conflito: " + string.Join("; ", just));
        var exc = criados.Where(c => c.ExcecaoMotivo is not null).Select(c => AgendaService.Dm(c.InicioUtc) + " (" + c.ExcecaoMotivo + ")").ToList();
        if (exc.Count > 0) extras.Add("exceção de unidade fechada só para este compromisso: " + string.Join("; ", exc));
        await _auditoria.RegistrarAsync(ausencia ? "AGENDA_AUSENCIA_REGISTRADA" : "AGENDA_COMPROMISSO_CRIADO", "AgendaCompromisso", criados[0].Id.ToString(CultureInfo.InvariantCulture),
            $"{v.Identificador} criou {req.Tipo}{(assistidaId is int a3 ? " vinculado a " + AgendaService.CodigoAssistida(a3) : "")}{(extras.Count > 0 ? " · " + string.Join(" · ", extras) : "")}.", v.Identificador);
        var snap = await _snapshot.PersistAsync("agenda_compromisso", CancellationToken.None);
        return Ok(new { ids = criados.Select(c => "cp:" + c.Id).ToList(), aviso = snap.AvisoSnapshot });
    }

    [HttpPost("compromissos/{id:int}/cancelar")]
    public async Task<ActionResult> CancelarCompromisso(int id, AgendaCancelarRequest req, CancellationToken ct)
    {
        var v = await _assistidas.ObterUsuarioAsync(User, ct);
        if (v is null) return Unauthorized();
        var c = await _db.AgendaCompromissos.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return NaoEncontrado();
        if (c.DonoUserId != v.UserId && c.RegistradoPorUserId != v.UserId) return Proibido("Só quem criou ou a dona do compromisso pode cancelar.");
        if (c.Situacao != "ativo") return BadRequest(new { mensagem = "Este compromisso já foi cancelado." });
        var agora = _agenda.AgoraUtc;
        if ((c.FimUtc ?? c.InicioUtc.AddDays(1)) <= agora) return BadRequest(new { mensagem = "Compromisso que já passou não é cancelado: ele fica no histórico." });

        var alvo = new List<AgendaCompromisso> { c };
        if (c.SerieId is not null && req.Escopo is "seguintes" or "todas")
        {
            var serie = await _db.AgendaCompromissos.Where(x => x.SerieId == c.SerieId && x.Situacao == "ativo" && x.InicioUtc > agora).ToListAsync(ct);
            alvo = req.Escopo == "seguintes" ? serie.Where(x => x.InicioUtc >= c.InicioUtc).ToList() : serie;
            if (!alvo.Contains(c) && c.InicioUtc > agora) alvo.Add(c);
        }
        foreach (var x in alvo) x.Situacao = "cancelado";
        await _db.SaveChangesAsync(ct);
        await _auditoria.RegistrarAsync("AGENDA_COMPROMISSO_CANCELADO", "AgendaCompromisso", id.ToString(CultureInfo.InvariantCulture),
            $"{v.Identificador} cancelou {alvo.Count} ocorrência(s) ({req.Escopo}).", v.Identificador);
        var snap = await _snapshot.PersistAsync("agenda_compromisso", CancellationToken.None);
        return Ok(new { canceladas = alvo.Count, aviso = snap.AvisoSnapshot });
    }

    /// <summary>Ausência informada por colega: a própria pessoa ou a ADM confirma (passa a pedir confirmação) ou diz que não procede.</summary>
    [HttpPost("compromissos/{id:int}/ausencia/{acao}")]
    public async Task<ActionResult> DecidirAusencia(int id, string acao, CancellationToken ct)
    {
        var v = await _assistidas.ObterUsuarioAsync(User, ct);
        if (v is null) return Unauthorized();
        if (acao is not ("confirmar" or "recusar")) return BadRequest(new { mensagem = "Ação inválida." });
        var c = await _db.AgendaCompromissos.SingleOrDefaultAsync(x => x.Id == id && x.Tipo == AgendaTipoManual.Ausencia, ct);
        if (c is null) return NaoEncontrado();
        if (c.DonoUserId != v.UserId && v.Perfil != PerfisAcesso.Adm) return Proibido("Só a própria pessoa ou a ADM confirma a ausência.");
        if (c.SituacaoAusencia != "informada" || c.Situacao != "ativo") return BadRequest(new { mensagem = "Esta ausência já foi decidida." });
        c.SituacaoAusencia = acao == "confirmar" ? "confirmada" : "recusada";
        c.AusenciaConfirmadaPorIdentificador = v.Identificador;
        await _db.SaveChangesAsync(ct);
        await _auditoria.RegistrarAsync("AGENDA_AUSENCIA_" + (acao == "confirmar" ? "CONFIRMADA" : "RECUSADA"), "AgendaCompromisso", id.ToString(CultureInfo.InvariantCulture),
            $"{v.Identificador} {(acao == "confirmar" ? "confirmou" : "marcou como não procede")} a ausência informada.", v.Identificador);
        var snap = await _snapshot.PersistAsync("agenda_ausencia", CancellationToken.None);
        return Ok(new { situacao = c.SituacaoAusencia, aviso = snap.AvisoSnapshot });
    }

    // ------------------------------------------------------------------ comunicação de falta/atraso

    /// <summary>Alunas que a pessoa pode comunicar e as próximas aulas de cada uma.</summary>
    [HttpGet("comunicacoes/opcoes")]
    public async Task<ActionResult<List<AgendaAlunaAulasResponse>>> OpcoesComunicacao(CancellationToken ct)
    {
        var v = await _assistidas.ObterUsuarioAsync(User, ct);
        if (v is null) return Unauthorized();
        if (!TiposManuais(v.Perfil).ContainsKey(AgendaTipoManual.Comunicacao)) return Ok(new List<AgendaAlunaAulasResponse>());
        var ctx = await _agenda.CarregarContextoAsync(ct);
        var agora = _agenda.AgoraUtc;
        var aulas = (await _agenda.ItensAsync(ctx, agora, agora.AddDays(21), ct))
            .Where(i => i.Fonte == "aula" && i.IniUtc > agora && i.AulaCancelada is null).OrderBy(i => i.IniUtc).ToList();
        var saida = new List<AgendaAlunaAulasResponse>();
        foreach (var aid in ctx.Alunas.Where(a => a.Situacao == AssistidaCursoSituacao.Cursando).Select(a => a.AssistidaId).Distinct())
        {
            if (!ctx.Assistidas.TryGetValue(aid, out var a) || !a.Ativa || !PodeComunicar(ctx, v, aid, null)) continue;
            var dela = aulas.Where(i => ctx.AlunasDoCurso(i.CursoId!.Value).Contains(aid) && PodeComunicar(ctx, v, aid, i.CursoId)).Take(6)
                .Select(i => new[] { i.Id, $"{ctx.Cursos[i.CursoId!.Value].Nome} · {AgendaService.DiaSemana(i.IniUtc)} {AgendaService.Dm(i.IniUtc)} {AgendaService.Hm(i.IniUtc)}" }).ToList();
            if (dela.Count > 0) saida.Add(new AgendaAlunaAulasResponse { AssistidaId = aid, Identificacao = ctx.Identificacao(aid), Aulas = dela });
        }
        return Ok(saida.OrderBy(s => s.Identificacao).ToList());
    }

    /// <summary>Só uma comunicação: não marca nada na chamada e não guarda o motivo pessoal.</summary>
    [HttpPost("comunicacoes")]
    public async Task<ActionResult> Comunicar(AgendaComunicacaoRequest req, CancellationToken ct)
    {
        var v = await _assistidas.ObterUsuarioAsync(User, ct);
        if (v is null) return Unauthorized();
        if (!TiposManuais(v.Perfil).ContainsKey(AgendaTipoManual.Comunicacao)) return Proibido("Seu perfil não registra comunicação de falta.");
        if (req.Comunicacao is not ("falta" or "atraso")) return BadRequest(new { mensagem = "Escolha falta ou atraso." });
        var ctx = await _agenda.CarregarContextoAsync(ct);
        var aula = await AcharItemAsync(ctx, req.AulaId, ct);
        if (aula is null || aula.Fonte != "aula") return BadRequest(new { mensagem = "Aula não encontrada." });
        if (aula.AulaCancelada is not null || aula.IniUtc <= _agenda.AgoraUtc) return BadRequest(new { mensagem = "Escolha uma aula futura que não esteja cancelada." });
        if (!ctx.AlunasDoCurso(aula.CursoId!.Value).Contains(req.AssistidaId)) return BadRequest(new { mensagem = "Essa assistida não está cursando essa turma." });
        if (!PodeComunicar(ctx, v, req.AssistidaId, aula.CursoId)) return Proibido("Você não pode registrar comunicação para essa aluna.");

        var profs = ctx.ProfessorasDoCurso(aula.CursoId.Value).ToList();
        var c = new AgendaCompromisso
        {
            Tipo = AgendaTipoManual.Comunicacao, Titulo = req.Comunicacao == "falta" ? "Avisou que vai faltar" : "Avisou que vai chegar atrasada",
            DonoUserId = profs.FirstOrDefault() ?? v.UserId, RegistradoPorUserId = v.UserId, InicioUtc = aula.IniUtc, FimUtc = null,
            OcupaHorario = false, AssistidaId = req.AssistidaId, CursoId = aula.CursoId, Comunicacao = req.Comunicacao,
            Visibilidade = AgendaVisibilidade.Pessoas, CriadoEm = _agenda.AgoraUtc
        };
        foreach (var p in profs.Skip(1)) c.Pessoas.Add(new AgendaCompromissoPessoa { UserId = p, Papel = AgendaPapel.Ver });
        _db.AgendaCompromissos.Add(c);
        await _db.SaveChangesAsync(ct);
        await _auditoria.RegistrarAsync("AGENDA_COMUNICACAO_FALTA", "AgendaCompromisso", c.Id.ToString(CultureInfo.InvariantCulture),
            $"{v.Identificador} registrou comunicação de {req.Comunicacao} de {AgendaService.CodigoAssistida(req.AssistidaId)} ({ctx.Cursos[aula.CursoId.Value].Nome} {AgendaService.Dm(aula.IniUtc)}).", v.Identificador);
        var snap = await _snapshot.PersistAsync("agenda_comunicacao", CancellationToken.None);
        return Ok(new { id = "cp:" + c.Id, aviso = snap.AvisoSnapshot });
    }

    // ------------------------------------------------------------------ apoio

    internal static Dictionary<string, string> TiposManuais(string perfil)
    {
        var t = new Dictionary<string, string>
        {
            [AgendaTipoManual.Lembrete] = "Lembrete",
            [AgendaTipoManual.Prazo] = "Prazo",
            [AgendaTipoManual.Reuniao] = "Reunião interna",
            [AgendaTipoManual.Ausencia] = "Ausência ou atraso (meu ou de colega)"
        };
        if (perfil == PerfisAcesso.Juridico) t[AgendaTipoManual.Audiencia] = "Audiência";
        if (perfil == PerfisAcesso.AssistenteSocial) t[AgendaTipoManual.Visita] = "Visita / saída externa";
        if (perfil is PerfisAcesso.Recepcao or PerfisAcesso.AssistenteSocial or PerfisAcesso.Professor) t[AgendaTipoManual.Comunicacao] = "Comunicação de falta/atraso de aluna";
        if (perfil == PerfisAcesso.Adm) t[AgendaTipoManual.Evento] = "Evento da Casa";
        return t;
    }

    internal static Dictionary<string, string> TiposAgendamento(string? area) => area switch
    {
        AgendaArea.Psicologia => new() { ["sessao"] = "Sessão", ["primeiro"] = "Primeiro atendimento" },
        AgendaArea.Juridico => new() { ["atendimento"] = "Atendimento", ["retorno"] = "Retorno", ["primeiro"] = "Primeiro atendimento" },
        AgendaArea.ServicoSocial => new() { ["atendimento_social"] = "Atendimento social (na unidade)", ["visita"] = "Visita domiciliar", ["visita_institucional"] = "Visita institucional" },
        _ => new()
    };

    private static List<AgendaPessoaResponse> Pessoas(AgendaService.Contexto ctx) => ctx.Usuarios.Values
        .Where(u => u.Ativo && !u.IdentificadorFuncionario.StartsWith("EQP-", StringComparison.OrdinalIgnoreCase) && u.Perfil != PerfisAcesso.Equipe)
        .OrderBy(u => u.NomeCompleto)
        .Select(u => new AgendaPessoaResponse { Id = u.Id, Nome = u.NomeCompleto, Curto = ctx.Curto(u.Id), Perfil = u.Perfil, Area = RotuloPerfil(u.Perfil) })
        .ToList();

    private static string RotuloPerfil(string perfil) => perfil switch
    {
        PerfisAcesso.Adm => "Coordenação",
        PerfisAcesso.Recepcao => "Recepção",
        PerfisAcesso.Professor => "Professora",
        PerfisAcesso.Juridico => "Jurídico",
        PerfisAcesso.Psicologia => "Psicologia",
        PerfisAcesso.AssistenteSocial => "Serviço Social",
        _ => "Equipe"
    };

    private static string? ValidarPessoas(AgendaService.Contexto ctx, IEnumerable<string> ids)
    {
        foreach (var id in ids.Distinct())
        {
            if (!ctx.Usuarios.TryGetValue(id, out var u) || !u.Ativo) return "Escolha pessoas ativas da equipe.";
        }
        return null;
    }

    /// <summary>Permissão para vincular/agendar: assistida ativa e permissão ATUAL no caso/prontuário da área da pessoa.</summary>
    internal static bool PodeVincular(AgendaService.Contexto ctx, UsuarioAtuante v, int assistidaId)
    {
        var area = AgendaService.AreaDoPerfil(v.Perfil);
        if (area is null || !ctx.Assistidas.TryGetValue(assistidaId, out var a) || !a.Ativa) return false;
        var temOrigem = area switch
        {
            AgendaArea.Juridico => ctx.Casos.ContainsKey(assistidaId) || a.EncaminhadaJuridicoEm is not null,
            AgendaArea.Psicologia => ctx.ProntuariosPsi.ContainsKey(assistidaId) || a.EncaminhadaPsicologiaEm is not null,
            _ => ctx.ProntuariosSoc.ContainsKey(assistidaId) || a.EncaminhadaAssistenciaSocialEm is not null
        };
        return temOrigem && AgendaService.AcessoOrigem(ctx, v, area, assistidaId);
    }

    private static bool PodeComunicar(AgendaService.Contexto ctx, UsuarioAtuante v, int assistidaId, int? cursoId)
    {
        if (v.Perfil == PerfisAcesso.Recepcao) return true;
        if (v.Perfil == PerfisAcesso.Professor)
        {
            var meus = ctx.ProfessoresCursos.Where(p => p.UserId == v.UserId).Select(p => p.CursoId).ToHashSet();
            return cursoId is int c ? meus.Contains(c) : ctx.Alunas.Any(a => a.AssistidaId == assistidaId && meus.Contains(a.CursoId));
        }
        if (v.Perfil == PerfisAcesso.AssistenteSocial) return AgendaService.VeCursoDaAssistida(ctx, v, assistidaId);
        return false;
    }

    private async Task<List<AgendaService.Item>> ItensDoPeriodoAsync(AgendaService.Contexto ctx, DateTime ini, DateTime fim, CancellationToken ct) =>
        await _agenda.ItensAsync(ctx, ini.AddDays(-1), fim.AddDays(1), ct);

    private List<AgendaConflito> Conferencia(AgendaService.Contexto ctx, List<AgendaService.Item> itens, UsuarioAtuante v, string dono, List<string> participantes,
        int? assistidaId, DateTime ini, DateTime fim, bool diaInteiro, int deslocamento, string? ignorar, bool comExcecao)
    {
        return _agenda.Conflitos(ctx, itens, new AgendaContextoConferencia
        {
            Viewer = v, DonoId = dono, Participantes = participantes, AssistidaId = assistidaId,
            IniUtc = ini.AddMinutes(-deslocamento), FimUtc = fim.AddMinutes(deslocamento),
            DiaInteiro = diaInteiro, IgnorarItemId = ignorar, ComExcecao = comExcecao
        });
    }

    /// <summary>
    /// Decide UMA ocorrência no servidor: bloqueio só passa com exceção da ADM ligada a este compromisso;
    /// "precisa de confirmação" só passa com justificativa. Devolve null quando pode salvar.
    /// </summary>
    private AgendaConferenciaOcorrenciaResponse? Decidir(AgendaService.Contexto ctx, List<AgendaService.Item> itens, UsuarioAtuante v, string dono, List<string> participantes,
        int? assistidaId, DateTime ini, DateTime fim, bool diaInteiro, int deslocamento, string? ignorar, AgendaOcorrenciaRequest o)
    {
        var excecao = v.Perfil == PerfisAcesso.Adm && Limpo(o.ExcecaoMotivo) is not null;
        var cf = Conferencia(ctx, itens, v, dono, participantes, assistidaId, ini, fim, diaInteiro, deslocamento, ignorar, excecao);
        var bloqueado = cf.Any(c => c.Nivel == AgendaService.Bloqueio);
        var semJustificativa = cf.Any(c => c.Nivel == AgendaService.Confirmar) && Limpo(o.Justificativa) is null;
        if (!bloqueado && !semJustificativa) return null;
        return new AgendaConferenciaOcorrenciaResponse { Inicio = AgendaService.Iso(ini), Rotulo = Rotulo(ini), Conflitos = cf.Select(Dto).ToList() };
    }

    private static AgendaConflitoResponse Dto(AgendaConflito c) => new() { Nivel = c.Nivel, Mensagem = c.Mensagem, UnidadeFechada = c.UnidadeFechada };

    private static string Rotulo(DateTime utc) => $"{AgendaService.DiaSemana(utc)} {AgendaService.Dm(utc)} {AgendaService.Hm(utc)}";

    internal static string? Limpo(string? s)
    {
        var t = s?.Trim();
        return string.IsNullOrEmpty(t) ? null : t.Length > 300 ? t[..300] : t;
    }

    private static readonly string[] FormatosHora = ["yyyy-MM-dd'T'HH:mm", "yyyy-MM-dd'T'HH:mm:ss", "yyyy-MM-dd HH:mm"];

    internal static bool LerLocal(string? s, out DateTime utc)
    {
        utc = default;
        if (string.IsNullOrWhiteSpace(s)) return false;
        if (!DateTime.TryParseExact(s.Trim(), FormatosHora, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local)) return false;
        utc = AgendaService.ParaUtc(local);
        return true;
    }

    internal static bool LerDia(string? s, out DateOnly dia) =>
        DateOnly.TryParseExact(s?.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out dia);

    private static bool LerOcorrencia(AgendaOcorrenciaRequest o, bool diaInteiro, out DateTime ini, out DateTime fim, out string erro)
    {
        erro = string.Empty;
        fim = default;
        if (diaInteiro)
        {
            var texto = o.Inicio.Length >= 10 ? o.Inicio[..10] : o.Inicio;
            if (!LerDia(texto, out var dia)) { ini = default; erro = "Data inválida."; return false; }
            ini = AgendaService.InicioDiaUtc(dia);
            fim = AgendaService.InicioDiaUtc(dia.AddDays(1));
            return true;
        }
        if (!LerLocal(o.Inicio, out ini)) { erro = "Data ou horário inválido."; return false; }
        if (!LerLocal(o.Fim, out fim) || fim <= ini) { erro = "O fim precisa ser depois do início."; return false; }
        if ((fim - ini).TotalHours > 12) { erro = "Um compromisso pode ter no máximo 12 horas. Use “dia inteiro”."; return false; }
        return true;
    }

    /// <summary>Acha um item pelo id da agenda (carrega só o período em que ele cai).</summary>
    private async Task<AgendaService.Item?> AcharItemAsync(AgendaService.Contexto ctx, string id, CancellationToken ct)
    {
        DateTime? quando = null;
        var partes = id.Split(':');
        if (partes.Length < 2) return null;
        int.TryParse(partes[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var n);
        switch (partes[0])
        {
            case "ag": quando = await _db.AgendaAgendamentos.Where(a => a.Id == n).Select(a => (DateTime?)a.InicioUtc).SingleOrDefaultAsync(ct); break;
            case "cp": quando = await _db.AgendaCompromissos.Where(a => a.Id == n).Select(a => (DateTime?)a.InicioUtc).SingleOrDefaultAsync(ct); break;
            case "dia": quando = ctx.Dias.Where(d => d.Id == n).Select(d => (DateTime?)AgendaService.InicioDiaUtc(d.Data)).SingleOrDefault(); break;
            case "aula": if (partes.Length >= 3 && LerDia(partes[2], out var d0)) quando = AgendaService.InicioDiaUtc(d0); break;
            case "retj":
                var r = await _db.AtendimentosJuridicos.Where(a => a.Id == n).Select(a => a.DataRetorno).SingleOrDefaultAsync(ct);
                if (r is not null) quando = AgendaService.InicioDiaUtc(r.Value);
                break;
            case "filaj": quando = ctx.Assistidas.TryGetValue(n, out var asf) ? asf.JuridicoAgendadoPara : null; break;
            case "proxp": quando = await _db.SessoesPsicologicas.Where(s => s.Id == n).Select(s => s.ProximaSessao).SingleOrDefaultAsync(ct); break;
            case "meta":
                var m = await _db.MetasAcompanhamentoSocial.Where(x => x.Id == n).Select(x => x.Prazo).SingleOrDefaultAsync(ct);
                if (m is not null) quando = AgendaService.InicioDiaUtc(m.Value);
                break;
        }
        if (quando is null) return null;
        var itens = await _agenda.ItensAsync(ctx, quando.Value.AddDays(-1), quando.Value.AddDays(2), ct);
        return itens.FirstOrDefault(i => i.Id == id);
    }

    private ObjectResult Proibido(string mensagem) => StatusCode(StatusCodes.Status403Forbidden, new { mensagem });

    private NotFoundObjectResult NaoEncontrado() => NotFound(new { mensagem = "Item não encontrado." });
}
