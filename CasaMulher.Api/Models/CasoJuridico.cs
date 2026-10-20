namespace CasaMulher.Api.Models;

/// <summary>
/// Acompanhamento jurídico de uma assistida: responsáveis, visibilidade e situação atual.
/// Existe no máximo um caso por assistida; cada atendimento gera um protocolo próprio.
/// </summary>
public class CasoJuridico
{
    public int Id { get; set; }

    public int AssistidaId { get; set; }

    public Assistida? Assistida { get; set; }

    /// <summary>equipe (todo o Jurídico vê) ou responsaveis (só as responsáveis).</summary>
    public string Visibilidade { get; set; } = CasoJuridicoVisibilidade.Equipe;

    public string Situacao { get; set; } = CasoJuridicoSituacao.EmAndamento;

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    public DateTime AtualizadoEm { get; set; } = DateTime.UtcNow;

    public ICollection<CasoJuridicoResponsavel> Responsaveis { get; set; } = new List<CasoJuridicoResponsavel>();

    public ICollection<AtendimentoJuridico> Atendimentos { get; set; } = new List<AtendimentoJuridico>();
}

public class CasoJuridicoResponsavel
{
    public int Id { get; set; }

    public int CasoJuridicoId { get; set; }

    public CasoJuridico? Caso { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string Identificador { get; set; } = string.Empty;

    public string Nome { get; set; } = string.Empty;

    public string AtribuidoPorIdentificador { get; set; } = string.Empty;

    public DateTime AtribuidoEm { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Registro de um atendimento jurídico. Depois de salvo não é editado nem removido;
/// correções entram como <see cref="AtendimentoJuridicoAdendo"/>.
/// </summary>
public class AtendimentoJuridico
{
    public int Id { get; set; }

    public int CasoJuridicoId { get; set; }

    public CasoJuridico? Caso { get; set; }

    /// <summary>CJ-AAAA-NNNNNN, gerado no servidor.</summary>
    public string Protocolo { get; set; } = string.Empty;

    public int Ano { get; set; }

    public int Sequencial { get; set; }

    public string ProfissionalUserId { get; set; } = string.Empty;

    public string ProfissionalIdentificador { get; set; } = string.Empty;

    public string ProfissionalNome { get; set; } = string.Empty;

    public string TipoDemanda { get; set; } = string.Empty;

    public string? TipoDemandaOutro { get; set; }

    public bool? TemMedidaProtetiva { get; set; }

    public string? NumeroProcesso { get; set; }

    public string? Vara { get; set; }

    /// <summary>Chaves separadas por vírgula (defensoria, delegacia_mulher, creas...).</summary>
    public string? Encaminhamentos { get; set; }

    public string? EncaminhamentoOutro { get; set; }

    public string Relato { get; set; } = string.Empty;

    public string? ProximoPasso { get; set; }

    public DateOnly? DataRetorno { get; set; }

    /// <summary>Situação do caso registrada neste atendimento.</summary>
    public string Situacao { get; set; } = CasoJuridicoSituacao.EmAndamento;

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    public ICollection<AtendimentoJuridicoAdendo> Adendos { get; set; } = new List<AtendimentoJuridicoAdendo>();
}

public class AtendimentoJuridicoAdendo
{
    public int Id { get; set; }

    public int AtendimentoJuridicoId { get; set; }

    public AtendimentoJuridico? Atendimento { get; set; }

    public string Texto { get; set; } = string.Empty;

    public string AutorUserId { get; set; } = string.Empty;

    public string AutorIdentificador { get; set; } = string.Empty;

    public string AutorNome { get; set; } = string.Empty;

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Acesso temporário de uma profissional do Jurídico a um caso "somente responsáveis",
/// liberado na hora mediante motivo, auditado e avisado às responsáveis.
/// </summary>
public class CasoJuridicoAcessoEmergencia
{
    public int Id { get; set; }

    public int CasoJuridicoId { get; set; }

    public CasoJuridico? Caso { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string Identificador { get; set; } = string.Empty;

    public string Nome { get; set; } = string.Empty;

    public string Motivo { get; set; } = string.Empty;

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    public DateTime ExpiraEm { get; set; }
}

public static class CasoJuridicoVisibilidade
{
    public const string Equipe = "equipe";
    public const string Responsaveis = "responsaveis";

    public static readonly string[] Todos = [Equipe, Responsaveis];
}

public static class CasoJuridicoSituacao
{
    public const string EmAndamento = "em_andamento";
    public const string AguardandoDocumento = "aguardando_documento";
    public const string Encerrado = "encerrado";

    public static readonly string[] Todos = [EmAndamento, AguardandoDocumento, Encerrado];
}

public static class AtendimentoJuridicoCatalogo
{
    public static readonly string[] TiposDemanda =
    [
        "medida_protetiva",
        "divorcio",
        "guarda",
        "pensao",
        "partilha",
        "violencia_patrimonial",
        "outro"
    ];

    public static readonly string[] Encaminhamentos =
    [
        "defensoria",
        "delegacia_mulher",
        "creas",
        "conselho_tutelar",
        "outro"
    ];
}
