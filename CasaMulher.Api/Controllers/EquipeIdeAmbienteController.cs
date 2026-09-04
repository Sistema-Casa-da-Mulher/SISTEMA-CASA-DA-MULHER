using System.Threading.Tasks;
using CasaMulher.Api.DTOs;
using CasaMulher.Api.Models;
using CasaMulher.Api.Security;
using CasaMulher.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;

namespace CasaMulher.Api.Controllers
{
    [ApiController]
    [Route("api/equipe-ide/ambiente")]
    [Authorize(Policy = PoliticasAcesso.AcessoEquipe)]
    public class EquipeIdeAmbienteController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IGitHubIdeService _githubService;
        private readonly IHostEnvironment _env;

        public EquipeIdeAmbienteController(
            UserManager<ApplicationUser> userManager,
            IGitHubIdeService githubService,
            IHostEnvironment env)
        {
            _userManager = userManager;
            _githubService = githubService;
            _env = env;
        }

        [HttpGet("status")]
        public async Task<IActionResult> GetStatus()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var githubStatus = await _githubService.ObterStatusAsync();
            var perfis = await _userManager.GetRolesAsync(user);

            var status = new EquipeIdeAmbienteStatusDto
            {
                ApiOnline = true,
                Ambiente = _env.EnvironmentName,
                Usuario = new EquipeIdeUsuarioAtualDto
                {
                    Id = user.Id,
                    Nome = user.NomeCompleto ?? user.UserName ?? "Desconhecido",
                    Perfil = string.Join(", ", perfis)
                },
                GitHubIde = new EquipeIdeGitHubStatusResumoDto
                {
                    Enabled = githubStatus?.Enabled ?? false,
                    ModoSeguroEquipe = true, // Controlado internamente
                    ForkPessoal = true
                }
            };

            return Ok(status);
        }
    }
}
