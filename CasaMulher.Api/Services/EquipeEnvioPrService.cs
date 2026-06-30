using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using CasaMulher.Api.Data;
using CasaMulher.Api.DTOs;
using CasaMulher.Api.Models;
using CasaMulher.Api.Utils;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Octokit;

namespace CasaMulher.Api.Services
{
    public class EquipeEnvioPrService : IEquipeEnvioPrService
    {
        private readonly AppDbContext _dbContext;
        private readonly GitHubIdeSettings _settings;
        private readonly IDataProtector _protector;
        private readonly ILogger<EquipeEnvioPrService> _logger;
        private readonly IAuditoriaService _auditoriaService;

        private readonly string[] _pastasIgnoradas = { ".git", "node_modules", "bin", "obj", ".vs" };
        private readonly string[] _extensoesIgnoradas = { ".tmp", ".log", ".suo", ".user" };
        private readonly string[] _arquivosBloqueados = { ".env", "secrets.json", "appsettings.production.json" };
        private readonly string[] _prefixosPrototipos = { "prototipos/", "casamulher.api/wwwroot/prototipos/", "projetocasadamulher/telas/prototipos/" };

        public EquipeEnvioPrService(
            AppDbContext dbContext,
            IOptions<GitHubIdeSettings> settings,
            IDataProtectionProvider dataProtectionProvider,
            ILogger<EquipeEnvioPrService> logger,
            IAuditoriaService auditoriaService)
        {
            _dbContext = dbContext;
            _settings = settings.Value;
            _protector = dataProtectionProvider.CreateProtector("GitHubIde.PersonalTokens");
            _logger = logger;
            _auditoriaService = auditoriaService;
        }

        private async Task<(GitHubClient Client, GitHubUsuarioVinculo Vinculo)> ObterClienteGitHubAsync(ApplicationUser usuario)
        {
            var vinculo = await _dbContext.GitHubUsuarioVinculos
                .FirstOrDefaultAsync(v => v.ApplicationUserId == usuario.Id && v.RevogadoEm == null);

            if (vinculo == null || string.IsNullOrEmpty(vinculo.AccessTokenEncrypted))
                throw new InvalidOperationException("Usuário não possui vínculo ativo com GitHub (token ausente).");

            string personalToken = _protector.Unprotect(vinculo.AccessTokenEncrypted);

            var client = new GitHubClient(new ProductHeaderValue("CasaMulher-Ide"))
            {
                Credentials = new Credentials(personalToken)
            };

            return (client, vinculo);
        }

        private string CalcularGitBlobSha(byte[] conteudo)
        {
            var header = $"blob {conteudo.Length}\0";
            var headerBytes = Encoding.ASCII.GetBytes(header);
            var fullBytes = new byte[headerBytes.Length + conteudo.Length];
            Buffer.BlockCopy(headerBytes, 0, fullBytes, 0, headerBytes.Length);
            Buffer.BlockCopy(conteudo, 0, fullBytes, headerBytes.Length, conteudo.Length);

            using var sha1 = SHA1.Create();
            var hash = sha1.ComputeHash(fullBytes);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        private bool DeveIgnorar(string caminho)
        {
            var p = caminho.ToLowerInvariant().Replace("\\", "/");
            if (_pastasIgnoradas.Any(pasta => p.Contains($"/{pasta}/") || p.StartsWith($"{pasta}/"))) return true;
            if (_extensoesIgnoradas.Any(ext => p.EndsWith(ext))) return true;
            return false;
        }

        private bool DeveBloquear(string caminho)
        {
            var nome = Path.GetFileName(caminho).ToLowerInvariant();
            if (_arquivosBloqueados.Contains(nome)) return true;
            if (nome.StartsWith("appsettings") && nome.EndsWith(".json")) return true;
            if (nome.EndsWith(".pfx") || nome.EndsWith(".pem") || nome.EndsWith(".key")) return true;
            return false;
        }

        private bool EmPrototipo(string caminho)
        {
            var p = caminho.ToLowerInvariant().Replace("\\", "/");
            return _prefixosPrototipos.Any(prefix => p.StartsWith(prefix));
        }

        public async Task<AnalisarUploadResponse> AnalisarUploadAsync(IFormFile arquivo, ApplicationUser usuario)
        {
            if (arquivo == null || arquivo.Length == 0)
                throw new ArgumentException("Arquivo ZIP inválido ou vazio.");

            var (client, _) = await ObterClienteGitHubAsync(usuario);

            // Obtém Tree da Main
            TreeResponse mainTree;
            try
            {
                var baseRef = await client.Git.Reference.Get(_settings.Owner, _settings.Repo, $"heads/{_settings.BaseBranch}");
                mainTree = await client.Git.Tree.GetRecursive(_settings.Owner, _settings.Repo, baseRef.Object.Sha);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter tree da main para comparação.");
                throw new InvalidOperationException("Não foi possível acessar o repositório principal no GitHub para comparação.");
            }

            var treeDict = mainTree.Tree.Where(t => t.Type == TreeType.Blob).ToDictionary(t => t.Path.Replace("\\", "/"), t => t.Sha);

            var result = new AnalisarUploadResponse();
            using var stream = arquivo.OpenReadStream();
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

            // Tentar descobrir se todos os arquivos estão numa pasta raiz única (ex: SISTEMA-CASA-DA-MULHER-main)
            string rootFolder = null;
            var entries = archive.Entries.Where(e => !e.FullName.EndsWith("/")).ToList();
            if (entries.Count > 0)
            {
                var firstEntryParts = entries[0].FullName.Replace("\\", "/").Split('/');
                if (firstEntryParts.Length > 1)
                {
                    var possibleRoot = firstEntryParts[0] + "/";
                    if (entries.All(e => e.FullName.Replace("\\", "/").StartsWith(possibleRoot)))
                    {
                        rootFolder = possibleRoot;
                    }
                }
            }

            foreach (var entry in entries)
            {
                var caminhoZip = entry.FullName.Replace("\\", "/");
                if (rootFolder != null && caminhoZip.StartsWith(rootFolder))
                    caminhoZip = caminhoZip.Substring(rootFolder.Length);

                if (DeveIgnorar(caminhoZip))
                {
                    result.Arquivos.Add(new ArquivoAnalisadoDto { Caminho = caminhoZip, Status = "Ignorado" });
                    continue;
                }

                if (DeveBloquear(caminhoZip))
                {
                    result.Arquivos.Add(new ArquivoAnalisadoDto { Caminho = caminhoZip, Status = "Bloqueado", MotivoBloqueio = "Arquivo sensível" });
                    result.TotalBloqueados++;
                    continue;
                }

                using var ms = new MemoryStream();
                using var entryStream = entry.Open();
                await entryStream.CopyToAsync(ms);
                var contentBytes = ms.ToArray();

                var sha = CalcularGitBlobSha(contentBytes);
                var isPrototipo = EmPrototipo(caminhoZip);

                var status = "Novo";
                if (treeDict.TryGetValue(caminhoZip, out var mainSha))
                {
                    status = mainSha.Equals(sha, StringComparison.OrdinalIgnoreCase) ? "Identico" : "Modificado";
                }

                if (status == "Novo") result.TotalNovos++;
                else if (status == "Modificado") result.TotalModificados++;
                
                if ((status == "Novo" || status == "Modificado") && !isPrototipo)
                {
                    result.TotalForaPrototipo++;
                    result.ContemAlteracoesForaPrototipo = true;
                }

                result.Arquivos.Add(new ArquivoAnalisadoDto
                {
                    Caminho = caminhoZip,
                    Status = status,
                    EmPrototipo = isPrototipo
                });
            }

            // TODO: Tratar arquivos removidos (não trivial via upload de zip parcial). Por hora, ignoramos removidos.

            return result;
        }

        public async Task<CriarEnvioPrResponse> CriarUploadPrAsync(CriarUploadPrRequest request, ApplicationUser usuario)
        {
            var analise = await AnalisarUploadAsync(request.ArquivoZip, usuario);
            
            if (!analise.ValidoParaEnvio)
                return new CriarEnvioPrResponse { Sucesso = false, Mensagem = "Análise rejeitou o arquivo. Corrija bloqueios e garanta que existam alterações." };

            if (analise.ContemAlteracoesForaPrototipo && !request.ConfirmouRevisaoExtraForaPrototipos)
                return new CriarEnvioPrResponse { Sucesso = false, Mensagem = "Você precisa confirmar a revisão de arquivos fora da área de protótipos." };

            if (!request.ConfirmouRevisaoArquivos || !request.ConfirmouSemSegredos)
                return new CriarEnvioPrResponse { Sucesso = false, Mensagem = "Aceite os termos de revisão no formulário." };

            var (client, vinculo) = await ObterClienteGitHubAsync(usuario);

            // Garantir Fork
            var existingForks = await client.Repository.Forks.GetAll(_settings.Owner, _settings.Repo);
            var personalFork = existingForks.FirstOrDefault(f => string.Equals(f.Owner.Login, vinculo.GitHubLogin, StringComparison.OrdinalIgnoreCase));

            if (personalFork == null)
            {
                personalFork = await client.Repository.Forks.Create(_settings.Owner, _settings.Repo, new NewRepositoryFork());
                await Task.Delay(5000); // Wait for GitHub
            }

            Reference baseRef = null;
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    baseRef = await client.Git.Reference.Get(personalFork.Owner.Login, personalFork.Name, $"heads/{_settings.BaseBranch}");
                    break;
                }
                catch (NotFoundException)
                {
                    if (i == 2) throw;
                    await Task.Delay(3000);
                }
            }

            var branchNameRef = $"refs/heads/envio-equipe/{usuario.IdentificadorFuncionario}/{DateTime.Now:yyyyMMdd-HHmmss}";
            await client.Git.Reference.Create(personalFork.Owner.Login, personalFork.Name, new NewReference(branchNameRef, baseRef.Object.Sha));

            var tree = new NewTree { BaseTree = baseRef.Object.Sha };
            
            using var stream = request.ArquivoZip.OpenReadStream();
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
            
            string rootFolder = null;
            var entries = archive.Entries.Where(e => !e.FullName.EndsWith("/")).ToList();
            if (entries.Count > 0)
            {
                var firstEntryParts = entries[0].FullName.Replace("\\", "/").Split('/');
                if (firstEntryParts.Length > 1)
                {
                    var possibleRoot = firstEntryParts[0] + "/";
                    if (entries.All(e => e.FullName.Replace("\\", "/").StartsWith(possibleRoot)))
                        rootFolder = possibleRoot;
                }
            }

            var alterados = analise.Arquivos.Where(a => a.Status == "Novo" || a.Status == "Modificado").ToList();

            foreach (var alt in alterados)
            {
                var entryPath = rootFolder != null ? rootFolder + alt.Caminho : alt.Caminho;
                var entry = archive.GetEntry(entryPath) ?? archive.GetEntry(entryPath.Replace("/", "\\"));
                if (entry != null)
                {
                    using var ms = new MemoryStream();
                    using var es = entry.Open();
                    await es.CopyToAsync(ms);
                    var blobContent = Convert.ToBase64String(ms.ToArray());
                    var blob = new NewBlob { Content = blobContent, Encoding = EncodingType.Base64 };
                    var blobRef = await client.Git.Blob.Create(personalFork.Owner.Login, personalFork.Name, blob);
                    tree.Tree.Add(new NewTreeItem { Path = alt.Caminho, Mode = "100644", Type = TreeType.Blob, Sha = blobRef.Sha });
                }
            }

            var createdTree = await client.Git.Tree.Create(personalFork.Owner.Login, personalFork.Name, tree);
            
            var safeTitulo = string.IsNullOrWhiteSpace(request.Titulo) ? "Sem Título" : request.Titulo;
            var commitMsg = $"Envio Rápido: {safeTitulo}";
            var commit = new NewCommit(commitMsg, createdTree.Sha, baseRef.Object.Sha);
            var createdCommit = await client.Git.Commit.Create(personalFork.Owner.Login, personalFork.Name, commit);

            await client.Git.Reference.Update(personalFork.Owner.Login, personalFork.Name, branchNameRef, new ReferenceUpdate(createdCommit.Sha));

            // PR Body
            var prBody = $"## Envio rápido da equipe\n\nEsta alteração foi enviada pela área \"Enviar alteração pronta\" do painel da equipe.\n\n";
            prBody += $"## Origem\n- Tipo: Upload ZIP\n- Repositório de origem: Upload Local\n\n";
            prBody += $"## Resumo\n- Arquivos novos: {analise.TotalNovos}\n- Arquivos modificados: {analise.TotalModificados}\n- Arquivos fora de protótipos: {analise.TotalForaPrototipo}\n\n";
            prBody += $"## Escopo\n";
            if (analise.ContemAlteracoesForaPrototipo)
            {
                prBody += $"⚠️ **Arquivos fora de protótipos:**\n";
                foreach (var f in alterados.Where(a => !a.EmPrototipo)) prBody += $"- `{f.Caminho}`\n";
                prBody += $"\n## Atenção\nEsta contribuição altera arquivos fora da área de protótipos. Revise com cuidado antes do merge.\n\n";
            }
            else
            {
                prBody += $"✅ Todos os arquivos alterados estão dentro da área de protótipos.\n\n";
            }
            prBody += $"## Observação da pessoa autora\n{request.Descricao}";

            var prHead = $"{vinculo.GitHubLogin}:{branchNameRef.Replace("refs/heads/", "")}";
            var newPr = new NewPullRequest(commitMsg, prHead, _settings.BaseBranch) { Body = prBody };
            var pr = await client.PullRequest.Create(_settings.Owner, _settings.Repo, newPr);

            await _auditoriaService.RegistrarAsync("ENVIO_RAPIDO_PR_CRIADO", "EquipeEnvioPr", null, $"PR Upload criado: {pr.HtmlUrl}", usuario.IdentificadorFuncionario);

            return new CriarEnvioPrResponse { Sucesso = true, PullRequestUrl = pr.HtmlUrl, Mensagem = "Pull Request criado com sucesso!" };
        }

        private (string Owner, string Repo) ParseRepoUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) throw new ArgumentException("URL inválida.");
            var parts = url.TrimEnd('/').Split('/');
            if (parts.Length < 2) throw new ArgumentException("URL do repositório inválida.");
            var repo = parts[^1];
            if (repo.EndsWith(".git")) repo = repo.Substring(0, repo.Length - 4);
            return (parts[^2], repo);
        }

        public async Task<AnalisarUploadResponse> AnalisarBranchAsync(AnalisarBranchPrRequest request, ApplicationUser usuario)
        {
            var (client, _) = await ObterClienteGitHubAsync(usuario);
            var (repoOwner, repoName) = ParseRepoUrl(request.RepositorioUrl);

            Octokit.CompareResult compareResult;
            try
            {
                // Compare oficial main vs fork branch
                compareResult = await client.Repository.Commit.Compare(_settings.Owner, _settings.Repo, _settings.BaseBranch, $"{repoOwner}:{request.Branch}");
            }
            catch (Octokit.NotFoundException)
            {
                throw new Exception($"Não foi possível encontrar a branch '{request.Branch}' no repositório '{repoOwner}/{repoName}'. Verifique se o nome está correto e se as permissões estão em dia.");
            }

            var result = new AnalisarUploadResponse();

            foreach (var file in compareResult.Files)
            {
                var caminho = file.Filename.Replace("\\", "/");
                var isPrototipo = EmPrototipo(caminho);

                if (DeveIgnorar(caminho))
                {
                    result.Arquivos.Add(new ArquivoAnalisadoDto { Caminho = caminho, Status = "Ignorado", EmPrototipo = isPrototipo });
                    continue;
                }

                if (DeveBloquear(caminho))
                {
                    result.Arquivos.Add(new ArquivoAnalisadoDto { Caminho = caminho, Status = "Bloqueado", MotivoBloqueio = "Arquivo sensível", EmPrototipo = isPrototipo });
                    result.TotalBloqueados++;
                    continue;
                }

                var status = file.Status switch
                {
                    "added" => "Novo",
                    "modified" => "Modificado",
                    "removed" => "Removido",
                    _ => "Modificado"
                };

                if (status == "Novo") result.TotalNovos++;
                else if (status == "Modificado") result.TotalModificados++;
                else if (status == "Removido") result.TotalRemovidos++;

                if (!isPrototipo)
                {
                    result.TotalForaPrototipo++;
                    result.ContemAlteracoesForaPrototipo = true;
                }

                result.Arquivos.Add(new ArquivoAnalisadoDto { Caminho = caminho, Status = status, EmPrototipo = isPrototipo });
            }

            return result;
        }

        public async Task<CriarEnvioPrResponse> CriarBranchPrAsync(CriarBranchPrRequest request, ApplicationUser usuario)
        {
            var analise = await AnalisarBranchAsync(new AnalisarBranchPrRequest { Branch = request.Branch, RepositorioUrl = request.RepositorioUrl }, usuario);

            if (!analise.ValidoParaEnvio)
                return new CriarEnvioPrResponse { Sucesso = false, Mensagem = "Análise rejeitou a branch. Corrija bloqueios e garanta que existam alterações." };

            if (analise.ContemAlteracoesForaPrototipo && !request.ConfirmouRevisaoExtraForaPrototipos)
                return new CriarEnvioPrResponse { Sucesso = false, Mensagem = "Você precisa confirmar a revisão de arquivos fora da área de protótipos." };

            var (client, vinculo) = await ObterClienteGitHubAsync(usuario);
            var (repoOwner, _) = ParseRepoUrl(request.RepositorioUrl);

            var safeTitulo = string.IsNullOrWhiteSpace(request.Titulo) ? "Sem Título" : request.Titulo;
            var prBody = $"## Envio rápido da equipe\n\nEsta alteração foi enviada pela área \"Enviar alteração pronta\" do painel da equipe.\n\n";
            prBody += $"## Origem\n- Tipo: Branch GitHub\n- Repositório de origem: {request.RepositorioUrl}\n- Branch de origem: {request.Branch}\n\n";
            prBody += $"## Resumo\n- Arquivos novos: {analise.TotalNovos}\n- Arquivos modificados: {analise.TotalModificados}\n- Arquivos removidos: {analise.TotalRemovidos}\n- Arquivos fora de protótipos: {analise.TotalForaPrototipo}\n\n";
            prBody += $"## Escopo\n";
            if (analise.ContemAlteracoesForaPrototipo)
            {
                prBody += $"⚠️ **Arquivos fora de protótipos:**\n";
                foreach (var f in analise.Arquivos.Where(a => !a.EmPrototipo && a.Status != "Ignorado")) prBody += $"- `{f.Caminho}`\n";
                prBody += $"\n## Atenção\nEsta contribuição altera arquivos fora da área de protótipos. Revise com cuidado antes do merge.\n\n";
            }
            else
            {
                prBody += $"✅ Todos os arquivos alterados estão dentro da área de protótipos.\n\n";
            }
            prBody += $"## Observação da pessoa autora\n{request.Descricao}";

            var prHead = $"{repoOwner}:{request.Branch}";
            var newPr = new NewPullRequest($"Envio Rápido: {safeTitulo}", prHead, _settings.BaseBranch) { Body = prBody };
            
            try
            {
                var pr = await client.PullRequest.Create(_settings.Owner, _settings.Repo, newPr);
                await _auditoriaService.RegistrarAsync("ENVIO_RAPIDO_PR_CRIADO", "EquipeEnvioPr", null, $"PR Branch criado: {pr.HtmlUrl}", usuario.IdentificadorFuncionario);
                return new CriarEnvioPrResponse { Sucesso = true, PullRequestUrl = pr.HtmlUrl, Mensagem = "Pull Request criado com sucesso a partir da branch!" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao criar PR da branch.");
                return new CriarEnvioPrResponse { Sucesso = false, Mensagem = $"Erro do GitHub: {ex.Message}" };
            }
        }
    }
}
