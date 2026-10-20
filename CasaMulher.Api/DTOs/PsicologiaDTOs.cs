namespace CasaMulher.Api.DTOs;

public class ResponsavelPsicologiaResponse
{
    public string UserId { get; set; } = string.Empty;
    public string Identificador { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public string? Crp { get; set; }
}

public class PsicologiaAssistidaResumoResponse
{
    public int AssistidaId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string CpfMascarado { get; set; } = string.Empty;
    public string? Telefone { get; set; }
    public bool SemTelefoneSeguro { get; set; }
    public string ContatoSeguro { get; set; } = string.Empty;
    public bool AcessoRestrito { get; set; }
    public string Origem { get; set; } = string.Empty;
    public DateTime? EncaminhadaPsicologiaEm { get; set; }
    public int? ProntuarioId { get; set; }
    public string? NumeroProntuario { get; set; }
    public string? Situacao { get; set; }
    public string? Visibilidade { get; set; }
    public List<ResponsavelPsicologiaResponse> Responsaveis { get; set; } = [];
    public bool SouResponsavel { get; set; }
    public int TotalSessoes { get; set; }
    public DateTime? UltimaSessaoEm { get; set; }
    public DateTime? ProximaSessao { get; set; }
    public bool RiscoElevado { get; set; }
}

public class AdendoPsicologiaResponse
{
    public int Id { get; set; }
    public string Texto { get; set; } = string.Empty;
    public string AutorNome { get; set; } = string.Empty;
    public string AutorIdentificador { get; set; } = string.Empty;
    public DateTime CriadoEm { get; set; }
}

public class SessaoPsicologicaResponse
{
    public int Id { get; set; }
    public int Numero { get; set; }
    public DateTime CriadoEm { get; set; }
    public string ProfissionalNome { get; set; } = string.Empty;
    public string ProfissionalIdentificador { get; set; } = string.Empty;
    public string? ProfissionalCrp { get; set; }

    /// <summary>Verdadeiro quando quem consulta só pode ver que a sessão existiu.</summary>
    public bool ConteudoOculto { get; set; }

    public string? TipoAtendimento { get; set; }
    public string? Presenca { get; set; }
    public string? Modalidade { get; set; }
    public string? Demanda { get; set; }
    public string? Evolucao { get; set; }
    public string? Plano { get; set; }
    public DateTime? ProximaSessao { get; set; }
    public string? Risco { get; set; }
    public List<string> EncaminhamentosExternos { get; set; } = [];
    public string? EncaminhamentoExternoOutro { get; set; }
    public List<AdendoPsicologiaResponse> Adendos { get; set; } = [];
}

public class ProntuarioPsicologicoDetalheResponse
{
    public AssistidaResponse Assistida { get; set; } = new();
    public int? ProntuarioId { get; set; }
    public string? NumeroProntuario { get; set; }
    public string Visibilidade { get; set; } = string.Empty;
    public string Situacao { get; set; } = string.Empty;
    public string? MotivoEncerramento { get; set; }
    public DateTime? EncerradoEm { get; set; }
    public List<ResponsavelPsicologiaResponse> Responsaveis { get; set; } = [];
    public bool SouResponsavel { get; set; }
    public bool PodeVerConteudo { get; set; }
    public bool PodeRegistrar { get; set; }
    public bool PodeAtribuir { get; set; }
    public bool PodeAlterarVisibilidade { get; set; }
    public bool PodePedirAcessoEmergencia { get; set; }
    public DateTime? AcessoEmergenciaExpiraEm { get; set; }
    public string? RiscoAtual { get; set; }
    public bool RiscoElevado { get; set; }
    public bool ConsentimentoRegistrado { get; set; }
    public bool EmAtendimentoJuridico { get; set; }
    public int TotalSessoes { get; set; }
    public int TotalFaltas { get; set; }
    public int FaltasSeguidas { get; set; }
    public List<SessaoPsicologicaResponse> Sessoes { get; set; } = [];
}

public class NovaSessaoPsicologicaRequest
{
    public string TipoAtendimento { get; set; } = string.Empty;
    public string Presenca { get; set; } = string.Empty;
    public string? Modalidade { get; set; }
    public string? Demanda { get; set; }
    public string? Evolucao { get; set; }
    public string? Plano { get; set; }
    public string? ProximaSessao { get; set; }
    public string? Risco { get; set; }
    public List<string> EncaminhamentosExternos { get; set; } = [];
    public string? EncaminhamentoExternoOutro { get; set; }
    public bool ConsentimentoSigilo { get; set; }
    public bool EncaminharJuridico { get; set; }
}

public class SessaoRegistradaResponse
{
    public int SessaoId { get; set; }
    public string NumeroProntuario { get; set; } = string.Empty;
    public int NumeroSessao { get; set; }
    public DateTime CriadoEm { get; set; }
    public string Presenca { get; set; } = string.Empty;
    public string? AvisoSnapshot { get; set; }
}

/// <summary>Dados da declaração de comparecimento: nada do conteúdo da sessão.</summary>
public class DeclaracaoComparecimentoResponse
{
    public string AssistidaNome { get; set; } = string.Empty;
    public DateTime Data { get; set; }
    public string ProfissionalNome { get; set; } = string.Empty;
    public string? ProfissionalCrp { get; set; }
}

public class NovoAdendoPsicologiaRequest
{
    public string Texto { get; set; } = string.Empty;
}

public class AtribuirResponsavelPsicologiaRequest
{
    public string UserId { get; set; } = string.Empty;
}

public class AlterarVisibilidadeProntuarioRequest
{
    public string Visibilidade { get; set; } = string.Empty;
}

public class AcessoEmergenciaPsicologiaRequest
{
    public string Motivo { get; set; } = string.Empty;
}

public class EncerrarProntuarioRequest
{
    public string Motivo { get; set; } = string.Empty;
}

public class IndicadoresPsicologiaResponse
{
    public int Fila { get; set; }
    public int EmAcompanhamento { get; set; }
    public int Encerrados { get; set; }
    public int SessoesNoMes { get; set; }
    public int FaltasNoMes { get; set; }
    public int RiscoElevado { get; set; }
    public Dictionary<string, int> EncaminhamentosNoMes { get; set; } = [];
}
