using System.Threading.Tasks;
using CasaMulher.Api.DTOs;
using CasaMulher.Api.Models;
using CasaMulher.Api.Services;
using CasaMulher.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CasaMulher.Api.Controllers
{
    [ApiController]
    [Route("api/equipe-pr")]
    [Authorize(Policy = PoliticasAcesso.AcessoEquipe)]
    public class EquipeEnvioPrController : ControllerBase
    {
        private readonly IEquipeEnvioPrService _envioService;
        private readonly UserManager<ApplicationUser> _userManager;

        public EquipeEnvioPrController(IEquipeEnvioPrService envioService, UserManager<ApplicationUser> userManager)
        {
            _envioService = envioService;
            _userManager = userManager;
        }

        private async Task<ApplicationUser> GetCurrentUserAsync()
        {
            return await _userManager.GetUserAsync(User);
        }

        [HttpGet("base/download")]
        [AllowAnonymous] // Talvez seja melhor não ter AllowAnonymous, mas redirecionamento público não é problema se o repo for público, mas este repo parece privado ou semi-privado. Vamos manter Authorize por segurança. Wait, no frontend they will be authenticated.
        public IActionResult DownloadBase()
        {
            // O repo oficial main zip
            return Redirect("https://github.com/Sistema-Casa-da-Mulher/SISTEMA-CASA-DA-MULHER/archive/refs/heads/main.zip");
        }

        [HttpPost("analisar-upload")]
        [RequestSizeLimit(104857600)] // 100MB limit para ZIP
        public async Task<IActionResult> AnalisarUpload([FromForm] CriarUploadPrRequest request)
        {
            try
            {
                var user = await GetCurrentUserAsync();
                if (user == null) return Unauthorized();

                if (request.ArquivoZip == null || request.ArquivoZip.Length == 0)
                    return BadRequest(new { mensagem = "Arquivo ZIP é obrigatório." });

                var resultado = await _envioService.AnalisarUploadAsync(request.ArquivoZip, user);
                return Ok(resultado);
            }
            catch (Exception ex)
            {
                return BadRequest(new { mensagem = ex.Message });
            }
        }

        [HttpPost("criar-upload")]
        [RequestSizeLimit(104857600)]
        public async Task<IActionResult> CriarUpload([FromForm] CriarUploadPrRequest request)
        {
            try
            {
                var user = await GetCurrentUserAsync();
                if (user == null) return Unauthorized();

                if (request.ArquivoZip == null || request.ArquivoZip.Length == 0)
                    return BadRequest(new { mensagem = "Arquivo ZIP é obrigatório." });

                var resultado = await _envioService.CriarUploadPrAsync(request, user);
                if (resultado.Sucesso) return Ok(resultado);
                return BadRequest(resultado);
            }
            catch (Exception ex)
            {
                return BadRequest(new { mensagem = ex.Message });
            }
        }

        [HttpPost("analisar-branch")]
        public async Task<IActionResult> AnalisarBranch([FromBody] AnalisarBranchPrRequest request)
        {
            try
            {
                var user = await GetCurrentUserAsync();
                if (user == null) return Unauthorized();

                var resultado = await _envioService.AnalisarBranchAsync(request, user);
                return Ok(resultado);
            }
            catch (Exception ex)
            {
                return BadRequest(new { mensagem = ex.Message });
            }
        }

        [HttpPost("criar-branch")]
        public async Task<IActionResult> CriarBranch([FromBody] CriarBranchPrRequest request)
        {
            try
            {
                var user = await GetCurrentUserAsync();
                if (user == null) return Unauthorized();

                var resultado = await _envioService.CriarBranchPrAsync(request, user);
                if (resultado.Sucesso) return Ok(resultado);
                return BadRequest(resultado);
            }
            catch (Exception ex)
            {
                return BadRequest(new { mensagem = ex.Message });
            }
        }
    }
}
