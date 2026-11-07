namespace CasaMulher.Api.Models;

/// <summary>
/// Prontuário social da assistida (um por assistida, número gerado no servidor).
/// Mesmo sigilo da Psicologia: por padrão só as assistentes sociais responsáveis veem o
/// conteúdo; os registros nunca são apagados (correções entram como adendo).
/// </summary>
public class ProntuarioSocial
{
    public int Id { get; set; }

    public int AssistidaId { get; set; }

    public Assistida? Assistida { get; set; }

    /// <summary>SOC-AAAA-NNNNNN.</summary>
    public string Numero { get; set; } = string.Empty;

    public int Ano { get; set; }

    public int Sequencial { get; set; }

    /// <summary>responsaveis (padrão) ou equipe (toda a equipe do Serviço Social).</summary>
    public string Visibilidade { get; set; } = ProntuarioVisibilidade.Responsaveis;

    public string Situacao { get; set; } = ProntuarioSituacao.EmAcompanhamento;

    public string? MotivoEncerramento { get; set; }

    public DateTime? EncerradoEm { get; set; }

    /// <summary>
    /// Indicadores de vulnerabilidade do estudo social mais recente (chaves separadas por vírgula).
    /// Fica aqui para o aviso da ficha não depender de abrir o histórico do estudo.
    /// </summary>
    public string? VulnerabilidadesAtuais { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    public DateTime AtualizadoEm { get; set; } = DateTime.UtcNow;

    public ICollection<ProntuarioSocialResponsavel> Responsaveis { get; set; } = new List<ProntuarioSocialResponsavel>();

    public ICollection<AtendimentoSocial> Atendimentos { get; set; } = new List<AtendimentoSocial>();

    public ICollection<EstudoSocial> Estudos { get; set; } = new List<EstudoSocial>();

    public ICollection<EncaminhamentoRede> EncaminhamentosRede { get; set; } = new List<EncaminhamentoRede>();

    public ICollection<MetaAcompanhamentoSocial> Metas { get; set; } = new List<MetaAcompanhamentoSocial>();

    public ICollection<AnexoSocial> Anexos { get; set; } = new List<AnexoSocial>();
}

public class ProntuarioSocialResponsavel
{
    public int Id { get; set; }

    public int ProntuarioSocialId { get; set; }

    public ProntuarioSocial? Prontuario { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string Identificador { get; set; } = string.Empty;

    public string Nome { get; set; } = string.Empty;

    public string? Cress { get; set; }

    public string AtribuidoPorIdentificador { get; set; } = string.Empty;

    public DateTime AtribuidoEm { get; set; } = DateTime.UtcNow;
}

/// <summary>Atendimento do Serviço Social. Depois de salvo não é editado; correções entram como adendo.</summary>
public class AtendimentoSocial
{
    public int Id { get; set; }

    public int ProntuarioSocialId { get; set; }

    public ProntuarioSocial? Prontuario { get; set; }

    /// <summary>Número do atendimento dentro do prontuário (1, 2, 3...).</summary>
    public int Numero { get; set; }

    /// <summary>Quando o atendimento aconteceu (informado em horário de Brasília, guardado em UTC).</summary>
    public DateTime RealizadoEm { get; set; }

    public string ProfissionalUserId { get; set; } = string.Empty;

    public string ProfissionalIdentificador { get; set; } = string.Empty;

    public string ProfissionalNome { get; set; } = string.Empty;

    /// <summary>CRESS da profissional no momento do registro (vai para a declaração de comparecimento).</summary>
    public string? ProfissionalCress { get; set; }

    /// <summary>acolhimento, individual, visita_domiciliar, contato_telefonico, articulacao_rede ou outro.</summary>
    public string Tipo { get; set; } = string.Empty;

    public string? TipoOutro { get; set; }

    public string Relato { get; set; } = string.Empty;

    public string? Providencias { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    public ICollection<AtendimentoSocialAdendo> Adendos { get; set; } = new List<AtendimentoSocialAdendo>();
}

public class AtendimentoSocialAdendo
{
    public int Id { get; set; }

    public int AtendimentoSocialId { get; set; }

    public AtendimentoSocial? Atendimento { get; set; }

    public string Texto { get; set; } = string.Empty;

    public string AutorUserId { get; set; } = string.Empty;

    public string AutorIdentificador { get; set; } = string.Empty;

    public string AutorNome { get; set; } = string.Empty;

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Estudo social (ficha socioeconômica). Cada atualização é uma nova versão; a mais recente
/// vale como a atual e as anteriores ficam como histórico.
/// </summary>
public class EstudoSocial
{
    public int Id { get; set; }

    public int ProntuarioSocialId { get; set; }

    public ProntuarioSocial? Prontuario { get; set; }

    public int Versao { get; set; }

    public int? PessoasNoDomicilio { get; set; }

    public string? ComposicaoFamiliar { get; set; }

    /// <summary>JSON com os dependentes: [{ "parentesco": "filho", "idade": 7 }].</summary>
    public string DependentesJson { get; set; } = "[]";

    public string? FaixaRenda { get; set; }

    public string? SituacaoTrabalho { get; set; }

    public string? Moradia { get; set; }

    public bool? ConviveComAgressor { get; set; }

    public string? Escolaridade { get; set; }

    /// <summary>sim, nao ou nao_sabe.</summary>
    public string? CadUnico { get; set; }

    /// <summary>NIS somente com dígitos (11).</summary>
    public string? Nis { get; set; }

    /// <summary>Chaves separadas por vírgula (bolsa_familia, bpc, aluguel_social, outro).</summary>
    public string? Beneficios { get; set; }

    public string? BeneficioOutro { get; set; }

    public string? RedeApoio { get; set; }

    /// <summary>Chaves separadas por vírgula (inseguranca_alimentar, sem_renda, sem_moradia_segura, dependencia_economica_agressor).</summary>
    public string? Vulnerabilidades { get; set; }

    public string? Observacoes { get; set; }

    public string ProfissionalUserId { get; set; } = string.Empty;

    public string ProfissionalIdentificador { get; set; } = string.Empty;

    public string ProfissionalNome { get; set; } = string.Empty;

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
}

/// <summary>Encaminhamento da assistida para um serviço da rede (fora da Casa).</summary>
public class EncaminhamentoRede
{
    public int Id { get; set; }

    public int ProntuarioSocialId { get; set; }

    public ProntuarioSocial? Prontuario { get; set; }

    public string Destino { get; set; } = string.Empty;

    public string? DestinoOutro { get; set; }

    public DateOnly Data { get; set; }

    public string Motivo { get; set; } = string.Empty;

    /// <summary>enviado, retorno_recebido, sem_retorno ou cancelado.</summary>
    public string Status { get; set; } = EncaminhamentoRedeStatus.Enviado;

    public string? ObservacaoStatus { get; set; }

    public DateTime? StatusAtualizadoEm { get; set; }

    public string? StatusAtualizadoPorIdentificador { get; set; }

    public string ProfissionalUserId { get; set; } = string.Empty;

    public string ProfissionalIdentificador { get; set; } = string.Empty;

    public string ProfissionalNome { get; set; } = string.Empty;

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
}

/// <summary>Meta ou ação do plano de acompanhamento.</summary>
public class MetaAcompanhamentoSocial
{
    public int Id { get; set; }

    public int ProntuarioSocialId { get; set; }

    public ProntuarioSocial? Prontuario { get; set; }

    public string Descricao { get; set; } = string.Empty;

    public DateOnly? Prazo { get; set; }

    /// <summary>pendente, em_andamento ou concluida.</summary>
    public string Situacao { get; set; } = MetaSocialSituacao.Pendente;

    public DateTime? ConcluidaEm { get; set; }

    public string CriadaPorUserId { get; set; } = string.Empty;

    public string CriadaPorIdentificador { get; set; } = string.Empty;

    public string CriadaPorNome { get; set; } = string.Empty;

    public string? AtualizadaPorIdentificador { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    public DateTime AtualizadoEm { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Documento anexado ao prontuário social (opcionalmente ligado a um atendimento).
/// Os dados do arquivo ficam em <see cref="AnexoSocialConteudo"/>, criptografados, para a
/// listagem nunca carregar o arquivo. Exclusão é lógica, com motivo.
/// </summary>
public class AnexoSocial
{
    public int Id { get; set; }

    public int ProntuarioSocialId { get; set; }

    public ProntuarioSocial? Prontuario { get; set; }

    public int? AtendimentoSocialId { get; set; }

    public AtendimentoSocial? Atendimento { get; set; }

    public string Tipo { get; set; } = string.Empty;

    public string? Descricao { get; set; }

    public string NomeArquivo { get; set; } = string.Empty;

    /// <summary>application/pdf, image/jpeg ou image/png (conferido pela assinatura do arquivo).</summary>
    public string ContentType { get; set; } = string.Empty;

    public long TamanhoBytes { get; set; }

    /// <summary>SHA-256 do arquivo original, para conferir a integridade no download.</summary>
    public string Sha256 { get; set; } = string.Empty;

    /// <summary>servico_social (padrão) ou equipe_tecnica.</summary>
    public string Visibilidade { get; set; } = AnexoSocialVisibilidade.ServicoSocial;

    public string AutorUserId { get; set; } = string.Empty;

    public string AutorIdentificador { get; set; } = string.Empty;

    public string AutorNome { get; set; } = string.Empty;

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    public bool Excluido { get; set; }

    public DateTime? ExcluidoEm { get; set; }

    public string? ExcluidoPorIdentificador { get; set; }

    public string? MotivoExclusao { get; set; }

    public AnexoSocialConteudo? Conteudo { get; set; }
}

public class AnexoSocialConteudo
{
    public int Id { get; set; }

    public int AnexoSocialId { get; set; }

    public AnexoSocial? Anexo { get; set; }

    /// <summary>Arquivo criptografado (AES-256-GCM, ver AnexosCriptografia).</summary>
    public byte[] Dados { get; set; } = [];
}

public class ProntuarioSocialAcessoEmergencia
{
    public int Id { get; set; }

    public int ProntuarioSocialId { get; set; }

    public ProntuarioSocial? Prontuario { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string Identificador { get; set; } = string.Empty;

    public string Nome { get; set; } = string.Empty;

    public string Motivo { get; set; } = string.Empty;

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    public DateTime ExpiraEm { get; set; }
}

public static class EncaminhamentoRedeStatus
{
    public const string Enviado = "enviado";
    public const string RetornoRecebido = "retorno_recebido";
    public const string SemRetorno = "sem_retorno";
    public const string Cancelado = "cancelado";

    public static readonly string[] Todos = [Enviado, RetornoRecebido, SemRetorno, Cancelado];
}

public static class MetaSocialSituacao
{
    public const string Pendente = "pendente";
    public const string EmAndamento = "em_andamento";
    public const string Concluida = "concluida";

    public static readonly string[] Todos = [Pendente, EmAndamento, Concluida];
}

public static class AnexoSocialVisibilidade
{
    /// <summary>Só o Serviço Social (com as mesmas regras do prontuário).</summary>
    public const string ServicoSocial = "servico_social";

    /// <summary>Também Jurídico e Psicologia que são responsáveis pela assistida.</summary>
    public const string EquipeTecnica = "equipe_tecnica";

    public static readonly string[] Todos = [ServicoSocial, EquipeTecnica];
}

/// <summary>Listas fechadas do Serviço Social. Os rótulos ficam na tela e nos relatórios.</summary>
public static class ServicoSocialCatalogo
{
    public static readonly string[] TiposAtendimento = ["acolhimento", "individual", "visita_domiciliar", "contato_telefonico", "articulacao_rede", "outro"];

    /// <summary>Tipos em que a assistida esteve na Casa: só eles geram declaração de comparecimento.</summary>
    public static readonly string[] TiposComComparecimento = ["acolhimento", "individual", "outro"];

    public static readonly string[] FaixasRenda = ["sem_renda", "ate_meio_sm", "meio_a_1_sm", "1_a_2_sm", "2_a_3_sm", "acima_3_sm", "nao_informada"];

    public static readonly string[] SituacoesTrabalho = ["formal", "informal", "autonoma", "desempregada", "do_lar", "aposentada_pensionista", "estudante", "outro"];

    public static readonly string[] Moradias = ["propria", "alugada", "cedida", "abrigo", "situacao_rua", "outro"];

    public static readonly string[] Escolaridades = ["sem_escolaridade", "fundamental_incompleto", "fundamental_completo", "medio_incompleto", "medio_completo", "superior_incompleto", "superior_completo"];

    public static readonly string[] CadUnico = ["sim", "nao", "nao_sabe"];

    public static readonly string[] Beneficios = ["bolsa_familia", "bpc", "aluguel_social", "outro"];

    public static readonly string[] Vulnerabilidades = ["inseguranca_alimentar", "sem_renda", "sem_moradia_segura", "dependencia_economica_agressor"];

    public static readonly string[] Parentescos = ["filho", "filha", "enteado", "neto", "outro"];

    public static readonly string[] DestinosRede = ["cras", "creas", "defensoria", "conselho_tutelar", "ubs_saude", "caps", "casa_abrigo", "inss", "ddm_delegacia", "outro"];

    public static readonly string[] MotivosEncerramento = ["objetivos_alcancados", "desistencia", "encaminhamento_externo", "mudanca_cidade", "outro"];

    public static readonly string[] TiposAnexo = ["rg", "cpf", "certidao", "comprovante_residencia", "comprovante_renda", "nis_cadunico", "boletim_ocorrencia", "medida_protetiva", "laudo_relatorio", "outro"];

    /// <summary>
    /// Indicadores que o próprio estudo já revela, somados aos marcados pela profissional:
    /// sem renda pela faixa de renda; moradia insegura por situação de rua ou por conviver com o agressor.
    /// </summary>
    public static List<string> VulnerabilidadesComDerivadas(IEnumerable<string> marcadas, string? faixaRenda, string? moradia, bool? conviveComAgressor)
    {
        var lista = marcadas.Where(v => Vulnerabilidades.Contains(v)).ToList();
        if (faixaRenda == "sem_renda") lista.Add("sem_renda");
        if (moradia == "situacao_rua" || conviveComAgressor == true) lista.Add("sem_moradia_segura");
        return Vulnerabilidades.Where(lista.Contains).ToList();
    }
}
