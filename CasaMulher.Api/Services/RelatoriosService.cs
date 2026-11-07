using System.Globalization;
using CasaMulher.Api.Data;
using CasaMulher.Api.DTOs;
using CasaMulher.Api.Models;
using CasaMulher.Api.Security;
using Microsoft.EntityFrameworkCore;

namespace CasaMulher.Api.Services;

public sealed record PeriodoRelatorio(string Tipo, string? Ref, DateOnly Ini, DateOnly Fim, string Rotulo)
{
    public bool Dentro(DateOnly? data) => data is not null && data >= Ini && data <= Fim;

    public RelatorioPeriodoDto ParaDto() => new()
    {
        Tipo = Tipo,
        Ref = Ref,
        Ini = Ini.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        Fim = Fim.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        Rotulo = Rotulo
    };
}

/// <summary>Critérios dos pontos de atenção do ADM: exemplos ajustáveis na tela, não regras da Casa.</summary>
public sealed record CriteriosAtencao(int PrazoFila = 10, int FaltasSeguidas = 3, int DiasRetornoVencido = 30, int OcupacaoBaixa = 40)
{
    public static CriteriosAtencao De(int? prazoFila, int? faltasSeguidas, int? diasRetorno, int? ocupacaoBaixa) => new(
        Math.Clamp(prazoFila ?? 10, 1, 365),
        Math.Clamp(faltasSeguidas ?? 3, 1, 50),
        Math.Clamp(diasRetorno ?? 30, 1, 365),
        Math.Clamp(ocupacaoBaixa ?? 40, 1, 100));
}

/// <summary>
/// Relatórios estatísticos e individuais. Todos os números são calculados aqui, com as mesmas
/// regras de sigilo dos módulos: casos restritos, "somente responsáveis", prontuário só da
/// psicóloga responsável, Coordenação só com metadados e números, Recepção sem nada da Psicologia.
/// </summary>
public sealed partial class RelatoriosService
{
    private static readonly CultureInfo PtBr = new("pt-BR");
    private static readonly string[] Meses = ["janeiro", "fevereiro", "março", "abril", "maio", "junho", "julho", "agosto", "setembro", "outubro", "novembro", "dezembro"];

    private readonly AppDbContext _db;

    public RelatoriosService(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>Usado nos testes para fixar o "hoje" dos cálculos.</summary>
    public DateOnly? HojeFixo { get; set; }

    public DateOnly Hoje => HojeFixo ?? Local(DateTime.UtcNow);

    // ------------------------------------------------------------------ perfil

    public static bool PerfilComRelatorio(string perfil) =>
        perfil is PerfisAcesso.Adm or PerfisAcesso.Recepcao or PerfisAcesso.Juridico or PerfisAcesso.Psicologia or PerfisAcesso.Professor;

    /// <summary>
    /// Perfil cujo relatório é montado. A equipe de desenvolvimento (EQP) escolhe a visão para testar;
    /// os demais perfis sempre veem só o próprio.
    /// </summary>
    public static string PerfilRelatorio(UsuarioAtuante usuario, string? visao)
    {
        if (usuario.Perfil == PerfisAcesso.Equipe)
        {
            return visao is not null && PerfilComRelatorio(visao) ? visao : PerfisAcesso.Adm;
        }

        return usuario.Perfil;
    }

    // ------------------------------------------------------------------ datas

    private static TimeZoneInfo? _fuso;

    private static TimeZoneInfo Fuso
    {
        get
        {
            if (_fuso is not null) return _fuso;
            try
            {
                _fuso = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
            }
            catch (TimeZoneNotFoundException)
            {
                _fuso = TimeZoneInfo.CreateCustomTimeZone("BRT", TimeSpan.FromHours(-3), "BRT", "BRT");
            }

            return _fuso;
        }
    }

    public static DateTime HoraLocal(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Fuso);

    public static DateOnly Local(DateTime utc) => DateOnly.FromDateTime(HoraLocal(utc));

    public static DateOnly? Local(DateTime? utc) => utc is null ? null : Local(utc.Value);

    public static DateTime ParaUtc(DateTime local) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), Fuso);

    private static string Fd(DateOnly? d) => d is null ? "-" : d.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    private static string Fdc(DateOnly? d) => d is null ? "-" : d.Value.ToString("dd/MM", CultureInfo.InvariantCulture);

    private static string Iso(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static int Dias(DateOnly de, DateOnly ate) => ate.DayNumber - de.DayNumber;

    public PeriodoRelatorio ResolverPeriodo(string? tipo, string? referencia, DateOnly inicioDados)
    {
        var hoje = Hoje;
        DateOnly Teto(DateOnly d) => d > hoje ? hoje : d;

        switch (tipo)
        {
            case "geral":
                return new PeriodoRelatorio("geral", null, inicioDados > hoje ? hoje : inicioDados, hoje, "Geral (desde o início)");
            case "anual":
            {
                var ano = int.TryParse(referencia, out var a) && a is > 2000 and < 3000 ? a : hoje.Year;
                return new PeriodoRelatorio("anual", ano.ToString(CultureInfo.InvariantCulture), new DateOnly(ano, 1, 1), Teto(new DateOnly(ano, 12, 31)), $"Ano de {ano}");
            }
            case "semestral":
            {
                var partes = (referencia ?? string.Empty).Split('-');
                var ano = partes.Length == 2 && int.TryParse(partes[0], out var a) && a is > 2000 and < 3000 ? a : hoje.Year;
                var sem = partes.Length == 2 && partes[1] == "2" ? 2 : partes.Length == 2 ? 1 : (hoje.Month > 6 ? 2 : 1);
                var ini = new DateOnly(ano, sem == 1 ? 1 : 7, 1);
                var fim = new DateOnly(ano, sem == 1 ? 6 : 12, sem == 1 ? 30 : 31);
                return new PeriodoRelatorio("semestral", $"{ano}-{sem}", ini, Teto(fim), $"{sem}º semestre de {ano}");
            }
            default:
            {
                var ok = DateOnly.TryParseExact((referencia ?? string.Empty) + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var mes);
                var ini = ok ? mes : new DateOnly(hoje.Year, hoje.Month, 1);
                var fim = ini.AddMonths(1).AddDays(-1);
                var rotulo = char.ToUpper(Meses[ini.Month - 1][0], PtBr) + Meses[ini.Month - 1][1..] + $" de {ini.Year}";
                return new PeriodoRelatorio("mensal", ini.ToString("yyyy-MM", CultureInfo.InvariantCulture), ini, Teto(fim), rotulo);
            }
        }
    }

    public PeriodoRelatorio? Anterior(PeriodoRelatorio p)
    {
        return p.Tipo switch
        {
            "mensal" => ResolverPeriodo("mensal", p.Ini.AddMonths(-1).ToString("yyyy-MM", CultureInfo.InvariantCulture), p.Ini),
            "semestral" => ResolverPeriodo("semestral", p.Ini.Month == 1 ? $"{p.Ini.Year - 1}-2" : $"{p.Ini.Year}-1", p.Ini),
            "anual" => ResolverPeriodo("anual", (p.Ini.Year - 1).ToString(CultureInfo.InvariantCulture), p.Ini),
            _ => null
        };
    }

    /// <summary>"Geral" do individual começa na entrada da assistida (ou na entrada no curso, para a professora).</summary>
    private static PeriodoRelatorio DesdeEntrada(PeriodoRelatorio p, DateOnly entrada, string deOnde)
    {
        return p.Tipo == "geral"
            ? p with { Ini = entrada, Rotulo = $"Geral: desde {deOnde} ({Fd(entrada)})" }
            : p;
    }

    // ------------------------------------------------------------------ dados

    internal sealed class Matricula
    {
        public required AssistidaCurso Vinculo { get; init; }
        public required Assistida Assistida { get; init; }
        public required Curso Curso { get; init; }
        public DateOnly Inicio { get; init; }
        public DateOnly? Fim { get; init; }
        public string Situacao => Vinculo.Situacao;

        public bool AtivaEm(DateOnly data) =>
            Situacao != AssistidaCursoSituacao.Aguardando && Inicio <= data && (Fim is null || Fim >= data);

        public bool VinculadaNo(PeriodoRelatorio p) =>
            Situacao != AssistidaCursoSituacao.Aguardando && Inicio <= p.Fim && (Fim is null || Fim >= p.Ini);
    }

    internal sealed record RegistroAula(int AssistidaId, int CursoId, DateOnly Data, string Status, string? Observacao, string? Tema);

    internal sealed record Usuario(string Id, string Nome, string Identificador, string Perfil, string? Crp, bool Ativo);

    internal sealed class Dados
    {
        public List<Assistida> Assistidas { get; init; } = [];
        public Dictionary<int, Assistida> PorId { get; init; } = [];
        public Dictionary<int, Curso> Cursos { get; init; } = [];
        public List<ProfessorCurso> Professoras { get; init; } = [];
        public List<Matricula> Matriculas { get; init; } = [];
        public List<Chamada> Chamadas { get; init; } = [];
        public List<RegistroAula> Registros { get; init; } = [];
        public List<PlanoAula> Planos { get; init; } = [];
        public Dictionary<int, CasoJuridico> Casos { get; init; } = [];
        public Dictionary<int, ProntuarioPsicologico> Prontuarios { get; init; } = [];
        public Dictionary<string, Usuario> Usuarios { get; init; } = [];
        public List<CasoJuridicoAcessoEmergencia> EmergenciasJuridico { get; init; } = [];
        public List<ProntuarioPsicologicoAcessoEmergencia> EmergenciasPsicologia { get; init; } = [];
        public DateOnly InicioDados { get; init; }

        public DateOnly? PrimeiroAtendimentoJuridico(int assistidaId) =>
            Casos.TryGetValue(assistidaId, out var c) && c.Atendimentos.Count > 0 ? Local(c.Atendimentos.Min(a => a.CriadoEm)) : null;

        public DateOnly? PrimeiraSessao(int assistidaId) =>
            Prontuarios.TryGetValue(assistidaId, out var p) && p.Sessoes.Count > 0 ? Local(p.Sessoes.Min(s => s.CriadoEm)) : null;

        public string NomeUsuario(string? userId, string padrao = "-") =>
            userId is not null && Usuarios.TryGetValue(userId, out var u) ? u.Nome : padrao;
    }

    internal async Task<Dados> CarregarAsync(CancellationToken ct)
    {
        var assistidas = await _db.Assistidas.AsNoTracking()
            .Include(a => a.Cursos)
            .Where(a => a.Ativa)
            .ToListAsync(ct);
        var cursos = await _db.Cursos.AsNoTracking().ToDictionaryAsync(c => c.Id, ct);
        var porId = assistidas.ToDictionary(a => a.Id);

        var matriculas = assistidas
            .SelectMany(a => a.Cursos.Where(c => cursos.ContainsKey(c.CursoId)).Select(c => new Matricula
            {
                Vinculo = c,
                Assistida = a,
                Curso = cursos[c.CursoId],
                Inicio = Local(c.CriadoEm),
                Fim = AssistidaCursoSituacao.Encerrada(c.Situacao)
                    ? c.TerminoEm ?? Local(c.SituacaoAlteradaEm ?? c.CriadoEm)
                    : null
            }))
            .ToList();

        var chamadas = await _db.Chamadas.AsNoTracking().Include(c => c.Registros).ToListAsync(ct);
        var registros = chamadas
            .SelectMany(c => c.Registros
                .Where(r => r.Status is "presente" or "faltou" or "justificada" && porId.ContainsKey(r.AssistidaId))
                .Select(r => new RegistroAula(r.AssistidaId, c.CursoId, c.Data, r.Status!, r.Observacao, c.Tema)))
            .OrderBy(r => r.Data)
            .ToList();

        var casos = await _db.CasosJuridicos.AsNoTracking()
            .Include(c => c.Responsaveis)
            .Include(c => c.Atendimentos).ThenInclude(a => a.Adendos)
            .ToListAsync(ct);
        var prontuarios = await _db.ProntuariosPsicologicos.AsNoTracking()
            .Include(p => p.Responsaveis)
            .Include(p => p.Sessoes).ThenInclude(s => s.Adendos)
            .ToListAsync(ct);

        var usuarios = await _db.Users.AsNoTracking()
            .Select(u => new Usuario(u.Id, u.NomeCompleto, u.IdentificadorFuncionario, u.Perfil.ToLower(), u.Crp, u.Ativo))
            .ToListAsync(ct);

        var datas = assistidas.Select(a => a.CriadoEm).ToList();
        var inicio = datas.Count > 0 ? Local(datas.Min()) : Hoje;

        return new Dados
        {
            Assistidas = assistidas,
            PorId = porId,
            Cursos = cursos,
            Professoras = await _db.ProfessoresCursos.AsNoTracking().ToListAsync(ct),
            Matriculas = matriculas,
            Chamadas = chamadas,
            Registros = registros,
            Planos = await _db.PlanosAula.AsNoTracking().ToListAsync(ct),
            Casos = casos.Where(c => porId.ContainsKey(c.AssistidaId)).ToDictionary(c => c.AssistidaId),
            Prontuarios = prontuarios.Where(p => porId.ContainsKey(p.AssistidaId)).ToDictionary(p => p.AssistidaId),
            Usuarios = usuarios.ToDictionary(u => u.Id),
            EmergenciasJuridico = await _db.CasosJuridicosAcessosEmergencia.AsNoTracking().ToListAsync(ct),
            EmergenciasPsicologia = await _db.ProntuariosPsicologicosAcessosEmergencia.AsNoTracking().ToListAsync(ct),
            InicioDados = new DateOnly(inicio.Year, inicio.Month, 1)
        };
    }

    public async Task<DateOnly> InicioDadosAsync(CancellationToken ct)
    {
        var primeira = await _db.Assistidas.AsNoTracking().OrderBy(a => a.CriadoEm).Select(a => (DateTime?)a.CriadoEm).FirstOrDefaultAsync(ct);
        var d = primeira is null ? Hoje : Local(primeira.Value);
        return new DateOnly(d.Year, d.Month, 1);
    }

    // ------------------------------------------------------------------ acesso (mesmas regras dos módulos)

    internal static bool PodeVerConteudoCaso(CasoJuridico caso, UsuarioAtuante usuario, Dados dados, DateTime agora)
    {
        return caso.Visibilidade == CasoJuridicoVisibilidade.Equipe
            || caso.Responsaveis.Any(r => r.UserId == usuario.UserId)
            || dados.EmergenciasJuridico.Any(e => e.CasoJuridicoId == caso.Id && e.UserId == usuario.UserId && e.ExpiraEm > agora);
    }

    internal static bool ResponsavelCaso(CasoJuridico? caso, UsuarioAtuante usuario) =>
        caso is not null && caso.Responsaveis.Any(r => r.UserId == usuario.UserId);

    internal static bool PodeVerConteudoProntuario(ProntuarioPsicologico p, UsuarioAtuante usuario, Dados dados, DateTime agora)
    {
        return p.Visibilidade == ProntuarioVisibilidade.Equipe
            || p.Responsaveis.Any(r => r.UserId == usuario.UserId)
            || dados.EmergenciasPsicologia.Any(e => e.ProntuarioPsicologicoId == p.Id && e.UserId == usuario.UserId && e.ExpiraEm > agora);
    }

    internal static bool ResponsavelProntuario(ProntuarioPsicologico? p, UsuarioAtuante usuario) =>
        p is not null && p.Responsaveis.Any(r => r.UserId == usuario.UserId);

    internal List<int> CursosDaProfessora(UsuarioAtuante usuario, Dados dados) =>
        dados.Professoras.Where(p => p.UserId == usuario.UserId).Select(p => p.CursoId).Distinct().ToList();

    /// <summary>Assistidas que o perfil pode escolher no relatório individual.</summary>
    internal List<Assistida> AssistidasVisiveis(string perfil, UsuarioAtuante usuario, Dados dados)
    {
        IEnumerable<Assistida> lista = perfil switch
        {
            PerfisAcesso.Adm => dados.Assistidas,
            PerfisAcesso.Recepcao => dados.Assistidas.Where(a => !a.AcessoRestrito),
            PerfisAcesso.Juridico => dados.Assistidas.Where(a => a.EncaminhadaJuridicoEm is not null || dados.Casos.ContainsKey(a.Id)),
            PerfisAcesso.Psicologia => dados.Assistidas.Where(a => a.EncaminhadaPsicologiaEm is not null || dados.Prontuarios.ContainsKey(a.Id)),
            PerfisAcesso.Professor => dados.Matriculas
                .Where(m => !m.Assistida.AcessoRestrito && CursosDaProfessora(usuario, dados).Contains(m.Curso.Id))
                .Select(m => m.Assistida)
                .Distinct(),
            _ => []
        };

        return lista.OrderBy(a => a.Nome, StringComparer.Create(PtBr, true)).ToList();
    }

    public async Task<List<RelatorioAssistidaOpcaoDto>> ListarAssistidasAsync(UsuarioAtuante usuario, string perfil, CancellationToken ct)
    {
        var dados = await CarregarAsync(ct);
        return AssistidasVisiveis(perfil, usuario, dados)
            .Select(a => new RelatorioAssistidaOpcaoDto
            {
                Id = a.Id,
                Nome = a.Nome,
                CpfMascarado = AssistidasService.MascararCpf(a.Cpf),
                Entrada = Iso(Local(a.CriadoEm))
            })
            .ToList();
    }

    // ------------------------------------------------------------------ peças

    private static string N(double v) => v.ToString("N0", PtBr);

    private static int Pct(double a, double b) => b > 0 ? (int)Math.Round(a * 100 / b, MidpointRounding.AwayFromZero) : 0;

    private static RelatorioKpiDto Kpi(string valor, string rotulo, string? ajuda = null, double? num = null, double? anterior = null, string? sufixoDelta = null, bool alerta = false, string? titulo = null, bool dadoNovo = false) => new()
    {
        Valor = valor,
        Rotulo = rotulo,
        Ajuda = ajuda,
        ValorNum = anterior is null ? null : num,
        Anterior = anterior,
        SufixoDelta = sufixoDelta,
        Alerta = alerta,
        Titulo = titulo,
        DadoNovo = dadoNovo
    };

    private static RelatorioKpiDto KpiN(int valor, string rotulo, int? anterior = null, string? ajuda = null) =>
        Kpi(N(valor), rotulo, ajuda, valor, anterior);

    private static RelatorioItemDto Item(string rotulo, double valor, string? cor = null, string? extra = null) =>
        new() { Rotulo = rotulo, Valor = valor, Cor = cor, Extra = extra };

    private static RelatorioBlocoDto Barras(string titulo, string? nota, IEnumerable<RelatorioItemDto> itens, bool largo = false, string? sufixo = null, double? max = null) => new()
    {
        Tipo = "barras", Titulo = titulo, Nota = nota, Itens = itens.ToList(), Largo = largo, Sufixo = sufixo, Max = max
    };

    private static RelatorioBlocoDto Colunas(string titulo, string? nota, IEnumerable<RelatorioItemDto> itens, string? cor = null, string? sufixo = null, bool largo = false) => new()
    {
        Tipo = "colunas", Titulo = titulo, Nota = nota, Itens = itens.ToList(), Cor = cor, Sufixo = sufixo, Largo = largo
    };

    private static RelatorioBlocoDto Pilha(string titulo, string? nota, IEnumerable<RelatorioItemDto> itens, string? texto = null) => new()
    {
        Tipo = "pilha", Titulo = titulo, Nota = nota, Itens = itens.ToList(), Texto = texto
    };

    private static RelatorioBlocoDto Tabela(string titulo, string? nota, IEnumerable<RelatorioColunaDto> colunas, IEnumerable<List<RelatorioCelulaDto>> linhas, bool largo = false, string? vazio = null) => new()
    {
        Tipo = "tabela", Titulo = titulo, Nota = nota, Colunas = colunas.ToList(), Linhas = linhas.ToList(), Largo = largo, Vazio = vazio
    };

    private static RelatorioBlocoDto Texto(string titulo, string? nota, string texto, string tipo = "texto", bool largo = false) => new()
    {
        Tipo = tipo, Titulo = titulo, Nota = nota, Texto = texto, Largo = largo
    };

    private static RelatorioBlocoDto Kpis(string titulo, string? nota, params RelatorioKpiDto[] kpis) => new()
    {
        Tipo = "kpis", Titulo = titulo, Nota = nota, Kpis = kpis.ToList()
    };

    private static RelatorioColunaDto Col(string t, bool num = false, bool novo = false) => new() { T = t, Num = num, DadoNovo = novo };

    private static RelatorioCelulaDto C(string texto, string? badge = null, string? sub = null, bool destaque = false) =>
        new() { Texto = texto, Badge = badge, Sub = sub, Destaque = destaque };

    private static RelatorioCelulaDto C(int valor) => new() { Texto = N(valor) };

    private static List<RelatorioCelulaDto> Linha(params RelatorioCelulaDto[] celulas) => celulas.ToList();

    private static RelatorioBadgeDto Badge(string texto, string? tipo = null) => new() { Texto = texto, Tipo = tipo };

    /// <summary>Conta valores (um item pode cair em mais de uma categoria) e ordena do maior para o menor.</summary>
    private static List<RelatorioItemDto> Contar<T>(IEnumerable<T> lista, Func<T, IEnumerable<string?>> chaves)
    {
        return lista
            .SelectMany(x => chaves(x).Where(k => !string.IsNullOrWhiteSpace(k)).Distinct())
            .GroupBy(k => k!)
            .Select(g => Item(g.Key, g.Count()))
            .OrderByDescending(i => i.Valor)
            .ThenBy(i => i.Rotulo, StringComparer.Create(PtBr, true))
            .ToList();
    }

    private static List<RelatorioItemDto> Contar<T>(IEnumerable<T> lista, Func<T, string?> chave) =>
        Contar(lista, x => new[] { chave(x) });

    /// <summary>Colunas mês a mês. No mensal mostra os últimos 6 meses até o escolhido.</summary>
    private static List<RelatorioItemDto> PorMes(PeriodoRelatorio p, IEnumerable<DateOnly> datas)
    {
        var ini = p.Tipo == "mensal" ? new DateOnly(p.Ini.Year, p.Ini.Month, 1).AddMonths(-5) : new DateOnly(p.Ini.Year, p.Ini.Month, 1);
        var lista = datas.ToList();
        var itens = new List<RelatorioItemDto>();
        for (var m = ini; m <= p.Fim; m = m.AddMonths(1))
        {
            var fimMes = m.AddMonths(1).AddDays(-1);
            itens.Add(Item($"{Meses[m.Month - 1][..3]}/{m.Year % 100:D2}", lista.Count(d => d >= m && d <= fimMes)));
        }

        return itens;
    }

    private static string NotaMes(PeriodoRelatorio p) =>
        p.Tipo == "mensal" ? "Últimos 6 meses até o mês escolhido." : "Mês a mês dentro do período.";

    private static int? Idade(DateOnly? nascimento, DateOnly em)
    {
        if (nascimento is null) return null;
        var idade = em.Year - nascimento.Value.Year;
        if (em < nascimento.Value.AddYears(idade)) idade--;
        return idade;
    }

    private static List<RelatorioItemDto> FaixaEtaria(IEnumerable<Assistida> lista)
    {
        var faixas = new (string Rotulo, int De, int Ate)[] { ("Até 17", 0, 17), ("18 a 24", 18, 24), ("25 a 34", 25, 34), ("35 a 44", 35, 44), ("45 a 59", 45, 59), ("60 ou mais", 60, 200) };
        var idades = lista.Select(a => Idade(a.DataNascimento, Local(a.CriadoEm))).ToList();
        var itens = faixas.Select(f => Item(f.Rotulo, idades.Count(i => i >= f.De && i <= f.Ate))).Where(i => i.Rotulo != "Até 17" || i.Valor > 0).ToList();
        var sem = idades.Count(i => i is null);
        if (sem > 0) itens.Add(Item("Sem data de nascimento", sem, "neutro"));
        return itens;
    }

    private sealed record Frequencia(int Presente, int Justificada, int Faltou)
    {
        public int Total => Presente + Justificada + Faltou;
        public int Pct => RelatoriosService.Pct(Presente, Total);
    }

    private static Frequencia Freq(IEnumerable<RegistroAula> registros)
    {
        var lista = registros.ToList();
        return new Frequencia(lista.Count(r => r.Status == "presente"), lista.Count(r => r.Status == "justificada"), lista.Count(r => r.Status == "faltou"));
    }

    /// <summary>Faltas seguidas contando da aula mais recente para trás, até a última presença.</summary>
    private static int SequenciaFaltas(IEnumerable<RegistroAula> registrosOrdenados)
    {
        var seq = 0;
        foreach (var r in registrosOrdenados.OrderByDescending(r => r.Data))
        {
            if (r.Status == "presente") break;
            seq++;
        }

        return seq;
    }

    private static RelatorioItemDto[] PilhaFrequencia(Frequencia f) =>
    [
        Item("Presente", f.Presente, "ok"),
        Item("Falta justificada", f.Justificada, "justificada"),
        Item("Faltou", f.Faltou, "falta")
    ];

    private static readonly Dictionary<string, string> RotulosSituacaoCurso = new()
    {
        [AssistidaCursoSituacao.Aguardando] = "Aguardando vaga",
        [AssistidaCursoSituacao.Cursando] = "Cursando",
        [AssistidaCursoSituacao.Concluiu] = "Concluído",
        [AssistidaCursoSituacao.Interrompido] = "Interrompido"
    };

    private static RelatorioCelulaDto CelulaSituacaoCurso(string situacao) => C(
        RotulosSituacaoCurso.GetValueOrDefault(situacao, situacao),
        situacao switch
        {
            AssistidaCursoSituacao.Concluiu => "ok",
            AssistidaCursoSituacao.Interrompido => "restrito",
            AssistidaCursoSituacao.Cursando => "psicologia",
            _ => "neutro"
        });

    /// <summary>Para a Coordenação: só categorias da lista; texto livre vira "Outro".</summary>
    private static string MotivoCategoria(string? motivo)
    {
        if (string.IsNullOrWhiteSpace(motivo)) return "Sem motivo registrado";
        return AssistidaCursoSituacao.MotivosSugeridos.FirstOrDefault(m => string.Equals(m, motivo.Trim(), StringComparison.OrdinalIgnoreCase)) ?? "Outro";
    }

    internal static readonly Dictionary<string, string> RotulosDemanda = new()
    {
        ["medida_protetiva"] = "Medida protetiva",
        ["divorcio"] = "Divórcio",
        ["guarda"] = "Guarda",
        ["pensao"] = "Pensão alimentícia",
        ["partilha"] = "Partilha de bens",
        ["violencia_patrimonial"] = "Violência patrimonial",
        ["outro"] = "Outro"
    };

    internal static readonly Dictionary<string, string> RotulosEncJuridico = new()
    {
        ["defensoria"] = "Defensoria Pública",
        ["delegacia_mulher"] = "Delegacia da Mulher",
        ["creas"] = "CREAS",
        ["conselho_tutelar"] = "Conselho Tutelar",
        ["outro"] = "Outro"
    };

    internal static readonly Dictionary<string, string> RotulosSituacaoCaso = new()
    {
        [CasoJuridicoSituacao.EmAndamento] = "Em andamento",
        [CasoJuridicoSituacao.AguardandoDocumento] = "Aguardando documento",
        [CasoJuridicoSituacao.Encerrado] = "Encerrado"
    };

    internal static readonly Dictionary<string, string> RotulosPresenca = new()
    {
        ["compareceu"] = "Compareceu",
        ["faltou_justificou"] = "Faltou e justificou",
        ["faltou"] = "Faltou",
        ["remarcou"] = "Remarcou"
    };

    internal static readonly Dictionary<string, string> RotulosTipoSessao = new()
    {
        ["acolhimento"] = "Acolhimento",
        ["individual"] = "Individual",
        ["crise"] = "Atendimento de crise"
    };

    internal static readonly Dictionary<string, string> RotulosModalidade = new()
    {
        ["presencial"] = "Presencial",
        ["online"] = "On-line",
        ["telefone"] = "Telefone"
    };

    internal static readonly Dictionary<string, string> RotulosEncPsicologia = new()
    {
        ["caps"] = "CAPS",
        ["ubs"] = "UBS",
        ["cras_creas"] = "CRAS/CREAS",
        ["abrigo"] = "Abrigo",
        ["outro"] = "Outro"
    };

    internal static readonly Dictionary<string, string> RotulosEncerramento = new()
    {
        ["alta"] = "Alta",
        ["desistencia"] = "Interrompido pela assistida",
        ["encaminhamento_externo"] = "Encaminhamento externo",
        ["mudanca_cidade"] = "Mudança de cidade",
        ["outro"] = "Outro"
    };

    internal static readonly string[] OrdemRisco = ["baixo", "moderado", "alto", "iminente"];

    internal static string RotuloRisco(string? risco) =>
        string.IsNullOrWhiteSpace(risco) ? "-" : char.ToUpper(risco[0], PtBr) + risco[1..];

    private static IEnumerable<string> Separar(string? csv, Dictionary<string, string> rotulos) =>
        string.IsNullOrWhiteSpace(csv)
            ? []
            : csv.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(k => rotulos.GetValueOrDefault(k.Trim(), k.Trim()));

    private static string RotuloContato(string contato) => contato switch
    {
        AssistidaContatoSeguro.NaoLigar => "Não ligar",
        AssistidaContatoSeguro.Horario => "Só no horário combinado",
        _ => "Pode ligar"
    };

    private static RelatorioBlocoDto BlocoContato(string titulo, string? nota, List<Assistida> lista) => Pilha(titulo, nota,
        [
            Item("Pode ligar", lista.Count(a => !a.SemTelefoneSeguro && a.ContatoSeguro == AssistidaContatoSeguro.PodeLigar), "ok"),
            Item("Só no horário combinado", lista.Count(a => !a.SemTelefoneSeguro && a.ContatoSeguro == AssistidaContatoSeguro.Horario), "justificada"),
            Item("Não ligar", lista.Count(a => a.SemTelefoneSeguro || a.ContatoSeguro == AssistidaContatoSeguro.NaoLigar), "falta")
        ],
        $"Sem telefone seguro: {lista.Count(a => a.SemTelefoneSeguro)} · Endereço sigiloso: {lista.Count(a => a.EnderecoSigiloso)}");

    private static string RotuloOrigemPara(string origem, string perfilViewer) => origem switch
    {
        AssistidaOrigem.Recepcao => "Recepção",
        AssistidaOrigem.Juridico => perfilViewer == PerfisAcesso.Juridico ? "Cadastro direto no Jurídico" : "Direto no Jurídico",
        AssistidaOrigem.Psicologia => perfilViewer is PerfisAcesso.Psicologia ? "Cadastro direto na Psicologia"
            : perfilViewer == PerfisAcesso.Adm ? "Direto na Psicologia" : "Equipe técnica",
        _ => origem
    };

    /// <summary>Situação operacional do encaminhamento ao Jurídico (o que a Recepção pode ver).</summary>
    internal static (string Rotulo, string Chave, string? Badge) SituacaoOperacionalJuridico(Assistida a, Dados dados, DateOnly ate)
    {
        var cancelado = Local(a.JuridicoEncaminhamentoCanceladoEm);
        var primeiro = dados.PrimeiroAtendimentoJuridico(a.Id);
        var agendadoEm = Local(a.JuridicoAgendadoEm);

        if (cancelado is not null && cancelado <= ate && (primeiro is null || primeiro > ate))
            return ($"Cancelado em {Fd(cancelado)}", "Cancelado", "restrito");
        if (primeiro is not null && primeiro <= ate)
            return ($"Primeiro atendimento realizado em {Fd(primeiro)}", "Primeiro atendimento realizado", "ok");
        if (a.JuridicoAgendadoPara is not null && (agendadoEm is null || agendadoEm <= ate))
        {
            var quando = HoraLocal(a.JuridicoAgendadoPara.Value);
            return ($"Agendado para {quando:dd/MM/yyyy} às {quando:HH:mm}", "Agendado", "juridico");
        }

        return ("Aguardando atendimento", "Aguardando atendimento", null);
    }

    /// <summary>Encaminhada ao Jurídico que ainda não teve o 1º atendimento até a data (sem cancelamento).</summary>
    private static bool AguardandoJuridico(Assistida a, Dados dados, DateOnly ate)
    {
        var enc = Local(a.EncaminhadaJuridicoEm);
        if (enc is null || enc > ate) return false;
        var primeiro = dados.PrimeiroAtendimentoJuridico(a.Id);
        if (primeiro is not null && primeiro <= ate) return false;
        var cancelado = Local(a.JuridicoEncaminhamentoCanceladoEm);
        return cancelado is null || cancelado > ate;
    }

    private static bool AguardandoPsicologia(Assistida a, Dados dados, DateOnly ate)
    {
        var enc = Local(a.EncaminhadaPsicologiaEm);
        if (enc is null || enc > ate) return false;
        var primeira = dados.PrimeiraSessao(a.Id);
        return primeira is null || primeira > ate;
    }

    private static string? UltimaSituacaoCaso(CasoJuridico caso, DateOnly ate)
    {
        return caso.Atendimentos.Where(a => Local(a.CriadoEm) <= ate).OrderBy(a => a.CriadoEm).LastOrDefault()?.Situacao;
    }

    private static string? RiscoAte(ProntuarioPsicologico p, DateOnly ate)
    {
        return p.Sessoes.Where(s => !string.IsNullOrWhiteSpace(s.Risco) && Local(s.CriadoEm) <= ate).OrderBy(s => s.CriadoEm).LastOrDefault()?.Risco;
    }

    private static bool ProntuarioAtivoEm(ProntuarioPsicologico p, DateOnly data) =>
        p.Sessoes.Count > 0 && Local(p.Sessoes.Min(s => s.CriadoEm)) <= data
        && (p.EncerradoEm is null || Local(p.EncerradoEm.Value) > data);

    private static string Rodape(UsuarioAtuante usuario, DateTime agora) =>
        $"Gerado por {usuario.Nome} ({usuario.Identificador}) em {HoraLocal(agora):dd/MM/yyyy} às {HoraLocal(agora):HH:mm}. Documento de uso interno da Casa da Mulher, com dados protegidos pela LGPD. A impressão deste relatório fica registrada no histórico de ações.";
}
