namespace CasaMulher.Api.Models;

/// <summary>
/// Agendamento de um atendimento (Jurídico, Psicologia ou Serviço Social), com histórico próprio.
/// Nunca é sobrescrito: remarcar cria outro agendamento e marca este como "remarcado".
/// </summary>
public class AgendaAgendamento
{
    public int Id { get; set; }

    /// <summary>juridico, psicologia ou servico_social.</summary>
    public string Area { get; set; } = string.Empty;

    public int AssistidaId { get; set; }

    public Assistida? Assistida { get; set; }

    /// <summary>atendimento, retorno, primeiro, sessao, visita, visita_institucional, atendimento_social.</summary>
    public string Tipo { get; set; } = string.Empty;

    public string ResponsavelUserId { get; set; } = string.Empty;

    public DateTime InicioUtc { get; set; }

    public DateTime FimUtc { get; set; }

    /// <summary>Minutos de deslocamento antes e depois (visitas). Ocupam a agenda.</summary>
    public int DeslocamentoMinutos { get; set; }

    public bool Presencial { get; set; }

    /// <summary>A Recepção vê a assistida como esperada no dia (sem área nem motivo).</summary>
    public bool AvisarRecepcao { get; set; }

    /// <summary>agendado, realizado, cancelado, remarcado ou falta_registrada.</summary>
    public string Situacao { get; set; } = AgendaSituacao.Agendado;

    /// <summary>Observação operacional (ex.: trazer documentos). Nunca relato.</summary>
    public string? Observacao { get; set; }

    public int? RemarcadoDeId { get; set; }

    /// <summary>esperada, chegou ou recebida (só para presencial).</summary>
    public string? ChegadaEstado { get; set; }

    public DateTime? ChegouEm { get; set; }

    public string? ChegadaMarcadaPorIdentificador { get; set; }

    public DateTime? RecebidaEm { get; set; }

    /// <summary>Justificativa curta dada ao confirmar por cima de um conflito.</summary>
    public string? Justificativa { get; set; }

    /// <summary>Exceção de unidade fechada autorizada pela ADM só para este agendamento.</summary>
    public string? ExcecaoMotivo { get; set; }

    public string? ExcecaoPorIdentificador { get; set; }

    public string CriadoPorUserId { get; set; } = string.Empty;

    public string CriadoPorIdentificador { get; set; } = string.Empty;

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    public DateTime AtualizadoEm { get; set; } = DateTime.UtcNow;

    public ICollection<AgendaAgendamentoHistorico> Historico { get; set; } = new List<AgendaAgendamentoHistorico>();
}

public class AgendaAgendamentoHistorico
{
    public int Id { get; set; }

    public int AgendaAgendamentoId { get; set; }

    public AgendaAgendamento? Agendamento { get; set; }

    public string Texto { get; set; } = string.Empty;

    public string PorIdentificador { get; set; } = string.Empty;

    public DateTime Em { get; set; } = DateTime.UtcNow;
}

/// <summary>Compromisso criado à mão (lembrete, prazo, reunião, ausência, comunicação de falta...).</summary>
public class AgendaCompromisso
{
    public int Id { get; set; }

    /// <summary>lembrete, prazo, reuniao, audiencia, visita, evento, ausencia ou comunicacao.</summary>
    public string Tipo { get; set; } = string.Empty;

    public string Titulo { get; set; } = string.Empty;

    /// <summary>Quem vai estar ocupada (ou a quem o aviso se refere).</summary>
    public string DonoUserId { get; set; } = string.Empty;

    public string RegistradoPorUserId { get; set; } = string.Empty;

    public DateTime InicioUtc { get; set; }

    public DateTime? FimUtc { get; set; }

    /// <summary>Duração. Dia inteiro pode ou não ocupar (ver <see cref="OcupaHorario"/>).</summary>
    public bool DiaInteiro { get; set; }

    public bool OcupaHorario { get; set; }

    public string? Local { get; set; }

    /// <summary>Caso/prontuário vinculado: área (juridico, psicologia, servico_social) + assistida.</summary>
    public string? OrigemArea { get; set; }

    public int? AssistidaId { get; set; }

    /// <summary>eu (só quem participa), area, pessoas ou equipe.</summary>
    public string Visibilidade { get; set; } = AgendaVisibilidade.Eu;

    /// <summary>informada, confirmada ou recusada (só ausências).</summary>
    public string? SituacaoAusencia { get; set; }

    public string? AusenciaConfirmadaPorIdentificador { get; set; }

    /// <summary>ativo ou cancelado.</summary>
    public string Situacao { get; set; } = "ativo";

    public string? SerieId { get; set; }

    public string? Justificativa { get; set; }

    public string? ExcecaoMotivo { get; set; }

    public string? ExcecaoPorIdentificador { get; set; }

    /// <summary>Comunicação de falta/atraso: curso e data da aula. Não marca a chamada.</summary>
    public int? CursoId { get; set; }

    /// <summary>falta ou atraso.</summary>
    public string? Comunicacao { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    public ICollection<AgendaCompromissoPessoa> Pessoas { get; set; } = new List<AgendaCompromissoPessoa>();
}

/// <summary>Participante (ocupa a agenda) ou pessoa que só pode ver (não ocupa).</summary>
public class AgendaCompromissoPessoa
{
    public int Id { get; set; }

    public int AgendaCompromissoId { get; set; }

    public AgendaCompromisso? Compromisso { get; set; }

    public string UserId { get; set; } = string.Empty;

    /// <summary>participante ou ver.</summary>
    public string Papel { get; set; } = AgendaPapel.Participante;
}

/// <summary>Feriado, unidade fechada ou evento da Casa (configurado pela ADM).</summary>
public class AgendaDiaEspecial
{
    public int Id { get; set; }

    public DateOnly Data { get; set; }

    /// <summary>Que data é esta: feriado, evento ou outra (data comum, ex.: manutenção).</summary>
    public string Tipo { get; set; } = string.Empty;

    /// <summary>Como a Casa funciona neste dia: normal, fechada ou especial (horário especial).</summary>
    public string Funcionamento { get; set; } = AgendaFuncionamento.Normal;

    /// <summary>Horário especial: agendamentos só dentro deste intervalo.</summary>
    public TimeOnly? HorarioInicio { get; set; }

    public TimeOnly? HorarioFim { get; set; }

    public string Titulo { get; set; } = string.Empty;

    public string? Nota { get; set; }

    /// <summary>Reabertura para todos: ação separada e explícita da ADM.</summary>
    public string? ReabertaMotivo { get; set; }

    public string? ReabertaPorIdentificador { get; set; }

    public DateTime? ReabertaEm { get; set; }

    public string CriadoPorIdentificador { get; set; } = string.Empty;

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
}

/// <summary>Horário semanal de uma turma. Gera as aulas na agenda.</summary>
public class CursoHorario
{
    public int Id { get; set; }

    public int CursoId { get; set; }

    public Curso? Curso { get; set; }

    /// <summary>0 = domingo ... 6 = sábado.</summary>
    public int DiaSemana { get; set; }

    public TimeOnly Inicio { get; set; }

    public TimeOnly Fim { get; set; }

    public string? Sala { get; set; }

    /// <summary>Primeira data em que o horário vale (aulas antes disso não geram pendência).</summary>
    public DateOnly ValidoDesde { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
}

/// <summary>Cancelamento de aula: "esta" (uma data) ou "seguintes" (só as futuras a partir da data).</summary>
public class AulaCancelamento
{
    public int Id { get; set; }

    public int CursoId { get; set; }

    public DateOnly Data { get; set; }

    /// <summary>esta ou seguintes.</summary>
    public string Escopo { get; set; } = "esta";

    public string Motivo { get; set; } = string.Empty;

    public string PorIdentificador { get; set; } = string.Empty;

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    /// <summary>Desfeito: o cancelamento continua no histórico.</summary>
    public DateTime? DesfeitoEm { get; set; }
}

/// <summary>Compromisso futuro recebido em "Passar responsabilidades", aguardando a sucessora revisar.</summary>
public class AgendaRevisao
{
    public int Id { get; set; }

    public int AgendaAgendamentoId { get; set; }

    public AgendaAgendamento? Agendamento { get; set; }

    public string DeUserId { get; set; } = string.Empty;

    public string ParaUserId { get; set; } = string.Empty;

    /// <summary>pendente, assumido ou remarcado.</summary>
    public string Situacao { get; set; } = "pendente";

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    public DateTime? ResolvidoEm { get; set; }
}

/// <summary>Reavaliação de risco registrada na Psicologia (resolve a pendência mesmo com o risco igual).</summary>
public class ReavaliacaoRiscoPsicologica
{
    public int Id { get; set; }

    public int ProntuarioPsicologicoId { get; set; }

    public ProntuarioPsicologico? Prontuario { get; set; }

    public string Classificacao { get; set; } = string.Empty;

    public string ProximaAcao { get; set; } = string.Empty;

    public string ProfissionalUserId { get; set; } = string.Empty;

    public string ProfissionalIdentificador { get; set; } = string.Empty;

    public string ProfissionalNome { get; set; } = string.Empty;

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
}

/// <summary>Configuração geral da agenda (uma linha).</summary>
public class AgendaConfiguracao
{
    public int Id { get; set; }

    public int LimiteCompromissosDia { get; set; } = 6;

    public int LimiteHorasDia { get; set; } = 6;

    public TimeOnly ExpedienteInicio { get; set; } = new(8, 0);

    public TimeOnly ExpedienteFim { get; set; } = new(17, 0);

    public DateTime AtualizadoEm { get; set; } = DateTime.UtcNow;
}

public static class AgendaTipoDia
{
    public const string Feriado = "feriado";
    public const string Evento = "evento";
    public const string Outra = "outra";

    public static readonly string[] Todos = [Feriado, Evento, Outra];
}

public static class AgendaFuncionamento
{
    public const string Normal = "normal";
    public const string Fechada = "fechada";
    public const string Especial = "especial";

    public static readonly string[] Todos = [Normal, Fechada, Especial];
}

public static class AgendaArea
{
    public const string Juridico = "juridico";
    public const string Psicologia = "psicologia";
    public const string ServicoSocial = "servico_social";

    public static readonly string[] Todas = [Juridico, Psicologia, ServicoSocial];
}

public static class AgendaSituacao
{
    public const string Agendado = "agendado";
    public const string Realizado = "realizado";
    public const string Cancelado = "cancelado";
    public const string Remarcado = "remarcado";
    public const string FaltaRegistrada = "falta_registrada";

    public static readonly string[] Resultados = [Realizado, Cancelado, Remarcado, FaltaRegistrada];
}

public static class AgendaVisibilidade
{
    public const string Eu = "eu";
    public const string Area = "area";
    public const string Pessoas = "pessoas";
    public const string Equipe = "equipe";

    public static readonly string[] Todas = [Eu, Area, Pessoas, Equipe];
}

public static class AgendaPapel
{
    public const string Participante = "participante";
    public const string Ver = "ver";
}

public static class AgendaTipoManual
{
    public const string Lembrete = "lembrete";
    public const string Prazo = "prazo";
    public const string Reuniao = "reuniao";
    public const string Audiencia = "audiencia";
    public const string Visita = "visita";
    public const string Evento = "evento";
    public const string Ausencia = "ausencia";
    public const string Comunicacao = "comunicacao";

    public static readonly string[] Todos = [Lembrete, Prazo, Reuniao, Audiencia, Visita, Evento, Ausencia, Comunicacao];

    /// <summary>Lembrete, prazo e comunicação são marcos: nunca ocupam horário.</summary>
    public static bool NuncaOcupa(string tipo) => tipo is Lembrete or Prazo or Comunicacao;
}
