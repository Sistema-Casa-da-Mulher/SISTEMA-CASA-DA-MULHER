namespace CasaMulher.Api.DTOs;

/// <summary>Item da agenda já filtrado para quem está olhando (o que não pode ver não chega aqui).</summary>
public class AgendaItemResponse
{
    public string Id { get; set; } = string.Empty;
    public string Fonte { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string Nivel { get; set; } = string.Empty;
    public string? Grupo { get; set; }
    /// <summary>Horário de Brasília, sem fuso (yyyy-MM-ddTHH:mm:ss).</summary>
    public string Inicio { get; set; } = string.Empty;
    public string? Fim { get; set; }
    public string? JanelaInicio { get; set; }
    public string? JanelaFim { get; set; }
    public bool DiaInteiro { get; set; }
    public bool Ocupa { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string? Sub { get; set; }
    public string Cor { get; set; } = "k-man";
    public string? DonoId { get; set; }
    public string? DonoNome { get; set; }
    public bool Meu { get; set; }
    public List<string> Pessoas { get; set; } = [];
    public string? Situacao { get; set; }
    public string? Chegada { get; set; }
    public string? ChegouEm { get; set; }
    public string? RecebidaEm { get; set; }
    public string? Ausencia { get; set; }
    public bool Cancelado { get; set; }
    public List<string> Tags { get; set; } = [];
    /// <summary>Só no nível detalhe (para remarcar/agendar a partir do item).</summary>
    public int? AssistidaId { get; set; }
    public string? Area { get; set; }
    public int? CursoId { get; set; }
}

public class AgendaListaResponse
{
    public List<AgendaItemResponse> Itens { get; set; } = [];
    /// <summary>Só ADM: atendimentos técnicos por dia, sem nomes.</summary>
    public Dictionary<string, AgendaContagemDia> Contagens { get; set; } = [];
    public string Agora { get; set; } = string.Empty;
}

public class AgendaContagemDia
{
    public int Juridico { get; set; }
    public int Psicologia { get; set; }
    public int ServicoSocial { get; set; }
}

public class AgendaDetalheResponse
{
    public AgendaItemResponse Item { get; set; } = new();
    public List<string[]> Linhas { get; set; } = [];
    public string? Nota { get; set; }
    public List<string> Acoes { get; set; } = [];
}

public class AgendaPendenciaResponse
{
    public string Tipo { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public string Texto { get; set; } = string.Empty;
    public string? Nota { get; set; }
    public List<string> Acoes { get; set; } = [];
}

public class AgendaPessoaResponse
{
    public string Id { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public string Curto { get; set; } = string.Empty;
    public string Perfil { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
}

public class AgendaOrigemResponse
{
    public int AssistidaId { get; set; }
    public string Identificacao { get; set; } = string.Empty;
    public string? Referencia { get; set; }
    public bool Restrito { get; set; }
}

public class AgendaMeResponse
{
    public string Id { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public string Perfil { get; set; } = string.Empty;
    public string? Area { get; set; }
    public string Agora { get; set; } = string.Empty;
    public List<string> FiltrosPadrao { get; set; } = [];
    public List<string> FiltrosDisponiveis { get; set; } = [];
    public List<AgendaPessoaResponse> Pessoas { get; set; } = [];
    public List<string[]> TiposManuais { get; set; } = [];
    public List<string[]> TiposAgendamento { get; set; } = [];
    public int DuracaoPadrao { get; set; }
    public int DeslocamentoPadrao { get; set; }
    public string ExpedienteInicio { get; set; } = "08:00";
    public string ExpedienteFim { get; set; } = "17:00";
}

public class AgendaOcorrenciaRequest
{
    /// <summary>Horário de Brasília (yyyy-MM-ddTHH:mm).</summary>
    public string Inicio { get; set; } = string.Empty;
    public string? Fim { get; set; }
    public string? Justificativa { get; set; }
    public string? ExcecaoMotivo { get; set; }
}

public class AgendaConferirRequest
{
    public List<AgendaOcorrenciaRequest> Ocorrencias { get; set; } = [];
    public string? DonoId { get; set; }
    public List<string> Participantes { get; set; } = [];
    public int? AssistidaId { get; set; }
    public bool DiaInteiro { get; set; }
    public int DeslocamentoMinutos { get; set; }
    public string? IgnorarItemId { get; set; }
}

public class AgendaConflitoResponse
{
    public string Nivel { get; set; } = string.Empty;
    public string Mensagem { get; set; } = string.Empty;
    public bool UnidadeFechada { get; set; }
}

public class AgendaConferenciaOcorrenciaResponse
{
    public string Inicio { get; set; } = string.Empty;
    public string Rotulo { get; set; } = string.Empty;
    public List<AgendaConflitoResponse> Conflitos { get; set; } = [];
}

public class AgendaDisponibilidadeRequest
{
    public List<string> Pessoas { get; set; } = [];
    public int? AssistidaId { get; set; }
    /// <summary>yyyy-MM-dd (Brasília).</summary>
    public string De { get; set; } = string.Empty;
    public string Ate { get; set; } = string.Empty;
    public string? IgnorarItemId { get; set; }
}

public class AgendaFaixaResponse
{
    public string Inicio { get; set; } = string.Empty;
    public string Fim { get; set; } = string.Empty;
    public string Rotulo { get; set; } = string.Empty;
    public bool Revelado { get; set; }
    public bool Aviso { get; set; }
    public string Cor { get; set; } = "k-neu";
}

public class AgendaCompromissoRequest
{
    public string Tipo { get; set; } = string.Empty;
    public string? Titulo { get; set; }
    /// <summary>Ausência: em nome de quem (vazio = eu).</summary>
    public string? DonoId { get; set; }
    public bool DiaInteiro { get; set; }
    public string? Local { get; set; }
    public int? AssistidaId { get; set; }
    public string Visibilidade { get; set; } = "eu";
    public List<string> Participantes { get; set; } = [];
    public List<string> PodemVer { get; set; } = [];
    public List<AgendaOcorrenciaRequest> Ocorrencias { get; set; } = [];
    /// <summary>Comunicação de falta/atraso.</summary>
    public int? CursoId { get; set; }
    public string? Comunicacao { get; set; }
}

public class AgendaAgendamentoRequest
{
    public int AssistidaId { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string Inicio { get; set; } = string.Empty;
    public int DuracaoMinutos { get; set; }
    public int DeslocamentoMinutos { get; set; }
    public bool Presencial { get; set; }
    public bool AvisarRecepcao { get; set; }
    public string? Observacao { get; set; }
    public string? Justificativa { get; set; }
    public string? ExcecaoMotivo { get; set; }
    public int? RemarcarDeId { get; set; }
}

public class AgendaResultadoRequest
{
    public string Situacao { get; set; } = string.Empty;
}

public class AgendaCancelarRequest
{
    /// <summary>esta, seguintes ou todas.</summary>
    public string Escopo { get; set; } = "esta";
    public string? Motivo { get; set; }
}

public class AgendaJustificativaRequest
{
    public string? Justificativa { get; set; }
}

public class AgendaAulaCancelarRequest
{
    public int CursoId { get; set; }
    public string Data { get; set; } = string.Empty;
    public string Escopo { get; set; } = "esta";
    public string Motivo { get; set; } = string.Empty;
}

public class AgendaDiaEspecialRequest
{
    public string Data { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string? Nota { get; set; }
}

public class AgendaMotivoRequest
{
    public string Motivo { get; set; } = string.Empty;
}

public class AgendaConfiguracaoResponse
{
    public int LimiteCompromissosDia { get; set; }
    public int LimiteHorasDia { get; set; }
    public string ExpedienteInicio { get; set; } = "08:00";
    public string ExpedienteFim { get; set; } = "17:00";
    public List<AgendaDiaEspecialResponse> Dias { get; set; } = [];
    public List<string> Excecoes { get; set; } = [];
    public List<string> AusenciasInformadas { get; set; } = [];
}

public class AgendaDiaEspecialResponse
{
    public int Id { get; set; }
    public string Data { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string? Nota { get; set; }
    public string? Reaberta { get; set; }
}

public class AgendaConfiguracaoRequest
{
    public int LimiteCompromissosDia { get; set; }
    public int LimiteHorasDia { get; set; }
    public string ExpedienteInicio { get; set; } = "08:00";
    public string ExpedienteFim { get; set; } = "17:00";
}

public class CursoHorarioDto
{
    public int? Id { get; set; }
    public int DiaSemana { get; set; }
    public string Inicio { get; set; } = string.Empty;
    public string Fim { get; set; } = string.Empty;
    public string? Sala { get; set; }
}

public class AgendaComunicacaoRequest
{
    public int AssistidaId { get; set; }
    public string AulaId { get; set; } = string.Empty;
    public string Comunicacao { get; set; } = "falta";
}

public class ReavaliacaoRiscoRequest
{
    public int AssistidaId { get; set; }
    public string Classificacao { get; set; } = string.Empty;
    public string ProximaAcao { get; set; } = string.Empty;
}

public class ReavaliacaoRiscoResponse
{
    public int Id { get; set; }
    public string Em { get; set; } = string.Empty;
    public string Profissional { get; set; } = string.Empty;
    public string Classificacao { get; set; } = string.Empty;
    public string ProximaAcao { get; set; } = string.Empty;
}

public class AgendaAlunaAulasResponse
{
    public int AssistidaId { get; set; }
    public string Identificacao { get; set; } = string.Empty;
    public List<string[]> Aulas { get; set; } = [];
}
