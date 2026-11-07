using System.Globalization;
using CasaMulher.Api.DTOs;
using CasaMulher.Api.Models;
using CasaMulher.Api.Security;
using Microsoft.EntityFrameworkCore;

namespace CasaMulher.Api.Services;

public sealed partial class AgendaService
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public static string Iso(DateTime utc) => ParaLocal(utc).ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);

    public static string Cor(Item it, string nivel)
    {
        if (nivel is NivelOcupado or NivelContagem or NivelSemAcesso) return "k-neu";
        if (nivel == NivelRecepcao) return "k-equipe";
        if (it.Fonte == "casa") return it.Tipo == "fechada" ? "k-fec" : it.Tipo == "feriado" ? "k-fer" : "k-man";
        if (it.Fonte == "aula") return "k-aula";
        if (it.Tipo == AgendaTipoManual.Ausencia) return "k-aus";
        if (it.Tipo == AgendaTipoManual.Comunicacao) return "k-aula";
        if (it.Fonte == "manual" && it.Area is null) return "k-man";
        return it.Area switch
        {
            AgendaArea.Psicologia => "k-psi",
            AgendaArea.Juridico => "k-jur",
            AgendaArea.ServicoSocial => "k-soc",
            _ => "k-man"
        };
    }

    private static string TextoChegada(AgendaAgendamento a)
    {
        return a.ChegadaEstado switch
        {
            "chegou" => a.ChegouEm is null ? "chegou" : "chegou às " + Hm(a.ChegouEm.Value),
            "recebida" => a.RecebidaEm is null ? "recebida pela equipe" : "recebida pela equipe às " + Hm(a.RecebidaEm.Value),
            _ => "esperada"
        };
    }

    /// <summary>Monta a resposta do item SÓ com o que o nível permite.</summary>
    public AgendaItemResponse Montar(Contexto ctx, Item it, UsuarioAtuante v, string nivel)
    {
        var r = new AgendaItemResponse
        {
            Id = it.Id, Fonte = it.Fonte, Nivel = nivel, Grupo = Grupo(it, v, nivel),
            Inicio = Iso(it.IniUtc), Fim = it.FimUtc is null ? null : Iso(it.FimUtc.Value),
            DiaInteiro = it.DiaInteiro || (it.FimUtc is null && it.Fonte is "casa" or "marco" && it.Tipo != "proxima_sessao"),
            Ocupa = it.Ocupa, Cor = Cor(it, nivel), Meu = it.Envolve(v.UserId)
        };
        if (it.Deslocamento > 0 && nivel == NivelDetalhe)
        {
            r.JanelaInicio = Iso(it.Janela.Ini);
            r.JanelaFim = Iso(it.Janela.Fim);
        }

        switch (nivel)
        {
            case NivelRecepcao:
                r.Tipo = "atendimento";
                r.Titulo = ctx.Identificacao(it.AssistidaId!.Value);
                r.Sub = "Atendimento com a equipe técnica";
                r.Chegada = it.Agendamento!.ChegadaEstado ?? "esperada";
                r.ChegouEm = it.Agendamento.ChegouEm is null ? null : Iso(it.Agendamento.ChegouEm.Value);
                r.RecebidaEm = it.Agendamento.RecebidaEm is null ? null : Iso(it.Agendamento.RecebidaEm.Value);
                return r;
            case NivelContagem:
                r.Tipo = "contagem";
                r.Titulo = "Atendimento técnico";
                return r;
            case NivelSemAcesso:
                r.Tipo = "semacesso";
                r.Titulo = "Compromisso de um caso que você não acessa mais";
                r.Sub = "sem acesso ao caso";
                r.Tags.Add("sem acesso");
                return r;
            case NivelCurso:
                r.Tipo = "aula";
                r.Titulo = it.Titulo;
                r.Sub = (it.Sala ?? "") + (it.DonoId.Length > 0 ? " · Prof.ª " + ctx.Curto(it.DonoId) : "");
                r.Cancelado = it.AulaCancelada is not null;
                if (r.Cancelado) r.Tags.Add("cancelada");
                return r;
        }

        // detalhe
        r.Tipo = it.Tipo;
        r.AssistidaId = it.Fonte == "aula" ? null : it.AssistidaId;
        r.Area = it.Area;
        r.CursoId = it.CursoId;
        r.Titulo = it.Titulo;
        r.DonoId = it.DonoId.Length > 0 ? it.DonoId : null;
        r.DonoNome = it.DonoId.Length > 0 ? ctx.Nome(it.DonoId) : null;
        r.Pessoas = new[] { it.DonoId }.Concat(it.Participantes).Where(x => x.Length > 0).Distinct().ToList();
        var partes = new List<string>();
        if (it.AssistidaId is int aid && it.Fonte != "aula") partes.Add(ctx.Identificacao(aid));
        if (!it.Envolve(v.UserId) && it.DonoId.Length > 0 && it.Fonte is not ("casa" or "aula")) partes.Insert(0, ctx.Curto(it.DonoId));

        if (it.Agendamento is { } a)
        {
            r.Situacao = a.Situacao;
            r.Cancelado = a.Situacao is AgendaSituacao.Cancelado or AgendaSituacao.Remarcado;
            if (a.Presencial && a.ChegadaEstado is not null && a.Situacao == AgendaSituacao.Agendado)
            {
                r.Chegada = a.ChegadaEstado;
                r.ChegouEm = a.ChegouEm is null ? null : Iso(a.ChegouEm.Value);
                r.RecebidaEm = a.RecebidaEm is null ? null : Iso(a.RecebidaEm.Value);
                if (a.ChegadaEstado != "esperada") r.Tags.Add(TextoChegada(a));
            }
            if (a.Situacao == AgendaSituacao.Agendado && a.FimUtc < AgoraUtc) r.Tags.Add("resultado pendente");
            if (a.Situacao is AgendaSituacao.Remarcado or AgendaSituacao.Cancelado) r.Tags.Add(a.Situacao);
        }
        if (it.Compromisso is { } c)
        {
            r.Cancelado = c.Situacao == "cancelado" || c.SituacaoAusencia == "recusada";
            if (c.Tipo == AgendaTipoManual.Ausencia)
            {
                r.Ausencia = c.SituacaoAusencia;
                if (c.RegistradoPorUserId != c.DonoUserId) partes.Add("registrado por " + ctx.Curto(c.RegistradoPorUserId));
                partes.Add(c.SituacaoAusencia ?? "");
                if (c.SituacaoAusencia == "informada") r.Tags.Add("informada");
                if (c.DonoUserId != v.UserId) r.Tags.Add("aviso da equipe");
            }
            if (c.Local is not null) partes.Add(c.Local);
            if (it.Ver.Contains(v.UserId) && !it.Envolve(v.UserId)) r.Tags.Add("compartilhado com você");
            if (c.Tipo == AgendaTipoManual.Comunicacao && c.AssistidaId is int al)
            {
                r.Titulo = ctx.Identificacao(al) + ": " + c.Titulo.ToLower(PtBr);
                partes.Clear();
                partes.Add("registrado por " + ctx.Curto(c.RegistradoPorUserId));
            }
        }
        if (it.Fonte == "aula")
        {
            r.Cancelado = it.AulaCancelada is not null;
            partes.Add((it.Sala ?? "") + " · " + ctx.AlunasDoCurso(it.CursoId!.Value).Count() + " aluna(s)");
            if (r.Cancelado) r.Tags.Add("cancelada");
            else if (it.AulaComChamada) r.Tags.Add("chamada feita");
            else if (it.FimUtc < AgoraUtc) r.Tags.Add("chamada pendente");
        }
        if (it.Fonte == "casa" && it.Dia is { } d)
        {
            if (d.Nota is not null) partes.Add(d.Nota);
            if (d.ReabertaEm is not null) r.Tags.Add("reaberta");
        }
        r.Sub = string.Join(" · ", partes.Where(p => !string.IsNullOrWhiteSpace(p)));
        return r;
    }

    /// <summary>Linhas do detalhe (nunca relato, evolução, anotação ou motivo).</summary>
    public async Task<AgendaDetalheResponse> DetalheAsync(Contexto ctx, Item it, UsuarioAtuante v, string nivel, CancellationToken ct)
    {
        var resp = new AgendaDetalheResponse { Item = Montar(ctx, it, v, nivel) };
        var linhas = resp.Linhas;
        var quando = it.DiaInteiro || (it.FimUtc is null && it.Fonte is "casa")
            ? $"{ParaLocal(it.IniUtc).ToString("dddd", PtBr)}, {Dm(it.IniUtc)} (dia inteiro)"
            : $"{ParaLocal(it.IniUtc).ToString("dddd", PtBr)}, {Dm(it.IniUtc)}, {Hm(it.IniUtc)}{(it.FimUtc is null ? "" : "–" + Hm(it.FimUtc.Value))}";
        linhas.Add(["Quando", quando]);

        switch (nivel)
        {
            case NivelRecepcao:
                linhas.Add(["Assistida", ctx.Identificacao(it.AssistidaId!.Value)]);
                linhas.Add(["O quê", "Atendimento com a equipe técnica"]);
                linhas.Add(["Situação", Maiuscula(TextoChegada(it.Agendamento!))]);
                if ((it.Agendamento!.ChegadaEstado ?? "esperada") == "esperada") resp.Acoes.Add("chegou");
                resp.Nota = "A Recepção não vê com qual área é o atendimento, nem o motivo.";
                return resp;
            case NivelSemAcesso:
                linhas.Add(["O quê", "Compromisso de um caso que você não acessa mais"]);
                resp.Nota = "Você criou ou participa deste compromisso, mas não tem mais permissão no caso/prontuário de origem. Por isso não aparecem assistida, título nem observação.";
                return resp;
            case NivelCurso:
                linhas.Add(["Turma", ctx.Cursos[it.CursoId!.Value].Nome]);
                if (it.DonoId.Length > 0) linhas.Add(["Professora", ctx.Nome(it.DonoId)]);
                if (it.Sala is not null) linhas.Add(["Sala", it.Sala]);
                if (it.AulaCancelada is not null) linhas.Add(["Situação", "Cancelada: " + it.AulaCancelada]);
                resp.Nota = "Aula de outra turma: só dia, horário, sala e professora. Lista de alunas e observações não aparecem.";
                return resp;
            case NivelContagem:
                resp.Nota = "Atendimentos técnicos aparecem para a Coordenação só como número.";
                return resp;
        }

        if (it.Fonte == "casa")
        {
            linhas.Add(["O quê", it.Titulo]);
            if (it.Dia!.Nota is not null) linhas.Add(["Observação", it.Dia.Nota]);
            if (it.Dia.ReabertaEm is not null) linhas.Add(["Reaberta", $"por {it.Dia.ReabertaPorIdentificador}: {it.Dia.ReabertaMotivo}"]);
            return resp;
        }

        if (it.Fonte == "aula")
        {
            var curso = ctx.Cursos[it.CursoId!.Value];
            linhas.Add(["Turma", curso.Nome]);
            if (it.Sala is not null) linhas.Add(["Sala", it.Sala]);
            linhas.Add(["Alunas", ctx.AlunasDoCurso(curso.Id).Count().ToString(CultureInfo.InvariantCulture)]);
            linhas.Add(["Chamada", it.AulaCancelada is not null ? "não se aplica (aula cancelada)" : it.AulaComChamada ? "feita" : it.FimUtc < AgoraUtc ? "pendente" : "ainda não é o dia"]);
            if (it.AulaCancelada is not null) linhas.Add(["Situação", "Cancelada: " + it.AulaCancelada]);
            var dia = it.AulaData!.Value;
            var inicioDia = InicioDiaUtc(dia);
            var comunicacoes = await _db.AgendaCompromissos.AsNoTracking()
                .Where(c => c.Tipo == AgendaTipoManual.Comunicacao && c.CursoId == curso.Id && c.InicioUtc >= inicioDia && c.InicioUtc < inicioDia.AddDays(1) && c.Situacao == "ativo")
                .ToListAsync(ct);
            if (comunicacoes.Count > 0)
            {
                linhas.Add(["Comunicações", string.Join("\n", comunicacoes.Select(c => $"{ctx.Identificacao(c.AssistidaId ?? 0)}: {c.Titulo.ToLower(PtBr)} (registrado por {ctx.Curto(c.RegistradoPorUserId)})"))]);
                resp.Nota = "A comunicação aparece ao lado do nome, mas não marca nada na chamada.";
            }
            if (ctx.ProfessorasDoCurso(curso.Id).Contains(v.UserId) || v.Perfil == PerfisAcesso.Adm)
            {
                if (it.AulaCancelada is not null) resp.Acoes.Add("desfazerAula");
                else if (!it.AulaComChamada && it.IniUtc > AgoraUtc) resp.Acoes.Add("cancelarAula");
            }
            return resp;
        }

        linhas.Add(["O quê", it.Titulo]);
        if (it.DonoId.Length > 0) linhas.Add(["Responsável", ctx.Nome(it.DonoId)]);
        if (it.AssistidaId is int aid) linhas.Add(["Assistida", ctx.Identificacao(aid)]);
        if (it.Area is not null && it.AssistidaId is int aid2)
        {
            var refe = Referencia(ctx, it.Area, aid2);
            if (refe is not null) linhas.Add(["Origem", refe]);
        }

        if (it.Agendamento is { } a)
        {
            if (a.DeslocamentoMinutos > 0) linhas.Add(["Deslocamento", $"{a.DeslocamentoMinutos} min antes e depois (ocupa a agenda)"]);
            linhas.Add(["Presencial", a.Presencial ? "sim" + (a.AvisarRecepcao ? " · Recepção avisada no dia" : " · Recepção não avisada") : "não"]);
            linhas.Add(["Situação", a.Situacao.Replace('_', ' ')]);
            if (a.Presencial && a.ChegadaEstado is not null && a.Situacao == AgendaSituacao.Agendado) linhas.Add(["Chegada", TextoChegada(a)]);
            if (a.Observacao is not null) linhas.Add(["Observação", a.Observacao]);
            if (a.Justificativa is not null) linhas.Add(["Justificativa", a.Justificativa]);
            if (a.ExcecaoMotivo is not null) linhas.Add(["Exceção", $"Unidade fechada: exceção autorizada por {a.ExcecaoPorIdentificador} só para este agendamento ({a.ExcecaoMotivo})"]);
            if (a.Historico.Count > 0) linhas.Add(["Histórico", string.Join("\n", a.Historico.OrderBy(h => h.Em).Select(h => $"{Dm(h.Em)} {Hm(h.Em)} · {h.Texto}"))]);
            var dono = a.ResponsavelUserId == v.UserId;
            if (a.Situacao == AgendaSituacao.Agendado)
            {
                if (dono && a.ChegadaEstado == "chegou") resp.Acoes.Add("recebida");
                if (dono && a.FimUtc < AgoraUtc) resp.Acoes.Add("resultado");
                if (dono && a.InicioUtc > AgoraUtc) { resp.Acoes.Add("remarcar"); resp.Acoes.Add("cancelarAgendamento"); }
            }
            resp.Nota = "Abrir este detalhe fica registrado no histórico de ações. Relatos e evoluções nunca aparecem na agenda.";
            return resp;
        }

        if (it.Compromisso is { } c)
        {
            if (c.RegistradoPorUserId != c.DonoUserId) linhas.Add(["Registrado por", $"{ctx.Nome(c.RegistradoPorUserId)} (em nome de {ctx.Curto(c.DonoUserId)})"]);
            if (c.Local is not null) linhas.Add(["Local", c.Local + " (informar a sala não reserva a sala)"]);
            if (it.Participantes.Count > 0) linhas.Add(["Participantes", string.Join(", ", it.Participantes.Select(ctx.Curto)) + " (ocupa a agenda delas)"]);
            linhas.Add(["Quem pode ver", c.Visibilidade switch
            {
                AgendaVisibilidade.Eu => "Só quem participa",
                AgendaVisibilidade.Area => "Minha área",
                AgendaVisibilidade.Pessoas => "Pessoas escolhidas" + (it.Ver.Count > 0 ? ": " + string.Join(", ", it.Ver.Select(ctx.Curto)) : ""),
                _ => "Toda a equipe"
            } + " (só ver não ocupa a agenda)"]);
            if (c.Tipo == AgendaTipoManual.Ausencia)
                linhas.Add(["Ausência", c.SituacaoAusencia == "informada" ? "INFORMADA (não cancela nada e não bloqueia até ser confirmada)" : c.SituacaoAusencia ?? ""]);
            if (c.SerieId is not null) linhas.Add(["Repete", "toda semana"]);
            if (c.Justificativa is not null) linhas.Add(["Justificativa", c.Justificativa]);
            if (c.ExcecaoMotivo is not null) linhas.Add(["Exceção", $"Unidade fechada: exceção autorizada por {c.ExcecaoPorIdentificador} só para este compromisso ({c.ExcecaoMotivo})"]);
            if (c.Situacao == "cancelado") linhas.Add(["Situação", "cancelado"]);
            if (it.Ver.Contains(v.UserId) && !it.Envolve(v.UserId)) resp.Nota = "Compartilhado com você só para ver: não ocupa a sua agenda e não entra na conferência dos seus horários.";
            if (c.Tipo == AgendaTipoManual.Comunicacao) resp.Nota = "É só uma comunicação: não marca nada na chamada. O motivo pessoal não é registrado aqui.";
            if (c.Tipo == AgendaTipoManual.Ausencia && c.SituacaoAusencia == "informada" && (c.DonoUserId == v.UserId || v.Perfil == PerfisAcesso.Adm))
            {
                resp.Acoes.Add("ausOk");
                resp.Acoes.Add("ausNao");
            }
            if ((c.DonoUserId == v.UserId || c.RegistradoPorUserId == v.UserId) && c.Situacao == "ativo" && c.Tipo != AgendaTipoManual.Ausencia
                && (c.FimUtc ?? c.InicioUtc) > AgoraUtc)
            {
                resp.Acoes.Add(c.SerieId is null ? "cancelarManual" : "cancelarSerie");
            }
            return resp;
        }

        // marcos
        if (it.Tipo == "proxima_sessao") resp.Nota = "Anotação antiga da sessão. Para reservar o horário, crie um agendamento.";
        return resp;
    }

    private static string Maiuscula(string s) => s.Length == 0 ? s : char.ToUpper(s[0], PtBr) + s[1..];

    private static string? Referencia(Contexto ctx, string area, int assistidaId) => area switch
    {
        AgendaArea.Psicologia => ctx.ProntuariosPsi.TryGetValue(assistidaId, out var p) ? p.Numero : null,
        AgendaArea.ServicoSocial => ctx.ProntuariosSoc.TryGetValue(assistidaId, out var s) ? s.Numero : null,
        AgendaArea.Juridico => ctx.Casos.TryGetValue(assistidaId, out var c) ? "Caso jurídico" + (c.Visibilidade == CasoJuridicoVisibilidade.Responsaveis ? " · somente responsáveis" : "") : null,
        _ => null
    };

    // ---------------- pendências ----------------
    /// <summary>
    /// Pendências da pessoa. Todas conferem a permissão ATUAL na origem antes de serem montadas
    /// (a mesma checagem é refeita ao executar a ação).
    /// </summary>
    public async Task<List<AgendaPendenciaResponse>> PendenciasAsync(Contexto ctx, UsuarioAtuante v, CancellationToken ct)
    {
        var lista = new List<AgendaPendenciaResponse>();
        var agora = AgoraUtc;
        var hoje = Hoje;
        bool Pode(string? area, int? aid) => AcessoOrigem(ctx, v, area, aid);

        var meus = await _db.AgendaAgendamentos.AsNoTracking()
            .Where(a => a.ResponsavelUserId == v.UserId && a.Situacao == AgendaSituacao.Agendado && a.InicioUtc > agora.AddDays(-60))
            .ToListAsync(ct);
        foreach (var a in meus.Where(a => Pode(a.Area, a.AssistidaId)).OrderBy(a => a.InicioUtc))
        {
            var titulo = TituloAgendamento(a.Area, a.Tipo);
            if (a.ChegadaEstado == "chegou")
                lista.Insert(0, new AgendaPendenciaResponse { Tipo = "chegada", Id = "ag:" + a.Id, Texto = $"{ctx.Identificacao(a.AssistidaId)} chegou às {(a.ChegouEm is null ? "-" : Hm(a.ChegouEm.Value))} para {titulo.ToLower(PtBr)} das {Hm(a.InicioUtc)}", Acoes = ["recebida"] });
            if (a.FimUtc < agora)
                lista.Add(new AgendaPendenciaResponse { Tipo = "resultado", Id = "ag:" + a.Id, Texto = $"Resultado pendente de registro: {titulo} de {ctx.Identificacao(a.AssistidaId)} ({DiaSemana(a.InicioUtc)} {Dm(a.InicioUtc)} {Hm(a.InicioUtc)})", Nota = "Horário passado sem registro não significa falta.", Acoes = ["resultado"] });
        }

        // ausências informadas por colega (a própria pessoa ou a ADM confirma)
        var ausencias = await _db.AgendaCompromissos.AsNoTracking()
            .Where(c => c.Tipo == AgendaTipoManual.Ausencia && c.SituacaoAusencia == "informada" && c.Situacao == "ativo"
                && (c.FimUtc ?? c.InicioUtc.AddDays(1)) > agora.AddDays(-7))
            .ToListAsync(ct);
        foreach (var c in ausencias.Where(c => (c.DonoUserId == v.UserId && c.RegistradoPorUserId != v.UserId) || v.Perfil == PerfisAcesso.Adm))
        {
            lista.Add(new AgendaPendenciaResponse
            {
                Tipo = "ausencia", Id = "cp:" + c.Id,
                Texto = $"{ctx.Curto(c.RegistradoPorUserId)} informou uma ausência {(c.DonoUserId == v.UserId ? "sua" : "de " + ctx.Curto(c.DonoUserId))}: {DiaSemana(c.InicioUtc)} {Dm(c.InicioUtc)}, {(c.DiaInteiro ? "dia inteiro" : Hm(c.InicioUtc) + "–" + (c.FimUtc is null ? "" : Hm(c.FimUtc.Value)))}",
                Acoes = c.DonoUserId == v.UserId || v.Perfil == PerfisAcesso.Adm ? ["ausOk", "ausNao"] : []
            });
        }

        // revisão depois de "Passar responsabilidades": nada entra sozinho na agenda da sucessora
        var revisoes = await _db.AgendaRevisoes.AsNoTracking().Include(r => r.Agendamento)
            .Where(r => r.ParaUserId == v.UserId && r.Situacao == "pendente")
            .ToListAsync(ct);
        foreach (var r in revisoes.Where(r => r.Agendamento is not null && Pode(r.Agendamento.Area, r.Agendamento.AssistidaId) && r.Agendamento.Situacao == AgendaSituacao.Agendado))
        {
            var a = r.Agendamento!;
            lista.Insert(0, new AgendaPendenciaResponse
            {
                Tipo = "revisar", Id = "ag:" + a.Id,
                Texto = $"Compromisso recebido de {ctx.Curto(r.DeUserId)} para revisar: {TituloAgendamento(a.Area, a.Tipo)} de {ctx.Identificacao(a.AssistidaId)}, {DiaSemana(a.InicioUtc)} {Dm(a.InicioUtc)} {Hm(a.InicioUtc)}",
                Nota = "Ainda não está na sua agenda. Assuma (o sistema confere sua disponibilidade) ou remarque.",
                Acoes = ["assumir", "remarcar"]
            });
        }

        // reavaliação de risco (Psicologia): aberta com risco alto/iminente, resolvida ao registrar a reavaliação
        if (AssistidasService.AtuaComoPsicologia(v.Perfil))
        {
            var reavaliacoes = await _db.ReavaliacoesRiscoPsicologicas.AsNoTracking()
                .GroupBy(r => r.ProntuarioPsicologicoId).Select(g => new { g.Key, Ultima = g.Max(x => x.CriadoEm) })
                .ToDictionaryAsync(x => x.Key, x => x.Ultima, ct);
            foreach (var p in ctx.ProntuariosPsi.Values.Where(p => PsicologiaCatalogo.RiscoElevado(p.RiscoAtual) && p.Situacao == ProntuarioSituacao.EmAcompanhamento
                && p.Responsaveis.Any(r => r.UserId == v.UserId) && Pode(AgendaArea.Psicologia, p.AssistidaId)))
            {
                if (reavaliacoes.TryGetValue(p.Id, out var ultima) && ultima >= (p.RiscoAtualizadoEm ?? DateTime.MinValue)) continue;
                lista.Insert(0, new AgendaPendenciaResponse
                {
                    Tipo = "reavaliacao", Id = "psi:" + p.AssistidaId,
                    Texto = $"Reavaliação de risco pendente: {ctx.Identificacao(p.AssistidaId)} (risco {p.RiscoAtual} registrado em {(p.RiscoAtualizadoEm is null ? "-" : Dm(p.RiscoAtualizadoEm.Value))})",
                    Nota = "Situações urgentes seguem o fluxo de atendimento da Casa (CAPS, SAMU 192, CVV 188).",
                    Acoes = ["reavaliar"]
                });
            }
        }

        // aulas: chamada pendente e aula em dia de unidade fechada
        if (v.Perfil == PerfisAcesso.Professor)
        {
            var meusCursos = ctx.ProfessoresCursos.Where(p => p.UserId == v.UserId).Select(p => p.CursoId).ToHashSet();
            var de = hoje.AddDays(-30);
            var aulas = Aulas(ctx, de, hoje.AddDays(30)).Where(a => meusCursos.Contains(a.CursoId!.Value) && a.AulaCancelada is null).ToList();
            foreach (var a in aulas.Where(a => a.FimUtc < agora && !a.AulaComChamada))
                lista.Add(new AgendaPendenciaResponse { Tipo = "chamada", Id = a.Id, Texto = $"Chamada pendente: {ctx.Cursos[a.CursoId!.Value].Nome}, {DiaSemana(a.IniUtc)} {Dm(a.IniUtc)}", Acoes = ["chamada"] });
            foreach (var a in aulas.Where(a => a.IniUtc > agora && ctx.Dias.Any(d => d.Tipo == "fechada" && d.ReabertaEm == null && d.Data == a.AulaData)))
                lista.Add(new AgendaPendenciaResponse { Tipo = "fechada", Id = a.Id, Texto = $"Aula em dia de unidade fechada ({Dm(a.IniUtc)}): cancelar ou remarcar {ctx.Cursos[a.CursoId!.Value].Nome}", Acoes = ["cancelarAula"] });
        }

        // Jurídico: encaminhamentos recebidos sem agendamento
        if (v.Perfil == PerfisAcesso.Juridico)
        {
            var semAgendamento = ctx.Assistidas.Values
                .Where(a => a.Ativa && a.EncaminhadaJuridicoEm != null && a.JuridicoAgendadoPara == null && a.JuridicoEncaminhamentoCanceladoEm == null
                    && !ctx.Casos.ContainsKey(a.Id))
                .OrderBy(a => a.EncaminhadaJuridicoEm).ToList();
            foreach (var a in semAgendamento.Take(5))
                lista.Add(new AgendaPendenciaResponse { Tipo = "encaminhamento", Id = "jur:" + a.Id, Texto = $"Encaminhamento recebido sem agendamento: {ctx.Identificacao(a.Id)} (desde {Dm(a.EncaminhadaJuridicoEm!.Value)})", Acoes = ["agendar"] });
            if (semAgendamento.Count > 5)
                lista.Add(new AgendaPendenciaResponse { Tipo = "encaminhamento", Id = "jur:fila", Texto = $"E mais {semAgendamento.Count - 5} na fila de encaminhadas." });
        }

        return lista;
    }

    /// <summary>ADM: atendimentos técnicos por dia, só números.</summary>
    public static Dictionary<string, AgendaContagemDia> Contagens(IEnumerable<Item> itens)
    {
        var saida = new Dictionary<string, AgendaContagemDia>();
        foreach (var it in itens.Where(i => i.Fonte == "agendamento" && i.Ocupa))
        {
            var k = DiaLocal(it.IniUtc).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (!saida.TryGetValue(k, out var c)) saida[k] = c = new AgendaContagemDia();
            if (it.Area == AgendaArea.Juridico) c.Juridico++;
            else if (it.Area == AgendaArea.Psicologia) c.Psicologia++;
            else c.ServicoSocial++;
        }
        return saida;
    }
}
