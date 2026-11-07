/* Psicologia - Relatório Estatístico/Geral da área */
document.addEventListener("DOMContentLoaded", function () {
    "use strict";
    const C = CASA, { fd, pct, n, kpis, barras, colunas, pilha, tabela, bloco, badge, esc } = RU;
    const U = { id: "PSI-000001", nome: "Psicóloga Um", perfilNome: "Psicologia", email: "psicologia.teste@casamulher.local" };
    const estado = { escopo: "todos" };
    const ORDEM = ["Baixo", "Moderado", "Alto", "Iminente"];

    function render(p) {
        const ant = C.anterior(p);
        const meus = estado.escopo === "meus";
        const pronts = C.PSI_PRONT.filter(x => !meus || x.responsavel === U.id);
        const ids = new Set(pronts.map(x => x.assistidaId));
        const sessQ = q => C.PSI_SESS.filter(s => ids.has(s.assistidaId) && C.dentro(s.data, q));
        const S = sessQ(p), Sant = ant ? sessQ(ant) : null;
        const comp = S.filter(s => s.presenca === "Compareceu").length;
        const ativos = pronts.filter(x => x.abertura <= p.fim && (!x.encerramento || x.encerramento.data > p.fim));
        const riscos = ativos.map(x => ({ pr: x, r: EST.riscoAtual(x.assistidaId, p.fim) }));
        const fila = C.PSI_FILA.filter(f => f.encaminhadaEm <= p.fim && (!f.atendidaEm || f.atendidaEm > p.fim));
        const atendP = C.PSI_FILA.filter(f => C.dentro(f.atendidaEm, p));
        const espera = atendP.length ? Math.round(atendP.reduce((s, f) => s + C.diffDias(f.encaminhadaEm, f.atendidaEm), 0) / atendP.length) : null;
        const encerr = pronts.filter(x => x.encerramento && C.dentro(x.encerramento.data, p));

        // evolução do risco: primeira x última avaliação no período, por prontuário
        const evo = { Melhorou: 0, "Ficou igual": 0, Piorou: 0 };
        pronts.forEach(pr => {
            const ss = S.filter(s => s.assistidaId === pr.assistidaId && s.risco);
            if (ss.length < 2) return;
            const d = ORDEM.indexOf(ss[ss.length - 1].risco) - ORDEM.indexOf(ss[0].risco);
            evo[d < 0 ? "Melhorou" : d > 0 ? "Piorou" : "Ficou igual"]++;
        });

        let h = `<div class="rel-cabecalho"><h2>Relatório Estatístico/Geral da Psicologia${meus ? " · só os meus prontuários" : ""}</h2><span class="rel-periodo">${esc(p.rotulo)} · ${fd(p.ini)} a ${fd(p.fim)}</span></div>`;
        h += kpis([
            { valor: n(S.length), valorNum: S.length, anterior: Sant && Sant.length, rotulo: "Sessões registradas" },
            { valor: pct(comp, S.length) + "%", rotulo: "Comparecimento", ajuda: comp + " de " + S.length },
            { valor: n(ativos.length), rotulo: "Em acompanhamento", ajuda: "No fim do período" },
            { valor: n(pronts.filter(x => C.dentro(x.abertura, p)).length), rotulo: "Prontuários abertos" },
            { valor: n(encerr.length), rotulo: "Acompanhamentos encerrados" },
            { valor: n(fila.length), rotulo: "Na fila" },
            { valor: espera != null ? espera + " dias" : "-", rotulo: "Espera média na fila" },
            { valor: n(riscos.filter(x => x.r === "Alto" || x.r === "Iminente").length), rotulo: "Com risco alto ou iminente", alerta: true }
        ]);
        h += `<div class="rel-grade">`;
        h += bloco("Sessões por mês", p.tipo === "mensal" ? "Últimos 6 meses até o mês escolhido." : "Mês a mês dentro do período.", colunas(EST.porMes(p, C.PSI_SESS.filter(s => ids.has(s.assistidaId)), "data"), { cor: "var(--rel-azul)" }));
        h += bloco("Presença", "", pilha([
            { rotulo: "Compareceu", valor: comp, cor: "var(--rel-ok)" },
            { rotulo: "Faltou e justificou", valor: S.filter(s => s.presenca === "Faltou e justificou").length, cor: "var(--rel-justificada)" },
            { rotulo: "Faltou", valor: S.filter(s => s.presenca === "Faltou").length, cor: "var(--rel-falta)" },
            { rotulo: "Remarcou", valor: S.filter(s => s.presenca === "Remarcou").length, cor: "var(--rel-neutro)" }]) +
            `<div style="margin-top:14px"><p class="rel-nota">Modalidade</p>${barras(EST.contar(S, s => s.modalidade))}</div>`);
        h += bloco("Tipo de atendimento", "", barras(EST.contar(S, s => s.tipo)));
        h += bloco("Risco atual", "Última avaliação de cada acompanhamento ativo.", barras(ORDEM.map(r => ({ rotulo: r, valor: riscos.filter(x => x.r === r).length, cor: r === "Alto" ? "#D9787F" : r === "Iminente" ? "#A23B3B" : r === "Moderado" ? "#E0B35E" : "#8DBFA3" }))));
        h += bloco("Como o risco evoluiu", "Compara a primeira e a última avaliação de cada prontuário dentro do período (quem teve pelo menos 2).", barras(Object.keys(evo).map(k => ({ rotulo: k, valor: evo[k] }))));
        h += bloco("Motivos de encerramento", encerr.length + " encerramentos no período.", barras(EST.contar(encerr, x => x.encerramento.motivo)));
        h += bloco("Encaminhamentos externos", "CAPS, UBS, CRAS/CREAS, abrigo.", barras(EST.contar(S, s => s.encaminhamentos)));
        h += bloco("De onde vieram", "Quem encaminhou as assistidas que abriram prontuário no período.", barras(EST.contar(atendP, f => { const pf = C.func(f.por).perfil; return pf === "recepcao" ? "Recepção" : pf === "juridico" ? "Jurídico" : "Cadastro direto na Psicologia"; })));
        h += bloco("Por psicóloga", "", tabela([{ t: "Psicóloga" }, { t: "CRP" }, { t: "Sessões", num: true }, { t: "Prontuários ativos", num: true }, { t: "Comparecimento", num: true }],
            C.EQUIPE.filter(f => f.perfil === "psicologia").map(f => { const sf = S.filter(s => s.psicologa === f.id); return [esc(f.nome), esc(f.crp), sf.length, ativos.filter(x => x.responsavel === f.id).length, sf.length ? pct(sf.filter(s => s.presenca === "Compareceu").length, sf.length) + "%" : "-"]; })));
        const porPront = ativos.map(x => C.PSI_SESS.filter(s => s.assistidaId === x.assistidaId && s.data <= p.fim).length);
        h += bloco("Duração dos acompanhamentos", "", kpis([
            { valor: porPront.length ? Math.round(porPront.reduce((s, x) => s + x, 0) / porPront.length) : "-", rotulo: "Sessões em média por prontuário ativo" },
            { valor: encerr.length ? Math.round(encerr.reduce((s, x) => s + C.diffDias(x.abertura, x.encerramento.data), 0) / encerr.length) + " dias" : "-", rotulo: "Tempo médio até o encerramento" },
            { valor: n(S.filter(s => s.declaracao).length), rotulo: "Declarações de comparecimento" },
            { valor: n(C.AUDITORIA.filter(x => x.tipo === "Acesso de emergência (Psicologia)" && C.dentro(x.data, p)).length), rotulo: "Acessos de emergência" }
        ]));

        const linhas = ativos.concat(encerr.filter(x => !ativos.includes(x))).map(pr => {
            const a = C.assistida(pr.assistidaId), sp = S.filter(s => s.assistidaId === pr.assistidaId);
            const todas = C.PSI_SESS.filter(s => s.assistidaId === pr.assistidaId && s.data <= p.fim);
            const r = EST.riscoAtual(pr.assistidaId, p.fim);
            return [pr.numero, esc(a.nome), esc(C.func(pr.responsavel).nome), sp.length, todas.length, todas.length ? fd(todas[todas.length - 1].data) : "-", r ? `<span class="soft-badge ${r === "Alto" || r === "Iminente" ? "soft-badge--risco" : ""}">${r}</span>` : "-", pr.encerramento && pr.encerramento.data <= p.fim ? "Encerrado: " + esc(pr.encerramento.motivo) : "Em acompanhamento", pr.visibilidade === "equipe" ? "Equipe" : badge("Só a responsável")];
        }).sort((x, y) => y[3] - x[3]);
        h += bloco("Prontuários", "Ativos no fim do período e encerrados nele. Sem conteúdo das sessões.", tabela([{ t: "Prontuário" }, { t: "Assistida" }, { t: "Responsável" }, { t: "Sessões no período", num: true }, { t: "Total", num: true }, { t: "Última sessão" }, { t: "Risco" }, { t: "Situação" }, { t: "Anotações" }], linhas), true);
        h += `</div>`;
        return h;
    }

    paginaRelatorios({
        perfil: "psicologia",
        usuario: U,
        renderArea: render,
        assistidaInicial: 1,
        montarFiltrosArea: function (el, redesenhar) {
            el.innerHTML = `<div class="soft-form-group"><label>Prontuários</label><select class="soft-input"><option value="todos">Todos os prontuários</option><option value="meus">Minhas assistidas</option></select></div>`;
            el.querySelector("select").addEventListener("change", e => { estado.escopo = e.target.value; redesenhar(); });
        }
    });
});
