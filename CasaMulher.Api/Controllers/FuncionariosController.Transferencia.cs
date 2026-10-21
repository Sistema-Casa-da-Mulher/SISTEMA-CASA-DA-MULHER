using CasaMulher.Api.DTOs;
using CasaMulher.Api.Models;
using CasaMulher.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CasaMulher.Api.Controllers;

/// <summary>
/// Passar responsabilidades: quando uma profissional sai da Casa, o ADM transfere para outra
/// do mesmo perfil os cursos (professora), os casos (Jurídico) e os prontuários (Psicologia).
/// Os registros já feitos continuam assinados por quem fez; a conta nova é sempre outra.
/// </summary>
public partial class FuncionariosController
{
    [HttpGet("{id}/responsabilidades")]
    public async Task<ActionResult<ResponsabilidadesFuncionarioResponse>> Responsabilidades(string id)
    {
        if (!await PodeGerenciarFuncionariosInstitucionaisAsync()) return Forbid();

        var funcionario = await _userManager.FindByIdAsync(id);
        if (funcionario is null || EhContaEquipe(funcionario)) return NotFound(new { mensagem = "Funcionário não encontrado." });

        var cursos = await _dbContext.ProfessoresCursos.AsNoTracking()
            .Where(p => p.UserId == id)
            .Select(p => p.Curso!.Nome)
            .OrderBy(n => n)
            .ToListAsync();

        var destinos = await _dbContext.Users.AsNoTracking()
            .Where(u => u.Id != id && u.Ativo && u.Perfil == funcionario.Perfil && !u.IdentificadorFuncionario.StartsWith("EQP-"))
            .OrderBy(u => u.NomeCompleto)
            .Select(u => new ProfessoraCursosResponse { UserId = u.Id, Nome = u.NomeCompleto, Identificador = u.IdentificadorFuncionario, Ativo = u.Ativo })
            .ToListAsync();

        return Ok(new ResponsabilidadesFuncionarioResponse
        {
            UserId = funcionario.Id,
            Nome = funcionario.NomeCompleto,
            Perfil = funcionario.Perfil,
            Cursos = cursos,
            CasosJuridicos = await _dbContext.CasosJuridicosResponsaveis.CountAsync(r => r.UserId == id),
            ProntuariosPsicologicos = await _dbContext.ProntuariosPsicologicosResponsaveis.CountAsync(r => r.UserId == id),
            Destinos = destinos
        });
    }

    [HttpPost("{id}/transferir-responsabilidades")]
    public async Task<ActionResult<TransferenciaResultadoResponse>> TransferirResponsabilidades(string id, TransferirResponsabilidadesRequest request)
    {
        if (!await PodeGerenciarFuncionariosInstitucionaisAsync()) return Forbid();

        var origem = await _userManager.FindByIdAsync(id);
        if (origem is null || EhContaEquipe(origem)) return NotFound(new { mensagem = "Funcionário não encontrado." });
        if (!await PodeExecutarAcaoFuncionarioAsync(origem)) return Forbid();

        var destino = await _userManager.FindByIdAsync(request.DestinoUserId);
        if (destino is null || destino.Id == origem.Id || !destino.Ativo || EhContaEquipe(destino))
        {
            return BadRequest(new { mensagem = "Escolha uma pessoa ativa para assumir as responsabilidades." });
        }

        if (!string.Equals(destino.Perfil, origem.Perfil, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { mensagem = "Quem assume precisa ter o mesmo perfil de quem saiu." });
        }

        if (request.DesativarConta)
        {
            var bloqueio = await ValidarDesativacaoSeguraAsync(origem);
            if (bloqueio is not null) return bloqueio;
        }

        var contexto = await _contextoAcesso.ObterAsync(User, HttpContext.RequestAborted);
        var adm = contexto?.IdentificadorFuncionario ?? "ADM";
        var agora = DateTime.UtcNow;

        await using var transacao = await _dbContext.Database.BeginTransactionAsync();

        // Professora: os cursos. Plano de aula e chamada pertencem ao curso e seguem junto.
        var cursosDestino = await _dbContext.ProfessoresCursos.Where(p => p.UserId == destino.Id).Select(p => p.CursoId).ToListAsync();
        var cursosOrigem = await _dbContext.ProfessoresCursos.Where(p => p.UserId == origem.Id).ToListAsync();
        foreach (var vinculo in cursosOrigem)
        {
            if (cursosDestino.Contains(vinculo.CursoId))
            {
                _dbContext.ProfessoresCursos.Remove(vinculo);
            }
            else
            {
                vinculo.UserId = destino.Id;
                vinculo.AtribuidoEm = agora;
            }
        }

        // Jurídico: responsável pelos casos.
        var casosDestino = await _dbContext.CasosJuridicosResponsaveis.Where(r => r.UserId == destino.Id).Select(r => r.CasoJuridicoId).ToListAsync();
        var casosOrigem = await _dbContext.CasosJuridicosResponsaveis.Where(r => r.UserId == origem.Id).ToListAsync();
        foreach (var responsavel in casosOrigem)
        {
            if (casosDestino.Contains(responsavel.CasoJuridicoId))
            {
                _dbContext.CasosJuridicosResponsaveis.Remove(responsavel);
                continue;
            }

            responsavel.UserId = destino.Id;
            responsavel.Identificador = destino.IdentificadorFuncionario;
            responsavel.Nome = destino.NomeCompleto;
            responsavel.AtribuidoPorIdentificador = adm;
            responsavel.AtribuidoEm = agora;
        }

        // Psicologia: responsável pelos prontuários.
        var prontuariosDestino = await _dbContext.ProntuariosPsicologicosResponsaveis.Where(r => r.UserId == destino.Id).Select(r => r.ProntuarioPsicologicoId).ToListAsync();
        var prontuariosOrigem = await _dbContext.ProntuariosPsicologicosResponsaveis.Where(r => r.UserId == origem.Id).ToListAsync();
        foreach (var responsavel in prontuariosOrigem)
        {
            if (prontuariosDestino.Contains(responsavel.ProntuarioPsicologicoId))
            {
                _dbContext.ProntuariosPsicologicosResponsaveis.Remove(responsavel);
                continue;
            }

            responsavel.UserId = destino.Id;
            responsavel.Identificador = destino.IdentificadorFuncionario;
            responsavel.Nome = destino.NomeCompleto;
            responsavel.Crp = destino.Crp;
            responsavel.AtribuidoPorIdentificador = adm;
            responsavel.AtribuidoEm = agora;
        }

        await _dbContext.SaveChangesAsync();

        if (request.DesativarConta && origem.Ativo)
        {
            origem.Ativo = false;
            await _userManager.UpdateAsync(origem);
        }

        await transacao.CommitAsync();

        var cursosService = HttpContext.RequestServices.GetRequiredService<CursosService>();
        await cursosService.AtualizarResumoProfessoraAsync(origem.Id);
        await cursosService.AtualizarResumoProfessoraAsync(destino.Id);

        await _auditoriaService.RegistrarAsync(
            "RESPONSABILIDADES_TRANSFERIDAS",
            "ApplicationUser",
            origem.Id,
            $"Transferiu de {origem.IdentificadorFuncionario} para {destino.IdentificadorFuncionario}: {cursosOrigem.Count} curso(s), {casosOrigem.Count} caso(s) jurídico(s), {prontuariosOrigem.Count} prontuário(s).");

        if (request.DesativarConta)
        {
            await _auditoriaService.RegistrarAsync(
                "FUNCIONARIO_DESATIVADO",
                "ApplicationUser",
                origem.Id,
                $"Desativou o funcionário {origem.IdentificadorFuncionario} ({origem.Email}) ao transferir as responsabilidades.");
        }

        var snapshot = await _securitySnapshot.PersistAsync("responsabilidades_transferidas", CancellationToken.None);

        return Ok(new TransferenciaResultadoResponse
        {
            Cursos = cursosOrigem.Count,
            CasosJuridicos = casosOrigem.Count,
            ProntuariosPsicologicos = prontuariosOrigem.Count,
            ContaDesativada = request.DesativarConta,
            Aviso = snapshot.AvisoSnapshot
        });
    }
}
