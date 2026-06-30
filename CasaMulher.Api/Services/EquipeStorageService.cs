using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CasaMulher.Api.DTOs;
using CasaMulher.Api.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Octokit;

namespace CasaMulher.Api.Services
{
    public class EquipeStorageService : IEquipeStorageService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<EquipeStorageService> _logger;
        private readonly IEquipeEnvioPrService _prService;
        private readonly string _owner;
        private readonly string _repo;
        private readonly string _branch;
        private readonly string _token;

        public EquipeStorageService(IConfiguration config, ILogger<EquipeStorageService> logger, IEquipeEnvioPrService prService)
        {
            _config = config;
            _logger = logger;
            _prService = prService;

            _owner = _config["GitHubStorage:Owner"] ?? throw new ArgumentException("GitHubStorage:Owner não configurado.");
            _repo = _config["GitHubStorage:Repo"] ?? throw new ArgumentException("GitHubStorage:Repo não configurado.");
            _branch = _config["GitHubStorage:Branch"] ?? "main";
            
            // Busca o token configurado no ambiente
            _token = _config["GitHubStorage__Token"] ?? _config["GitHubStorage:Token"] ?? string.Empty;
        }

        private GitHubClient ObterCliente()
        {
            if (string.IsNullOrEmpty(_token))
                throw new InvalidOperationException("Token do storage não configurado (GitHubStorage__Token).");

            return new GitHubClient(new ProductHeaderValue("CasaMulher-Storage"))
            {
                Credentials = new Credentials(_token)
            };
        }

        private string GerarIdSeguro(string identificador, string titulo)
        {
            string slug = Regex.Replace(titulo.ToLowerInvariant(), @"[^a-z0-9\s-]", "");
            slug = Regex.Replace(slug, @"\s+", "-").Trim('-');
            if (string.IsNullOrEmpty(slug)) slug = "rascunho";
            
            string data = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            return $"{identificador.ToLowerInvariant()}-{slug}-{data}";
        }

        public async Task<EquipeStorageResponse> SalvarRascunhoAsync(SalvarRascunhoFormRequest request, ApplicationUser usuario)
        {
            if (request.ArquivoZip == null || request.ArquivoZip.Length == 0)
                throw new ArgumentException("Arquivo ZIP é obrigatório.");

            if (string.IsNullOrWhiteSpace(request.Titulo))
                throw new ArgumentException("Título é obrigatório.");

            var client = ObterCliente();
            var refBranch = $"heads/{_branch}";
            var branchRef = await client.Git.Reference.Get(_owner, _repo, refBranch);
            var baseCommit = await client.Git.Commit.Get(_owner, _repo, branchRef.Object.Sha);
            var baseTreeSha = baseCommit.Tree.Sha;

            var newTree = new NewTree { BaseTree = baseTreeSha };

            string id = GerarIdSeguro(usuario.IdentificadorFuncionario, request.Titulo);
            string slugProjeto = Regex.Replace(request.Titulo.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
            if (string.IsNullOrEmpty(slugProjeto)) slugProjeto = "rascunho";
            string versaoDir = $"usuarios/{usuario.IdentificadorFuncionario}/{slugProjeto}/versoes/{id}/";

            long totalBytes = 0;
            int totalArquivos = 0;
            bool temForaPrototipo = false;
            var listaArquivos = new List<ManifestArquivoDTO>();

            using var ms = new MemoryStream();
            await request.ArquivoZip.CopyToAsync(ms);
            using var archive = new ZipArchive(ms, ZipArchiveMode.Read);

            // Ignorar root folder do ZIP
            string? rootFolder = null;
            if (archive.Entries.Count > 0)
            {
                var firstEntryParts = archive.Entries[0].FullName.Replace("\\", "/").Split('/');
                if (firstEntryParts.Length > 1)
                {
                    var possibleRoot = firstEntryParts[0] + "/";
                    if (archive.Entries.All(e => e.FullName.Replace("\\", "/").StartsWith(possibleRoot)))
                    {
                        rootFolder = possibleRoot;
                    }
                }
            }

            foreach (var entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name)) continue; // É pasta

                var caminhoOriginal = entry.FullName.Replace("\\", "/");
                if (rootFolder != null && caminhoOriginal.StartsWith(rootFolder))
                {
                    caminhoOriginal = caminhoOriginal.Substring(rootFolder.Length);
                }

                if (DeveBloquear(caminhoOriginal) || DeveIgnorar(caminhoOriginal)) continue;

                if (!EmPrototipo(caminhoOriginal)) temForaPrototipo = true;

                using var entryStream = entry.Open();
                using var fileMs = new MemoryStream();
                await entryStream.CopyToAsync(fileMs);
                var contentBytes = fileMs.ToArray();

                totalBytes += contentBytes.Length;
                totalArquivos++;

                listaArquivos.Add(new ManifestArquivoDTO { Caminho = caminhoOriginal, TamanhoBytes = contentBytes.Length });

                var blob = new NewBlob
                {
                    Encoding = EncodingType.Base64,
                    Content = Convert.ToBase64String(contentBytes)
                };
                var blobRef = await client.Git.Blob.Create(_owner, _repo, blob);

                newTree.Tree.Add(new NewTreeItem
                {
                    Path = versaoDir + "arquivos/" + caminhoOriginal,
                    Mode = "100644",
                    Type = TreeType.Blob,
                    Sha = blobRef.Sha
                });
            }

            if (totalArquivos == 0)
                throw new ArgumentException("O arquivo ZIP não contém arquivos válidos para salvar.");

            var manifest = new ManifestRascunhoDTO
            {
                Id = id,
                Titulo = request.Titulo,
                Descricao = request.Descricao,
                Tipo = request.Tipo,
                Autor = new ManifestAutorDTO { Id = usuario.IdentificadorFuncionario, Nome = usuario.NomeCompleto, Perfil = usuario.Perfil },
                CriadoEm = DateTime.UtcNow,
                CompartilhadoEquipe = request.CompartilhadoEquipe,
                TotalArquivos = totalArquivos,
                TamanhoTotalBytes = totalBytes,
                TemArquivosForaPrototipos = temForaPrototipo,
                Arquivos = listaArquivos
            };

            var manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
            var manifestBlob = new NewBlob
            {
                Encoding = EncodingType.Utf8,
                Content = manifestJson
            };
            var manifestBlobRef = await client.Git.Blob.Create(_owner, _repo, manifestBlob);

            newTree.Tree.Add(new NewTreeItem
            {
                Path = versaoDir + "manifest.json",
                Mode = "100644",
                Type = TreeType.Blob,
                Sha = manifestBlobRef.Sha
            });

            var createdTree = await client.Git.Tree.Create(_owner, _repo, newTree);
            var newCommit = new NewCommit($"Salva rascunho {id}", createdTree.Sha, new[] { baseCommit.Sha });
            var createdCommit = await client.Git.Commit.Create(_owner, _repo, newCommit);

            await client.Git.Reference.Update(_owner, _repo, refBranch, new ReferenceUpdate(createdCommit.Sha));

            return new EquipeStorageResponse { Sucesso = true, Mensagem = "Rascunho salvo com sucesso!", RascunhoId = id };
        }

        private bool EmPrototipo(string caminho)
        {
            var p = caminho.ToLowerInvariant().Replace("\\", "/");
            return p.StartsWith("prototipos/");
        }

        private bool DeveBloquear(string caminho)
        {
            var nome = Path.GetFileName(caminho).ToLowerInvariant();
            if (nome.StartsWith("appsettings") && nome.EndsWith(".json")) return true;
            if (nome.EndsWith(".pfx") || nome.EndsWith(".pem") || nome.EndsWith(".key")) return true;
            return false;
        }

        private bool DeveIgnorar(string caminho)
        {
            var p = caminho.ToLowerInvariant().Replace("\\", "/");
            if (p.Contains("/.git/") || p.StartsWith(".git/")) return true;
            if (p.Contains("/node_modules/") || p.StartsWith("node_modules/")) return true;
            if (p.Contains("/bin/") || p.StartsWith("bin/")) return true;
            if (p.Contains("/obj/") || p.StartsWith("obj/")) return true;
            return false;
        }

        public async Task<List<ManifestRascunhoDTO>> ListarMeusRascunhosAsync(ApplicationUser usuario)
        {
            return await ListarRascunhosInternoAsync(m => m.Autor.Id == usuario.IdentificadorFuncionario);
        }

        public async Task<List<ManifestRascunhoDTO>> ListarCompartilhadosAsync(ApplicationUser usuario)
        {
            // Retorna compartilhados que não são da própria pessoa (ou retorna todos se quiser)
            return await ListarRascunhosInternoAsync(m => m.CompartilhadoEquipe);
        }

        private async Task<List<ManifestRascunhoDTO>> ListarRascunhosInternoAsync(Func<ManifestRascunhoDTO, bool> filter)
        {
            var client = ObterCliente();
            var resultados = new List<ManifestRascunhoDTO>();
            
            try
            {
                var refBranch = $"heads/{_branch}";
                var branchRef = await client.Git.Reference.Get(_owner, _repo, refBranch);
                var tree = await client.Git.Tree.GetRecursive(_owner, _repo, branchRef.Object.Sha);

                var manifestsTree = tree.Tree.Where(t => t.Path.EndsWith("/manifest.json") && t.Path.StartsWith("usuarios/")).ToList();

                foreach (var item in manifestsTree)
                {
                    var blob = await client.Git.Blob.Get(_owner, _repo, item.Sha);
                    string json = blob.Encoding == EncodingType.Base64 ? Encoding.UTF8.GetString(Convert.FromBase64String(blob.Content)) : blob.Content;
                    
                    try
                    {
                        var m = JsonSerializer.Deserialize<ManifestRascunhoDTO>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (m != null && filter(m))
                        {
                            resultados.Add(m);
                        }
                    }
                    catch { /* ignora manifests mal formatados */ }
                }
            }
            catch (Octokit.ApiException ex) when (ex is Octokit.NotFoundException || ex.Message.Contains("Git Repository is empty") || ex.Message.Contains("empty"))
            {
                // Repositório ou branch não existem ainda ou o repositório está vazio.
                _logger.LogWarning("Repositório de storage não encontrado, branch inexistente ou repositório vazio.");
            }

            return resultados.OrderByDescending(r => r.CriadoEm).ToList();
        }

        public async Task<byte[]> BaixarRascunhoAsync(string id, ApplicationUser usuario)
        {
            var manifestTuple = await LocalizarManifestAsync(id);
            if (manifestTuple == null) throw new ArgumentException("Rascunho não encontrado.");
            
            var manifest = manifestTuple.Value.manifest;
            var manifestPath = manifestTuple.Value.path;

            // path do manifest é "usuarios/.../versoes/id/manifest.json"
            // arquivos estão em "usuarios/.../versoes/id/arquivos/"
            var arquivosPrefix = manifestPath.Replace("manifest.json", "arquivos/");

            var client = ObterCliente();
            var refBranch = $"heads/{_branch}";
            var branchRef = await client.Git.Reference.Get(_owner, _repo, refBranch);
            var tree = await client.Git.Tree.GetRecursive(_owner, _repo, branchRef.Object.Sha);

            var files = tree.Tree.Where(t => t.Path.StartsWith(arquivosPrefix) && t.Type == TreeType.Blob).ToList();

            using var ms = new MemoryStream();
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
            {
                foreach (var file in files)
                {
                    var blob = await client.Git.Blob.Get(_owner, _repo, file.Sha);
                    byte[] content = blob.Encoding == EncodingType.Base64 ? Convert.FromBase64String(blob.Content) : Encoding.UTF8.GetBytes(blob.Content);
                    
                    var relativePath = file.Path.Substring(arquivosPrefix.Length);
                    var entry = zip.CreateEntry(relativePath);
                    using var entryStream = entry.Open();
                    await entryStream.WriteAsync(content);
                }
            }

            return ms.ToArray();
        }

        private async Task<(ManifestRascunhoDTO manifest, string path)?> LocalizarManifestAsync(string id)
        {
            var client = ObterCliente();
            var refBranch = $"heads/{_branch}";
            var branchRef = await client.Git.Reference.Get(_owner, _repo, refBranch);
            var tree = await client.Git.Tree.GetRecursive(_owner, _repo, branchRef.Object.Sha);

            var manifestsTree = tree.Tree.Where(t => t.Path.EndsWith("/manifest.json") && t.Path.StartsWith("usuarios/")).ToList();

            foreach (var item in manifestsTree)
            {
                var blob = await client.Git.Blob.Get(_owner, _repo, item.Sha);
                string json = blob.Encoding == EncodingType.Base64 ? Encoding.UTF8.GetString(Convert.FromBase64String(blob.Content)) : blob.Content;
                
                try
                {
                    var m = JsonSerializer.Deserialize<ManifestRascunhoDTO>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (m != null && m.Id == id)
                    {
                        return (m, item.Path);
                    }
                }
                catch { }
            }
            return null;
        }

        public async Task<CriarEnvioPrResponse> CriarPrDeRascunhoAsync(string id, ApplicationUser usuario)
        {
            var manifestTuple = await LocalizarManifestAsync(id);
            if (manifestTuple == null) throw new ArgumentException("Rascunho não encontrado.");
            var manifest = manifestTuple.Value.manifest;

            // Pode exigir que apenas quem é ADM ou o autor possa criar PR de um rascunho não compartilhado, mas
            // assumimos que a UI já controlou isso, ou podemos validar aqui.
            
            // 1. Baixar o ZIP em memória
            var zipBytes = await BaixarRascunhoAsync(id, usuario);

            // 2. Criar um FormFile mock
            var stream = new MemoryStream(zipBytes);
            var formFile = new FormFile(stream, 0, zipBytes.Length, "ArquivoZip", $"{id}.zip")
            {
                Headers = new HeaderDictionary(),
                ContentType = "application/zip"
            };

            // 3. Montar a requisição de PR e delegar para EquipeEnvioPrService
            var request = new CriarUploadPrRequest
            {
                ArquivoZip = formFile,
                Titulo = $"PR a partir do rascunho: {manifest.Titulo}",
                Descricao = $"## Origem\n\n- Tipo: Rascunho da Equipe\n- Rascunho: {manifest.Titulo}\n- Autor original: {manifest.Autor.Id} ({manifest.Autor.Nome})\n- Enviado por: {usuario.IdentificadorFuncionario}\n- Versão salva em: {manifest.CriadoEm:dd/MM/yyyy HH:mm}\n\n{manifest.Descricao}",
                ConfirmouRevisaoArquivos = true,
                ConfirmouRevisaoExtraForaPrototipos = manifest.TemArquivosForaPrototipos,
                ConfirmouSemSegredos = true
            };

            // O EquipeEnvioPrService cuidará do resto (comparar com a main, commitar as diferenças e abrir o PR)
            return await _prService.CriarUploadPrAsync(request, usuario);
        }
    }
}
