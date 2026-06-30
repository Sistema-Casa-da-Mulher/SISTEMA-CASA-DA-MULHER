using System;
using System.Threading.Tasks;
using CasaMulher.Api.DTOs;
using CasaMulher.Api.Models;
using CasaMulher.Api.Security;
using CasaMulher.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CasaMulher.Api.Controllers
{
    [ApiController]
    [Route("api/equipe-storage")]
    [Authorize(Policy = PoliticasAcesso.AcessoEquipe)]
    public class EquipeStorageController : ControllerBase
    {
        private readonly IEquipeStorageService _storageService;
        private readonly UserManager<ApplicationUser> _userManager;

        public EquipeStorageController(IEquipeStorageService storageService, UserManager<ApplicationUser> userManager)
        {
            _storageService = storageService;
            _userManager = userManager;
        }

        private async Task<ApplicationUser?> GetCurrentUserAsync()
        {
            return await _userManager.GetUserAsync(User);
        }

        [HttpPost("salvar")]
        [RequestSizeLimit(104857600)] // 100MB
        public async Task<IActionResult> Salvar([FromForm] SalvarRascunhoFormRequest request)
        {
            try
            {
                var user = await GetCurrentUserAsync();
                if (user == null) return Unauthorized();

                var resultado = await _storageService.SalvarRascunhoAsync(request, user);
                if (resultado.Sucesso) return Ok(resultado);
                return BadRequest(resultado);
            }
            catch (Exception ex)
            {
                return BadRequest(new { mensagem = ex.Message });
            }
        }

        [HttpGet("meus-rascunhos")]
        public async Task<IActionResult> MeusRascunhos()
        {
            try
            {
                var user = await GetCurrentUserAsync();
                if (user == null) return Unauthorized();

                var lista = await _storageService.ListarMeusRascunhosAsync(user);
                return Ok(lista);
            }
            catch (Exception ex)
            {
                return BadRequest(new { mensagem = ex.Message });
            }
        }

        [HttpGet("compartilhados")]
        public async Task<IActionResult> Compartilhados()
        {
            try
            {
                var user = await GetCurrentUserAsync();
                if (user == null) return Unauthorized();

                var lista = await _storageService.ListarCompartilhadosAsync(user);
                return Ok(lista);
            }
            catch (Exception ex)
            {
                return BadRequest(new { mensagem = ex.Message });
            }
        }

        [HttpGet("download")]
        public async Task<IActionResult> Download([FromQuery] string id)
        {
            try
            {
                var user = await GetCurrentUserAsync();
                if (user == null) return Unauthorized();

                var zipBytes = await _storageService.BaixarRascunhoAsync(id, user);
                return File(zipBytes, "application/zip", $"{id}.zip");
            }
            catch (Exception ex)
            {
                return BadRequest(new { mensagem = ex.Message });
            }
        }

        [HttpPost("criar-pr")]
        public async Task<IActionResult> CriarPr([FromBody] dynamic body)
        {
            try
            {
                string id = body?.id?.ToString() ?? string.Empty;
                var user = await GetCurrentUserAsync();
                if (user == null) return Unauthorized();

                if (string.IsNullOrEmpty(id)) return BadRequest(new { mensagem = "ID do rascunho inválido." });

                var resultado = await _storageService.CriarPrDeRascunhoAsync(id, user);
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
