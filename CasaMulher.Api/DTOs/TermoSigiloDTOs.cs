namespace CasaMulher.Api.DTOs;

public class AceitarTermoSigiloRequest
{
    public string? Versao { get; set; }

    public string? NomeAssinado { get; set; }

    public List<bool>? Confirmacoes { get; set; }
}

public class TermoSigiloSecaoResponse
{
    public string Titulo { get; set; } = string.Empty;

    public IReadOnlyList<string> Paragrafos { get; set; } = Array.Empty<string>();
}

public class TermoSigiloResponse
{
    public string Versao { get; set; } = string.Empty;

    public string Titulo { get; set; } = string.Empty;

    public string Hash { get; set; } = string.Empty;

    public IReadOnlyList<string> Resumo { get; set; } = Array.Empty<string>();

    public List<TermoSigiloSecaoResponse> Secoes { get; set; } = new();

    public IReadOnlyList<string> Confirmacoes { get; set; } = Array.Empty<string>();

    public bool Obrigatorio { get; set; }

    public bool Aceito { get; set; }

    public DateTime? AceitoEm { get; set; }

    public string NomeCompleto { get; set; } = string.Empty;

    public string IdentificadorFuncionario { get; set; } = string.Empty;

    public string? AvisoSnapshot { get; set; }
}
