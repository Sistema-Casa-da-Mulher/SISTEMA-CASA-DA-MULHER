/* =====================================================================
   PROTÓTIPO - peças comuns dos relatórios
   RU  = interface (relógio, menu, período, gráficos, tabelas)
   EST = contas usadas por mais de uma tela (os números batem entre telas)
   RI  = relatório individual da assistida, com as regras de sigilo de cada perfil
   ===================================================================== */
(function () {
    "use strict";
    const C = window.CASA;

    // ------------------------------------------------------------------ RU
    const esc = s => String(s == null ? "" : s).replace(/[&<>"']/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
    const fd = s => { if (!s) return "-"; const [a, m, d] = s.split("-"); return d + "/" + m + "/" + a; };
    const fdc = s => { if (!s) return "-"; const [, m, d] = s.split("-"); return d + "/" + m; };
    const pct = (a, b) => b ? Math.round(a * 100 / b) : 0;
    const n = v => Number(v || 0).toLocaleString("pt-BR");
    const novo = (t) => `<span class="rel-dado-novo" title="Este dado ainda não é gravado pelo sistema">${t || "dado novo"}</span>`;

    function iniciarPagina(usuario) {
        const elD = document.getElementById("currentDate"), elT = document.getElementById("currentTime");
        function tic() {
            const agora = new Date();
            elD.textContent = agora.toLocaleDateString("pt-BR", { weekday: "long", year: "numeric", month: "long", day: "numeric" });
            elT.textContent = agora.toLocaleTimeString("pt-BR");
        }
        if (elD && elT) { tic(); setInterval(tic, 1000); }
        const card = document.getElementById("sessionCard"), trig = document.getElementById("sessionTrigger");
        if (trig) trig.addEventListener("click", () => card.classList.toggle("open"));
        document.addEventListener("click", e => { if (card && !card.contains(e.target)) card.classList.remove("open"); });
        const set = (id, v) => { const el = document.getElementById(id); if (el) el.textContent = v; };
        set("sessionUserName", usuario.nome); set("sessionUserRole", usuario.perfilNome); set("sessionUserEmail", usuario.email); set("sessionUserId", usuario.id);
        const sair = document.getElementById("btnSair"); if (sair) sair.addEventListener("click", () => alert("Protótipo: aqui o sistema encerra a sessão."));
    }

    function abas(seletorBotoes, seletorPaineis, aoTrocar) {
        const bts = document.querySelectorAll(seletorBotoes);
        bts.forEach(b => b.addEventListener("click", () => {
            bts.forEach(x => x.classList.toggle("active", x === b));
            document.querySelectorAll(seletorPaineis).forEach(p => p.hidden = p.dataset.painel !== b.dataset.aba);
            if (aoTrocar) aoTrocar(b.dataset.aba);
        }));
    }

    // Controle de período: Mensal | Semestral | Anual | Geral + referência
    function controlePeriodo(el, aoMudar, inicial) {
        let tipo = (inicial && inicial.tipo) || "mensal";
        const rotulos = { mensal: "Mensal", semestral: "Semestral", anual: "Anual", geral: "Geral" };
        el.innerHTML = `
            <div class="soft-form-group"><label>Período</label>
                <div class="rel-periodo-tipo">${Object.keys(rotulos).map(t => `<button type="button" data-tipo="${t}">${rotulos[t]}</button>`).join("")}</div>
            </div>
            <div class="soft-form-group rel-ref"><label>Referência</label><select class="soft-input"></select></div>`;
        const sel = el.querySelector("select"), grpRef = el.querySelector(".rel-ref");
        function preencher(ref) {
            const ops = C.opcoes(tipo);
            grpRef.style.display = ops.length ? "" : "none";
            sel.innerHTML = ops.map(o => `<option value="${o[0]}">${o[1]}</option>`).join("");
            if (ref && ops.some(o => o[0] === ref)) sel.value = ref;
            el.querySelectorAll("[data-tipo]").forEach(b => b.classList.toggle("active", b.dataset.tipo === tipo));
        }
        function emitir() { aoMudar(C.periodo(tipo, sel.value)); }
        el.querySelectorAll("[data-tipo]").forEach(b => b.addEventListener("click", () => { tipo = b.dataset.tipo; preencher(); emitir(); }));
        sel.addEventListener("change", emitir);
        preencher(inicial && inicial.ref);
        return { periodo: () => C.periodo(tipo, sel.value) };
    }

    function kpis(lista) {
        return `<div class="rel-kpis">${lista.map(k => {
            let delta = "";
            if (k.anterior != null && k.valorNum != null) {
                const d = k.valorNum - k.anterior;
                const cls = d > 0 ? "sobe" : d < 0 ? "desce" : "";
                delta = `<span class="rel-kpi-delta ${cls}">${d === 0 ? "igual ao" : (d > 0 ? "+" : "") + n(d) + (k.sufixoDelta || "") + " vs"} período anterior</span>`;
            } else if (k.ajuda) delta = `<span class="rel-kpi-delta">${k.ajuda}</span>`;
            return `<div class="rel-kpi ${k.alerta ? "rel-kpi--alerta" : ""}" ${k.titulo ? `title="${esc(k.titulo)}"` : ""}><span class="rel-kpi-valor">${k.valor}</span><span class="rel-kpi-rotulo">${k.rotulo}${k.novo ? novo() : ""}</span>${delta}</div>`;
        }).join("")}</div>`;
    }

    function barras(itens, op) {
        op = op || {};
        if (!itens.length) return `<p class="soft-empty-state">Nada no período.</p>`;
        const max = op.max || Math.max(1, ...itens.map(i => i.valor));
        return `<div class="rel-barras">${itens.map(i => `
            <div class="rel-barra-linha" title="${esc(i.rotulo)}: ${n(i.valor)}${op.sufixo || ""}${i.dica ? " - " + esc(i.dica) : ""}">
                <span class="rel-barra-rotulo">${esc(i.rotulo)}</span>
                <span class="rel-barra-trilho"><span class="rel-barra-valor-fill" style="width:${Math.max(0.5, i.valor * 100 / max)}%;${i.cor ? "background:" + i.cor : ""}"></span></span>
                <span class="rel-barra-num">${n(i.valor)}${op.sufixo || ""}${i.extra ? ` <small>${i.extra}</small>` : ""}</span>
            </div>`).join("")}</div>`;
    }

    function colunas(itens, op) {
        op = op || {};
        if (!itens.length) return `<p class="soft-empty-state">Nada no período.</p>`;
        const max = Math.max(1, ...itens.map(i => i.valor));
        const iMax = itens.findIndex(i => i.valor === max);
        return `<div class="rel-colunas">${itens.map((i, k) => `
            <div class="rel-coluna ${k === iMax || k === itens.length - 1 ? "mostrar" : ""}" title="${esc(i.rotulo)}: ${n(i.valor)}${op.sufixo || ""}">
                <span class="rel-coluna-valor">${n(i.valor)}${op.sufixo || ""}</span>
                <span class="rel-coluna-fill" style="height:${i.valor * 100 / max}%;${op.cor ? "background:" + op.cor : ""}"></span>
            </div>`).join("")}</div>
            <div class="rel-colunas-rotulos">${itens.map(i => `<span>${esc(i.rotulo)}</span>`).join("")}</div>`;
    }

    function pilha(segs) {
        const tot = segs.reduce((s, x) => s + x.valor, 0);
        if (!tot) return `<p class="soft-empty-state">Nada no período.</p>`;
        return `<div class="rel-pilha">${segs.filter(s => s.valor).map(s => `<span style="width:${s.valor * 100 / tot}%;background:${s.cor}" title="${esc(s.rotulo)}: ${n(s.valor)} (${pct(s.valor, tot)}%)"></span>`).join("")}</div>
            <div class="rel-legenda">${segs.map(s => `<span><i style="background:${s.cor}"></i>${esc(s.rotulo)}: <b>${n(s.valor)}</b> (${pct(s.valor, tot)}%)</span>`).join("")}</div>`;
    }

    function tabela(cols, linhas, op) {
        op = op || {};
        if (!linhas.length) return `<p class="soft-empty-state">${op.vazio || "Nada no período."}</p>`;
        return `<div class="rel-tabela-wrap"><table class="rel-tabela"><thead><tr>${cols.map(c => `<th class="${c.num ? "num" : ""}">${c.t}</th>`).join("")}</tr></thead>
            <tbody>${linhas.map(l => `<tr>${l.map((v, i) => `<td class="${cols[i].num ? "num" : ""}">${v}</td>`).join("")}</tr>`).join("")}</tbody></table></div>`;
    }

    const bloco = (titulo, nota, corpo, largo) => `<div class="rel-bloco ${largo ? "rel-bloco--largo" : ""}"><h3>${titulo}</h3>${nota ? `<p class="rel-nota">${nota}</p>` : ""}${corpo}</div>`;
    const badge = (t, tipo) => `<span class="soft-badge ${tipo ? "soft-badge--" + tipo : ""}">${t}</span>`;

    function rodape(usuario, extra) {
        const agora = new Date();
        return `<div class="rel-rodape">Gerado por ${esc(usuario.nome)} (${esc(usuario.id)}) em ${agora.toLocaleDateString("pt-BR")} às ${agora.toLocaleTimeString("pt-BR").slice(0, 5)}. Documento de uso interno da Casa da Mulher, com dados protegidos pela LGPD. A geração e a impressão deste relatório ficam registradas no histórico de ações.${extra ? " " + extra : ""}</div>`;
    }

    function seletorAssistida(el, lista, aoEscolher, inicialId) {
        el.innerHTML = `<label>Assistida</label><div class="rel-busca"><input type="search" class="soft-input" placeholder="Buscar por nome ou CPF..." autocomplete="off"><div class="rel-busca-lista" hidden></div></div>`;
        const inp = el.querySelector("input"), box = el.querySelector(".rel-busca-lista");
        const atual = lista.find(a => a.id === inicialId) || lista[0];
        if (atual) inp.value = atual.nome;
        function mostrar() {
            const q = inp.value.trim().toLowerCase();
            const r = lista.filter(a => !q || a.nome.toLowerCase().includes(q) || a.cpf.includes(q)).slice(0, 40);
            box.innerHTML = r.length ? r.map(a => `<button type="button" data-id="${a.id}">${esc(a.nome)} <small>· CPF ${a.cpf} · entrada ${fd(a.entrada)}</small></button>`).join("") : `<div style="padding:10px 14px;color:#AD859B">Nenhuma assistida encontrada.</div>`;
            box.hidden = false;
        }
        inp.addEventListener("focus", () => { inp.select(); mostrar(); });
        inp.addEventListener("input", mostrar);
        box.addEventListener("click", e => { const b = e.target.closest("button"); if (!b) return; const a = lista.find(x => x.id === +b.dataset.id); inp.value = a.nome; box.hidden = true; aoEscolher(a.id); });
        document.addEventListener("click", e => { if (!el.contains(e.target)) box.hidden = true; });
        return atual ? atual.id : null;
    }

    // ------------------------------------------------------------------ EST
    const ativaEm = (m, data) => m.inicio <= data && (!m.fim || m.fim >= data);
    function registros(p, filtro) {
        filtro = filtro || {};
        const r = [];
        C.AULAS.forEach(a => {
            if (p && !C.dentro(a.data, p)) return;
            if (filtro.cursoIds && !filtro.cursoIds.includes(a.cursoId)) return;
            a.registros.forEach(x => { if (filtro.assistidaId && x.assistidaId !== filtro.assistidaId) return; r.push(Object.assign({ data: a.data, cursoId: a.cursoId, tema: a.tema }, x)); });
        });
        return r;
    }
    function freq(regs) {
        const o = { presente: 0, justificada: 0, faltou: 0 };
        regs.forEach(r => o[r.status]++);
        o.total = regs.length; o.pct = pct(o.presente, o.total);
        return o;
    }
    function procuraCursos(p) {
        return C.CURSOS.map(c => {
            const ms = C.MATRICULAS.filter(m => m.cursoId === c.id);
            const aulas = C.AULAS.filter(a => a.cursoId === c.id && C.dentro(a.data, p));
            const f = freq(registros(p, { cursoIds: [c.id] }));
            return {
                curso: c,
                procura: ms.filter(m => C.dentro(m.direcionadaEm, p)).length,
                ativas: ms.filter(m => ativaEm(m, p.fim)).length,
                concluiram: ms.filter(m => m.situacao === "concluiu" && C.dentro(m.fim, p)).length,
                desistiram: ms.filter(m => m.situacao === "desistiu" && C.dentro(m.fim, p)).length,
                vinculadas: ms.filter(m => m.inicio <= p.fim && (!m.fim || m.fim >= p.ini)).length,
                totalHistorico: ms.length,
                aulas: aulas.length,
                freq: f
            };
        });
    }
    function encaminhamentos(p) {
        const filas = { juridico: C.JUR_FILA, psicologia: C.PSI_FILA, assistente_social: C.SOCIAL_FILA };
        const nomes = { juridico: "Jurídico", psicologia: "Psicologia", assistente_social: "Assistência social" };
        return Object.keys(filas).map(k => {
            const f = filas[k];
            const atendidas = f.filter(x => C.dentro(x.atendidaEm, p));
            const esperas = atendidas.map(x => C.diffDias(x.encaminhadaEm, x.atendidaEm));
            const aguardando = f.filter(x => x.encaminhadaEm <= p.fim && (!x.atendidaEm || x.atendidaEm > p.fim) && !(x.canceladoEm && x.canceladoEm <= p.fim));
            return {
                chave: k, nome: nomes[k],
                encaminhadas: f.filter(x => C.dentro(x.encaminhadaEm, p)).length,
                atendidas: atendidas.length,
                aguardando: aguardando.length,
                semAgendamento: aguardando.filter(x => !x.agendadoPara || x.agendadoEm > p.fim).length,
                cancelados: f.filter(x => C.dentro(x.canceladoEm, p)).length,
                semRegistro: k === "assistente_social",
                esperaMedia: esperas.length ? Math.round(esperas.reduce((s, x) => s + x, 0) / esperas.length) : null,
                maisAntiga: aguardando.length ? Math.max(...aguardando.map(x => C.diffDias(x.encaminhadaEm, p.fim))) : 0
            };
        });
    }
    function faixaEtaria(lista) {
        const fx = [["18 a 24", 18, 24], ["25 a 34", 25, 34], ["35 a 44", 35, 44], ["45 a 59", 45, 59], ["60 ou mais", 60, 200]];
        return fx.map(f => ({ rotulo: f[0], valor: lista.filter(a => { const i = C.idade(a.nasc, a.entrada); return i >= f[1] && i <= f[2]; }).length }));
    }
    function contar(lista, fn) {
        const o = {};
        lista.forEach(x => { const k = fn(x); if (k == null || k === "") return; (Array.isArray(k) ? k : [k]).forEach(kk => o[kk] = (o[kk] || 0) + 1); });
        return Object.keys(o).map(k => ({ rotulo: k, valor: o[k] })).sort((a, b) => b.valor - a.valor);
    }
    function porMes(p, lista, campo) {
        let ps = p;
        if (p.tipo === "mensal") ps = { ini: C.addDias(p.ini, -150).slice(0, 8) + "01", fim: p.fim };
        return C.mesesDe(ps).map(m => ({ rotulo: m.rotulo, valor: lista.filter(x => (typeof campo === "function" ? campo(x) : x[campo]) && (typeof campo === "function" ? campo(x) : x[campo]).startsWith(m.chave)).length }));
    }
    function riscoAtual(assistidaId, ate) {
        const s = C.PSI_SESS.filter(x => x.assistidaId === assistidaId && x.risco && (!ate || x.data <= ate));
        return s.length ? s[s.length - 1].risco : null;
    }
    function sequenciaFaltas(regsOrdenados) {
        let seq = 0;
        for (let i = regsOrdenados.length - 1; i >= 0; i--) { if (regsOrdenados[i].status === "presente") break; seq++; }
        return seq;
    }

    // ------------------------------------------------------------------ RI
    // perfis: recepcao | juridico | psicologia | professor | adm
    const SITUACAO_CURSO = { aguardando: "Aguardando vaga", cursando: "Cursando", concluiu: "Concluído", desistiu: "Interrompido", encerrado: "Curso encerrado pela Casa" };
    const badgeSituacao = s => badge(SITUACAO_CURSO[s], s === "concluiu" ? "ok" : s === "desistiu" ? "restrito" : s === "cursando" ? "psicologia" : "");
    const cursosDoUsuario = uid => C.CURSOS.filter(c => c.prof === uid).map(c => c.id);

    function listaVisivel(perfil, uid) {
        const A = C.ASSISTIDAS;
        if (perfil === "adm") return A.slice();
        if (perfil === "recepcao") return A.filter(a => !a.restrito);
        if (perfil === "juridico") return A.filter(a => C.JUR_FILA.some(f => f.assistidaId === a.id));
        if (perfil === "psicologia") return A.filter(a => C.PSI_FILA.some(f => f.assistidaId === a.id));
        if (perfil === "professor") { const cs = cursosDoUsuario(uid); return A.filter(a => !a.restrito && C.MATRICULAS.some(m => m.assistidaId === a.id && cs.includes(m.cursoId))); }
        return [];
    }

    function marcos(a, perfil, uid) {
        const ev = [];
        const add = (data, area, texto, quem) => ev.push({ data, area, texto, quem });
        const nomeQuem = id => { const f = C.func(id); if (perfil === "recepcao" && f.perfil === "psicologia") return "Equipe técnica da Casa"; return f.nome; };
        const cs = perfil === "professor" ? cursosDoUsuario(uid) : null;
        if (perfil !== "professor") {
            C.REC_EVENTOS.filter(e => e.assistidaId === a.id).forEach(e => {
                if (e.encTipo === "psicologia" && perfil !== "recepcao" && perfil !== "psicologia") return;
                if (e.tipo === "Dados de contato atualizados" && perfil !== "recepcao" && perfil !== "adm") return;
                add(e.data, e.encTipo === "juridico" ? "juridico" : e.encTipo === "assistente_social" ? "social" : e.encTipo === "psicologia" && perfil === "psicologia" ? "psicologia" : "", (e.encTipo === "psicologia" && perfil === "psicologia" ? "Encaminhada à Psicologia" : e.tipo) + (e.detalhe ? ": " + e.detalhe.charAt(0).toLowerCase() + e.detalhe.slice(1) : ""), nomeQuem(e.por));
            });
            a.encaminhamentos.filter(e => C.func(e.por).perfil !== "recepcao").forEach(e => {
                if (e.tipo === "psicologia" && !["psicologia", "juridico"].includes(perfil)) return;
                add(e.data, e.tipo === "psicologia" ? "psicologia" : e.tipo === "juridico" ? "juridico" : "social", "Encaminhada " + (e.tipo === "psicologia" ? "à Psicologia" : e.tipo === "juridico" ? "ao Jurídico" : "à Assistência social"), nomeQuem(e.por));
            });
        }
        C.MATRICULAS.filter(m => m.assistidaId === a.id && (!cs || cs.includes(m.cursoId))).forEach(m => {
            const c = C.curso(m.cursoId);
            add(m.inicio, "curso", "Começou o curso " + c.nome, C.func(c.prof).nome);
            if (m.fim) add(m.fim, "curso", (m.situacao === "concluiu" ? "Concluiu " : m.situacao === "desistiu" ? "Participação interrompida no curso " : "Curso encerrado pela Casa: ") + c.nome + (m.motivoSaida && perfil === "professor" ? " (motivo: " + m.motivoSaida.toLowerCase() + ")" : ""), "");
        });
        if (perfil === "professor") return ev.sort((x, y) => x.data.localeCompare(y.data));
        const fj = C.JUR_FILA.find(f => f.assistidaId === a.id);
        const ats = C.JUR_ATEND.filter(x => x.assistidaId === a.id);
        if (fj && perfil === "recepcao") {
            if (fj.agendadoPara && fj.agendadoEm) add(fj.agendadoEm, "juridico", "Jurídico agendou o primeiro atendimento para " + fd(fj.agendadoPara), "");
            if (fj.atendidaEm) add(fj.atendidaEm, "juridico", "Primeiro atendimento realizado no Jurídico", "");
            if (fj.canceladoEm) add(fj.canceladoEm, "juridico", "Encaminhamento ao Jurídico cancelado", "");
        }
        if (fj && fj.atendidaEm && perfil !== "recepcao") {
            if (perfil === "juridico" || perfil === "adm") {
                ats.forEach((x, i) => { if (i === 0 || x.situacao !== ats[i - 1].situacao) add(x.data, "juridico", (i === 0 ? "Primeiro atendimento no Jurídico" : "Situação do caso mudou para") + " - " + x.situacao + " (" + x.protocolo + ")", C.func(x.profissional).nome); });
            } else add(fj.atendidaEm, "juridico", "Iniciou acompanhamento no Jurídico", "");
        }
        const pr = C.PSI_PRONT.find(x => x.assistidaId === a.id);
        if (pr && (perfil === "psicologia" || perfil === "juridico")) {
            // completo = psicóloga que pode ver as anotações; senão, só o que o Jurídico vê
            const completo = perfil === "psicologia" && (pr.responsavel === uid || pr.visibilidade === "equipe");
            const ss = C.PSI_SESS.filter(s => s.assistidaId === a.id && s.risco);
            add(pr.abertura, "psicologia", perfil === "psicologia" ? "Prontuário aberto " + pr.numero + (completo && ss.length ? " (risco inicial: " + ss[0].risco.toLowerCase() + ")" : "") : "Em acompanhamento na Psicologia", perfil === "psicologia" ? C.func(pr.responsavel).nome : "");
            ss.forEach((s, i) => {
                const antes = i ? ss[i - 1].risco : null;
                const grave = r => r === "Alto" || r === "Iminente";
                if (completo && s.risco !== antes && i > 0) add(s.data, "psicologia", "Risco reavaliado: " + antes + " → " + s.risco, C.func(s.psicologa).nome);
                if (!completo && grave(s.risco) && !grave(antes)) add(s.data, "psicologia", "Alerta: risco " + s.risco.toLowerCase() + " registrado pela Psicologia", "");
                if (!completo && !grave(s.risco) && grave(antes)) add(s.data, "psicologia", "Alerta de risco retirado pela Psicologia", "");
            });
            if (pr.encerramento) add(pr.encerramento.data, "psicologia", completo ? "Acompanhamento encerrado: " + pr.encerramento.motivo : "Acompanhamento na Psicologia encerrado", "");
        }
        return ev.sort((x, y) => x.data.localeCompare(y.data));
    }

    // registros sigilosos escolhidos para entrar no PDF (preenchido a cada geração)
    let incluidos = [];
    const marcado = (opc, chave) => opc.sel && opc.sel.has(chave);
    function seletorRegistro(chave, texto, pode) {
        return `<label class="rel-check rel-nao-imprimir rel-sel-reg"><input type="checkbox" data-reg="${chave}" ${pode ? "" : "disabled"}> ${texto}</label>`;
    }

    function render(el, cfg) {
        const { perfil, usuario, p } = cfg;
        const opc = cfg.opcoes || {};
        incluidos = [];
        const a = C.assistida(cfg.assistidaId);
        if (!a) { el.innerHTML = `<p class="soft-empty-state">Escolha uma assistida.</p>`; return; }
        const uid = usuario.id;
        const fimRef = p.fim;
        const pr = C.PSI_PRONT.find(x => x.assistidaId === a.id);
        const caso = C.JUR_CASOS.find(x => x.assistidaId === a.id);
        const risco = riscoAtual(a.id, fimRef);
        const riscoGrave = (risco === "Alto" || risco === "Iminente") && pr && !pr.encerramento;
        const cs = perfil === "professor" ? cursosDoUsuario(uid) : null;
        const mats = C.MATRICULAS.filter(m => m.assistidaId === a.id && (!cs || cs.includes(m.cursoId)));
        const inicioRef = perfil === "professor" ? mats.map(m => m.inicio).sort()[0] : a.entrada;
        // Período "Geral" começa na entrada dela, não no início do sistema
        const per = p.tipo === "geral" ? Object.assign({}, p, { ini: inicioRef, rotulo: "Geral: desde " + (perfil === "professor" ? "a entrada no curso" : "a entrada na Casa") + " (" + fd(inicioRef) + ")" }) : p;
        const tempoCasa = C.diffDias(a.entrada, C.HOJE);
        const tempoTxt = tempoCasa > 60 ? Math.floor(tempoCasa / 30.4) + " meses" : tempoCasa + " dias";

        let h = "";
        // ---- cabeçalho ----
        const badges = [];
        if (a.restrito && perfil !== "recepcao" && perfil !== "professor") badges.push(badge("Acesso restrito", "restrito"));
        if (perfil === "psicologia" && pr) badges.push(badge("Prontuário " + pr.numero, "psicologia"));
        if ((perfil === "psicologia" || perfil === "juridico") && riscoGrave) badges.push(badge("Risco " + risco, "risco"));
        if (perfil === "juridico" && pr && !pr.encerramento) badges.push(badge("Em acompanhamento na Psicologia", "psicologia"));
        if (perfil === "psicologia" && caso) badges.push(badge("Em acompanhamento no Jurídico", "juridico"));
        if (perfil === "juridico" && caso) badges.push(badge("Caso " + (C.JUR_ATEND.filter(x => x.casoId === caso.id).slice(-1)[0] || {}).situacao, "juridico"));
        const cursando = mats.filter(m => m.situacao === "cursando");
        if (cursando.length && perfil !== "professor") badges.push(badge(cursando.length === 1 ? "Cursando 1 curso" : "Cursando " + cursando.length + " cursos", "ok"));
        const contatoTxt = a.semTelefoneSeguro ? "Sem telefone seguro" : (a.contato === "Não ligar" ? "Não ligar" : "(11) 9****-**** · " + a.contato + (a.obsContato ? " (" + a.obsContato + ")" : ""));
        const quemCadastrou = perfil === "recepcao" && C.func(a.cadastradaPor).perfil === "psicologia" ? "Equipe técnica da Casa" : C.func(a.cadastradaPor).nome + " (" + ({ recepcao: "Recepção", juridico: "Jurídico", psicologia: "Psicologia" })[C.func(a.cadastradaPor).perfil] + ")";
        const ficha = perfil === "professor" ? [
            ["Idade", C.idade(a.nasc) + " anos"],
            ["Contato", a.contato === "Não ligar" || a.semTelefoneSeguro ? "Não ligar (falar só na aula)" : contatoTxt],
            ["Na turma desde", fd(inicioRef)],
            ["Cursos comigo", mats.length]
        ] : [
            ["CPF", a.cpf],
            ["Nascimento", fd(a.nasc) + " (" + C.idade(a.nasc) + " anos)"],
            ["Contato", contatoTxt],
            ["Endereço", a.enderecoSigiloso ? "Endereço sigiloso" : a.rua + " - " + a.bairro + ", " + a.cidade],
            ["Entrada na Casa", fd(a.entrada) + " · há " + tempoTxt],
            ["Cadastrada por", quemCadastrou]
        ];
        h += `<div class="rel-cabecalho"><h2>Relatório individual da assistida</h2><span class="rel-periodo">${esc(per.rotulo)}${per.tipo !== "geral" ? " · " + fd(per.ini) + " a " + fd(per.fim) : ""}</span></div>`;
        h += `<p class="rel-nome">${esc(a.nome)}</p><div class="rel-badges">${badges.join("")}</div><dl class="rel-ficha">${ficha.map(f => `<div><dt>${f[0]}</dt><dd>${esc(f[1])}</dd></div>`).join("")}</dl>`;
        if (a.restrito && (perfil === "adm" || perfil === "juridico" || perfil === "psicologia")) h += `<div class="soft-alert soft-alert--restrito" style="margin-top:14px">Caso com acesso restrito (${esc(perfil === "adm" ? "motivo visível só para Jurídico e Psicologia" : a.restrito)}). Esta assistida não aparece para Recepção, Professoras e outros perfis. Não imprima nem compartilhe fora da equipe.</div>`;
        if (riscoGrave && (perfil === "psicologia" || perfil === "juridico")) h += `<div class="soft-alert soft-alert--risco" style="margin-top:14px"><b>Risco ${risco.toLowerCase()} registrado pela Psicologia.</b> ${perfil === "juridico" ? "O Jurídico vê só este alerta, nunca o conteúdo das sessões." : "O alerta sai quando um risco menor for registrado."}<ul><li>Risco de vida ou crise: SAMU 192 ou acompanhar até o pronto-socorro.</li><li>Sofrimento intenso: CAPS de referência e CVV 188 (24 horas, gratuito).</li><li>Violência em curso: Polícia 190, Delegacia da Mulher e Jurídico da Casa.</li></ul></div>`;

        // ---- resumo do período ----
        const regsAll = registros(null, { assistidaId: a.id }).filter(r => !cs || cs.includes(r.cursoId));
        const regsP = regsAll.filter(r => C.dentro(r.data, per));
        const fP = freq(regsP);
        const atsAll = C.JUR_ATEND.filter(x => x.assistidaId === a.id);
        const atsP = atsAll.filter(x => C.dentro(x.data, per));
        const sessAll = C.PSI_SESS.filter(x => x.assistidaId === a.id);
        const sessP = sessAll.filter(x => C.dentro(x.data, per));
        const k = [];
        if (perfil === "professor" || perfil === "adm") k.push({ valor: regsP.length ? fP.pct + "%" : "-", rotulo: "Frequência nos cursos", ajuda: regsP.length ? fP.presente + " presenças em " + fP.total + " aulas" : "Sem aulas no período" });
        if (perfil === "professor") { k.push({ valor: fP.faltou, rotulo: "Faltas sem justificativa", alerta: fP.faltou >= 3 }); k.push({ valor: fP.justificada, rotulo: "Faltas justificadas" }); k.push({ valor: sequenciaFaltas(regsAll.sort((x, y) => x.data.localeCompare(y.data))), rotulo: "Faltas seguidas agora", ajuda: "Sem contar presenças depois" }); }
        if (perfil === "recepcao" || perfil === "adm") k.push({ valor: mats.length, rotulo: "Cursos desde a entrada", ajuda: mats.filter(m => m.situacao === "concluiu").length + " concluídos · " + mats.filter(m => m.situacao === "desistiu").length + " desistência(s)" });
        if (perfil === "juridico" || perfil === "adm") k.push({ valor: atsP.length, rotulo: "Atendimentos jurídicos no período", ajuda: atsAll.length + " desde a entrada" });
        if (perfil === "juridico") { const prox = atsAll.length ? atsAll[atsAll.length - 1].retorno : ""; k.push({ valor: prox ? fdc(prox) : "-", rotulo: "Próximo retorno", ajuda: prox ? (prox < C.HOJE ? "Já passou, sem novo registro" : "Agendado") : "Sem retorno marcado" }); }
        if (perfil === "psicologia") {
            const comp = sessP.filter(s => s.presenca === "Compareceu").length;
            k.push({ valor: sessP.length, rotulo: "Sessões no período", ajuda: sessAll.length + " desde a abertura" });
            k.push({ valor: sessP.length ? pct(comp, sessP.length) + "%" : "-", rotulo: "Comparecimento", ajuda: comp + " de " + sessP.length });
            k.push({ valor: risco || "-", rotulo: "Risco atual", alerta: riscoGrave });
        }
        if (perfil === "recepcao") { const encs = a.encaminhamentos.length; k.push({ valor: encs, rotulo: "Encaminhamentos feitos", ajuda: "Jurídico, equipe técnica e Assistência social" }); }
        if (perfil === "adm") { const encV = a.encaminhamentos.filter(e => e.tipo !== "psicologia"); k.push({ valor: encV.length, rotulo: "Encaminhamentos", ajuda: (encV.map(e => ({ juridico: "Jurídico", assistente_social: "Assistência social" })[e.tipo]).join(", ") || "Nenhum") + " (Psicologia não aparece por sigilo)" }); }
        h += `<div style="margin-top:20px">${kpis(k)}</div>`;

        // ---- trajetória ----
        const ev = marcos(a, perfil, uid).filter(e => C.dentro(e.data, per));
        const corpoTempo = ev.length ? `<ul class="rel-tempo">${ev.map(e => `<li class="area-${e.area}"><span class="rel-tempo-data">${fd(e.data)}</span>${esc(e.texto)}${e.quem ? ` <span class="rel-tempo-quem">· ${esc(e.quem)}</span>` : ""}</li>`).join("")}</ul>` : `<p class="soft-empty-state">Nenhum marco neste período.</p>`;
        const notaTempo = { recepcao: "Entrada, direcionamentos, encaminhamentos e cursos. A Recepção não vê nada da Psicologia (aparece como “Equipe técnica da Casa”) nem o conteúdo do Jurídico.", juridico: "Inclui o que o Jurídico pode ver das outras áreas: cursos e, da Psicologia, só se está em acompanhamento e o alerta de risco.", psicologia: "Da área jurídica aparece só que ela está em acompanhamento, sem conteúdo.", professor: "Só os cursos que você dá. Nada das outras áreas aparece para a professora.", adm: "A Coordenação vê metadados (datas, situação, protocolo), nunca relatos nem nada da Psicologia por assistida." }[perfil];
        h += `<div class="rel-grade" style="margin-top:6px">${bloco("Trajetória no período", notaTempo, corpoTempo, true)}</div>`;

        // ---- histórico detalhado da área ----
        h += `<div class="rel-grade" style="margin-top:1.4rem">`;
        if (perfil === "recepcao" || perfil === "adm") h += historicoCursosResumo(mats, per, perfil);
        if (perfil === "recepcao") h += historicoRecepcao(a, per);
        if (perfil === "professor") h += mats.map(m => historicoCurso(a, m, per)).join("");
        if (perfil === "juridico") h += historicoJuridico(a, caso, atsAll, per, uid, opc);
        if (perfil === "adm") h += historicoJuridicoMeta(atsAll, per) + bloco("Psicologia", "", `<div class="rel-sigilo">Pela regra de sigilo da Psicologia, a Coordenação não vê se uma assistida específica é atendida, nem as sessões. Os números da Psicologia aparecem só no Relatório Estatístico/Geral, sem nomes.</div>`) + bloco("Assistência social", "", a.encaminhamentos.some(e => e.tipo === "assistente_social") ? `<p>Encaminhada em ${fd(a.encaminhamentos.find(e => e.tipo === "assistente_social").data)}. <b>Sem confirmação de atendimento no sistema</b> (o perfil ainda não existe, então o atendimento pode ter acontecido sem registro).</p>` : `<p class="soft-empty-state">Sem encaminhamento.</p>`);
        if (perfil === "psicologia") h += historicoPsicologia(a, pr, sessAll, per, uid, opc);
        if (perfil === "juridico" || perfil === "psicologia") h += historicoCursosResumo(mats, per, perfil);
        h += `</div>`;
        const quaisSig = perfil === "psicologia" ? "evolução(ões)" : "relato(s)";
        h += rodape(usuario, incluidos.length ? `<b>Conteúdo sigiloso incluído: ${incluidos.length} ${quaisSig} (${incluidos.join(", ")}).</b>` : (perfil === "juridico" || perfil === "psicologia" ? "Sem relatos nem evoluções." : ""));
        el.innerHTML = h;
        // marca de novo as caixas escolhidas (a escolha vale só para esta geração)
        el.querySelectorAll("[data-reg]").forEach(ch => { ch.checked = marcado(opc, ch.dataset.reg); });
    }

    function historicoCursosResumo(mats, per, perfil) {
        const comFreq = perfil === "adm";
        const linhas = mats.map(m => {
            const c = C.curso(m.cursoId);
            const f = freq(registros(per, { assistidaId: m.assistidaId, cursoIds: [m.cursoId] }));
            const base = [esc(c.nome), fd(m.inicio), m.fim ? fd(m.fim) : "-", badgeSituacao(m.situacao)];
            return comFreq ? base.concat([f.total ? f.pct + "%" : "-", f.faltou, f.justificada]) : base;
        });
        const cols = [{ t: "Curso" }, { t: "Início" }, { t: "Término / saída" + novo() }, { t: "Situação" }];
        if (comFreq) cols.push({ t: "Frequência no período", num: true }, { t: "Faltas", num: true }, { t: "Justificadas", num: true });
        const nota = perfil === "adm" ? "Frequência calculada pela chamada da professora no período escolhido. O motivo de uma interrupção não aparece aqui." : perfil === "recepcao" ? "A frequência detalhada fica com a professora e a Coordenação." : "Regra inicial: só curso, situação e datas. Frequência, faltas, justificativas, observações e motivo da saída ficam com a professora; se for relevante, a equipe compartilha de forma específica.";
        return bloco("Cursos", nota, tabela(cols, linhas, { vazio: "Não está em nenhum curso." }), true);
    }

    function situacaoOperacional(f) {
        if (!f) return "-";
        if (f.canceladoEm) return badge("Cancelado em " + fd(f.canceladoEm), "restrito");
        if (f.atendidaEm) return badge("Primeiro atendimento realizado em " + fd(f.atendidaEm), "ok");
        if (f.agendadoPara) return badge("Agendado para " + fd(f.agendadoPara), "juridico");
        return badge("Aguardando atendimento");
    }

    function historicoRecepcao(a, per) {
        const ev = C.REC_EVENTOS.filter(e => e.assistidaId === a.id && C.dentro(e.data, per));
        const encs = a.encaminhamentos.map(e => {
            if (e.tipo === "juridico") { const f = C.JUR_FILA.find(x => x.assistidaId === a.id); return ["Jurídico", fd(e.data), situacaoOperacional(f)]; }
            if (e.tipo === "psicologia") return ["Equipe técnica", fd(e.data), badge("Encaminhada")];
            return ["Assistência social", fd(e.data), badge("Sem confirmação de atendimento no sistema")];
        });
        return bloco("Ações da Recepção no período", "Tudo o que foi feito na ficha pela Recepção (vem do histórico de ações).", tabela([{ t: "Data" }, { t: "Ação" }, { t: "Detalhe" }, { t: "Por" }], ev.map(e => [fd(e.data), esc(e.tipo), esc(e.detalhe), esc(C.func(e.por).perfil === "psicologia" ? "Equipe técnica da Casa" : C.func(e.por).nome)]), { vazio: "Nenhuma ação da Recepção no período." })) +
            bloco("Encaminhamentos (situação hoje)", "Situação operacional, vinda do registro do próprio Jurídico. Demanda, relato, processo, orientação e motivo de cancelamento não aparecem.", tabela([{ t: "Para" }, { t: "Encaminhada em" }, { t: "Situação" }], encs, { vazio: "Sem encaminhamentos." }));
    }

    function historicoCurso(a, m, per) {
        const c = C.curso(m.cursoId);
        const regs = registros(null, { assistidaId: a.id, cursoIds: [m.cursoId] }).sort((x, y) => x.data.localeCompare(y.data));
        const rP = regs.filter(r => C.dentro(r.data, per));
        const f = freq(rP);
        const faltas = rP.filter(r => r.status !== "presente");
        const obs = rP.filter(r => r.observacao && r.status === "presente" || (r.observacao && r.status === "faltou"));
        const seq = sequenciaFaltas(regs);
        let corpo = `<div class="rel-badges" style="margin-bottom:10px">${badgeSituacao(m.situacao)}${seq >= 3 && m.situacao === "cursando" ? badge(seq + " faltas seguidas", "restrito") : ""}</div>
            <dl class="rel-ficha" style="margin-top:0"><div><dt>Entrou no curso</dt><dd>${fd(m.inicio)}</dd></div><div><dt>${m.situacao === "desistiu" ? "Saiu em" : "Terminou em"}${novo()}</dt><dd>${m.fim ? fd(m.fim) : "Ainda cursando"}</dd></div>${m.motivoSaida ? `<div><dt>Motivo da saída${novo()}</dt><dd>${esc(m.motivoSaida)}</dd></div>` : ""}<div><dt>Aulas no período</dt><dd>${f.total}</dd></div></dl>
            <div style="margin:14px 0 6px">${pilha([{ rotulo: "Presente", valor: f.presente, cor: "var(--rel-ok)" }, { rotulo: "Falta justificada", valor: f.justificada, cor: "var(--rel-justificada)" }, { rotulo: "Faltou", valor: f.faltou, cor: "var(--rel-falta)" }])}</div>
            <h4 style="color:#8B586F;margin:16px 0 8px;font-size:0.92rem">Chamada aula a aula</h4>
            <div class="rel-chamada">${rP.map(r => `<span class="rel-dia ${r.status}" title="${fd(r.data)} - ${({ presente: "Presente", faltou: "Faltou", justificada: "Justificada" })[r.status]}${r.observacao ? ": " + esc(r.observacao) : ""} · Aula: ${esc(r.tema)}"><b>${({ presente: "P", faltou: "F", justificada: "J" })[r.status]}</b>${fdc(r.data)}</span>`).join("") || `<span class="soft-empty-state">Sem aulas no período.</span>`}</div>
            <div class="rel-legenda"><span><i style="background:var(--rel-ok)"></i>P = presente</span><span><i style="background:var(--rel-justificada)"></i>J = justificada</span><span><i style="background:var(--rel-falta)"></i>F = faltou</span></div>
            <h4 style="color:#8B586F;margin:18px 0 8px;font-size:0.92rem">Faltas e justificativas</h4>
            ${tabela([{ t: "Data" }, { t: "Aula" }, { t: "Registro" }, { t: "Justificativa / observação" }], faltas.map(r => [fd(r.data), esc(r.tema), r.status === "justificada" ? badge("Justificada", "ok") : badge("Faltou", "restrito"), esc(r.observacao || "-")]), { vazio: "Nenhuma falta no período." })}
            <h4 style="color:#8B586F;margin:18px 0 8px;font-size:0.92rem">Observações da professora sobre ela</h4>
            ${obs.length ? `<ul class="rel-tempo">${obs.map(r => `<li class="area-curso"><span class="rel-tempo-data">${fd(r.data)}</span>${esc(r.observacao)} <span class="rel-tempo-quem">· aula “${esc(r.tema)}”</span></li>`).join("")}</ul>` : `<p class="soft-empty-state">Sem observações no período.</p>`}`;
        return bloco(esc(c.nome), "Professora: " + esc(C.func(c.prof).nome) + " · aulas às " + ["domingos", "segundas", "terças", "quartas", "quintas", "sextas", "sábados"][c.dia], corpo, true);
    }

    function historicoJuridico(a, caso, ats, per, uid, opc) {
        if (!caso) return bloco("Jurídico", "", `<p>Encaminhada e ainda na fila do Jurídico.</p>`, true);
        const souResp = caso.responsaveis.includes(uid);
        const podeVer = caso.visibilidade === "equipe" || souResp;
        const atsP = ats.filter(x => C.dentro(x.data, per));
        const podeExportar = souResp;
        let cab = `<dl class="rel-ficha" style="margin-top:0"><div><dt>Responsáveis</dt><dd>${caso.responsaveis.map(r => esc(C.func(r).nome)).join(", ")}</dd></div><div><dt>Quem vê o conteúdo</dt><dd>${caso.visibilidade === "equipe" ? "Toda a equipe do Jurídico" : "Somente as responsáveis"}</dd></div>${podeVer ? `<div><dt>Tipo de demanda</dt><dd>${esc(caso.demanda)}</dd></div><div><dt>Processo / vara</dt><dd>${caso.processo ? esc(caso.processo) + "<br><small>" + esc(caso.vara) + "</small>" : "Sem processo"}</dd></div>` : ""}</dl>`;
        if (!podeVer) {
            return bloco("Atendimentos jurídicos", "", cab + `${opc.incluirRelatos ? `<div class="soft-alert rel-nao-imprimir" style="margin:12px 0">Marcar “Incluir relatos” não libera acesso: este PDF sai sem nenhum relato.</div>` : ""}<div class="rel-sigilo" style="margin:12px 0">Este caso está como <b>somente as responsáveis</b>. Você vê só protocolo, data, profissional e situação. Para ver o conteúdo, peça acesso de emergência (vale 12 horas, fica registrado e as responsáveis são avisadas).</div>` +
                tabela([{ t: "Protocolo" }, { t: "Data" }, { t: "Profissional" }, { t: "Situação" }], atsP.map(x => [x.protocolo, fd(x.data), esc(C.func(x.profissional).nome), esc(x.situacao)])) + `<p class="rel-nao-imprimir" style="margin-top:12px"><button class="soft-btn soft-btn-warning" type="button" onclick="alert('Protótipo: abre o pedido de acesso de emergência com motivo.')">Pedir acesso de emergência</button></p>`, true);
        }
        const encs = contar(atsP, x => x.encaminhamentos);
        const cards = atsP.map(x => `<div class="rel-cartao-atend"><header><b>${x.protocolo}</b><span>${fd(x.data)} às ${x.hora} · ${esc(C.func(x.profissional).nome)}</span></header>
            <div class="rel-badges">${badge(x.situacao, x.situacao === "Encerrado" ? "ok" : "juridico")}${x.medidaProtetiva === "Sim" ? badge("Medida protetiva", "restrito") : ""}</div>
            <dl><div><dt>Tipo de demanda</dt><dd>${esc(x.tipoDemanda)}</dd></div><div><dt>Medida protetiva</dt><dd>${esc(x.medidaProtetiva)}</dd></div><div><dt>Encaminhamentos</dt><dd>${x.encaminhamentos.length ? esc(x.encaminhamentos.join(", ")) : "-"}</dd></div><div><dt>Próximo passo</dt><dd>${esc(x.proximoPasso)}</dd></div><div><dt>Retorno</dt><dd>${x.retorno ? fd(x.retorno) : "-"}</dd></div></dl>
            ${opc.incluirRelatos ? seletorRegistro("J:" + x.protocolo, "Incluir o relato deste atendimento", podeExportar) : ""}
            ${opc.incluirRelatos && podeExportar && marcado(opc, "J:" + x.protocolo) ? (incluidos.push(x.protocolo), `<div class="rel-texto-sigiloso"><b>Relato e orientações:</b> ${esc(x.relato)}</div>`) : ""}
            ${x.adendos.map(ad => `<div class="rel-texto-sigiloso" style="border-color:#D9C6EA"><b>Adendo ${fd(ad.data)} · ${esc(C.func(ad.por).nome)}:</b> ${esc(ad.texto)}</div>`).join("")}
        </div>`).join("");
        const aviso = !opc.incluirRelatos ? "" : podeExportar
            ? `<div class="rel-sigilo rel-nao-imprimir" style="margin:12px 0">Escolha abaixo quais relatos entram no PDF. Nada vem marcado: inclua só o necessário para a finalidade. <button type="button" class="soft-btn soft-btn-secondary" style="margin-left:8px;padding:5px 12px" data-reg-todos="${atsP.map(x => "J:" + x.protocolo).join(",")}">Marcar todos do período</button></div>`
            : `<div class="rel-sigilo rel-nao-imprimir" style="margin:12px 0">Você pode ler este caso, mas só as responsáveis (${caso.responsaveis.map(r => esc(C.func(r).nome)).join(", ")}) podem incluir relatos num PDF. Marcar a opção não dá permissão.</div>`;
        return bloco("Atendimentos jurídicos", (opc.incluirRelatos ? "Relatos aparecem só nos atendimentos marcados (isso já é a prévia do que sai no papel)." : "Relatos ocultos. Marque “Incluir relatos” no topo para escolher quais entram.") + " Atendimentos não são editados; correções aparecem como adendo.", cab + aviso +
            `<div class="rel-kpis" style="margin-top:12px">${[["Atendimentos", atsP.length], ["Com medida protetiva", atsP.filter(x => x.medidaProtetiva === "Sim").length], ["Encaminhamentos externos", encs.reduce((s, x) => s + x.valor, 0)], ["Adendos", atsP.reduce((s, x) => s + x.adendos.length, 0)]].map(v => `<div class="rel-kpi"><span class="rel-kpi-valor">${v[1]}</span><span class="rel-kpi-rotulo">${v[0]}</span></div>`).join("")}</div>` +
            (cards || `<p class="soft-empty-state">Nenhum atendimento jurídico no período.</p>`), true);
    }

    function historicoJuridicoMeta(ats, per) {
        const atsP = ats.filter(x => C.dentro(x.data, per));
        return bloco("Jurídico (só metadados)", "A Coordenação vê protocolo, data, profissional e situação. O relato nunca aparece aqui.", tabela([{ t: "Protocolo" }, { t: "Data" }, { t: "Profissional" }, { t: "Situação" }], atsP.map(x => [x.protocolo, fd(x.data), esc(C.func(x.profissional).nome), esc(x.situacao)]), { vazio: ats.length ? "Nenhum atendimento jurídico no período." : "Sem atendimento jurídico." }), true);
    }

    function historicoPsicologia(a, pr, sess, per, uid, opc) {
        if (!pr) return bloco("Prontuário", "", `<p>Encaminhada e ainda na fila da Psicologia.</p>`, true);
        const podeVer = pr.responsavel === uid || pr.visibilidade === "equipe";
        const podeExportar = pr.responsavel === uid;
        const sP = sess.filter(s => C.dentro(s.data, per));
        const cab = `<dl class="rel-ficha" style="margin-top:0"><div><dt>Prontuário</dt><dd>${pr.numero}</dd></div><div><dt>Psicóloga responsável</dt><dd>${esc(C.func(pr.responsavel).nome)} · ${esc(C.func(pr.responsavel).crp)}</dd></div><div><dt>Aberto em</dt><dd>${fd(pr.abertura)}</dd></div><div><dt>Sigilo informado e aceito</dt><dd>${fd(pr.consentimento)} (1º atendimento)</dd></div><div><dt>Situação</dt><dd>${pr.encerramento ? "Encerrado em " + fd(pr.encerramento.data) + " · " + esc(pr.encerramento.motivo) : "Em acompanhamento"}</dd></div><div><dt>Quem vê as anotações</dt><dd>${pr.visibilidade === "equipe" ? "Toda a equipe de Psicologia" : "Somente a responsável"}</dd></div></dl>`;
        if (!podeVer) {
            return bloco("Prontuário psicológico", "", cab + `${opc.incluirEvolucao ? `<div class="soft-alert rel-nao-imprimir" style="margin:12px 0">Marcar “Incluir evoluções” não libera acesso: este PDF sai sem nenhuma evolução.</div>` : ""}<div class="rel-sigilo" style="margin:12px 0">As anotações deste prontuário são só da psicóloga responsável. Você vê que as sessões existiram. Em emergência, peça acesso (vale 12 horas, fica registrado e a responsável é avisada).</div>` +
                tabela([{ t: "Sessão" }, { t: "Data" }, { t: "Presença" }], sP.map(s => ["nº " + s.numero, fd(s.data), esc(s.presenca)])) + `<p class="rel-nao-imprimir" style="margin-top:12px"><button class="soft-btn soft-btn-warning" type="button" onclick="alert('Protótipo: abre o pedido de acesso de emergência com motivo.')">Pedir acesso de emergência</button></p>`, true);
        }
        const pres = contar(sP, s => s.presenca);
        const cor = { "Compareceu": "var(--rel-ok)", "Faltou e justificou": "var(--rel-justificada)", "Faltou": "var(--rel-falta)", "Remarcou": "var(--rel-neutro)" };
        const encs = contar(sP, s => s.encaminhamentos);
        let corpo = cab + `<div class="rel-grade" style="margin-top:14px">
            ${bloco("Presença nas sessões", "", pilha(["Compareceu", "Faltou e justificou", "Faltou", "Remarcou"].map(k => ({ rotulo: k, valor: (pres.find(x => x.rotulo === k) || { valor: 0 }).valor, cor: cor[k] }))))}
            ${bloco("Evolução do risco", "Uma barra por sessão, da mais antiga para a mais recente. Cinza: não houve avaliação (falta).", `<div class="rel-risco-linha">${sP.map(s => `<span class="${s.risco ? "risco-" + s.risco : "risco-nulo"}" title="Sessão ${s.numero} - ${fd(s.data)}: ${s.risco || s.presenca}"></span>`).join("")}</div><div class="rel-legenda"><span><i class="risco-Baixo"></i>Baixo</span><span><i class="risco-Moderado"></i>Moderado</span><span><i class="risco-Alto"></i>Alto</span><span><i class="risco-Iminente"></i>Iminente</span></div>`)}
            ${bloco("Modalidade e encaminhamentos externos", "", barras(contar(sP, s => s.modalidade)) + `<div style="margin-top:12px">${encs.length ? barras(encs) : '<p class="rel-nota">Sem encaminhamentos externos no período.</p>'}</div>`)}
            ${bloco("Declarações de comparecimento", "Emitidas sem nada do que foi dito.", `<p>${sP.filter(s => s.declaracao).length} no período${sP.filter(s => s.declaracao).length ? ": " + sP.filter(s => s.declaracao).map(s => fd(s.data)).join(", ") : ""}.</p>`)}
            </div>`;
        corpo += `<h4 style="color:#8B586F;margin:16px 0 8px;font-size:0.92rem">Sessões no período</h4>` + tabela(
            [{ t: "Nº" }, { t: "Data" }, { t: "Tipo" }, { t: "Presença" }, { t: "Modalidade" }, { t: "Risco" }, { t: "Próxima" }].concat(opc.incluirEvolucao ? [{ t: "Evolução e plano" }] : []),
            sP.map(s => { const chave = "P:" + s.numero; const vai = podeExportar && marcado(opc, chave); if (opc.incluirEvolucao && vai) incluidos.push("sessão nº " + s.numero); return [s.numero, fd(s.data), esc(s.tipo), esc(s.presenca), esc(s.modalidade), s.risco ? `<span class="soft-badge ${s.risco === "Alto" || s.risco === "Iminente" ? "soft-badge--risco" : ""}">${s.risco}</span>` : "-", fd(s.proxima)].concat(opc.incluirEvolucao ? [seletorRegistro(chave, "Incluir", podeExportar) + (vai ? `<div class="rel-texto-sigiloso" style="margin:4px 0 0">${esc(s.evolucao)}<br><small><b>Plano:</b> ${esc(s.plano)}</small>${s.demanda ? `<br><small><b>Demanda:</b> ${esc(s.demanda)}</small>` : ""}</div>` : "")] : []); }),
            { vazio: "Nenhuma sessão no período." });
        const aviso = !opc.incluirEvolucao ? "" : podeExportar
            ? `<div class="rel-sigilo rel-nao-imprimir" style="margin:12px 0">Escolha na tabela quais evoluções entram no PDF. Nada vem marcado: inclua só o necessário. Para mandar algo a outra instituição, use “Documento para outra instituição”. <button type="button" class="soft-btn soft-btn-secondary" style="margin-left:8px;padding:5px 12px" data-reg-todos="${sP.map(x => "P:" + x.numero).join(",")}">Marcar todas do período</button></div>`
            : `<div class="rel-sigilo rel-nao-imprimir" style="margin:12px 0">Só a psicóloga responsável (${esc(C.func(pr.responsavel).nome)}) pode incluir evoluções num PDF. Marcar a opção não dá permissão.</div>`;
        return bloco("Prontuário psicológico", opc.incluirEvolucao ? "Evoluções aparecem só nas sessões marcadas (isso já é a prévia do que sai no papel)." : "Evoluções ocultas. Marque “Incluir evoluções” no topo para escolher quais entram (só a psicóloga responsável).", aviso + corpo, true);
    }

    window.RU = { esc, fd, fdc, pct, n, novo, iniciarPagina, abas, controlePeriodo, kpis, barras, colunas, pilha, tabela, bloco, badge, rodape, seletorAssistida };
    window.EST = { registros, freq, procuraCursos, encaminhamentos, faixaEtaria, contar, porMes, riscoAtual, sequenciaFaltas, ativaEm };
    window.RI = { render, listaVisivel, cursosDoUsuario, incluidos: () => incluidos.slice() };
})();
