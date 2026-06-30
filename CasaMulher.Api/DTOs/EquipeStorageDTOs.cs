using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;

namespace CasaMulher.Api.DTOs
{
    public class SalvarRascunhoFormRequest
    {
        public IFormFile? ArquivoZip { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public string Descricao { get; set; } = string.Empty;
        public string Tipo { get; set; } = string.Empty;
        public bool CompartilhadoEquipe { get; set; }
    }

    public class ManifestAutorDTO
    {
        public string Id { get; set; } = string.Empty;
        public string Nome { get; set; } = string.Empty;
        public string Perfil { get; set; } = string.Empty;
    }

    public class ManifestArquivoDTO
    {
        public string Caminho { get; set; } = string.Empty;
        public long TamanhoBytes { get; set; }
    }

    public class ManifestRascunhoDTO
    {
        public string Id { get; set; } = string.Empty;
        public string Titulo { get; set; } = string.Empty;
        public string Descricao { get; set; } = string.Empty;
        public string Tipo { get; set; } = string.Empty;
        public ManifestAutorDTO Autor { get; set; } = new();
        public DateTime CriadoEm { get; set; }
        public bool CompartilhadoEquipe { get; set; }
        public int TotalArquivos { get; set; }
        public long TamanhoTotalBytes { get; set; }
        public bool TemArquivosForaPrototipos { get; set; }
        public List<ManifestArquivoDTO> Arquivos { get; set; } = new();
    }

    public class EquipeStorageResponse
    {
        public bool Sucesso { get; set; }
        public string Mensagem { get; set; } = string.Empty;
        public string? RascunhoId { get; set; }
    }
}
