namespace CasaMulher.Api.DTOs;

public class ResponsavelSocialResponse
{
    public string UserId { get; set; } = string.Empty;
    public string Identificador { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public string? Cress { get; set; }
}

public class MeuCressRequest
{
    public string Cress { get; set; } = string.Empty;
}

public class SocialAssistidaResumoResponse
{
    public int AssistidaId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string CpfMascarado { get; set; } = string.Empty;
    public string? Telefone { get; set; }
    public bool SemTelefoneSeguro { get; set; }
    public string ContatoSeguro { get; set; } = string.Empty;
    public bool AcessoRestrito { get; set; }
    public string Origem { get; set; } = string.Empty;
    public DateTime? EncaminhadaServicoSocialEm { get; set; }
    public int? ProntuarioId { get; set; }
    public string? NumeroProntuario { get; set; }
    public string? Situacao { get; set; }
    public string? Visibilidade { get; set; }
    public List<ResponsavelSocialResponse> Responsaveis { get; set; } = [];
    public bool SouResponsavel { get; set; }
    public int TotalAtendimentos { get; set; }
    public DateTime? UltimoAtendimentoEm { get; set; }
    public bool Vulnerabilidade { get; set; }
}

public class AdendoSocialResponse
{
    public int Id { get; set; }
    public string Texto { get; set; } = string.Empty;
    public string AutorNome { get; set; } = string.Empty;
    public string AutorIdentificador { get; set; } = string.Empty;
    public DateTime CriadoEm { get; set; }
}

public class AtendimentoSocialResponse
{
    public int Id { get; set; }
    public int Numero { get; set; }
    public DateTime RealizadoEm { get; set; }
    public DateTime CriadoEm { get; set; }
    public string ProfissionalNome { get; set; } = string.Empty;
    public string ProfissionalIdentificador { get; set; } = string.Empty;
    public string? ProfissionalCress { get; set; }

    /// <summary>Verdadeiro quando quem consulta só pode ver que o atendimento existiu.</summary>
    public bool ConteudoOculto { get; set; }

    public string? Tipo { get; set; }
    public string? TipoOutro { get; set; }
    public string? Relato { get; set; }
    public string? Providencias { get; set; }
    public bool PodeDeclaracao { get; set; }
    public int TotalAnexos { get; set; }
    public List<AdendoSocialResponse> Adendos { get; set; } = [];
}

public class DependenteDto
{
    public string Parentesco { get; set; } = string.Empty;
    public int? Idade { get; set; }
}

public class EstudoSocialResponse
{
    public int Id { get; set; }
    public int Versao { get; set; }
    public int? PessoasNoDomicilio { get; set; }
    public string? ComposicaoFamiliar { get; set; }
    public List<DependenteDto> Dependentes { get; set; } = [];
    public string? FaixaRenda { get; set; }
    public string? SituacaoTrabalho { get; set; }
    public string? Moradia { get; set; }
    public bool? ConviveComAgressor { get; set; }
    public string? Escolaridade { get; set; }
    public string? CadUnico { get; set; }
    public string? Nis { get; set; }
    public List<string> Beneficios { get; set; } = [];
    public string? BeneficioOutro { get; set; }
    public string? RedeApoio { get; set; }
    public List<string> Vulnerabilidades { get; set; } = [];
    public string? Observacoes { get; set; }
    public string ProfissionalNome { get; set; } = string.Empty;
    public string ProfissionalIdentificador { get; set; } = string.Empty;
    public DateTime CriadoEm { get; set; }
}

public class EstudoSocialRequest
{
    public int? PessoasNoDomicilio { get; set; }
    public string? ComposicaoFamiliar { get; set; }
    public List<DependenteDto> Dependentes { get; set; } = [];
    public string? FaixaRenda { get; set; }
    public string? SituacaoTrabalho { get; set; }
    public string? Moradia { get; set; }
    public bool? ConviveComAgressor { get; set; }
    public string? Escolaridade { get; set; }
    public string? CadUnico { get; set; }
    public string? Nis { get; set; }
    public List<string> Beneficios { get; set; } = [];
    public string? BeneficioOutro { get; set; }
    public string? RedeApoio { get; set; }
    public List<string> Vulnerabilidades { get; set; } = [];
    public string? Observacoes { get; set; }
}

public class EncaminhamentoRedeResponse
{
    public int Id { get; set; }
    public string Destino { get; set; } = string.Empty;
    public string? DestinoOutro { get; set; }
    public string Data { get; set; } = string.Empty;
    public string Motivo { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ObservacaoStatus { get; set; }
    public DateTime? StatusAtualizadoEm { get; set; }
    public string ProfissionalNome { get; set; } = string.Empty;
    public string ProfissionalIdentificador { get; set; } = string.Empty;
    public DateTime CriadoEm { get; set; }
}

public class EncaminhamentoRedeRequest
{
    public string Destino { get; set; } = string.Empty;
    public string? DestinoOutro { get; set; }
    public string? Data { get; set; }
    public string Motivo { get; set; } = string.Empty;
}

public class AtualizarEncaminhamentoRedeRequest
{
    public string Status { get; set; } = string.Empty;
    public string? Observacao { get; set; }
}

public class MetaSocialResponse
{
    public int Id { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public string? Prazo { get; set; }
    public string Situacao { get; set; } = string.Empty;
    public bool Atrasada { get; set; }
    public DateTime? ConcluidaEm { get; set; }
    public string CriadaPorNome { get; set; } = string.Empty;
    public DateTime CriadoEm { get; set; }
    public DateTime AtualizadoEm { get; set; }
}

public class MetaSocialRequest
{
    public string Descricao { get; set; } = string.Empty;
    public string? Prazo { get; set; }
}

public class AtualizarMetaSocialRequest
{
    public string Situacao { get; set; } = string.Empty;
}

public class AnexoSocialResponse
{
    public int Id { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public string NomeArquivo { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long TamanhoBytes { get; set; }
    public string Visibilidade { get; set; } = string.Empty;
    public int? AtendimentoNumero { get; set; }
    public string AutorNome { get; set; } = string.Empty;
    public string AutorIdentificador { get; set; } = string.Empty;
    public DateTime CriadoEm { get; set; }
    public bool PodeExcluir { get; set; }
}

public class ExcluirAnexoRequest
{
    public string Motivo { get; set; } = string.Empty;
}

/// <summary>Anexos que o Serviço Social abriu para a equipe técnica (Jurídico/Psicologia responsáveis).</summary>
public class AnexosCompartilhadosResponse
{
    public string? NumeroProntuario { get; set; }
    public List<AnexoSocialResponse> Anexos { get; set; } = [];
}

public class ProntuarioSocialDetalheResponse
{
    public AssistidaResponse Assistida { get; set; } = new();
    public int? ProntuarioId { get; set; }
    public string? NumeroProntuario { get; set; }
    public string Visibilidade { get; set; } = string.Empty;
    public string Situacao { get; set; } = string.Empty;
    public string? MotivoEncerramento { get; set; }
    public DateTime? EncerradoEm { get; set; }
    public List<ResponsavelSocialResponse> Responsaveis { get; set; } = [];
    public bool SouResponsavel { get; set; }
    public bool PodeVerConteudo { get; set; }
    public bool PodeRegistrar { get; set; }
    public bool PodeAtribuir { get; set; }
    public bool PodeAlterarVisibilidade { get; set; }
    public bool PodePedirAcessoEmergencia { get; set; }
    public DateTime? AcessoEmergenciaExpiraEm { get; set; }

    /// <summary>O aviso aparece para toda a equipe do Serviço Social, sem o conteúdo.</summary>
    public List<string> Vulnerabilidades { get; set; } = [];

    public bool EmAtendimentoJuridico { get; set; }
    public bool EmAcompanhamentoPsicologia { get; set; }
    public bool AnexosDisponiveis { get; set; }

    /// <summary>CRESS de quem está usando a tela (vazio = mostrar aviso para preencher).</summary>
    public string? MeuCress { get; set; }
    public int TotalAtendimentos { get; set; }
    public List<AtendimentoSocialResponse> Atendimentos { get; set; } = [];
    public EstudoSocialResponse? Estudo { get; set; }
    public List<EstudoSocialResponse> HistoricoEstudo { get; set; } = [];
    public List<EncaminhamentoRedeResponse> EncaminhamentosRede { get; set; } = [];
    public List<MetaSocialResponse> Metas { get; set; } = [];
    public List<AnexoSocialResponse> Anexos { get; set; } = [];
}

public class NovoAtendimentoSocialRequest
{
    public string Tipo { get; set; } = string.Empty;
    public string? TipoOutro { get; set; }

    /// <summary>Data e hora no horário de Brasília (yyyy-MM-ddTHH:mm). Vazio = agora.</summary>
    public string? RealizadoEm { get; set; }

    public string Relato { get; set; } = string.Empty;
    public string? Providencias { get; set; }
    public bool EncaminharJuridico { get; set; }
    public bool EncaminharPsicologia { get; set; }
}

public class AtendimentoSocialRegistradoResponse
{
    public int AtendimentoId { get; set; }
    public string NumeroProntuario { get; set; } = string.Empty;
    public int NumeroAtendimento { get; set; }
    public DateTime RealizadoEm { get; set; }
    public bool PodeDeclaracao { get; set; }
    public string? AvisoSnapshot { get; set; }
}

public class DeclaracaoComparecimentoSocialResponse
{
    public string AssistidaNome { get; set; } = string.Empty;
    public DateTime Data { get; set; }
    public string ProfissionalNome { get; set; } = string.Empty;
    public string ProfissionalIdentificador { get; set; } = string.Empty;
    public string? ProfissionalCress { get; set; }
}

public class IndicadoresServicoSocialResponse
{
    public int Fila { get; set; }
    public int EmAcompanhamento { get; set; }
    public int Encerrados { get; set; }
    public int AtendimentosNoMes { get; set; }
    public int ComVulnerabilidade { get; set; }
    public int EncaminhamentosRedeNoMes { get; set; }
    public int EncaminhamentosSemRetorno { get; set; }
    public Dictionary<string, int> AtendimentosPorTipoNoMes { get; set; } = [];
}
