using System.Globalization;
using CasaMulher.Api.DTOs;
using CasaMulher.Api.Models;
using CasaMulher.Api.Security;
using CasaMulher.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CasaMulher.Api.Controllers;

/// <summary>Agendamentos (com histórico próprio), chegada na Recepção, revisão após "Passar responsabilidades",
/// aulas, configuração da ADM e reavaliação de risco.</summary>
public partial class AgendaController
{
    // ------------------------------------------------------------------ agendamentos

    [HttpPost("agendamentos")]
    public async Task<ActionResult> Agendar(AgendaAgendamentoRequest req, CancellationToken ct)
    {
        var v = await _assistidas.ObterUsuarioAsync(User, ct);
        if (v is null) return Unauthorized();
        var area = AgendaService.AreaDoPerfil(v.Perfil);
        if (area is null) return Proibido("Agendamentos são feitos pelo Jurídico, pela Psicologia ou pelo Serviço Social.");
        if (!TiposAgendamento(area).ContainsKey(req.Tipo)) return BadRequest(new { mensagem = "Tipo de agendamento inválido." });
        if (req.DuracaoMinutos is < 15 or > 480) return BadRequest(new { mensagem = "Duração entre 15 minutos e 8 horas." });
        var visita = req.Tipo is "visita" or "visita_institucional";
        var desl = visita ? Math.Clamp(req.DeslocamentoMinutos, 0, 240) : 0;
        var presencial = !visita && req.Presencial;
        var observacao = Limpo(req.Observacao);

        var ctx = await _agenda.CarregarContextoAsync(ct);
        if (!PodeVincular(ctx, v, req.AssistidaId)) return Proibido("Você não tem permissão no caso/prontuário dessa assistida.");
        if (!LerLocal(req.Inicio, out var ini)) return BadRequest(new { mensagem = "Data ou horário inválido." });
        var fim = ini.AddMinutes(req.DuracaoMinutos);

        AgendaAgendamento? anterior = null;
        AgendaRevisao? revisao = null;
        if (req.RemarcarDeId is int rid)
        {
            anterior = await _db.AgendaAgendamentos.Include(a => a.Historico).SingleOrDefaultAsync(a => a.Id == rid, ct);
            if (anterior is null || anterior.Situacao != AgendaSituacao.Agendado || anterior.AssistidaId != req.AssistidaId || anterior.Area != area)
                return BadRequest(new { mensagem = "Agendamento para remarcar não encontrado." });
            if (anterior.ResponsavelUserId != v.UserId)
            {
                revisao = await _db.AgendaRevisoes.SingleOrDefaultAsync(r => r.AgendaAgendamentoId == rid && r.ParaUserId == v.UserId && r.Situacao == "pendente", ct);
                if (revisao is null) return Proibido("Só a responsável pode remarcar este agendamento.");
            }
        }

        var itens = await ItensDoPeriodoAsync(ctx, ini, fim, ct);
        var ocorrencia = new AgendaOcorrenciaRequest { Inicio = req.Inicio, Justificativa = req.Justificativa, ExcecaoMotivo = req.ExcecaoMotivo };
        var falha = Decidir(ctx, itens, v, v.UserId, [], req.AssistidaId, ini, fim, false, desl, anterior is null ? null : "ag:" + anterior.Id, ocorrencia);
        if (falha is not null) return Conflict(new { mensagem = "Há conflitos sem confirmação. Nada foi salvo.", ocorrencias = new[] { falha } });

        var agora = _agenda.AgoraUtc;
        var novo = new AgendaAgendamento
        {
            Area = area, AssistidaId = req.AssistidaId, Tipo = req.Tipo, ResponsavelUserId = v.UserId, InicioUtc = ini, FimUtc = fim,
            DeslocamentoMinutos = desl, Presencial = presencial, AvisarRecepcao = presencial && req.AvisarRecepcao,
            ChegadaEstado = presencial ? "esperada" : null, Observacao = observacao, Justificativa = Limpo(req.Justificativa),
            RemarcadoDeId = anterior?.Id, CriadoPorUserId = v.UserId, CriadoPorIdentificador = v.Identificador, CriadoEm = agora, AtualizadoEm = agora
        };
        novo.Historico.Add(new AgendaAgendamentoHistorico
        {
            Texto = anterior is null ? $"Agendado por {ctx.Curto(v.UserId)}" : $"Remarcado por {ctx.Curto(v.UserId)} (antes: {AgendaService.Dm(anterior.InicioUtc)} {AgendaService.Hm(anterior.InicioUtc)})",
            PorIdentificador = v.Identificador, Em = agora
        });
        if (novo.Justificativa is not null) novo.Historico.Add(new AgendaAgendamentoHistorico { Texto = "Confirmado por cima de conflito: " + novo.Justificativa, PorIdentificador = v.Identificador, Em = agora });
        _db.AgendaAgendamentos.Add(novo);

        if (anterior is not null)
        {
            anterior.Situacao = AgendaSituacao.Remarcado;
            anterior.ChegadaEstado = null; // sai das esperadas da Recepção
            anterior.AtualizadoEm = agora;
            anterior.Historico.Add(new AgendaAgendamentoHistorico { Texto = $"Remarcado para {AgendaService.Dm(ini)} {AgendaService.Hm(ini)} por {ctx.Curto(v.UserId)}", PorIdentificador = v.Identificador, Em = agora });
            if (revisao is not null) { revisao.Situacao = "remarcado"; revisao.ResolvidoEm = agora; }
        }

        if (area == AgendaArea.Juridico && req.Tipo == "primeiro")
        {
            var assistida = await _db.Assistidas.SingleAsync(a => a.Id == req.AssistidaId, ct);
            assistida.JuridicoAgendadoPara = ini;
            assistida.JuridicoAgendadoEm = agora;
            assistida.JuridicoAgendadoPorIdentificador = v.Identificador;
        }
        await _db.SaveChangesAsync(ct);

        await _auditoria.RegistrarAsync(anterior is null ? "AGENDA_AGENDAMENTO_CRIADO" : "AGENDA_AGENDAMENTO_REMARCADO", "AgendaAgendamento", novo.Id.ToString(CultureInfo.InvariantCulture),
            $"{v.Identificador} agendou {AgendaService.TituloAgendamento(area, req.Tipo).ToLower(CultureInfo.GetCultureInfo("pt-BR"))} de {AgendaService.CodigoAssistida(req.AssistidaId)} em {AgendaService.Dm(ini)} {AgendaService.Hm(ini)}"
            + (anterior is null ? "" : $" (remarcado do #{anterior.Id})") + (novo.Justificativa is null ? "" : $" · confirmou por cima de conflito: “{novo.Justificativa}”") + ".", v.Identificador);
        var snap = await _snapshot.PersistAsync("agenda_agendamento", CancellationToken.None);
        return Ok(new { id = "ag:" + novo.Id, aviso = snap.AvisoSnapshot });
    }

    /// <summary>Recepção: "Chegou". Só para quem está como esperada hoje (Avisar Recepção ligado).</summary>
    [HttpPost("agendamentos/{id:int}/chegou")]
    public async Task<ActionResult> Chegou(int id, CancellationToken ct)
    {
        var v = await _assistidas.ObterUsuarioAsync(User, ct);
        if (v is null) return Unauthorized();
        if (v.Perfil != PerfisAcesso.Recepcao) return Proibido("Só a Recepção marca a chegada.");
        var a = await _db.AgendaAgendamentos.Include(x => x.Historico).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (a is null) return NaoEncontrado();
        var ctx = await _agenda.CarregarContextoAsync(ct);
        if (_agenda.Revelar(ctx, AgendaService.DeAgendamento(a), v) != AgendaService.NivelRecepcao) return NaoEncontrado();
        if (a.Situacao != AgendaSituacao.Agendado || (a.ChegadaEstado ?? "esperada") != "esperada") return BadRequest(new { mensagem = "A chegada já foi registrada." });
        var agora = _agenda.AgoraUtc;
        a.ChegadaEstado = "chegou";
        a.ChegouEm = agora;
        a.ChegadaMarcadaPorIdentificador = v.Identificador;
        a.AtualizadoEm = agora;
        a.Historico.Add(new AgendaAgendamentoHistorico { Texto = "Recepção registrou a chegada", PorIdentificador = v.Identificador, Em = agora });
        await _db.SaveChangesAsync(ct);
        await _auditoria.RegistrarAsync("AGENDA_CHEGADA", "AgendaAgendamento", id.ToString(CultureInfo.InvariantCulture), $"{v.Identificador} registrou a chegada de {AgendaService.CodigoAssistida(a.AssistidaId)}.", v.Identificador);
        var snap = await _snapshot.PersistAsync("agenda_chegada", CancellationToken.None);
        return Ok(new { aviso = snap.AvisoSnapshot });
    }

    [HttpPost("agendamentos/{id:int}/recebida")]
    public async Task<ActionResult> Recebida(int id, CancellationToken ct)
    {
        var (erro, a, v, ctx) = await CarregarMeuAgendamentoAsync(id, ct);
        if (erro is not null) return erro;
        if (a!.ChegadaEstado != "chegou") return BadRequest(new { mensagem = "A Recepção ainda não registrou a chegada." });
        var agora = _agenda.AgoraUtc;
        a.ChegadaEstado = "recebida";
        a.RecebidaEm = agora;
        a.AtualizadoEm = agora;
        a.Historico.Add(new AgendaAgendamentoHistorico { Texto = $"Recebida por {ctx!.Curto(v!.UserId)}", PorIdentificador = v.Identificador, Em = agora });
        await _db.SaveChangesAsync(ct);
        await _auditoria.RegistrarAsync("AGENDA_RECEBIDA", "AgendaAgendamento", id.ToString(CultureInfo.InvariantCulture), $"{v.Identificador} recebeu {AgendaService.CodigoAssistida(a.AssistidaId)}.", v.Identificador);
        var snap = await _snapshot.PersistAsync("agenda_chegada", CancellationToken.None);
        return Ok(new { aviso = snap.AvisoSnapshot });
    }

    [HttpPost("agendamentos/{id:int}/resultado")]
    public async Task<ActionResult> Resultado(int id, AgendaResultadoRequest req, CancellationToken ct)
    {
        var (erro, a, v, ctx) = await CarregarMeuAgendamentoAsync(id, ct);
        if (erro is not null) return erro;
        if (req.Situacao is not (AgendaSituacao.Realizado or AgendaSituacao.FaltaRegistrada or AgendaSituacao.Cancelado))
            return BadRequest(new { mensagem = "Escolha realizado, falta registrada ou cancelado. Para remarcar, use Remarcar." });
        if (a!.InicioUtc > _agenda.AgoraUtc) return BadRequest(new { mensagem = "O resultado é registrado depois do horário marcado." });
        var agora = _agenda.AgoraUtc;
        a.Situacao = req.Situacao;
        a.AtualizadoEm = agora;
        a.Historico.Add(new AgendaAgendamentoHistorico { Texto = $"Resultado: {req.Situacao.Replace('_', ' ')} ({ctx!.Curto(v!.UserId)})", PorIdentificador = v.Identificador, Em = agora });
        await _db.SaveChangesAsync(ct);
        await _auditoria.RegistrarAsync("AGENDA_RESULTADO", "AgendaAgendamento", id.ToString(CultureInfo.InvariantCulture), $"{v.Identificador} registrou {req.Situacao} para {AgendaService.CodigoAssistida(a.AssistidaId)}.", v.Identificador);
        var snap = await _snapshot.PersistAsync("agenda_resultado", CancellationToken.None);
        return Ok(new { aviso = snap.AvisoSnapshot });
    }

    [HttpPost("agendamentos/{id:int}/cancelar")]
    public async Task<ActionResult> CancelarAgendamento(int id, AgendaCancelarRequest req, CancellationToken ct)
    {
        var (erro, a, v, ctx) = await CarregarMeuAgendamentoAsync(id, ct);
        if (erro is not null) return erro;
        if (a!.InicioUtc <= _agenda.AgoraUtc) return BadRequest(new { mensagem = "Horário que já passou: registre o resultado." });
        var agora = _agenda.AgoraUtc;
        a.Situacao = AgendaSituacao.Cancelado;
        a.ChegadaEstado = null;
        a.AtualizadoEm = agora;
        var motivo = Limpo(req.Motivo);
        a.Historico.Add(new AgendaAgendamentoHistorico { Texto = $"Cancelado por {ctx!.Curto(v!.UserId)}{(motivo is null ? "" : ": " + motivo)}", PorIdentificador = v.Identificador, Em = agora });
        if (a.Area == AgendaArea.Juridico && a.Tipo == "primeiro")
        {
            var assistida = await _db.Assistidas.SingleAsync(x => x.Id == a.AssistidaId, ct);
            if (assistida.JuridicoAgendadoPara == a.InicioUtc) { assistida.JuridicoAgendadoPara = null; assistida.JuridicoAgendadoEm = null; }
        }
        await _db.SaveChangesAsync(ct);
        await _auditoria.RegistrarAsync("AGENDA_AGENDAMENTO_CANCELADO", "AgendaAgendamento", id.ToString(CultureInfo.InvariantCulture), $"{v.Identificador} cancelou o agendamento de {AgendaService.CodigoAssistida(a.AssistidaId)}.", v.Identificador);
        var snap = await _snapshot.PersistAsync("agenda_agendamento", CancellationToken.None);
        return Ok(new { aviso = snap.AvisoSnapshot });
    }

    /// <summary>Sucessora assume um compromisso recebido: o sistema confere a disponibilidade dela antes.</summary>
    [HttpPost("revisoes/{agendamentoId:int}/assumir")]
    public async Task<ActionResult> Assumir(int agendamentoId, AgendaJustificativaRequest req, CancellationToken ct)
    {
        var v = await _assistidas.ObterUsuarioAsync(User, ct);
        if (v is null) return Unauthorized();
        var rev = await _db.AgendaRevisoes.Include(r => r.Agendamento!).ThenInclude(a => a.Historico)
            .SingleOrDefaultAsync(r => r.AgendaAgendamentoId == agendamentoId && r.ParaUserId == v.UserId && r.Situacao == "pendente", ct);
        if (rev?.Agendamento is null) return NaoEncontrado();
        var a = rev.Agendamento;
        var ctx = await _agenda.CarregarContextoAsync(ct);
        if (!AgendaService.AcessoOrigem(ctx, v, a.Area, a.AssistidaId) || AgendaService.AreaDoPerfil(v.Perfil) != a.Area)
            return Proibido("Você não tem permissão no caso/prontuário de origem.");
        if (a.Situacao != AgendaSituacao.Agendado) return BadRequest(new { mensagem = "Este agendamento não está mais ativo." });

        var itens = await ItensDoPeriodoAsync(ctx, a.InicioUtc, a.FimUtc, ct);
        var falha = Decidir(ctx, itens, v, v.UserId, [], a.AssistidaId, a.InicioUtc, a.FimUtc, false, a.DeslocamentoMinutos, "ag:" + a.Id,
            new AgendaOcorrenciaRequest { Inicio = AgendaService.Iso(a.InicioUtc), Justificativa = req.Justificativa });
        if (falha is not null) return Conflict(new { mensagem = "Há conflitos na sua agenda. Confirme com justificativa ou remarque.", ocorrencias = new[] { falha } });

        var agora = _agenda.AgoraUtc;
        a.ResponsavelUserId = v.UserId;
        a.AtualizadoEm = agora;
        a.Historico.Add(new AgendaAgendamentoHistorico { Texto = $"Assumido por {ctx.Curto(v.UserId)} (recebido de {ctx.Curto(rev.DeUserId)}){(Limpo(req.Justificativa) is string j ? " · justificativa: " + j : "")}", PorIdentificador = v.Identificador, Em = agora });
        rev.Situacao = "assumido";
        rev.ResolvidoEm = agora;
        await _db.SaveChangesAsync(ct);
        await _auditoria.RegistrarAsync("AGENDA_REVISAO_ASSUMIDA", "AgendaAgendamento", a.Id.ToString(CultureInfo.InvariantCulture), $"{v.Identificador} assumiu o agendamento recebido de {ctx.Usuarios.GetValueOrDefault(rev.DeUserId)?.IdentificadorFuncionario}.", v.Identificador);
        var snap = await _snapshot.PersistAsync("agenda_revisao", CancellationToken.None);
        return Ok(new { aviso = snap.AvisoSnapshot });
    }

    private async Task<(ActionResult? Erro, AgendaAgendamento? A, UsuarioAtuante? V, AgendaService.Contexto? Ctx)> CarregarMeuAgendamentoAsync(int id, CancellationToken ct)
    {
        var v = await _assistidas.ObterUsuarioAsync(User, ct);
        if (v is null) return (Unauthorized(), null, null, null);
        var a = await _db.AgendaAgendamentos.Include(x => x.Historico).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (a is null) return (NaoEncontrado(), null, null, null);
        if (a.ResponsavelUserId != v.UserId) return (Proibido("Só a responsável pelo agendamento faz essa ação."), null, null, null);
        var ctx = await _agenda.CarregarContextoAsync(ct);
        if (!AgendaService.AcessoOrigem(ctx, v, a.Area, a.AssistidaId)) return (Proibido("Você não tem mais permissão no caso/prontuário de origem."), null, null, null);
        if (a.Situacao != AgendaSituacao.Agendado) return (BadRequest(new { mensagem = "Este agendamento não está mais ativo." }), null, null, null);
        return (null, a, v, ctx);
    }

    // ------------------------------------------------------------------ aulas

    /// <summary>Cancelar aula ("esta" ou "esta e as próximas"). Não encerra a turma; aula com chamada não é cancelada.</summary>
    [HttpPost("aulas/cancelar")]
    public async Task<ActionResult> CancelarAula(AgendaAulaCancelarRequest req, CancellationToken ct)
    {
        var v = await _assistidas.ObterUsuarioAsync(User, ct);
        if (v is null) return Unauthorized();
        var ctx = await _agenda.CarregarContextoAsync(ct);
        if (!PodeMexerNaTurma(ctx, v, req.CursoId)) return Proibido("Só a professora da turma ou a ADM cancela aulas.");
        if (!LerDia(req.Data, out var dia)) return BadRequest(new { mensagem = "Data inválida." });
        if (req.Escopo is not ("esta" or "seguintes")) return BadRequest(new { mensagem = "Escolha “só esta aula” ou “esta e as próximas”." });
        var motivo = Limpo(req.Motivo);
        if (motivo is null) return BadRequest(new { mensagem = "Informe o motivo." });
        var aula = (await _agenda.ItensAsync(ctx, AgendaService.InicioDiaUtc(dia), AgendaService.InicioDiaUtc(dia.AddDays(1)), ct))
            .FirstOrDefault(i => i.Fonte == "aula" && i.CursoId == req.CursoId && i.AulaData == dia);
        if (aula is null) return BadRequest(new { mensagem = "Não há aula dessa turma nesse dia." });
        if (aula.AulaComChamada) return BadRequest(new { mensagem = "Aula com chamada feita não é cancelada." });
        if (aula.IniUtc <= _agenda.AgoraUtc) return BadRequest(new { mensagem = "Só aulas futuras podem ser canceladas." });
        if (aula.AulaCancelada is not null) return BadRequest(new { mensagem = "Esta aula já está cancelada." });

        _db.AulasCancelamentos.Add(new AulaCancelamento { CursoId = req.CursoId, Data = dia, Escopo = req.Escopo, Motivo = motivo, PorIdentificador = v.Identificador, CriadoEm = _agenda.AgoraUtc });
        await _db.SaveChangesAsync(ct);
        await _auditoria.RegistrarAsync("AGENDA_AULA_CANCELADA", "Curso", req.CursoId.ToString(CultureInfo.InvariantCulture),
            $"{v.Identificador} cancelou {(req.Escopo == "esta" ? "a aula" : "as aulas a partir")} de {AgendaService.Dm(dia)} ({ctx.Cursos[req.CursoId].Nome}): {motivo}. A turma continua ativa.", v.Identificador);
        var snap = await _snapshot.PersistAsync("agenda_aula", CancellationToken.None);
        return Ok(new { aviso = snap.AvisoSnapshot });
    }

    [HttpPost("aulas/desfazer")]
    public async Task<ActionResult> DesfazerCancelamentoAula(AgendaAulaCancelarRequest req, CancellationToken ct)
    {
        var v = await _assistidas.ObterUsuarioAsync(User, ct);
        if (v is null) return Unauthorized();
        var ctx = await _agenda.CarregarContextoAsync(ct);
        if (!PodeMexerNaTurma(ctx, v, req.CursoId)) return Proibido("Só a professora da turma ou a ADM desfaz o cancelamento.");
        if (!LerDia(req.Data, out var dia)) return BadRequest(new { mensagem = "Data inválida." });
        var cancelamentos = await _db.AulasCancelamentos.Where(c => c.CursoId == req.CursoId && c.DesfeitoEm == null && c.Data <= dia).ToListAsync(ct);
        var alvo = cancelamentos.FirstOrDefault(c => c.Escopo == "esta" && c.Data == dia)
            ?? cancelamentos.Where(c => c.Escopo == "seguintes").OrderByDescending(c => c.Data).FirstOrDefault();
        if (alvo is null) return BadRequest(new { mensagem = "Não há cancelamento para desfazer nessa data." });
        alvo.DesfeitoEm = _agenda.AgoraUtc;
        await _db.SaveChangesAsync(ct);
        await _auditoria.RegistrarAsync("AGENDA_AULA_CANCELAMENTO_DESFEITO", "Curso", req.CursoId.ToString(CultureInfo.InvariantCulture),
            $"{v.Identificador} desfez o cancelamento de {AgendaService.Dm(alvo.Data)} ({alvo.Escopo}). O cancelamento continua no histórico.", v.Identificador);
        var snap = await _snapshot.PersistAsync("agenda_aula", CancellationToken.None);
        return Ok(new { aviso = snap.AvisoSnapshot });
    }

    private static bool PodeMexerNaTurma(AgendaService.Contexto ctx, UsuarioAtuante v, int cursoId) =>
        ctx.Cursos.ContainsKey(cursoId) && (v.Perfil == PerfisAcesso.Adm || ctx.ProfessorasDoCurso(cursoId).Contains(v.UserId));

    [HttpGet("cursos/{cursoId:int}/horarios")]
    public async Task<ActionResult<List<CursoHorarioDto>>> Horarios(int cursoId, CancellationToken ct)
    {
        var v = await _assistidas.ObterUsuarioAsync(User, ct);
        if (v is null) return Unauthorized();
        return Ok(await _db.CursosHorarios.AsNoTracking().Where(h => h.CursoId == cursoId).OrderBy(h => h.DiaSemana).ThenBy(h => h.Inicio)
            .Select(h => new CursoHorarioDto { Id = h.Id, DiaSemana = h.DiaSemana, Inicio = h.Inicio.ToString("HH:mm"), Fim = h.Fim.ToString("HH:mm"), Sala = h.Sala })
            .ToListAsync(ct));
    }

    /// <summary>ADM: horário semanal da turma. Horário novo só vale a partir de hoje (aulas antigas não viram pendência).</summary>
    [HttpPut("cursos/{cursoId:int}/horarios")]
    public async Task<ActionResult> SalvarHorarios(int cursoId, List<CursoHorarioDto> req, CancellationToken ct)
    {
        var v = await _assistidas.ObterUsuarioAsync(User, ct);
        if (v is null) return Unauthorized();
        if (v.Perfil != PerfisAcesso.Adm) return Proibido("Só a ADM define o horário das turmas.");
        if (!await _db.Cursos.AnyAsync(c => c.Id == cursoId, ct)) return NaoEncontrado();
        if (req.Count > 14) return BadRequest(new { mensagem = "Muitos horários." });
        var novos = new List<(CursoHorarioDto D, TimeOnly I, TimeOnly F)>();
        foreach (var h in req)
        {
            if (h.DiaSemana is < 0 or > 6 || !TimeOnly.TryParseExact(h.Inicio, "HH:mm", out var i) || !TimeOnly.TryParseExact(h.Fim, "HH:mm", out var f) || f <= i)
                return BadRequest(new { mensagem = "Horário inválido." });
            novos.Add((h, i, f));
        }
        var atuais = await _db.CursosHorarios.Where(h => h.CursoId == cursoId).ToListAsync(ct);
        var hoje = _agenda.Hoje;
        foreach (var velho in atuais.Where(a => !novos.Any(n => n.D.Id == a.Id))) _db.CursosHorarios.Remove(velho);
        foreach (var n in novos)
        {
            var existente = atuais.FirstOrDefault(a => a.Id == n.D.Id);
            if (existente is null)
            {
                _db.CursosHorarios.Add(new CursoHorario { CursoId = cursoId, DiaSemana = n.D.DiaSemana, Inicio = n.I, Fim = n.F, Sala = Limpo(n.D.Sala), ValidoDesde = hoje });
                continue;
            }
            if (existente.DiaSemana != n.D.DiaSemana || existente.Inicio != n.I || existente.Fim != n.F) existente.ValidoDesde = hoje;
            existente.DiaSemana = n.D.DiaSemana;
            existente.Inicio = n.I;
            existente.Fim = n.F;
            existente.Sala = Limpo(n.D.Sala);
        }
        await _db.SaveChangesAsync(ct);
        await _auditoria.RegistrarAsync("AGENDA_HORARIO_TURMA", "Curso", cursoId.ToString(CultureInfo.InvariantCulture), $"{v.Identificador} atualizou o horário semanal ({novos.Count} horário(s)).", v.Identificador);
        var snap = await _snapshot.PersistAsync("agenda_horario", CancellationToken.None);
        return Ok(new { aviso = snap.AvisoSnapshot });
    }

    // ------------------------------------------------------------------ configuração (ADM)

    [HttpGet("configuracao")]
    public async Task<ActionResult<AgendaConfiguracaoResponse>> Configuracao(CancellationToken ct)
    {
        var v = await _assistidas.ObterUsuarioAsync(User, ct);
        if (v is null) return Unauthorized();
        if (v.Perfil != PerfisAcesso.Adm) return Proibido("Só a ADM configura a agenda.");
        var ctx = await _agenda.CarregarContextoAsync(ct);
        var desde = _agenda.AgoraUtc.AddDays(-1);
        var excecoes = new List<string>();
        foreach (var c in await _db.AgendaCompromissos.AsNoTracking().Include(c => c.Pessoas).Where(c => c.ExcecaoMotivo != null && c.InicioUtc > desde).OrderBy(c => c.InicioUtc).ToListAsync(ct))
        {
            var pessoas = new[] { c.DonoUserId }.Concat(c.Pessoas.Where(p => p.Papel == AgendaPapel.Participante).Select(p => p.UserId)).Select(ctx.Curto);
            excecoes.Add($"{AgendaService.Dm(c.InicioUtc)} {AgendaService.Hm(c.InicioUtc)}{(c.FimUtc is null ? "" : "–" + AgendaService.Hm(c.FimUtc.Value))} · {c.Titulo} · {string.Join(", ", pessoas)} ({c.ExcecaoMotivo}; por {c.ExcecaoPorIdentificador})");
        }
        var ausencias = (await _db.AgendaCompromissos.AsNoTracking()
                .Where(c => c.Tipo == AgendaTipoManual.Ausencia && c.SituacaoAusencia == "informada" && c.Situacao == "ativo" && c.InicioUtc > desde).OrderBy(c => c.InicioUtc).ToListAsync(ct))
            .Select(c => $"{ctx.Curto(c.DonoUserId)}: {AgendaService.DiaSemana(c.InicioUtc)} {AgendaService.Dm(c.InicioUtc)} {(c.DiaInteiro ? "dia inteiro" : AgendaService.Hm(c.InicioUtc) + "–" + (c.FimUtc is null ? "" : AgendaService.Hm(c.FimUtc.Value)))} (por {ctx.Curto(c.RegistradoPorUserId)})")
            .ToList();
        var dias = new List<AgendaDiaEspecialResponse>();
        foreach (var d in ctx.Dias.Where(d => d.Data >= _agenda.Hoje.AddDays(-30)).OrderBy(d => d.Data))
        {
            var af = await _agenda.AfetadosAsync(ctx, d, ct);
            dias.Add(new AgendaDiaEspecialResponse
            {
                Id = d.Id, Data = d.Data.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), Tipo = d.Tipo, Funcionamento = d.Funcionamento,
                HorarioInicio = d.HorarioInicio?.ToString("HH:mm"), HorarioFim = d.HorarioFim?.ToString("HH:mm"),
                TextoFuncionamento = AgendaService.TextoFuncionamento(d), Titulo = d.Titulo, Nota = d.Nota,
                Reaberta = d.ReabertaEm is null ? null : $"{d.ReabertaPorIdentificador}: {d.ReabertaMotivo}",
                Afetados = af.Total, AfetadosTexto = af.Total == 0 ? null : af.Texto
            });
        }
        return Ok(new AgendaConfiguracaoResponse
        {
            LimiteCompromissosDia = ctx.Config.LimiteCompromissosDia, LimiteHorasDia = ctx.Config.LimiteHorasDia,
            ExpedienteInicio = ctx.Config.ExpedienteInicio.ToString("HH:mm"), ExpedienteFim = ctx.Config.ExpedienteFim.ToString("HH:mm"),
            Dias = dias,
            Excecoes = excecoes, AusenciasInformadas = ausencias
        });
    }

    [HttpPut("configuracao")]
    public async Task<ActionResult> SalvarConfiguracao(AgendaConfiguracaoRequest req, CancellationToken ct)
    {
        var v = await _assistidas.ObterUsuarioAsync(User, ct);
        if (v is null) return Unauthorized();
        if (v.Perfil != PerfisAcesso.Adm) return Proibido("Só a ADM configura a agenda.");
        if (req.LimiteCompromissosDia is < 1 or > 30 || req.LimiteHorasDia is < 1 or > 16) return BadRequest(new { mensagem = "Limites inválidos." });
        if (!TimeOnly.TryParseExact(req.ExpedienteInicio, "HH:mm", out var i) || !TimeOnly.TryParseExact(req.ExpedienteFim, "HH:mm", out var f) || f <= i)
            return BadRequest(new { mensagem = "Expediente inválido." });
        var cfg = await _db.AgendaConfiguracoes.OrderBy(c => c.Id).FirstOrDefaultAsync(ct);
        if (cfg is null) { cfg = new AgendaConfiguracao(); _db.AgendaConfiguracoes.Add(cfg); }
        cfg.LimiteCompromissosDia = req.LimiteCompromissosDia;
        cfg.LimiteHorasDia = req.LimiteHorasDia;
        cfg.ExpedienteInicio = i;
        cfg.ExpedienteFim = f;
        cfg.AtualizadoEm = _agenda.AgoraUtc;
        await _db.SaveChangesAsync(ct);
        await _auditoria.RegistrarAsync("AGENDA_CONFIGURACAO", "AgendaConfiguracao", cfg.Id.ToString(CultureInfo.InvariantCulture),
            $"{v.Identificador} ajustou a agenda: aviso a partir de {req.LimiteCompromissosDia} compromissos ou {req.LimiteHorasDia} h; expediente {req.ExpedienteInicio}–{req.ExpedienteFim}.", v.Identificador);
        var snap = await _snapshot.PersistAsync("agenda_configuracao", CancellationToken.None);
        return Ok(new { aviso = snap.AvisoSnapshot });
    }

    /// <summary>Lê e valida o pedido: "que data é esta?" (tipo) + "como a Casa funciona neste dia?" (funcionamento).</summary>
    private string? LerDiaEspecial(AgendaDiaEspecialRequest req, AgendaDiaEspecial d)
    {
        if (!LerDia(req.Data, out var dia)) return "Data inválida.";
        if (!AgendaTipoDia.Todos.Contains(req.Tipo)) return "Escolha que data é esta: feriado, evento ou data comum.";
        var func = string.IsNullOrWhiteSpace(req.Funcionamento) ? AgendaFuncionamento.Normal : req.Funcionamento;
        if (!AgendaFuncionamento.Todos.Contains(func)) return "Escolha como a Casa funciona neste dia.";
        TimeOnly? ini = null, fim = null;
        if (func == AgendaFuncionamento.Especial)
        {
            if (!TimeOnly.TryParseExact(req.HorarioInicio ?? "", "HH:mm", out var hi) || !TimeOnly.TryParseExact(req.HorarioFim ?? "", "HH:mm", out var hf))
                return "Informe o horário especial (início e fim).";
            if (hf <= hi) return "No horário especial, o fim precisa ser depois do início.";
            ini = hi; fim = hf;
        }
        var titulo = Limpo(req.Titulo);
        if (titulo is null) return "Informe a identificação da data (ex.: Proclamação da República, Manutenção elétrica).";
        if (req.Tipo == AgendaTipoDia.Outra && func == AgendaFuncionamento.Normal) return "Uma data comum com funcionamento normal não precisa ser cadastrada.";
        d.Data = dia; d.Tipo = req.Tipo; d.Funcionamento = func; d.HorarioInicio = ini; d.HorarioFim = fim; d.Titulo = titulo; d.Nota = Limpo(req.Nota);
        return null;
    }

    private static string Descrever(AgendaDiaEspecial d) =>
        $"{AgendaService.Dm(d.Data)} {d.Titulo} ({(d.Tipo switch { AgendaTipoDia.Feriado => "feriado", AgendaTipoDia.Evento => "evento", _ => "data comum" })}; funcionamento: {AgendaService.TextoFuncionamento(d).ToLowerInvariant()})";

    /// <summary>Prévia antes de salvar: quantos compromissos já marcados ficariam fora do funcionamento (só números).</summary>
    [HttpPost("dias/previa")]
    public async Task<ActionResult<AgendaAfetadosResponse>> PreviaDia(AgendaDiaEspecialRequest req, CancellationToken ct)
    {
        var v = await _assistidas.ObterUsuarioAsync(User, ct);
        if (v is null) return Unauthorized();
        if (v.Perfil != PerfisAcesso.Adm) return Proibido("Só a ADM configura os dias da Casa.");
        var d = new AgendaDiaEspecial();
        var erro = LerDiaEspecial(req, d);
        if (erro is not null) return BadRequest(new { mensagem = erro });
        var ctx = await _agenda.CarregarContextoAsync(ct);
        return Ok(await _agenda.AfetadosAsync(ctx, d, ct));
    }

    /// <summary>Cadastra o dia. Se já houver algo marcado fora do funcionamento, nada é cancelado:
    /// cada responsável recebe a pendência "fora do funcionamento" e a ADM vê quantos faltam tratar.</summary>
    [HttpPost("dias")]
    public async Task<ActionResult> CriarDia(AgendaDiaEspecialRequest req, CancellationToken ct)
    {
        var v = await _assistidas.ObterUsuarioAsync(User, ct);
        if (v is null) return Unauthorized();
        if (v.Perfil != PerfisAcesso.Adm) return Proibido("Só a ADM configura os dias da Casa.");
        var d = new AgendaDiaEspecial { CriadoPorIdentificador = v.Identificador, CriadoEm = _agenda.AgoraUtc };
        var erro = LerDiaEspecial(req, d);
        if (erro is not null) return BadRequest(new { mensagem = erro });
        _db.AgendaDiasEspeciais.Add(d);
        await _db.SaveChangesAsync(ct);
        var ctx = await _agenda.CarregarContextoAsync(ct);
        var afetados = await _agenda.AfetadosAsync(ctx, d, ct);
        await _auditoria.RegistrarAsync("AGENDA_DIA_ESPECIAL", "AgendaDiaEspecial", d.Id.ToString(CultureInfo.InvariantCulture),
            $"{v.Identificador} cadastrou {Descrever(d)}. Compromissos fora do funcionamento: {afetados.Total}.", v.Identificador);
        var snap = await _snapshot.PersistAsync("agenda_dia", CancellationToken.None);
        return Ok(new { id = d.Id, afetados, aviso = snap.AvisoSnapshot });
    }

    [HttpPut("dias/{id:int}")]
    public async Task<ActionResult> EditarDia(int id, AgendaDiaEspecialRequest req, CancellationToken ct)
    {
        var v = await _assistidas.ObterUsuarioAsync(User, ct);
        if (v is null) return Unauthorized();
        if (v.Perfil != PerfisAcesso.Adm) return Proibido("Só a ADM configura os dias da Casa.");
        var d = await _db.AgendaDiasEspeciais.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (d is null) return NaoEncontrado();
        var antes = Descrever(d);
        var funcAntes = (d.Funcionamento, d.HorarioInicio, d.HorarioFim);
        var erro = LerDiaEspecial(req, d);
        if (erro is not null) return BadRequest(new { mensagem = erro });
        // mudou o funcionamento: a reabertura antiga não vale mais para a nova regra
        if (funcAntes != (d.Funcionamento, d.HorarioInicio, d.HorarioFim)) { d.ReabertaEm = null; d.ReabertaMotivo = null; d.ReabertaPorIdentificador = null; }
        await _db.SaveChangesAsync(ct);
        var ctx = await _agenda.CarregarContextoAsync(ct);
        var afetados = await _agenda.AfetadosAsync(ctx, d, ct);
        await _auditoria.RegistrarAsync("AGENDA_DIA_EDITADO", "AgendaDiaEspecial", id.ToString(CultureInfo.InvariantCulture),
            $"{v.Identificador} alterou {antes} para {Descrever(d)}. Compromissos fora do funcionamento: {afetados.Total}.", v.Identificador);
        var snap = await _snapshot.PersistAsync("agenda_dia", CancellationToken.None);
        return Ok(new { id = d.Id, afetados, aviso = snap.AvisoSnapshot });
    }

    [HttpDelete("dias/{id:int}")]
    public async Task<ActionResult> RemoverDia(int id, CancellationToken ct)
    {
        var v = await _assistidas.ObterUsuarioAsync(User, ct);
        if (v is null) return Unauthorized();
        if (v.Perfil != PerfisAcesso.Adm) return Proibido("Só a ADM configura os dias da Casa.");
        var d = await _db.AgendaDiasEspeciais.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (d is null) return NaoEncontrado();
        _db.AgendaDiasEspeciais.Remove(d);
        await _db.SaveChangesAsync(ct);
        await _auditoria.RegistrarAsync("AGENDA_DIA_REMOVIDO", "AgendaDiaEspecial", id.ToString(CultureInfo.InvariantCulture), $"{v.Identificador} removeu {Descrever(d)}.", v.Identificador);
        var snap = await _snapshot.PersistAsync("agenda_dia", CancellationToken.None);
        return Ok(new { aviso = snap.AvisoSnapshot });
    }

    /// <summary>Reabrir a unidade para TODOS: ação separada e explícita (diferente da exceção de um compromisso).</summary>
    [HttpPost("dias/{id:int}/reabrir")]
    public async Task<ActionResult> Reabrir(int id, AgendaMotivoRequest req, CancellationToken ct)
    {
        var v = await _assistidas.ObterUsuarioAsync(User, ct);
        if (v is null) return Unauthorized();
        if (v.Perfil != PerfisAcesso.Adm) return Proibido("Só a ADM reabre a unidade.");
        var d = await _db.AgendaDiasEspeciais.SingleOrDefaultAsync(x => x.Id == id && x.Funcionamento != AgendaFuncionamento.Normal, ct);
        if (d is null) return NaoEncontrado();
        if (d.ReabertaEm is not null) return BadRequest(new { mensagem = "Já está reaberta." });
        var motivo = Limpo(req.Motivo);
        if (motivo is null) return BadRequest(new { mensagem = "Informe o motivo." });
        d.ReabertaEm = _agenda.AgoraUtc;
        d.ReabertaMotivo = motivo;
        d.ReabertaPorIdentificador = v.Identificador;
        await _db.SaveChangesAsync(ct);
        await _auditoria.RegistrarAsync("AGENDA_UNIDADE_REABERTA", "AgendaDiaEspecial", id.ToString(CultureInfo.InvariantCulture), $"{v.Identificador} voltou ao funcionamento normal para todos em {Descrever(d)}: {motivo}.", v.Identificador);
        var snap = await _snapshot.PersistAsync("agenda_dia", CancellationToken.None);
        return Ok(new { aviso = snap.AvisoSnapshot });
    }

    // ------------------------------------------------------------------ reavaliação de risco (Psicologia)

    /// <summary>Registra a reavaliação (resolve a pendência mesmo com o risco igual). Não muda o alerta de risco do prontuário,
    /// que continua sendo retirado só por uma sessão posterior com risco menor.</summary>
    [HttpPost("reavaliacoes")]
    public async Task<ActionResult<ReavaliacaoRiscoResponse>> Reavaliar(ReavaliacaoRiscoRequest req, CancellationToken ct)
    {
        var v = await _assistidas.ObterUsuarioAsync(User, ct);
        if (v is null) return Unauthorized();
        if (!AssistidasService.AtuaComoPsicologia(v.Perfil)) return Proibido("Só a Psicologia registra reavaliação de risco.");
        var ctx = await _agenda.CarregarContextoAsync(ct);
        if (!ctx.ProntuariosPsi.TryGetValue(req.AssistidaId, out var p) || !AgendaService.AcessoOrigem(ctx, v, AgendaArea.Psicologia, req.AssistidaId)
            || !p.Responsaveis.Any(r => r.UserId == v.UserId))
            return Proibido("Você não tem permissão neste prontuário.");
        if (!PsicologiaCatalogo.Riscos.Contains(req.Classificacao)) return BadRequest(new { mensagem = "Escolha a classificação do risco." });
        var proxima = Limpo(req.ProximaAcao);
        if (proxima is null) return BadRequest(new { mensagem = "Informe a próxima ação." });
        var r = new ReavaliacaoRiscoPsicologica
        {
            ProntuarioPsicologicoId = p.Id, Classificacao = req.Classificacao, ProximaAcao = proxima,
            ProfissionalUserId = v.UserId, ProfissionalIdentificador = v.Identificador, ProfissionalNome = v.Nome, CriadoEm = _agenda.AgoraUtc
        };
        _db.ReavaliacoesRiscoPsicologicas.Add(r);
        await _db.SaveChangesAsync(ct);
        await _auditoria.RegistrarAsync("PSICOLOGIA_REAVALIACAO_RISCO", "ProntuarioPsicologico", p.Id.ToString(CultureInfo.InvariantCulture), $"{v.Identificador} registrou reavaliação de risco em {p.Numero}.", v.Identificador);
        await _snapshot.PersistAsync("psicologia_reavaliacao", CancellationToken.None);
        return Ok(ParaDto(r));
    }

    /// <summary>Ver reavaliações: confere a permissão ATUAL no prontuário a cada chamada.</summary>
    [HttpGet("reavaliacoes/{assistidaId:int}")]
    public async Task<ActionResult<List<ReavaliacaoRiscoResponse>>> Reavaliacoes(int assistidaId, CancellationToken ct)
    {
        var v = await _assistidas.ObterUsuarioAsync(User, ct);
        if (v is null) return Unauthorized();
        var ctx = await _agenda.CarregarContextoAsync(ct);
        if (!ctx.ProntuariosPsi.TryGetValue(assistidaId, out var p) || !AgendaService.AcessoOrigem(ctx, v, AgendaArea.Psicologia, assistidaId))
            return Proibido("Você não tem permissão neste prontuário.");
        var lista = await _db.ReavaliacoesRiscoPsicologicas.AsNoTracking().Where(x => x.ProntuarioPsicologicoId == p.Id).OrderByDescending(x => x.CriadoEm).ToListAsync(ct);
        await _auditoria.RegistrarAsync("PSICOLOGIA_REAVALIACAO_VISTA", "ProntuarioPsicologico", p.Id.ToString(CultureInfo.InvariantCulture), $"{v.Identificador} abriu as reavaliações de risco de {p.Numero}.", v.Identificador);
        return Ok(lista.Select(ParaDto).ToList());
    }

    private static ReavaliacaoRiscoResponse ParaDto(ReavaliacaoRiscoPsicologica r) => new()
    {
        Id = r.Id, Em = AgendaService.Iso(r.CriadoEm), Profissional = r.ProfissionalNome, Classificacao = r.Classificacao, ProximaAcao = r.ProximaAcao
    };
}
