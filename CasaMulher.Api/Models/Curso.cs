namespace CasaMulher.Api.Models;

/// <summary>
/// Curso oferecido pela Casa. Nunca é apagado se já tiver alunas, professoras ou aulas:
/// nesse caso só é desativado, para o histórico continuar íntegro.
/// </summary>
public class Curso
{
    public int Id { get; set; }

    public string Nome { get; set; } = string.Empty;

    public bool Ativo { get; set; } = true;

    /// <summary>Número de vagas da turma (definido pelo ADM). Vazio = sem registro.</summary>
    public int? Vagas { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    public DateTime AtualizadoEm { get; set; } = DateTime.UtcNow;
}

/// <summary>Vínculo professora ↔ curso (uma professora pode ter vários cursos).</summary>
public class ProfessorCurso
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public int CursoId { get; set; }

    public Curso? Curso { get; set; }

    public DateTime AtribuidoEm { get; set; } = DateTime.UtcNow;
}

/// <summary>Vínculo assistida ↔ curso (direcionamento a um ou mais cursos).</summary>
public class AssistidaCurso
{
    public int Id { get; set; }

    public int AssistidaId { get; set; }

    public Assistida? Assistida { get; set; }

    public int CursoId { get; set; }

    public Curso? Curso { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    /// <summary>aguardando, cursando, concluiu ou interrompido (registrado pela professora).</summary>
    public string Situacao { get; set; } = AssistidaCursoSituacao.Cursando;

    /// <summary>Data em que a aluna terminou ou saiu do curso.</summary>
    public DateOnly? TerminoEm { get; set; }

    /// <summary>Motivo da saída: só a professora vê o texto; a Coordenação vê só a contagem.</summary>
    public string? MotivoSaida { get; set; }

    public DateTime? SituacaoAlteradaEm { get; set; }

    public string? SituacaoAlteradaPorIdentificador { get; set; }
}

public static class AssistidaCursoSituacao
{
    public const string Aguardando = "aguardando";
    public const string Cursando = "cursando";
    public const string Concluiu = "concluiu";
    public const string Interrompido = "interrompido";

    public static readonly string[] Todos = [Aguardando, Cursando, Concluiu, Interrompido];

    public static bool Encerrada(string? situacao) => situacao is Concluiu or Interrompido;

    /// <summary>Motivos sugeridos; a professora pode escrever outro.</summary>
    public static readonly string[] MotivosSugeridos =
    [
        "Conseguiu emprego / horário de trabalho",
        "Saúde",
        "Transporte ou distância",
        "Cuidado com filhos ou familiares",
        "Segurança",
        "Mudou de cidade",
        "Perdeu o interesse",
        "Outro"
    ];
}

/// <summary>Aula planejada de um curso. Pertence ao curso, não à professora, para seguir quem assumir a turma.</summary>
public class PlanoAula
{
    public int Id { get; set; }

    public int CursoId { get; set; }

    public Curso? Curso { get; set; }

    public DateOnly Data { get; set; }

    public string Tema { get; set; } = string.Empty;

    /// <summary>Atividades em JSON (lista de textos).</summary>
    public string AtividadesJson { get; set; } = "[]";

    public string? Observacoes { get; set; }

    /// <summary>plano ou criado_pela_chamada.</summary>
    public string Origem { get; set; } = "plano";

    public string CriadoPorUserId { get; set; } = string.Empty;

    public string CriadoPorIdentificador { get; set; } = string.Empty;

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    public DateTime AtualizadoEm { get; set; } = DateTime.UtcNow;
}

/// <summary>Chamada de um dia de aula de um curso (uma por curso e data).</summary>
public class Chamada
{
    public int Id { get; set; }

    public int CursoId { get; set; }

    public Curso? Curso { get; set; }

    public DateOnly Data { get; set; }

    public string? Tema { get; set; }

    public int? PlanoAulaId { get; set; }

    public string AtividadesJson { get; set; } = "[]";

    public string RegistradaPorUserId { get; set; } = string.Empty;

    public string RegistradaPorIdentificador { get; set; } = string.Empty;

    public string RegistradaPorNome { get; set; } = string.Empty;

    public DateTime AtualizadoEm { get; set; } = DateTime.UtcNow;

    public ICollection<ChamadaRegistro> Registros { get; set; } = new List<ChamadaRegistro>();
}

public class ChamadaRegistro
{
    public int Id { get; set; }

    public int ChamadaId { get; set; }

    public Chamada? Chamada { get; set; }

    public int AssistidaId { get; set; }

    /// <summary>presente, faltou, justificada ou vazio (não marcado).</summary>
    public string? Status { get; set; }

    public string? Observacao { get; set; }
}
