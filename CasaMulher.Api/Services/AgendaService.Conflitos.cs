using System.Globalization;
using CasaMulher.Api.Models;

namespace CasaMulher.Api.Services;

public sealed record AgendaConflito(string Nivel, string Mensagem, bool UnidadeFechada = false);

public sealed class AgendaContextoConferencia
{
    public required UsuarioAtuante Viewer { get; init; }
    public required string DonoId { get; init; }
    public List<string> Participantes { get; init; } = [];
    public int? AssistidaId { get; init; }
    public required DateTime IniUtc { get; init; }
    public required DateTime FimUtc { get; init; }
    public bool DiaInteiro { get; init; }
    public string? IgnorarItemId { get; init; }
    /// <summary>Exceção de unidade fechada já autorizada para ESTE compromisso.</summary>
    public bool ComExcecao { get; init; }
}

public sealed record AgendaFaixa(DateTime IniUtc, DateTime FimUtc, string Rotulo, bool Revelado, bool Aviso, string Cor);

public sealed partial class AgendaService
{
    public const string Bloqueio = "bloqueio";
    public const string Confirmar = "confirmar";
    public const string Aviso = "aviso";

    /// <summary>
    /// O horário cai fora do funcionamento da Casa neste dia? Devolve a mensagem (ou null).
    /// Fechada: o dia todo. Horário especial: fora do intervalo. "Reabrir para todos" volta ao normal.
    /// </summary>
    public static string? ForaDoFuncionamento(AgendaDiaEspecial d, DateTime iniUtc, DateTime? fimUtc, bool diaInteiro)
    {
        if (d.ReabertaEm is not null) return null;
        var nome = d.Tipo == AgendaTipoDia.Feriado ? "Feriado: " + d.Titulo : d.Titulo;
        if (d.Funcionamento == AgendaFuncionamento.Fechada) return $"Casa fechada neste dia ({nome}).";
        if (d.Funcionamento != AgendaFuncionamento.Especial || d.HorarioInicio is null || d.HorarioFim is null) return null;
        var faixa = $"das {d.HorarioInicio:HH\\:mm} às {d.HorarioFim:HH\\:mm}";
        if (diaInteiro) return $"Horário especial neste dia ({nome}): a Casa funciona só {faixa}.";
        var ini = TimeOnly.FromDateTime(ParaLocal(iniUtc));
        var fimLocal = ParaLocal(fimUtc ?? iniUtc);
        var fim = TimeOnly.FromDateTime(fimLocal);
        var outroDia = fimLocal.Date > ParaLocal(iniUtc).Date;
        if (ini < d.HorarioInicio || fim > d.HorarioFim || outroDia) return $"Horário especial neste dia ({nome}): a Casa funciona só {faixa}.";
        return null;
    }

    private static bool Sobrepoe((DateTime Ini, DateTime Fim) a, (DateTime Ini, DateTime Fim) b) => a.Ini < b.Fim && b.Ini < a.Fim;

    private static string H(DateTime utc) => Hm(utc);

    /// <summary>
    /// Conferência de conflitos de UMA ocorrência. Usa todos os itens, inclusive os que a pessoa
    /// não pode ver (oculto não é livre); a mensagem só revela o que ela já poderia ver.
    /// </summary>
    public List<AgendaConflito> Conflitos(Contexto ctx, List<Item> todosItens, AgendaContextoConferencia c)
    {
        var v = c.Viewer;
        var saida = new List<AgendaConflito>();
        var vistos = new HashSet<string>();
        void Add(string nivel, string msg, string? chave = null, bool fechada = false)
        {
            if (chave is not null && !vistos.Add(chave)) return;
            if (saida.Any(o => o.Mensagem == msg)) return;
            saida.Add(new AgendaConflito(nivel, msg, fechada));
        }

        var diaLocal = DiaLocal(c.IniUtc);
        var janela = c.DiaInteiro ? (InicioDiaUtc(diaLocal), InicioDiaUtc(diaLocal.AddDays(1))) : (c.IniUtc, c.FimUtc);
        var todos = todosItens.Where(i => i.Id != c.IgnorarItemId).ToList();

        foreach (var d in ctx.Dias.Where(d => d.Data == diaLocal))
        {
            var fora = ForaDoFuncionamento(d, c.IniUtc, c.FimUtc, c.DiaInteiro);
            if (fora is not null)
            {
                if (c.ComExcecao) Add(Aviso, $"{fora} Exceção autorizada só para este compromisso.");
                else Add(Bloqueio, $"{fora} Só a ADM pode autorizar uma exceção, ligada a este compromisso.", null, true);
            }
            else if (d.Funcionamento == AgendaFuncionamento.Especial && d.ReabertaEm is null && d.HorarioInicio is not null)
            {
                Add(Aviso, $"Horário especial da Casa neste dia ({d.Titulo}): das {d.HorarioInicio:HH\\:mm} às {d.HorarioFim:HH\\:mm}.");
            }
            if (d.Tipo == AgendaTipoDia.Feriado && fora is null)
            {
                Add(Aviso, $"Feriado: {d.Titulo}. A Casa funciona{(d.Funcionamento == AgendaFuncionamento.Especial ? " em horário especial" : " normalmente")}.{(string.IsNullOrWhiteSpace(d.Nota) ? "" : " " + d.Nota)}");
            }
        }
        if (c.IniUtc < AgoraUtc && !c.DiaInteiro) Add(Aviso, "Esse horário já passou.");

        var pessoas = new List<string> { c.DonoId };
        pessoas.AddRange(c.Participantes.Where(p => !string.IsNullOrEmpty(p) && p != c.DonoId));
        foreach (var pid in pessoas.Distinct())
        {
            var eu = pid == v.UserId;
            var curto = ctx.Curto(pid);
            var dela = todos.Where(i => i.Envolve(pid) && i.Ocupa).ToList();
            foreach (var it in dela)
            {
                if (!Sobrepoe(it.Janela, janela)) continue;
                if (it.Tipo == AgendaTipoManual.Ausencia)
                {
                    var quando = it.DiaInteiro ? "o dia inteiro" : "nesse horário";
                    if (it.Compromisso?.SituacaoAusencia == "informada")
                        Add(Aviso, $"Há uma ausência informada para {(eu ? "você" : curto)} {quando}, ainda não confirmada. Nada foi cancelado por causa dela.", it.Id);
                    else
                        Add(Confirmar, $"{(eu ? "Você está" : curto + " está")} ausente {quando} (ausência confirmada).", it.Id);
                    continue;
                }
                var n = Revelar(ctx, it, v);
                if (n == NivelDetalhe)
                {
                    var quando = it.DiaInteiro || it.FimUtc is null ? "(dia inteiro)" : $"das {H(it.IniUtc)} às {H(it.FimUtc.Value)}";
                    Add(Confirmar, $"{(eu ? "Você já tem" : curto + " já tem")} {it.Titulo.ToLower(CultureInfo.GetCultureInfo("pt-BR"))} {quando}{(it.Deslocamento > 0 ? " (com deslocamento)" : "")}.", it.Id);
                }
                else
                {
                    Add(Confirmar, $"Há uma indisponibilidade {(eu ? "na sua agenda" : "na agenda de " + curto)} nesse horário.", it.Id);
                }
            }

            if (!c.DiaInteiro)
            {
                var doDia = dela.Where(i => DiaLocal(i.IniUtc) == diaLocal && !i.DiaInteiro && i.FimUtc is not null).ToList();
                var horas = doDia.Sum(i => (i.Janela.Fim - i.Janela.Ini).TotalHours) + (c.FimUtc - c.IniUtc).TotalHours;
                var lim = ctx.Config.LimiteCompromissosDia;
                if (doDia.Count + 1 > lim || horas > ctx.Config.LimiteHorasDia)
                {
                    Add(Aviso, $"{(eu ? "Seu dia" : "O dia de " + curto)} ficaria com {doDia.Count + 1} compromissos e {horas.ToString("0.0", CultureInfo.GetCultureInfo("pt-BR"))} h ocupadas (aviso a partir de {lim} compromissos ou {ctx.Config.LimiteHorasDia} h).");
                }
                if (pid == c.DonoId)
                {
                    var colado = dela.FirstOrDefault(i =>
                    {
                        if (i.DiaInteiro || i.FimUtc is null) return false;
                        var j = i.Janela;
                        return !Sobrepoe(j, janela) && (Math.Abs((j.Fim - janela.Item1).TotalMinutes) < 10 || Math.Abs((janela.Item2 - j.Ini).TotalMinutes) < 10);
                    });
                    if (colado is not null)
                    {
                        var depois = colado.FimUtc <= c.IniUtc;
                        Add(Aviso, $"Fica sem intervalo {(depois ? "depois" : "antes")} de outro compromisso{(Revelar(ctx, colado, v) == NivelDetalhe ? $" ({colado.Titulo.ToLower(CultureInfo.GetCultureInfo("pt-BR"))} das {H(colado.IniUtc)})" : "")}.");
                    }
                    var ini = TimeOnly.FromDateTime(ParaLocal(c.IniUtc));
                    var fim = TimeOnly.FromDateTime(ParaLocal(c.FimUtc));
                    var dow = ParaLocal(c.IniUtc).DayOfWeek;
                    if (ini < ctx.Config.ExpedienteInicio || fim > ctx.Config.ExpedienteFim || ParaLocal(c.FimUtc).Date > ParaLocal(c.IniUtc).Date || dow is DayOfWeek.Saturday or DayOfWeek.Sunday)
                    {
                        Add(Aviso, $"Fora do expediente{(eu ? $" ({ctx.Config.ExpedienteInicio:HH\\:mm}–{ctx.Config.ExpedienteFim:HH\\:mm})" : " de " + curto)}.");
                    }
                }
            }
        }

        if (c.AssistidaId is int aid)
        {
            foreach (var it in todos)
            {
                if (vistos.Contains(it.Id) || !it.Ocupa || it.Tipo is AgendaTipoManual.Comunicacao or AgendaTipoManual.Ausencia) continue;
                var daAssistida = it.AssistidaId == aid || (it.Fonte == "aula" && ctx.AlunasDoCurso(it.CursoId!.Value).Contains(aid));
                if (!daAssistida || !Sobrepoe(it.Janela, janela)) continue;
                if (it.Fonte == "aula")
                {
                    if (VeCursoDaAssistida(ctx, v, aid))
                        Add(Confirmar, $"A assistida tem aula de {ctx.Cursos[it.CursoId!.Value].Nome} das {H(it.IniUtc)} às {H(it.FimUtc!.Value)}.", it.Id);
                    else
                        Add(Confirmar, "Há uma indisponibilidade da assistida nesse horário.", it.Id);
                }
                else if (Revelar(ctx, it, v) == NivelDetalhe)
                {
                    Add(Confirmar, $"A assistida já tem {it.Titulo.ToLower(CultureInfo.GetCultureInfo("pt-BR"))} das {H(it.IniUtc)} às {H(it.FimUtc ?? it.IniUtc)}.", it.Id);
                }
                else
                {
                    Add(Confirmar, "Há uma indisponibilidade da assistida nesse horário.", it.Id);
                }
            }
        }

        var ordem = new Dictionary<string, int> { [Bloqueio] = 0, [Confirmar] = 1, [Aviso] = 2 };
        return saida.OrderBy(o => ordem[o.Nivel]).ToList();
    }

    /// <summary>
    /// Faixas ocupadas para o modo de escolha ("Ver disponibilidade"). Ausência só informada vem
    /// como aviso: aparece, mas não impede escolher o horário.
    /// </summary>
    public List<AgendaFaixa> Faixas(Contexto ctx, List<Item> todosItens, UsuarioAtuante v, List<string> pessoas, int? assistidaId, string? ignorarItemId)
    {
        var saida = new List<AgendaFaixa>();
        foreach (var it in todosItens)
        {
            if (it.Id == ignorarItemId || !it.Ocupa || it.Tipo == AgendaTipoManual.Comunicacao) continue;
            var daPessoa = pessoas.Any(it.Envolve);
            var daAssistida = assistidaId is int aid && it.Tipo != AgendaTipoManual.Ausencia
                && (it.AssistidaId == aid || (it.Fonte == "aula" && ctx.AlunasDoCurso(it.CursoId!.Value).Contains(aid)));
            if (!daPessoa && !daAssistida) continue;
            var n = Revelar(ctx, it, v);
            var aviso = it.Tipo == AgendaTipoManual.Ausencia && it.Compromisso?.SituacaoAusencia == "informada";
            var rot = "Indisponível";
            var cor = "k-neu";
            if (aviso) rot = "Ausência informada (aviso)";
            else if (n == NivelDetalhe && daPessoa) { rot = it.Titulo; cor = Cor(it, n); }
            else if (it.Fonte == "aula" && daAssistida && VeCursoDaAssistida(ctx, v, assistidaId!.Value)) { rot = "Aula da assistida"; cor = "k-aula"; }
            var revelado = rot != "Indisponível" && !aviso;
            if (pessoas.Count > 1 && !it.Envolve(v.UserId) && daPessoa && it.DonoId.Length > 0) rot += " · " + ctx.Curto(it.DonoId);
            var j = it.Janela;
            saida.Add(new AgendaFaixa(j.Ini, j.Fim, rot, revelado, aviso, aviso ? "k-aus" : cor));
        }
        return saida;
    }
}
