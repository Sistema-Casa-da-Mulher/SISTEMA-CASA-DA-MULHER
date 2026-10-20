namespace CasaMulher.Api.Models;

/// <summary>
/// Prontuário psicológico da assistida (um por assistida, número gerado no servidor).
/// Segue o sigilo profissional da Psicologia: por padrão só a psicóloga responsável
/// vê o conteúdo, e o registro nunca é apagado (guarda mínima de 5 anos).
/// </summary>
public class ProntuarioPsicologico
{
    public int Id { get; set; }

    public int AssistidaId { get; set; }

    public Assistida? Assistida { get; set; }

    /// <summary>PSI-AAAA-NNNNNN.</summary>
    public string Numero { get; set; } = string.Empty;

    public int Ano { get; set; }

    public int Sequencial { get; set; }

    /// <summary>responsaveis (padrão) ou equipe (toda a equipe de Psicologia).</summary>
    public string Visibilidade { get; set; } = ProntuarioVisibilidade.Responsaveis;

    public string Situacao { get; set; } = ProntuarioSituacao.EmAcompanhamento;

    public string? MotivoEncerramento { get; set; }

    public DateTime? EncerradoEm { get; set; }

    /// <summary>Último nível de risco registrado (baixo, moderado, alto, iminente).</summary>
    public string? RiscoAtual { get; set; }

    public DateTime? RiscoAtualizadoEm { get; set; }

    /// <summary>Quando a assistida foi informada sobre o sigilo e seus limites.</summary>
    public DateTime? ConsentimentoSigiloEm { get; set; }

    public string? ConsentimentoPorIdentificador { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    public DateTime AtualizadoEm { get; set; } = DateTime.UtcNow;

    public ICollection<ProntuarioPsicologicoResponsavel> Responsaveis { get; set; } = new List<ProntuarioPsicologicoResponsavel>();

    public ICollection<SessaoPsicologica> Sessoes { get; set; } = new List<SessaoPsicologica>();
}

public class ProntuarioPsicologicoResponsavel
{
    public int Id { get; set; }

    public int ProntuarioPsicologicoId { get; set; }

    public ProntuarioPsicologico? Prontuario { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string Identificador { get; set; } = string.Empty;

    public string Nome { get; set; } = string.Empty;

    public string? Crp { get; set; }

    public string AtribuidoPorIdentificador { get; set; } = string.Empty;

    public DateTime AtribuidoEm { get; set; } = DateTime.UtcNow;
}

/// <summary>Registro de uma sessão. Depois de salvo não é editado; correções entram como adendo.</summary>
public class SessaoPsicologica
{
    public int Id { get; set; }

    public int ProntuarioPsicologicoId { get; set; }

    public ProntuarioPsicologico? Prontuario { get; set; }

    /// <summary>Número da sessão dentro do prontuário (1, 2, 3...).</summary>
    public int Numero { get; set; }

    public string ProfissionalUserId { get; set; } = string.Empty;

    public string ProfissionalIdentificador { get; set; } = string.Empty;

    public string ProfissionalNome { get; set; } = string.Empty;

    public string? ProfissionalCrp { get; set; }

    /// <summary>acolhimento, individual ou crise.</summary>
    public string TipoAtendimento { get; set; } = string.Empty;

    /// <summary>compareceu, faltou, faltou_justificou ou remarcou.</summary>
    public string Presenca { get; set; } = string.Empty;

    /// <summary>presencial, online ou telefone.</summary>
    public string? Modalidade { get; set; }

    public string? Demanda { get; set; }

    public string? Evolucao { get; set; }

    public string? Plano { get; set; }

    public DateTime? ProximaSessao { get; set; }

    public string? Risco { get; set; }

    /// <summary>Chaves separadas por vírgula (caps, ubs, cras_creas, abrigo, outro).</summary>
    public string? EncaminhamentosExternos { get; set; }

    public string? EncaminhamentoExternoOutro { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    public ICollection<SessaoPsicologicaAdendo> Adendos { get; set; } = new List<SessaoPsicologicaAdendo>();
}

public class SessaoPsicologicaAdendo
{
    public int Id { get; set; }

    public int SessaoPsicologicaId { get; set; }

    public SessaoPsicologica? Sessao { get; set; }

    public string Texto { get; set; } = string.Empty;

    public string AutorUserId { get; set; } = string.Empty;

    public string AutorIdentificador { get; set; } = string.Empty;

    public string AutorNome { get; set; } = string.Empty;

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
}

public class ProntuarioPsicologicoAcessoEmergencia
{
    public int Id { get; set; }

    public int ProntuarioPsicologicoId { get; set; }

    public ProntuarioPsicologico? Prontuario { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string Identificador { get; set; } = string.Empty;

    public string Nome { get; set; } = string.Empty;

    public string Motivo { get; set; } = string.Empty;

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    public DateTime ExpiraEm { get; set; }
}

public static class ProntuarioVisibilidade
{
    public const string Responsaveis = "responsaveis";
    public const string Equipe = "equipe";

    public static readonly string[] Todos = [Responsaveis, Equipe];
}

public static class ProntuarioSituacao
{
    public const string EmAcompanhamento = "em_acompanhamento";
    public const string Encerrado = "encerrado";
}

public static class PsicologiaCatalogo
{
    public static readonly string[] TiposAtendimento = ["acolhimento", "individual", "crise"];

    public static readonly string[] Presencas = ["compareceu", "faltou", "faltou_justificou", "remarcou"];

    public static readonly string[] Modalidades = ["presencial", "online", "telefone"];

    public static readonly string[] Riscos = ["baixo", "moderado", "alto", "iminente"];

    public static readonly string[] EncaminhamentosExternos = ["caps", "ubs", "cras_creas", "abrigo", "outro"];

    public static readonly string[] MotivosEncerramento = ["alta", "desistencia", "encaminhamento_externo", "mudanca_cidade", "outro"];

    public static bool RiscoElevado(string? risco) => risco is "alto" or "iminente";
}
