using System.Collections.Generic;
using Microsoft.AspNetCore.Http;

namespace CasaMulher.Api.DTOs
{
    public sealed class ArquivoAnalisadoDto
    {
        public string Caminho { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty; // "Novo", "Modificado", "Removido", "Ignorado", "Identico", "Bloqueado"
        public string MotivoBloqueio { get; set; } = string.Empty;
        public bool EmPrototipo { get; set; }
    }

    public sealed class AnalisarUploadResponse
    {
        public List<ArquivoAnalisadoDto> Arquivos { get; set; } = new();
        public int TotalNovos { get; set; }
        public int TotalModificados { get; set; }
        public int TotalRemovidos { get; set; }
        public int TotalForaPrototipo { get; set; }
        public int TotalBloqueados { get; set; }
        public bool ContemAlteracoesForaPrototipo { get; set; }
        public bool ValidoParaEnvio => TotalBloqueados == 0 && (TotalNovos > 0 || TotalModificados > 0 || TotalRemovidos > 0);
    }

    public sealed class CriarUploadPrRequest
    {
        public IFormFile ArquivoZip { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public string Descricao { get; set; } = string.Empty;
        public string TipoAlteracao { get; set; } = string.Empty;
        public bool ConfirmouSemSegredos { get; set; }
        public bool ConfirmouRevisaoArquivos { get; set; }
        public bool ConfirmouRevisaoExtraForaPrototipos { get; set; }
    }

    public sealed class AnalisarBranchPrRequest
    {
        public string RepositorioUrl { get; set; } = string.Empty;
        public string Branch { get; set; } = string.Empty;
    }

    public sealed class CriarBranchPrRequest
    {
        public string RepositorioUrl { get; set; } = string.Empty;
        public string Branch { get; set; } = string.Empty;
        public string Titulo { get; set; } = string.Empty;
        public string Descricao { get; set; } = string.Empty;
        public string TipoAlteracao { get; set; } = string.Empty;
        public bool ConfirmouSemSegredos { get; set; }
        public bool ConfirmouRevisaoArquivos { get; set; }
        public bool ConfirmouRevisaoExtraForaPrototipos { get; set; }
    }

    public sealed class CriarEnvioPrResponse
    {
        public bool Sucesso { get; set; }
        public string Mensagem { get; set; } = string.Empty;
        public string PullRequestUrl { get; set; } = string.Empty;
    }
}
