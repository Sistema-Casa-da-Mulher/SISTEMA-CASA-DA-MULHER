using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using CasaMulher.Api.DTOs;
using CasaMulher.Api.Models;

namespace CasaMulher.Api.Services
{
    public interface IEquipeEnvioPrService
    {
        Task<AnalisarUploadResponse> AnalisarUploadAsync(IFormFile arquivo, ApplicationUser usuario);
        Task<CriarEnvioPrResponse> CriarUploadPrAsync(CriarUploadPrRequest request, ApplicationUser usuario);
        Task<AnalisarUploadResponse> AnalisarBranchAsync(AnalisarBranchPrRequest request, ApplicationUser usuario);
        Task<CriarEnvioPrResponse> CriarBranchPrAsync(CriarBranchPrRequest request, ApplicationUser usuario);
    }
}
