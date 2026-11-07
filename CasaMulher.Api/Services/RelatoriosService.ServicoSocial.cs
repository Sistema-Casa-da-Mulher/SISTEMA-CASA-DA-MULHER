using System.Globalization;
using CasaMulher.Api.DTOs;
using CasaMulher.Api.Models;
using CasaMulher.Api.Security;
using Microsoft.EntityFrameworkCore;

namespace CasaMulher.Api.Services;

/// <summary>
/// Relatórios do Serviço Social. Mesmas regras do módulo: relatos, estudo social, rede e plano só
/// de prontuários que a pessoa pode ver; a Coordenação recebe só números; anexos nunca entram.
/// </summary>
public sealed partial class RelatoriosService
{
    public const string PrefixoServicoSocial = "S:";

    internal static readonly Dictionary<string, string> RotulosTipoSocial = new()
    {
        ["acolhimento"] = "Acolhimento",
        ["individual"] = "Atendimento individual",
        ["visita_domiciliar"] = "Visita domiciliar",
        ["contato_telefonico"] = "Contato telefônico",
        ["articulacao_rede"] = "Articulação com a rede",
        ["outro"] = "Outro"
    };

    internal static readonly Dictionary<string, string> RotulosDestinoRede = new()
    {
        ["cras"] = "CRAS",
        ["creas"] = "CREAS",
        ["defensoria"] = "Defensoria Pública",
        ["conselho_tutelar"] = "Conselho Tutelar",
        ["ubs_saude"] = "UBS / Saúde",
        ["caps"] = "CAPS",
        ["casa_abrigo"] = "Casa-abrigo",
        ["inss"] = "INSS",
        ["ddm_delegacia"] = "DDM / Delegacia",
        ["outro"] = "Outro"
    };

    internal static readonly Dictionary<string, string> RotulosStatusRede = new()
    {
        [EncaminhamentoRedeStatus.Enviado] = "Enviado",
        [EncaminhamentoRedeStatus.RetornoRecebido] = "Retorno recebido",
        [EncaminhamentoRedeStatus.SemRetorno] = "Sem retorno",
        [EncaminhamentoRedeStatus.Cancelado] = "Cancelado"
    };

    internal static readonly Dictionary<string, string> RotulosBeneficio = new()
    {
        ["bolsa_familia"] = "Bolsa Família",
        ["bpc"] = "BPC",
        ["aluguel_social"] = "Aluguel social",
        ["outro"] = "Outros"
    };

    internal static readonly Dictionary<string, string> RotulosVulnerabilidade = new()
    {
        ["inseguranca_alimentar"] = "Insegurança alimentar",
        ["sem_renda"] = "Sem renda",
        ["sem_moradia_segura"] = "Sem moradia segura",
        ["dependencia_economica_agressor"] = "Dependência econômica do agressor"
    };

    internal static readonly Dictionary<string, string> RotulosFaixaRenda = new()
    {
        ["sem_renda"] = "Sem renda",
        ["ate_meio_sm"] = "Até ½ salário mínimo",
        ["meio_a_1_sm"] = "De ½ a 1 salário mínimo",
        ["1_a_2_sm"] = "De 1 a 2 salários mínimos",
        ["2_a_3_sm"] = "De 2 a 3 salários mínimos",
        ["acima_3_sm"] = "Acima de 3 salários mínimos",
        ["nao_informada"] = "Não informada"
    };

    internal static readonly Dictionary<string, string> RotulosMoradia = new()
    {
        ["propria"] = "Própria",
        ["alugada"] = "Alugada",
        ["cedida"] = "Cedida",
        ["abrigo"] = "Abrigo",
        ["situacao_rua"] = "Situação de rua",
        ["outro"] = "Outro"
    };

    internal static readonly Dictionary<string, string> RotulosEncerramentoSocial = new()
    {
        ["objetivos_alcancados"] = "Objetivos alcançados",
        ["desistencia"] = "Interrompido pela assistida",
        ["encaminhamento_externo"] = "Encaminhamento externo",
        ["mudanca_cidade"] = "Mudança de cidade",
        ["outro"] = "Outro"
    };

    internal static readonly Dictionary<string, string> RotulosMeta = new()
    {
        [MetaSocialSituacao.Pendente] = "Pendente",
        [MetaSocialSituacao.EmAndamento] = "Em andamento",
        [MetaSocialSituacao.Concluida] = "Concluída"
    };

    private static string RotuloDestino(EncaminhamentoRede e) =>
        e.Destino == "outro" && !string.IsNullOrWhiteSpace(e.DestinoOutro) ? $"Outro: {e.DestinoOutro}" : RotulosDestinoRede.GetValueOrDefault(e.Destino, e.Destino);

    /// <summary>Versão do estudo social que valia na data (a mais recente gravada até ela).</summary>
    private static EstudoSocial? EstudoAte(ProntuarioSocial p, DateOnly ate) =>
        p.Estudos.Where(e => Local(e.CriadoEm) <= ate).OrderByDescending(e => e.Versao).FirstOrDefault();

    /// <summary>Metas existentes até a data e quantas estavam concluídas nela.</summary>
    private static (int Total, int Concluidas, int Andamento, int Pendentes, int Atrasadas) PlanoAte(IEnumerable<MetaAcompanhamentoSocial> metas, DateOnly ate)
    {
        var lista = metas.Where(m => Local(m.CriadoEm) <= ate).ToList();
        var concluidas = lista.Count(m => m.Situacao == MetaSocialSituacao.Concluida && m.ConcluidaEm is not null && Local(m.ConcluidaEm.Value) <= ate);
        var abertas = lista.Where(m => !(m.Situacao == MetaSocialSituacao.Concluida && m.ConcluidaEm is not null && Local(m.ConcluidaEm.Value) <= ate)).ToList();
        return (lista.Count, concluidas, abertas.Count(m => m.Situacao == MetaSocialSituacao.EmAndamento), abertas.Count(m => m.Situacao != MetaSocialSituacao.EmAndamento),
            abertas.Count(m => m.Prazo is not null && m.Prazo < ate));
    }

    // ================================================================== Serviço Social (estatístico da área)

    private RelatorioResponse GeralServicoSocial(Dados d, PeriodoRelatorio p, PeriodoRelatorio? ant, UsuarioAtuante u, bool meus, DateTime agora)
    {
        var pronts = d.ProntuariosSociais.Values.Where(x => !meus || ResponsavelProntuarioSocial(x, u)).ToList();
        var visiveis = pronts.Where(x => PodeVerConteudoProntuarioSocial(x, u, d, agora)).ToList();
        var idsVisiveis = visiveis.Select(x => x.Id).ToHashSet();
        List<AtendimentoSocial> Ats(PeriodoRelatorio q) => pronts.SelectMany(x => x.Atendimentos).Where(a => q.Dentro(Local(a.RealizadoEm))).ToList();
        var A = Ats(p);
        var aVis = A.Where(a => idsVisiveis.Contains(a.ProntuarioSocialId)).ToList();
        var ativos = pronts.Where(x => ProntuarioSocialAtivoEm(x, p.Fim)).ToList();
        var ids = pronts.Select(x => x.AssistidaId).ToHashSet();
        var fila = d.Assistidas.Where(a => AguardandoServicoSocial(a, d, p.Fim) && (!meus || ids.Contains(a.Id))).ToList();
        var abertosP = pronts.Where(x => x.Atendimentos.Count > 0 && p.Dentro(Local(x.Atendimentos.Min(a => a.RealizadoEm)))).ToList();
        var esperas = abertosP.Where(x => d.PorId[x.AssistidaId].EncaminhadaAssistenciaSocialEm is not null)
            .Select(x => Math.Max(0, Dias(Local(d.PorId[x.AssistidaId].EncaminhadaAssistenciaSocialEm!.Value), Local(x.Atendimentos.Min(a => a.RealizadoEm))))).ToList();
        var encerr = pronts.Where(x => x.EncerradoEm is not null && p.Dentro(Local(x.EncerradoEm.Value))).ToList();
        var redeP = visiveis.SelectMany(x => x.EncaminhamentosRede).Where(e => p.Dentro(e.Data)).ToList();
        var plano = PlanoAte(visiveis.SelectMany(x => x.Metas), p.Fim);
        var estudos = visiveis.Where(x => ProntuarioSocialAtivoEm(x, p.Fim) || p.Dentro(Local(x.EncerradoEm))).Select(x => EstudoAte(x, p.Fim)).Where(e => e is not null).Select(e => e!).ToList();
        var comVulnerabilidade = ativos.Count(x => !string.IsNullOrWhiteSpace(x.VulnerabilidadesAtuais));

        var r = new RelatorioResponse { Titulo = "Relatório Estatístico/Geral do Serviço Social" + (meus ? " · só os meus prontuários" : "") };
        r.Kpis =
        [
            KpiN(A.Count, "Atendimentos registrados", ant is null ? null : Ats(ant).Count),
            Kpi(N(ativos.Count), "Em acompanhamento", "No fim do período"),
            Kpi(N(abertosP.Count), "Prontuários abertos"),
            Kpi(N(encerr.Count), "Acompanhamentos encerrados"),
            Kpi(N(fila.Count), "Na fila"),
            Kpi(esperas.Count > 0 ? Math.Round(esperas.Average()) + " dias" : "-", "Espera média na fila"),
            Kpi(N(redeP.Count), "Encaminhamentos à rede", $"{redeP.Count(e => e.Status == EncaminhamentoRedeStatus.RetornoRecebido)} com retorno"),
            Kpi(plano.Total > 0 ? Pct(plano.Concluidas, plano.Total) + "%" : "-", "Plano de acompanhamento concluído", $"{plano.Concluidas} de {plano.Total} metas"),
            Kpi(N(comVulnerabilidade), "Com aviso de vulnerabilidade", "Acompanhamentos ativos", alerta: comVulnerabilidade > 0)
        ];

        if (pronts.Count != visiveis.Count)
        {
            r.Avisos.Add(new RelatorioAvisoDto
            {
                Tipo = "sigilo",
                Texto = $"{pronts.Count - visiveis.Count} prontuário(s) são só das assistentes sociais responsáveis. Deles entram apenas a contagem de atendimentos, a fila e o aviso de vulnerabilidade; tipo de atendimento, rede, estudo social e plano vêm só dos prontuários que você pode ver."
            });
        }

        var profissionais = d.Usuarios.Values.Where(x => x.Perfil == PerfisAcesso.AssistenteSocial && (x.Ativo || A.Any(a => a.ProfissionalUserId == x.Id))).OrderBy(x => x.Nome).ToList();
        var tempos = encerr.Where(x => x.Atendimentos.Count > 0).Select(x => Dias(Local(x.Atendimentos.Min(a => a.RealizadoEm)), Local(x.EncerradoEm!.Value))).ToList();

        r.Secoes.Add(new RelatorioSecaoDto
        {
            Blocos =
            [
                Colunas("Atendimentos por mês", NotaMes(p), PorMes(p, pronts.SelectMany(x => x.Atendimentos).Select(a => Local(a.RealizadoEm))), "roxo"),
                Barras("Atendimentos por tipo", "Prontuários que você pode ver.", Contar(aVis, a => RotulosTipoSocial.GetValueOrDefault(a.Tipo, a.Tipo))),
                new RelatorioBlocoDto
                {
                    Tipo = "barras", Titulo = "Encaminhamentos à rede por destino", Nota = $"{redeP.Count} encaminhamento(s) com data no período.",
                    Itens = Contar(redeP, e => RotulosDestinoRede.GetValueOrDefault(e.Destino, e.Destino)),
                    Vazio = "Nenhum encaminhamento à rede no período."
                },
                Pilha("Situação dos encaminhamentos à rede", "Situação atual dos encaminhamentos do período.",
                    RotulosStatusRede.Select(k => Item(k.Value, redeP.Count(e => e.Status == k.Key), k.Key switch
                    {
                        EncaminhamentoRedeStatus.RetornoRecebido => "ok",
                        EncaminhamentoRedeStatus.SemRetorno => "falta",
                        EncaminhamentoRedeStatus.Cancelado => "neutro",
                        _ => "justificada"
                    }))),
                Barras("Benefícios", $"Estudo social vigente no fim do período ({estudos.Count} acompanhamento(s) com estudo).", Contar(estudos, e => Separar(e.Beneficios, RotulosBeneficio))),
                Barras("Indicadores de vulnerabilidade", "Do estudo social vigente. Uma assistida pode ter mais de um.", Contar(estudos, e => Separar(e.Vulnerabilidades, RotulosVulnerabilidade))),
                Barras("Renda familiar aproximada", null, Contar(estudos, e => RotulosFaixaRenda.GetValueOrDefault(e.FaixaRenda ?? "", "Não informada"))),
                Barras("Moradia", $"Convivem com o agressor: {estudos.Count(e => e.ConviveComAgressor == true)}", Contar(estudos, e => RotulosMoradia.GetValueOrDefault(e.Moradia ?? "", "Sem registro"))),
                Kpis("Plano de acompanhamento", "Metas e ações dos prontuários que você pode ver, como estavam no fim do período.",
                    Kpi(N(plano.Total), "Metas no plano"),
                    Kpi(N(plano.Concluidas), "Concluídas"),
                    Kpi(N(plano.Andamento), "Em andamento"),
                    Kpi(N(plano.Pendentes), "Pendentes"),
                    Kpi(N(plano.Atrasadas), "Com prazo vencido", alerta: plano.Atrasadas > 0)),
                Barras("Motivos de encerramento", $"{encerr.Count} encerramentos no período.", Contar(encerr.Where(x => idsVisiveis.Contains(x.Id)), x => RotulosEncerramentoSocial.GetValueOrDefault(x.MotivoEncerramento ?? "", "Outro"))),
                Barras("Por onde entraram na Casa", "Das assistidas que abriram prontuário no período.", Contar(abertosP, x => RotuloOrigemPara(d.PorId[x.AssistidaId].Origem, PerfisAcesso.AssistenteSocial))),
                Tabela("Por assistente social", null, [Col("Assistente social"), Col("Atendimentos", true), Col("Prontuários ativos", true)],
                    profissionais.Select(x => Linha(C(x.Nome), C(A.Count(a => a.ProfissionalUserId == x.Id)), C(ativos.Count(pr => pr.Responsaveis.Any(rr => rr.UserId == x.Id)))))),
                Kpis("Duração e sigilo", null,
                    Kpi(tempos.Count > 0 ? Math.Round(tempos.Average()) + " dias" : "-", "Tempo médio até o encerramento"),
                    Kpi(N(d.EmergenciasSocial.Count(e => p.Dentro(Local(e.CriadoEm)) && pronts.Any(x => x.Id == e.ProntuarioSocialId))), "Acessos de emergência"),
                    Kpi(N(visiveis.SelectMany(x => x.Anexos).Count(a => !a.Excluido && p.Dentro(Local(a.CriadoEm)))), "Documentos anexados", "Os documentos não entram no PDF"))
            ]
        });

        var linhas = ativos.Concat(encerr.Where(x => !ativos.Contains(x)))
            .Select(pr =>
            {
                var a = d.PorId[pr.AssistidaId];
                var noP = A.Count(x => x.ProntuarioSocialId == pr.Id);
                var todos = pr.Atendimentos.Where(x => Local(x.RealizadoEm) <= p.Fim).OrderBy(x => x.RealizadoEm).ToList();
                return (noP, Linha(
                    C(pr.Numero), C(a.Nome, sub: a.AcessoRestrito ? "Acesso restrito" : null),
                    C(pr.Responsaveis.Count == 0 ? "-" : string.Join(", ", pr.Responsaveis.Select(x => x.Nome))),
                    C(noP), C(todos.Count), C(todos.Count > 0 ? Fd(Local(todos[^1].RealizadoEm)) : "-"),
                    string.IsNullOrWhiteSpace(pr.VulnerabilidadesAtuais) ? C("-") : C("Sim", "risco"),
                    C(pr.EncerradoEm is not null && Local(pr.EncerradoEm.Value) <= p.Fim ? "Encerrado" : "Em acompanhamento"),
                    pr.Visibilidade == ProntuarioVisibilidade.Equipe ? C("Equipe") : C("Só a responsável", "neutro")));
            })
            .OrderByDescending(x => x.noP).Select(x => x.Item2).ToList();
        r.Secoes.Add(new RelatorioSecaoDto
        {
            Blocos = [Tabela("Prontuários sociais", "Ativos no fim do período e encerrados nele. Sem relatos nem estudo social.",
                [Col("Prontuário"), Col("Assistida"), Col("Responsável"), Col("Atendimentos no período", true), Col("Total", true), Col("Último atendimento"), Col("Vulnerabilidade"), Col("Situação"), Col("Anotações")], linhas, true)]
        });

        return r;
    }

    // ================================================================== Serviço Social para a Coordenação (só números)

    private static RelatorioSecaoDto SecaoAdmServicoSocial(Dados d, PeriodoRelatorio p)
    {
        var pronts = d.ProntuariosSociais.Values.ToList();
        var atsP = pronts.SelectMany(x => x.Atendimentos).Where(a => p.Dentro(Local(a.RealizadoEm))).ToList();
        var redeP = pronts.SelectMany(x => x.EncaminhamentosRede).Where(e => p.Dentro(e.Data)).ToList();
        var encerrados = pronts.Where(x => x.EncerradoEm is not null && p.Dentro(Local(x.EncerradoEm.Value))).ToList();
        var estudos = pronts.Where(x => ProntuarioSocialAtivoEm(x, p.Fim)).Select(x => EstudoAte(x, p.Fim)).Where(e => e is not null).Select(e => e!).ToList();
        var plano = PlanoAte(pronts.SelectMany(x => x.Metas), p.Fim);

        return new RelatorioSecaoDto
        {
            Titulo = "Serviço Social em números",
            Subtitulo = "(sem nomes: a Coordenação não vê relatos, estudo social nem documentos)",
            Blocos =
            [
                Kpis("Acompanhamentos", null,
                    Kpi(N(pronts.Count(x => ProntuarioSocialAtivoEm(x, p.Fim))), "Em acompanhamento"),
                    Kpi(N(pronts.Count(x => x.Atendimentos.Count > 0 && p.Dentro(Local(x.Atendimentos.Min(a => a.RealizadoEm))))), "Prontuários abertos"),
                    Kpi(N(encerrados.Count), "Encerrados"),
                    Kpi(plano.Total > 0 ? Pct(plano.Concluidas, plano.Total) + "%" : "-", "Plano concluído", $"{plano.Concluidas} de {plano.Total} metas")),
                Barras("Atendimentos por tipo", $"{atsP.Count} atendimentos no período.", Contar(atsP, a => RotulosTipoSocial.GetValueOrDefault(a.Tipo, a.Tipo))),
                new RelatorioBlocoDto
                {
                    Tipo = "barras", Titulo = "Encaminhamentos à rede por destino", Nota = $"{redeP.Count} no período.",
                    Itens = Contar(redeP, e => RotulosDestinoRede.GetValueOrDefault(e.Destino, e.Destino)),
                    Extras = [Pilha("Situação", null, RotulosStatusRede.Select(k => Item(k.Value, redeP.Count(e => e.Status == k.Key), k.Key == EncaminhamentoRedeStatus.RetornoRecebido ? "ok" : k.Key == EncaminhamentoRedeStatus.SemRetorno ? "falta" : "neutro")))]
                },
                new RelatorioBlocoDto
                {
                    Tipo = "barras", Titulo = "Benefícios e vulnerabilidades", Nota = "Contagens do estudo social dos acompanhamentos ativos no fim do período.",
                    Itens = Contar(estudos, e => Separar(e.Beneficios, RotulosBeneficio)),
                    Extras = [Barras("", null, Contar(estudos, e => Separar(e.Vulnerabilidades, RotulosVulnerabilidade)))]
                },
                Barras("Motivos de encerramento", null, Contar(encerrados, x => RotulosEncerramentoSocial.GetValueOrDefault(x.MotivoEncerramento ?? "", "Outro")))
            ]
        };
    }

    // ================================================================== individual

    private List<RelatorioBlocoDto> HistoricoServicoSocial(ProntuarioSocial? pront, List<AtendimentoSocial> aP, UsuarioAtuante u, bool podeVer, PeriodoRelatorio p, DateOnly hoje)
    {
        if (pront is null || (pront.Atendimentos.Count == 0 && pront.Estudos.Count == 0))
        {
            return [Texto("Prontuário social", null, "Encaminhada e ainda na fila do Serviço Social.", largo: true)];
        }

        var responsavel = pront.Responsaveis.OrderBy(x => x.AtribuidoEm).FirstOrDefault();
        var abertura = pront.Atendimentos.Count > 0 ? Local(pront.Atendimentos.Min(a => a.RealizadoEm)) : Local(pront.CriadoEm);
        var pares = new List<RelatorioParDto>
        {
            new() { Rotulo = "Prontuário", Valor = pront.Numero },
            new() { Rotulo = "Assistente social responsável", Valor = responsavel is null ? "Sem responsável" : responsavel.Nome + (string.IsNullOrWhiteSpace(responsavel.Cress) ? "" : $" · {responsavel.Cress}") },
            new() { Rotulo = "Aberto em", Valor = Fd(abertura) },
            new() { Rotulo = "Situação", Valor = pront.EncerradoEm is null ? "Em acompanhamento" : $"Encerrado em {Fd(Local(pront.EncerradoEm.Value))}" + (podeVer ? " · " + RotulosEncerramentoSocial.GetValueOrDefault(pront.MotivoEncerramento ?? "", "Outro") : "") },
            new() { Rotulo = "Quem vê as anotações", Valor = pront.Visibilidade == ProntuarioVisibilidade.Equipe ? "Toda a equipe do Serviço Social" : "Somente a responsável" }
        };

        if (!podeVer)
        {
            return
            [
                new RelatorioBlocoDto
                {
                    Tipo = "pares", Titulo = "Prontuário social", Largo = true, Pares = pares,
                    Extras =
                    [
                        Texto("", null, "O conteúdo deste prontuário é só das assistentes sociais responsáveis. Você vê que os atendimentos existiram. Em emergência, peça acesso na ficha do Serviço Social (vale 12 horas, fica registrado e a responsável é avisada).", "sigilo"),
                        Tabela("", null, [Col("Atendimento"), Col("Data"), Col("Profissional")], aP.Select(a => Linha(C("nº " + a.Numero), C(Fd(Local(a.RealizadoEm))), C(a.ProfissionalNome))), vazio: "Nenhum atendimento no período.")
                    ]
                }
            ];
        }

        var resp = ResponsavelProntuarioSocial(pront, u);
        var estudo = EstudoAte(pront, p.Fim);
        var redeP = pront.EncaminhamentosRede.Where(e => p.Dentro(e.Data)).OrderBy(e => e.Data).ToList();
        var plano = PlanoAte(pront.Metas, p.Fim);
        var blocos = new List<RelatorioBlocoDto>
        {
            new()
            {
                Tipo = "pares",
                Titulo = "Prontuário social",
                Nota = "Relatos ficam ocultos: marque “Incluir relatos” no topo para escolher quais entram (só a assistente social responsável). Documentos anexados nunca entram no PDF.",
                Largo = true,
                Pares = pares
            }
        };

        if (estudo is not null)
        {
            var dependentes = TextoDependentes(estudo);
            blocos.Add(new RelatorioBlocoDto
            {
                Tipo = "pares",
                Titulo = $"Estudo social (versão {estudo.Versao}, {Fd(Local(estudo.CriadoEm))})",
                Nota = "Ficha socioeconômica vigente no fim do período. O NIS não sai no relatório.",
                Pares =
                [
                    new() { Rotulo = "Pessoas no domicílio", Valor = estudo.PessoasNoDomicilio?.ToString(CultureInfo.InvariantCulture) ?? "-" },
                    new() { Rotulo = "Dependentes", Valor = dependentes },
                    new() { Rotulo = "Renda familiar", Valor = RotulosFaixaRenda.GetValueOrDefault(estudo.FaixaRenda ?? "", "-") },
                    new() { Rotulo = "Moradia", Valor = RotulosMoradia.GetValueOrDefault(estudo.Moradia ?? "", "-") + (estudo.ConviveComAgressor == true ? " · convive com o agressor" : "") },
                    new() { Rotulo = "CadÚnico", Valor = estudo.CadUnico switch { "sim" => "Sim", "nao" => "Não", "nao_sabe" => "Não sabe", _ => "-" } },
                    new() { Rotulo = "Benefícios", Valor = Separar(estudo.Beneficios, RotulosBeneficio).DefaultIfEmpty("Nenhum").Aggregate((x, y) => x + ", " + y) },
                    new() { Rotulo = "Vulnerabilidades", Valor = Separar(estudo.Vulnerabilidades, RotulosVulnerabilidade).DefaultIfEmpty("Nenhuma registrada").Aggregate((x, y) => x + ", " + y) }
                ]
            });
        }

        blocos.Add(Tabela("Encaminhamentos à rede no período", "Destino, data e situação. O motivo fica no prontuário.",
            [Col("Data"), Col("Destino"), Col("Situação")],
            redeP.Select(e => Linha(C(Fd(e.Data)), C(RotuloDestino(e)), C(RotulosStatusRede.GetValueOrDefault(e.Status, e.Status), e.Status == EncaminhamentoRedeStatus.RetornoRecebido ? "ok" : "neutro"))),
            vazio: "Nenhum encaminhamento à rede no período."));

        blocos.Add(new RelatorioBlocoDto
        {
            Tipo = "tabela",
            Titulo = "Plano de acompanhamento",
            Nota = plano.Total > 0 ? $"{plano.Concluidas} de {plano.Total} metas concluídas ({Pct(plano.Concluidas, plano.Total)}%) no fim do período." : null,
            Colunas = [Col("Meta / ação"), Col("Prazo"), Col("Situação")],
            Linhas = pront.Metas.Where(m => Local(m.CriadoEm) <= p.Fim).OrderBy(m => m.Prazo ?? DateOnly.MaxValue)
                .Select(m => Linha(C(m.Descricao), C(m.Prazo is null ? "-" : Fd(m.Prazo)), C(RotulosMeta.GetValueOrDefault(m.Situacao, m.Situacao), m.Situacao == MetaSocialSituacao.Concluida ? "ok" : m.Prazo is not null && m.Prazo < hoje ? "risco" : "neutro")))
                .ToList(),
            Vazio = "Nenhuma meta no plano."
        });

        blocos.Add(new RelatorioBlocoDto
        {
            Tipo = "registros",
            Titulo = "Atendimentos no período",
            Vazio = "Nenhum atendimento no período.",
            Registros = aP.OrderBy(a => a.RealizadoEm).Select(a => new RelatorioRegistroDto
            {
                Chave = resp ? PrefixoServicoSocial + a.Id : null,
                Titulo = $"Atendimento nº {a.Numero}",
                Subtitulo = $"{HoraLocal(a.RealizadoEm):dd/MM/yyyy HH:mm} · {a.ProfissionalNome}",
                Badges = [Badge(RotulosTipoSocial.GetValueOrDefault(a.Tipo, a.Tipo), "juridico")],
                Campos =
                [
                    new() { Rotulo = "Tipo", Valor = a.Tipo == "outro" && !string.IsNullOrWhiteSpace(a.TipoOutro) ? $"Outro: {a.TipoOutro}" : RotulosTipoSocial.GetValueOrDefault(a.Tipo, a.Tipo) },
                    new() { Rotulo = "Adendos", Valor = a.Adendos.Count == 0 ? "Nenhum" : $"{a.Adendos.Count} (entram junto com o relato)" },
                    new() { Rotulo = "Documentos anexados", Valor = pront.Anexos.Count(x => !x.Excluido && x.AtendimentoSocialId == a.Id) is var n && n > 0 ? $"{n} (não entram no PDF)" : "Nenhum" }
                ]
            }).ToList()
        });

        return blocos;
    }

    private static string TextoDependentes(EstudoSocial e)
    {
        List<DependenteDto> lista;
        try
        {
            lista = System.Text.Json.JsonSerializer.Deserialize<List<DependenteDto>>(e.DependentesJson, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)) ?? [];
        }
        catch (System.Text.Json.JsonException)
        {
            lista = [];
        }

        if (lista.Count == 0) return "Nenhum informado";
        return string.Join(", ", lista.Select(x => $"{x.Parentesco}{(x.Idade is null ? "" : $" ({x.Idade} anos)")}"));
    }

    private async Task<List<RelatorioConteudoSigilosoDto>?> ConteudoSigilosoSocialAsync(UsuarioAtuante u, int assistidaId, List<string> chaves, CancellationToken ct)
    {
        var pront = await _db.ProntuariosSociais.AsNoTracking()
            .Include(p => p.Responsaveis)
            .Include(p => p.Atendimentos).ThenInclude(a => a.Adendos)
            .SingleOrDefaultAsync(p => p.AssistidaId == assistidaId && p.Assistida!.Ativa, ct);
        if (!ResponsavelProntuarioSocial(pront, u)) return null;

        var resultado = new List<RelatorioConteudoSigilosoDto>();
        foreach (var chave in chaves)
        {
            if (!chave.StartsWith(PrefixoServicoSocial, StringComparison.Ordinal) || !int.TryParse(chave[PrefixoServicoSocial.Length..], out var id)) return null;
            var a = pront!.Atendimentos.SingleOrDefault(x => x.Id == id);
            if (a is null) return null;
            var textos = new List<RelatorioParDto> { new() { Rotulo = "Relato / evolução", Valor = a.Relato } };
            if (!string.IsNullOrWhiteSpace(a.Providencias)) textos.Add(new RelatorioParDto { Rotulo = "Providências", Valor = a.Providencias });
            textos.AddRange(a.Adendos.OrderBy(x => x.CriadoEm).Select(x => new RelatorioParDto { Rotulo = $"Adendo {Fd(Local(x.CriadoEm))} · {x.AutorNome}", Valor = x.Texto }));
            resultado.Add(new RelatorioConteudoSigilosoDto { Chave = chave, Rotulo = $"atendimento nº {a.Numero} ({pront.Numero})", Textos = textos });
        }

        return resultado;
    }
}
