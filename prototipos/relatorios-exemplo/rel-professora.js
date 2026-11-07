/* Professora - Relatório Estatístico/Geral das turmas */
document.addEventListener("DOMContentLoaded", function () {
    "use strict";
    const C = CASA, { fd, fdc, pct, n, novo, kpis, barras, colunas, pilha, tabela, bloco, badge, esc } = RU;
    const U = { id: "PRO-000001", nome: "Ana Lima", perfilNome: "Professora", email: "professora.ana@casamulher.local" };
    const meusCursos = RI.cursosDoUsuario(U.id);
    const estado = { curso: "todos" };

    function render(p) {
        const ant = C.anterior(p);
        const cs = estado.curso === "todos" ? meusCursos : [+estado.curso];
        const nomeCursos = cs.map(id => C.curso(id).nome).join(" e ");
        const mats = C.MATRICULAS.filter(m => cs.includes(m.cursoId) && !C.assistida(m.assistidaId).restrito);
        const regs = EST.registros(p, { cursoIds: cs }).filter(r => !C.assistida(r.assistidaId).restrito);
        const F = EST.freq(regs), Fant = ant ? EST.freq(EST.registros(ant, { cursoIds: cs })) : null;
        const aulas = C.AULAS.filter(a => cs.includes(a.cursoId) && C.dentro(a.data, p)).sort((x, y) => x.data.localeCompare(y.data));
        const ativas = mats.filter(m => EST.ativaEm(m, p.fim));
        const entraram = mats.filter(m => C.dentro(m.inicio, p));
        const concluiram = mats.filter(m => m.situacao === "concluiu" && C.dentro(m.fim, p));
        const desistiram = mats.filter(m => m.situacao === "desistiu" && C.dentro(m.fim, p));

        // por aluna
        const porAluna = mats.filter(m => m.inicio <= p.fim && (!m.fim || m.fim >= p.ini)).map(m => {
            const r = regs.filter(x => x.matriculaId === m.id);
            const todos = EST.registros({ ini: m.inicio, fim: p.fim }, { assistidaId: m.assistidaId, cursoIds: [m.cursoId] }).sort((a, b) => a.data.localeCompare(b.data));
            return { m, a: C.assistida(m.assistidaId), f: EST.freq(r), seq: EST.sequenciaFaltas(todos) };
        });
        const alerta = porAluna.filter(x => x.m.situacao === "cursando" && EST.ativaEm(x.m, p.fim) && (x.seq >= 3 || (x.f.total >= 4 && x.f.pct < 60)));

        let h = `<div class="rel-cabecalho"><h2>Relatório Estatístico/Geral · ${esc(nomeCursos)}</h2><span class="rel-periodo">${esc(p.rotulo)} · ${fd(p.ini)} a ${fd(p.fim)}</span></div>`;
        if (alerta.length) h += `<div class="soft-alert" style="margin-bottom:1.2rem"><b>${alerta.length} aluna(s) para avaliar</b> (3 ou mais faltas seguidas ou frequência abaixo de 60%): ${alerta.map(x => esc(x.a.nome.split(" ")[0] + " " + x.a.nome.split(" ").slice(-1)) + (x.a.contato === "Não ligar" || x.a.semTelefoneSeguro ? " (não ligar)" : "")).join(", ")}.<br><small>Isso não significa abandono e não dispara contato automático: pode ser transporte, saúde ou segurança. Você e a equipe decidem como abordar, pelo canal e no horário seguros de cada uma.</small></div>`;
        h += kpis([
            { valor: n(ativas.length), rotulo: "Alunas ativas", ajuda: "No fim do período" },
            { valor: n(entraram.length), rotulo: "Entraram na turma" },
            { valor: n(concluiram.length), rotulo: "Concluíram" + novo() },
            { valor: n(desistiram.length), rotulo: "Interrupções" + novo() },
            { valor: F.pct + "%", valorNum: F.pct, anterior: Fant && Fant.total ? Fant.pct : null, sufixoDelta: " p.p.", rotulo: "Frequência média" },
            { valor: n(aulas.length), rotulo: "Aulas dadas" },
            { valor: n(F.justificada), rotulo: "Faltas justificadas" },
            { valor: n(F.faltou), rotulo: "Faltas sem justificativa" }
        ]);
        h += `<div class="rel-grade">`;
        h += bloco("Frequência aula a aula", "Percentual de presentes em cada aula do período.", colunas(aulas.map(a => { const r = a.registros.filter(x => !C.assistida(x.assistidaId).restrito); return { rotulo: fdc(a.data), valor: pct(r.filter(x => x.status === "presente").length, r.length) }; }).slice(-26), { sufixo: "%", cor: "var(--rel-ok)" }), true);
        h += bloco("Presenças e faltas", regs.length + " registros de chamada.", pilha([
            { rotulo: "Presente", valor: F.presente, cor: "var(--rel-ok)" },
            { rotulo: "Justificada", valor: F.justificada, cor: "var(--rel-justificada)" },
            { rotulo: "Faltou", valor: F.faltou, cor: "var(--rel-falta)" }]) +
            `<div style="margin-top:14px"><p class="rel-nota">Justificativas mais comuns</p>${barras(EST.contar(regs.filter(r => r.status === "justificada"), r => r.observacao))}</div>`);
        h += bloco("Motivos de interrupção" + novo(), desistiram.length + " no período. Hoje o sistema não pede o motivo ao tirar a aluna. O motivo fica só com a professora (e em contagem, sem nomes, no ADM).", barras(EST.contar(desistiram, m => m.motivoSaida)) +
            `<div style="margin-top:14px"><p class="rel-nota">Dia da semana com mais faltas</p>${barras(EST.contar(regs.filter(r => r.status !== "presente"), r => ["Dom", "Segunda", "Terça", "Quarta", "Quinta", "Sexta", "Sáb"][C.s2d(r.data).getDay()] + " (" + C.curso(r.cursoId).nome.split(" ")[0] + ")"))}</div>`);

        h += bloco("Alunas: presenças, faltas e situação", "O histórico completo de cada uma fica na aba “Relatório individual da assistida”. Ordenado da menor para a maior frequência.",
            tabela([{ t: "Aluna" }, { t: "Curso" }, { t: "Entrou" }, { t: "Saiu / terminou" }, { t: "Situação" }, { t: "P", num: true }, { t: "J", num: true }, { t: "F", num: true }, { t: "Frequência", num: true }, { t: "Faltas seguidas", num: true }],
                porAluna.sort((x, y) => x.f.pct - y.f.pct).map(x => [esc(x.a.nome), esc(C.curso(x.m.cursoId).nome), fd(x.m.inicio), x.m.fim && x.m.fim <= p.fim ? fd(x.m.fim) : "-", ({ cursando: badge("Cursando", "psicologia"), concluiu: badge("Concluiu", "ok"), desistiu: badge("Interrompido", "restrito"), encerrado: badge("Encerrado") })[x.m.situacao] + (x.m.motivoSaida ? `<br><small>${esc(x.m.motivoSaida)}</small>` : ""), x.f.presente, x.f.justificada, x.f.faltou, x.f.total ? x.f.pct + "%" : "-", x.seq >= 3 ? `<b style="color:#A23B3B">${x.seq}</b>` : x.seq])), true);

        const obs = regs.filter(r => r.observacao && r.status !== "justificada").sort((a, b) => b.data.localeCompare(a.data));
        h += bloco("Observações da professora sobre as alunas", "Anotações feitas na chamada, por dia.", obs.length ? `<ul class="rel-tempo">${obs.slice(0, 25).map(r => `<li class="area-curso"><span class="rel-tempo-data">${fd(r.data)}</span><b>${esc(C.assistida(r.assistidaId).nome)}</b>: ${esc(r.observacao)}</li>`).join("")}</ul>` : `<p class="soft-empty-state">Sem observações no período.</p>`);
        h += bloco("Aulas e plano de aula", "Tema e observações gerais de cada aula (do plano de aula salvo no servidor).", tabela([{ t: "Data" }, { t: "Curso" }, { t: "Tema" }, { t: "Presentes", num: true }, { t: "Observações da aula" }],
            aulas.slice().reverse().map(a => [fd(a.data), esc(C.curso(a.cursoId).nome.split(" ")[0]), esc(a.tema), a.semChamada ? badge("Chamada não lançada", "restrito") : a.registros.filter(x => x.status === "presente").length + "/" + a.registros.length, esc(a.observacoes || "-")])));
        h += `</div>`;
        return h;
    }

    paginaRelatorios({
        perfil: "professor",
        usuario: U,
        renderArea: render,
        assistidaInicial: 2,
        montarFiltrosArea: function (el, redesenhar) {
            el.innerHTML = `<div class="soft-form-group"><label>Curso</label><select class="soft-input"><option value="todos">Todos os meus cursos</option>${meusCursos.map(id => `<option value="${id}">${esc(C.curso(id).nome)}</option>`).join("")}</select></div>`;
            el.querySelector("select").addEventListener("change", e => { estado.curso = e.target.value; redesenhar(); });
        }
    });
});
