using System.Globalization;
using System.Text.Json;
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
/// Cursos da Casa: cadastro pelo ADM, vínculo com professoras e, para a professora,
/// alunas, plano de aula e chamada do curso (antes ficavam só no navegador).
/// Plano e chamada pertencem ao curso, então seguem quem assumir a turma.
/// </summary>
[ApiController]
[Route("api/cursos")]
[Authorize]
public class CursosController : ControllerBase
{
    private static readonly string[] StatusChamada = ["presente", "faltou", "justificada"];

    private static readonly Dictionary<string, string[]> AtividadesPadrao = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Informática e Inclusão Digital"] = ["Teclado", "Mouse", "Área de trabalho", "Digitação", "Navegador", "E-mail", "Pesquisa na internet", "Segurança digital"]
    };

    private readonly AppDbContext _dbContext;
    private readonly AssistidasService _assistidas;
    private readonly CursosService _cursos;
    private readonly IAuditoriaService _auditoria;
    private readonly SecuritySnapshotPersistenceService _snapshot;

    public CursosController(
        AppDbContext dbContext,
        AssistidasService assistidas,
        CursosService cursos,
        IAuditoriaService auditoria,
        SecuritySnapshotPersistenceService snapshot)
    {
        _dbContext = dbContext;
        _assistidas = assistidas;
        _cursos = cursos;
        _auditoria = auditoria;
        _snapshot = snapshot;
    }

    /// <summary>Lista para o direcionamento da Recepção. Com todos=true (ADM), inclui os desativados e os totais.</summary>
    [HttpGet]
    public async Task<ActionResult<List<CursoResponse>>> Listar([FromQuery] bool todos, CancellationToken cancellationToken)
    {
        var usuario = await _assistidas.ObterUsuarioAsync(User, cancellationToken);
        if (usuario is null) return Unauthorized();

        await _cursos.GarantirCursosPadraoAsync(cancellationToken);
        var incluirInativos = todos && usuario.Perfil is PerfisAcesso.Adm or PerfisAcesso.Equipe;

        var cursos = await _dbContext.Cursos.AsNoTracking()
            .Where(c => incluirInativos || c.Ativo)
            .OrderBy(c => c.Nome)
            .ToListAsync(cancellationToken);

        var alunas = await _dbContext.AssistidasCursos.AsNoTracking()
            .Where(a => a.Assistida!.Ativa)
            .GroupBy(a => a.CursoId).Select(g => new { g.Key, Total = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Total, cancellationToken);
        var professoras = await _dbContext.ProfessoresCursos.AsNoTracking()
            .GroupBy(p => p.CursoId).Select(g => new { g.Key, Total = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Total, cancellationToken);
        var comHistorico = await CursosComHistoricoAsync(cancellationToken);

        return Ok(cursos.Select(c => new CursoResponse
        {
            Id = c.Id,
            Nome = c.Nome,
            Ativo = c.Ativo,
            TotalAlunas = incluirInativos ? alunas.GetValueOrDefault(c.Id) : 0,
            TotalProfessoras = incluirInativos ? professoras.GetValueOrDefault(c.Id) : 0,
            PodeApagar = incluirInativos && !comHistorico.Contains(c.Id)
        }).ToList());
    }

    [HttpPost]
    [Authorize(Policy = PoliticasAcesso.SomenteAdm)]
    public async Task<ActionResult<CursoResponse>> Criar(SalvarCursoRequest request, CancellationToken cancellationToken)
    {
        var nome = request.Nome?.Trim() ?? string.Empty;
        var erro = await ValidarNomeAsync(nome, null, cancellationToken);
        if (erro is not null) return BadRequest(new { mensagem = erro });

        var curso = new Curso { Nome = nome };
        _dbContext.Cursos.Add(curso);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _auditoria.RegistrarAsync("CURSO_CRIADO", "Curso", curso.Id.ToString(CultureInfo.InvariantCulture), $"Criou o curso \"{nome}\".");
        await _snapshot.PersistAsync("curso_criado", CancellationToken.None);

        return Ok(new CursoResponse { Id = curso.Id, Nome = curso.Nome, Ativo = true, PodeApagar = true });
    }

    /// <summary>Renomear mantém o mesmo curso: alunas, professoras e aulas continuam ligadas.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Policy = PoliticasAcesso.SomenteAdm)]
    public async Task<IActionResult> Renomear(int id, SalvarCursoRequest request, CancellationToken cancellationToken)
    {
        var curso = await _dbContext.Cursos.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (curso is null) return NotFound(new { mensagem = "Curso não encontrado." });

        var nome = request.Nome?.Trim() ?? string.Empty;
        var erro = await ValidarNomeAsync(nome, id, cancellationToken);
        if (erro is not null) return BadRequest(new { mensagem = erro });

        var anterior = curso.Nome;
        curso.Nome = nome;
        curso.AtualizadoEm = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        await AtualizarResumoProfessorasDoCursoAsync(id, cancellationToken);

        await _auditoria.RegistrarAsync("CURSO_RENOMEADO", "Curso", id.ToString(CultureInfo.InvariantCulture), $"Renomeou o curso \"{anterior}\" para \"{nome}\".");
        await _snapshot.PersistAsync("curso_renomeado", CancellationToken.None);
        return Ok(new { mensagem = "Curso renomeado." });
    }

    [HttpPost("{id:int}/desativar")]
    [Authorize(Policy = PoliticasAcesso.SomenteAdm)]
    public Task<IActionResult> Desativar(int id, CancellationToken cancellationToken) => AlterarAtivoAsync(id, false, cancellationToken);

    [HttpPost("{id:int}/reativar")]
    [Authorize(Policy = PoliticasAcesso.SomenteAdm)]
    public Task<IActionResult> Reativar(int id, CancellationToken cancellationToken) => AlterarAtivoAsync(id, true, cancellationToken);

    /// <summary>Só apaga curso que nunca teve aluna, professora, plano ou chamada.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = PoliticasAcesso.SomenteAdm)]
    public async Task<IActionResult> Apagar(int id, CancellationToken cancellationToken)
    {
        var curso = await _dbContext.Cursos.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (curso is null) return NotFound(new { mensagem = "Curso não encontrado." });

        if ((await CursosComHistoricoAsync(cancellationToken)).Contains(id))
        {
            return Conflict(new { mensagem = "Este curso já tem alunas, professoras ou aulas registradas. Desative em vez de apagar, para não perder o histórico." });
        }

        _dbContext.Cursos.Remove(curso);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _auditoria.RegistrarAsync("CURSO_APAGADO", "Curso", id.ToString(CultureInfo.InvariantCulture), $"Apagou o curso \"{curso.Nome}\" (sem histórico).");
        await _snapshot.PersistAsync("curso_apagado", CancellationToken.None);
        return Ok(new { mensagem = "Curso apagado." });
    }

    [HttpGet("professoras")]
    [Authorize(Policy = PoliticasAcesso.SomenteAdm)]
    public async Task<ActionResult<List<ProfessoraCursosResponse>>> ListarProfessoras(CancellationToken cancellationToken)
    {
        var professoras = await _dbContext.Users.AsNoTracking()
            .Where(u => u.Perfil == PerfisAcesso.Professor)
            .OrderBy(u => u.NomeCompleto)
            .ToListAsync(cancellationToken);

        var vinculos = await _dbContext.ProfessoresCursos.AsNoTracking()
            .Include(p => p.Curso)
            .ToListAsync(cancellationToken);

        return Ok(professoras.Select(p => new ProfessoraCursosResponse
        {
            UserId = p.Id,
            Nome = p.NomeCompleto,
            Identificador = p.IdentificadorFuncionario,
            Ativo = p.Ativo,
            Cursos = vinculos.Where(v => v.UserId == p.Id)
                .OrderBy(v => v.Curso!.Nome)
                .Select(v => new CursoResumoResponse { Id = v.CursoId, Nome = v.Curso!.Nome, Ativo = v.Curso.Ativo })
                .ToList()
        }).ToList());
    }

    [HttpPut("professoras/{userId}")]
    [Authorize(Policy = PoliticasAcesso.SomenteAdm)]
    public async Task<IActionResult> DefinirCursosProfessora(string userId, DefinirCursosProfessoraRequest request, CancellationToken cancellationToken)
    {
        var professora = await _dbContext.Users.SingleOrDefaultAsync(u => u.Id == userId && u.Perfil == PerfisAcesso.Professor, cancellationToken);
        if (professora is null) return NotFound(new { mensagem = "Professora não encontrada." });

        var ids = request.CursoIds.Distinct().ToList();
        var validos = await _dbContext.Cursos.Where(c => ids.Contains(c.Id) && c.Ativo).Select(c => c.Id).ToListAsync(cancellationToken);
        if (validos.Count != ids.Count) return BadRequest(new { mensagem = "Escolha apenas cursos ativos." });

        var atuais = await _dbContext.ProfessoresCursos.Where(p => p.UserId == userId).ToListAsync(cancellationToken);
        // Cursos desativados continuam ligados (histórico); só os ativos são editados aqui.
        var ativosAtuais = await _dbContext.Cursos.Where(c => c.Ativo).Select(c => c.Id).ToListAsync(cancellationToken);
        _dbContext.ProfessoresCursos.RemoveRange(atuais.Where(a => ativosAtuais.Contains(a.CursoId) && !ids.Contains(a.CursoId)));
        foreach (var id in ids.Where(id => atuais.All(a => a.CursoId != id)))
        {
            _dbContext.ProfessoresCursos.Add(new ProfessorCurso { UserId = userId, CursoId = id });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await _cursos.AtualizarResumoProfessoraAsync(userId, cancellationToken);

        await _auditoria.RegistrarAsync(
            "PROFESSORA_CURSOS_ALTERADOS",
            "ApplicationUser",
            userId,
            $"Definiu os cursos de {professora.IdentificadorFuncionario}: {(ids.Count == 0 ? "nenhum" : string.Join(", ", ids))}.");
        await _snapshot.PersistAsync("professora_cursos", CancellationToken.None);
        return Ok(new { mensagem = "Cursos da professora atualizados." });
    }

    [HttpGet("meus")]
    [Authorize(Policy = PoliticasAcesso.AcessoCursos)]
    public async Task<ActionResult<List<CursoResumoResponse>>> MeusCursos(CancellationToken cancellationToken)
    {
        var usuario = await _assistidas.ObterUsuarioAsync(User, cancellationToken);
        if (usuario is null) return Unauthorized();

        await _cursos.MigrarDadosLegadosAsync(cancellationToken);
        var query = usuario.Perfil is PerfisAcesso.Adm or PerfisAcesso.Equipe
            ? _dbContext.Cursos.AsNoTracking().Where(c => c.Ativo)
            : _dbContext.ProfessoresCursos.AsNoTracking().Where(p => p.UserId == usuario.UserId && p.Curso!.Ativo).Select(p => p.Curso!);

        return Ok(await query.OrderBy(c => c.Nome)
            .Select(c => new CursoResumoResponse { Id = c.Id, Nome = c.Nome, Ativo = c.Ativo })
            .ToListAsync(cancellationToken));
    }

    /// <summary>Alunas do curso. Restritas nunca aparecem; telefone some de quem pediu para não receber ligação.</summary>
    [HttpGet("{id:int}/alunas")]
    [Authorize(Policy = PoliticasAcesso.AcessoCursos)]
    public async Task<ActionResult<List<AlunaCursoResponse>>> Alunas(int id, CancellationToken cancellationToken)
    {
        var usuario = await _assistidas.ObterUsuarioAsync(User, cancellationToken);
        if (usuario is null) return Unauthorized();
        if (!await _cursos.ProfessoraTemCursoAsync(usuario, id, cancellationToken)) return ForbidCurso();

        return Ok(await CarregarAlunasAsync(id, cancellationToken));
    }

    [HttpGet("{id:int}/aulas")]
    [Authorize(Policy = PoliticasAcesso.AcessoCursos)]
    public async Task<ActionResult<AulasCursoResponse>> Aulas(int id, CancellationToken cancellationToken)
    {
        var usuario = await _assistidas.ObterUsuarioAsync(User, cancellationToken);
        if (usuario is null) return Unauthorized();
        if (!await _cursos.ProfessoraTemCursoAsync(usuario, id, cancellationToken)) return ForbidCurso();

        var curso = await _dbContext.Cursos.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (curso is null) return NotFound(new { mensagem = "Curso não encontrado." });

        var planos = await _dbContext.PlanosAula.AsNoTracking().Where(p => p.CursoId == id).OrderBy(p => p.Data).ToListAsync(cancellationToken);
        var chamadas = await _dbContext.Chamadas.AsNoTracking().Include(c => c.Registros).Where(c => c.CursoId == id).OrderBy(c => c.Data).ToListAsync(cancellationToken);
        var idsRegistrados = chamadas.SelectMany(c => c.Registros).Select(r => r.AssistidaId).Distinct().ToList();
        var nomes = await _dbContext.Assistidas.AsNoTracking()
            .Where(a => idsRegistrados.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => a.Nome, cancellationToken);

        var atividades = new List<string>(AtividadesPadrao.GetValueOrDefault(curso.Nome) ?? []);
        foreach (var lista in planos.Select(p => p.AtividadesJson).Concat(chamadas.Select(c => c.AtividadesJson)))
        {
            foreach (var atividade in LerLista(lista))
            {
                if (!atividades.Any(a => AssistidasService.NormalizarTexto(a) == AssistidasService.NormalizarTexto(atividade)))
                {
                    atividades.Add(atividade);
                }
            }
        }

        return Ok(new AulasCursoResponse
        {
            Curso = new CursoResumoResponse { Id = curso.Id, Nome = curso.Nome, Ativo = curso.Ativo },
            Planos = planos.Select(MapearPlano).ToList(),
            Chamadas = chamadas.Select(c => new ChamadaResponse
            {
                Id = c.Id,
                Data = c.Data.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Tema = c.Tema,
                PlanoAulaId = c.PlanoAulaId,
                Atividades = LerLista(c.AtividadesJson),
                RegistradaPorNome = c.RegistradaPorNome,
                Registros = c.Registros.Select(r => new ChamadaRegistroDto
                {
                    AssistidaId = r.AssistidaId,
                    Nome = nomes.GetValueOrDefault(r.AssistidaId),
                    Status = r.Status,
                    Observacao = r.Observacao
                }).ToList()
            }).ToList(),
            AtividadesSugeridas = atividades
        });
    }

    [HttpPost("{id:int}/planos")]
    [Authorize(Policy = PoliticasAcesso.AcessoCursos)]
    public Task<ActionResult<PlanoAulaResponse>> CriarPlano(int id, SalvarPlanoAulaRequest request, CancellationToken cancellationToken)
        => SalvarPlanoAsync(id, null, request, cancellationToken);

    [HttpPut("{id:int}/planos/{planoId:int}")]
    [Authorize(Policy = PoliticasAcesso.AcessoCursos)]
    public Task<ActionResult<PlanoAulaResponse>> AtualizarPlano(int id, int planoId, SalvarPlanoAulaRequest request, CancellationToken cancellationToken)
        => SalvarPlanoAsync(id, planoId, request, cancellationToken);

    [HttpDelete("{id:int}/planos/{planoId:int}")]
    [Authorize(Policy = PoliticasAcesso.AcessoCursos)]
    public async Task<IActionResult> ExcluirPlano(int id, int planoId, CancellationToken cancellationToken)
    {
        var usuario = await _assistidas.ObterUsuarioAsync(User, cancellationToken);
        if (usuario is null) return Unauthorized();
        if (!await _cursos.ProfessoraTemCursoAsync(usuario, id, cancellationToken)) return ForbidCurso();

        var plano = await _dbContext.PlanosAula.SingleOrDefaultAsync(p => p.Id == planoId && p.CursoId == id, cancellationToken);
        if (plano is null) return NotFound(new { mensagem = "Aula não encontrada." });

        // A chamada que apontava para esta aula continua, só perde o vínculo.
        var chamadas = await _dbContext.Chamadas.Where(c => c.PlanoAulaId == planoId).ToListAsync(cancellationToken);
        chamadas.ForEach(c => c.PlanoAulaId = null);
        _dbContext.PlanosAula.Remove(plano);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _snapshot.PersistAsync("plano_aula_excluido", CancellationToken.None);
        return Ok(new { mensagem = "Aula excluída do plano." });
    }

    [HttpPut("{id:int}/chamadas/{data}")]
    [Authorize(Policy = PoliticasAcesso.AcessoCursos)]
    public async Task<ActionResult<ChamadaResponse>> SalvarChamada(int id, string data, SalvarChamadaRequest request, CancellationToken cancellationToken)
    {
        var usuario = await _assistidas.ObterUsuarioAsync(User, cancellationToken);
        if (usuario is null) return Unauthorized();
        if (!await _cursos.ProfessoraTemCursoAsync(usuario, id, cancellationToken)) return ForbidCurso();

        if (!DateOnly.TryParse(data, CultureInfo.InvariantCulture, out var dia)) return BadRequest(new { mensagem = "Data inválida." });
        if (request.Registros.Any(r => r.Status is not null && !StatusChamada.Contains(r.Status))) return BadRequest(new { mensagem = "Status de presença inválido." });

        var alunasDoCurso = (await CarregarAlunasAsync(id, cancellationToken)).Select(a => a.Id).ToHashSet();
        var atividades = LimparLista(request.Atividades);
        var tema = Limitar(request.Tema?.Trim(), 200);

        // Mesmo comportamento de antes: tema novo vira aula no plano; tema existente recebe as atividades.
        var planoId = request.PlanoAulaId;
        if (!string.IsNullOrWhiteSpace(tema))
        {
            var planos = await _dbContext.PlanosAula.Where(p => p.CursoId == id).ToListAsync(cancellationToken);
            var plano = planos.FirstOrDefault(p => p.Id == planoId)
                ?? planos.FirstOrDefault(p => AssistidasService.NormalizarTexto(p.Tema) == AssistidasService.NormalizarTexto(tema));
            if (plano is null)
            {
                plano = new PlanoAula
                {
                    CursoId = id,
                    Data = dia,
                    Tema = tema,
                    AtividadesJson = JsonSerializer.Serialize(atividades),
                    Origem = "criado_pela_chamada",
                    CriadoPorUserId = usuario.UserId,
                    CriadoPorIdentificador = usuario.Identificador
                };
                _dbContext.PlanosAula.Add(plano);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            else
            {
                var existentes = LerLista(plano.AtividadesJson);
                existentes.AddRange(atividades.Where(a => !existentes.Any(e => AssistidasService.NormalizarTexto(e) == AssistidasService.NormalizarTexto(a))));
                plano.AtividadesJson = JsonSerializer.Serialize(existentes);
                plano.AtualizadoEm = DateTime.UtcNow;
            }

            planoId = plano.Id;
        }

        var chamada = await _dbContext.Chamadas.Include(c => c.Registros)
            .SingleOrDefaultAsync(c => c.CursoId == id && c.Data == dia, cancellationToken);
        if (chamada is null)
        {
            chamada = new Chamada { CursoId = id, Data = dia };
            _dbContext.Chamadas.Add(chamada);
        }

        chamada.Tema = tema;
        chamada.PlanoAulaId = planoId;
        chamada.AtividadesJson = JsonSerializer.Serialize(atividades);
        chamada.RegistradaPorUserId = usuario.UserId;
        chamada.RegistradaPorIdentificador = usuario.Identificador;
        chamada.RegistradaPorNome = usuario.Nome;
        chamada.AtualizadoEm = DateTime.UtcNow;

        foreach (var registro in request.Registros.Where(r => alunasDoCurso.Contains(r.AssistidaId) || chamada.Registros.Any(e => e.AssistidaId == r.AssistidaId)))
        {
            var existente = chamada.Registros.FirstOrDefault(e => e.AssistidaId == registro.AssistidaId);
            if (existente is null)
            {
                existente = new ChamadaRegistro { AssistidaId = registro.AssistidaId };
                chamada.Registros.Add(existente);
            }

            existente.Status = registro.Status;
            existente.Observacao = Limitar(registro.Observacao?.Trim(), 300);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await _snapshot.PersistAsync("chamada_salva", CancellationToken.None);

        return Ok(new ChamadaResponse
        {
            Id = chamada.Id,
            Data = chamada.Data.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Tema = chamada.Tema,
            PlanoAulaId = chamada.PlanoAulaId,
            Atividades = atividades,
            RegistradaPorNome = chamada.RegistradaPorNome
        });
    }

    private async Task<ActionResult<PlanoAulaResponse>> SalvarPlanoAsync(int cursoId, int? planoId, SalvarPlanoAulaRequest request, CancellationToken cancellationToken)
    {
        var usuario = await _assistidas.ObterUsuarioAsync(User, cancellationToken);
        if (usuario is null) return Unauthorized();
        if (!await _cursos.ProfessoraTemCursoAsync(usuario, cursoId, cancellationToken)) return ForbidCurso();

        if (!DateOnly.TryParse(request.Data, CultureInfo.InvariantCulture, out var data)) return BadRequest(new { mensagem = "Informe a data da aula." });
        var tema = request.Tema?.Trim() ?? string.Empty;
        if (tema.Length == 0) return BadRequest(new { mensagem = "Informe o tema da aula." });

        PlanoAula? plano;
        if (planoId is null)
        {
            plano = new PlanoAula { CursoId = cursoId, CriadoPorUserId = usuario.UserId, CriadoPorIdentificador = usuario.Identificador };
            _dbContext.PlanosAula.Add(plano);
        }
        else
        {
            plano = await _dbContext.PlanosAula.SingleOrDefaultAsync(p => p.Id == planoId && p.CursoId == cursoId, cancellationToken);
            if (plano is null) return NotFound(new { mensagem = "Aula não encontrada." });
        }

        plano.Data = data;
        plano.Tema = Limitar(tema, 200)!;
        plano.AtividadesJson = JsonSerializer.Serialize(LimparLista(request.Atividades));
        plano.Observacoes = Limitar(request.Observacoes?.Trim(), 2000);
        plano.AtualizadoEm = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _snapshot.PersistAsync("plano_aula", CancellationToken.None);

        return Ok(MapearPlano(plano));
    }

    private async Task<List<AlunaCursoResponse>> CarregarAlunasAsync(int cursoId, CancellationToken cancellationToken)
    {
        return await _dbContext.AssistidasCursos.AsNoTracking()
            .Where(v => v.CursoId == cursoId && v.Assistida!.Ativa && !v.Assistida.AcessoRestrito)
            .OrderBy(v => v.Assistida!.Nome)
            .Select(v => new AlunaCursoResponse
            {
                Id = v.AssistidaId,
                Nome = v.Assistida!.Nome,
                Telefone = v.Assistida.SemTelefoneSeguro || v.Assistida.ContatoSeguro == AssistidaContatoSeguro.NaoLigar ? null : v.Assistida.Telefone,
                DataAcolhimento = v.Assistida.CriadoEm
            })
            .ToListAsync(cancellationToken);
    }

    private async Task<HashSet<int>> CursosComHistoricoAsync(CancellationToken cancellationToken)
    {
        var ids = new HashSet<int>();
        ids.UnionWith(await _dbContext.AssistidasCursos.Select(a => a.CursoId).Distinct().ToListAsync(cancellationToken));
        ids.UnionWith(await _dbContext.ProfessoresCursos.Select(a => a.CursoId).Distinct().ToListAsync(cancellationToken));
        ids.UnionWith(await _dbContext.PlanosAula.Select(a => a.CursoId).Distinct().ToListAsync(cancellationToken));
        ids.UnionWith(await _dbContext.Chamadas.Select(a => a.CursoId).Distinct().ToListAsync(cancellationToken));
        return ids;
    }

    private async Task<IActionResult> AlterarAtivoAsync(int id, bool ativo, CancellationToken cancellationToken)
    {
        var curso = await _dbContext.Cursos.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (curso is null) return NotFound(new { mensagem = "Curso não encontrado." });

        curso.Ativo = ativo;
        curso.AtualizadoEm = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        await AtualizarResumoProfessorasDoCursoAsync(id, cancellationToken);

        await _auditoria.RegistrarAsync(
            ativo ? "CURSO_REATIVADO" : "CURSO_DESATIVADO",
            "Curso",
            id.ToString(CultureInfo.InvariantCulture),
            $"{(ativo ? "Reativou" : "Desativou")} o curso \"{curso.Nome}\".");
        await _snapshot.PersistAsync("curso_status", CancellationToken.None);
        return Ok(new { mensagem = ativo ? "Curso reativado." : "Curso desativado. Ele sai das listas, mas o histórico continua." });
    }

    private async Task AtualizarResumoProfessorasDoCursoAsync(int cursoId, CancellationToken cancellationToken)
    {
        var professoras = await _dbContext.ProfessoresCursos.Where(p => p.CursoId == cursoId).Select(p => p.UserId).ToListAsync(cancellationToken);
        foreach (var userId in professoras)
        {
            await _cursos.AtualizarResumoProfessoraAsync(userId, cancellationToken);
        }
    }

    private async Task<string?> ValidarNomeAsync(string nome, int? id, CancellationToken cancellationToken)
    {
        if (nome.Length < 3 || nome.Length > 120) return "O nome do curso deve ter entre 3 e 120 caracteres.";

        var normalizado = AssistidasService.NormalizarTexto(nome);
        var nomes = await _dbContext.Cursos.Where(c => c.Id != id).Select(c => c.Nome).ToListAsync(cancellationToken);
        return nomes.Any(n => AssistidasService.NormalizarTexto(n) == normalizado) ? "Já existe um curso com este nome." : null;
    }

    private ObjectResult ForbidCurso()
    {
        return StatusCode(StatusCodes.Status403Forbidden, new { mensagem = "Você não está vinculada a este curso." });
    }

    private static PlanoAulaResponse MapearPlano(PlanoAula p)
    {
        return new PlanoAulaResponse
        {
            Id = p.Id,
            Data = p.Data.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Tema = p.Tema,
            Atividades = LerLista(p.AtividadesJson),
            Observacoes = p.Observacoes,
            Origem = p.Origem,
            CriadoPorIdentificador = p.CriadoPorIdentificador
        };
    }

    private static List<string> LerLista(string? json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json ?? "[]") ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static List<string> LimparLista(IEnumerable<string>? itens)
    {
        var resultado = new List<string>();
        foreach (var item in itens ?? [])
        {
            var texto = Limitar(item?.Trim(), 80);
            if (!string.IsNullOrWhiteSpace(texto) && !resultado.Any(r => AssistidasService.NormalizarTexto(r) == AssistidasService.NormalizarTexto(texto)))
            {
                resultado.Add(texto);
            }
        }

        return resultado.Take(40).ToList();
    }

    private static string? Limitar(string? valor, int maximo)
    {
        if (string.IsNullOrEmpty(valor)) return null;
        return valor.Length <= maximo ? valor : valor[..maximo];
    }
}
