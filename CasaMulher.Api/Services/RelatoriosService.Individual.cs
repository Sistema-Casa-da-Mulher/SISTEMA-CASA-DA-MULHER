using System.Globalization;
using CasaMulher.Api.DTOs;
using CasaMulher.Api.Models;
using CasaMulher.Api.Security;
using Microsoft.EntityFrameworkCore;

namespace CasaMulher.Api.Services;

/// <summary>Relatório individual da assistida, com as regras de sigilo de cada perfil.</summary>
public sealed partial class RelatoriosService
{
    public const string PrefixoJuridico = "J:";
    public const string PrefixoPsicologia = "P:";

    public async Task<RelatorioIndividualResponse?> GerarIndividualAsync(
        UsuarioAtuante usuario,
        string perfil,
        int assistidaId,
        string? tipo,
        string? referencia,
        CancellationToken ct)
    {
        var d = await CarregarAsync(ct);
        var a = AssistidasVisiveis(perfil, usuario, d).SingleOrDefault(x => x.Id == assistidaId);
        if (a is null) return null;

        var agora = DateTime.UtcNow;
        var hoje = Hoje;
        var entrada = Local(a.CriadoEm);
        var meusCursos = perfil == PerfisAcesso.Professor ? CursosDaProfessora(usuario, d) : null;
        var mats = d.Matriculas.Where(m => m.Assistida.Id == a.Id && (meusCursos is null || meusCursos.Contains(m.Curso.Id))).OrderBy(m => m.Inicio).ToList();
        var inicioRef = perfil == PerfisAcesso.Professor && mats.Count > 0 ? mats.Min(m => m.Inicio) : entrada;
        var p = DesdeEntrada(ResolverPeriodo(tipo, referencia, d.InicioDados), inicioRef, perfil == PerfisAcesso.Professor ? "a entrada no curso" : "a entrada na Casa");

        var caso = d.Casos.GetValueOrDefault(a.Id);
        var pront = d.Prontuarios.GetValueOrDefault(a.Id);
        var prontAtivo = pront is not null && pront.Situacao == ProntuarioSituacao.EmAcompanhamento && pront.Sessoes.Count > 0;
        var riscoGrave = prontAtivo && PsicologiaCatalogo.RiscoElevado(pront!.RiscoAtual);
        var ehJur = perfil == PerfisAcesso.Juridico;
        var ehPsi = perfil == PerfisAcesso.Psicologia;
        var ehAdm = perfil == PerfisAcesso.Adm;
        var ehRec = perfil == PerfisAcesso.Recepcao;
        var ehProf = perfil == PerfisAcesso.Professor;
        var ehSoc = perfil == PerfisAcesso.AssistenteSocial;
        var podeVerCaso = ehJur && caso is not null && PodeVerConteudoCaso(caso, usuario, d, agora);
        var podeVerPront = ehPsi && pront is not null && PodeVerConteudoProntuario(pront, usuario, d, agora);
        var prontSoc = d.ProntuariosSociais.GetValueOrDefault(a.Id);
        var socAtivo = prontSoc is not null && prontSoc.Situacao == ProntuarioSituacao.EmAcompanhamento && prontSoc.Atendimentos.Count > 0;
        var vulneravel = socAtivo && !string.IsNullOrWhiteSpace(prontSoc!.VulnerabilidadesAtuais);
        var podeVerSoc = ehSoc && prontSoc is not null && PodeVerConteudoProntuarioSocial(prontSoc, usuario, d, agora);

        var r = new RelatorioIndividualResponse
        {
            Perfil = perfil,
            Titulo = "Relatório individual da assistida",
            AssistidaId = a.Id,
            Nome = a.Nome,
            Periodo = p.ParaDto(),
            Rodape = Rodape(usuario, agora).Replace("A impressão deste relatório fica registrada", "A geração e a impressão deste relatório ficam registradas")
        };

        // ---- cabeçalho
        if (a.AcessoRestrito && !ehRec && !ehProf) r.Badges.Add(Badge("Acesso restrito", "restrito"));
        if (ehPsi && pront is not null) r.Badges.Add(Badge("Prontuário " + pront.Numero, "psicologia"));
        if ((ehPsi || ehJur) && riscoGrave) r.Badges.Add(Badge("Risco " + RotuloRisco(pront!.RiscoAtual).ToLower(PtBr), "risco"));
        if ((ehJur || ehSoc) && prontAtivo) r.Badges.Add(Badge("Em acompanhamento na Psicologia", "psicologia"));
        if (ehSoc && prontSoc is not null) r.Badges.Add(Badge("Prontuário " + prontSoc.Numero, "juridico"));
        if ((ehJur || ehPsi) && socAtivo) r.Badges.Add(Badge("Em acompanhamento no Serviço Social", "juridico"));
        if ((ehJur || ehPsi || ehSoc) && vulneravel) r.Badges.Add(Badge("Aviso de vulnerabilidade social", "risco"));
        if (ehSoc && caso is not null && caso.Atendimentos.Count > 0 && caso.Situacao != CasoJuridicoSituacao.Encerrado) r.Badges.Add(Badge("Em acompanhamento no Jurídico", "juridico"));
        if (ehPsi && caso is not null && caso.Atendimentos.Count > 0 && caso.Situacao != CasoJuridicoSituacao.Encerrado) r.Badges.Add(Badge("Em acompanhamento no Jurídico", "juridico"));
        if (ehJur && caso is not null && caso.Atendimentos.Count > 0) r.Badges.Add(Badge("Caso " + RotulosSituacaoCaso.GetValueOrDefault(caso.Situacao, caso.Situacao).ToLower(PtBr), "juridico"));
        var cursando = mats.Count(m => m.Situacao == AssistidaCursoSituacao.Cursando);
        if (cursando > 0 && !ehProf) r.Badges.Add(Badge(cursando == 1 ? "Cursando 1 curso" : $"Cursando {cursando} cursos", "ok"));

        var contato = ContatoTexto(a);
        var tempo = Dias(entrada, hoje);
        var tempoTxt = tempo > 60 ? $"{Math.Floor(tempo / 30.4)} meses" : $"{tempo} dias";
        var cadastradaPor = (a.Origem == AssistidaOrigem.Psicologia && !ehPsi) || (a.Origem == AssistidaOrigem.ServicoSocial && !ehSoc)
            ? "Equipe técnica da Casa"
            : $"{a.CadastradaPorNome} ({a.Origem switch { AssistidaOrigem.Juridico => "Jurídico", AssistidaOrigem.Psicologia => "Psicologia", AssistidaOrigem.ServicoSocial => "Serviço Social", _ => "Recepção" }})";

        r.Ficha = ehProf
            ?
            [
                new RelatorioParDto { Rotulo = "Idade", Valor = Idade(a.DataNascimento, hoje) is { } i ? $"{i} anos" : "Sem registro" },
                new RelatorioParDto { Rotulo = "Contato", Valor = a.SemTelefoneSeguro || a.ContatoSeguro == AssistidaContatoSeguro.NaoLigar ? "Não ligar (falar só na aula)" : contato },
                new RelatorioParDto { Rotulo = "Na turma desde", Valor = mats.Count > 0 ? Fd(inicioRef) : "-" },
                new RelatorioParDto { Rotulo = "Cursos comigo", Valor = mats.Count.ToString(CultureInfo.InvariantCulture) }
            ]
            :
            [
                new RelatorioParDto { Rotulo = "CPF", Valor = AssistidasService.FormatarCpf(a.Cpf) },
                new RelatorioParDto { Rotulo = "Nascimento", Valor = a.DataNascimento is null ? "Sem registro" : $"{Fd(a.DataNascimento)} ({Idade(a.DataNascimento, hoje)} anos)" },
                new RelatorioParDto { Rotulo = "Contato", Valor = contato },
                new RelatorioParDto { Rotulo = "Endereço", Valor = a.EnderecoSigiloso ? "Endereço sigiloso" : EnderecoTexto(a) },
                new RelatorioParDto { Rotulo = "Entrada na Casa", Valor = $"{Fd(entrada)} · há {tempoTxt}" },
                new RelatorioParDto { Rotulo = "Cadastrada por", Valor = cadastradaPor }
            ];

        if (a.AcessoRestrito && (ehAdm || ehJur || ehPsi || ehSoc))
        {
            var motivo = ehAdm ? "motivo visível só para Jurídico, Psicologia e Serviço Social" : a.MotivoRestricao switch
            {
                AssistidaMotivoRestricao.SegredoJustica => "segredo de justiça",
                AssistidaMotivoRestricao.CasaAbrigo => "casa-abrigo",
                AssistidaMotivoRestricao.RiscoGrave => "risco grave",
                AssistidaMotivoRestricao.PedidoAssistida => "pedido da assistida",
                _ => "sem motivo informado"
            };
            r.Avisos.Add(new RelatorioAvisoDto { Tipo = "restrito", Texto = $"Caso com acesso restrito ({motivo}). Esta assistida não aparece para Recepção, Professoras e outros perfis. Não imprima nem compartilhe fora da equipe." });
        }

        if (riscoGrave && (ehPsi || ehJur))
        {
            r.Avisos.Add(new RelatorioAvisoDto
            {
                Tipo = "risco",
                Titulo = $"Risco {RotuloRisco(pront!.RiscoAtual).ToLower(PtBr)} registrado pela Psicologia.",
                Texto = ehJur ? "O Jurídico vê só este alerta, nunca o conteúdo das sessões." : "O alerta sai quando um risco menor for registrado.",
                Itens =
                [
                    "Risco de vida ou crise: SAMU 192 ou acompanhar até o pronto-socorro.",
                    "Sofrimento intenso: CAPS de referência e CVV 188 (24 horas, gratuito).",
                    "Violência em curso: Polícia 190, Delegacia da Mulher e Jurídico da Casa."
                ]
            });
        }

        if (vulneravel && (ehSoc || ehJur || ehPsi))
        {
            r.Avisos.Add(new RelatorioAvisoDto
            {
                Tipo = "risco",
                Titulo = "Aviso de vulnerabilidade social registrado pelo Serviço Social.",
                Texto = ehSoc
                    ? "Indicadores do estudo social vigente: " + string.Join(", ", Separar(prontSoc!.VulnerabilidadesAtuais, RotulosVulnerabilidade)) + "."
                    : "Só o aviso, sem detalhes. Converse com o Serviço Social da Casa se precisar."
            });
        }

        // ---- resumo do período
        var regsAll = d.Registros.Where(x => x.AssistidaId == a.Id && (meusCursos is null || meusCursos.Contains(x.CursoId))).ToList();
        var regsP = regsAll.Where(x => p.Dentro(x.Data)).ToList();
        var fP = Freq(regsP);
        var atsAll = caso?.Atendimentos.OrderBy(x => x.CriadoEm).ToList() ?? [];
        var atsP = atsAll.Where(x => p.Dentro(Local(x.CriadoEm))).ToList();
        var sessAll = pront?.Sessoes.OrderBy(x => x.CriadoEm).ToList() ?? [];
        var sessP = sessAll.Where(x => p.Dentro(Local(x.CriadoEm))).ToList();

        if (ehProf || ehAdm) r.Kpis.Add(Kpi(regsP.Count > 0 ? fP.Pct + "%" : "-", "Frequência nos cursos", regsP.Count > 0 ? $"{fP.Presente} presenças em {fP.Total} aulas" : "Sem aulas no período"));
        if (ehProf)
        {
            r.Kpis.Add(Kpi(N(fP.Faltou), "Faltas sem justificativa", alerta: fP.Faltou >= 3));
            r.Kpis.Add(Kpi(N(fP.Justificada), "Faltas justificadas"));
            r.Kpis.Add(Kpi(N(SequenciaFaltas(regsAll)), "Faltas seguidas agora", "Sem contar presenças depois"));
        }

        if (ehRec || ehAdm)
        {
            r.Kpis.Add(Kpi(N(mats.Count), "Cursos desde a entrada", $"{mats.Count(m => m.Situacao == AssistidaCursoSituacao.Concluiu)} concluídos · {mats.Count(m => m.Situacao == AssistidaCursoSituacao.Interrompido)} interrompido(s)"));
        }

        if (ehJur || ehAdm) r.Kpis.Add(Kpi(N(atsP.Count), "Atendimentos jurídicos no período", $"{atsAll.Count} desde a entrada"));
        if (ehJur)
        {
            var prox = podeVerCaso ? atsAll.LastOrDefault()?.DataRetorno : null;
            r.Kpis.Add(Kpi(prox is null ? "-" : Fdc(prox), "Próximo retorno", prox is null ? (podeVerCaso || caso is null ? "Sem retorno marcado" : "Só para quem vê o caso") : prox < hoje ? "Já passou, sem novo registro" : "Agendado"));
        }

        if (ehPsi)
        {
            r.Kpis.Add(Kpi(N(sessP.Count), "Sessões no período", $"{sessAll.Count} desde a abertura"));
            if (podeVerPront)
            {
                var comp = sessP.Count(s => s.Presenca == "compareceu");
                r.Kpis.Add(Kpi(sessP.Count > 0 ? Pct(comp, sessP.Count) + "%" : "-", "Comparecimento", $"{comp} de {sessP.Count}"));
            }

            r.Kpis.Add(Kpi(RotuloRisco(pront?.RiscoAtual), "Risco atual", alerta: riscoGrave));
        }

        var atsSocAll = prontSoc?.Atendimentos.OrderBy(x => x.RealizadoEm).ToList() ?? [];
        var atsSocP = atsSocAll.Where(x => p.Dentro(Local(x.RealizadoEm))).ToList();
        if (ehSoc)
        {
            r.Kpis.Add(Kpi(N(atsSocP.Count), "Atendimentos no período", $"{atsSocAll.Count} desde a abertura"));
            if (podeVerSoc)
            {
                var plano = PlanoAte(prontSoc!.Metas, p.Fim);
                r.Kpis.Add(Kpi(N(prontSoc.EncaminhamentosRede.Count(e => p.Dentro(e.Data))), "Encaminhamentos à rede no período"));
                r.Kpis.Add(Kpi(plano.Total > 0 ? Pct(plano.Concluidas, plano.Total) + "%" : "-", "Plano concluído", $"{plano.Concluidas} de {plano.Total} metas"));
            }
        }

        var encsVisiveis = new List<(string Area, DateOnly Data)>();
        if (a.EncaminhadaJuridicoEm is not null) encsVisiveis.Add(("Jurídico", Local(a.EncaminhadaJuridicoEm.Value)));
        if (a.EncaminhadaAssistenciaSocialEm is not null) encsVisiveis.Add(("Serviço Social", Local(a.EncaminhadaAssistenciaSocialEm.Value)));
        if (ehRec) r.Kpis.Add(Kpi(N(encsVisiveis.Count), "Encaminhamentos feitos", "Jurídico e Serviço Social"));
        if (ehAdm) r.Kpis.Add(Kpi(N(encsVisiveis.Count), "Encaminhamentos", (encsVisiveis.Count == 0 ? "Nenhum" : string.Join(", ", encsVisiveis.Select(e => e.Area))) + " (Psicologia não aparece por sigilo)"));

        // ---- trajetória
        var marcos = Marcos(a, perfil, d, mats, caso, pront, podeVerCaso, podeVerPront, prontSoc, podeVerSoc).Where(m => p.Dentro(DateOnly.ParseExact(m.Data, "yyyy-MM-dd", CultureInfo.InvariantCulture))).ToList();
        var notaTempo = perfil switch
        {
            PerfisAcesso.Recepcao => "Entrada, direcionamentos, encaminhamentos e cursos. A Recepção não vê nada da Psicologia nem o conteúdo do Jurídico.",
            PerfisAcesso.Juridico => "Inclui o que o Jurídico pode ver das outras áreas: cursos (nome, situação e datas) e, da Psicologia, só o alerta de risco atual.",
            PerfisAcesso.Psicologia => "Da área jurídica aparece só se ela está em acompanhamento, sem conteúdo.",
            PerfisAcesso.Professor => "Só os cursos que você dá. Nada das outras áreas aparece para a professora.",
            PerfisAcesso.AssistenteSocial => "Do Jurídico e da Psicologia aparece só se ela está em acompanhamento, sem conteúdo.",
            _ => "A Coordenação vê metadados (datas, situação, protocolo), nunca relatos nem nada da Psicologia por assistida."
        };
        r.Secoes.Add(new RelatorioSecaoDto { Blocos = [new RelatorioBlocoDto { Tipo = "marcos", Titulo = "Trajetória no período", Nota = notaTempo, Largo = true, Marcos = marcos, Vazio = "Nenhum marco neste período." }] });

        // ---- histórico detalhado da área
        var hist = new RelatorioSecaoDto();
        if (ehRec || ehAdm) hist.Blocos.Add(CursosResumo(mats, p, perfil, d));
        if (ehRec) hist.Blocos.AddRange(await HistoricoRecepcaoAsync(a, p, d, ct));
        if (ehProf) hist.Blocos.AddRange(mats.Select(m => HistoricoCurso(m, p, d)));
        if (ehJur) hist.Blocos.Add(HistoricoJuridico(a, caso, atsP, usuario, podeVerCaso, r, d));
        if (ehAdm)
        {
            hist.Blocos.Add(Tabela("Jurídico (só metadados)", "A Coordenação vê protocolo, data, profissional e situação. O relato nunca aparece aqui.",
                [Col("Protocolo"), Col("Data"), Col("Profissional"), Col("Situação")],
                atsP.Select(x => Linha(C(x.Protocolo), C(Fd(Local(x.CriadoEm))), C(x.ProfissionalNome), C(RotulosSituacaoCaso.GetValueOrDefault(x.Situacao, x.Situacao)))), true,
                atsAll.Count > 0 ? "Nenhum atendimento jurídico no período." : "Sem atendimento jurídico."));
            hist.Blocos.Add(Texto("Psicologia", null, "Pela regra de sigilo da Psicologia, a Coordenação não vê se uma assistida específica é atendida, nem as sessões. Os números da Psicologia aparecem só no Relatório Estatístico/Geral, sem nomes.", "sigilo"));
            hist.Blocos.Add(Texto("Serviço Social", null, (a.EncaminhadaAssistenciaSocialEm is null ? "Sem encaminhamento ao Serviço Social. " : $"Encaminhada ao Serviço Social em {Fd(Local(a.EncaminhadaAssistenciaSocialEm.Value))}. ")
                + "Pela regra de sigilo, a Coordenação não vê o acompanhamento social por assistida (atendimentos, estudo social, rede e documentos). Os números do Serviço Social aparecem só no Relatório Estatístico/Geral, sem nomes.", "sigilo"));
        }

        if (ehPsi) hist.Blocos.AddRange(await HistoricoPsicologiaAsync(pront, sessP, usuario, podeVerPront, r, p, ct));
        if (ehSoc) hist.Blocos.AddRange(HistoricoServicoSocial(prontSoc, atsSocP, usuario, podeVerSoc, p, hoje));
        if (ehJur || ehPsi || ehSoc) hist.Blocos.Add(CursosResumo(mats, p, perfil, d));
        r.Secoes.Add(hist);

        // ---- conteúdo sigiloso e documento externo
        if (ehJur)
        {
            r.TipoSigiloso = "relatos";
            var resp = ResponsavelCaso(caso, usuario);
            r.PodeIncluirSigiloso = resp && atsP.Count > 0;
            r.RegistrosSigilosos = resp ? atsP.Select(x => new RelatorioRegistroSigilosoDto { Chave = PrefixoJuridico + x.Id, Rotulo = x.Protocolo }).ToList() : [];
            r.AvisoSigiloso = caso is null ? "Ainda não há atendimentos jurídicos para incluir."
                : !podeVerCaso ? "Marcar “Incluir relatos” não libera acesso: este PDF sai sem nenhum relato."
                : !resp ? $"Você pode ler este caso, mas só as responsáveis ({NomesResponsaveis(caso.Responsaveis.Select(x => x.Nome))}) podem incluir relatos num PDF. Marcar a opção não dá permissão."
                : atsP.Count == 0 ? "Nenhum atendimento no período escolhido." : null;
            r.PodeDocumentoExterno = podeVerCaso && atsAll.Count > 0;
        }

        if (ehPsi)
        {
            r.TipoSigiloso = "evolucoes";
            var resp = ResponsavelProntuario(pront, usuario);
            r.PodeIncluirSigiloso = resp && sessP.Count > 0;
            r.RegistrosSigilosos = resp ? sessP.Select(x => new RelatorioRegistroSigilosoDto { Chave = PrefixoPsicologia + x.Id, Rotulo = $"sessão nº {x.Numero}" }).ToList() : [];
            r.AvisoSigiloso = pront is null ? "Ainda não há sessões para incluir."
                : !podeVerPront ? "Marcar “Incluir evoluções” não libera acesso: este PDF sai sem nenhuma evolução."
                : !resp ? $"Só a psicóloga responsável ({NomesResponsaveis(pront.Responsaveis.Select(x => x.Nome))}) pode incluir evoluções num PDF. Marcar a opção não dá permissão."
                : sessP.Count == 0 ? "Nenhuma sessão no período escolhido." : null;
            r.PodeDocumentoExterno = resp && sessAll.Count > 0;
        }

        if (ehSoc)
        {
            r.TipoSigiloso = "relatos";
            var resp = ResponsavelProntuarioSocial(prontSoc, usuario);
            r.PodeIncluirSigiloso = resp && atsSocP.Count > 0;
            r.RegistrosSigilosos = resp ? atsSocP.Select(x => new RelatorioRegistroSigilosoDto { Chave = PrefixoServicoSocial + x.Id, Rotulo = $"atendimento nº {x.Numero}" }).ToList() : [];
            r.AvisoSigiloso = prontSoc is null || atsSocAll.Count == 0 ? "Ainda não há atendimentos para incluir."
                : !podeVerSoc ? "Marcar “Incluir relatos” não libera acesso: este PDF sai sem nenhum relato."
                : !resp ? $"Só a assistente social responsável ({NomesResponsaveis(prontSoc.Responsaveis.Select(x => x.Nome))}) pode incluir relatos num PDF. Marcar a opção não dá permissão."
                : atsSocP.Count == 0 ? "Nenhum atendimento no período escolhido." : null;
            r.PodeDocumentoExterno = resp && atsSocAll.Count > 0;
        }

        return r;
    }

    private static string NomesResponsaveis(IEnumerable<string> nomes)
    {
        var lista = nomes.ToList();
        return lista.Count == 0 ? "sem responsável definida" : string.Join(", ", lista);
    }

    private static string ContatoTexto(Assistida a)
    {
        if (a.SemTelefoneSeguro) return "Sem telefone seguro";
        var digitos = AssistidasService.SomenteDigitos(a.Telefone);
        var tel = digitos.Length >= 10 ? $"({digitos[..2]}) {new string('*', digitos.Length - 6)}-{digitos[^4..]}" : "Sem telefone";
        var regra = RotuloContato(a.ContatoSeguro);
        return $"{tel} · {regra}" + (string.IsNullOrWhiteSpace(a.ContatoSeguroObservacao) ? "" : $" ({a.ContatoSeguroObservacao})");
    }

    private static string EnderecoTexto(Assistida a)
    {
        var rua = string.Join(", ", new[] { a.Endereco, a.EnderecoNumero }.Where(x => !string.IsNullOrWhiteSpace(x)));
        var local = string.Join(" - ", new[] { a.Bairro, string.Join("/", new[] { a.Cidade, a.Uf }.Where(x => !string.IsNullOrWhiteSpace(x))) }.Where(x => !string.IsNullOrWhiteSpace(x)));
        var texto = string.Join(" - ", new[] { rua, local }.Where(x => !string.IsNullOrWhiteSpace(x)));
        return texto.Length == 0 ? "Sem registro" : texto;
    }

    private static List<RelatorioMarcoDto> Marcos(Assistida a, string perfil, Dados d, List<Matricula> mats, CasoJuridico? caso, ProntuarioPsicologico? pront, bool podeVerCaso, bool podeVerPront, ProntuarioSocial? prontSoc, bool podeVerSoc)
    {
        var ev = new List<RelatorioMarcoDto>();
        void Add(DateOnly data, string area, string texto, string? quem = null) => ev.Add(new RelatorioMarcoDto { Data = Iso(data), Area = area, Texto = texto, Quem = quem });
        var ehProf = perfil == PerfisAcesso.Professor;

        if (!ehProf)
        {
            var quem = (a.Origem == AssistidaOrigem.Psicologia && perfil != PerfisAcesso.Psicologia) || (a.Origem == AssistidaOrigem.ServicoSocial && perfil != PerfisAcesso.AssistenteSocial)
                ? "Equipe técnica da Casa"
                : a.CadastradaPorNome;
            Add(Local(a.CriadoEm), "", "Cadastro na Casa", quem);
            if (a.EncaminhadaJuridicoEm is not null) Add(Local(a.EncaminhadaJuridicoEm.Value), "juridico", "Encaminhada ao Jurídico");
            if (a.EncaminhadaAssistenciaSocialEm is not null) Add(Local(a.EncaminhadaAssistenciaSocialEm.Value), "social", "Encaminhada ao Serviço Social");
            if (a.EncaminhadaPsicologiaEm is not null && perfil is PerfisAcesso.Psicologia or PerfisAcesso.Juridico or PerfisAcesso.AssistenteSocial)
                Add(Local(a.EncaminhadaPsicologiaEm.Value), "psicologia", "Encaminhada à Psicologia");
        }

        foreach (var m in mats)
        {
            Add(m.Inicio, "curso", (m.Situacao == AssistidaCursoSituacao.Aguardando ? "Aguardando vaga no curso " : "Começou o curso ") + m.Curso.Nome);
            if (m.Fim is not null)
            {
                var motivo = ehProf && !string.IsNullOrWhiteSpace(m.Vinculo.MotivoSaida) ? $" (motivo: {m.Vinculo.MotivoSaida.ToLower(PtBr)})" : "";
                Add(m.Fim.Value, "curso", (m.Situacao == AssistidaCursoSituacao.Concluiu ? "Concluiu " : "Participação interrompida no curso ") + m.Curso.Nome + motivo);
            }
        }

        if (ehProf) return ev.OrderBy(e => e.Data).ToList();

        var primeiro = d.PrimeiroAtendimentoJuridico(a.Id);
        if (perfil is PerfisAcesso.Recepcao or PerfisAcesso.Juridico or PerfisAcesso.Adm)
        {
            if (a.JuridicoAgendadoPara is not null && a.JuridicoAgendadoEm is not null)
                Add(Local(a.JuridicoAgendadoEm.Value), "juridico", $"Jurídico agendou o primeiro atendimento para {HoraLocal(a.JuridicoAgendadoPara.Value):dd/MM/yyyy HH:mm}");
            if (a.JuridicoEncaminhamentoCanceladoEm is not null)
                Add(Local(a.JuridicoEncaminhamentoCanceladoEm.Value), "juridico", "Encaminhamento ao Jurídico cancelado"
                    + (perfil == PerfisAcesso.Juridico && !string.IsNullOrWhiteSpace(a.JuridicoMotivoCancelamento) ? $": {a.JuridicoMotivoCancelamento}" : ""));
        }

        if (perfil == PerfisAcesso.Recepcao && primeiro is not null)
        {
            Add(primeiro.Value, "juridico", "Primeiro atendimento realizado no Jurídico");
        }

        if (caso is not null && perfil is PerfisAcesso.Juridico or PerfisAcesso.Adm)
        {
            var ats = caso.Atendimentos.OrderBy(x => x.CriadoEm).ToList();
            for (var i = 0; i < ats.Count; i++)
            {
                if (i > 0 && ats[i].Situacao == ats[i - 1].Situacao) continue;
                var sit = RotulosSituacaoCaso.GetValueOrDefault(ats[i].Situacao, ats[i].Situacao);
                Add(Local(ats[i].CriadoEm), "juridico", (i == 0 ? "Primeiro atendimento no Jurídico" : "Situação do caso mudou para") + $" - {sit} ({ats[i].Protocolo})", ats[i].ProfissionalNome);
            }
        }

        if (prontSoc is not null && prontSoc.Atendimentos.Count > 0 && perfil is PerfisAcesso.Juridico or PerfisAcesso.Psicologia)
        {
            Add(Local(prontSoc.Atendimentos.Min(x => x.RealizadoEm)), "social", "Iniciou acompanhamento no Serviço Social");
        }

        if (prontSoc is not null && perfil == PerfisAcesso.AssistenteSocial)
        {
            var ats = prontSoc.Atendimentos.OrderBy(x => x.RealizadoEm).ToList();
            var resp = prontSoc.Responsaveis.OrderBy(x => x.AtribuidoEm).FirstOrDefault();
            if (ats.Count > 0) Add(Local(ats[0].RealizadoEm), "social", $"Primeiro atendimento no Serviço Social ({prontSoc.Numero})", resp?.Nome);
            if (podeVerSoc)
            {
                foreach (var e in prontSoc.Estudos.OrderBy(x => x.Versao))
                    Add(Local(e.CriadoEm), "social", e.Versao == 1 ? "Estudo social preenchido" : $"Estudo social atualizado (versão {e.Versao})", e.ProfissionalNome);
                foreach (var e in prontSoc.EncaminhamentosRede.OrderBy(x => x.Data))
                    Add(e.Data, "social", $"Encaminhada à rede: {RotuloDestino(e)} ({RotulosStatusRede.GetValueOrDefault(e.Status, e.Status).ToLower(PtBr)})", e.ProfissionalNome);
                foreach (var m in prontSoc.Metas.Where(x => x.ConcluidaEm is not null))
                    Add(Local(m.ConcluidaEm!.Value), "social", "Meta concluída: " + m.Descricao);
            }

            if (prontSoc.EncerradoEm is not null)
                Add(Local(prontSoc.EncerradoEm.Value), "social", podeVerSoc ? "Acompanhamento encerrado: " + RotulosEncerramentoSocial.GetValueOrDefault(prontSoc.MotivoEncerramento ?? "", "Outro") : "Acompanhamento encerrado");
        }

        if (caso is not null && caso.Atendimentos.Count > 0 && perfil is PerfisAcesso.Psicologia or PerfisAcesso.AssistenteSocial)
        {
            Add(Local(caso.Atendimentos.Min(x => x.CriadoEm)), "juridico", "Iniciou acompanhamento no Jurídico");
        }

        if (pront is not null && pront.Sessoes.Count > 0 && perfil == PerfisAcesso.Psicologia)
        {
            var ss = pront.Sessoes.OrderBy(s => s.CriadoEm).ToList();
            var resp = pront.Responsaveis.FirstOrDefault();
            var comRisco = ss.Where(s => !string.IsNullOrWhiteSpace(s.Risco)).ToList();
            Add(Local(ss[0].CriadoEm), "psicologia", $"Prontuário aberto {pront.Numero}" + (podeVerPront && comRisco.Count > 0 ? $" (risco inicial: {RotuloRisco(comRisco[0].Risco).ToLower(PtBr)})" : ""), resp?.Nome);
            if (podeVerPront)
            {
                for (var i = 1; i < comRisco.Count; i++)
                {
                    if (comRisco[i].Risco != comRisco[i - 1].Risco)
                        Add(Local(comRisco[i].CriadoEm), "psicologia", $"Risco reavaliado: {RotuloRisco(comRisco[i - 1].Risco)} → {RotuloRisco(comRisco[i].Risco)}", comRisco[i].ProfissionalNome);
                }
            }

            if (pront.EncerradoEm is not null)
                Add(Local(pront.EncerradoEm.Value), "psicologia", podeVerPront ? "Acompanhamento encerrado: " + RotulosEncerramento.GetValueOrDefault(pront.MotivoEncerramento ?? "", "Outro") : "Acompanhamento encerrado");
        }

        return ev.OrderBy(e => e.Data).ToList();
    }

    private static RelatorioBlocoDto CursosResumo(List<Matricula> mats, PeriodoRelatorio p, string perfil, Dados d)
    {
        var comFreq = perfil == PerfisAcesso.Adm;
        var cols = new List<RelatorioColunaDto> { Col("Curso"), Col("Início"), Col("Término / saída", novo: true), Col("Situação") };
        if (comFreq) cols.AddRange([Col("Frequência no período", true), Col("Faltas", true), Col("Justificadas", true)]);
        var linhas = mats.Select(m =>
        {
            var linha = Linha(C(m.Curso.Nome), C(Fd(m.Inicio)), C(m.Fim is null ? (m.Situacao == AssistidaCursoSituacao.Cursando ? "-" : "sem registro") : Fd(m.Fim)), CelulaSituacaoCurso(m.Situacao));
            if (comFreq)
            {
                var f = Freq(d.Registros.Where(x => x.AssistidaId == m.Assistida.Id && x.CursoId == m.Curso.Id && p.Dentro(x.Data)));
                linha.AddRange([C(f.Total > 0 ? f.Pct + "%" : "-"), C(f.Faltou), C(f.Justificada)]);
            }

            return linha;
        });
        var nota = perfil switch
        {
            PerfisAcesso.Adm => "Frequência calculada pela chamada da professora no período escolhido. O motivo de uma interrupção não aparece aqui.",
            PerfisAcesso.Recepcao => "A frequência detalhada fica com a professora e a Coordenação.",
            _ => "Regra inicial: só curso, situação e datas. Frequência, faltas, justificativas, observações e motivo da saída ficam com a professora; se for relevante, a equipe compartilha de forma específica."
        };
        return Tabela("Cursos", nota, cols, linhas, true, "Não está em nenhum curso.");
    }

    private async Task<List<RelatorioBlocoDto>> HistoricoRecepcaoAsync(Assistida a, PeriodoRelatorio p, Dados d, CancellationToken ct)
    {
        var (ini, fim) = FaixaUtc(p);
        var id = a.Id.ToString(CultureInfo.InvariantCulture);
        var eventos = await _db.AuditoriaEventos.AsNoTracking()
            .Where(e => e.Entidade == "Assistida" && e.EntidadeId == id && e.PerfilFuncionario == PerfisAcesso.Recepcao && e.CriadoEm >= ini && e.CriadoEm < fim)
            .OrderBy(e => e.CriadoEm)
            .Select(e => new { e.Acao, e.Descricao, e.NomeFuncionario, e.IdentificadorFuncionario, e.CriadoEm })
            .ToListAsync(ct);

        // Só o tipo da ação: a descrição não é mostrada (pode citar áreas que a Recepção não vê).
        var linhas = eventos
            .Where(e => !(e.Acao == "ASSISTIDA_ENCAMINHADA" && e.Descricao.Contains("Psicologia", StringComparison.OrdinalIgnoreCase)))
            .Select(e => Linha(
                C(Fd(Local(e.CriadoEm))),
                C(e.Acao switch
                {
                    "ASSISTIDA_CADASTRADA" => "Cadastro",
                    "ASSISTIDA_ATUALIZADA" => "Ficha atualizada",
                    "ASSISTIDA_ENCAMINHADA" => "Encaminhamento ao Jurídico",
                    "ASSISTIDA_REMOVIDA" => "Cadastro removido",
                    "ASSISTIDA_REATIVADA" => "Cadastro reativado",
                    _ => "Outra ação na ficha"
                }),
                C(string.IsNullOrWhiteSpace(e.NomeFuncionario) ? e.IdentificadorFuncionario : e.NomeFuncionario)))
            .ToList();

        var encs = new List<List<RelatorioCelulaDto>>();
        if (a.EncaminhadaJuridicoEm is not null)
        {
            var sit = SituacaoOperacionalJuridico(a, d, Hoje);
            encs.Add(Linha(C("Jurídico"), C(Fd(Local(a.EncaminhadaJuridicoEm.Value))), C(sit.Rotulo, sit.Badge ?? "neutro")));
        }

        if (a.EncaminhadaAssistenciaSocialEm is not null)
        {
            encs.Add(Linha(C("Assistência social"), C(Fd(Local(a.EncaminhadaAssistenciaSocialEm.Value))), C("Sem confirmação de atendimento no sistema", "neutro")));
        }

        return
        [
            Tabela("Ações da Recepção no período", "O que foi feito na ficha pela Recepção (vem do histórico de ações).", [Col("Data"), Col("Ação"), Col("Por")], linhas, vazio: "Nenhuma ação da Recepção no período."),
            Tabela("Encaminhamentos (situação hoje)", "Situação operacional, vinda do registro do próprio Jurídico. Demanda, relato, processo, orientação e motivo de cancelamento não aparecem.", [Col("Para"), Col("Encaminhada em"), Col("Situação")], encs, vazio: "Sem encaminhamentos.")
        ];
    }

    private RelatorioBlocoDto HistoricoCurso(Matricula m, PeriodoRelatorio p, Dados d)
    {
        var regs = d.Registros.Where(x => x.AssistidaId == m.Assistida.Id && x.CursoId == m.Curso.Id).OrderBy(x => x.Data).ToList();
        var rP = regs.Where(x => p.Dentro(x.Data)).ToList();
        var f = Freq(rP);
        var seq = SequenciaFaltas(regs);
        var faltas = rP.Where(x => x.Status != "presente").ToList();
        var obs = rP.Where(x => !string.IsNullOrWhiteSpace(x.Observacao) && x.Status != "justificada").ToList();
        var profs = d.Professoras.Where(x => x.CursoId == m.Curso.Id).Select(x => d.NomeUsuario(x.UserId)).ToList();
        var rotulosStatus = new Dictionary<string, string> { ["presente"] = "Presente", ["faltou"] = "Faltou", ["justificada"] = "Justificada" };

        var bloco = new RelatorioBlocoDto
        {
            Tipo = "pares",
            Titulo = m.Curso.Nome,
            Nota = "Professora: " + (profs.Count == 0 ? "-" : string.Join(", ", profs)),
            Largo = true,
            Texto = seq >= 3 && m.Situacao == AssistidaCursoSituacao.Cursando ? $"{seq} faltas seguidas" : null,
            Itens = [Item(RotulosSituacaoCurso.GetValueOrDefault(m.Situacao, m.Situacao), 0, m.Situacao)],
            Pares =
            [
                new RelatorioParDto { Rotulo = "Entrou no curso", Valor = Fd(m.Inicio) },
                new RelatorioParDto { Rotulo = m.Situacao == AssistidaCursoSituacao.Interrompido ? "Saiu em" : "Terminou em", Valor = m.Fim is null ? (m.Situacao == AssistidaCursoSituacao.Aguardando ? "Aguardando vaga" : "Ainda cursando") : Fd(m.Fim), DadoNovo = true },
                new RelatorioParDto { Rotulo = "Motivo da saída", Valor = m.Vinculo.MotivoSaida ?? (m.Situacao == AssistidaCursoSituacao.Interrompido ? "Sem registro" : "-"), DadoNovo = true },
                new RelatorioParDto { Rotulo = "Aulas no período", Valor = N(f.Total) }
            ],
            Extras =
            [
                Pilha("", null, PilhaFrequencia(f)),
                new RelatorioBlocoDto
                {
                    Tipo = "chamada", Titulo = "Chamada aula a aula",
                    Itens = rP.Select(x => new RelatorioItemDto { Rotulo = Fdc(x.Data), Tipo = x.Status, Extra = $"{Fd(x.Data)} - {rotulosStatus[x.Status]}{(string.IsNullOrWhiteSpace(x.Observacao) ? "" : ": " + x.Observacao)} · Aula: {x.Tema ?? "-"}" }).ToList(),
                    Vazio = "Sem aulas no período."
                },
                Tabela("Faltas e justificativas", null, [Col("Data"), Col("Aula"), Col("Registro"), Col("Justificativa / observação")],
                    faltas.Select(x => Linha(C(Fd(x.Data)), C(x.Tema ?? "-"), x.Status == "justificada" ? C("Justificada", "ok") : C("Faltou", "restrito"), C(x.Observacao ?? "-"))), vazio: "Nenhuma falta no período."),
                new RelatorioBlocoDto
                {
                    Tipo = "marcos", Titulo = "Observações da professora sobre ela",
                    Marcos = obs.Select(x => new RelatorioMarcoDto { Data = Iso(x.Data), Area = "curso", Texto = x.Observacao!, Quem = $"aula “{x.Tema ?? "-"}”" }).ToList(),
                    Vazio = "Sem observações no período."
                }
            ]
        };
        return bloco;
    }

    private static RelatorioBlocoDto HistoricoJuridico(Assistida a, CasoJuridico? caso, List<AtendimentoJuridico> atsP, UsuarioAtuante u, bool podeVer, RelatorioIndividualResponse r, Dados d)
    {
        if (caso is null || caso.Atendimentos.Count == 0)
        {
            var sit = SituacaoOperacionalJuridico(a, d, Local(DateTime.UtcNow));
            var texto = sit.Chave == "Cancelado"
                ? "O encaminhamento ao Jurídico foi cancelado" + (string.IsNullOrWhiteSpace(a.JuridicoMotivoCancelamento) ? "." : $": {a.JuridicoMotivoCancelamento}")
                : $"Encaminhada e ainda na fila do Jurídico ({sit.Rotulo.ToLower(PtBr)}).";
            return Texto("Jurídico", null, texto, largo: true);
        }

        var resp = ResponsavelCaso(caso, u);
        var ultimo = caso.Atendimentos.OrderBy(x => x.CriadoEm).Last();
        var pares = new List<RelatorioParDto>
        {
            new() { Rotulo = "Responsáveis", Valor = NomesResponsaveis(caso.Responsaveis.Select(x => x.Nome)) },
            new() { Rotulo = "Quem vê o conteúdo", Valor = caso.Visibilidade == CasoJuridicoVisibilidade.Equipe ? "Toda a equipe do Jurídico" : "Somente as responsáveis" }
        };

        if (!podeVer)
        {
            return new RelatorioBlocoDto
            {
                Tipo = "pares", Titulo = "Atendimentos jurídicos", Largo = true, Pares = pares,
                Extras =
                [
                    Texto("", null, "Este caso está como somente as responsáveis. Você vê só protocolo, data, profissional e situação. Para ver o conteúdo, peça acesso de emergência na ficha do Jurídico (vale 12 horas, fica registrado e as responsáveis são avisadas).", "sigilo"),
                    Tabela("", null, [Col("Protocolo"), Col("Data"), Col("Profissional"), Col("Situação")],
                        atsP.Select(x => Linha(C(x.Protocolo), C(Fd(Local(x.CriadoEm))), C(x.ProfissionalNome), C(RotulosSituacaoCaso.GetValueOrDefault(x.Situacao, x.Situacao)))), vazio: "Nenhum atendimento no período.")
                ]
            };
        }

        var processo = caso.Atendimentos.Where(x => !string.IsNullOrWhiteSpace(x.NumeroProcesso)).OrderBy(x => x.CriadoEm).LastOrDefault();
        pares.Add(new RelatorioParDto { Rotulo = "Tipo de demanda", Valor = RotulosDemanda.GetValueOrDefault(ultimo.TipoDemanda, "Outro") + (ultimo.TipoDemanda == "outro" && !string.IsNullOrWhiteSpace(ultimo.TipoDemandaOutro) ? $" ({ultimo.TipoDemandaOutro})" : "") });
        pares.Add(new RelatorioParDto { Rotulo = "Processo / vara", Valor = processo is null ? "Sem processo" : processo.NumeroProcesso!, Sub = processo?.Vara });

        var encs = atsP.SelectMany(x => Separar(x.Encaminhamentos, RotulosEncJuridico)).Count();
        return new RelatorioBlocoDto
        {
            Tipo = "pares",
            Titulo = "Atendimentos jurídicos",
            Nota = "Relatos ficam ocultos: marque “Incluir relatos” no topo para escolher quais entram (só as responsáveis). Atendimentos não são editados; correções aparecem como adendo.",
            Largo = true,
            Pares = pares,
            Extras =
            [
                Kpis("", null, Kpi(N(atsP.Count), "Atendimentos"), Kpi(N(atsP.Count(x => x.TemMedidaProtetiva == true)), "Com medida protetiva"), Kpi(N(encs), "Encaminhamentos externos"), Kpi(N(atsP.Sum(x => x.Adendos.Count)), "Adendos")),
                new RelatorioBlocoDto
                {
                    Tipo = "registros", Titulo = "",
                    Vazio = "Nenhum atendimento jurídico no período.",
                    Registros = atsP.OrderBy(x => x.CriadoEm).Select(x => new RelatorioRegistroDto
                    {
                        Chave = resp ? PrefixoJuridico + x.Id : null,
                        Titulo = x.Protocolo,
                        Subtitulo = $"{HoraLocal(x.CriadoEm):dd/MM/yyyy} às {HoraLocal(x.CriadoEm):HH:mm} · {x.ProfissionalNome}",
                        Badges = new[] { Badge(RotulosSituacaoCaso.GetValueOrDefault(x.Situacao, x.Situacao), x.Situacao == CasoJuridicoSituacao.Encerrado ? "ok" : "juridico") }
                            .Concat(x.TemMedidaProtetiva == true ? [Badge("Medida protetiva", "restrito")] : Array.Empty<RelatorioBadgeDto>()).ToList(),
                        Campos =
                        [
                            new() { Rotulo = "Tipo de demanda", Valor = RotulosDemanda.GetValueOrDefault(x.TipoDemanda, "Outro") },
                            new() { Rotulo = "Medida protetiva", Valor = x.TemMedidaProtetiva switch { true => "Sim", false => "Não", _ => "Não informado" } },
                            new() { Rotulo = "Encaminhamentos", Valor = string.Join(", ", Separar(x.Encaminhamentos, RotulosEncJuridico)) is { Length: > 0 } e ? e : "-" },
                            new() { Rotulo = "Próximo passo", Valor = x.ProximoPasso ?? "-" },
                            new() { Rotulo = "Retorno", Valor = x.DataRetorno is null ? "-" : Fd(x.DataRetorno) },
                            new() { Rotulo = "Adendos", Valor = x.Adendos.Count == 0 ? "Nenhum" : $"{x.Adendos.Count} (entram junto com o relato)" }
                        ]
                    }).ToList()
                }
            ]
        };
    }

    private async Task<List<RelatorioBlocoDto>> HistoricoPsicologiaAsync(ProntuarioPsicologico? pront, List<SessaoPsicologica> sP, UsuarioAtuante u, bool podeVer, RelatorioIndividualResponse r, PeriodoRelatorio p, CancellationToken ct)
    {
        if (pront is null || pront.Sessoes.Count == 0)
        {
            return [Texto("Prontuário", null, "Encaminhada e ainda na fila da Psicologia.", largo: true)];
        }

        var resp = ResponsavelProntuario(pront, u);
        var responsavel = pront.Responsaveis.OrderBy(x => x.AtribuidoEm).FirstOrDefault();
        var pares = new List<RelatorioParDto>
        {
            new() { Rotulo = "Prontuário", Valor = pront.Numero },
            new() { Rotulo = "Psicóloga responsável", Valor = responsavel is null ? "Sem responsável" : responsavel.Nome + (string.IsNullOrWhiteSpace(responsavel.Crp) ? "" : $" · CRP {responsavel.Crp}") },
            new() { Rotulo = "Aberto em", Valor = Fd(Local(pront.Sessoes.Min(s => s.CriadoEm))) },
            new() { Rotulo = "Sigilo informado e aceito", Valor = pront.ConsentimentoSigiloEm is null ? "Sem registro" : Fd(Local(pront.ConsentimentoSigiloEm.Value)) },
            new() { Rotulo = "Situação", Valor = pront.EncerradoEm is null ? "Em acompanhamento" : $"Encerrado em {Fd(Local(pront.EncerradoEm.Value))}" + (podeVer ? " · " + RotulosEncerramento.GetValueOrDefault(pront.MotivoEncerramento ?? "", "Outro") : "") },
            new() { Rotulo = "Quem vê as anotações", Valor = pront.Visibilidade == ProntuarioVisibilidade.Equipe ? "Toda a equipe de Psicologia" : "Somente a responsável" }
        };

        if (!podeVer)
        {
            return
            [
                new RelatorioBlocoDto
                {
                    Tipo = "pares", Titulo = "Prontuário psicológico", Largo = true, Pares = pares,
                    Extras =
                    [
                        Texto("", null, "As anotações deste prontuário são só da psicóloga responsável. Você vê que as sessões existiram. Em emergência, peça acesso na ficha da Psicologia (vale 12 horas, fica registrado e a responsável é avisada).", "sigilo"),
                        Tabela("", null, [Col("Sessão"), Col("Data"), Col("Profissional")], sP.Select(s => Linha(C("nº " + s.Numero), C(Fd(Local(s.CriadoEm))), C(s.ProfissionalNome))), vazio: "Nenhuma sessão no período.")
                    ]
                }
            ];
        }

        var (ini, fim) = FaixaUtc(p);
        var idPront = pront.Id.ToString(CultureInfo.InvariantCulture);
        var declaracoes = await _db.AuditoriaEventos.AsNoTracking()
            .CountAsync(e => e.Acao == "PSICOLOGIA_DECLARACAO_EMITIDA" && e.EntidadeId == idPront && e.CriadoEm >= ini && e.CriadoEm < fim, ct);

        return
        [
            new RelatorioBlocoDto
            {
                Tipo = "pares",
                Titulo = "Prontuário psicológico",
                Nota = "Evoluções ficam ocultas: marque “Incluir evoluções” no topo para escolher quais entram (só a psicóloga responsável).",
                Largo = true,
                Pares = pares,
                Extras =
                [
                    Pilha("Presença nas sessões", null, RotulosPresenca.Select(k => Item(k.Value, sP.Count(s => s.Presenca == k.Key), CorPresenca(k.Key)))),
                    new RelatorioBlocoDto
                    {
                        Tipo = "risco", Titulo = "Evolução do risco", Nota = "Uma barra por sessão, da mais antiga para a mais recente. Cinza: não houve avaliação.",
                        Itens = sP.Select(s => new RelatorioItemDto { Rotulo = $"Sessão {s.Numero} - {Fd(Local(s.CriadoEm))}: {(string.IsNullOrWhiteSpace(s.Risco) ? RotulosPresenca.GetValueOrDefault(s.Presenca, s.Presenca) : RotuloRisco(s.Risco))}", Cor = string.IsNullOrWhiteSpace(s.Risco) ? "neutro" : "risco_" + s.Risco }).ToList(),
                        Legenda = OrdemRisco.Select(k => Item(RotuloRisco(k), 0, "risco_" + k)).ToList()
                    },
                    new RelatorioBlocoDto
                    {
                        Tipo = "barras", Titulo = "Modalidade e encaminhamentos externos", Itens = Contar(sP, s => RotulosModalidade.GetValueOrDefault(s.Modalidade ?? "", null!)),
                        Extras = [Barras("", null, Contar(sP, s => Separar(s.EncaminhamentosExternos, RotulosEncPsicologia)))]
                    },
                    Texto("Declarações de comparecimento", "Emitidas sem nada do que foi dito.", $"{declaracoes} no período."),
                    new RelatorioBlocoDto
                    {
                        Tipo = "registros", Titulo = "Sessões no período", Vazio = "Nenhuma sessão no período.",
                        Registros = sP.OrderBy(s => s.CriadoEm).Select(s => new RelatorioRegistroDto
                        {
                            Chave = resp ? PrefixoPsicologia + s.Id : null,
                            Titulo = $"Sessão nº {s.Numero}",
                            Subtitulo = $"{HoraLocal(s.CriadoEm):dd/MM/yyyy} · {s.ProfissionalNome}",
                            Badges = new[] { Badge(RotulosPresenca.GetValueOrDefault(s.Presenca, s.Presenca), s.Presenca == "compareceu" ? "ok" : "neutro") }
                                .Concat(string.IsNullOrWhiteSpace(s.Risco) ? Array.Empty<RelatorioBadgeDto>() : [Badge("Risco " + RotuloRisco(s.Risco).ToLower(PtBr), PsicologiaCatalogo.RiscoElevado(s.Risco) ? "risco" : "neutro")]).ToList(),
                            Campos =
                            [
                                new() { Rotulo = "Tipo", Valor = RotulosTipoSessao.GetValueOrDefault(s.TipoAtendimento, s.TipoAtendimento) },
                                new() { Rotulo = "Modalidade", Valor = RotulosModalidade.GetValueOrDefault(s.Modalidade ?? "", "-") },
                                new() { Rotulo = "Próxima sessão", Valor = s.ProximaSessao is null ? "-" : Fd(Local(s.ProximaSessao.Value)) },
                                new() { Rotulo = "Adendos", Valor = s.Adendos.Count == 0 ? "Nenhum" : $"{s.Adendos.Count} (entram junto com a evolução)" }
                            ]
                        }).ToList()
                    }
                ]
            }
        ];
    }

    // ------------------------------------------------------------------ conteúdo sigiloso

    /// <summary>
    /// Textos dos relatos/evoluções escolhidos para o PDF. Só a responsável recebe; para qualquer outra
    /// pessoa (inclusive quem pode ler o caso) a resposta é nula e nada é enviado.
    /// </summary>
    public async Task<List<RelatorioConteudoSigilosoDto>?> ConteudoSigilosoAsync(UsuarioAtuante u, string perfil, int assistidaId, IEnumerable<string> chaves, CancellationToken ct)
    {
        var lista = chaves.Distinct().Take(200).ToList();
        if (lista.Count == 0) return [];

        if (perfil == PerfisAcesso.Juridico)
        {
            var caso = await _db.CasosJuridicos.AsNoTracking()
                .Include(c => c.Responsaveis)
                .Include(c => c.Atendimentos).ThenInclude(a => a.Adendos)
                .SingleOrDefaultAsync(c => c.AssistidaId == assistidaId && c.Assistida!.Ativa, ct);
            if (!ResponsavelCaso(caso, u)) return null;

            var resultado = new List<RelatorioConteudoSigilosoDto>();
            foreach (var chave in lista)
            {
                if (!chave.StartsWith(PrefixoJuridico, StringComparison.Ordinal) || !int.TryParse(chave[PrefixoJuridico.Length..], out var id)) return null;
                var at = caso!.Atendimentos.SingleOrDefault(x => x.Id == id);
                if (at is null) return null;
                var textos = new List<RelatorioParDto> { new() { Rotulo = "Relato e orientações", Valor = at.Relato } };
                textos.AddRange(at.Adendos.OrderBy(x => x.CriadoEm).Select(x => new RelatorioParDto { Rotulo = $"Adendo {Fd(Local(x.CriadoEm))} · {x.AutorNome}", Valor = x.Texto }));
                resultado.Add(new RelatorioConteudoSigilosoDto { Chave = chave, Rotulo = at.Protocolo, Textos = textos });
            }

            return resultado;
        }

        if (perfil == PerfisAcesso.AssistenteSocial)
        {
            return await ConteudoSigilosoSocialAsync(u, assistidaId, lista, ct);
        }

        if (perfil == PerfisAcesso.Psicologia)
        {
            var pront = await _db.ProntuariosPsicologicos.AsNoTracking()
                .Include(p => p.Responsaveis)
                .Include(p => p.Sessoes).ThenInclude(s => s.Adendos)
                .SingleOrDefaultAsync(p => p.AssistidaId == assistidaId && p.Assistida!.Ativa, ct);
            if (!ResponsavelProntuario(pront, u)) return null;

            var resultado = new List<RelatorioConteudoSigilosoDto>();
            foreach (var chave in lista)
            {
                if (!chave.StartsWith(PrefixoPsicologia, StringComparison.Ordinal) || !int.TryParse(chave[PrefixoPsicologia.Length..], out var id)) return null;
                var s = pront!.Sessoes.SingleOrDefault(x => x.Id == id);
                if (s is null) return null;
                var textos = new List<RelatorioParDto>();
                if (!string.IsNullOrWhiteSpace(s.Demanda)) textos.Add(new RelatorioParDto { Rotulo = "Demanda", Valor = s.Demanda });
                textos.Add(new RelatorioParDto { Rotulo = "Evolução", Valor = string.IsNullOrWhiteSpace(s.Evolucao) ? "-" : s.Evolucao });
                if (!string.IsNullOrWhiteSpace(s.Plano)) textos.Add(new RelatorioParDto { Rotulo = "Plano", Valor = s.Plano });
                textos.AddRange(s.Adendos.OrderBy(x => x.CriadoEm).Select(x => new RelatorioParDto { Rotulo = $"Adendo {Fd(Local(x.CriadoEm))} · {x.AutorNome}", Valor = x.Texto }));
                resultado.Add(new RelatorioConteudoSigilosoDto { Chave = chave, Rotulo = $"sessão nº {s.Numero} ({pront.Numero})", Textos = textos });
            }

            return resultado;
        }

        return null;
    }

    // ------------------------------------------------------------------ documento para outra instituição

    public async Task<DocumentoExternoResponse?> DocumentoExternoAsync(UsuarioAtuante u, string perfil, int assistidaId, string? tipo, string? referencia, CancellationToken ct)
    {
        if (perfil is not (PerfisAcesso.Juridico or PerfisAcesso.Psicologia or PerfisAcesso.AssistenteSocial)) return null;
        var d = await CarregarAsync(ct);
        if (!d.PorId.TryGetValue(assistidaId, out var a)) return null;
        var p = DesdeEntrada(ResolverPeriodo(tipo, referencia, d.InicioDados), Local(a.CriadoEm), "a entrada na Casa");
        var agora = DateTime.UtcNow;
        var registro = d.Usuarios.TryGetValue(u.UserId, out var usr) && !string.IsNullOrWhiteSpace(usr.Crp) ? $"CRP {usr.Crp}" : $"ID {u.Identificador}";
        var resp = new DocumentoExternoResponse
        {
            Nome = a.Nome,
            Cpf = AssistidasService.FormatarCpf(a.Cpf),
            PeriodoIni = Fd(p.Ini),
            PeriodoFim = Fd(p.Fim),
            ProfissionalNome = u.Nome,
            ProfissionalRegistro = registro
        };

        if (perfil == PerfisAcesso.Juridico)
        {
            if (!d.Casos.TryGetValue(a.Id, out var caso) || caso.Atendimentos.Count == 0 || !PodeVerConteudoCaso(caso, u, d, agora)) return null;
            resp.Area = "Jurídico";
            resp.Quantidade = caso.Atendimentos.Count(x => p.Dentro(Local(x.CriadoEm)));
            resp.RotuloQuantidade = "atendimento(s) jurídico(s)";
            resp.Situacao = caso.Situacao == CasoJuridicoSituacao.Encerrado ? "foi acompanhada pelo Jurídico da Casa" : "está em acompanhamento no Jurídico da Casa";
            return resp;
        }

        if (perfil == PerfisAcesso.AssistenteSocial)
        {
            if (!d.ProntuariosSociais.TryGetValue(a.Id, out var social) || social.Atendimentos.Count == 0 || !ResponsavelProntuarioSocial(social, u)) return null;
            resp.Area = "Serviço Social";
            resp.Quantidade = social.Atendimentos.Count(x => p.Dentro(Local(x.RealizadoEm)));
            resp.RotuloQuantidade = "atendimento(s) do Serviço Social";
            resp.Situacao = social.Situacao == ProntuarioSituacao.Encerrado ? "foi acompanhada pelo Serviço Social da Casa" : "está em acompanhamento no Serviço Social da Casa";
            return resp;
        }

        if (!d.Prontuarios.TryGetValue(a.Id, out var pront) || pront.Sessoes.Count == 0 || !ResponsavelProntuario(pront, u)) return null;
        resp.Area = "Psicologia";
        resp.Quantidade = pront.Sessoes.Count(s => s.Presenca == "compareceu" && p.Dentro(Local(s.CriadoEm)));
        resp.RotuloQuantidade = "sessão(ões) com comparecimento";
        resp.Situacao = pront.Situacao == ProntuarioSituacao.Encerrado ? "foi acompanhada pela Psicologia da Casa" : "está em acompanhamento psicológico na Casa";
        return resp;
    }
}
