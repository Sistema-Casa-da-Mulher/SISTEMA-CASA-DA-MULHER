namespace CasaMulher.Api.Models;

/// <summary>
/// Registro imutável do aceite do Termo de Confidencialidade e Proteção de Dados.
/// Cada versão do termo exige um novo aceite; registros antigos nunca são alterados.
/// </summary>
public class TermoSigiloAceite
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string IdentificadorFuncionario { get; set; } = string.Empty;

    public string Perfil { get; set; } = string.Empty;

    /// <summary>Versão do termo aceita (ver <c>TermoSigilo.Versao</c>).</summary>
    public string VersaoTermo { get; set; } = string.Empty;

    /// <summary>SHA-256 do texto exato exibido, para provar qual conteúdo foi aceito.</summary>
    public string HashTermo { get; set; } = string.Empty;

    /// <summary>Nome completo digitado pela funcionária como assinatura.</summary>
    public string NomeAssinado { get; set; } = string.Empty;

    public DateTime AceitoEm { get; set; } = DateTime.UtcNow;

    public string? EnderecoIp { get; set; }

    public string? UserAgent { get; set; }
}
