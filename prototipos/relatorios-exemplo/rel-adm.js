/* ADM / Coordenação - Relatório Estatístico/Geral da Casa (sem identificar assistidas) */
document.addEventListener("DOMContentLoaded", function () {
    "use strict";
    const C = CASA, { fd, pct, n, novo, kpis, barras, colunas, pilha, tabela, bloco, badge, esc } = RU;
    const NOMES_ENC = { juridico: "Jurídico", psicologia: "Psicologia", assistente_social: "Assistência social" };
    // Critérios dos pontos de atenção: exemplos para a Casa ajustar, não regras da Casa
    const CRIT = { prazoFila: 10, faltasSeguidas: 3, diasSemAtualizacao: 30, ocupacaoBaixa: 40 };
    const COR_CURSO = "var(--rel-barra)", COR_ENC = "var(--rel-roxo)";

    function indicadores(p) {
        const novas = C.ASSISTIDAS.filter(a => C.dentro(a.entrada, p)).length;
        const ativas = new Set();
        EST.registros(p).forEach(r => { if (r.status === "presente") ativas.add(r.assistidaId); });
        C.JUR_ATEND.forEach(x => { if (C.dentro(x.data, p)) ativas.add(x.assistidaId); });
        C.PSI_SESS.forEach(x => { if (C.dentro(x.data, p) && x.presenca === "Compareceu") ativas.add(x.assistidaId); });
        C.REC_EVENTOS.forEach(x => { if (C.dentro(x.data, p)) ativas.add(x.assistidaId); });
        const f = EST.freq(EST.registros(p));
        const encs = EST.encaminhamentos(p);
        return {
            novas, ativas: ativas.size, freq: f.pct,
            jur: C.JUR_ATEND.filter(x => C.dentro(x.data, p)).length,
            psi: C.PSI_SESS.filter(x => C.dentro(x.data, p)).length,
            aulas: C.AULAS.filter(a => C.dentro(a.data, p)).length,
            fila: encs.filter(e => !e.semRegistro).reduce((s, e) => s + e.aguardando, 0),
            encs
        };
    }

    function render(p) {
        const ant = C.anterior(p);
        const I = indicadores(p), A = ant ? indicadores(ant) : null;
        let h = `<div class="rel-cabecalho"><h2>Relatório Estatístico/Geral da Casa</h2><span class="rel-periodo">${esc(p.rotulo)} · ${fd(p.ini)} a ${fd(p.fim)}${ant ? " · comparado com " + esc(ant.rotulo.toLowerCase()) : ""}</span></div>`;

        // 1. resumo
        h += kpis([
            { valor: n(I.novas), valorNum: I.novas, anterior: A && A.novas, rotulo: "Novas assistidas" },
            { valor: n(I.ativas), valorNum: I.ativas, anterior: A && A.ativas, rotulo: "Assistidas atendidas", titulo: "Tiveram presença em aula, atendimento jurídico, sessão de psicologia ou passaram na Recepção" },
            { valor: n(I.jur), valorNum: I.jur, anterior: A && A.jur, rotulo: "Atendimentos jurídicos" },
            { valor: n(I.psi), valorNum: I.psi, anterior: A && A.psi, rotulo: "Sessões de psicologia" },
            { valor: n(I.aulas), valorNum: I.aulas, anterior: A && A.aulas, rotulo: "Aulas dadas" },
            { valor: I.freq + "%", valorNum: I.freq, anterior: A && A.freq, sufixoDelta: " p.p.", rotulo: "Frequência média nos cursos" },
            { valor: n(I.fila), rotulo: "Aguardando 1º atendimento", ajuda: "Jurídico e Psicologia, no fim do período" },
            { valor: n(C.ASSISTIDAS.filter(a => a.restrito && a.entrada <= p.fim).length), rotulo: "Casos com acesso restrito", ajuda: "Total na Casa" }
        ]);

        // 2. procura (o pedido principal)
        const pc = EST.procuraCursos(p);
        const ativos = pc.filter(x => x.curso.ativo || x.procura);
        const tudo = pc.filter(x => x.curso.ativo || x.procura).map(x => ({ rotulo: x.curso.nome, valor: x.procura, tipo: "Curso" }))
            .concat(I.encs.map(e => ({ rotulo: e.nome, valor: e.encaminhadas, tipo: "Encaminhamento" })))
            .sort((a, b) => b.valor - a.valor);
        const mais = tudo.slice(0, 5), menos = tudo.slice().reverse().slice(0, 5);
        const totalNovas = I.novas || 1;
        h += `<div class="rel-grade">` + bloco("O que as assistidas mais e menos procuraram", "Cursos e encaminhamentos escolhidos no direcionamento (Recepção, Jurídico ou Psicologia) dentro do período. Uma assistida pode escolher mais de um.",
            `<div class="rel-podio"><div class="rel-podio-card"><h4>Mais procurados</h4><ol>${mais.map(x => `<li><b>${esc(x.rotulo)}</b>: ${n(x.valor)} <small>(${x.tipo.toLowerCase()})</small></li>`).join("")}</ol></div>
             <div class="rel-podio-card rel-podio-card--menos"><h4>Menos procurados</h4><ol>${menos.map(x => `<li><b>${esc(x.rotulo)}</b>: ${n(x.valor)} <small>(${x.tipo.toLowerCase()})</small></li>`).join("")}</ol></div></div>` +
            barras(tudo.map(x => ({ rotulo: x.rotulo, valor: x.valor, cor: x.tipo === "Curso" ? COR_CURSO : COR_ENC, extra: x.valor ? pct(x.valor, totalNovas) + "%" : "" })), {}) +
            `<div class="rel-legenda"><span><i style="background:${COR_CURSO}"></i>Curso</span><span><i style="background:${COR_ENC}"></i>Encaminhamento</span><span>% = em relação às novas assistidas do período</span></div>`, true);

        // 3. tabela de cursos
        h += bloco("Cursos: procura, ocupação e resultado", "Ocupação = alunas ativas no fim do período ÷ vagas. Taxa de interrupção = interrupções no período ÷ todas as alunas que estiveram na turma no período (inclui quem continua cursando).",
            tabela([{ t: "Curso" }, { t: "Professora" }, { t: "Procura", num: true }, { t: "Ativas", num: true }, { t: "Vagas" + novo("novo"), num: true }, { t: "Ocupação", num: true }, { t: "Concluíram", num: true }, { t: "Interrupções", num: true }, { t: "Taxa de interrupção", num: true }, { t: "Frequência", num: true }, { t: "Aulas", num: true }],
                pc.slice().sort((a, b) => b.procura - a.procura).map(x => [esc(x.curso.nome) + (x.curso.ativo ? "" : " " + badge("Desativado")), esc(C.func(x.curso.prof).nome), x.procura, x.ativas, x.curso.vagas, x.curso.ativo ? pct(x.ativas, x.curso.vagas) + "%" : "-", x.concluiram, x.desistiram, x.vinculadas ? pct(x.desistiram, x.vinculadas) + "% de " + x.vinculadas : "-", x.freq.total ? x.freq.pct + "%" : "-", x.aulas])), true);

        // 4. encaminhamentos
        h += bloco("Encaminhamentos: procura e espera", "Aguardando = encaminhadas que ainda não tiveram o primeiro atendimento no fim do período.",
            tabela([{ t: "Área" }, { t: "Encaminhadas", num: true }, { t: "1º atendimento no período", num: true }, { t: "Aguardando", num: true }, { t: "Espera média", num: true }, { t: "Espera mais longa", num: true }],
                I.encs.map(e => e.semRegistro ? [e.nome, e.encaminhadas, "sem registro", e.aguardando + " sem confirmação", "-", "-"] : [e.nome, e.encaminhadas, e.atendidas, e.aguardando, e.esperaMedia != null ? e.esperaMedia + " dias" : "-", e.maisAntiga ? e.maisAntiga + " dias" : "-"])) +
            `<div class="rel-sigilo" style="margin-top:14px"><b>Assistência social:</b> encaminhamentos sem confirmação de atendimento no sistema. O perfil ainda não existe, então a falta de registro não prova que a assistida não foi atendida. Por isso não entra na espera nem no total aguardando.</div>`, true);

        const novasP = C.ASSISTIDAS.filter(a => C.dentro(a.entrada, p));
        const combos = EST.contar(novasP, a => {
            const partes = [];
            if (a.cursosDirecionados.some(c => c.data === a.entrada)) partes.push("Curso");
            ["juridico", "psicologia", "assistente_social"].forEach(t => { if (a.encaminhamentos.some(e => e.tipo === t && e.data === a.entrada)) partes.push(NOMES_ENC[t]); });
            return partes.join(" + ") || "Sem direcionamento na entrada";
        });
        h += bloco("Combinações mais comuns na chegada", "O que cada nova assistida escolheu já no primeiro direcionamento.", barras(combos.slice(0, 8), { }));
        h += bloco("Por onde entraram", "Quem fez o cadastro (a ficha é única para a Casa).", barras(EST.contar(novasP, a => ({ recepcao: "Recepção", juridico: "Direto no Jurídico", psicologia: "Direto na Psicologia" })[a.origem])));
        h += `</div>`;

        // 5. evolução mensal
        h += `<div class="rel-grade" style="margin-top:1.4rem">`;
        const notaMes = p.tipo === "mensal" ? "Últimos 6 meses até o mês escolhido." : "Mês a mês dentro do período.";
        h += bloco("Novas assistidas por mês", notaMes, colunas(EST.porMes(p, C.ASSISTIDAS, "entrada")));
        h += bloco("Atendimentos jurídicos por mês", notaMes, colunas(EST.porMes(p, C.JUR_ATEND, "data"), { cor: "var(--rel-roxo)" }));
        h += bloco("Sessões de psicologia por mês", notaMes, colunas(EST.porMes(p, C.PSI_SESS, "data"), { cor: "var(--rel-azul)" }));
        h += bloco("Presenças em aula por mês", notaMes, colunas(EST.porMes(p, EST.registros(null).filter(r => r.status === "presente"), "data"), { cor: "var(--rel-ok)" }));
        h += `</div>`;

        // 6. perfil
        h += `<div class="rel-grade" style="margin-top:1.4rem">`;
        h += bloco("Faixa etária na chegada", novasP.length + " novas assistidas no período.", barras(EST.faixaEtaria(novasP)));
        h += bloco("Onde moram", "Cidade e bairros mais frequentes (endereços sigilosos ficam fora).", barras(EST.contar(novasP.filter(a => !a.enderecoSigiloso), a => a.cidade.split("/")[0])) + `<div style="margin-top:14px">${barras(EST.contar(novasP.filter(a => !a.enderecoSigiloso && a.cidade.startsWith("Itaqua")), a => a.bairro).slice(0, 8))}</div>`);
        h += bloco("Contato e segurança", "Como a Casa pode falar com elas.", pilha([
            { rotulo: "Pode ligar", valor: novasP.filter(a => a.contato === "Pode ligar").length, cor: "var(--rel-ok)" },
            { rotulo: "Só no horário combinado", valor: novasP.filter(a => a.contato === "Só no horário combinado").length, cor: "var(--rel-justificada)" },
            { rotulo: "Não ligar", valor: novasP.filter(a => a.contato === "Não ligar").length, cor: "var(--rel-falta)" }]) +
            `<p style="margin:14px 0 0;font-size:0.88rem;color:var(--rel-tinta)">Sem telefone seguro: <b>${novasP.filter(a => a.semTelefoneSeguro).length}</b> · Endereço sigiloso: <b>${novasP.filter(a => a.enderecoSigiloso).length}</b></p>`);
        const areas = {};
        const marca = (id, ar) => { (areas[id] = areas[id] || new Set()).add(ar); };
        EST.registros(p).forEach(r => { if (r.status === "presente") marca(r.assistidaId, "curso"); });
        C.JUR_ATEND.forEach(x => { if (C.dentro(x.data, p)) marca(x.assistidaId, "jur"); });
        C.PSI_SESS.forEach(x => { if (C.dentro(x.data, p) && x.presenca === "Compareceu") marca(x.assistidaId, "psi"); });
        const qtdAreas = Object.values(areas).map(st => st.size);
        h += bloco("Em quantas frentes cada uma é atendida", "Assistidas com presença em curso, atendimento jurídico ou sessão no período (" + qtdAreas.length + ").", barras([1, 2, 3].map(k => ({ rotulo: k === 1 ? "1 frente" : k + " frentes", valor: qtdAreas.filter(x => x === k).length }))) + `<p class="rel-nota" style="margin-top:12px">Frentes: cursos, Jurídico e Psicologia. Ajuda a ver quem depende de vários serviços da Casa ao mesmo tempo.</p>`);
        h += `</div>`;

        // 7. jurídico em números
        const atsP = C.JUR_ATEND.filter(x => C.dentro(x.data, p));
        const casosNovos = C.JUR_CASOS.filter(c => C.dentro(c.abertoEm, p));
        const sitFim = C.JUR_CASOS.filter(c => c.abertoEm <= p.fim).map(c => { const l = C.JUR_ATEND.filter(x => x.casoId === c.id && x.data <= p.fim); return l.length ? l[l.length - 1].situacao : null; });
        h += `<h3 style="color:#8B586F;margin:2rem 0 0.8rem">Jurídico em números</h3><div class="rel-grade">`;
        h += bloco("Tipos de demanda (casos novos)", casosNovos.length + " casos abertos no período.", barras(EST.contar(casosNovos, c => c.demanda)));
        h += bloco("Situação dos casos no fim do período", "Todos os casos já abertos até o fim do período.", pilha([
            { rotulo: "Em andamento", valor: sitFim.filter(s => s === "Em andamento").length, cor: "var(--rel-roxo)" },
            { rotulo: "Aguardando documento", valor: sitFim.filter(s => s === "Aguardando documento").length, cor: "var(--rel-justificada)" },
            { rotulo: "Encerrado", valor: sitFim.filter(s => s === "Encerrado").length, cor: "var(--rel-neutro)" }]) +
            `<p style="margin:14px 0 0;font-size:0.88rem">Atendimentos com medida protetiva: <b>${atsP.filter(x => x.medidaProtetiva === "Sim").length}</b> de ${atsP.length} · Adendos: <b>${atsP.reduce((s, x) => s + x.adendos.length, 0)}</b></p>`);
        h += bloco("Encaminhamentos externos do Jurídico", "", barras(EST.contar(atsP, x => x.encaminhamentos)));
        h += bloco("Atendimentos por profissional", "Só a contagem.", barras(EST.contar(atsP, x => C.func(x.profissional).nome)));
        h += `</div>`;

        // 8. psicologia em números
        const sP = C.PSI_SESS.filter(x => C.dentro(x.data, p));
        const prontAtivos = C.PSI_PRONT.filter(x => x.abertura <= p.fim && (!x.encerramento || x.encerramento.data > p.fim));
        h += `<h3 style="color:#8B586F;margin:2rem 0 0.8rem">Psicologia em números <span style="font-size:0.8rem;color:var(--rel-tinta-3);font-weight:600">(sem nomes, como a Coordenação já vê hoje)</span></h3><div class="rel-grade">`;
        h += bloco("Acompanhamentos", "", kpis([
            { valor: prontAtivos.length, rotulo: "Em acompanhamento" },
            { valor: C.PSI_PRONT.filter(x => C.dentro(x.abertura, p)).length, rotulo: "Prontuários abertos" },
            { valor: C.PSI_PRONT.filter(x => x.encerramento && C.dentro(x.encerramento.data, p)).length, rotulo: "Encerrados" }
        ]));
        h += bloco("Presença nas sessões", sP.length + " sessões no período.", pilha([
            { rotulo: "Compareceu", valor: sP.filter(s => s.presenca === "Compareceu").length, cor: "var(--rel-ok)" },
            { rotulo: "Faltou e justificou", valor: sP.filter(s => s.presenca === "Faltou e justificou").length, cor: "var(--rel-justificada)" },
            { rotulo: "Faltou", valor: sP.filter(s => s.presenca === "Faltou").length, cor: "var(--rel-falta)" },
            { rotulo: "Remarcou", valor: sP.filter(s => s.presenca === "Remarcou").length, cor: "var(--rel-neutro)" }]) + `<div style="margin-top:14px">${barras(EST.contar(sP, s => s.modalidade))}</div>`);
        h += bloco("Risco clínico", "", `<div class="rel-sigilo">A classificação de risco não aparece para o ADM. Ela chega à equipe técnica pelo alerta na ficha, na hora em que é registrada (uma urgência não pode depender da leitura de um relatório). Se a coordenação técnica precisar desse número, cria-se uma permissão específica para ela.</div>`);
        h += bloco("Motivos de encerramento", "", barras(EST.contar(C.PSI_PRONT.filter(x => x.encerramento && C.dentro(x.encerramento.data, p)), x => x.encerramento.motivo)) + `<div style="margin-top:14px"><p class="rel-nota">Encaminhamentos externos</p>${barras(EST.contar(sP, s => s.encaminhamentos))}</div>`);
        h += `</div>`;

        // 9. cursos: frequência e interrupções
        const regsP = EST.registros(p);
        const desist = C.MATRICULAS.filter(m => m.situacao === "desistiu" && C.dentro(m.fim, p));
        const alerta3 = C.MATRICULAS.filter(m => m.situacao === "cursando" && m.inicio <= p.fim).filter(m => {
            const r = EST.registros({ ini: m.inicio, fim: p.fim }, { assistidaId: m.assistidaId, cursoIds: [m.cursoId] }).sort((a, b) => a.data.localeCompare(b.data));
            return EST.sequenciaFaltas(r) >= CRIT.faltasSeguidas;
        });
        h += `<h3 style="color:#8B586F;margin:2rem 0 0.8rem">Cursos: frequência e interrupções</h3><div class="rel-grade">`;
        h += bloco("Frequência por curso", "Presenças ÷ chamadas registradas no período.", barras(pc.filter(x => x.freq.total).sort((a, b) => b.freq.pct - a.freq.pct).map(x => ({ rotulo: x.curso.nome, valor: x.freq.pct, extra: x.freq.total + " chamadas" })), { sufixo: "%", max: 100 }));
        h += bloco("Faltas no período", "", pilha([
            { rotulo: "Presente", valor: regsP.filter(r => r.status === "presente").length, cor: "var(--rel-ok)" },
            { rotulo: "Justificada", valor: regsP.filter(r => r.status === "justificada").length, cor: "var(--rel-justificada)" },
            { rotulo: "Faltou", valor: regsP.filter(r => r.status === "faltou").length, cor: "var(--rel-falta)" }]) +
            `<div style="margin-top:14px"><p class="rel-nota">Justificativas mais comuns</p>${barras(EST.contar(regsP.filter(r => r.status === "justificada"), r => r.observacao).slice(0, 6))}</div>`);
        h += bloco("Motivos de interrupção" + novo(), desist.length + " interrupções no período. Só a contagem por motivo, sem nomes.", barras(EST.contar(desist, m => m.motivoSaida)));
        h += bloco("Faltas seguidas (para avaliação)", `Cursando e com ${CRIT.faltasSeguidas} ou mais faltas seguidas até o fim do período. Isso pede avaliação da professora e da equipe: não é conclusão de abandono e não gera contato automático (pode ser transporte, saúde ou segurança).`, kpis([{ valor: alerta3.length, rotulo: `Alunas com ${CRIT.faltasSeguidas}+ faltas seguidas` }]) + barras(EST.contar(alerta3, m => C.curso(m.cursoId).nome)));
        h += `</div>`;

        // 10. equipe
        const linhasEq = C.EQUIPE.filter(f => f.perfil !== "adm").map(f => {
            let v = 0, desc = "";
            if (f.perfil === "recepcao") { v = C.REC_EVENTOS.filter(e => e.por === f.id && C.dentro(e.data, p)).length; desc = "ações na ficha"; }
            if (f.perfil === "juridico") { v = atsP.filter(x => x.profissional === f.id).length; desc = "atendimentos"; }
            if (f.perfil === "psicologia") { v = sP.filter(x => x.psicologa === f.id).length; desc = "sessões"; }
            if (f.perfil === "professor") { v = C.AULAS.filter(a => a.professora === f.id && C.dentro(a.data, p)).length; desc = "aulas (" + C.CURSOS.filter(c => c.prof === f.id && c.ativo).length + " cursos ativos)"; }
            return [esc(f.nome), `<span class="rel-nowrap">${esc(f.id)}</span>`, ({ recepcao: "Recepção", juridico: "Jurídico", psicologia: "Psicologia", professor: "Professora" })[f.perfil], n(v), desc];
        });
        const aud = C.AUDITORIA.filter(x => C.dentro(x.data, p));
        h += `<h3 style="color:#8B586F;margin:2rem 0 0.8rem">Equipe, sigilo e segurança</h3><div class="rel-grade">`;
        h += bloco("Trabalho por profissional", "Contagens do período, sem conteúdo.", tabela([{ t: "Profissional" }, { t: "ID" }, { t: "Perfil" }, { t: "Total", num: true }, { t: "O quê" }], linhasEq));
        h += bloco("Sigilo e segurança", "Vem do histórico de ações (auditoria).", tabela([{ t: "Item" }, { t: "No período", num: true }], [
            ["Casos marcados com acesso restrito (total)", C.ASSISTIDAS.filter(a => a.restrito && a.entrada <= p.fim).length],
            ["Acessos de emergência no Jurídico", aud.filter(x => x.tipo === "Acesso de emergência (Jurídico)").length],
            ["Acessos de emergência na Psicologia", aud.filter(x => x.tipo === "Acesso de emergência (Psicologia)").length],
            ["Termos de sigilo assinados", aud.filter(x => x.tipo === "Termo de sigilo assinado").length],
            ["Responsabilidades passadas", aud.filter(x => x.tipo === "Passar responsabilidades").length],
            ["Fichas abertas", n(aud.filter(x => x.tipo === "Ficha aberta").length)],
            ["Logins", n(aud.filter(x => x.tipo === "Login").length)]
        ]) + `<div style="margin-top:14px"><p class="rel-nota">Casos restritos por motivo</p>${barras(EST.contar(C.ASSISTIDAS.filter(a => a.restrito && a.entrada <= p.fim), a => a.restrito))}</div>`);
        h += `</div>`;

        // 11. pontos de atenção: até 5, cada um com o que aconteceu, o tamanho e quem pode agir
        const pontos = [];
        const ponto = (titulo, oque, quem) => { const [q, acao] = quem.split(": "); pontos.push(`<li><b>${titulo}:</b> ${oque} <span class="rel-ponto-quem">Quem age: ${q}. Ação: ${acao}</span></li>`); };
        const filaAcima = ["juridico", "psicologia"].map(k => {
            const f = k === "juridico" ? C.JUR_FILA : C.PSI_FILA;
            const ag = f.filter(x => x.encaminhadaEm <= p.fim && (!x.atendidaEm || x.atendidaEm > p.fim) && !(x.canceladoEm && x.canceladoEm <= p.fim));
            const dias = ag.map(x => C.diffDias(x.encaminhadaEm, p.fim));
            return { nome: NOMES_ENC[k], total: ag.length, acima: dias.filter(d => d > CRIT.prazoFila).length, maior: dias.length ? Math.max(...dias) : 0 };
        }).filter(x => x.acima > 0).sort((a, b) => b.maior - a.maior);
        if (filaAcima.length) ponto("Fila e demora", filaAcima.map(x => `${x.nome}: ${x.total} aguardando 1º atendimento, ${x.acima} acima de ${CRIT.prazoFila} dias; a mais antiga há ${x.maior} dias.`).join(" "), "Coordenação com a equipe da área: rever agenda e capacidade.");
        const jurSemAg = I.encs.find(e => e.chave === "juridico").semAgendamento;
        const vencidos = C.JUR_CASOS.filter(c => c.abertoEm <= p.fim).map(c => { const l = C.JUR_ATEND.filter(x => x.casoId === c.id && x.data <= p.fim); return l[l.length - 1]; })
            .filter(u => u && u.situacao !== "Encerrado" && u.retorno && C.diffDias(u.retorno, p.fim) > CRIT.diasSemAtualizacao).length;
        if (jurSemAg || vencidos) ponto("Encaminhamentos sem andamento", `${jurSemAg ? jurSemAg + " encaminhamento(s) ao Jurídico sem agendamento" : ""}${jurSemAg && vencidos ? "; " : ""}${vencidos ? vencidos + " caso(s) do Jurídico com retorno vencido há mais de " + CRIT.diasSemAtualizacao + " dias sem novo registro" : ""}.`, "Coordenação do Jurídico: distribuir e conferir as pendências.");
        const lotados = pc.filter(x => x.curso.ativo && x.ativas >= x.curso.vagas && x.procura > 0);
        const ociosos = pc.filter(x => x.curso.ativo && pct(x.ativas, x.curso.vagas) < CRIT.ocupacaoBaixa);
        if (lotados.length || ociosos.length) ponto("Capacidade dos cursos", `${lotados.length ? "Turma cheia e ainda com procura no período: " + lotados.map(x => esc(x.curso.nome) + " (" + x.ativas + "/" + x.curso.vagas + ", " + x.procura + " novas)").join(", ") + ". " : ""}${ociosos.length ? "Menos de " + CRIT.ocupacaoBaixa + "% das vagas ocupadas: " + ociosos.map(x => esc(x.curso.nome) + " (" + x.ativas + "/" + x.curso.vagas + ")").join(", ") + "." : ""}`, "Coordenação com as professoras: ajustar vagas, divulgação ou oferta.");
        if (alerta3.length || desist.length) ponto("Interrupção da participação", `${alerta3.length} aluna(s) com ${CRIT.faltasSeguidas} ou mais faltas seguidas e ${desist.length} interrupção(ões) registrada(s) no período.`, "Professora responsável e equipe: avaliar o acompanhamento, pelo canal e no horário seguros de cada uma (não é abandono nem gera contato automático).");
        const semChamada = C.AULAS.filter(a => a.semChamada && C.dentro(a.data, p));
        if (semChamada.length) ponto("Qualidade dos registros", `${semChamada.length} aula(s) sem chamada lançada (${[...new Set(semChamada.map(a => C.curso(a.cursoId).nome))].map(esc).join(", ")}). Frequência e faltas deste relatório podem estar incompletas.`, "Professoras: lançar a chamada antes de qualquer decisão baseada nesses números.");
        const social = I.encs.find(e => e.chave === "assistente_social");
        const notaSocial = social.aguardando ? `<p class="rel-nota" style="margin:10px 0 0">Assistência social: ${social.aguardando} encaminhamento(s) sem confirmação de atendimento no sistema. Não entra nos destaques porque ausência de registro não prova ausência de atendimento.</p>` : "";
        const criterios = `<div class="rel-criterios rel-nao-imprimir">Critérios (exemplos, configuráveis pela Casa):
            <label>prazo da fila <input type="number" min="1" data-criterio="prazoFila" value="${CRIT.prazoFila}"> dias</label>
            <label>faltas seguidas <input type="number" min="1" data-criterio="faltasSeguidas" value="${CRIT.faltasSeguidas}"></label>
            <label>retorno vencido há <input type="number" min="1" data-criterio="diasSemAtualizacao" value="${CRIT.diasSemAtualizacao}"> dias</label>
            <label>ocupação baixa abaixo de <input type="number" min="1" max="100" data-criterio="ocupacaoBaixa" value="${CRIT.ocupacaoBaixa}">%</label></div>`;
        const corpoPontos = pontos.length ? `<ul class="rel-pontos">${pontos.slice(0, 5).join("")}</ul>` : `<p style="margin:8px 0 0">Nenhum alerta identificado pelos critérios configurados.</p>`;
        h = h.replace(`<div class="rel-kpis">`, `<div class="soft-alert soft-alert--info" style="margin-bottom:1.2rem"><b>Pontos de atenção do período</b> <span class="rel-nota">(até 5, sem nomes e sem conteúdo de atendimentos)</span>${corpoPontos}${notaSocial}${criterios}</div><div class="rel-kpis">`);
        return h;
    }

    paginaRelatorios({
        perfil: "adm",
        usuario: { id: "ADM-000001", nome: "Coordenação Geral", perfilNome: "ADM / Coordenação", email: "coordenacao@casamulher.local" },
        renderArea: render,
        aoMudarCriterio: (k, v) => { if (v > 0) CRIT[k] = v; },
        assistidaInicial: 1
    });
});
