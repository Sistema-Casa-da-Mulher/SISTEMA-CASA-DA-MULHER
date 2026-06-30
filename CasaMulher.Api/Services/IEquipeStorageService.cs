using System.Collections.Generic;
using System.Threading.Tasks;
using CasaMulher.Api.DTOs;
using CasaMulher.Api.Models;

namespace CasaMulher.Api.Services
{
    public interface IEquipeStorageService
    {
        Task<EquipeStorageResponse> SalvarRascunhoAsync(SalvarRascunhoFormRequest request, ApplicationUser usuario);
        Task<List<ManifestRascunhoDTO>> ListarMeusRascunhosAsync(ApplicationUser usuario);
        Task<List<ManifestRascunhoDTO>> ListarCompartilhadosAsync(ApplicationUser usuario);
        Task<byte[]> BaixarRascunhoAsync(string id, ApplicationUser usuario);
        Task<CriarEnvioPrResponse> CriarPrDeRascunhoAsync(string id, ApplicationUser usuario);
    }
}
