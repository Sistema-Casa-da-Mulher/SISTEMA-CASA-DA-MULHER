/* Jurídico - Relatório Estatístico/Geral da área */
document.addEventListener("DOMContentLoaded", function () {
    "use strict";
    const C = CASA, { fd, pct, n, kpis, barras, colunas, pilha, tabela, bloco, badge, esc } = RU;
    const U = { id: "JUR-000003", nome: "Jurídico Teste", perfilNome: "Jurídico", email: "juridico.teste@casamulher.local" };
    const estado = { escopo: "todos" };

    function render(p) {
        const ant = C.anterior(p);
        const meus = estado.escopo === "meus";
        const casoOk = c => !meus || c.responsaveis.includes(U.id);
        const casosIds = new Set(C.JUR_CASOS.filter(casoOk).map(c => c.id));
        const atsQ = q => C.JUR_ATEND.filter(x => casosIds.has(x.casoId) && C.dentro(x.data, q));
        const ats = atsQ(p), atsAnt = ant ? atsQ(ant) : null;
        const casos = C.JUR_CASOS.filter(casoOk);
        const casosNovos = casos.filter(c => C.dentro(c.abertoEm, p));
        const ultimoAte = (c, data) => { const l = C.JUR_ATEND.filter(x => x.casoId === c.id && x.data <= data); return l[l.length - 1]; };
        const sitFim = casos.filter(c => c.abertoEm <= p.fim).map(c => ultimoAte(c, p.fim).situacao);
        const fila = C.JUR_FILA.filter(f => f.encaminhadaEm <= p.fim && (!f.atendidaEm || f.atendidaEm > p.fim));
        const atendP = C.JUR_FILA.filter(f => C.dentro(f.atendidaEm, p));
        const espera = atendP.length ? Math.round(atendP.reduce((s, f) => s + C.diffDias(f.encaminhadaEm, f.atendidaEm), 0) / atendP.length) : null;
        const retornos7 = casos.map(c => ultimoAte(c, C.HOJE)).filter(x => x && x.retorno && x.retorno >= C.HOJE && x.retorno <= C.addDias(C.HOJE, 7)).length;
        const encerradosP = casos.filter(c => { const l = C.JUR_ATEND.filter(x => x.casoId === c.id); const u = l[l.length - 1]; return u.situacao === "Encerrado" && C.dentro(u.data, p); }).length;

        let h = `<div class="rel-cabecalho"><h2>Relatório Estatístico/Geral do Jurídico${meus ? " · só os meus casos" : ""}</h2><span class="rel-periodo">${esc(p.rotulo)} · ${fd(p.ini)} a ${fd(p.fim)}</span></div>`;
        h += kpis([
            { valor: n(ats.length), valorNum: ats.length, anterior: atsAnt && atsAnt.length, rotulo: "Atendimentos (protocolos)" },
            { valor: n(new Set(ats.map(x => x.assistidaId)).size), rotulo: "Assistidas atendidas" },
            { valor: n(casosNovos.length), rotulo: "Casos novos" },
            { valor: n(sitFim.filter(s => s !== "Encerrado").length), rotulo: "Casos em andamento", ajuda: "No fim do período (inclui aguardando documento)" },
            { valor: n(encerradosP), rotulo: "Casos encerrados" },
            { valor: n(fila.length), rotulo: "Na fila", ajuda: "Encaminhadas sem 1º atendimento" },
            { valor: espera != null ? espera + " dias" : "-", rotulo: "Espera média na fila" },
            { valor: n(retornos7), rotulo: "Retornos nos próximos 7 dias", ajuda: "Contado a partir de hoje" }
        ]);
        h += `<div class="rel-grade">`;
        h += bloco("Atendimentos por mês", p.tipo === "mensal" ? "Últimos 6 meses até o mês escolhido." : "Mês a mês dentro do período.", colunas(EST.porMes(p, C.JUR_ATEND.filter(x => casosIds.has(x.casoId)), "data"), { cor: "var(--rel-roxo)" }));
        h += bloco("Tipo de demanda", "Atendimentos do período por tipo.", barras(EST.contar(ats, x => x.tipoDemanda)));
        h += bloco("Situação dos casos", "No fim do período, de todos os casos já abertos.", pilha([
            { rotulo: "Em andamento", valor: sitFim.filter(s => s === "Em andamento").length, cor: "var(--rel-roxo)" },
            { rotulo: "Aguardando documento", valor: sitFim.filter(s => s === "Aguardando documento").length, cor: "var(--rel-justificada)" },
            { rotulo: "Encerrado", valor: sitFim.filter(s => s === "Encerrado").length, cor: "var(--rel-neutro)" }]));
        h += bloco("Medida protetiva", "Resposta à pergunta “Existe medida protetiva?” nos atendimentos do período.", barras(EST.contar(ats, x => x.medidaProtetiva)));
        h += bloco("Encaminhamentos externos", "Para onde o Jurídico encaminhou.", barras(EST.contar(ats, x => x.encaminhamentos)));
        h += bloco("De onde vieram", "Quem encaminhou ao Jurídico as assistidas que tiveram 1º atendimento no período.", barras(EST.contar(atendP, f => { const pf = C.func(f.por).perfil; return pf === "recepcao" ? "Recepção" : pf === "psicologia" ? "Psicologia" : "Cadastro direto no Jurídico"; })));
        h += bloco("Atendimentos por profissional", "", barras(EST.contar(ats, x => C.func(x.profissional).nome)));
        h += bloco("Sigilo", "", tabela([{ t: "Item" }, { t: "Quantidade", num: true }], [
            ["Casos “somente as responsáveis”", casos.filter(c => c.visibilidade === "responsaveis" && c.abertoEm <= p.fim).length],
            ["Casos com acesso restrito", casos.filter(c => C.assistida(c.assistidaId).restrito && c.abertoEm <= p.fim).length],
            ["Casos com mais de uma responsável", casos.filter(c => c.responsaveis.length > 1 && c.abertoEm <= p.fim).length],
            ["Adendos no período", ats.reduce((s, x) => s + x.adendos.length, 0)],
            ["Acessos de emergência no período", C.AUDITORIA.filter(x => x.tipo === "Acesso de emergência (Jurídico)" && C.dentro(x.data, p)).length],
            ["Assistidas também em acompanhamento na Psicologia", new Set(ats.map(x => x.assistidaId).filter(id => C.PSI_PRONT.some(pr => pr.assistidaId === id && !pr.encerramento))).size],
            ["Com alerta de risco alto/iminente hoje", new Set(ats.map(x => x.assistidaId).filter(id => { const r = EST.riscoAtual(id); const pr = C.PSI_PRONT.find(q => q.assistidaId === id); return pr && !pr.encerramento && (r === "Alto" || r === "Iminente"); })).size]
        ]));
        h += bloco("Processos", "", kpis([{ valor: casos.filter(c => c.processo && c.abertoEm <= p.fim).length, rotulo: "Casos com nº de processo" }, { valor: casos.filter(c => !c.processo && c.abertoEm <= p.fim).length, rotulo: "Sem processo (orientação)" }]) + barras(EST.contar(casos.filter(c => c.vara && c.abertoEm <= p.fim), c => c.vara)));

        // lista de casos com movimento no período
        const idsP = [...new Set(ats.map(x => x.casoId))];
        const linhas = idsP.map(id => C.JUR_CASOS.find(c => c.id === id)).map(c => {
            const a = C.assistida(c.assistidaId), l = C.JUR_ATEND.filter(x => x.casoId === c.id), u = l[l.length - 1];
            const podeVer = c.visibilidade === "equipe" || c.responsaveis.includes(U.id);
            return [esc(a.nome) + (a.restrito ? " " + badge("Restrito", "restrito") : ""), esc(c.demanda), l.filter(x => C.dentro(x.data, p)).length, l.length, u.protocolo, esc(u.situacao), u.retorno ? fd(u.retorno) : "-", c.responsaveis.map(r => esc(C.func(r).nome)).join(", "), podeVer ? "Equipe" : badge("Só responsáveis")];
        }).sort((x, y) => y[2] - x[2]);
        h += bloco("Casos com atendimento no período", "A lista mostra só metadados. O conteúdo de cada caso fica no relatório individual.", tabela([{ t: "Assistida" }, { t: "Demanda" }, { t: "No período", num: true }, { t: "Total", num: true }, { t: "Último protocolo" }, { t: "Situação" }, { t: "Retorno" }, { t: "Responsáveis" }, { t: "Quem vê" }], linhas), true);
        h += `</div>`;
        return h;
    }

    paginaRelatorios({
        perfil: "juridico",
        usuario: U,
        renderArea: render,
        estadoArea: estado,
        assistidaInicial: 1,
        montarFiltrosArea: function (el, redesenhar) {
            el.innerHTML = `<div class="soft-form-group"><label>Casos</label><select class="soft-input"><option value="todos">Todos os casos</option><option value="meus">Minhas assistidas</option></select></div>`;
            el.querySelector("select").addEventListener("change", e => { estado.escopo = e.target.value; redesenhar(); });
        }
    });
});
