namespace CasaMulher.Api.DTOs;

public class CursoResponse
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public bool Ativo { get; set; }
    public int TotalAlunas { get; set; }
    public int TotalProfessoras { get; set; }
    public bool PodeApagar { get; set; }
}

public class CursoResumoResponse
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public bool Ativo { get; set; }
}

public class SalvarCursoRequest
{
    public string Nome { get; set; } = string.Empty;
}

public class ProfessoraCursosResponse
{
    public string UserId { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public string Identificador { get; set; } = string.Empty;
    public bool Ativo { get; set; }
    public List<CursoResumoResponse> Cursos { get; set; } = [];
}

public class DefinirCursosProfessoraRequest
{
    public List<int> CursoIds { get; set; } = [];
}

public class AlunaCursoResponse
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Telefone { get; set; }
    public DateTime DataAcolhimento { get; set; }
}

public class PlanoAulaResponse
{
    public int Id { get; set; }
    public string Data { get; set; } = string.Empty;
    public string Tema { get; set; } = string.Empty;
    public List<string> Atividades { get; set; } = [];
    public string? Observacoes { get; set; }
    public string Origem { get; set; } = string.Empty;
    public string CriadoPorIdentificador { get; set; } = string.Empty;
}

public class ChamadaRegistroDto
{
    public int AssistidaId { get; set; }
    public string? Nome { get; set; }
    public string? Status { get; set; }
    public string? Observacao { get; set; }
}

public class ChamadaResponse
{
    public int Id { get; set; }
    public string Data { get; set; } = string.Empty;
    public string? Tema { get; set; }
    public int? PlanoAulaId { get; set; }
    public List<string> Atividades { get; set; } = [];
    public string RegistradaPorNome { get; set; } = string.Empty;
    public List<ChamadaRegistroDto> Registros { get; set; } = [];
}

public class AulasCursoResponse
{
    public CursoResumoResponse Curso { get; set; } = new();
    public List<PlanoAulaResponse> Planos { get; set; } = [];
    public List<ChamadaResponse> Chamadas { get; set; } = [];
    public List<string> AtividadesSugeridas { get; set; } = [];
}

public class SalvarPlanoAulaRequest
{
    public string Data { get; set; } = string.Empty;
    public string Tema { get; set; } = string.Empty;
    public List<string> Atividades { get; set; } = [];
    public string? Observacoes { get; set; }
}

public class SalvarChamadaRequest
{
    public string? Tema { get; set; }
    public int? PlanoAulaId { get; set; }
    public List<string> Atividades { get; set; } = [];
    public List<ChamadaRegistroDto> Registros { get; set; } = [];
}

public class ResponsabilidadesFuncionarioResponse
{
    public string UserId { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public string Perfil { get; set; } = string.Empty;
    public List<string> Cursos { get; set; } = [];
    public int CasosJuridicos { get; set; }
    public int ProntuariosPsicologicos { get; set; }
    public List<ProfessoraCursosResponse> Destinos { get; set; } = [];
}

public class TransferirResponsabilidadesRequest
{
    public string DestinoUserId { get; set; } = string.Empty;
    public bool DesativarConta { get; set; } = true;
}

public class TransferenciaResultadoResponse
{
    public int Cursos { get; set; }
    public int CasosJuridicos { get; set; }
    public int ProntuariosPsicologicos { get; set; }
    public bool ContaDesativada { get; set; }
    public string? Aviso { get; set; }
}
