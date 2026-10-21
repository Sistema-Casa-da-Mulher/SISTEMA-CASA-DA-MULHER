using CasaMulher.Api.Data;
using CasaMulher.Api.Models;
using CasaMulher.Api.Security;
using Microsoft.EntityFrameworkCore;

namespace CasaMulher.Api.Services;

/// <summary>
/// Cursos cadastrados no banco, vínculos com professoras e assistidas e a migração
/// dos dados antigos (curso gravado como texto na professora e no direcionamento).
/// </summary>
public sealed class CursosService
{
    // Cursos que existiam fixos na Recepção (CURSOS_RECEPCAO): viram registros no primeiro uso.
    public static readonly (string Chave, string Nome)[] CursosPadrao =
    [
        ("Informatica", "Informática e Inclusão Digital"),
        ("Culinaria", "Culinária e Autonomia"),
        ("Estetica", "Estética e Autoestima"),
        ("Primeiros Socorros", "Primeiros Socorros"),
        ("Danca", "Dança Circular"),
        ("Pilates", "Pilates e Bem-estar"),
        ("Empoderamento", "Empoderamento Feminino")
    ];

    private readonly AppDbContext _dbContext;
    private readonly ILogger<CursosService> _logger;

    public CursosService(AppDbContext dbContext, ILogger<CursosService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task GarantirCursosPadraoAsync(CancellationToken cancellationToken = default)
    {
        if (await _dbContext.Cursos.AnyAsync(cancellationToken))
        {
            return;
        }

        var agora = DateTime.UtcNow;
        foreach (var (_, nome) in CursosPadrao)
        {
            _dbContext.Cursos.Add(new Curso { Nome = nome, Ativo = true, CriadoEm = agora, AtualizadoEm = agora });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Acha o curso pelo nome atual ou pela chave antiga da Recepção (ex.: "Culinaria").</summary>
    public static Curso? ResolverCurso(IEnumerable<Curso> cursos, string? texto)
    {
        var alvo = AssistidasService.NormalizarTexto(texto);
        if (alvo.Length == 0) return null;

        var lista = cursos.ToList();
        var direto = lista.FirstOrDefault(c => AssistidasService.NormalizarTexto(c.Nome) == alvo);
        if (direto is not null) return direto;

        var padrao = CursosPadrao.FirstOrDefault(p => AssistidasService.NormalizarTexto(p.Chave) == alvo);
        return padrao.Nome is null
            ? null
            : lista.FirstOrDefault(c => AssistidasService.NormalizarTexto(c.Nome) == AssistidasService.NormalizarTexto(padrao.Nome));
    }

    /// <summary>
    /// Leva os dados antigos para o novo formato. Pode rodar várias vezes: só mexe no que
    /// ainda está no formato antigo (direcionamento em texto, professora sem vínculo).
    /// </summary>
    public async Task MigrarDadosLegadosAsync(CancellationToken cancellationToken = default)
    {
        await GarantirCursosPadraoAsync(cancellationToken);
        var cursos = await _dbContext.Cursos.ToListAsync(cancellationToken);

        var professoras = await _dbContext.Users
            .Where(u => u.ProfessorCurso != null && u.ProfessorCurso != "")
            .ToListAsync(cancellationToken);
        var comVinculo = (await _dbContext.ProfessoresCursos.Select(p => p.UserId).Distinct().ToListAsync(cancellationToken)).ToHashSet();

        foreach (var professora in professoras.Where(p => !comVinculo.Contains(p.Id)))
        {
            foreach (var nome in professora.ProfessorCurso!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var curso = ResolverCurso(cursos, nome);
                if (curso is not null && !_dbContext.ProfessoresCursos.Local.Any(v => v.UserId == professora.Id && v.CursoId == curso.Id))
                {
                    _dbContext.ProfessoresCursos.Add(new ProfessorCurso { UserId = professora.Id, CursoId = curso.Id });
                }
            }
        }

        var legadas = await _dbContext.Assistidas
            .Include(a => a.Cursos)
            .Where(a => a.Direcionamento != null)
            .ToListAsync(cancellationToken);

        foreach (var assistida in legadas)
        {
            var direcionamento = assistida.Direcionamento!;
            var agora = DateTime.UtcNow;

            if (string.Equals(direcionamento, AssistidasService.DirecionamentoJuridico, StringComparison.OrdinalIgnoreCase))
            {
                assistida.EncaminhadaJuridicoEm ??= agora;
            }
            else if (string.Equals(direcionamento, AssistidasService.DirecionamentoPsicologia, StringComparison.OrdinalIgnoreCase))
            {
                assistida.EncaminhadaPsicologiaEm ??= agora;
            }
            else if (string.Equals(direcionamento, "AssistenteSocial", StringComparison.OrdinalIgnoreCase))
            {
                assistida.EncaminhadaAssistenciaSocialEm ??= agora;
            }
            else
            {
                var curso = ResolverCurso(cursos, direcionamento);
                if (curso is null)
                {
                    // Curso desconhecido: cria para não perder o direcionamento já feito.
                    curso = new Curso { Nome = direcionamento.Trim(), Ativo = true };
                    _dbContext.Cursos.Add(curso);
                    cursos.Add(curso);
                }

                if (!assistida.Cursos.Any(c => c.Curso == curso || (curso.Id != 0 && c.CursoId == curso.Id)))
                {
                    assistida.Cursos.Add(new AssistidaCurso { Curso = curso, CriadoEm = assistida.CriadoEm });
                }
            }

            assistida.Direcionamento = null;
        }

        var alterados = await _dbContext.SaveChangesAsync(cancellationToken);
        if (alterados > 0)
        {
            _logger.LogInformation("Migração de cursos: {Total} registro(s) levados ao novo formato.", alterados);
        }
    }

    /// <summary>Atualiza o texto-resumo dos cursos da professora (usado no painel e no e-mail).</summary>
    public async Task AtualizarResumoProfessoraAsync(string userId, CancellationToken cancellationToken = default)
    {
        var usuario = await _dbContext.Users.SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (usuario is null) return;

        var nomes = await _dbContext.ProfessoresCursos
            .Where(p => p.UserId == userId && p.Curso!.Ativo)
            .OrderBy(p => p.Curso!.Nome)
            .Select(p => p.Curso!.Nome)
            .ToListAsync(cancellationToken);

        usuario.ProfessorCurso = nomes.Count == 0 ? null : string.Join(", ", nomes);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Professora vinculada ao curso (EQP só existe em dev/homologação e testa tudo).</summary>
    public async Task<bool> ProfessoraTemCursoAsync(UsuarioAtuante usuario, int cursoId, CancellationToken cancellationToken)
    {
        if (usuario.Perfil is PerfisAcesso.Adm or PerfisAcesso.Equipe)
        {
            return true;
        }

        return await _dbContext.ProfessoresCursos.AnyAsync(p => p.UserId == usuario.UserId && p.CursoId == cursoId, cancellationToken);
    }
}
