namespace CasaMulher.Api.DTOs;

public class ResponsavelJuridicoResponse
{
    public string UserId { get; set; } = string.Empty;
    public string Identificador { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
}

public class JuridicoAssistidaResumoResponse
{
    public int AssistidaId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string CpfMascarado { get; set; } = string.Empty;
    public string? Telefone { get; set; }
    public bool SemTelefoneSeguro { get; set; }
    public string ContatoSeguro { get; set; } = string.Empty;
    public bool AcessoRestrito { get; set; }
    public string? MotivoRestricao { get; set; }
    public string Origem { get; set; } = string.Empty;
    public DateTime? EncaminhadaJuridicoEm { get; set; }
    public int? CasoId { get; set; }
    public string? Situacao { get; set; }
    public string? Visibilidade { get; set; }
    public List<ResponsavelJuridicoResponse> Responsaveis { get; set; } = [];
    public bool SouResponsavel { get; set; }
    public int TotalAtendimentos { get; set; }
    public DateTime? UltimoAtendimentoEm { get; set; }
    public string? ProximoRetorno { get; set; }
}

public class AdendoJuridicoResponse
{
    public int Id { get; set; }
    public string Texto { get; set; } = string.Empty;
    public string AutorNome { get; set; } = string.Empty;
    public string AutorIdentificador { get; set; } = string.Empty;
    public DateTime CriadoEm { get; set; }
}

public class AtendimentoJuridicoResponse
{
    public int Id { get; set; }
    public string Protocolo { get; set; } = string.Empty;
    public DateTime CriadoEm { get; set; }
    public string ProfissionalNome { get; set; } = string.Empty;
    public string ProfissionalIdentificador { get; set; } = string.Empty;
    public string Situacao { get; set; } = string.Empty;

    /// <summary>Verdadeiro quando quem consulta só pode ver os metadados.</summary>
    public bool ConteudoOculto { get; set; }

    public string? TipoDemanda { get; set; }
    public string? TipoDemandaOutro { get; set; }
    public bool? TemMedidaProtetiva { get; set; }
    public string? NumeroProcesso { get; set; }
    public string? Vara { get; set; }
    public List<string> Encaminhamentos { get; set; } = [];
    public string? EncaminhamentoOutro { get; set; }
    public string? Relato { get; set; }
    public string? ProximoPasso { get; set; }
    public string? DataRetorno { get; set; }
    public List<AdendoJuridicoResponse> Adendos { get; set; } = [];
}

public class CasoJuridicoDetalheResponse
{
    public AssistidaResponse Assistida { get; set; } = new();
    public int? CasoId { get; set; }
    public string Visibilidade { get; set; } = string.Empty;
    public string Situacao { get; set; } = string.Empty;
    public List<ResponsavelJuridicoResponse> Responsaveis { get; set; } = [];
    public bool SouResponsavel { get; set; }
    public bool PodeVerConteudo { get; set; }
    public bool PodeRegistrar { get; set; }
    public bool PodeAtribuir { get; set; }
    public bool PodeAlterarVisibilidade { get; set; }
    public bool PodePedirAcessoEmergencia { get; set; }
    public DateTime? AcessoEmergenciaExpiraEm { get; set; }

    /// <summary>Só o fato de estar em acompanhamento na Psicologia, sem conteúdo.</summary>
    public bool AcompanhamentoPsicologico { get; set; }

    /// <summary>Alerta de risco alto/iminente registrado pela Psicologia, sem detalhes.</summary>
    public bool AlertaRiscoPsicologico { get; set; }

    public bool EncaminhadaPsicologia { get; set; }

    public List<AtendimentoJuridicoResponse> Atendimentos { get; set; } = [];
}

public class NovoAtendimentoJuridicoRequest
{
    public string TipoDemanda { get; set; } = string.Empty;
    public string? TipoDemandaOutro { get; set; }
    public bool? TemMedidaProtetiva { get; set; }
    public string? NumeroProcesso { get; set; }
    public string? Vara { get; set; }
    public List<string> Encaminhamentos { get; set; } = [];
    public string? EncaminhamentoOutro { get; set; }
    public string Relato { get; set; } = string.Empty;
    public string? ProximoPasso { get; set; }
    public string? DataRetorno { get; set; }
    public string? Situacao { get; set; }
}

/// <summary>Resposta do registro: só o necessário para o comprovante, sem o relato.</summary>
public class AtendimentoRegistradoResponse
{
    public int AtendimentoId { get; set; }
    public string Protocolo { get; set; } = string.Empty;
    public DateTime CriadoEm { get; set; }
    public string AssistidaNome { get; set; } = string.Empty;
    public string ProfissionalNome { get; set; } = string.Empty;
    public string? DataRetorno { get; set; }
    public string? AvisoSnapshot { get; set; }
}

public class NovoAdendoJuridicoRequest
{
    public string Texto { get; set; } = string.Empty;
}

public class AtribuirResponsavelJuridicoRequest
{
    public string UserId { get; set; } = string.Empty;
}

public class AlterarVisibilidadeCasoRequest
{
    public string Visibilidade { get; set; } = string.Empty;
}

public class AcessoEmergenciaJuridicoRequest
{
    public string Motivo { get; set; } = string.Empty;
}

public class IndicadoresJuridicoResponse
{
    public int Fila { get; set; }
    public int CasosEmAndamento { get; set; }
    public int CasosEncerrados { get; set; }
    public int AtendimentosNoMes { get; set; }
    public int RetornosProximos7Dias { get; set; }
    public Dictionary<string, int> AtendimentosPorTipo { get; set; } = [];
}
