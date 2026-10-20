namespace CasaMulher.Api.Models;

/// <summary>
/// Cadastro único da mulher atendida, compartilhado entre Recepção, Jurídico e demais áreas.
/// Os dados sensíveis de cada área (ex.: relato jurídico) ficam em tabelas próprias.
/// </summary>
public class Assistida
{
    public int Id { get; set; }

    public string Nome { get; set; } = string.Empty;

    /// <summary>CPF somente com dígitos (11). Único no sistema.</summary>
    public string Cpf { get; set; } = string.Empty;

    public DateOnly? DataNascimento { get; set; }

    public string? Telefone { get; set; }

    /// <summary>Marcado quando a assistida não tem um telefone seguro para contato.</summary>
    public bool SemTelefoneSeguro { get; set; }

    /// <summary>pode_ligar, nao_ligar ou horario.</summary>
    public string ContatoSeguro { get; set; } = AssistidaContatoSeguro.PodeLigar;

    public string? ContatoSeguroObservacao { get; set; }

    public string? Email { get; set; }

    /// <summary>Endereço não deve ser informado (ex.: casa-abrigo de endereço sigiloso).</summary>
    public bool EnderecoSigiloso { get; set; }

    public string? Cep { get; set; }

    public string? Endereco { get; set; }

    public string? EnderecoNumero { get; set; }

    public string? Bairro { get; set; }

    public string? Cidade { get; set; }

    public string? Uf { get; set; }

    /// <summary>Direcionamento principal feito no acolhimento (curso ou assistência).</summary>
    public string? Direcionamento { get; set; }

    /// <summary>Preenchido quando a assistida é encaminhada ao Jurídico.</summary>
    public DateTime? EncaminhadaJuridicoEm { get; set; }

    /// <summary>Preenchido quando a assistida é encaminhada à Psicologia.</summary>
    public DateTime? EncaminhadaPsicologiaEm { get; set; }

    public string? Observacoes { get; set; }

    /// <summary>recepcao ou juridico: por qual porta o cadastro entrou.</summary>
    public string Origem { get; set; } = AssistidaOrigem.Recepcao;

    public string CadastradaPorUserId { get; set; } = string.Empty;

    public string CadastradaPorIdentificador { get; set; } = string.Empty;

    public string CadastradaPorNome { get; set; } = string.Empty;

    public string? CadastradaPorEmail { get; set; }

    /// <summary>Acesso restrito: não aparece para Recepção, Professor e demais perfis.</summary>
    public bool AcessoRestrito { get; set; }

    public string? MotivoRestricao { get; set; }

    public string? RestricaoAlteradaPorIdentificador { get; set; }

    public DateTime? RestricaoAlteradaEm { get; set; }

    /// <summary>Remoção lógica: o histórico jurídico nunca é apagado junto.</summary>
    public bool Ativa { get; set; } = true;

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    public DateTime AtualizadoEm { get; set; } = DateTime.UtcNow;
}

public static class AssistidaOrigem
{
    public const string Recepcao = "recepcao";
    public const string Juridico = "juridico";
    public const string Psicologia = "psicologia";
}

public static class AssistidaContatoSeguro
{
    public const string PodeLigar = "pode_ligar";
    public const string NaoLigar = "nao_ligar";
    public const string Horario = "horario";

    public static readonly string[] Todos = [PodeLigar, NaoLigar, Horario];
}

public static class AssistidaMotivoRestricao
{
    public const string SegredoJustica = "segredo_justica";
    public const string CasaAbrigo = "casa_abrigo";
    public const string RiscoGrave = "risco_grave";
    public const string PedidoAssistida = "pedido_assistida";

    public static readonly string[] Todos = [SegredoJustica, CasaAbrigo, RiscoGrave, PedidoAssistida];
}
