/* Recepção - Relatório Estatístico/Geral da área */
document.addEventListener("DOMContentLoaded", function () {
    "use strict";
    const C = CASA, { fd, pct, n, kpis, barras, colunas, pilha, tabela, bloco, badge, esc } = RU;
    const ENC = { juridico: "Jurídica", psicologia: "Psicóloga", assistente_social: "Assistente social" };
    const filaJurNoFim = p => C.JUR_FILA.filter(f => f.encaminhadaEm <= p.fim && !C.assistida(f.assistidaId).restrito);
    const visiveis = C.ASSISTIDAS.filter(a => !a.restrito);

    function render(p) {
        const ant = C.anterior(p);
        const evs = q => C.REC_EVENTOS.filter(e => C.dentro(e.data, q) && C.func(e.por).perfil === "recepcao" && !C.assistida(e.assistidaId).restrito);
        const acolh = q => visiveis.filter(a => a.origem === "recepcao" && C.dentro(a.entrada, q));
        const E = evs(p), A = acolh(p), Eant = ant ? evs(ant) : null, Aant = ant ? acolh(ant) : null;
        const encsRec = q => evs(q).filter(e => e.encTipo);
        const direc = C.MATRICULAS.filter(m => C.dentro(m.direcionadaEm, p) && !C.assistida(m.assistidaId).restrito);

        let h = `<div class="rel-cabecalho"><h2>Relatório Estatístico/Geral da Recepção</h2><span class="rel-periodo">${esc(p.rotulo)} · ${fd(p.ini)} a ${fd(p.fim)}</span></div>`;
        h += kpis([
            { valor: n(A.length), valorNum: A.length, anterior: Aant && Aant.length, rotulo: "Acolhimentos novos" },
            { valor: n(encsRec(p).length), valorNum: encsRec(p).length, anterior: ant && encsRec(ant).length, rotulo: "Encaminhamentos feitos" },
            { valor: n(direc.length), rotulo: "Direcionamentos para cursos" },
            { valor: n(E.filter(e => e.tipo === "Dados de contato atualizados").length), rotulo: "Fichas atualizadas" },
            { valor: n(visiveis.filter(a => a.origem !== "recepcao" && C.dentro(a.entrada, p)).length), rotulo: "Cadastradas direto por outra área", ajuda: "Já chegam com ficha pronta" },
            { valor: n(filaJurNoFim(p).filter(f => (!f.atendidaEm || f.atendidaEm > p.fim) && !(f.canceladoEm && f.canceladoEm <= p.fim)).length), rotulo: "Jurídico: sem 1º atendimento", ajuda: "Aguardando ou agendado, no fim do período" },
            { valor: n(C.SOCIAL_FILA.filter(f => f.encaminhadaEm <= p.fim && !C.assistida(f.assistidaId).restrito).length), rotulo: "Assistência social sem confirmação no sistema", ajuda: "O perfil ainda não existe" }
        ]);

        h += `<div class="rel-grade">`;
        h += bloco("Acolhimentos por mês", p.tipo === "mensal" ? "Últimos 6 meses até o mês escolhido." : "Mês a mês dentro do período.", colunas(EST.porMes(p, visiveis.filter(a => a.origem === "recepcao"), "entrada")));
        const sitJ = f => f.canceladoEm && f.canceladoEm <= p.fim ? "Cancelado" : f.atendidaEm && f.atendidaEm <= p.fim ? "Primeiro atendimento realizado" : f.agendadoPara && f.agendadoEm <= p.fim ? "Agendado" : "Aguardando atendimento";
        const fj = filaJurNoFim(p);
        h += bloco("Encaminhamentos ao Jurídico: situação no fim do período", "Vem do registro do próprio Jurídico. A Recepção não vê demanda, relato, processo, orientação nem motivo de cancelamento.", barras(["Aguardando atendimento", "Agendado", "Primeiro atendimento realizado", "Cancelado"].map(k => ({ rotulo: k, valor: fj.filter(f => sitJ(f) === k).length }))));
        h += bloco("Encaminhamentos feitos pela Recepção", "Opções do card de direcionamento.", barras(["juridico", "psicologia", "assistente_social"].map(t => ({ rotulo: ENC[t], valor: encsRec(p).filter(e => e.encTipo === t).length }))));
        const pc = C.CURSOS.filter(c => c.ativo || direc.some(m => m.cursoId === c.id)).map(c => ({ rotulo: c.nome, valor: direc.filter(m => m.cursoId === c.id).length })).sort((a, b) => b.valor - a.valor);
        h += bloco("Cursos mais e menos escolhidos", "Do mais procurado ao menos procurado no período.", barras(pc), true);

        // movimento por dia e hora
        const dias = ["Seg", "Ter", "Qua", "Qui", "Sex"], horas = [8, 9, 10, 11, 13, 14, 15, 16, 17];
        const cel = {};
        A.forEach(a => { const d = C.s2d(a.entrada).getDay(); cel[d + "-" + a.horaEntrada] = (cel[d + "-" + a.horaEntrada] || 0) + 1; });
        const max = Math.max(1, ...Object.values(cel));
        h += bloco("Movimento por dia e horário", "Quantos acolhimentos começaram em cada faixa (pelo horário em que a ficha foi criada). Ajuda a montar a escala da Recepção.",
            `<div class="rel-tabela-wrap"><table class="rel-tabela"><thead><tr><th></th>${horas.map(x => `<th class="num">${x}h</th>`).join("")}</tr></thead><tbody>${dias.map((d, i) => `<tr><th>${d}</th>${horas.map(hh => { const v = cel[(i + 1) + "-" + hh] || 0; return `<td class="num" style="background:rgba(196,139,165,${v / max * 0.85});color:${v / max > 0.5 ? "#fff" : "inherit"}" title="${d} ${hh}h: ${v}">${v || ""}</td>`; }).join("")}</tr>`).join("")}</tbody></table></div>`);
        h += bloco("Por recepcionista", "Acolhimentos e ações na ficha.", tabela([{ t: "Recepcionista" }, { t: "Acolhimentos", num: true }, { t: "Encaminhamentos", num: true }, { t: "Atualizações", num: true }],
            C.EQUIPE.filter(f => f.perfil === "recepcao").map(f => [esc(f.nome), A.filter(a => a.cadastradaPor === f.id).length, E.filter(e => e.por === f.id && e.encTipo).length, E.filter(e => e.por === f.id && e.tipo === "Dados de contato atualizados").length])));
        h += bloco("Faixa etária", A.length + " acolhimentos no período.", barras(EST.faixaEtaria(A)));
        h += bloco("Bairros", "Endereços sigilosos ficam fora.", barras(EST.contar(A.filter(a => !a.enderecoSigiloso), a => a.cidade.startsWith("Itaqua") ? a.bairro : a.cidade.split("/")[0]).slice(0, 10)));
        h += bloco("Como podemos falar com elas", "", pilha([
            { rotulo: "Pode ligar", valor: A.filter(a => a.contato === "Pode ligar").length, cor: "var(--rel-ok)" },
            { rotulo: "Só no horário combinado", valor: A.filter(a => a.contato === "Só no horário combinado").length, cor: "var(--rel-justificada)" },
            { rotulo: "Não ligar", valor: A.filter(a => a.contato === "Não ligar").length, cor: "var(--rel-falta)" }]) +
            `<p style="margin:14px 0 0;font-size:0.88rem">Sem telefone seguro: <b>${A.filter(a => a.semTelefoneSeguro).length}</b> · Endereço sigiloso: <b>${A.filter(a => a.enderecoSigiloso).length}</b></p>`);
        h += bloco("O que mudou nas fichas", "Tipos de atualização feitos pela Recepção.", barras(EST.contar(E.filter(e => e.tipo === "Dados de contato atualizados" || e.tipo === "Direcionamento alterado"), e => e.tipo === "Direcionamento alterado" ? "Novo curso no direcionamento" : e.detalhe)));

        // lista nominal (a Recepção já vê esses nomes em "Mulheres atendidas")
        h += bloco("Mulheres acolhidas no período", "Mesmas informações da aba “Mulheres atendidas”. Casos restritos não aparecem.", tabela([{ t: "Data" }, { t: "Nome" }, { t: "Idade", num: true }, { t: "Cursos" }, { t: "Encaminhamentos" }, { t: "Recepcionista" }],
            A.slice().sort((x, y) => y.entrada.localeCompare(x.entrada)).map(a => [fd(a.entrada), esc(a.nome), C.idade(a.nasc, a.entrada), C.MATRICULAS.filter(m => m.assistidaId === a.id).map(m => esc(C.curso(m.cursoId).nome)).join("<br>") || "-", a.encaminhamentos.filter(e => e.data === a.entrada).map(e => badge(ENC[e.tipo])).join(" ") || "-", esc(C.func(a.cadastradaPor).nome)])), true);
        h += `</div>`;
        return h;
    }

    paginaRelatorios({
        perfil: "recepcao",
        usuario: { id: "REC-000001", nome: "Carla Mendes", perfilNome: "Recepção", email: "recepcao@casamulher.local" },
        renderArea: render,
        assistidaInicial: 1
    });
});
