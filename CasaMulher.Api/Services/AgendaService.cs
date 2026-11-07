using System.Globalization;
using CasaMulher.Api.Data;
using CasaMulher.Api.Models;
using CasaMulher.Api.Security;
using Microsoft.EntityFrameworkCore;

namespace CasaMulher.Api.Services;

/// <summary>
/// Agenda e Calendário. Monta, para quem está olhando, os compromissos automáticos
/// (agendamentos, aulas, marcos, dias da Casa) e os manuais, já com o nível de revelação.
/// Regras (plano v3): a permissão na ORIGEM vem primeiro (cadastro não basta; autoria não basta);
/// oculto não é livre (conta nos conflitos); disponibilidade só em contexto de agendamento.
/// </summary>
public sealed partial class AgendaService
{
    public const string NivelDetalhe = "detalhe";
    public const string NivelRecepcao = "recepcao";
    public const string NivelCurso = "curso";
    public const string NivelContagem = "contagem";
    public const string NivelSemAcesso = "semacesso";
    public const string NivelOcupado = "ocupado";
    public const string NivelOculto = "oculto";

    private static readonly TimeZoneInfo Fuso = CarregarFuso();

    private readonly AppDbContext _db;

    public AgendaService(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>Relógio (substituível nos testes).</summary>
    public Func<DateTime> RelogioUtc { get; set; } = () => DateTime.UtcNow;

    public DateTime AgoraUtc => RelogioUtc();

    // ---------------- fuso ----------------
    private static TimeZoneInfo CarregarFuso()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.CreateCustomTimeZone("BRT", TimeSpan.FromHours(-3), "BRT", "BRT"); }
    }

    public static DateTime ParaLocal(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Fuso);

    public static DateTime ParaUtc(DateTime local) => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), Fuso);

    public static DateTime InicioDiaUtc(DateOnly dia) => ParaUtc(dia.ToDateTime(TimeOnly.MinValue));

    public static DateOnly DiaLocal(DateTime utc) => DateOnly.FromDateTime(ParaLocal(utc));

    public DateOnly Hoje => DiaLocal(AgoraUtc);

    public static string Hm(DateTime utc) => ParaLocal(utc).ToString("HH:mm", CultureInfo.InvariantCulture);

    public static string Dm(DateTime utc) => ParaLocal(utc).ToString("dd/MM", CultureInfo.InvariantCulture);

    public static string Dm(DateOnly d) => d.ToString("dd/MM", CultureInfo.InvariantCulture);

    private static readonly string[] DiasSemana = ["dom", "seg", "ter", "qua", "qui", "sex", "sáb"];

    public static string DiaSemana(DateTime utc) => DiasSemana[(int)ParaLocal(utc).DayOfWeek];

    public static string CodigoAssistida(int id) => "AS-" + id.ToString("D4", CultureInfo.InvariantCulture);

    // ---------------- contexto carregado ----------------
    public sealed class Contexto
    {
        public required Dictionary<string, ApplicationUser> Usuarios { get; init; }
        public required Dictionary<int, Assistida> Assistidas { get; init; }
        public required Dictionary<int, CasoJuridico> Casos { get; init; }
        public required Dictionary<int, ProntuarioPsicologico> ProntuariosPsi { get; init; }
        public required Dictionary<int, ProntuarioSocial> ProntuariosSoc { get; init; }
        public required Dictionary<int, Curso> Cursos { get; init; }
        public required List<ProfessorCurso> ProfessoresCursos { get; init; }
        public required List<AssistidaCurso> Alunas { get; init; }
        public required List<CursoHorario> Horarios { get; init; }
        public required List<AulaCancelamento> Cancelamentos { get; init; }
        public required HashSet<(int, DateOnly)> Chamadas { get; init; }
        public required List<AgendaDiaEspecial> Dias { get; init; }
        public required AgendaConfiguracao Config { get; init; }

        public string Nome(string userId) => Usuarios.TryGetValue(userId, out var u) ? u.NomeCompleto : "Colega";

        public string Curto(string userId)
        {
            var nome = Nome(userId).Trim();
            var partes = nome.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (partes.Length == 0) return nome;
            return partes[0] is "Dra." or "Dr." or "Prof." or "Prof.ª" && partes.Length > 1 ? partes[1] : partes[0];
        }

        public string Perfil(string userId) => Usuarios.TryGetValue(userId, out var u) ? u.Perfil.ToLowerInvariant() : string.Empty;

        public string Identificacao(int assistidaId) =>
            Assistidas.TryGetValue(assistidaId, out var a) ? $"{a.Nome} ({CodigoAssistida(a.Id)})" : CodigoAssistida(assistidaId);

        public IEnumerable<string> ProfessorasDoCurso(int cursoId) => ProfessoresCursos.Where(p => p.CursoId == cursoId).Select(p => p.UserId);

        public IEnumerable<int> AlunasDoCurso(int cursoId) =>
            Alunas.Where(a => a.CursoId == cursoId && a.Situacao == AssistidaCursoSituacao.Cursando).Select(a => a.AssistidaId);
    }

    public async Task<Contexto> CarregarContextoAsync(CancellationToken ct = default)
    {
        var config = await _db.AgendaConfiguracoes.AsNoTracking().OrderBy(c => c.Id).FirstOrDefaultAsync(ct) ?? new AgendaConfiguracao();
        return new Contexto
        {
            Usuarios = await _db.Users.AsNoTracking().ToDictionaryAsync(u => u.Id, ct),
            Assistidas = await _db.Assistidas.AsNoTracking().ToDictionaryAsync(a => a.Id, ct),
            Casos = (await _db.CasosJuridicos.AsNoTracking().Include(c => c.Responsaveis).ToListAsync(ct))
                .GroupBy(c => c.AssistidaId).ToDictionary(g => g.Key, g => g.First()),
            ProntuariosPsi = (await _db.ProntuariosPsicologicos.AsNoTracking().Include(p => p.Responsaveis).ToListAsync(ct))
                .GroupBy(p => p.AssistidaId).ToDictionary(g => g.Key, g => g.First()),
            ProntuariosSoc = (await _db.ProntuariosSociais.AsNoTracking().Include(p => p.Responsaveis).ToListAsync(ct))
                .GroupBy(p => p.AssistidaId).ToDictionary(g => g.Key, g => g.First()),
            Cursos = await _db.Cursos.AsNoTracking().ToDictionaryAsync(c => c.Id, ct),
            ProfessoresCursos = await _db.ProfessoresCursos.AsNoTracking().ToListAsync(ct),
            Alunas = await _db.AssistidasCursos.AsNoTracking().ToListAsync(ct),
            Horarios = await _db.CursosHorarios.AsNoTracking().ToListAsync(ct),
            Cancelamentos = await _db.AulasCancelamentos.AsNoTracking().Where(c => c.DesfeitoEm == null).ToListAsync(ct),
            Chamadas = (await _db.Chamadas.AsNoTracking().Select(c => new { c.CursoId, c.Data }).ToListAsync(ct))
                .Select(c => (c.CursoId, c.Data)).ToHashSet(),
            Dias = await _db.AgendaDiasEspeciais.AsNoTracking().ToListAsync(ct),
            Config = config
        };
    }

    // ---------------- item interno ----------------
    public sealed class Item
    {
        public required string Id { get; init; }
        /// <summary>agendamento, manual, aula, casa ou marco.</summary>
        public required string Fonte { get; init; }
        public required string Tipo { get; init; }
        public required DateTime IniUtc { get; init; }
        public DateTime? FimUtc { get; init; }
        public bool DiaInteiro { get; init; }
        public bool Ocupa { get; init; }
        public int Deslocamento { get; init; }
        public required string DonoId { get; init; }
        public HashSet<string> Participantes { get; init; } = [];
        public HashSet<string> Ver { get; init; } = [];
        public string? Area { get; init; }
        public int? AssistidaId { get; init; }
        public required string Titulo { get; init; }
        public AgendaAgendamento? Agendamento { get; init; }
        public AgendaCompromisso? Compromisso { get; init; }
        public AgendaDiaEspecial? Dia { get; init; }
        public int? CursoId { get; init; }
        public DateOnly? AulaData { get; init; }
        public string? AulaCancelada { get; init; }
        public bool AulaComChamada { get; init; }
        public string? Sala { get; init; }

        /// <summary>Janela ocupada (inclui deslocamento; dia inteiro = o dia todo).</summary>
        public (DateTime Ini, DateTime Fim) Janela
        {
            get
            {
                if (DiaInteiro || FimUtc is null)
                {
                    var dia = DiaLocal(IniUtc);
                    return (InicioDiaUtc(dia), InicioDiaUtc(dia.AddDays(1)));
                }
                return (IniUtc.AddMinutes(-Deslocamento), FimUtc.Value.AddMinutes(Deslocamento));
            }
        }

        public bool Envolve(string userId) => DonoId == userId || Participantes.Contains(userId);
    }

    /// <summary>Todos os itens do período (sem filtro de quem vê).</summary>
    public async Task<List<Item>> ItensAsync(Contexto ctx, DateTime deUtc, DateTime ateUtc, CancellationToken ct = default)
    {
        var itens = new List<Item>();

        var agendamentos = await _db.AgendaAgendamentos.AsNoTracking()
            .Include(a => a.Historico)
            .Where(a => a.InicioUtc < ateUtc && a.FimUtc > deUtc.AddHours(-2))
            .ToListAsync(ct);
        foreach (var a in agendamentos) itens.Add(DeAgendamento(a));

        var compromissos = await _db.AgendaCompromissos.AsNoTracking()
            .Include(c => c.Pessoas)
            .Where(c => c.InicioUtc < ateUtc && (c.FimUtc ?? c.InicioUtc.AddDays(1)) > deUtc.AddDays(-1))
            .ToListAsync(ct);
        foreach (var c in compromissos) itens.Add(DeCompromisso(c));

        var deDia = DiaLocal(deUtc);
        var ateDia = DiaLocal(ateUtc);
        foreach (var d in ctx.Dias.Where(d => d.Data >= deDia && d.Data <= ateDia))
        {
            itens.Add(new Item
            {
                Id = "dia:" + d.Id, Fonte = "casa", Tipo = d.Tipo, IniUtc = InicioDiaUtc(d.Data), DiaInteiro = true, Ocupa = false,
                DonoId = string.Empty, Titulo = d.Titulo, Dia = d
            });
        }

        itens.AddRange(Aulas(ctx, deDia, ateDia));
        itens.AddRange(await MarcosAsync(ctx, deUtc, ateUtc, agendamentos, ct));
        return itens;
    }

    public static Item DeAgendamento(AgendaAgendamento a) => new()
    {
        Id = "ag:" + a.Id, Fonte = "agendamento", Tipo = a.Tipo, IniUtc = a.InicioUtc, FimUtc = a.FimUtc,
        Ocupa = a.Situacao is not (AgendaSituacao.Cancelado or AgendaSituacao.Remarcado),
        Deslocamento = a.DeslocamentoMinutos, DonoId = a.ResponsavelUserId, Area = a.Area, AssistidaId = a.AssistidaId,
        Titulo = TituloAgendamento(a.Area, a.Tipo), Agendamento = a
    };

    public static Item DeCompromisso(AgendaCompromisso c) => new()
    {
        Id = "cp:" + c.Id, Fonte = "manual", Tipo = c.Tipo, IniUtc = c.InicioUtc, FimUtc = c.FimUtc, DiaInteiro = c.DiaInteiro,
        Ocupa = c.OcupaHorario && c.Situacao == "ativo" && c.SituacaoAusencia != "recusada" && (c.FimUtc is not null || c.DiaInteiro),
        DonoId = c.DonoUserId,
        Participantes = c.Pessoas.Where(p => p.Papel == AgendaPapel.Participante).Select(p => p.UserId).ToHashSet(),
        Ver = c.Pessoas.Where(p => p.Papel == AgendaPapel.Ver).Select(p => p.UserId).ToHashSet(),
        Area = c.OrigemArea, AssistidaId = c.AssistidaId, Titulo = c.Titulo, Compromisso = c, CursoId = c.CursoId
    };

    public static string TituloAgendamento(string area, string tipo) => (area, tipo) switch
    {
        (AgendaArea.Psicologia, "primeiro") => "Primeiro atendimento (Psicologia)",
        (AgendaArea.Psicologia, _) => "Sessão",
        (AgendaArea.Juridico, "retorno") => "Retorno jurídico",
        (AgendaArea.Juridico, "primeiro") => "Primeiro atendimento jurídico",
        (AgendaArea.Juridico, "audiencia") => "Audiência",
        (AgendaArea.Juridico, _) => "Atendimento jurídico",
        (AgendaArea.ServicoSocial, "visita") => "Visita domiciliar",
        (AgendaArea.ServicoSocial, "visita_institucional") => "Visita institucional",
        (AgendaArea.ServicoSocial, _) => "Atendimento social",
        _ => "Atendimento"
    };

    // ---------------- aulas ----------------
    public static string? MotivoCancelamentoAula(Contexto ctx, int cursoId, DateOnly data, DateTime aulaIniUtc)
    {
        var esta = ctx.Cancelamentos.FirstOrDefault(c => c.CursoId == cursoId && c.Escopo == "esta" && c.Data == data);
        if (esta is not null) return esta.Motivo;
        // "esta e as próximas": só ocorrências futuras no momento do cancelamento e sem chamada
        var serie = ctx.Cancelamentos
            .Where(c => c.CursoId == cursoId && c.Escopo == "seguintes" && c.Data <= data && aulaIniUtc > c.CriadoEm)
            .OrderBy(c => c.Data).FirstOrDefault();
        if (serie is not null && !ctx.Chamadas.Contains((cursoId, data))) return serie.Motivo;
        return null;
    }

    private static IEnumerable<Item> Aulas(Contexto ctx, DateOnly de, DateOnly ate)
    {
        foreach (var h in ctx.Horarios)
        {
            if (!ctx.Cursos.TryGetValue(h.CursoId, out var curso) || !curso.Ativo) continue;
            var profs = ctx.ProfessorasDoCurso(h.CursoId).ToList();
            for (var d = de; d <= ate; d = d.AddDays(1))
            {
                if ((int)d.DayOfWeek != h.DiaSemana || d < h.ValidoDesde) continue;
                var ini = ParaUtc(d.ToDateTime(h.Inicio));
                var fim = ParaUtc(d.ToDateTime(h.Fim));
                var cancelada = MotivoCancelamentoAula(ctx, h.CursoId, d, ini);
                yield return new Item
                {
                    Id = $"aula:{h.CursoId}:{d:yyyy-MM-dd}:{h.Id}", Fonte = "aula", Tipo = "aula", IniUtc = ini, FimUtc = fim,
                    Ocupa = cancelada is null, DonoId = profs.FirstOrDefault() ?? string.Empty,
                    Participantes = profs.Skip(1).ToHashSet(), Titulo = "Aula: " + curso.Nome, CursoId = h.CursoId, AulaData = d,
                    AulaCancelada = cancelada, AulaComChamada = ctx.Chamadas.Contains((h.CursoId, d)), Sala = h.Sala
                };
            }
        }
    }

    // ---------------- marcos (não ocupam horário) ----------------
    private async Task<List<Item>> MarcosAsync(Contexto ctx, DateTime deUtc, DateTime ateUtc, List<AgendaAgendamento> agendamentos, CancellationToken ct)
    {
        var itens = new List<Item>();
        var deDia = DiaLocal(deUtc);
        var ateDia = DiaLocal(ateUtc);

        // Jurídico: retorno previsto no último atendimento do caso
        var retornos = await _db.AtendimentosJuridicos.AsNoTracking()
            .Where(a => a.DataRetorno != null && a.DataRetorno >= deDia && a.DataRetorno <= ateDia)
            .Select(a => new { a.Id, a.CasoJuridicoId, a.DataRetorno, a.ProfissionalUserId, a.Caso!.AssistidaId, a.CriadoEm })
            .ToListAsync(ct);
        foreach (var r in retornos.GroupBy(r => r.CasoJuridicoId).Select(g => g.OrderByDescending(x => x.CriadoEm).First()))
        {
            itens.Add(new Item
            {
                Id = "retj:" + r.Id, Fonte = "marco", Tipo = "retorno_previsto", IniUtc = InicioDiaUtc(r.DataRetorno!.Value), DiaInteiro = true,
                Ocupa = false, DonoId = r.ProfissionalUserId, Area = AgendaArea.Juridico, AssistidaId = r.AssistidaId, Titulo = "Retorno previsto (anotado no atendimento)"
            });
        }

        // Jurídico: primeiro atendimento marcado pela fila (antes da agenda), se não houver agendamento correspondente
        var filaJur = ctx.Assistidas.Values.Where(a => a.Ativa && a.JuridicoAgendadoPara != null && a.JuridicoEncaminhamentoCanceladoEm == null
            && a.JuridicoAgendadoPara >= deUtc && a.JuridicoAgendadoPara < ateUtc);
        foreach (var a in filaJur)
        {
            if (agendamentos.Any(g => g.Area == AgendaArea.Juridico && g.AssistidaId == a.Id && Math.Abs((g.InicioUtc - a.JuridicoAgendadoPara!.Value).TotalMinutes) < 1)) continue;
            var dono = ctx.Usuarios.Values.FirstOrDefault(u => u.IdentificadorFuncionario == a.JuridicoAgendadoPorIdentificador)?.Id ?? string.Empty;
            itens.Add(new Item
            {
                Id = "filaj:" + a.Id, Fonte = "marco", Tipo = "primeiro", IniUtc = a.JuridicoAgendadoPara!.Value, FimUtc = a.JuridicoAgendadoPara.Value.AddHours(1),
                Ocupa = true, DonoId = dono, Area = AgendaArea.Juridico, AssistidaId = a.Id, Titulo = "Primeiro atendimento jurídico"
            });
        }

        // Psicologia: próxima sessão anotada na última sessão (texto antigo; não substitui o agendamento)
        var proximas = await _db.SessoesPsicologicas.AsNoTracking()
            .Where(s => s.ProximaSessao != null && s.ProximaSessao >= deUtc && s.ProximaSessao < ateUtc)
            .Select(s => new { s.Id, s.ProntuarioPsicologicoId, s.Numero, s.ProximaSessao, s.ProfissionalUserId, s.Prontuario!.AssistidaId })
            .ToListAsync(ct);
        foreach (var s in proximas.GroupBy(s => s.ProntuarioPsicologicoId).Select(g => g.OrderByDescending(x => x.Numero).First()))
        {
            if (agendamentos.Any(g => g.Area == AgendaArea.Psicologia && g.AssistidaId == s.AssistidaId && DiaLocal(g.InicioUtc) == DiaLocal(s.ProximaSessao!.Value))) continue;
            itens.Add(new Item
            {
                Id = "proxp:" + s.Id, Fonte = "marco", Tipo = "proxima_sessao", IniUtc = s.ProximaSessao!.Value, Ocupa = false,
                DonoId = s.ProfissionalUserId, Area = AgendaArea.Psicologia, AssistidaId = s.AssistidaId,
                Titulo = $"Próxima sessão anotada na sessão nº {s.Numero} (agende para confirmar)"
            });
        }

        // Serviço Social: prazo das metas do plano
        var metas = await _db.MetasAcompanhamentoSocial.AsNoTracking()
            .Where(m => m.Prazo != null && m.Prazo >= deDia && m.Prazo <= ateDia && m.Situacao != MetaSocialSituacao.Concluida)
            .Select(m => new { m.Id, m.Prazo, m.CriadaPorUserId, m.Prontuario!.AssistidaId })
            .ToListAsync(ct);
        foreach (var m in metas)
        {
            itens.Add(new Item
            {
                Id = "meta:" + m.Id, Fonte = "marco", Tipo = "prazo_meta", IniUtc = InicioDiaUtc(m.Prazo!.Value), DiaInteiro = true, Ocupa = false,
                DonoId = m.CriadaPorUserId, Area = AgendaArea.ServicoSocial, AssistidaId = m.AssistidaId, Titulo = "Prazo de meta do plano de acompanhamento"
            });
        }

        return itens;
    }

    // ---------------- permissão na origem ----------------
    /// <summary>
    /// A pessoa tem permissão no caso/prontuário de origem? Ter acesso à ficha cadastral não basta.
    /// Acesso de emergência não vale para a agenda (continua só na ficha).
    /// </summary>
    public static bool AcessoOrigem(Contexto ctx, UsuarioAtuante v, string? area, int? assistidaId)
    {
        if (area is null || assistidaId is null) return true;
        var id = assistidaId.Value;
        switch (area)
        {
            case AgendaArea.Juridico:
                if (!AssistidasService.AtuaComoJuridico(v.Perfil)) return false;
                if (!ctx.Casos.TryGetValue(id, out var caso)) return true;
                return caso.Visibilidade == CasoJuridicoVisibilidade.Equipe || caso.Responsaveis.Any(r => r.UserId == v.UserId);
            case AgendaArea.Psicologia:
                if (!AssistidasService.AtuaComoPsicologia(v.Perfil)) return false;
                if (!ctx.ProntuariosPsi.TryGetValue(id, out var psi)) return true;
                return psi.Visibilidade == ProntuarioVisibilidade.Equipe || psi.Responsaveis.Any(r => r.UserId == v.UserId);
            case AgendaArea.ServicoSocial:
                if (!AssistidasService.AtuaComoServicoSocial(v.Perfil)) return false;
                if (!ctx.ProntuariosSoc.TryGetValue(id, out var soc)) return true;
                return soc.Visibilidade == ProntuarioVisibilidade.Equipe || soc.Responsaveis.Any(r => r.UserId == v.UserId);
            default:
                return false;
        }
    }

    public static string? AreaDoPerfil(string perfil) => perfil switch
    {
        PerfisAcesso.Juridico => AgendaArea.Juridico,
        PerfisAcesso.Psicologia => AgendaArea.Psicologia,
        PerfisAcesso.AssistenteSocial => AgendaArea.ServicoSocial,
        _ => null
    };

    /// <summary>Quem já vê curso e situação da assistida (aula aparece pelo nome no conflito).</summary>
    public static bool VeCursoDaAssistida(Contexto ctx, UsuarioAtuante v, int assistidaId)
    {
        if (v.Perfil is PerfisAcesso.Recepcao or PerfisAcesso.Adm) return true;
        if (v.Perfil == PerfisAcesso.Professor)
        {
            var meus = ctx.ProfessoresCursos.Where(p => p.UserId == v.UserId).Select(p => p.CursoId).ToHashSet();
            return ctx.Alunas.Any(a => a.AssistidaId == assistidaId && meus.Contains(a.CursoId));
        }
        var area = AreaDoPerfil(v.Perfil);
        return area is not null && TemOrigem(ctx, area, assistidaId) && AcessoOrigem(ctx, v, area, assistidaId);
    }

    private static bool TemOrigem(Contexto ctx, string area, int assistidaId) => area switch
    {
        AgendaArea.Juridico => ctx.Casos.ContainsKey(assistidaId),
        AgendaArea.Psicologia => ctx.ProntuariosPsi.ContainsKey(assistidaId),
        AgendaArea.ServicoSocial => ctx.ProntuariosSoc.ContainsKey(assistidaId),
        _ => false
    };

    // ---------------- revelação ----------------
    public string Revelar(Contexto ctx, Item it, UsuarioAtuante v)
    {
        if (it.Fonte == "casa") return NivelDetalhe;

        if (it.Fonte == "aula")
        {
            if (v.Perfil == PerfisAcesso.Adm) return NivelDetalhe;
            return ctx.ProfessorasDoCurso(it.CursoId!.Value).Contains(v.UserId) ? NivelDetalhe : NivelCurso;
        }

        if (it.Fonte == "manual")
        {
            var c = it.Compromisso!;
            var minha = it.DonoId == v.UserId || c.RegistradoPorUserId == v.UserId || it.Participantes.Contains(v.UserId);
            // 1º a origem: ter criado o item não mantém acesso a um caso que a pessoa não acessa mais
            if (it.Area is not null && it.AssistidaId is not null && !AcessoOrigem(ctx, v, it.Area, it.AssistidaId))
            {
                return minha ? NivelSemAcesso : (it.Ocupa ? NivelOcupado : NivelOculto);
            }
            if (minha) return NivelDetalhe;
            if (c.Tipo == AgendaTipoManual.Ausencia && v.Perfil == PerfisAcesso.Adm) return NivelDetalhe;
            var inclui = c.Visibilidade == AgendaVisibilidade.Equipe
                || (c.Visibilidade == AgendaVisibilidade.Area && ctx.Perfil(it.DonoId) == v.Perfil)
                || it.Ver.Contains(v.UserId);
            if (inclui) return NivelDetalhe;
            return it.Ocupa ? NivelOcupado : NivelOculto;
        }

        // agendamento ou marco de caso/prontuário
        if (it.Envolve(v.UserId) && !AcessoOrigem(ctx, v, it.Area, it.AssistidaId)) return NivelSemAcesso;
        if (AcessoOrigem(ctx, v, it.Area, it.AssistidaId) && AreaDoPerfil(v.Perfil) == it.Area) return NivelDetalhe;
        if (AcessoOrigem(ctx, v, it.Area, it.AssistidaId) && v.Perfil == PerfisAcesso.Equipe) return NivelDetalhe;
        if (it.Fonte == "marco" && !it.Ocupa) return NivelOculto;
        if (v.Perfil == PerfisAcesso.Adm) return it.Ocupa ? NivelContagem : NivelOculto;
        var a = it.Agendamento;
        if (v.Perfil == PerfisAcesso.Recepcao && a is not null && a.Presencial && a.AvisarRecepcao
            && DiaLocal(a.InicioUtc) == Hoje
            && a.Situacao is AgendaSituacao.Agendado or AgendaSituacao.Realizado)
        {
            return NivelRecepcao;
        }
        return it.Ocupa ? NivelOcupado : NivelOculto;
    }

    /// <summary>Grupo do filtro. "ocupado" e "oculto" nunca entram em filtro: disponibilidade só ao agendar.</summary>
    public static string? Grupo(Item it, UsuarioAtuante v, string nivel)
    {
        if (nivel is NivelOcupado or NivelOculto) return null;
        if (it.Fonte == "casa") return "casa";
        if (it.Fonte == "aula") return nivel == NivelDetalhe ? "turmas" : "outrasTurmas";
        if (nivel == NivelRecepcao) return "esperadas";
        if (nivel == NivelContagem) return "contagem";
        if (nivel == NivelSemAcesso) return "meus";
        if (it.Envolve(v.UserId) || it.Compromisso?.RegistradoPorUserId == v.UserId) return "meus";
        if (it.Fonte == "manual" && (it.Compromisso!.Visibilidade == AgendaVisibilidade.Equipe || it.Tipo == AgendaTipoManual.Ausencia)) return "equipe";
        if (it.Ver.Contains(v.UserId)) return "compartilhados";
        return "area";
    }
}
