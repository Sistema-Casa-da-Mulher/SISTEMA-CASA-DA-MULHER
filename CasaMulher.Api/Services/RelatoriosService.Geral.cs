using System.Globalization;
using CasaMulher.Api.DTOs;
using CasaMulher.Api.Models;
using CasaMulher.Api.Security;
using Microsoft.EntityFrameworkCore;

namespace CasaMulher.Api.Services;

/// <summary>Relatório Estatístico/Geral: da Casa (ADM) ou da área (demais perfis).</summary>
public sealed partial class RelatoriosService
{
    public async Task<RelatorioResponse> GerarGeralAsync(
        UsuarioAtuante usuario,
        string perfil,
        string? tipo,
        string? referencia,
        string? filtro,
        CriteriosAtencao criterios,
        CancellationToken ct)
    {
        var dados = await CarregarAsync(ct);
        var p = ResolverPeriodo(tipo, referencia, dados.InicioDados);
        var ant = Anterior(p);
        var agora = DateTime.UtcNow;

        var resposta = perfil switch
        {
            PerfisAcesso.Adm => await GeralAdmAsync(dados, p, ant, criterios, ct),
            PerfisAcesso.Recepcao => await GeralRecepcaoAsync(dados, p, ant, ct),
            PerfisAcesso.Juridico => GeralJuridico(dados, p, ant, usuario, filtro == "meus", agora),
            PerfisAcesso.Psicologia => GeralPsicologia(dados, p, ant, usuario, filtro == "meus", agora),
            PerfisAcesso.Professor => GeralProfessora(dados, p, ant, usuario, filtro),
            PerfisAcesso.AssistenteSocial => GeralServicoSocial(dados, p, ant, usuario, filtro == "meus", agora),
            _ => throw new InvalidOperationException("Perfil sem relatório.")
        };

        resposta.Perfil = perfil;
        resposta.Periodo = p.ParaDto();
        resposta.Comparacao ??= ant is null ? null : $"comparado com {ant.Rotulo.ToLower(PtBr)}";
        resposta.Rodape = Rodape(usuario, agora);
        return resposta;
    }

    private (DateTime Ini, DateTime Fim) FaixaUtc(PeriodoRelatorio p) =>
        (ParaUtc(p.Ini.ToDateTime(TimeOnly.MinValue)), ParaUtc(p.Fim.AddDays(1).ToDateTime(TimeOnly.MinValue)));

    // ================================================================== ADM / Coordenação

    private sealed record IndicadoresCasa(int Novas, int Atendidas, int Jur, int Psi, int Soc, int Aulas, int Freq, int FreqTotal, int Fila);

    private static IndicadoresCasa IndicadoresAdm(Dados d, PeriodoRelatorio p)
    {
        var regs = d.Registros.Where(r => p.Dentro(r.Data)).ToList();
        var atendidas = new HashSet<int>(regs.Where(r => r.Status == "presente").Select(r => r.AssistidaId));
        var ats = d.Casos.Values.SelectMany(c => c.Atendimentos.Select(a => (c.AssistidaId, a))).Where(x => p.Dentro(Local(x.a.CriadoEm))).ToList();
        var sess = d.Prontuarios.Values.SelectMany(pr => pr.Sessoes.Select(s => (pr.AssistidaId, s))).Where(x => p.Dentro(Local(x.s.CriadoEm))).ToList();
        atendidas.UnionWith(ats.Select(x => x.AssistidaId));
        atendidas.UnionWith(sess.Where(x => x.s.Presenca == "compareceu").Select(x => x.AssistidaId));
        var soc = d.ProntuariosSociais.Values.SelectMany(pr => pr.Atendimentos.Select(a => (pr.AssistidaId, a))).Where(x => p.Dentro(Local(x.a.RealizadoEm))).ToList();
        atendidas.UnionWith(soc.Select(x => x.AssistidaId));
        var f = Freq(regs);

        return new IndicadoresCasa(
            d.Assistidas.Count(a => p.Dentro(Local(a.CriadoEm))),
            atendidas.Count,
            ats.Count,
            sess.Count,
            soc.Count,
            regs.Select(r => (r.CursoId, r.Data)).Distinct().Count(),
            f.Pct,
            f.Total,
            d.Assistidas.Count(a => AguardandoJuridico(a, d, p.Fim) || AguardandoPsicologia(a, d, p.Fim) || AguardandoServicoSocial(a, d, p.Fim)));
    }

    private sealed record ProcuraCurso(Curso Curso, string Professoras, int Procura, int Ativas, int Concluiram, int Interrompidas, int Vinculadas, Frequencia Freq, int Aulas);

    private static List<ProcuraCurso> ProcuraCursos(Dados d, PeriodoRelatorio p, Func<Matricula, bool>? filtro = null)
    {
        return d.Cursos.Values.Select(c =>
        {
            var ms = d.Matriculas.Where(m => m.Curso.Id == c.Id && (filtro is null || filtro(m))).ToList();
            var ids = ms.Select(m => m.Assistida.Id).ToHashSet();
            var regs = d.Registros.Where(r => r.CursoId == c.Id && p.Dentro(r.Data) && ids.Contains(r.AssistidaId)).ToList();
            var profs = d.Professoras.Where(x => x.CursoId == c.Id).Select(x => d.NomeUsuario(x.UserId)).OrderBy(x => x).ToList();
            return new ProcuraCurso(
                c,
                profs.Count == 0 ? "-" : string.Join(", ", profs),
                ms.Count(m => p.Dentro(m.Inicio)),
                ms.Count(m => m.AtivaEm(p.Fim)),
                ms.Count(m => m.Situacao == AssistidaCursoSituacao.Concluiu && p.Dentro(m.Fim)),
                ms.Count(m => m.Situacao == AssistidaCursoSituacao.Interrompido && p.Dentro(m.Fim)),
                ms.Count(m => m.VinculadaNo(p)),
                Freq(regs),
                d.Registros.Where(r => r.CursoId == c.Id && p.Dentro(r.Data)).Select(r => r.Data).Distinct().Count());
        }).ToList();
    }

    private sealed record LinhaEncaminhamento(string Chave, string Nome, int Encaminhadas, int Atendidas, int Aguardando, int SemAgendamento, int Cancelados, int? EsperaMedia, int MaisAntiga, int AcimaPrazo);

    private static List<LinhaEncaminhamento> Encaminhamentos(Dados d, PeriodoRelatorio p, int prazoFila, IEnumerable<Assistida>? base_ = null)
    {
        var lista = (base_ ?? d.Assistidas).ToList();
        LinhaEncaminhamento Montar(string chave, string nome, Func<Assistida, DateTime?> enc, Func<Assistida, DateOnly?> primeiro, Func<Assistida, bool> aguardando)
        {
            var atendidas = lista.Where(a => enc(a) is not null && p.Dentro(primeiro(a))).ToList();
            var esperas = atendidas.Select(a => Math.Max(0, Dias(Local(enc(a)!.Value), primeiro(a)!.Value))).ToList();
            var ag = lista.Where(aguardando).ToList();
            var dias = ag.Select(a => Dias(Local(enc(a)!.Value), p.Fim)).ToList();
            return new LinhaEncaminhamento(
                chave,
                nome,
                lista.Count(a => p.Dentro(Local(enc(a)))),
                atendidas.Count,
                ag.Count,
                chave == "juridico" ? ag.Count(a => a.JuridicoAgendadoPara is null) : 0,
                chave == "juridico" ? lista.Count(a => p.Dentro(Local(a.JuridicoEncaminhamentoCanceladoEm))) : 0,
                esperas.Count > 0 ? (int)Math.Round(esperas.Average()) : null,
                dias.Count > 0 ? dias.Max() : 0,
                dias.Count(x => x > prazoFila));
        }

        return
        [
            Montar("juridico", "Jurídico", a => a.EncaminhadaJuridicoEm, a => d.PrimeiroAtendimentoJuridico(a.Id), a => AguardandoJuridico(a, d, p.Fim)),
            Montar("psicologia", "Psicologia", a => a.EncaminhadaPsicologiaEm, a => d.PrimeiraSessao(a.Id), a => AguardandoPsicologia(a, d, p.Fim)),
            Montar("assistente_social", "Serviço Social", a => a.EncaminhadaAssistenciaSocialEm, a => d.PrimeiroAtendimentoSocial(a.Id), a => AguardandoServicoSocial(a, d, p.Fim))
        ];
    }

    private async Task<RelatorioResponse> GeralAdmAsync(Dados d, PeriodoRelatorio p, PeriodoRelatorio? ant, CriteriosAtencao crit, CancellationToken ct)
    {
        var I = IndicadoresAdm(d, p);
        var A = ant is null ? null : IndicadoresAdm(d, ant);
        var r = new RelatorioResponse { Titulo = "Relatório Estatístico/Geral da Casa", Criterios = new RelatorioCriteriosDto { PrazoFila = crit.PrazoFila, FaltasSeguidas = crit.FaltasSeguidas, DiasRetornoVencido = crit.DiasRetornoVencido, OcupacaoBaixa = crit.OcupacaoBaixa } };
        var encs = Encaminhamentos(d, p, crit.PrazoFila);
        var pc = ProcuraCursos(d, p);
        var novas = d.Assistidas.Where(a => p.Dentro(Local(a.CriadoEm))).ToList();

        r.Kpis =
        [
            KpiN(I.Novas, "Novas assistidas", A?.Novas),
            Kpi(N(I.Atendidas), "Assistidas atendidas", null, I.Atendidas, A?.Atendidas, titulo: "Tiveram presença em aula, atendimento jurídico, sessão de psicologia com comparecimento ou atendimento do Serviço Social"),
            KpiN(I.Jur, "Atendimentos jurídicos", A?.Jur),
            KpiN(I.Psi, "Sessões de psicologia", A?.Psi),
            KpiN(I.Soc, "Atendimentos do Serviço Social", A?.Soc),
            KpiN(I.Aulas, "Aulas com chamada", A?.Aulas),
            Kpi(I.FreqTotal > 0 ? I.Freq + "%" : "-", "Frequência média nos cursos", I.FreqTotal > 0 ? null : "Sem chamada no período", I.Freq, A is { FreqTotal: > 0 } && I.FreqTotal > 0 ? A.Freq : null, " p.p."),
            Kpi(N(I.Fila), "Aguardando 1º atendimento", "Jurídico, Psicologia e Serviço Social, no fim do período"),
            Kpi(N(d.Assistidas.Count(a => a.AcessoRestrito && Local(a.CriadoEm) <= p.Fim)), "Casos com acesso restrito", "Total na Casa")
        ];

        // ---- pontos de atenção (até 5, sem nomes e sem conteúdo)
        var pontos = new List<RelatorioPontoDto>();
        var filaAcima = encs.Where(e => e.AcimaPrazo > 0).OrderByDescending(e => e.MaisAntiga).ToList();
        if (filaAcima.Count > 0)
        {
            pontos.Add(new RelatorioPontoDto
            {
                Titulo = "Fila e demora",
                Texto = string.Join(" ", filaAcima.Select(e => $"{e.Nome}: {e.Aguardando} aguardando 1º atendimento, {e.AcimaPrazo} acima de {crit.PrazoFila} dias; a mais antiga há {e.MaisAntiga} dias.")),
                Quem = "Coordenação com a equipe da área",
                Acao = "rever agenda e capacidade."
            });
        }

        var jurSemAg = encs.First(e => e.Chave == "juridico").SemAgendamento;
        var vencidos = d.Casos.Values
            .Select(c => c.Atendimentos.Where(a => Local(a.CriadoEm) <= p.Fim).OrderBy(a => a.CriadoEm).LastOrDefault())
            .Count(u => u is not null && u.Situacao != CasoJuridicoSituacao.Encerrado && u.DataRetorno is not null && Dias(u.DataRetorno.Value, p.Fim) > crit.DiasRetornoVencido);
        if (jurSemAg > 0 || vencidos > 0)
        {
            var partes = new List<string>();
            if (jurSemAg > 0) partes.Add($"{jurSemAg} encaminhamento(s) ao Jurídico sem agendamento");
            if (vencidos > 0) partes.Add($"{vencidos} caso(s) do Jurídico com retorno vencido há mais de {crit.DiasRetornoVencido} dias sem novo registro");
            pontos.Add(new RelatorioPontoDto { Titulo = "Encaminhamentos sem andamento", Texto = string.Join("; ", partes) + ".", Quem = "Coordenação do Jurídico", Acao = "distribuir e conferir as pendências." });
        }

        var ativosComVagas = pc.Where(x => x.Curso.Ativo && x.Curso.Vagas is > 0).ToList();
        var lotados = ativosComVagas.Where(x => x.Ativas >= x.Curso.Vagas && x.Procura > 0).ToList();
        var ociosos = ativosComVagas.Where(x => Pct(x.Ativas, x.Curso.Vagas!.Value) < crit.OcupacaoBaixa).ToList();
        var semVagas = pc.Count(x => x.Curso.Ativo && x.Curso.Vagas is null or 0);
        if (lotados.Count > 0 || ociosos.Count > 0)
        {
            var partes = new List<string>();
            if (lotados.Count > 0) partes.Add("Turma cheia e ainda com procura no período: " + string.Join(", ", lotados.Select(x => $"{x.Curso.Nome} ({x.Ativas}/{x.Curso.Vagas}, {x.Procura} novas)")) + ".");
            if (ociosos.Count > 0) partes.Add($"Menos de {crit.OcupacaoBaixa}% das vagas ocupadas: " + string.Join(", ", ociosos.Select(x => $"{x.Curso.Nome} ({x.Ativas}/{x.Curso.Vagas})")) + ".");
            pontos.Add(new RelatorioPontoDto { Titulo = "Capacidade dos cursos", Texto = string.Join(" ", partes), Quem = "Coordenação com as professoras", Acao = "ajustar vagas, divulgação ou oferta." });
        }

        var faltosas = d.Matriculas.Where(m => m.Situacao == AssistidaCursoSituacao.Cursando && m.AtivaEm(p.Fim))
            .Where(m => SequenciaFaltas(d.Registros.Where(x => x.AssistidaId == m.Assistida.Id && x.CursoId == m.Curso.Id && x.Data >= m.Inicio && x.Data <= p.Fim)) >= crit.FaltasSeguidas)
            .ToList();
        var interrompidas = d.Matriculas.Where(m => m.Situacao == AssistidaCursoSituacao.Interrompido && p.Dentro(m.Fim)).ToList();
        if (faltosas.Count > 0 || interrompidas.Count > 0)
        {
            pontos.Add(new RelatorioPontoDto
            {
                Titulo = "Interrupção da participação",
                Texto = $"{faltosas.Count} aluna(s) com {crit.FaltasSeguidas} ou mais faltas seguidas e {interrompidas.Count} interrupção(ões) registrada(s) no período.",
                Quem = "Professora responsável e equipe",
                Acao = "avaliar o acompanhamento, pelo canal e no horário seguros de cada uma (não é abandono nem gera contato automático)."
            });
        }

        var semChamada = AulasSemChamada(d, p, null);
        if (semChamada.Count > 0)
        {
            pontos.Add(new RelatorioPontoDto
            {
                Titulo = "Qualidade dos registros",
                Texto = $"{semChamada.Count} aula(s) planejada(s) sem chamada lançada ({string.Join(", ", semChamada.Select(x => x.Curso).Distinct())}). Frequência e faltas deste relatório podem estar incompletas.",
                Quem = "Professoras",
                Acao = "lançar a chamada antes de qualquer decisão baseada nesses números."
            });
        }

        r.Avisos.Add(new RelatorioAvisoDto
        {
            Tipo = "info",
            Titulo = "Pontos de atenção do período",
            Texto = pontos.Count == 0 ? "Nenhum alerta identificado pelos critérios configurados." : null,
            Pontos = pontos.Take(5).ToList(),
            Nota = semVagas > 0 ? $"{semVagas} curso(s) ativo(s) sem vagas cadastradas na tela Cursos: a ocupação deles aparece como \"sem registro\"." : string.Empty
        });

        // ---- procura
        var tudo = pc.Where(x => x.Curso.Ativo || x.Procura > 0).Select(x => (Rotulo: x.Curso.Nome, Valor: x.Procura, Tipo: "Curso"))
            .Concat(encs.Select(e => (Rotulo: e.Nome, Valor: e.Encaminhadas, Tipo: "Encaminhamento")))
            .OrderByDescending(x => x.Valor).ThenBy(x => x.Rotulo).ToList();
        var totalNovas = Math.Max(1, I.Novas);
        var secao = new RelatorioSecaoDto();
        secao.Blocos.Add(new RelatorioBlocoDto
        {
            Tipo = "podio",
            Titulo = "O que as assistidas mais e menos procuraram",
            Nota = "Cursos e encaminhamentos escolhidos no direcionamento (Recepção, Jurídico, Psicologia ou Serviço Social) dentro do período. Uma assistida pode escolher mais de um.",
            Largo = true,
            Itens = tudo.Select(x => Item(x.Rotulo, x.Valor, x.Tipo == "Curso" ? "barra" : "roxo", x.Valor > 0 ? Pct(x.Valor, totalNovas) + "%" : null)).Select((i, k) => { i.Tipo = tudo[k].Tipo; return i; }).ToList(),
            Legenda = [Item("Curso", 0, "barra"), Item("Encaminhamento", 0, "roxo"), Item("% = em relação às novas assistidas do período", 0)]
        });

        secao.Blocos.Add(Tabela("Cursos: procura, ocupação e resultado",
            "Ocupação = alunas ativas no fim do período ÷ vagas. Taxa de interrupção = interrupções no período ÷ todas as alunas que estiveram na turma no período (inclui quem continua cursando).",
            [Col("Curso"), Col("Professora"), Col("Procura", true), Col("Ativas", true), Col("Vagas", true, true), Col("Ocupação", true), Col("Concluíram", true, true), Col("Interrupções", true, true), Col("Taxa de interrupção", true), Col("Frequência", true), Col("Aulas", true)],
            pc.OrderByDescending(x => x.Procura).ThenBy(x => x.Curso.Nome).Select(x => Linha(
                C(x.Curso.Nome, x.Curso.Ativo ? null : null, x.Curso.Ativo ? null : "Desativado"),
                C(x.Professoras),
                C(x.Procura),
                C(x.Ativas),
                C(x.Curso.Vagas is > 0 ? N(x.Curso.Vagas.Value) : "sem registro"),
                C(x.Curso.Ativo && x.Curso.Vagas is > 0 ? Pct(x.Ativas, x.Curso.Vagas.Value) + "%" : "-"),
                C(x.Concluiram),
                C(x.Interrompidas),
                C(x.Vinculadas > 0 ? $"{Pct(x.Interrompidas, x.Vinculadas)}% de {x.Vinculadas}" : "-"),
                C(x.Freq.Total > 0 ? x.Freq.Pct + "%" : "-"),
                C(x.Aulas))), true));

        secao.Blocos.Add(new RelatorioBlocoDto
        {
            Tipo = "tabela",
            Titulo = "Encaminhamentos: procura e espera",
            Nota = "Aguardando = encaminhadas que ainda não tiveram o primeiro atendimento no fim do período (cancelados pelo Jurídico saem da fila).",
            Largo = true,
            Colunas = [Col("Área"), Col("Encaminhadas", true), Col("1º atendimento no período", true), Col("Aguardando", true), Col("Sem agendamento", true), Col("Cancelados", true), Col("Espera média", true), Col("Espera mais longa", true)],
            Linhas = encs.Select(e => Linha(C(e.Nome), C(e.Encaminhadas), C(e.Atendidas), C(e.Aguardando), C(e.Chave == "juridico" ? N(e.SemAgendamento) : "-"), C(e.Chave == "juridico" ? N(e.Cancelados) : "-"),
                C(e.EsperaMedia is null ? "-" : e.EsperaMedia + " dias"), C(e.MaisAntiga > 0 ? e.MaisAntiga + " dias" : "-"))).ToList()
        });

        var combos = Contar(novas, a =>
        {
            var entrada = Local(a.CriadoEm);
            var partes = new List<string>();
            if (a.Cursos.Any(c => Local(c.CriadoEm) == entrada)) partes.Add("Curso");
            if (Local(a.EncaminhadaJuridicoEm) == entrada) partes.Add("Jurídico");
            if (Local(a.EncaminhadaPsicologiaEm) == entrada) partes.Add("Psicologia");
            if (Local(a.EncaminhadaAssistenciaSocialEm) == entrada) partes.Add("Serviço Social");
            return partes.Count == 0 ? "Sem direcionamento na entrada" : string.Join(" + ", partes);
        });
        secao.Blocos.Add(Barras("Combinações mais comuns na chegada", "O que cada nova assistida escolheu já no primeiro direcionamento.", combos.Take(8)));
        secao.Blocos.Add(Barras("Por onde entraram", "Quem fez o cadastro (a ficha é única para a Casa).", Contar(novas, a => RotuloOrigemPara(a.Origem, PerfisAcesso.Adm))));
        r.Secoes.Add(secao);

        // ---- evolução mensal
        r.Secoes.Add(new RelatorioSecaoDto
        {
            Blocos =
            [
                Colunas("Novas assistidas por mês", NotaMes(p), PorMes(p, d.Assistidas.Select(a => Local(a.CriadoEm)))),
                Colunas("Atendimentos jurídicos por mês", NotaMes(p), PorMes(p, d.Casos.Values.SelectMany(c => c.Atendimentos).Select(a => Local(a.CriadoEm))), "roxo"),
                Colunas("Sessões de psicologia por mês", NotaMes(p), PorMes(p, d.Prontuarios.Values.SelectMany(x => x.Sessoes).Select(s => Local(s.CriadoEm))), "azul"),
                Colunas("Atendimentos do Serviço Social por mês", NotaMes(p), PorMes(p, d.ProntuariosSociais.Values.SelectMany(x => x.Atendimentos).Select(a => Local(a.RealizadoEm))), "roxo"),
                Colunas("Presenças em aula por mês", NotaMes(p), PorMes(p, d.Registros.Where(x => x.Status == "presente").Select(x => x.Data)), "ok")
            ]
        });

        // ---- perfil de quem chegou
        var areas = new Dictionary<int, HashSet<string>>();
        void Marca(int id, string area) { if (!areas.TryGetValue(id, out var s)) areas[id] = s = []; s.Add(area); }
        foreach (var x in d.Registros.Where(x => x.Status == "presente" && p.Dentro(x.Data))) Marca(x.AssistidaId, "curso");
        foreach (var c in d.Casos.Values) if (c.Atendimentos.Any(a => p.Dentro(Local(a.CriadoEm)))) Marca(c.AssistidaId, "jur");
        foreach (var pr in d.Prontuarios.Values) if (pr.Sessoes.Any(s => s.Presenca == "compareceu" && p.Dentro(Local(s.CriadoEm)))) Marca(pr.AssistidaId, "psi");
        foreach (var ps in d.ProntuariosSociais.Values) if (ps.Atendimentos.Any(a => p.Dentro(Local(a.RealizadoEm)))) Marca(ps.AssistidaId, "soc");
        var qtd = areas.Values.Select(s => s.Count).ToList();
        var comEndereco = novas.Where(a => !a.EnderecoSigiloso).ToList();

        r.Secoes.Add(new RelatorioSecaoDto
        {
            Blocos =
            [
                Barras("Faixa etária na chegada", $"{novas.Count} novas assistidas no período.", FaixaEtaria(novas)),
                new RelatorioBlocoDto
                {
                    Tipo = "barras", Titulo = "Onde moram", Nota = "Cidade e bairros mais frequentes (endereços sigilosos ficam fora).",
                    Itens = Contar(comEndereco, a => string.IsNullOrWhiteSpace(a.Cidade) ? "Sem cidade informada" : a.Cidade.Trim()),
                    Extras = [Barras("", null, Contar(comEndereco, a => a.Bairro?.Trim()).Take(8))]
                },
                BlocoContato("Contato e segurança", "Como a Casa pode falar com elas.", novas),
                Barras("Em quantas frentes cada uma é atendida", $"Assistidas com presença em curso, atendimento jurídico, sessão ou atendimento social no período ({qtd.Count}). Frentes: cursos, Jurídico, Psicologia e Serviço Social.",
                    new[] { 1, 2, 3, 4 }.Select(k => Item(k == 1 ? "1 frente" : $"{k} frentes", qtd.Count(x => x == k))))
            ]
        });

        // ---- Jurídico em números (só contagens)
        var atsP = d.Casos.Values.SelectMany(c => c.Atendimentos).Where(a => p.Dentro(Local(a.CriadoEm))).ToList();
        var casosNovos = d.Casos.Values.Where(c => c.Atendimentos.Count > 0 && p.Dentro(Local(c.Atendimentos.Min(a => a.CriadoEm)))).ToList();
        var sitFim = d.Casos.Values.Select(c => UltimaSituacaoCaso(c, p.Fim)).Where(s => s is not null).ToList();
        r.Secoes.Add(new RelatorioSecaoDto
        {
            Titulo = "Jurídico em números",
            Subtitulo = "Só contagens: a Coordenação não vê relatos.",
            Blocos =
            [
                Barras("Tipos de demanda (casos novos)", $"{casosNovos.Count} casos com 1º atendimento no período.", Contar(casosNovos, c => RotulosDemanda.GetValueOrDefault(c.Atendimentos.OrderBy(a => a.CriadoEm).First().TipoDemanda, "Outro"))),
                Pilha("Situação dos casos no fim do período", "Todos os casos com atendimento até o fim do período.",
                    [Item("Em andamento", sitFim.Count(s => s == CasoJuridicoSituacao.EmAndamento), "roxo"), Item("Aguardando documento", sitFim.Count(s => s == CasoJuridicoSituacao.AguardandoDocumento), "justificada"), Item("Encerrado", sitFim.Count(s => s == CasoJuridicoSituacao.Encerrado), "neutro")],
                    $"Atendimentos com medida protetiva: {atsP.Count(a => a.TemMedidaProtetiva == true)} de {atsP.Count} · Adendos: {atsP.Sum(a => a.Adendos.Count)}"),
                Barras("Encaminhamentos externos do Jurídico", null, Contar(atsP, a => Separar(a.Encaminhamentos, RotulosEncJuridico))),
                Barras("Atendimentos por profissional", "Só a contagem.", Contar(atsP, a => a.ProfissionalNome))
            ]
        });

        // ---- Psicologia em números (sem nomes e sem risco)
        var sP = d.Prontuarios.Values.SelectMany(x => x.Sessoes).Where(s => p.Dentro(Local(s.CriadoEm))).ToList();
        var encerrados = d.Prontuarios.Values.Where(x => x.EncerradoEm is not null && p.Dentro(Local(x.EncerradoEm.Value))).ToList();
        r.Secoes.Add(new RelatorioSecaoDto
        {
            Titulo = "Psicologia em números",
            Subtitulo = "(sem nomes, como a Coordenação já vê hoje)",
            Blocos =
            [
                Kpis("Acompanhamentos", null,
                    Kpi(N(d.Prontuarios.Values.Count(x => ProntuarioAtivoEm(x, p.Fim))), "Em acompanhamento"),
                    Kpi(N(d.Prontuarios.Values.Count(x => x.Sessoes.Count > 0 && p.Dentro(Local(x.Sessoes.Min(s => s.CriadoEm))))), "Prontuários abertos"),
                    Kpi(N(encerrados.Count), "Encerrados")),
                new RelatorioBlocoDto
                {
                    Tipo = "pilha", Titulo = "Presença nas sessões", Nota = $"{sP.Count} sessões no período.",
                    Itens = RotulosPresenca.Select(k => Item(k.Value, sP.Count(s => s.Presenca == k.Key), CorPresenca(k.Key))).ToList(),
                    Extras = [Barras("", null, Contar(sP, s => RotulosModalidade.GetValueOrDefault(s.Modalidade ?? "", null!)))]
                },
                Texto("Risco clínico", null, "A classificação de risco não aparece para o ADM. Ela chega à equipe técnica pelo alerta na ficha, na hora em que é registrada (uma urgência não pode depender da leitura de um relatório). Se a coordenação técnica precisar desse número, cria-se uma permissão específica para ela.", "sigilo"),
                new RelatorioBlocoDto
                {
                    Tipo = "barras", Titulo = "Motivos de encerramento", Itens = Contar(encerrados, x => RotulosEncerramento.GetValueOrDefault(x.MotivoEncerramento ?? "", "Outro")),
                    Extras = [Barras("Encaminhamentos externos", null, Contar(sP, s => Separar(s.EncaminhamentosExternos, RotulosEncPsicologia)))]
                }
            ]
        });

        // ---- Serviço Social em números (sem nomes, sem relatos, sem documentos)
        r.Secoes.Add(SecaoAdmServicoSocial(d, p));

        // ---- cursos: frequência e interrupções
        var regsP = d.Registros.Where(x => p.Dentro(x.Data)).ToList();
        r.Secoes.Add(new RelatorioSecaoDto
        {
            Titulo = "Cursos: frequência e interrupções",
            Blocos =
            [
                Barras("Frequência por curso", "Presenças ÷ chamadas registradas no período.", pc.Where(x => x.Freq.Total > 0).OrderByDescending(x => x.Freq.Pct).Select(x => Item(x.Curso.Nome, x.Freq.Pct, null, x.Freq.Total + " chamadas")), sufixo: "%", max: 100),
                Pilha("Faltas no período", "Justificativas e observações da chamada ficam só com a professora.", PilhaFrequencia(Freq(regsP))),
                new RelatorioBlocoDto { Tipo = "barras", Titulo = "Motivos de interrupção", DadoNovo = true, Nota = $"{interrompidas.Count} interrupções no período. Só a contagem por motivo, sem nomes.", Itens = Contar(interrompidas, m => MotivoCategoria(m.Vinculo.MotivoSaida)), Vazio = "Nenhuma interrupção registrada no período (ou ainda sem registro)." },
                new RelatorioBlocoDto
                {
                    Tipo = "kpis", Titulo = "Faltas seguidas (para avaliação)",
                    Nota = $"Cursando e com {crit.FaltasSeguidas} ou mais faltas seguidas até o fim do período. Isso pede avaliação da professora e da equipe: não é conclusão de abandono e não gera contato automático (pode ser transporte, saúde ou segurança).",
                    Kpis = [Kpi(N(faltosas.Count), $"Alunas com {crit.FaltasSeguidas}+ faltas seguidas")],
                    Extras = [Barras("", null, Contar(faltosas, m => m.Curso.Nome))]
                }
            ]
        });

        // ---- equipe, sigilo e segurança
        var (ini, fim) = FaixaUtc(p);
        var termos = await _db.TermoSigiloAceites.AsNoTracking().CountAsync(t => t.AceitoEm >= ini && t.AceitoEm < fim, ct);
        var transferencias = await _db.AuditoriaEventos.AsNoTracking().CountAsync(e => e.Acao == "RESPONSABILIDADES_TRANSFERIDAS" && e.CriadoEm >= ini && e.CriadoEm < fim, ct);
        var relatoriosImpressos = await _db.AuditoriaEventos.AsNoTracking().CountAsync(e => e.Acao == "RELATORIO_IMPRESSO" && e.CriadoEm >= ini && e.CriadoEm < fim, ct);
        var documentosExternos = await _db.AuditoriaEventos.AsNoTracking().CountAsync(e => e.Acao == "RELATORIO_DOCUMENTO_EXTERNO" && e.CriadoEm >= ini && e.CriadoEm < fim, ct);

        var perfisEquipe = new Dictionary<string, (string Nome, string Oque)>
        {
            [PerfisAcesso.Recepcao] = ("Recepção", "cadastros"),
            [PerfisAcesso.Juridico] = ("Jurídico", "atendimentos"),
            [PerfisAcesso.Psicologia] = ("Psicologia", "sessões"),
            [PerfisAcesso.AssistenteSocial] = ("Serviço Social", "atendimentos"),
            [PerfisAcesso.Professor] = ("Professora", "chamadas lançadas")
        };
        var linhasEquipe = d.Usuarios.Values
            .Where(u => perfisEquipe.ContainsKey(u.Perfil))
            .Select(u =>
            {
                var total = u.Perfil switch
                {
                    PerfisAcesso.Recepcao => d.Assistidas.Count(a => a.CadastradaPorUserId == u.Id && p.Dentro(Local(a.CriadoEm))),
                    PerfisAcesso.Juridico => atsP.Count(a => a.ProfissionalUserId == u.Id),
                    PerfisAcesso.Psicologia => sP.Count(s => s.ProfissionalUserId == u.Id),
                    PerfisAcesso.AssistenteSocial => d.ProntuariosSociais.Values.SelectMany(x => x.Atendimentos).Count(a => a.ProfissionalUserId == u.Id && p.Dentro(Local(a.RealizadoEm))),
                    _ => d.Chamadas.Count(c => c.RegistradaPorUserId == u.Id && p.Dentro(c.Data))
                };
                return (u, total);
            })
            .Where(x => x.u.Ativo || x.total > 0)
            .OrderBy(x => perfisEquipe[x.u.Perfil].Nome).ThenBy(x => x.u.Nome)
            .Select(x => Linha(C(x.u.Nome, sub: x.u.Ativo ? null : "Conta desativada"), C(x.u.Identificador), C(perfisEquipe[x.u.Perfil].Nome), C(x.total), C(perfisEquipe[x.u.Perfil].Oque)));

        var restritas = d.Assistidas.Where(a => a.AcessoRestrito && Local(a.CriadoEm) <= p.Fim).ToList();
        r.Secoes.Add(new RelatorioSecaoDto
        {
            Titulo = "Equipe, sigilo e segurança",
            Blocos =
            [
                Tabela("Trabalho por profissional", "Contagens do período, sem conteúdo.", [Col("Profissional"), Col("ID"), Col("Perfil"), Col("Total", true), Col("O quê")], linhasEquipe),
                new RelatorioBlocoDto
                {
                    Tipo = "tabela", Titulo = "Sigilo e segurança", Nota = "Vem dos registros do sistema e do histórico de ações.",
                    Colunas = [Col("Item"), Col("No período", true)],
                    Linhas =
                    [
                        Linha(C("Casos com acesso restrito (total)"), C(restritas.Count)),
                        Linha(C("Acessos de emergência no Jurídico"), C(d.EmergenciasJuridico.Count(e => p.Dentro(Local(e.CriadoEm))))),
                        Linha(C("Acessos de emergência na Psicologia"), C(d.EmergenciasPsicologia.Count(e => p.Dentro(Local(e.CriadoEm))))),
                        Linha(C("Acessos de emergência no Serviço Social"), C(d.EmergenciasSocial.Count(e => p.Dentro(Local(e.CriadoEm))))),
                        Linha(C("Documentos anexados no Serviço Social"), C(d.ProntuariosSociais.Values.SelectMany(x => x.Anexos).Count(a => p.Dentro(Local(a.CriadoEm))))),
                        Linha(C("Termos de sigilo assinados"), C(termos)),
                        Linha(C("Responsabilidades passadas"), C(transferencias)),
                        Linha(C("Relatórios impressos"), C(relatoriosImpressos)),
                        Linha(C("Documentos para outra instituição"), C(documentosExternos))
                    ],
                    Extras = [Barras("Casos restritos por motivo", null, Contar(restritas, a => a.MotivoRestricao switch
                    {
                        AssistidaMotivoRestricao.SegredoJustica => "Segredo de justiça",
                        AssistidaMotivoRestricao.CasaAbrigo => "Casa-abrigo",
                        AssistidaMotivoRestricao.RiscoGrave => "Risco grave",
                        AssistidaMotivoRestricao.PedidoAssistida => "Pedido da assistida",
                        _ => "Sem motivo informado"
                    }))]
                }
            ]
        });

        return r;
    }

    private static string CorPresenca(string presenca) => presenca switch
    {
        "compareceu" => "ok",
        "faltou_justificou" => "justificada",
        "faltou" => "falta",
        _ => "neutro"
    };

    private sealed record AulaPendente(string Curso, DateOnly Data);

    /// <summary>Aulas planejadas que já passaram e não têm chamada com presença marcada.</summary>
    private List<AulaPendente> AulasSemChamada(Dados d, PeriodoRelatorio p, List<int>? cursos)
    {
        var hoje = Hoje;
        var lancadas = d.Registros.Select(r => (r.CursoId, r.Data)).ToHashSet();
        return d.Planos
            .Where(pl => p.Dentro(pl.Data) && pl.Data < hoje && (cursos is null || cursos.Contains(pl.CursoId)) && !lancadas.Contains((pl.CursoId, pl.Data)) && d.Cursos.ContainsKey(pl.CursoId))
            .Select(pl => new AulaPendente(d.Cursos[pl.CursoId].Nome, pl.Data))
            .Distinct()
            .ToList();
    }

    // ================================================================== Recepção

    private async Task<RelatorioResponse> GeralRecepcaoAsync(Dados d, PeriodoRelatorio p, PeriodoRelatorio? ant, CancellationToken ct)
    {
        // Casos restritos ficam fora de tudo; nada da Psicologia aparece.
        var visiveis = d.Assistidas.Where(a => !a.AcessoRestrito).ToList();
        var idsVisiveis = visiveis.Select(a => a.Id.ToString(CultureInfo.InvariantCulture)).ToHashSet();
        List<Assistida> Acolh(PeriodoRelatorio q) => visiveis.Where(a => a.Origem == AssistidaOrigem.Recepcao && q.Dentro(Local(a.CriadoEm))).ToList();
        int Encs(PeriodoRelatorio q) => visiveis.Count(a => a.Origem == AssistidaOrigem.Recepcao && q.Dentro(Local(a.EncaminhadaJuridicoEm)))
            + visiveis.Count(a => a.Origem == AssistidaOrigem.Recepcao && q.Dentro(Local(a.EncaminhadaAssistenciaSocialEm)));

        var A = Acolh(p);
        var direc = d.Matriculas.Where(m => !m.Assistida.AcessoRestrito && p.Dentro(m.Inicio)).ToList();

        var perfisRecepcao = d.Usuarios.Values.Where(u => u.Perfil == PerfisAcesso.Recepcao).Select(u => u.Id).ToHashSet();
        var (ini, fim) = FaixaUtc(p);
        var atualizacoes = (await _db.AuditoriaEventos.AsNoTracking()
                .Where(e => e.Acao == "ASSISTIDA_ATUALIZADA" && e.CriadoEm >= ini && e.CriadoEm < fim)
                .Select(e => new { e.UsuarioId, e.EntidadeId, e.PerfilFuncionario })
                .ToListAsync(ct))
            .Where(e => e.EntidadeId is not null && idsVisiveis.Contains(e.EntidadeId) && (perfisRecepcao.Contains(e.UsuarioId) || e.PerfilFuncionario == PerfisAcesso.Recepcao))
            .ToList();

        var encJur = visiveis.Where(a => Local(a.EncaminhadaJuridicoEm) is { } e && e <= p.Fim).ToList();
        var r = new RelatorioResponse { Titulo = "Relatório Estatístico/Geral da Recepção" };
        r.Kpis =
        [
            KpiN(A.Count, "Acolhimentos novos", ant is null ? null : Acolh(ant).Count),
            KpiN(Encs(p), "Encaminhamentos feitos", ant is null ? null : Encs(ant), "Jurídico e Serviço Social"),
            KpiN(direc.Count, "Direcionamentos para cursos"),
            KpiN(atualizacoes.Count, "Fichas atualizadas"),
            Kpi(N(visiveis.Count(a => a.Origem != AssistidaOrigem.Recepcao && p.Dentro(Local(a.CriadoEm)))), "Cadastradas direto por outra área", "Já chegam com ficha pronta"),
            Kpi(N(encJur.Count(a => AguardandoJuridico(a, d, p.Fim))), "Jurídico: sem 1º atendimento", "Aguardando ou agendado, no fim do período"),
            Kpi(N(visiveis.Count(a => p.Dentro(Local(a.EncaminhadaAssistenciaSocialEm)))), "Encaminhadas ao Serviço Social", "O acompanhamento fica só com o Serviço Social")
        ];

        var ordemSit = new[] { "Aguardando atendimento", "Agendado", "Primeiro atendimento realizado", "Cancelado" };
        var sitJur = encJur.Select(a => SituacaoOperacionalJuridico(a, d, p.Fim).Chave).ToList();
        var cursosEscolhidos = d.Cursos.Values.Where(c => c.Ativo || direc.Any(m => m.Curso.Id == c.Id))
            .Select(c => Item(c.Nome, direc.Count(m => m.Curso.Id == c.Id))).OrderByDescending(i => i.Valor).ThenBy(i => i.Rotulo).ToList();

        // movimento por dia e horário (pelo horário em que a ficha foi criada)
        var dias = new[] { (DayOfWeek.Monday, "Seg"), (DayOfWeek.Tuesday, "Ter"), (DayOfWeek.Wednesday, "Qua"), (DayOfWeek.Thursday, "Qui"), (DayOfWeek.Friday, "Sex"), (DayOfWeek.Saturday, "Sáb") };
        var horarios = A.Select(a => HoraLocal(a.CriadoEm)).ToList();
        var horas = Enumerable.Range(7, 12).Where(h => h is >= 8 and <= 17 || horarios.Any(x => x.Hour == h)).ToList();
        var maxCel = Math.Max(1, horarios.GroupBy(x => (x.DayOfWeek, x.Hour)).Select(g => g.Count()).DefaultIfEmpty(0).Max());
        var calor = new RelatorioBlocoDto
        {
            Tipo = "tabela",
            Titulo = "Movimento por dia e horário",
            Nota = "Quantos acolhimentos começaram em cada faixa (pelo horário em que a ficha foi criada). Ajuda a montar a escala da Recepção.",
            Colunas = [Col("")] ,
            Linhas = dias.Where(x => x.Item1 != DayOfWeek.Saturday || horarios.Any(h => h.DayOfWeek == DayOfWeek.Saturday)).Select(dia =>
            {
                var linha = new List<RelatorioCelulaDto> { C(dia.Item2, destaque: true) };
                linha.AddRange(horas.Select(h =>
                {
                    var v = horarios.Count(x => x.DayOfWeek == dia.Item1 && x.Hour == h);
                    return new RelatorioCelulaDto { Texto = v > 0 ? N(v) : "", Calor = (double)v / maxCel };
                }));
                return linha;
            }).ToList()
        };
        calor.Colunas.AddRange(horas.Select(h => Col(h + "h", true)));

        var recepcionistas = d.Usuarios.Values.Where(u => u.Perfil == PerfisAcesso.Recepcao).OrderBy(u => u.Nome).ToList();
        r.Secoes.Add(new RelatorioSecaoDto
        {
            Blocos =
            [
                Colunas("Acolhimentos por mês", NotaMes(p), PorMes(p, visiveis.Where(a => a.Origem == AssistidaOrigem.Recepcao).Select(a => Local(a.CriadoEm)))),
                Barras("Encaminhamentos ao Jurídico: situação no fim do período", "Vem do registro do próprio Jurídico. A Recepção não vê demanda, relato, processo, orientação nem motivo de cancelamento.",
                    ordemSit.Select(k => Item(k, sitJur.Count(s => s == k), k switch { "Cancelado" => "falta", "Primeiro atendimento realizado" => "ok", "Agendado" => "roxo", _ => null }))),
                Barras("Encaminhamentos feitos pela Recepção", "Opções do card de direcionamento no período.",
                [
                    Item("Jurídico", visiveis.Count(a => a.Origem == AssistidaOrigem.Recepcao && p.Dentro(Local(a.EncaminhadaJuridicoEm)))),
                    Item("Serviço Social", visiveis.Count(a => a.Origem == AssistidaOrigem.Recepcao && p.Dentro(Local(a.EncaminhadaAssistenciaSocialEm))))
                ]),
                Barras("Cursos mais e menos escolhidos", "Do mais procurado ao menos procurado no período.", cursosEscolhidos, true),
                calor,
                Tabela("Por recepcionista", "Acolhimentos e atualizações de ficha.", [Col("Recepcionista"), Col("Acolhimentos", true), Col("Atualizações", true)],
                    recepcionistas.Select(u => Linha(C(u.Nome), C(A.Count(a => a.CadastradaPorUserId == u.Id)), C(atualizacoes.Count(e => e.UsuarioId == u.Id))))),
                Barras("Faixa etária", $"{A.Count} acolhimentos no período.", FaixaEtaria(A)),
                Barras("Bairros", "Endereços sigilosos ficam fora.", Contar(A.Where(a => !a.EnderecoSigiloso), a => a.Bairro?.Trim() ?? a.Cidade?.Trim()).Take(10)),
                BlocoContato("Como podemos falar com elas", null, A),
                Tabela("Mulheres acolhidas no período", "Mesmas informações da aba “Mulheres atendidas”. Casos restritos não aparecem.",
                    [Col("Data"), Col("Nome"), Col("Idade", true), Col("Cursos"), Col("Encaminhamentos"), Col("Recepcionista")],
                    A.OrderByDescending(a => a.CriadoEm).Select(a =>
                    {
                        var entrada = Local(a.CriadoEm);
                        var encsA = new List<string>();
                        if (Local(a.EncaminhadaJuridicoEm) == entrada) encsA.Add("Jurídico");
                        if (Local(a.EncaminhadaAssistenciaSocialEm) == entrada) encsA.Add("Serviço Social");
                        var cursos = d.Matriculas.Where(m => m.Assistida.Id == a.Id).Select(m => m.Curso.Nome).ToList();
                        return Linha(C(Fd(entrada)), C(a.Nome), C(Idade(a.DataNascimento, entrada)?.ToString(CultureInfo.InvariantCulture) ?? "-"),
                            C(cursos.Count == 0 ? "-" : string.Join(", ", cursos)), C(encsA.Count == 0 ? "-" : string.Join(", ", encsA)), C(a.CadastradaPorNome));
                    }), true)
            ]
        });

        return r;
    }

    // ================================================================== Jurídico

    private RelatorioResponse GeralJuridico(Dados d, PeriodoRelatorio p, PeriodoRelatorio? ant, UsuarioAtuante u, bool meus, DateTime agora)
    {
        var casos = d.Casos.Values.Where(c => !meus || ResponsavelCaso(c, u)).ToList();
        var visiveis = casos.Where(c => PodeVerConteudoCaso(c, u, d, agora)).Select(c => c.Id).ToHashSet();
        List<AtendimentoJuridico> Ats(PeriodoRelatorio q) => casos.SelectMany(c => c.Atendimentos).Where(a => q.Dentro(Local(a.CriadoEm))).ToList();
        var ats = Ats(p);
        var atsVis = ats.Where(a => visiveis.Contains(a.CasoJuridicoId)).ToList();
        var casosNovos = casos.Where(c => c.Atendimentos.Count > 0 && p.Dentro(Local(c.Atendimentos.Min(a => a.CriadoEm)))).ToList();
        var sitFim = casos.Select(c => UltimaSituacaoCaso(c, p.Fim)).Where(s => s is not null).ToList();
        var encerradosP = casos.Count(c => c.Atendimentos.Where(a => Local(a.CriadoEm) <= p.Fim).OrderBy(a => a.CriadoEm).LastOrDefault() is { } ult
            && ult.Situacao == CasoJuridicoSituacao.Encerrado && p.Dentro(Local(ult.CriadoEm)));
        var idsCasos = casos.Select(c => c.AssistidaId).ToHashSet();
        var fila = d.Assistidas.Where(a => AguardandoJuridico(a, d, p.Fim) && (!meus || idsCasos.Contains(a.Id))).ToList();
        var atendidasP = d.Assistidas.Where(a => a.EncaminhadaJuridicoEm is not null && p.Dentro(d.PrimeiroAtendimentoJuridico(a.Id)) && (!meus || idsCasos.Contains(a.Id))).ToList();
        var esperas = atendidasP.Select(a => Math.Max(0, Dias(Local(a.EncaminhadaJuridicoEm!.Value), d.PrimeiroAtendimentoJuridico(a.Id)!.Value))).ToList();
        var hoje = Hoje;
        var retornos7 = casos.Where(c => c.Situacao != CasoJuridicoSituacao.Encerrado)
            .Select(c => c.Atendimentos.OrderBy(a => a.CriadoEm).LastOrDefault()?.DataRetorno)
            .Count(x => x is not null && x >= hoje && x <= hoje.AddDays(7));

        var r = new RelatorioResponse { Titulo = "Relatório Estatístico/Geral do Jurídico" + (meus ? " · só os meus casos" : "") };
        r.Kpis =
        [
            KpiN(ats.Count, "Atendimentos (protocolos)", ant is null ? null : Ats(ant).Count),
            Kpi(N(ats.Select(a => a.CasoJuridicoId).Distinct().Count()), "Assistidas atendidas"),
            Kpi(N(casosNovos.Count), "Casos novos"),
            Kpi(N(sitFim.Count(s => s != CasoJuridicoSituacao.Encerrado)), "Casos em andamento", "No fim do período (inclui aguardando documento)"),
            Kpi(N(encerradosP), "Casos encerrados"),
            Kpi(N(fila.Count), "Na fila", "Encaminhadas sem 1º atendimento"),
            Kpi(esperas.Count > 0 ? Math.Round(esperas.Average()) + " dias" : "-", "Espera média na fila"),
            Kpi(N(retornos7), "Retornos nos próximos 7 dias", "Contado a partir de hoje")
        ];

        if (casos.Count != visiveis.Count)
        {
            r.Avisos.Add(new RelatorioAvisoDto
            {
                Tipo = "sigilo",
                Texto = $"{casos.Count - visiveis.Count} caso(s) estão como “somente as responsáveis” e você não é responsável. Eles entram só nas contagens de atendimentos, fila e situação; demanda, medida protetiva, encaminhamentos e processo vêm apenas dos casos que você pode ver."
            });
        }

        var risco = d.Prontuarios.Values.Where(x => x.Situacao == ProntuarioSituacao.EmAcompanhamento && x.Sessoes.Count > 0).ToDictionary(x => x.AssistidaId);
        var assistidasAtendidas = ats.Select(a => casos.First(c => c.Id == a.CasoJuridicoId).AssistidaId).Distinct().ToList();
        var casosAte = casos.Where(c => c.Atendimentos.Any(a => Local(a.CriadoEm) <= p.Fim)).ToList();
        var casosAteVis = casosAte.Where(c => visiveis.Contains(c.Id)).ToList();
        string? Processo(CasoJuridico c) => c.Atendimentos.Where(a => !string.IsNullOrWhiteSpace(a.NumeroProcesso)).OrderBy(a => a.CriadoEm).LastOrDefault()?.NumeroProcesso;
        string? Vara(CasoJuridico c) => c.Atendimentos.Where(a => !string.IsNullOrWhiteSpace(a.Vara)).OrderBy(a => a.CriadoEm).LastOrDefault()?.Vara;

        r.Secoes.Add(new RelatorioSecaoDto
        {
            Blocos =
            [
                Colunas("Atendimentos por mês", NotaMes(p), PorMes(p, casos.SelectMany(c => c.Atendimentos).Select(a => Local(a.CriadoEm))), "roxo"),
                Barras("Tipo de demanda", "Atendimentos do período por tipo (casos que você pode ver).", Contar(atsVis, a => RotulosDemanda.GetValueOrDefault(a.TipoDemanda, "Outro"))),
                Pilha("Situação dos casos", "No fim do período, de todos os casos já com atendimento.",
                    [Item("Em andamento", sitFim.Count(s => s == CasoJuridicoSituacao.EmAndamento), "roxo"), Item("Aguardando documento", sitFim.Count(s => s == CasoJuridicoSituacao.AguardandoDocumento), "justificada"), Item("Encerrado", sitFim.Count(s => s == CasoJuridicoSituacao.Encerrado), "neutro")]),
                Barras("Medida protetiva", "Resposta à pergunta “Existe medida protetiva?” nos atendimentos do período.",
                [Item("Sim", atsVis.Count(a => a.TemMedidaProtetiva == true), "falta"), Item("Não", atsVis.Count(a => a.TemMedidaProtetiva == false)), Item("Não informado", atsVis.Count(a => a.TemMedidaProtetiva is null), "neutro")]),
                Barras("Encaminhamentos externos", "Para onde o Jurídico encaminhou.", Contar(atsVis, a => Separar(a.Encaminhamentos, RotulosEncJuridico))),
                Barras("De onde vieram", "Por onde entraram na Casa as assistidas que tiveram 1º atendimento no período.", Contar(atendidasP, a => RotuloOrigemPara(a.Origem, PerfisAcesso.Juridico))),
                Barras("Atendimentos por profissional", null, Contar(ats, a => a.ProfissionalNome)),
                Tabela("Sigilo", null, [Col("Item"), Col("Quantidade", true)],
                [
                    Linha(C("Casos “somente as responsáveis”"), C(casosAte.Count(c => c.Visibilidade == CasoJuridicoVisibilidade.Responsaveis))),
                    Linha(C("Casos com acesso restrito"), C(casosAte.Count(c => d.PorId[c.AssistidaId].AcessoRestrito))),
                    Linha(C("Casos com mais de uma responsável"), C(casosAte.Count(c => c.Responsaveis.Count > 1))),
                    Linha(C("Adendos no período"), C(ats.Sum(a => a.Adendos.Count))),
                    Linha(C("Acessos de emergência no período"), C(d.EmergenciasJuridico.Count(e => p.Dentro(Local(e.CriadoEm)) && casos.Any(c => c.Id == e.CasoJuridicoId)))),
                    Linha(C("Assistidas também em acompanhamento na Psicologia"), C(assistidasAtendidas.Count(id => risco.ContainsKey(id)))),
                    Linha(C("Com alerta de risco alto/iminente hoje"), C(assistidasAtendidas.Count(id => risco.TryGetValue(id, out var pr) && PsicologiaCatalogo.RiscoElevado(pr.RiscoAtual))))
                ]),
                new RelatorioBlocoDto
                {
                    Tipo = "kpis", Titulo = "Processos", Nota = "Casos que você pode ver.",
                    Kpis = [Kpi(N(casosAteVis.Count(c => Processo(c) is not null)), "Casos com nº de processo"), Kpi(N(casosAteVis.Count(c => Processo(c) is null)), "Sem processo (orientação)")],
                    Extras = [Barras("", null, Contar(casosAteVis, c => Vara(c)))]
                }
            ]
        });

        var linhas = casos.Where(c => c.Atendimentos.Any(a => p.Dentro(Local(a.CriadoEm))))
            .Select(c =>
            {
                var a = d.PorId[c.AssistidaId];
                var lista = c.Atendimentos.OrderBy(x => x.CriadoEm).ToList();
                var ult = lista.Last();
                var podeVer = visiveis.Contains(c.Id);
                var noP = lista.Count(x => p.Dentro(Local(x.CriadoEm)));
                return (noP, Linha(
                    C(a.Nome, a.AcessoRestrito ? null : null, a.AcessoRestrito ? "Acesso restrito" : null),
                    C(podeVer ? RotulosDemanda.GetValueOrDefault(lista.First().TipoDemanda, "Outro") : "—"),
                    C(noP), C(lista.Count), C(ult.Protocolo),
                    C(RotulosSituacaoCaso.GetValueOrDefault(ult.Situacao, ult.Situacao)),
                    C(podeVer && ult.DataRetorno is not null ? Fd(ult.DataRetorno) : "-"),
                    C(c.Responsaveis.Count == 0 ? "-" : string.Join(", ", c.Responsaveis.Select(x => x.Nome))),
                    c.Visibilidade == CasoJuridicoVisibilidade.Equipe ? C("Equipe") : C("Só responsáveis", "neutro")));
            })
            .OrderByDescending(x => x.noP).Select(x => x.Item2).ToList();
        r.Secoes.Add(new RelatorioSecaoDto
        {
            Blocos = [Tabela("Casos com atendimento no período", "A lista mostra só metadados. O conteúdo de cada caso fica no relatório individual.",
                [Col("Assistida"), Col("Demanda"), Col("No período", true), Col("Total", true), Col("Último protocolo"), Col("Situação"), Col("Retorno"), Col("Responsáveis"), Col("Quem vê")], linhas, true)]
        });

        return r;
    }

    // ================================================================== Psicologia

    private RelatorioResponse GeralPsicologia(Dados d, PeriodoRelatorio p, PeriodoRelatorio? ant, UsuarioAtuante u, bool meus, DateTime agora)
    {
        var pronts = d.Prontuarios.Values.Where(x => !meus || ResponsavelProntuario(x, u)).ToList();
        var visiveis = pronts.Where(x => PodeVerConteudoProntuario(x, u, d, agora)).Select(x => x.Id).ToHashSet();
        List<SessaoPsicologica> Sess(PeriodoRelatorio q) => pronts.SelectMany(x => x.Sessoes).Where(s => q.Dentro(Local(s.CriadoEm))).ToList();
        var S = Sess(p);
        var sVis = S.Where(s => visiveis.Contains(s.ProntuarioPsicologicoId)).ToList();
        var comp = sVis.Count(s => s.Presenca == "compareceu");
        var ativos = pronts.Where(x => ProntuarioAtivoEm(x, p.Fim)).ToList();
        var ids = pronts.Select(x => x.AssistidaId).ToHashSet();
        var fila = d.Assistidas.Where(a => AguardandoPsicologia(a, d, p.Fim) && (!meus || ids.Contains(a.Id))).ToList();
        var abertosP = pronts.Where(x => x.Sessoes.Count > 0 && p.Dentro(Local(x.Sessoes.Min(s => s.CriadoEm)))).ToList();
        var esperas = abertosP.Where(x => d.PorId[x.AssistidaId].EncaminhadaPsicologiaEm is not null)
            .Select(x => Math.Max(0, Dias(Local(d.PorId[x.AssistidaId].EncaminhadaPsicologiaEm!.Value), Local(x.Sessoes.Min(s => s.CriadoEm))))).ToList();
        var encerr = pronts.Where(x => x.EncerradoEm is not null && p.Dentro(Local(x.EncerradoEm.Value))).ToList();
        var riscos = ativos.Select(x => x.RiscoAtual).ToList();

        var evo = new Dictionary<string, int> { ["Melhorou"] = 0, ["Ficou igual"] = 0, ["Piorou"] = 0 };
        foreach (var pr in pronts.Where(x => visiveis.Contains(x.Id)))
        {
            var ss = S.Where(s => s.ProntuarioPsicologicoId == pr.Id && !string.IsNullOrWhiteSpace(s.Risco)).OrderBy(s => s.CriadoEm).ToList();
            if (ss.Count < 2) continue;
            var dif = Array.IndexOf(OrdemRisco, ss[^1].Risco) - Array.IndexOf(OrdemRisco, ss[0].Risco);
            evo[dif < 0 ? "Melhorou" : dif > 0 ? "Piorou" : "Ficou igual"]++;
        }

        var r = new RelatorioResponse { Titulo = "Relatório Estatístico/Geral da Psicologia" + (meus ? " · só os meus prontuários" : "") };
        r.Kpis =
        [
            KpiN(S.Count, "Sessões registradas", ant is null ? null : Sess(ant).Count),
            Kpi(sVis.Count > 0 ? Pct(comp, sVis.Count) + "%" : "-", "Comparecimento", $"{comp} de {sVis.Count}"),
            Kpi(N(ativos.Count), "Em acompanhamento", "No fim do período"),
            Kpi(N(abertosP.Count), "Prontuários abertos"),
            Kpi(N(encerr.Count), "Acompanhamentos encerrados"),
            Kpi(N(fila.Count), "Na fila"),
            Kpi(esperas.Count > 0 ? Math.Round(esperas.Average()) + " dias" : "-", "Espera média na fila"),
            Kpi(N(riscos.Count(PsicologiaCatalogo.RiscoElevado)), "Com risco alto ou iminente", alerta: riscos.Any(PsicologiaCatalogo.RiscoElevado))
        ];

        if (pronts.Count != visiveis.Count)
        {
            r.Avisos.Add(new RelatorioAvisoDto
            {
                Tipo = "sigilo",
                Texto = $"{pronts.Count - visiveis.Count} prontuário(s) são só da psicóloga responsável. Deles entram apenas a contagem de sessões, a fila e o risco atual (o alerta já é da equipe); presença, modalidade, tipo, encaminhamentos e evolução do risco vêm só dos prontuários que você pode ver."
            });
        }

        var psicologas = d.Usuarios.Values.Where(x => x.Perfil == PerfisAcesso.Psicologia && (x.Ativo || S.Any(s => s.ProfissionalUserId == x.Id))).OrderBy(x => x.Nome).ToList();
        var duracoes = ativos.Select(x => x.Sessoes.Count(s => Local(s.CriadoEm) <= p.Fim)).ToList();
        var tempos = encerr.Where(x => x.Sessoes.Count > 0).Select(x => Dias(Local(x.Sessoes.Min(s => s.CriadoEm)), Local(x.EncerradoEm!.Value))).ToList();
        var (iniUtc, fimUtc) = FaixaUtc(p);

        r.Secoes.Add(new RelatorioSecaoDto
        {
            Blocos =
            [
                Colunas("Sessões por mês", NotaMes(p), PorMes(p, pronts.SelectMany(x => x.Sessoes).Select(s => Local(s.CriadoEm))), "azul"),
                new RelatorioBlocoDto
                {
                    Tipo = "pilha", Titulo = "Presença", Nota = "Prontuários que você pode ver.",
                    Itens = RotulosPresenca.Select(k => Item(k.Value, sVis.Count(s => s.Presenca == k.Key), CorPresenca(k.Key))).ToList(),
                    Extras = [Barras("Modalidade", null, Contar(sVis, s => RotulosModalidade.GetValueOrDefault(s.Modalidade ?? "", null!)))]
                },
                Barras("Tipo de atendimento", null, Contar(sVis, s => RotulosTipoSessao.GetValueOrDefault(s.TipoAtendimento, s.TipoAtendimento))),
                Barras("Risco atual", "Última avaliação de cada acompanhamento ativo.", OrdemRisco.Select(k => Item(RotuloRisco(k), riscos.Count(x => x == k), "risco_" + k))
                    .Append(Item("Sem avaliação", riscos.Count(x => string.IsNullOrWhiteSpace(x)), "neutro"))),
                Barras("Como o risco evoluiu", "Compara a primeira e a última avaliação de cada prontuário dentro do período (quem teve pelo menos 2).", evo.Select(k => Item(k.Key, k.Value))),
                Barras("Motivos de encerramento", $"{encerr.Count} encerramentos no período.", Contar(encerr.Where(x => visiveis.Contains(x.Id)), x => RotulosEncerramento.GetValueOrDefault(x.MotivoEncerramento ?? "", "Outro"))),
                Barras("Encaminhamentos externos", "CAPS, UBS, CRAS/CREAS, abrigo.", Contar(sVis, s => Separar(s.EncaminhamentosExternos, RotulosEncPsicologia))),
                Barras("Por onde entraram na Casa", "Das assistidas que abriram prontuário no período.", Contar(abertosP, x => RotuloOrigemPara(d.PorId[x.AssistidaId].Origem, PerfisAcesso.Psicologia))),
                Tabela("Por psicóloga", null, [Col("Psicóloga"), Col("CRP"), Col("Sessões", true), Col("Prontuários ativos", true)],
                    psicologas.Select(x => Linha(C(x.Nome), C(x.Crp ?? "-"), C(S.Count(s => s.ProfissionalUserId == x.Id)), C(ativos.Count(pr => pr.Responsaveis.Any(rr => rr.UserId == x.Id)))))),
                Kpis("Duração dos acompanhamentos", null,
                    Kpi(duracoes.Count > 0 ? N(Math.Round(duracoes.Average())) : "-", "Sessões em média por prontuário ativo"),
                    Kpi(tempos.Count > 0 ? Math.Round(tempos.Average()) + " dias" : "-", "Tempo médio até o encerramento"),
                    Kpi(N(d.EmergenciasPsicologia.Count(e => p.Dentro(Local(e.CriadoEm)) && pronts.Any(x => x.Id == e.ProntuarioPsicologicoId))), "Acessos de emergência"))
            ]
        });

        var linhas = ativos.Concat(encerr.Where(x => !ativos.Contains(x)))
            .Select(pr =>
            {
                var a = d.PorId[pr.AssistidaId];
                var noP = S.Count(s => s.ProntuarioPsicologicoId == pr.Id);
                var todas = pr.Sessoes.Where(s => Local(s.CriadoEm) <= p.Fim).OrderBy(s => s.CriadoEm).ToList();
                return (noP, Linha(
                    C(pr.Numero), C(a.Nome, sub: a.AcessoRestrito ? "Acesso restrito" : null),
                    C(pr.Responsaveis.Count == 0 ? "-" : string.Join(", ", pr.Responsaveis.Select(x => x.Nome))),
                    C(noP), C(todas.Count), C(todas.Count > 0 ? Fd(Local(todas[^1].CriadoEm)) : "-"),
                    string.IsNullOrWhiteSpace(pr.RiscoAtual) ? C("-") : C(RotuloRisco(pr.RiscoAtual), PsicologiaCatalogo.RiscoElevado(pr.RiscoAtual) ? "risco" : "neutro"),
                    C(pr.EncerradoEm is not null && Local(pr.EncerradoEm.Value) <= p.Fim ? "Encerrado" : "Em acompanhamento"),
                    pr.Visibilidade == ProntuarioVisibilidade.Equipe ? C("Equipe") : C("Só a responsável", "neutro")));
            })
            .OrderByDescending(x => x.noP).Select(x => x.Item2).ToList();
        r.Secoes.Add(new RelatorioSecaoDto
        {
            Blocos = [Tabela("Prontuários", "Ativos no fim do período e encerrados nele. Sem conteúdo das sessões.",
                [Col("Prontuário"), Col("Assistida"), Col("Responsável"), Col("Sessões no período", true), Col("Total", true), Col("Última sessão"), Col("Risco"), Col("Situação"), Col("Anotações")], linhas, true)]
        });

        return r;
    }

    // ================================================================== Professora

    private RelatorioResponse GeralProfessora(Dados d, PeriodoRelatorio p, PeriodoRelatorio? ant, UsuarioAtuante u, string? filtro)
    {
        var meus = CursosDaProfessora(u, d);
        var cs = int.TryParse(filtro, out var id) && meus.Contains(id) ? [id] : meus;
        var r = new RelatorioResponse();
        if (cs.Count == 0)
        {
            r.Titulo = "Relatório Estatístico/Geral das turmas";
            r.Avisos.Add(new RelatorioAvisoDto { Tipo = "info", Texto = "Nenhum curso vinculado ao seu perfil. Fale com a coordenação." });
            return r;
        }

        var nomes = string.Join(" e ", cs.Select(c => d.Cursos[c].Nome));
        r.Titulo = "Relatório Estatístico/Geral · " + nomes;
        var mats = d.Matriculas.Where(m => cs.Contains(m.Curso.Id) && !m.Assistida.AcessoRestrito).ToList();
        var idsOk = mats.Select(m => m.Assistida.Id).ToHashSet();
        List<RegistroAula> Regs(PeriodoRelatorio q) => d.Registros.Where(x => cs.Contains(x.CursoId) && q.Dentro(x.Data) && idsOk.Contains(x.AssistidaId)).ToList();
        var regs = Regs(p);
        var F = Freq(regs);
        var Fant = ant is null ? null : Freq(Regs(ant));
        var aulas = regs.GroupBy(x => (x.CursoId, x.Data)).OrderBy(g => g.Key.Data).ToList();
        var entraram = mats.Where(m => p.Dentro(m.Inicio)).ToList();
        var concluiram = mats.Where(m => m.Situacao == AssistidaCursoSituacao.Concluiu && p.Dentro(m.Fim)).ToList();
        var interromp = mats.Where(m => m.Situacao == AssistidaCursoSituacao.Interrompido && p.Dentro(m.Fim)).ToList();

        var porAluna = mats.Where(m => m.VinculadaNo(p) || (m.Situacao == AssistidaCursoSituacao.Aguardando && m.Inicio <= p.Fim)).Select(m =>
        {
            var rr = regs.Where(x => x.AssistidaId == m.Assistida.Id && x.CursoId == m.Curso.Id).ToList();
            var todos = d.Registros.Where(x => x.AssistidaId == m.Assistida.Id && x.CursoId == m.Curso.Id && x.Data <= p.Fim);
            return (m, f: Freq(rr), seq: SequenciaFaltas(todos));
        }).ToList();
        var alerta = porAluna.Where(x => x.m.Situacao == AssistidaCursoSituacao.Cursando && x.m.AtivaEm(p.Fim) && (x.seq >= 3 || (x.f.Total >= 4 && x.f.Pct < 60))).ToList();
        if (alerta.Count > 0)
        {
            r.Avisos.Add(new RelatorioAvisoDto
            {
                Tipo = "alerta",
                Titulo = $"{alerta.Count} aluna(s) para avaliar",
                Texto = "(3 ou mais faltas seguidas ou frequência abaixo de 60%): " + string.Join(", ", alerta.Select(x => x.m.Assistida.Nome + (x.m.Assistida.SemTelefoneSeguro || x.m.Assistida.ContatoSeguro == AssistidaContatoSeguro.NaoLigar ? " (não ligar)" : ""))) + ".",
                Nota = "Isso não significa abandono e não dispara contato automático: pode ser transporte, saúde ou segurança. Você e a equipe decidem como abordar, pelo canal e no horário seguros de cada uma."
            });
        }

        r.Kpis =
        [
            Kpi(N(mats.Count(m => m.AtivaEm(p.Fim))), "Alunas ativas", "No fim do período"),
            Kpi(N(entraram.Count), "Entraram na turma"),
            Kpi(N(concluiram.Count), "Concluíram", dadoNovo: true),
            Kpi(N(interromp.Count), "Interrupções", dadoNovo: true),
            Kpi(F.Total > 0 ? F.Pct + "%" : "-", "Frequência média", F.Total > 0 ? null : "Sem chamada no período", F.Pct, Fant is { Total: > 0 } && F.Total > 0 ? Fant.Pct : null, " p.p."),
            Kpi(N(aulas.Count), "Aulas com chamada"),
            Kpi(N(F.Justificada), "Faltas justificadas"),
            Kpi(N(F.Faltou), "Faltas sem justificativa")
        ];

        var diasSemana = new[] { "Domingo", "Segunda", "Terça", "Quarta", "Quinta", "Sexta", "Sábado" };
        var semChamada = AulasSemChamada(d, p, cs);
        var planos = d.Planos.Where(pl => cs.Contains(pl.CursoId)).ToList();
        var linhasAulas = aulas.Select(g => (Data: g.Key.Data, Curso: g.Key.CursoId, Linha: Linha(
                C(Fd(g.Key.Data)), C(d.Cursos[g.Key.CursoId].Nome), C(g.First().Tema ?? planos.FirstOrDefault(pl => pl.CursoId == g.Key.CursoId && pl.Data == g.Key.Data)?.Tema ?? "-"),
                C($"{g.Count(x => x.Status == "presente")}/{g.Count()}"),
                C(planos.FirstOrDefault(pl => pl.CursoId == g.Key.CursoId && pl.Data == g.Key.Data)?.Observacoes ?? "-"))))
            .Concat(semChamada.Select(x => (Data: x.Data, Curso: 0, Linha: Linha(C(Fd(x.Data)), C(x.Curso), C(planos.FirstOrDefault(pl => pl.Data == x.Data && d.Cursos[pl.CursoId].Nome == x.Curso)?.Tema ?? "-"), C("Chamada não lançada", "restrito"), C("-")))))
            .OrderByDescending(x => x.Data).Select(x => x.Linha).ToList();

        var obs = regs.Where(x => !string.IsNullOrWhiteSpace(x.Observacao) && x.Status != "justificada").OrderByDescending(x => x.Data).Take(25).ToList();

        r.Secoes.Add(new RelatorioSecaoDto
        {
            Blocos =
            [
                Colunas("Frequência aula a aula", "Percentual de presentes em cada aula do período.", aulas.TakeLast(26).Select(g => Item(Fdc(g.Key.Data), Pct(g.Count(x => x.Status == "presente"), g.Count()))), "ok", "%", true),
                new RelatorioBlocoDto
                {
                    Tipo = "pilha", Titulo = "Presenças e faltas", Nota = $"{regs.Count} registros de chamada.", Itens = PilhaFrequencia(F).ToList(),
                    Extras = [Barras("Justificativas mais comuns", null, Contar(regs.Where(x => x.Status == "justificada"), x => x.Observacao?.Trim()).Take(6))]
                },
                new RelatorioBlocoDto
                {
                    Tipo = "barras", Titulo = "Motivos de interrupção", DadoNovo = true,
                    Nota = $"{interromp.Count} no período. O motivo fica só com a professora (e em contagem, sem nomes, no ADM).",
                    Itens = Contar(interromp, m => m.Vinculo.MotivoSaida),
                    Vazio = "Nenhuma interrupção registrada no período.",
                    Extras = [Barras("Dia da semana com mais faltas", null, Contar(regs.Where(x => x.Status != "presente"), x => diasSemana[(int)x.Data.DayOfWeek] + (cs.Count > 1 ? " (" + d.Cursos[x.CursoId].Nome.Split(' ')[0] + ")" : "")))]
                },
                Tabela("Alunas: presenças, faltas e situação", "O histórico completo de cada uma fica na aba “Relatório individual da assistida”. Ordenado da menor para a maior frequência.",
                    [Col("Aluna"), Col("Curso"), Col("Entrou"), Col("Saiu / terminou", novo: true), Col("Situação"), Col("P", true), Col("J", true), Col("F", true), Col("Frequência", true), Col("Faltas seguidas", true)],
                    porAluna.OrderBy(x => x.f.Total == 0 ? 101 : x.f.Pct).Select(x =>
                    {
                        var sit = CelulaSituacaoCurso(x.m.Situacao);
                        sit.Sub = x.m.Vinculo.MotivoSaida;
                        return Linha(C(x.m.Assistida.Nome), C(x.m.Curso.Nome), C(Fd(x.m.Inicio)), C(x.m.Fim is not null && x.m.Fim <= p.Fim ? Fd(x.m.Fim) : "-"), sit,
                            C(x.f.Presente), C(x.f.Justificada), C(x.f.Faltou), C(x.f.Total > 0 ? x.f.Pct + "%" : "-"), C(N(x.seq), destaque: x.seq >= 3));
                    }), true),
                new RelatorioBlocoDto
                {
                    Tipo = "marcos", Titulo = "Observações da professora sobre as alunas", Nota = "Anotações feitas na chamada, por dia (as 25 mais recentes).",
                    Marcos = obs.Select(x => new RelatorioMarcoDto { Data = Iso(x.Data), Area = "curso", Texto = $"{d.PorId[x.AssistidaId].Nome}: {x.Observacao}" }).ToList(),
                    Vazio = "Sem observações no período."
                },
                Tabela("Aulas e plano de aula", "Tema e observações gerais de cada aula (do plano de aula salvo no servidor).", [Col("Data"), Col("Curso"), Col("Tema"), Col("Presentes", true), Col("Observações da aula")], linhasAulas)
            ]
        });

        return r;
    }
}
