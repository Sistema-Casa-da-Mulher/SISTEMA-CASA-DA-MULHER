namespace CasaMulher.Api.DTOs;

public class AssistidaRequest
{
    public string Nome { get; set; } = string.Empty;
    public string Cpf { get; set; } = string.Empty;
    public string? DataNascimento { get; set; }
    public string? Telefone { get; set; }
    public bool SemTelefoneSeguro { get; set; }
    public string? ContatoSeguro { get; set; }
    public string? ContatoSeguroObservacao { get; set; }
    public string? Email { get; set; }
    public bool EnderecoSigiloso { get; set; }
    public string? Cep { get; set; }
    public string? Endereco { get; set; }
    public string? EnderecoNumero { get; set; }
    public string? Bairro { get; set; }
    public string? Cidade { get; set; }
    public string? Uf { get; set; }
    public string? Direcionamento { get; set; }
    public bool EncaminharJuridico { get; set; }
    public string? Observacoes { get; set; }
}

public class AlterarRestricaoAssistidaRequest
{
    public bool AcessoRestrito { get; set; }
    public string? Motivo { get; set; }
}

public class ImportarAssistidasNavegadorRequest
{
    public List<AssistidaNavegadorItem> Itens { get; set; } = [];
}

/// <summary>Formato antigo salvo no localStorage pela Recepção (chave casamulher_itaqua).</summary>
public class AssistidaNavegadorItem
{
    public string? Nome { get; set; }
    public string? Cpf { get; set; }
    public string? Telefone { get; set; }
    public string? DataNascimento { get; set; }
    public string? Curso { get; set; }
    public string? EmailAssistida { get; set; }
    public string? Email { get; set; }
    public string? Endereco { get; set; }
    public string? EnderecoNumero { get; set; }
    public string? Observacoes { get; set; }
}

public class ImportarAssistidasNavegadorResponse
{
    public int Importadas { get; set; }
    public int Ignoradas { get; set; }

    /// <summary>Posições dos itens incompletos ou com CPF inválido (não foram importados).</summary>
    public List<int> Invalidos { get; set; } = [];

    public string? AvisoSnapshot { get; set; }
}

/// <summary>
/// Dados básicos da assistida. Os nomes extras (curso, emailAssistida, funcionarioAtendimento*)
/// mantêm compatibilidade com as telas que antes liam o localStorage.
/// </summary>
public class AssistidaResponse
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Cpf { get; set; } = string.Empty;
    public string CpfClean { get; set; } = string.Empty;
    public string CpfMascarado { get; set; } = string.Empty;
    public string? DataNascimento { get; set; }
    public string? Telefone { get; set; }
    public bool SemTelefoneSeguro { get; set; }
    public string ContatoSeguro { get; set; } = string.Empty;
    public string? ContatoSeguroObservacao { get; set; }
    public string? Email { get; set; }
    public string? EmailAssistida { get; set; }
    public bool EnderecoSigiloso { get; set; }
    public string? Cep { get; set; }
    public string? Endereco { get; set; }
    public string? EnderecoNumero { get; set; }
    public string? Bairro { get; set; }
    public string? Cidade { get; set; }
    public string? Uf { get; set; }
    public string? Direcionamento { get; set; }
    public string? Curso { get; set; }
    public bool EncaminhadaJuridico { get; set; }
    public DateTime? EncaminhadaJuridicoEm { get; set; }
    public string? SituacaoJuridico { get; set; }
    public string? Observacoes { get; set; }
    public string Origem { get; set; } = string.Empty;
    public string FuncionarioAtendimentoId { get; set; } = string.Empty;
    public string FuncionarioAtendimentoNomeSnapshot { get; set; } = string.Empty;
    public string? FuncionarioAtendimentoEmailSnapshot { get; set; }
    public bool AcessoRestrito { get; set; }
    public string? MotivoRestricao { get; set; }
    public DateTime CriadoEm { get; set; }
    public DateTime AtualizadoEm { get; set; }
    public string? AvisoSnapshot { get; set; }
}

/// <summary>Visão mínima para a tela do Professor: só o necessário para chamada.</summary>
public class AlunaCursoResponse
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Telefone { get; set; }
    public string? Curso { get; set; }
    public DateTime DataAcolhimento { get; set; }
}
