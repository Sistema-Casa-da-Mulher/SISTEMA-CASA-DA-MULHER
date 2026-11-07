namespace CasaMulher.Api.DTOs;

/// <summary>
/// Relatório já calculado no servidor, descrito em blocos genéricos (KPIs, barras, tabelas...).
/// O navegador só desenha: nenhum dado que o perfil não pode ver chega até ele.
/// </summary>
public class RelatorioResponse
{
    public string Perfil { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public RelatorioPeriodoDto Periodo { get; set; } = new();
    public string? Comparacao { get; set; }
    public List<RelatorioAvisoDto> Avisos { get; set; } = [];
    public List<RelatorioKpiDto> Kpis { get; set; } = [];
    public List<RelatorioSecaoDto> Secoes { get; set; } = [];
    public RelatorioCriteriosDto? Criterios { get; set; }
    public string? Rodape { get; set; }
}

public class RelatorioIndividualResponse : RelatorioResponse
{
    public int AssistidaId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public List<RelatorioBadgeDto> Badges { get; set; } = [];
    public List<RelatorioParDto> Ficha { get; set; } = [];

    /// <summary>relatos (Jurídico) ou evolucoes (Psicologia); vazio quando o perfil não tem conteúdo sigiloso.</summary>
    public string? TipoSigiloso { get; set; }

    /// <summary>Só a responsável pelo caso/prontuário pode incluir conteúdo sigiloso no PDF.</summary>
    public bool PodeIncluirSigiloso { get; set; }

    /// <summary>Explica por que a opção não libera nada (quando não pode incluir).</summary>
    public string? AvisoSigiloso { get; set; }

    public List<RelatorioRegistroSigilosoDto> RegistrosSigilosos { get; set; } = [];

    public bool PodeDocumentoExterno { get; set; }
}

public class RelatorioPeriodoDto
{
    public string Tipo { get; set; } = "mensal";
    public string? Ref { get; set; }
    public string Ini { get; set; } = string.Empty;
    public string Fim { get; set; } = string.Empty;
    public string Rotulo { get; set; } = string.Empty;
}

public class RelatorioAvisoDto
{
    /// <summary>info, alerta, risco, restrito ou sigilo.</summary>
    public string Tipo { get; set; } = "info";
    public string? Titulo { get; set; }
    public string? Texto { get; set; }
    public List<RelatorioPontoDto> Pontos { get; set; } = [];
    public List<string> Itens { get; set; } = [];
    public string? Nota { get; set; }
}

public class RelatorioPontoDto
{
    public string Titulo { get; set; } = string.Empty;
    public string Texto { get; set; } = string.Empty;
    public string? Quem { get; set; }
    public string? Acao { get; set; }
}

public class RelatorioCriteriosDto
{
    public int PrazoFila { get; set; }
    public int FaltasSeguidas { get; set; }
    public int DiasRetornoVencido { get; set; }
    public int OcupacaoBaixa { get; set; }
}

public class RelatorioKpiDto
{
    public string Valor { get; set; } = "-";
    public string Rotulo { get; set; } = string.Empty;
    public string? Ajuda { get; set; }
    public string? Titulo { get; set; }
    public double? ValorNum { get; set; }
    public double? Anterior { get; set; }
    public string? SufixoDelta { get; set; }
    public bool Alerta { get; set; }
    public bool DadoNovo { get; set; }
}

public class RelatorioSecaoDto
{
    public string? Titulo { get; set; }
    public string? Subtitulo { get; set; }
    public List<RelatorioBlocoDto> Blocos { get; set; } = [];
}

public class RelatorioBlocoDto
{
    public string Titulo { get; set; } = string.Empty;
    public string? Nota { get; set; }
    public bool Largo { get; set; }
    public bool DadoNovo { get; set; }

    /// <summary>barras, colunas, pilha, tabela, kpis, texto, sigilo, podio, ficha, marcos, registros, chamada, risco.</summary>
    public string Tipo { get; set; } = "texto";
    public List<RelatorioItemDto> Itens { get; set; } = [];
    public string? Sufixo { get; set; }
    public double? Max { get; set; }
    public string? Cor { get; set; }
    public List<RelatorioColunaDto> Colunas { get; set; } = [];
    public List<List<RelatorioCelulaDto>> Linhas { get; set; } = [];
    public string? Texto { get; set; }
    public string? Vazio { get; set; }
    public List<RelatorioKpiDto> Kpis { get; set; } = [];
    public List<RelatorioParDto> Pares { get; set; } = [];
    public List<RelatorioMarcoDto> Marcos { get; set; } = [];
    public List<RelatorioRegistroDto> Registros { get; set; } = [];
    public List<RelatorioItemDto> Legenda { get; set; } = [];

    /// <summary>Blocos menores dentro deste (ex.: barras extras abaixo de uma pilha).</summary>
    public List<RelatorioBlocoDto> Extras { get; set; } = [];
}

public class RelatorioItemDto
{
    public string Rotulo { get; set; } = string.Empty;
    public double Valor { get; set; }
    public string? Extra { get; set; }

    /// <summary>Nome de cor da paleta (ok, falta, justificada, neutro, roxo, azul, barra, risco_*).</summary>
    public string? Cor { get; set; }

    public string? Tipo { get; set; }
}

public class RelatorioColunaDto
{
    public string T { get; set; } = string.Empty;
    public bool Num { get; set; }
    public bool DadoNovo { get; set; }
}

public class RelatorioCelulaDto
{
    public string Texto { get; set; } = string.Empty;
    public string? Badge { get; set; }
    public string? Sub { get; set; }
    public bool Destaque { get; set; }

    /// <summary>0 a 1, para o mapa de calor (movimento por dia e horário).</summary>
    public double? Calor { get; set; }
}

public class RelatorioParDto
{
    public string Rotulo { get; set; } = string.Empty;
    public string Valor { get; set; } = string.Empty;
    public string? Sub { get; set; }
    public bool DadoNovo { get; set; }
}

public class RelatorioBadgeDto
{
    public string Texto { get; set; } = string.Empty;
    public string? Tipo { get; set; }
}

public class RelatorioMarcoDto
{
    public string Data { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public string Texto { get; set; } = string.Empty;
    public string? Quem { get; set; }
}

/// <summary>Atendimento ou sessão mostrado como cartão. Chave != null quando o conteúdo pode ser escolhido para o PDF.</summary>
public class RelatorioRegistroDto
{
    public string? Chave { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string? Subtitulo { get; set; }
    public List<RelatorioBadgeDto> Badges { get; set; } = [];
    public List<RelatorioParDto> Campos { get; set; } = [];
}

public class RelatorioRegistroSigilosoDto
{
    public string Chave { get; set; } = string.Empty;
    public string Rotulo { get; set; } = string.Empty;
}

public class RelatorioAssistidaOpcaoDto
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string CpfMascarado { get; set; } = string.Empty;
    public string Entrada { get; set; } = string.Empty;
}

public class RelatorioContextoResponse
{
    public string Perfil { get; set; } = string.Empty;
    public string PerfilNome { get; set; } = string.Empty;
    public string TituloArea { get; set; } = string.Empty;
    public string Intro { get; set; } = string.Empty;
    public string InicioDados { get; set; } = string.Empty;
    public string Hoje { get; set; } = string.Empty;

    /// <summary>Filtro extra do relatório da área: escopo (Jurídico/Psicologia) ou curso (Professora).</summary>
    public string? FiltroRotulo { get; set; }
    public List<RelatorioOpcaoDto> FiltroOpcoes { get; set; } = [];
    public string? RotuloSigiloso { get; set; }
    public bool DocumentoExterno { get; set; }
    public string? VoltarUrl { get; set; }
    public string? VoltarRotulo { get; set; }
    public List<string> VisoesDisponiveis { get; set; } = [];
}

public class RelatorioOpcaoDto
{
    public string Valor { get; set; } = string.Empty;
    public string Rotulo { get; set; } = string.Empty;
}

public class RelatorioSigilososRequest
{
    public List<string> Registros { get; set; } = [];
}

public class RelatorioConteudoSigilosoDto
{
    public string Chave { get; set; } = string.Empty;
    public string Rotulo { get; set; } = string.Empty;
    public List<RelatorioParDto> Textos { get; set; } = [];
}

public class RelatorioImpressaoRequest
{
    /// <summary>geral ou individual.</summary>
    public string Tipo { get; set; } = "geral";
    public int? AssistidaId { get; set; }
    public string? Periodo { get; set; }
    public string? Ref { get; set; }
    public string? Visao { get; set; }
    public List<string> Registros { get; set; } = [];
}

public class RelatorioImpressaoResponse
{
    public string Mensagem { get; set; } = string.Empty;
    public int RegistrosSigilosos { get; set; }
    public List<string> Rotulos { get; set; } = [];
    public DateTime RegistradoEm { get; set; }
}

public class DocumentoExternoRequest
{
    public string? Destinatario { get; set; }
    public string? Finalidade { get; set; }
    public string? Periodo { get; set; }
    public string? Ref { get; set; }
    public bool Confirmar { get; set; }
}

public class DocumentoExternoResponse
{
    public string Instituicao { get; set; } = "Casa da Mulher de Itaquaquecetuba";
    public string Nome { get; set; } = string.Empty;
    public string Cpf { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public string Situacao { get; set; } = string.Empty;
    public int Quantidade { get; set; }
    public string RotuloQuantidade { get; set; } = string.Empty;
    public string PeriodoIni { get; set; } = string.Empty;
    public string PeriodoFim { get; set; } = string.Empty;
    public string ProfissionalNome { get; set; } = string.Empty;
    public string ProfissionalRegistro { get; set; } = string.Empty;
    public bool Registrado { get; set; }
}

public class AgendarJuridicoRequest
{
    /// <summary>Data e hora local (yyyy-MM-ddTHH:mm).</summary>
    public string? DataHora { get; set; }
}

public class CancelarEncaminhamentoJuridicoRequest
{
    public string? Motivo { get; set; }
}

public class SituacaoAlunaRequest
{
    public string Situacao { get; set; } = string.Empty;
    public string? TerminoEm { get; set; }
    public string? MotivoSaida { get; set; }
}

public class VagasCursoRequest
{
    public int? Vagas { get; set; }
}
