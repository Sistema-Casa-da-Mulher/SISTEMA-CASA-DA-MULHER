using System.Globalization;
using System.Net.Mail;
using CasaMulher.Api.Data;
using CasaMulher.Api.DTOs;
using CasaMulher.Api.Models;
using CasaMulher.Api.Security;
using CasaMulher.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CasaMulher.Api.Controllers;

/// <summary>
/// Cadastro único das mulheres atendidas. Substitui o antigo armazenamento no
/// localStorage da Recepção: tudo passa pelo servidor, com login, perfil, termo de
/// sigilo e auditoria. Assistidas com acesso restrito só aparecem para Jurídico e ADM.
/// </summary>
[ApiController]
[Route("api/assistidas")]
[Authorize]
public class AssistidasController : ControllerBase
{
    public const string MensagemCpfRestrito =
        "Já existe um cadastro com acesso restrito para este CPF. Encaminhe a assistida para o Jurídico ou a coordenação.";

    private readonly AppDbContext _dbContext;
    private readonly AssistidasService _assistidas;
    private readonly IAuditoriaService _auditoria;
    private readonly SecuritySnapshotPersistenceService _snapshot;
    private readonly CursosService _cursos;

    public AssistidasController(
        AppDbContext dbContext,
        AssistidasService assistidas,
        IAuditoriaService auditoria,
        SecuritySnapshotPersistenceService snapshot,
        CursosService cursos)
    {
        _dbContext = dbContext;
        _assistidas = assistidas;
        _auditoria = auditoria;
        _snapshot = snapshot;
        _cursos = cursos;
    }

    public const string MensagemSemDirecionamento = "Escolha pelo menos um curso ou um encaminhamento no Direcionamento.";

    [HttpGet]
    [Authorize(Policy = PoliticasAcesso.CadastroAssistidas)]
    public async Task<ActionResult<List<AssistidaResponse>>> Listar([FromQuery] string? busca, CancellationToken cancellationToken)
    {
        var usuario = await _assistidas.ObterUsuarioAsync(User, cancellationToken);
        if (usuario is null) return Unauthorized();

        var query = ConsultaVisivel(usuario);
        var lista = await query.OrderBy(a => a.Nome).ToListAsync(cancellationToken);
        lista = FiltrarPorBusca(lista, busca);

        var situacoes = await ObterSituacoesJuridicasAsync(lista.Select(a => a.Id).ToList(), cancellationToken);
        return Ok(lista.Select(a => MapearResposta(a, situacoes.GetValueOrDefault(a.Id), usuario.Perfil)).ToList());
    }

    [HttpGet("{id:int}")]
    [Authorize(Policy = PoliticasAcesso.CadastroAssistidas)]
    public async Task<ActionResult<AssistidaResponse>> Obter(int id, CancellationToken cancellationToken)
    {
        var usuario = await _assistidas.ObterUsuarioAsync(User, cancellationToken);
        if (usuario is null) return Unauthorized();

        var assistida = await ConsultaVisivel(usuario).SingleOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (assistida is null) return NotFound(new { mensagem = "Assistida não encontrada." });

        var situacoes = await ObterSituacoesJuridicasAsync([assistida.Id], cancellationToken);
        return Ok(MapearResposta(assistida, situacoes.GetValueOrDefault(assistida.Id), usuario.Perfil));
    }

    [HttpPost]
    [Authorize(Policy = PoliticasAcesso.CadastroAssistidas)]
    public async Task<ActionResult<AssistidaResponse>> Criar(AssistidaRequest request, CancellationToken cancellationToken)
    {
        var usuario = await _assistidas.ObterUsuarioAsync(User, cancellationToken);
        if (usuario is null) return Unauthorized();

        var erro = Validar(request);
        if (erro is not null) return BadRequest(new { mensagem = erro });

        var cpf = AssistidasService.SomenteDigitos(request.Cpf);
        var existente = await _dbContext.Assistidas.Include(a => a.Cursos).ThenInclude(c => c.Curso).SingleOrDefaultAsync(a => a.Cpf == cpf, cancellationToken);
        var reativada = false;

        if (existente is not null && existente.Ativa)
        {
            if (existente.AcessoRestrito && !AssistidasService.PodeVerRestritas(usuario.Perfil))
            {
                return Conflict(new { mensagem = MensagemCpfRestrito, restrito = true });
            }

            return Conflict(new
            {
                mensagem = "Já existe uma mulher atendida com este CPF. O cadastro duplicado foi bloqueado.",
                assistidaId = existente.Id
            });
        }

        var assistida = existente ?? new Assistida();
        if (existente is not null)
        {
            // CPF de um cadastro removido: reaproveita o registro para manter o histórico ligado a ele.
            reativada = true;
            assistida.Ativa = true;
            if (assistida.AcessoRestrito && !AssistidasService.PodeVerRestritas(usuario.Perfil))
            {
                return Conflict(new { mensagem = MensagemCpfRestrito, restrito = true });
            }
        }

        AplicarDados(assistida, request, cpf);
        assistida.Origem = usuario.Perfil == PerfisAcesso.Psicologia
            ? AssistidaOrigem.Psicologia
            : AssistidasService.AtuaComoJuridico(usuario.Perfil) ? AssistidaOrigem.Juridico : AssistidaOrigem.Recepcao;
        assistida.CadastradaPorUserId = usuario.UserId;
        assistida.CadastradaPorIdentificador = usuario.Identificador;
        assistida.CadastradaPorNome = usuario.Nome;
        assistida.CadastradaPorEmail = usuario.Email;
        assistida.CriadoEm = reativada ? assistida.CriadoEm : DateTime.UtcNow;
        assistida.AtualizadoEm = DateTime.UtcNow;

        if (assistida.Origem == AssistidaOrigem.Juridico)
        {
            assistida.EncaminhadaJuridicoEm ??= DateTime.UtcNow;
        }

        if (assistida.Origem == AssistidaOrigem.Psicologia)
        {
            assistida.EncaminhadaPsicologiaEm ??= DateTime.UtcNow;
        }

        var erroCursos = await AplicarCursosAsync(assistida, request.CursoIds, cancellationToken);
        if (erroCursos is not null) return BadRequest(new { mensagem = erroCursos });
        if (!TemDirecionamento(assistida)) return BadRequest(new { mensagem = MensagemSemDirecionamento });

        if (existente is null)
        {
            _dbContext.Assistidas.Add(assistida);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditoria.RegistrarAsync(
            reativada ? "ASSISTIDA_REATIVADA" : "ASSISTIDA_CADASTRADA",
            "Assistida",
            assistida.Id.ToString(CultureInfo.InvariantCulture),
            $"{usuario.Identificador} cadastrou a assistida #{assistida.Id} (origem: {assistida.Origem}).");

        var resposta = MapearResposta(assistida, null, usuario.Perfil);
        resposta.AvisoSnapshot = (await _snapshot.PersistAsync("assistida_cadastrada", CancellationToken.None)).AvisoSnapshot;
        return CreatedAtAction(nameof(Obter), new { id = assistida.Id }, resposta);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = PoliticasAcesso.CadastroAssistidas)]
    public async Task<ActionResult<AssistidaResponse>> Atualizar(int id, AssistidaRequest request, CancellationToken cancellationToken)
    {
        var usuario = await _assistidas.ObterUsuarioAsync(User, cancellationToken);
        if (usuario is null) return Unauthorized();

        var assistida = await ConsultaVisivel(usuario).SingleOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (assistida is null) return NotFound(new { mensagem = "Assistida não encontrada." });

        var erro = Validar(request);
        if (erro is not null) return BadRequest(new { mensagem = erro });

        var cpf = AssistidasService.SomenteDigitos(request.Cpf);
        if (cpf != assistida.Cpf && await _dbContext.Assistidas.AnyAsync(a => a.Cpf == cpf && a.Id != id, cancellationToken))
        {
            return Conflict(new { mensagem = "Já existe outro cadastro com este CPF." });
        }

        AplicarDados(assistida, request, cpf);
        var erroCursos = await AplicarCursosAsync(assistida, request.CursoIds, cancellationToken);
        if (erroCursos is not null) return BadRequest(new { mensagem = erroCursos });
        if (!TemDirecionamento(assistida)) return BadRequest(new { mensagem = MensagemSemDirecionamento });

        assistida.AtualizadoEm = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditoria.RegistrarAsync(
            "ASSISTIDA_ATUALIZADA",
            "Assistida",
            assistida.Id.ToString(CultureInfo.InvariantCulture),
            $"{usuario.Identificador} atualizou os dados básicos da assistida #{assistida.Id}.");

        var situacoes = await ObterSituacoesJuridicasAsync([assistida.Id], cancellationToken);
        var resposta = MapearResposta(assistida, situacoes.GetValueOrDefault(assistida.Id), usuario.Perfil);
        resposta.AvisoSnapshot = (await _snapshot.PersistAsync("assistida_atualizada", CancellationToken.None)).AvisoSnapshot;
        return Ok(resposta);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = PoliticasAcesso.CadastroAssistidas)]
    public async Task<IActionResult> Remover(int id, CancellationToken cancellationToken)
    {
        var usuario = await _assistidas.ObterUsuarioAsync(User, cancellationToken);
        if (usuario is null) return Unauthorized();

        if (!AssistidasService.PodeRemover(usuario.Perfil))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { mensagem = "Seu perfil não pode remover cadastros." });
        }

        var assistida = await ConsultaVisivel(usuario).SingleOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (assistida is null) return NotFound(new { mensagem = "Assistida não encontrada." });

        assistida.Ativa = false;
        assistida.AtualizadoEm = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditoria.RegistrarAsync(
            "ASSISTIDA_REMOVIDA",
            "Assistida",
            assistida.Id.ToString(CultureInfo.InvariantCulture),
            $"{usuario.Identificador} removeu da listagem o cadastro da assistida #{assistida.Id}.");

        var snapshot = await _snapshot.PersistAsync("assistida_removida", CancellationToken.None);
        return Ok(new { mensagem = "Registro removido.", avisoSnapshot = snapshot.AvisoSnapshot });
    }

    [HttpPut("{id:int}/restricao")]
    [Authorize(Policy = PoliticasAcesso.CadastroAssistidas)]
    public async Task<ActionResult<AssistidaResponse>> AlterarRestricao(int id, AlterarRestricaoAssistidaRequest request, CancellationToken cancellationToken)
    {
        var usuario = await _assistidas.ObterUsuarioAsync(User, cancellationToken);
        if (usuario is null) return Unauthorized();

        if (!AssistidasService.PodeAlterarRestricao(usuario.Perfil))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { mensagem = "Somente Jurídico e coordenação podem alterar o acesso restrito." });
        }

        var assistida = await _dbContext.Assistidas.SingleOrDefaultAsync(a => a.Id == id && a.Ativa, cancellationToken);
        if (assistida is null) return NotFound(new { mensagem = "Assistida não encontrada." });

        if (request.AcessoRestrito && !AssistidaMotivoRestricao.Todos.Contains(request.Motivo))
        {
            return BadRequest(new { mensagem = "Informe o motivo do acesso restrito." });
        }

        assistida.AcessoRestrito = request.AcessoRestrito;
        assistida.MotivoRestricao = request.AcessoRestrito ? request.Motivo : null;
        assistida.RestricaoAlteradaPorIdentificador = usuario.Identificador;
        assistida.RestricaoAlteradaEm = DateTime.UtcNow;
        assistida.AtualizadoEm = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditoria.RegistrarAsync(
            request.AcessoRestrito ? "ASSISTIDA_ACESSO_RESTRITO_ATIVADO" : "ASSISTIDA_ACESSO_RESTRITO_REMOVIDO",
            "Assistida",
            assistida.Id.ToString(CultureInfo.InvariantCulture),
            request.AcessoRestrito
                ? $"{usuario.Identificador} marcou a assistida #{assistida.Id} como acesso restrito (motivo: {request.Motivo})."
                : $"{usuario.Identificador} removeu o acesso restrito da assistida #{assistida.Id}.");

        var situacoes = await ObterSituacoesJuridicasAsync([assistida.Id], cancellationToken);
        var resposta = MapearResposta(assistida, situacoes.GetValueOrDefault(assistida.Id), usuario.Perfil);
        resposta.AvisoSnapshot = (await _snapshot.PersistAsync("assistida_restricao", CancellationToken.None)).AvisoSnapshot;
        return Ok(resposta);
    }

    /// <summary>
    /// Encaminhamento interno: coloca a assistida na fila do Jurídico ou da Psicologia.
    /// Não expõe nada do atendimento de origem.
    /// </summary>
    [HttpPost("{id:int}/encaminhar")]
    [Authorize(Policy = PoliticasAcesso.CadastroAssistidas)]
    public async Task<IActionResult> Encaminhar(int id, EncaminharAssistidaRequest request, CancellationToken cancellationToken)
    {
        var usuario = await _assistidas.ObterUsuarioAsync(User, cancellationToken);
        if (usuario is null) return Unauthorized();

        var assistida = await ConsultaVisivel(usuario).SingleOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (assistida is null) return NotFound(new { mensagem = "Assistida não encontrada." });

        string rotulo;
        switch (request.Area)
        {
            case AssistidaOrigem.Juridico:
                assistida.EncaminhadaJuridicoEm ??= DateTime.UtcNow;
                rotulo = "Jurídico";
                break;
            case AssistidaOrigem.Psicologia:
                assistida.EncaminhadaPsicologiaEm ??= DateTime.UtcNow;
                rotulo = "Psicologia";
                break;
            default:
                return BadRequest(new { mensagem = "Área de encaminhamento inválida." });
        }

        assistida.AtualizadoEm = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditoria.RegistrarAsync(
            "ASSISTIDA_ENCAMINHADA",
            "Assistida",
            assistida.Id.ToString(CultureInfo.InvariantCulture),
            $"{usuario.Identificador} encaminhou a assistida #{assistida.Id} para {rotulo}.");

        var snapshot = await _snapshot.PersistAsync("assistida_encaminhada", CancellationToken.None);
        return Ok(new { mensagem = $"Encaminhada para {rotulo}.", avisoSnapshot = snapshot.AvisoSnapshot });
    }

    /// <summary>Migração única dos cadastros que ficaram salvos no navegador da Recepção.</summary>
    [HttpPost("importar-navegador")]
    [Authorize(Policy = PoliticasAcesso.AcessoRecepcao)]
    public async Task<ActionResult<ImportarAssistidasNavegadorResponse>> ImportarNavegador(
        ImportarAssistidasNavegadorRequest request,
        CancellationToken cancellationToken)
    {
        var usuario = await _assistidas.ObterUsuarioAsync(User, cancellationToken);
        if (usuario is null) return Unauthorized();

        var cpfsExistentes = (await _dbContext.Assistidas.Select(a => a.Cpf).ToListAsync(cancellationToken)).ToHashSet();
        var importadas = 0;
        var ignoradas = 0;
        var agora = DateTime.UtcNow;

        var invalidos = new List<int>();

        foreach (var (item, indice) in request.Itens.Take(2000).Select((item, indice) => (item, indice)))
        {
            var cpf = AssistidasService.SomenteDigitos(item.Cpf);
            var nome = item.Nome?.Trim();

            if (string.IsNullOrWhiteSpace(nome) || !AssistidasService.CpfValido(cpf))
            {
                // Fica no navegador para a Recepção corrigir; não é descartado.
                invalidos.Add(indice);
                ignoradas++;
                continue;
            }

            if (!cpfsExistentes.Add(cpf))
            {
                ignoradas++;
                continue;
            }

            var curso = string.IsNullOrWhiteSpace(item.Curso) ? null : Limitar(item.Curso.Trim(), 60);
            _dbContext.Assistidas.Add(new Assistida
            {
                Nome = Limitar(nome, 160)!,
                Cpf = cpf,
                Telefone = Limitar(item.Telefone?.Trim(), 20),
                DataNascimento = DateOnly.TryParse(item.DataNascimento, CultureInfo.InvariantCulture, out var data) ? data : null,
                Direcionamento = curso,
                EncaminhadaJuridicoEm = string.Equals(curso, AssistidasService.DirecionamentoJuridico, StringComparison.OrdinalIgnoreCase) ? agora : null,
                EncaminhadaPsicologiaEm = string.Equals(curso, AssistidasService.DirecionamentoPsicologia, StringComparison.OrdinalIgnoreCase) ? agora : null,
                Email = Limitar((item.EmailAssistida ?? item.Email)?.Trim(), 256),
                Endereco = Limitar(item.Endereco?.Trim(), 200),
                EnderecoNumero = Limitar(item.EnderecoNumero?.Trim(), 60),
                Observacoes = Limitar(item.Observacoes?.Trim(), 1000),
                Origem = AssistidaOrigem.Recepcao,
                CadastradaPorUserId = usuario.UserId,
                CadastradaPorIdentificador = usuario.Identificador,
                CadastradaPorNome = usuario.Nome,
                CadastradaPorEmail = usuario.Email,
                CriadoEm = agora,
                AtualizadoEm = agora
            });
            importadas++;
        }

        string? aviso = null;
        if (importadas > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            await _cursos.MigrarDadosLegadosAsync(cancellationToken);
            await _auditoria.RegistrarAsync(
                "ASSISTIDAS_IMPORTADAS_NAVEGADOR",
                "Assistida",
                null,
                $"{usuario.Identificador} importou {importadas} cadastro(s) salvos no navegador para o servidor.");
            aviso = (await _snapshot.PersistAsync("assistidas_importadas", CancellationToken.None)).AvisoSnapshot;
        }

        return Ok(new ImportarAssistidasNavegadorResponse { Importadas = importadas, Ignoradas = ignoradas, Invalidos = invalidos, AvisoSnapshot = aviso });
    }

    private IQueryable<Assistida> ConsultaVisivel(UsuarioAtuante usuario)
    {
        var query = _dbContext.Assistidas.Include(a => a.Cursos).ThenInclude(c => c.Curso).Where(a => a.Ativa);
        return AssistidasService.PodeVerRestritas(usuario.Perfil) ? query : query.Where(a => !a.AcessoRestrito);
    }

    private static List<Assistida> FiltrarPorBusca(List<Assistida> lista, string? busca)
    {
        var termo = AssistidasService.NormalizarTexto(busca);
        if (termo.Length == 0) return lista;

        var digitos = AssistidasService.SomenteDigitos(termo);
        var temLetras = termo.Any(char.IsLetter);

        return lista.Where(a =>
        {
            if (!temLetras && digitos.Length >= 3)
            {
                return a.Cpf.Contains(digitos) || AssistidasService.SomenteDigitos(a.Telefone).Contains(digitos);
            }

            return temLetras && AssistidasService.NormalizarTexto(a.Nome).Contains(termo);
        }).ToList();
    }

    private async Task<Dictionary<int, string>> ObterSituacoesJuridicasAsync(List<int> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0) return [];

        var casos = await _dbContext.CasosJuridicos.AsNoTracking()
            .Where(c => ids.Contains(c.AssistidaId))
            .Select(c => new { c.AssistidaId, c.Situacao, Total = c.Atendimentos.Count })
            .ToListAsync(cancellationToken);

        // Para a Recepção só interessa saber se já foi atendida; o conteúdo fica no Jurídico.
        return casos.ToDictionary(c => c.AssistidaId, c => c.Total == 0 ? "encaminhada" : c.Situacao == CasoJuridicoSituacao.Encerrado ? "encerrado" : "em_atendimento");
    }

    private static string? Validar(AssistidaRequest request)
    {
        var nome = request.Nome?.Trim() ?? string.Empty;
        if (nome.Length < 3 || nome.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length < 2)
        {
            return "Informe o nome completo (nome e sobrenome).";
        }

        if (nome.Length > 160) return "Nome muito longo.";
        if (!AssistidasService.CpfValido(request.Cpf)) return "CPF inválido. Confira os números informados.";

        if (!string.IsNullOrWhiteSpace(request.DataNascimento))
        {
            if (!DateOnly.TryParse(request.DataNascimento, CultureInfo.InvariantCulture, out var nascimento))
            {
                return "Data de nascimento inválida.";
            }

            var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
            if (nascimento > hoje || nascimento < hoje.AddYears(-120))
            {
                return "Data de nascimento fora do intervalo válido.";
            }
        }

        var telefone = AssistidasService.SomenteDigitos(request.Telefone);
        if (!request.SemTelefoneSeguro && telefone.Length > 0 && telefone.Length is not (10 or 11))
        {
            return "Telefone inválido. Informe DDD + número (10 ou 11 dígitos).";
        }

        if (!string.IsNullOrWhiteSpace(request.Email) && !MailAddress.TryCreate(request.Email.Trim(), out _))
        {
            return "E-mail inválido.";
        }

        if (!string.IsNullOrWhiteSpace(request.ContatoSeguro) && !AssistidaContatoSeguro.Todos.Contains(request.ContatoSeguro))
        {
            return "Opção de contato seguro inválida.";
        }

        var cep = AssistidasService.SomenteDigitos(request.Cep);
        if (!request.EnderecoSigiloso && cep.Length > 0 && cep.Length != 8)
        {
            return "CEP inválido (8 dígitos).";
        }

        return null;
    }

    /// <summary>Sincroniza os cursos do direcionamento. Curso desativado só fica se já estava ligado.</summary>
    private async Task<string?> AplicarCursosAsync(Assistida assistida, List<int>? cursoIds, CancellationToken cancellationToken)
    {
        if (cursoIds is null) return null;

        var ids = cursoIds.Distinct().ToList();
        var cursos = await _dbContext.Cursos.Where(c => ids.Contains(c.Id)).ToListAsync(cancellationToken);
        if (cursos.Count != ids.Count) return "Curso inválido no direcionamento.";

        var jaLigados = assistida.Cursos.Select(c => c.CursoId).ToHashSet();
        if (cursos.Any(c => !c.Ativo && !jaLigados.Contains(c.Id))) return "Este curso foi desativado e não recebe novas alunas.";

        foreach (var vinculo in assistida.Cursos.Where(c => !ids.Contains(c.CursoId)).ToList())
        {
            assistida.Cursos.Remove(vinculo);
        }

        foreach (var curso in cursos.Where(c => !jaLigados.Contains(c.Id)))
        {
            assistida.Cursos.Add(new AssistidaCurso { CursoId = curso.Id, Curso = curso, CriadoEm = DateTime.UtcNow });
        }

        return null;
    }

    private static bool TemDirecionamento(Assistida a)
    {
        return a.Cursos.Count > 0
            || a.EncaminhadaJuridicoEm is not null
            || a.EncaminhadaPsicologiaEm is not null
            || a.EncaminhadaAssistenciaSocialEm is not null
            || !string.IsNullOrWhiteSpace(a.Direcionamento);
    }

    private static void AplicarDados(Assistida assistida, AssistidaRequest request, string cpf)
    {
        assistida.Nome = request.Nome.Trim();
        assistida.Cpf = cpf;
        assistida.DataNascimento = DateOnly.TryParse(request.DataNascimento, CultureInfo.InvariantCulture, out var data) ? data : null;
        assistida.SemTelefoneSeguro = request.SemTelefoneSeguro;
        assistida.Telefone = request.SemTelefoneSeguro ? null : Limitar(request.Telefone?.Trim(), 20);
        assistida.ContatoSeguro = request.SemTelefoneSeguro
            ? AssistidaContatoSeguro.NaoLigar
            : request.ContatoSeguro ?? AssistidaContatoSeguro.PodeLigar;
        assistida.ContatoSeguroObservacao = Limitar(request.ContatoSeguroObservacao?.Trim(), 300);
        assistida.Email = Limitar(request.Email?.Trim(), 256);
        assistida.EnderecoSigiloso = request.EnderecoSigiloso;

        if (request.EnderecoSigiloso)
        {
            // Endereço sigiloso: não guarda nenhum dado de localização.
            assistida.Cep = null;
            assistida.Endereco = null;
            assistida.EnderecoNumero = null;
            assistida.Bairro = null;
            assistida.Cidade = null;
            assistida.Uf = null;
        }
        else
        {
            var cep = AssistidasService.SomenteDigitos(request.Cep);
            assistida.Cep = cep.Length == 8 ? cep : null;
            assistida.Endereco = Limitar(request.Endereco?.Trim(), 200);
            assistida.EnderecoNumero = Limitar(request.EnderecoNumero?.Trim(), 60);
            assistida.Bairro = Limitar(request.Bairro?.Trim(), 120);
            assistida.Cidade = Limitar(request.Cidade?.Trim(), 120);
            assistida.Uf = Limitar(request.Uf?.Trim().ToUpperInvariant(), 2);
        }

        // O direcionamento em texto é do formato antigo; o novo usa CursoIds + encaminhamentos.
        assistida.Direcionamento = request.CursoIds is null ? Limitar(request.Direcionamento?.Trim(), 60) : null;
        assistida.Observacoes = Limitar(request.Observacoes?.Trim(), 1000);

        var encaminhar = request.EncaminharJuridico
            || string.Equals(assistida.Direcionamento, AssistidasService.DirecionamentoJuridico, StringComparison.OrdinalIgnoreCase);

        if (encaminhar)
        {
            assistida.EncaminhadaJuridicoEm ??= DateTime.UtcNow;
        }

        if (request.EncaminharPsicologia
            || string.Equals(assistida.Direcionamento, AssistidasService.DirecionamentoPsicologia, StringComparison.OrdinalIgnoreCase))
        {
            assistida.EncaminhadaPsicologiaEm ??= DateTime.UtcNow;
        }

        if (request.EncaminharAssistenciaSocial
            || string.Equals(assistida.Direcionamento, "AssistenteSocial", StringComparison.OrdinalIgnoreCase))
        {
            assistida.EncaminhadaAssistenciaSocialEm ??= DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Monta os dados básicos. Para quem não é da Psicologia, esconde qualquer pista de
    /// acompanhamento psicológico (quem cadastrou e o encaminhamento), conforme o sigilo.
    /// </summary>
    internal static AssistidaResponse MapearResposta(Assistida a, string? situacaoJuridico, string perfilViewer)
    {
        var resposta = MapearDados(a, situacaoJuridico);

        if (!AssistidasService.AtuaComoPsicologia(perfilViewer))
        {
            if (a.Origem == AssistidaOrigem.Psicologia)
            {
                resposta.Origem = "equipe_tecnica";
                resposta.FuncionarioAtendimentoId = string.Empty;
                resposta.FuncionarioAtendimentoNomeSnapshot = "Equipe técnica da Casa";
                resposta.FuncionarioAtendimentoEmailSnapshot = null;
            }

            // O Jurídico pode saber do encaminhamento; Recepção e demais perfis não.
            if (!AssistidasService.AtuaComoJuridico(perfilViewer))
            {
                resposta.EncaminhadaPsicologia = false;
            }
        }

        return resposta;
    }

    private static AssistidaResponse MapearDados(Assistida a, string? situacaoJuridico)
    {
        return new AssistidaResponse
        {
            Id = a.Id,
            Nome = a.Nome,
            Cpf = AssistidasService.FormatarCpf(a.Cpf),
            CpfClean = a.Cpf,
            CpfMascarado = AssistidasService.MascararCpf(a.Cpf),
            DataNascimento = a.DataNascimento?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Telefone = a.Telefone,
            SemTelefoneSeguro = a.SemTelefoneSeguro,
            ContatoSeguro = a.ContatoSeguro,
            ContatoSeguroObservacao = a.ContatoSeguroObservacao,
            Email = a.Email,
            EmailAssistida = a.Email,
            EnderecoSigiloso = a.EnderecoSigiloso,
            Cep = a.Cep,
            Endereco = a.Endereco,
            EnderecoNumero = a.EnderecoNumero,
            Bairro = a.Bairro,
            Cidade = a.Cidade,
            Uf = a.Uf,
            Direcionamento = NomesCursos(a) ?? a.Direcionamento,
            Curso = NomesCursos(a) ?? a.Direcionamento,
            Cursos = a.Cursos
                .Where(c => c.Curso is not null)
                .OrderBy(c => c.Curso!.Nome)
                .Select(c => new CursoResumoResponse { Id = c.CursoId, Nome = c.Curso!.Nome, Ativo = c.Curso.Ativo })
                .ToList(),
            EncaminhadaAssistenciaSocial = a.EncaminhadaAssistenciaSocialEm is not null,
            EncaminhadaJuridico = a.EncaminhadaJuridicoEm is not null,
            EncaminhadaJuridicoEm = a.EncaminhadaJuridicoEm,
            EncaminhadaPsicologia = a.EncaminhadaPsicologiaEm is not null,
            SituacaoJuridico = situacaoJuridico ?? (a.EncaminhadaJuridicoEm is not null ? "encaminhada" : null),
            Observacoes = a.Observacoes,
            Origem = a.Origem,
            FuncionarioAtendimentoId = a.CadastradaPorIdentificador,
            FuncionarioAtendimentoNomeSnapshot = a.CadastradaPorNome,
            FuncionarioAtendimentoEmailSnapshot = a.CadastradaPorEmail,
            AcessoRestrito = a.AcessoRestrito,
            MotivoRestricao = a.MotivoRestricao,
            CriadoEm = a.CriadoEm,
            AtualizadoEm = a.AtualizadoEm
        };
    }

    private static string? NomesCursos(Assistida a)
    {
        var nomes = a.Cursos.Where(c => c.Curso is not null).Select(c => c.Curso!.Nome).OrderBy(n => n).ToList();
        return nomes.Count == 0 ? null : string.Join(", ", nomes);
    }

    private static string? Limitar(string? valor, int maximo)
    {
        if (string.IsNullOrEmpty(valor)) return null;
        return valor.Length <= maximo ? valor : valor[..maximo];
    }
}
