/*
 * Agenda e Calendário (portado do protótipo aprovado).
 * Toda regra de sigilo e de conflito roda no servidor (/api/agenda): esta tela só mostra
 * o que a API devolve para a pessoa logada e repassa as decisões (justificativa, exceção).
 * - Bloco "Agenda" no painel (#cardAgenda): Hoje, Próximos 7 dias e Pendências.
 * - "Calendário" no menu do nome (todas as páginas que carregam este arquivo): mini mês e calendário completo.
 */
(function () {
    "use strict";
    if (!window.CasaMulherAuth) return;

    const $ = (s, r) => (r || document).querySelector(s);
    const $$ = (s, r) => Array.from((r || document).querySelectorAll(s));
    const esc = s => String(s == null ? "" : s).replace(/[&<>"']/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
    const MESES = ["janeiro", "fevereiro", "março", "abril", "maio", "junho", "julho", "agosto", "setembro", "outubro", "novembro", "dezembro"];
    const SEM = ["dom", "seg", "ter", "qua", "qui", "sex", "sáb"];
    const SEM_LONGO = ["domingo", "segunda", "terça", "quarta", "quinta", "sexta", "sábado"];
    const FILTROS = { meus: "Meus compromissos", area: "Da minha área", equipe: "Avisos da equipe", compartilhados: "Compartilhados comigo", casa: "Feriados e dias da Casa", turmas: "Minhas turmas", outrasTurmas: "Aulas de outras turmas", esperadas: "Esperadas na unidade", contagem: "Atendimentos técnicos (só número)" };
    const LEGENDA = [["k-psi", "Psicologia"], ["k-jur", "Jurídico"], ["k-soc", "Serviço Social"], ["k-aula", "Aulas e comunicações"], ["k-man", "Compromissos e lembretes"], ["k-aus", "Ausências (tracejado = informada)"], ["k-equipe", "Esperada na unidade (Recepção)"], ["k-neu", "Indisponível / sem acesso / só número"], ["k-fer", "Feriado"], ["k-fec", "Unidade fechada"]];

    // ---------------- datas (horário de Brasília, sem fuso: a API manda o "relógio de parede") ----------------
    const pad = n => String(n).padStart(2, "0");
    const ymd = d => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
    const isoLocal = d => `${ymd(d)}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
    const lerIso = s => { if (!s) return null; const [a, b] = s.split("T"); const [y, m, d] = a.split("-").map(Number); const [h, mi] = (b || "00:00").split(":").map(Number); return new Date(y, m - 1, d, h || 0, mi || 0); };
    const lerData = (dStr, hStr) => lerIso(dStr + "T" + (hStr || "00:00"));
    const inicioDia = d => new Date(d.getFullYear(), d.getMonth(), d.getDate());
    const somaDias = (d, n) => new Date(d.getFullYear(), d.getMonth(), d.getDate() + n, d.getHours(), d.getMinutes());
    const mesmoDia = (a, b) => a && b && a.getFullYear() === b.getFullYear() && a.getMonth() === b.getMonth() && a.getDate() === b.getDate();
    const hm = d => `${pad(d.getHours())}:${pad(d.getMinutes())}`;
    const dm = d => `${pad(d.getDate())}/${pad(d.getMonth() + 1)}`;
    const inicioSemana = d => somaDias(inicioDia(d), -((d.getDay() + 6) % 7));

    const S = {
        me: null, agora: new Date(), hoje: inicioDia(new Date()), origens: null, pend: [], aba: "hoje",
        cache: {}, cal: { aberto: false, visao: "semana", ref: null, filtros: null, pessoas: new Set(), escolha: null, lista: null },
        mini: { ref: null, fixo: false, timer: null }
    };

    // ---------------- API ----------------
    async function api(caminho, opcoes) {
        const o = Object.assign({}, opcoes || {});
        let r;
        try { r = await CasaMulherAuth.apiFetch("/api/agenda" + caminho, o); } catch (e) { return { ok: false, status: 0, corpo: { mensagem: e.message } }; }
        let corpo = null;
        try { corpo = await r.json(); } catch { corpo = null; }
        return { ok: r.ok, status: r.status, corpo };
    }
    const post = (c, body) => api(c, { method: "POST", body: body || {} });
    function limparCache() { S.cache = {}; }
    async function itens(de, ate) {
        const k = ymd(de) + "|" + ymd(ate);
        if (!S.cache[k]) {
            S.cache[k] = api(`?de=${ymd(de)}&ate=${ymd(ate)}`).then(r => {
                if (!r.ok) { delete S.cache[k]; return { itens: [], contagens: {} }; }
                const lista = (r.corpo.itens || r.corpo.Itens || []).map(normalizar);
                return { itens: lista, contagens: r.corpo.contagens || {} };
            });
        }
        return S.cache[k];
    }
    // A API usa camelCase; aqui também convertemos as datas.
    function normalizar(x) {
        x.ini = lerIso(x.inicio);
        x.fim = x.fim ? lerIso(x.fim) : null;
        x.jIni = x.janelaInicio ? lerIso(x.janelaInicio) : null;
        x.jFim = x.janelaFim ? lerIso(x.janelaFim) : null;
        return x;
    }
    const doDia = (lista, d) => lista.filter(x => mesmoDia(x.ini, d));
    const filtrar = (lista, filtros) => lista.filter(x => x.grupo && filtros.has(x.grupo));

    // ---------------- textos ----------------
    const horaTxt = x => x.diaInteiro ? "dia todo" : hm(x.ini);
    const riscado = x => x.cancelado;
    const equipeAlheio = x => x.grupo === "equipe" && !x.meu;
    function curtoDe(x) { const p = (S.me.pessoas || []).find(p => p.id === x.donoId); return p ? p.curto : ""; }
    function tagHtml(x) {
        if (equipeAlheio(x)) return `<span class="ag-tag k-aus">aviso da equipe${x.ausencia === "informada" ? " · informada" : ""}</span>`;
        if (x.ausencia === "informada") return `<span class="ag-tag" style="--bg:#FFF4D1;--cor:#6B4F17">informada</span>`;
        if (x.nivel === "semacesso") return `<span class="ag-tag k-neu">sem acesso</span>`;
        if (x.chegada === "chegou") return `<span class="ag-tag" style="--bg:#E3EBF1;--cor:#4D6577">chegou</span>`;
        const t = (x.tags || []).find(t => ["chamada pendente", "resultado pendente", "compartilhado com você", "cancelada", "remarcado", "cancelado"].includes(t));
        if (t) return `<span class="ag-tag" style="--bg:#FFF4D1;--cor:#6B4F17">${esc(t)}</span>`;
        if (x.diaInteiro || (!x.fim && x.fonte !== "aula")) return `<span class="ag-tag">marco</span>`;
        return "";
    }
    function linhaHtml(x) {
        return `<li><button type="button" class="ag-linha ${x.cor} ${riscado(x) ? "riscado" : ""}" data-item="${esc(x.id)}"><span class="h">${horaTxt(x)}</span><span><span class="t">${esc(x.titulo)}</span><span class="s" style="display:block">${esc(x.sub || "")}</span></span>${tagHtml(x)}</button></li>`;
    }

    // ---------------- modal, aviso e descarte ----------------
    function toast(msg) {
        const t = document.createElement("div");
        t.className = "ag-toast"; t.setAttribute("role", "status"); t.textContent = msg; document.body.appendChild(t);
        setTimeout(() => t.remove(), 3600);
    }
    function modal(html, opts) {
        opts = opts || {};
        const m = document.createElement("div");
        m.className = "ag-modal";
        m.innerHTML = `<div class="ag-modal-c" role="dialog" aria-modal="true">${html}</div>`;
        const anterior = document.activeElement;
        const fechar = () => { m.remove(); if (anterior && anterior.focus && document.contains(anterior)) anterior.focus(); };
        const tentarFechar = depois => { const f = () => { fechar(); if (depois) depois(); }; if (opts.sujo && opts.sujo()) perguntarDescartar(f); else f(); };
        m.tentarFechar = tentarFechar;
        m.addEventListener("mousedown", e => { if (e.target === m) tentarFechar(); });
        m.addEventListener("click", e => { if (e.target.closest("[data-fechar]")) tentarFechar(); });
        m.addEventListener("keydown", e => { if (e.key === "Escape") { e.stopPropagation(); tentarFechar(); } });
        if (opts.acima) m.style.zIndex = 1700;
        document.body.appendChild(m);
        const f = m.querySelector("input:not([type=hidden]), select, textarea, button:not([data-fechar])") || m.querySelector(".ag-modal-c");
        if (f.classList.contains("ag-modal-c")) f.tabIndex = -1;
        f.focus();
        return { m, fechar, tentarFechar };
    }
    function perguntarDescartar(sim) {
        const p = document.createElement("div");
        p.className = "ag-modal"; p.style.zIndex = 1900;
        p.innerHTML = `<div class="ag-modal-c" role="alertdialog" aria-modal="true" style="max-width:420px"><h3>Descartar o que foi digitado?</h3><p>O que você estava preenchendo ainda não foi salvo.</p><div class="ag-acoes"><button type="button" class="soft-btn soft-btn-primary" data-continuar>Continuar editando</button><button type="button" class="soft-btn soft-btn-danger" data-descartar>Descartar</button></div></div>`;
        document.body.appendChild(p);
        $("[data-continuar]", p).focus();
        p.addEventListener("keydown", e => { if (e.key === "Escape") { e.stopPropagation(); p.remove(); } });
        p.addEventListener("click", e => { if (e.target.closest("[data-continuar]") || e.target === p) p.remove(); if (e.target.closest("[data-descartar]")) { p.remove(); sim(); } });
    }
    const msg = r => (r.corpo && (r.corpo.mensagem || r.corpo.title)) || (r.status === 403 ? "Você não tem permissão para isso." : r.status === 404 ? "Item não encontrado." : "Não foi possível concluir. Tente de novo.");
    async function depoisDeGravar(texto) { limparCache(); if (texto) toast(texto); await atualizarTudo(); }

    // ---------------- inicialização ----------------
    async function iniciar() {
        if (!CasaMulherAuth.getToken || !CasaMulherAuth.getToken()) return;
        const r = await api("/me");
        if (!r.ok || !r.corpo) return;
        S.me = r.corpo;
        S.agora = lerIso(S.me.agora) || new Date();
        S.hoje = inicioDia(S.agora);
        S.cal.ref = S.hoje;
        S.cal.filtros = new Set(S.me.filtrosPadrao);
        S.cal.lista = { de: somaDias(S.hoje, -30), ate: somaDias(S.hoje, 30) };
        S.mini.ref = new Date(S.hoje.getFullYear(), S.hoje.getMonth(), 1);
        ligarMenu();
        await atualizarTudo();
        setInterval(() => { limparCache(); atualizarTudo(); }, 5 * 60 * 1000);
    }
    async function atualizarTudo() {
        await renderAgenda();
        if (S.cal.aberto) await renderCal();
        if (!$("#agMini")?.classList.contains("hidden")) renderMini();
    }

    // ---------------- bloco Agenda (painel) ----------------
    async function renderAgenda() {
        const card = $("#cardAgenda");
        if (!card || !S.me) return;
        card.classList.remove("hidden");
        const filtros = new Set(S.me.filtrosPadrao);
        const [dados, pr] = await Promise.all([itens(S.hoje, somaDias(S.hoje, 7)), api("/pendencias")]);
        S.pend = pr.ok ? pr.corpo : [];
        const lista = filtrar(dados.itens, filtros);
        const hoje = lista.filter(x => mesmoDia(x.ini, S.hoje));
        const prox = lista.filter(x => x.ini >= somaDias(S.hoje, 1) && x.ini < somaDias(S.hoje, 8));
        let corpo = "";
        if (S.aba === "hoje") {
            let cnt = "";
            if (S.me.perfil === "adm") {
                const c = dados.contagens[ymd(S.hoje)] || { juridico: 0, psicologia: 0, servicoSocial: 0 };
                cnt = `<div class="ag-cnt" style="margin-bottom:10px"><span>Jurídico: ${c.juridico} atendimento(s)</span><span>Psicologia: ${c.psicologia}</span><span>Serviço Social: ${c.servicoSocial}</span></div><p class="ag-nota" style="margin:-4px 0 10px">Atendimentos técnicos aparecem só em número, sem nomes.</p>`;
            }
            corpo = cnt + (hoje.length ? `<ul class="ag-lista">${hoje.map(linhaHtml).join("")}</ul>` : `<div class="ag-vazio">Nada para hoje.</div>`);
        } else if (S.aba === "prox") {
            const por = {};
            prox.forEach(x => { const k = ymd(x.ini); (por[k] = por[k] || []).push(x); });
            corpo = Object.keys(por).sort().map(k => { const d = lerData(k); return `<div class="ag-dia-titulo">${SEM_LONGO[d.getDay()]}, ${dm(d)}</div><ul class="ag-lista">${por[k].map(linhaHtml).join("")}</ul>`; }).join("") || `<div class="ag-vazio">Nada nos próximos 7 dias.</div>`;
        } else {
            corpo = S.pend.length ? `<div style="display:flex;flex-direction:column;gap:8px">${S.pend.map(pendHtml).join("")}</div>` : `<div class="ag-vazio">Nenhuma pendência.</div>`;
        }
        const tm = S.me.tiposManuais.map(t => t[0]);
        const botoes = [`<button type="button" class="soft-btn soft-btn-primary" data-ag="novo">+ Novo compromisso</button>`];
        if (S.me.tiposAgendamento.length) botoes.push(`<button type="button" class="soft-btn soft-btn-secondary" data-ag="agendar">+ Novo agendamento</button>`);
        if (tm.includes("comunicacao")) botoes.push(`<button type="button" class="soft-btn soft-btn-secondary" data-ag="comunicar">Falta ou atraso de aluna</button>`);
        if (S.me.perfil === "adm") botoes.push(`<button type="button" class="soft-btn soft-btn-secondary" data-ag="config">Configuração da agenda</button>`);
        card.innerHTML = `<div class="ag-card-top"><h2 class="soft-card-title">Agenda</h2><div style="display:flex;gap:6px;flex-wrap:wrap">${botoes.join("")}</div></div>
            <div class="ag-subtabs" role="tablist">
                <button type="button" role="tab" aria-selected="${S.aba === "hoje"}" class="ag-subtab ${S.aba === "hoje" ? "active" : ""}" data-aba="hoje">Hoje <span class="n">${hoje.length}</span></button>
                <button type="button" role="tab" aria-selected="${S.aba === "prox"}" class="ag-subtab ${S.aba === "prox" ? "active" : ""}" data-aba="prox">Próximos 7 dias <span class="n">${prox.length}</span></button>
                <button type="button" role="tab" aria-selected="${S.aba === "pend"}" class="ag-subtab ${S.aba === "pend" ? "active" : ""}" data-aba="pend">Pendências <span class="n">${S.pend.length}</span></button>
            </div>${corpo}
            <p class="ag-nota">Para ver o mês, use <b>Calendário</b> no menu do seu nome (no relógio, no alto da tela).</p>`;
        if (!card.dataset.ligado) {
            card.dataset.ligado = "1";
            card.addEventListener("click", e => {
                const aba = e.target.closest("[data-aba]"); if (aba) { S.aba = aba.dataset.aba; renderAgenda(); return; }
                const it = e.target.closest("[data-item]"); if (it) { abrirDetalhe(it.dataset.item); return; }
                const p = e.target.closest("[data-pend]"); if (p) { acaoPendencia(p.dataset.pend, p.dataset.id); return; }
                const b = e.target.closest("[data-ag]"); if (!b) return;
                if (b.dataset.ag === "novo") abrirNovo({});
                if (b.dataset.ag === "agendar") abrirAgendamento({});
                if (b.dataset.ag === "comunicar") abrirNovo({ tipo: "comunicacao" });
                if (b.dataset.ag === "config") abrirConfig();
            });
        }
    }
    function pendHtml(p) {
        const b = (acao, txt) => `<button type="button" class="soft-btn soft-btn-secondary ag-btn-sm" data-pend="${acao}" data-id="${esc(p.id)}">${txt}</button>`;
        const rot = { recebida: "Marcar como recebida", resultado: "Registrar resultado", reavaliar: "Registrar reavaliação", ausOk: "Confirmar", ausNao: "Não procede", chamada: "Fazer chamada", cancelarAula: "Cancelar esta aula", agendar: "Agendar", assumir: "Assumir na minha agenda", remarcar: "Remarcar" };
        let acoes = (p.acoes || []).map(a => b(a, rot[a] || a)).join("");
        if (p.tipo === "reavaliacao") acoes += b("verReav", "Ver registros");
        return `<div class="ag-pend">${esc(p.texto)}${p.nota ? `<div class="nota">${esc(p.nota)}</div>` : ""}${acoes ? `<div class="acoes">${acoes}</div>` : ""}</div>`;
    }

    // ---------------- menu do usuário + mini calendário ----------------
    function ligarMenu() {
        const dd = $("#sessionDropdown"), card = $("#sessionCard");
        if (!dd || !card || $("#agCalItem")) return;
        const item = document.createElement("div");
        item.className = "ag-cal-item"; item.id = "agCalItem";
        item.innerHTML = `<button type="button" class="ag-cal-btn" id="agBtnCal" aria-haspopup="dialog" aria-expanded="false">📅 Calendário <span class="seta">mês ▸</span></button><div class="ag-mini hidden" id="agMini" role="dialog" aria-label="Mini calendário"></div>`;
        const acoes = $(".session-actions", dd);
        dd.insertBefore(item, acoes || null);
        const btn = $("#agBtnCal"), mini = $("#agMini");
        const abrirMini = fixo => { mini.classList.remove("hidden"); btn.setAttribute("aria-expanded", "true"); if (fixo) { S.mini.fixo = true; card.classList.add("open"); } renderMini(); };
        const fecharMini = () => { mini.classList.add("hidden"); btn.setAttribute("aria-expanded", "false"); S.mini.fixo = false; };
        S.fecharMini = fecharMini;
        item.addEventListener("mouseenter", () => { clearTimeout(S.mini.timer); S.mini.timer = setTimeout(() => abrirMini(false), 300); });
        item.addEventListener("mouseleave", () => { clearTimeout(S.mini.timer); if (!S.mini.fixo) S.mini.timer = setTimeout(() => mini.classList.add("hidden"), 250); });
        btn.addEventListener("click", e => {
            e.stopPropagation();
            if (window.matchMedia("(max-width: 720px)").matches) { abrirCal({ visao: "dia", data: S.hoje }); return; }
            if (mini.classList.contains("hidden") || !S.mini.fixo) { abrirMini(true); } else fecharMini();
        });
        document.addEventListener("mousedown", e => { if (!card.contains(e.target) && !e.target.closest(".ag-ov") && !e.target.closest(".ag-modal")) fecharMini(); });
        card.addEventListener("keydown", e => { if (e.key === "Escape" && !mini.classList.contains("hidden")) { fecharMini(); btn.focus(); } });
        mini.addEventListener("click", e => {
            e.stopPropagation();
            const nav = e.target.closest("[data-mes]");
            if (nav) { S.mini.ref = new Date(S.mini.ref.getFullYear(), S.mini.ref.getMonth() + (+nav.dataset.mes), 1); renderMini(); return; }
            const dia = e.target.closest("[data-dia]");
            if (dia) { abrirCal({ visao: "dia", data: lerData(dia.dataset.dia) }); return; }
            if (e.target.closest("#agMiniAbrir")) abrirCal({ visao: "semana", data: S.hoje });
        });
        mini.addEventListener("mouseover", e => { const d = e.target.closest("[data-dia]"); if (d) dica(d.dataset.dia); });
        mini.addEventListener("focusin", e => { const d = e.target.closest("[data-dia]"); if (d) dica(d.dataset.dia); });
        mini.addEventListener("keydown", e => {
            const d = e.target.closest("[data-dia]"); if (!d) return;
            const passo = { ArrowLeft: -1, ArrowRight: 1, ArrowUp: -7, ArrowDown: 7 }[e.key];
            if (!passo) return;
            e.preventDefault();
            const nd = somaDias(lerData(d.dataset.dia), passo);
            const foco = () => { const a = $(`[data-dia="${ymd(nd)}"]`, mini); if (a) a.focus(); };
            if (nd.getMonth() !== S.mini.ref.getMonth()) { S.mini.ref = new Date(nd.getFullYear(), nd.getMonth(), 1); renderMini().then(foco); } else foco();
        });
    }
    function gradeMes(ref) { const p = new Date(ref.getFullYear(), ref.getMonth(), 1); return somaDias(p, -((p.getDay() + 6) % 7)); }
    async function renderMini() {
        const mini = $("#agMini"); if (!mini) return;
        const ref = S.mini.ref, ini = gradeMes(ref);
        const dados = await itens(ini, somaDias(ini, 41));
        const vis = filtrar(dados.itens, new Set(S.me.filtrosPadrao));
        S.mini.dados = { vis, dados };
        let cel = ["seg", "ter", "qua", "qui", "sex", "sáb", "dom"].map(s => `<div class="sem">${s}</div>`).join("");
        for (let i = 0; i < 42; i++) {
            const d = somaDias(ini, i), its = doDia(vis, d);
            const casa = dados.itens.find(x => x.fonte === "casa" && mesmoDia(x.ini, d) && (x.tipo === "fechada" || x.tipo === "feriado"));
            const cores = []; its.forEach(x => { if (!cores.includes(x.cor)) cores.push(x.cor); });
            const pts = cores.slice(0, 3).map(k => `<i class="pt ${k}"></i>`).join("") + (its.length > 3 ? `<span class="mais">+${its.length - 3}</span>` : "");
            cel += `<button type="button" class="ag-mini-dia ${d.getMonth() !== ref.getMonth() ? "fora" : ""} ${mesmoDia(d, S.hoje) ? "hoje" : ""} ${casa ? (casa.tipo === "fechada" ? "fec" : "fer") : ""}" data-dia="${ymd(d)}" tabindex="${mesmoDia(d, S.hoje) ? 0 : -1}" aria-label="${SEM_LONGO[d.getDay()]} ${dm(d)}: ${its.length} item(ns)">${d.getDate()}<span class="pts">${pts}</span></button>`;
        }
        mini.innerHTML = `<div class="ag-mini-top"><button type="button" data-mes="-1" aria-label="Mês anterior">‹</button><span>${MESES[ref.getMonth()]} ${ref.getFullYear()}</span><button type="button" data-mes="1" aria-label="Próximo mês">›</button></div>
            <div class="ag-mini-grade">${cel}</div><div class="ag-mini-dica" id="agMiniDica" aria-live="polite">Passe o mouse ou use as setas num dia para ver o que tem nele.</div>
            <div class="ag-mini-rodape"><span>Clique num dia para abrir</span><button type="button" class="soft-btn soft-btn-primary ag-btn-sm" id="agMiniAbrir">Abrir completo</button></div>`;
    }
    function dica(diaStr) {
        const box = $("#agMiniDica"); if (!box || !S.mini.dados) return;
        const d = lerData(diaStr), its = doDia(S.mini.dados.vis, d);
        const linhas = its.slice(0, 6).map(x => `<li class="${x.cor}"><i></i><span><b>${horaTxt(x)}</b> ${esc(x.titulo)}</span></li>`).join("");
        let cnt = "";
        if (S.me.perfil === "adm") { const c = S.mini.dados.dados.contagens[ymd(d)]; const n = c ? c.juridico + c.psicologia + c.servicoSocial : 0; if (n) cnt = `<li class="k-neu"><i></i><span>${n} atendimento(s) técnico(s) (só números)</span></li>`; }
        box.innerHTML = `<b>${SEM_LONGO[d.getDay()]}, ${dm(d)}</b>${linhas || cnt ? `<ul>${cnt}${linhas}</ul>` : "<div>Nada neste dia.</div>"}${its.length > 6 ? `<div>…e mais ${its.length - 6}. Clique para ver tudo.</div>` : ""}`;
    }

    // ---------------- calendário completo ----------------
    let ov = null, fechandoPorVoltar = false;
    function abrirCal(o) {
        if (!ov || !document.body.contains(ov)) {
            ov = document.createElement("div");
            ov.className = "ag-ov hidden";
            ov.setAttribute("role", "dialog"); ov.setAttribute("aria-modal", "true"); ov.setAttribute("aria-label", "Calendário");
            document.body.appendChild(ov);
            ov.addEventListener("mousedown", e => { if (e.target === ov) fecharCal(); });
            ov.addEventListener("click", cliqueCal);
            ov.addEventListener("change", e => {
                const f = e.target.closest("[data-filtro]"); if (f) { f.checked ? S.cal.filtros.add(f.dataset.filtro) : S.cal.filtros.delete(f.dataset.filtro); renderCal(); }
                const p = e.target.closest("[data-pessoa]"); if (p) { p.checked ? S.cal.pessoas.add(p.dataset.pessoa) : S.cal.pessoas.delete(p.dataset.pessoa); renderCal(); }
                const l = e.target.closest("[data-lista]"); if (l && l.value) { S.cal.lista[l.dataset.lista] = lerData(l.value); renderCal(); }
            });
        }
        const C = S.cal;
        if (!C.aberto) C.opener = document.activeElement;
        if (o.acimaDeModal) {
            C.acima = true;
            C.anterior = C.aberto ? { visao: C.visao, ref: C.ref, escolha: C.escolha } : null;
            if (C.aberto) history.pushState({ calendario: true, escolhaSobre: true }, "", "#calendario");
        }
        C.visao = o.visao || "semana";
        C.ref = inicioDia(o.data || S.hoje);
        C.escolha = o.escolha || null;
        ov.style.zIndex = o.acimaDeModal ? 1600 : "";
        if (!C.aberto) history.pushState({ calendario: true }, "", "#calendario");
        C.aberto = true;
        if (S.fecharMini) S.fecharMini();
        const sc = $("#sessionCard"); if (sc) sc.classList.remove("open");
        ov.classList.remove("hidden");
        renderCal().then(() => { const x = $(".ag-ov-x", ov); if (x) x.focus(); });
    }
    function fecharCal() {
        const C = S.cal;
        if (!C.aberto) return;
        if (C.acima) {
            const ant = C.anterior;
            C.acima = false; C.anterior = null; ov.style.zIndex = "";
            if (ant) {
                Object.assign(C, ant);
                if (!fechandoPorVoltar && history.state && history.state.escolhaSobre) { S.ignorarVoltar = true; history.back(); }
                renderCal();
                const mm = $$(".ag-modal").pop(); if (mm) { const f0 = mm.querySelector("input, select"); if (f0) f0.focus(); }
                return;
            }
        }
        C.aberto = false; C.escolha = null;
        ov.classList.add("hidden");
        if (!fechandoPorVoltar && history.state && history.state.calendario) history.back();
        const o = C.opener; if (o && document.contains(o) && o.focus) o.focus();
    }
    document.addEventListener("keydown", e => {
        if (e.key === "Escape" && S.cal.aberto && (S.cal.acima || !document.querySelector(".ag-modal"))) fecharCal();
        if ((e.key === "Enter" || e.key === " ") && e.target.matches && e.target.matches("[data-livre]")) { e.preventDefault(); e.target.click(); }
    });
    window.addEventListener("popstate", () => {
        if (S.ignorarVoltar) { S.ignorarVoltar = false; return; }
        if (!S.cal.aberto) return;
        if (S.cal.acima) { fechandoPorVoltar = true; fecharCal(); fechandoPorVoltar = false; return; }
        const mods = $$(".ag-modal").filter(m => m.tentarFechar);
        if (mods.length) { history.pushState({ calendario: true }, "", "#calendario"); mods[mods.length - 1].tentarFechar(); return; }
        fechandoPorVoltar = true; fecharCal(); fechandoPorVoltar = false;
    });
    function periodoTxt() {
        const r = S.cal.ref;
        if (S.cal.visao === "dia") return `${SEM_LONGO[r.getDay()]}, ${dm(r)}/${r.getFullYear()}`;
        if (S.cal.visao === "semana") { const s = inicioSemana(r), f = somaDias(s, 6); return `${dm(s)} a ${dm(f)}/${f.getFullYear()}`; }
        if (S.cal.visao === "mes") return `${MESES[r.getMonth()]} ${r.getFullYear()}`;
        return "Lista";
    }
    function periodoDados() {
        const C = S.cal;
        if (C.visao === "dia") return [C.ref, C.ref];
        if (C.visao === "semana") { const s = inicioSemana(C.ref); return [s, somaDias(s, 6)]; }
        if (C.visao === "mes") { const s = gradeMes(C.ref); return [s, somaDias(s, 41)]; }
        return [C.lista.de, C.lista.ate];
    }
    async function renderCal() {
        if (!ov) return;
        const C = S.cal, E = C.escolha;
        const [de, ate] = periodoDados();
        const dados = await itens(de, ate);
        let faixas = null;
        if (E) {
            const r = await post("/disponibilidade", { pessoas: E.pessoas, assistidaId: E.assistidaId || null, de: ymd(de), ate: ymd(ate), ignorarItemId: E.ignorar || null });
            faixas = r.ok ? r.corpo.map(f => Object.assign(f, { ini: lerIso(f.inicio), fim: lerIso(f.fim) })) : [];
        }
        const visoes = E ? [["dia", "Dia"], ["semana", "Semana"]] : [["dia", "Dia"], ["semana", "Semana"], ["mes", "Mês"], ["lista", "Lista"]];
        const pessoas = (S.me.pessoas || []);
        const ctx = { dados, faixas };
        ov.innerHTML = `<div class="ag-ov-painel">
            <div class="ag-ov-top">
                <h2>Calendário</h2>
                <button type="button" class="ag-nav" data-nav="-1" aria-label="Anterior">‹</button><button type="button" class="ag-nav" data-nav="0">Hoje</button><button type="button" class="ag-nav" data-nav="1" aria-label="Próximo">›</button>
                <span class="per">${periodoTxt()}</span>
                <span class="ag-seg">${visoes.map(v => `<button type="button" data-visao="${v[0]}" class="${C.visao === v[0] ? "on" : ""}">${v[1]}</button>`).join("")}</span>
                ${E ? "" : `<button type="button" class="soft-btn soft-btn-primary ag-btn-sm" data-novo-cal>+ Novo compromisso</button>`}
                <button type="button" class="ag-ov-x" aria-label="Fechar calendário e voltar para onde eu estava" title="Fechar (Esc)">×</button>
            </div>
            ${E ? `<div class="ag-escolha-faixa"><b>Escolhendo horário para:</b> ${esc(E.rotulo)} (${E.dur} min${E.desl ? " + " + E.desl + " min de deslocamento" : ""}). Clique num horário livre para preencher o formulário. Cinza = indisponível (o motivo só aparece se você puder vê-lo). Amarelo = ausência só informada: o horário pode ser escolhido, com aviso.</div>` : ""}
            <div class="ag-ov-corpo">
                <aside class="ag-ov-lado">
                    ${E ? `<h4>Agendas consideradas</h4><div>${E.pessoas.map(pid => pid === S.me.id ? "Você" : esc((pessoas.find(p => p.id === pid) || {}).curto || "")).join(", ")}${E.assistidaNome ? " e a assistida <b>" + esc(E.assistidaNome) + "</b>" : ""}.</div><p class="ag-nota">Só aparecem as pessoas deste compromisso: consultar a disponibilidade de alguém exige marcar algo com ela.</p><p class="ag-nota">A conferência usa também o que você não pode ver: oculto não quer dizer livre.</p>` : `
                    <h4>Mostrar</h4>${S.me.filtrosDisponiveis.map(f => `<label><input type="checkbox" data-filtro="${f}" ${C.filtros.has(f) ? "checked" : ""}> ${FILTROS[f] || f}</label>`).join("")}
                    <h4>Pessoas (lado a lado no Dia)</h4>${pessoas.map(p => `<label><input type="checkbox" data-pessoa="${esc(p.id)}" ${C.pessoas.has(p.id) ? "checked" : ""}> ${esc(p.curto)} <span style="color:#B79AAA;font-size:.75rem">(${esc(p.area)})</span></label>`).join("")}
                    <p class="ag-nota">Aqui aparece só o que você já pode ver dessas pessoas (avisos, reuniões, casos da sua área). Atendimentos técnicos não aparecem nem como “Ocupado”: a disponibilidade de uma colega só é consultada ao marcar algo com ela (“Ver disponibilidade”).</p>`}
                    <h4>Legenda</h4><div class="leg">${LEGENDA.map(l => `<i class="${l[0]}"></i><span>${l[1]}</span>`).join("")}</div>
                </aside>
                <div class="ag-ov-area" id="agCalArea">${C.visao === "mes" ? mesHtml(ctx) : C.visao === "lista" ? listaHtml(ctx) : gradeHtml(ctx)}</div>
            </div>
            <div class="ag-ov-rodape"><span>Fechar com ×, Esc ou Voltar do navegador mantém a tela de baixo como estava.</span><span>Sessão expirada ou navegador fechado perdem o que não foi salvo.</span></div>
        </div>`;
        const area = $("#agCalArea", ov);
        if (C.visao === "semana" || C.visao === "dia") area.scrollTop = 44;
    }
    let H0 = 7, H1 = 20;
    const PX = 44;
    // A grade vai das 7h às 20h, e se estende quando há algo antes ou depois (nada fica fora da tela).
    function ajustarHoras(lista, dias) {
        H0 = 7; H1 = 20;
        const naGrade = x => !x.diaInteiro && x.fim && dias.some(d => mesmoDia(x.ini, d));
        lista.filter(naGrade).forEach(x => { const a = x.jIni || x.ini, b = x.jFim || x.fim; H0 = Math.min(H0, a.getHours()); H1 = Math.max(H1, mesmoDia(a, b) ? Math.ceil(b.getHours() + b.getMinutes() / 60) : 24); });
        if (dias.some(d => mesmoDia(d, S.hoje))) { H0 = Math.min(H0, S.agora.getHours()); H1 = Math.max(H1, Math.min(24, S.agora.getHours() + 1)); }
    }
    const topo = d => ((d.getHours() + d.getMinutes() / 60) - H0) * PX;
    function lanes(evs) {
        evs.sort((a, b) => a.a - b.a);
        let grupo = [], fimG = 0;
        const fechar = () => { const fins = []; grupo.forEach(e => { let i = fins.findIndex(f => f <= e.a); if (i < 0) { i = fins.length; fins.push(0); } fins[i] = e.b; e.lane = i; }); grupo.forEach(e => e.nl = fins.length); grupo = []; fimG = 0; };
        evs.forEach(e => { if (grupo.length && e.a >= fimG) fechar(); grupo.push(e); fimG = Math.max(fimG, +e.b); });
        if (grupo.length) fechar();
        return evs;
    }
    function evHtml(e, extra) {
        const x = e.x, fim = x.fim || somaDias(x.ini, 0);
        const t0 = Math.max(topo(x.ini), 0), h = Math.max(topo(fim) - topo(x.ini), 20);
        const L = `left:calc(${e.lane / e.nl * 100}% + 2px);width:calc(${100 / e.nl}% - 4px)`;
        const desloc = x.jIni ? `<div class="ag-faixa" style="top:${topo(x.jIni)}px;height:${topo(x.ini) - topo(x.jIni)}px;${L};opacity:.7">desloc.</div><div class="ag-faixa" style="top:${topo(fim)}px;height:${topo(x.jFim) - topo(fim)}px;${L};opacity:.7">desloc.</div>` : "";
        const linhas = h < 32 ? 1 : h < 58 ? 2 : 3;
        const sub = esc(x.sub || "");
        const conteudo = linhas === 1 ? `<span class="l1"><b>${hm(x.ini)}</b> ${esc(x.titulo)}${extra || ""}</span>`
            : linhas === 2 ? `<span class="l1"><b>${hm(x.ini)}</b> ${esc(x.titulo)}${extra || ""}</span><span class="l2">${sub}</span>`
            : `<span class="l1"><b>${hm(x.ini)}–${hm(fim)}</b>${extra || ""}</span><span class="l1">${esc(x.titulo)}</span><span class="l2">${sub}</span>`;
        return desloc + `<button type="button" class="ag-ev ${x.cor} ${riscado(x) ? "riscado" : ""} ${x.ausencia === "informada" ? "informada" : ""}" style="top:${t0}px;height:${h - 2}px;${L}" data-item="${esc(x.id)}" title="${esc(x.titulo + (x.sub ? " — " + x.sub : ""))}">${conteudo}</button>`;
    }
    function chipHtml(x) {
        const aviso = equipeAlheio(x);
        const hora = x.diaInteiro ? "" : hm(x.ini) + (aviso && x.fim ? "–" + hm(x.fim) : "") + " ";
        return `<button type="button" class="ag-chip ${x.cor} ${riscado(x) ? "riscado" : ""}" data-item="${esc(x.id)}" title="${esc(x.titulo + (x.sub ? " — " + x.sub : ""))}">${aviso ? "Aviso · " + esc(curtoDe(x)) + " " : ""}${hora}${esc(x.titulo)}</button>`;
    }
    const foraExp = () => { const i = +(S.me.expedienteInicio || "08:00").split(":")[0], f = +(S.me.expedienteFim || "17:00").split(":")[0]; return `<div class="fora" style="top:0;height:${(i - H0) * PX}px"></div><div class="fora" style="top:${(f - H0) * PX}px;height:${(H1 - f) * PX}px"></div>`; };
    const linhaAgora = d => mesmoDia(d, S.hoje) ? `<div class="ag-agora" style="top:${topo(S.agora)}px"></div>` : "";
    const fechadaNoDia = (dados, d) => dados.itens.some(x => x.fonte === "casa" && x.tipo === "fechada" && mesmoDia(x.ini, d) && !(x.tags || []).includes("reaberta"));
    function gradeHtml(ctx) {
        const C = S.cal, E = C.escolha, dados = ctx.dados;
        let colunas;
        if (C.visao === "dia") colunas = [{ dia: C.ref, pessoa: null, nome: "Você" }].concat(E ? [] : Array.from(C.pessoas).map(pid => ({ dia: C.ref, pessoa: pid, nome: ((S.me.pessoas || []).find(p => p.id === pid) || {}).curto || "" })));
        else { const s = inicioSemana(C.ref); colunas = [0, 1, 2, 3, 4, 5, 6].map(i => ({ dia: somaDias(s, i), pessoa: null })); }
        ajustarHoras(E ? ctx.faixas.map(f => ({ ini: f.ini, fim: f.fim })) : dados.itens, colunas.map(c => c.dia));
        const larg = colunas.map(c => C.visao === "semana" && [0, 6].includes(c.dia.getDay()) ? "0.55fr" : "1fr").join(" ");
        let cab = `<div class="cab"></div>`, todo = `<div class="todo" style="font-size:.68rem;color:#B79AAA;justify-content:center">dia todo</div>`, cols = "";
        colunas.forEach(c => {
            const d = c.dia, fec = fechadaNoDia(dados, d);
            cab += `<div class="cab ${mesmoDia(d, S.hoje) && !c.pessoa ? "hoje" : ""}">${c.pessoa ? esc(c.nome) : dm(d)}<small>${SEM[d.getDay()]}${c.pessoa ? " " + dm(d) : ""}</small></div>`;
            const todos = [], timed = [];
            if (E) {
                dados.itens.filter(x => x.fonte === "casa" && mesmoDia(x.ini, d)).forEach(x => todos.push(x));
                const lim = v => Math.min(Math.max(v, 0), (H1 - H0) * PX);
                const fx = ctx.faixas.filter(f => f.ini < somaDias(d, 1) && f.fim > d);
                let html = fx.map(f => { const a = f.ini < d ? d : f.ini, b = f.fim > somaDias(d, 1) ? somaDias(d, 1) : f.fim; const tb = mesmoDia(b, d) ? lim(topo(b)) : (H1 - H0) * PX; return `<div class="ag-faixa ${f.aviso ? "inf" : f.revelado ? "rev " + f.cor : ""}" style="top:${lim(topo(a))}px;height:${Math.max(tb - lim(topo(a)), 16)}px">${!mesmoDia(f.ini, f.fim) || (f.ini <= d && f.fim >= somaDias(d, 1)) ? "Dia inteiro" : hm(f.ini)} ${esc(f.rotulo)}</div>`; }).join("");
                const [ei, ef] = [S.me.expedienteInicio || "08:00", S.me.expedienteFim || "17:00"].map(t => { const [h, m] = t.split(":").map(Number); return h * 60 + m; });
                if (!fec && ![0, 6].includes(d.getDay())) {
                    for (let m = ei; m + E.dur <= ef; m += 30) {
                        const ini = new Date(d.getFullYear(), d.getMonth(), d.getDate(), Math.floor(m / 60), m % 60);
                        const ja = new Date(+ini - E.desl * 60000), jb = new Date(+ini + (E.dur + E.desl) * 60000);
                        if (ini < S.agora) continue;
                        if (fx.some(f => !f.aviso && f.ini < jb && ja < f.fim)) continue;
                        const comAviso = fx.some(f => f.aviso && f.ini < jb && ja < f.fim);
                        html += `<div class="ag-livre ${comAviso ? "aviso" : ""}" tabindex="0" role="button" data-livre="${isoLocal(ini)}" data-h="${hm(ini)} livre${comAviso ? " · com aviso" : ""}" style="top:${topo(ini) + 1}px;height:${Math.min(E.dur, 30) / 60 * PX - 2}px" aria-label="Escolher ${SEM[ini.getDay()]} ${dm(ini)} às ${hm(ini)}"></div>`;
                    }
                }
                if (E.atual && mesmoDia(E.atual.ini, d)) html += `<div class="ag-faixa novo" style="top:${topo(E.atual.ini)}px;height:${Math.max(topo(E.atual.fim) - topo(E.atual.ini), 16)}px;left:40%">horário no formulário</div>`;
                todo += `<div class="todo">${todos.map(chipHtml).join("")}</div>`;
                cols += `<div class="col ${fec ? "fec" : ""}" style="height:${(H1 - H0) * PX}px">${foraExp()}${html}${linhaAgora(d)}</div>`;
                return;
            }
            let xs;
            if (!c.pessoa) xs = filtrar(doDia(dados.itens, d), C.filtros);
            else xs = doDia(dados.itens, d).filter(x => x.nivel === "detalhe" && x.fonte !== "casa" && (x.pessoas || []).includes(c.pessoa));
            xs.forEach(x => { if (x.diaInteiro || !x.fim || (equipeAlheio(x) && !c.pessoa)) todos.push(x); else timed.push({ x, a: x.jIni || x.ini, b: x.jFim || x.fim }); });
            let cnt = "";
            if (S.me.perfil === "adm" && C.filtros.has("contagem") && !c.pessoa) { const k = dados.contagens[ymd(d)]; const n = k ? k.juridico + k.psicologia + k.servicoSocial : 0; if (n) cnt = `<span class="ag-chip k-neu" title="Só números">${n} atend. técnicos: J ${k.juridico} · P ${k.psicologia} · S ${k.servicoSocial}</span>`; }
            lanes(timed);
            todo += `<div class="todo">${cnt}${todos.map(chipHtml).join("")}</div>`;
            cols += `<div class="col ${fec ? "fec" : ""}" style="height:${(H1 - H0) * PX}px">${foraExp()}${timed.map(e => evHtml(e, !e.x.meu && e.x.donoId && C.visao === "semana" && e.x.nivel === "detalhe" && e.x.fonte !== "aula" ? " · " + esc(curtoDe(e.x)) : "")).join("")}${linhaAgora(d)}</div>`;
        });
        let horas = `<div class="horas" style="height:${(H1 - H0) * PX}px">`;
        for (let h = H0; h < H1; h++) horas += `<div class="hl" style="top:${(h - H0) * PX}px">${h}h</div>`;
        horas += "</div>";
        return `<div class="ag-grade" style="grid-template-columns:52px ${larg};min-width:${C.visao === "semana" ? 720 : colunas.length > 1 ? colunas.length * 200 : 0}px">${cab}${todo}${horas}${cols}</div>`;
    }
    function mesHtml(ctx) {
        const r = S.cal.ref, ini = gradeMes(r), dados = ctx.dados;
        let h = ["seg", "ter", "qua", "qui", "sex", "sáb", "dom"].map(s => `<div class="sem">${s}</div>`).join("");
        for (let i = 0; i < 42; i++) {
            const d = somaDias(ini, i), xs = filtrar(doDia(dados.itens, d), S.cal.filtros);
            const casa = dados.itens.find(x => x.fonte === "casa" && mesmoDia(x.ini, d) && x.tipo !== "evento");
            let extra = "";
            if (S.me.perfil === "adm" && S.cal.filtros.has("contagem")) { const k = dados.contagens[ymd(d)]; const n = k ? k.juridico + k.psicologia + k.servicoSocial : 0; if (n) extra = `<span class="ag-chip k-neu">${n} atend. técnicos</span>`; }
            h += `<div class="d ${d.getMonth() !== r.getMonth() ? "fora" : ""} ${mesmoDia(d, S.hoje) ? "hoje" : ""} ${casa ? (casa.tipo === "fechada" ? "fec" : "fer") : ""}"><span class="n" data-irdia="${ymd(d)}" role="button" tabindex="0">${d.getDate()}</span>${extra}${xs.slice(0, 4).map(chipHtml).join("")}${xs.length > 4 ? `<span class="ag-chip" data-irdia="${ymd(d)}" style="--bg:#fff;--cor:#8F7181">+${xs.length - 4} mais</span>` : ""}</div>`;
        }
        return `<div class="ag-mes">${h}</div>`;
    }
    function listaHtml(ctx) {
        const L = S.cal.lista, xs = filtrar(ctx.dados.itens, S.cal.filtros);
        const por = {};
        xs.forEach(x => { const k = ymd(x.ini); (por[k] = por[k] || []).push(x); });
        return `<div class="ag-lv"><div class="ag-form" style="max-width:420px"><div><label>De</label><input type="date" class="soft-input" data-lista="de" value="${ymd(L.de)}"></div><div><label>Até</label><input type="date" class="soft-input" data-lista="ate" value="${ymd(L.ate)}"></div></div>
            <p class="ag-nota">A lista abre nos últimos 30 dias e nos próximos 30 (até 62 dias por vez). O tempo de exibição não define quando os registros são apagados.</p>
            ${Object.keys(por).sort().map(k => { const d = lerData(k); return `<h3>${SEM_LONGO[d.getDay()]}, ${dm(d)}</h3><ul class="ag-lista">${por[k].map(linhaHtml).join("")}</ul>`; }).join("") || `<div class="ag-vazio">Nada no período.</div>`}</div>`;
    }
    function cliqueCal(e) {
        const t = e.target, C = S.cal;
        if (t.closest(".ag-ov-x")) return fecharCal();
        const nav = t.closest("[data-nav]");
        if (nav) { const n = +nav.dataset.nav; if (!n) C.ref = S.hoje; else C.ref = C.visao === "mes" ? new Date(C.ref.getFullYear(), C.ref.getMonth() + n, 1) : somaDias(C.ref, n * (C.visao === "dia" ? 1 : 7)); return renderCal(); }
        const vis = t.closest("[data-visao]"); if (vis) { C.visao = vis.dataset.visao; return renderCal(); }
        const ir = t.closest("[data-irdia]"); if (ir) { C.ref = lerData(ir.dataset.irdia); C.visao = "dia"; return renderCal(); }
        if (t.closest("[data-novo-cal]")) return abrirNovo({ data: C.ref });
        const livre = t.closest("[data-livre]"); if (livre) { const d = lerIso(livre.dataset.livre); const cb = C.escolha.aoEscolher; fecharCal(); cb(d); return; }
        const ev = t.closest("[data-item]"); if (ev) return abrirDetalhe(ev.dataset.item);
    }

    // ---------------- detalhe ----------------
    const numero = id => +String(id).split(":")[1];
    async function abrirDetalhe(id) {
        const r = await api("/itens/" + encodeURIComponent(id));
        if (!r.ok) { toast(r.status === 404 ? "Este compromisso não está disponível para você." : msg(r)); return; }
        const d = r.corpo, x = normalizar(d.item);
        const rot = { chegou: "Chegou", recebida: "Marcar como recebida", resultado: "Registrar resultado", remarcar: "Remarcar", cancelarAgendamento: "Cancelar agendamento", ausOk: "Confirmar ausência", ausNao: "Não procede", cancelarManual: "Cancelar compromisso", cancelarSerie: "Cancelar…", cancelarAula: "Cancelar aula…", desfazerAula: "Desfazer cancelamento" };
        const perigo = ["cancelarAgendamento", "cancelarManual", "cancelarSerie", "cancelarAula", "ausNao"];
        const acoes = (d.acoes || []).map(a => `<button type="button" class="soft-btn ${perigo.includes(a) ? "soft-btn-danger" : a === "chegou" || a === "ausOk" ? "soft-btn-primary" : "soft-btn-secondary"}" data-acao="${a}">${rot[a] || a}</button>`).join("");
        const { m, fechar } = modal(`<h3>${esc(x.titulo)}<button type="button" data-fechar aria-label="Fechar">×</button></h3>
            <dl class="ag-det">${(d.linhas || []).map(l => `<dt>${esc(l[0])}</dt><dd>${esc(l[1]).replace(/\n/g, "<br>")}</dd>`).join("")}</dl>
            ${d.nota ? `<p class="ag-nota">${esc(d.nota)}</p>` : ""}
            <div class="ag-acoes"><button type="button" class="soft-btn soft-btn-secondary" data-fechar>Fechar</button>${acoes}</div>`);
        m.addEventListener("click", ev => { const b = ev.target.closest("[data-acao]"); if (b) { fechar(); acaoItem(b.dataset.acao, x); } });
    }
    async function acaoItem(acao, x) {
        const n = numero(x.id);
        if (acao === "chegou") { const r = await post(`/agendamentos/${n}/chegou`); return r.ok ? depoisDeGravar("Chegada registrada. A profissional responsável vê isso na Agenda dela.") : (toast(msg(r)), depoisDeGravar()); }
        if (acao === "recebida") { const r = await post(`/agendamentos/${n}/recebida`); return r.ok ? depoisDeGravar("Marcada como recebida.") : toast(msg(r)); }
        if (acao === "resultado") return abrirResultado(n);
        if (acao === "remarcar") return abrirAgendamento({ remarcarDeId: n, assistidaId: x.assistidaId, tipo: x.tipo, data: x.ini });
        if (acao === "cancelarAgendamento") return confirmarCom("Cancelar agendamento", "O agendamento fica no histórico como cancelado. A assistida sai das esperadas da Recepção.", "Motivo (opcional, não aparece para a Recepção)", false, async t => { const r = await post(`/agendamentos/${n}/cancelar`, { escopo: "esta", motivo: t }); return r; }, "Agendamento cancelado.");
        if (acao === "ausOk" || acao === "ausNao") { const r = await post(`/compromissos/${n}/ausencia/${acao === "ausOk" ? "confirmar" : "recusar"}`); return r.ok ? depoisDeGravar(acao === "ausOk" ? "Ausência confirmada." : "Marcada como não procede.") : toast(msg(r)); }
        if (acao === "cancelarManual") return confirmarCom("Cancelar compromisso", "O compromisso fica no histórico como cancelado.", null, false, () => post(`/compromissos/${n}/cancelar`, { escopo: "esta" }), "Compromisso cancelado.");
        if (acao === "cancelarSerie") return cancelarSerie(n);
        if (acao === "cancelarAula") return abrirCancelarAula(x.id);
        if (acao === "desfazerAula") { const p = x.id.split(":"); const r = await post("/aulas/desfazer", { cursoId: +p[1], data: p[2] }); return r.ok ? depoisDeGravar("Cancelamento desfeito (continua no histórico).") : toast(msg(r)); }
    }
    function confirmarCom(titulo, texto, rotuloCampo, obrigatorio, enviar, ok) {
        const { m, fechar } = modal(`<h3>${esc(titulo)}<button type="button" data-fechar aria-label="Fechar">×</button></h3><p>${esc(texto)}</p>
            ${rotuloCampo ? `<form class="ag-form"><div class="full"><label for="agCampo">${esc(rotuloCampo)}</label><input id="agCampo" class="soft-input" maxlength="300"></div></form>` : ""}
            <div class="ag-erro" id="agErro"></div>
            <div class="ag-acoes"><button type="button" class="soft-btn soft-btn-secondary" data-fechar>Voltar</button><button type="button" class="soft-btn soft-btn-danger" id="agOk">Confirmar</button></div>`);
        $("#agOk", m).addEventListener("click", async () => {
            const v = rotuloCampo ? $("#agCampo", m).value.trim() : "";
            if (obrigatorio && !v) { $("#agErro", m).textContent = "Preencha o campo."; return; }
            const r = await enviar(v);
            if (!r.ok) { $("#agErro", m).textContent = msg(r); return; }
            fechar(); depoisDeGravar(ok);
        });
    }
    function cancelarSerie(n) {
        const { m, fechar } = modal(`<h3>Cancelar compromisso que se repete<button type="button" data-fechar aria-label="Fechar">×</button></h3>
            <form class="ag-form"><div class="full"><label class="linha-check"><input type="radio" name="esc" value="esta" checked> Só esta data</label><label class="linha-check"><input type="radio" name="esc" value="seguintes"> Esta e as próximas</label><label class="linha-check"><input type="radio" name="esc" value="todas"> Todas as futuras da série</label><div class="ajuda">Datas que já passaram ficam no histórico.</div></div></form>
            <div class="ag-erro" id="agErro"></div><div class="ag-acoes"><button type="button" class="soft-btn soft-btn-secondary" data-fechar>Voltar</button><button type="button" class="soft-btn soft-btn-danger" id="agOk">Cancelar</button></div>`);
        $("#agOk", m).addEventListener("click", async () => {
            const r = await post(`/compromissos/${n}/cancelar`, { escopo: $("input[name=esc]:checked", m).value });
            if (!r.ok) { $("#agErro", m).textContent = msg(r); return; }
            fechar(); depoisDeGravar(`${r.corpo.canceladas} data(s) cancelada(s).`);
        });
    }
    function abrirResultado(n) {
        const { m, fechar } = modal(`<h3>Registrar resultado<button type="button" data-fechar aria-label="Fechar">×</button></h3>
            <form class="ag-form"><div class="full"><label for="agRes">O que aconteceu</label><select id="agRes" class="soft-input"><option value="realizado">Realizado</option><option value="falta_registrada">Falta registrada</option><option value="cancelado">Cancelado</option></select><div class="ajuda">Para remarcar, use Remarcar no detalhe. Horário passado sem registro não significa falta.</div></div></form>
            <div class="ag-erro" id="agErro"></div><div class="ag-acoes"><button type="button" class="soft-btn soft-btn-secondary" data-fechar>Voltar</button><button type="button" class="soft-btn soft-btn-primary" id="agOk">Salvar</button></div>`);
        $("#agOk", m).addEventListener("click", async () => {
            const r = await post(`/agendamentos/${n}/resultado`, { situacao: $("#agRes", m).value });
            if (!r.ok) { $("#agErro", m).textContent = msg(r); return; }
            fechar(); depoisDeGravar("Resultado registrado.");
        });
    }
    function abrirCancelarAula(id) {
        const p = id.split(":"), cursoId = +p[1], data = p[2];
        let sujo = false;
        const { m, fechar } = modal(`<h3>Cancelar aula de ${dm(lerData(data))}<button type="button" data-fechar aria-label="Fechar">×</button></h3>
            <form class="ag-form" id="agF"><div class="full"><label class="linha-check"><input type="radio" name="esc" value="esta" checked> Só esta aula</label><label class="linha-check"><input type="radio" name="esc" value="seguintes"> Esta e as próximas</label>
            <div class="ajuda">Cancelar aulas <b>não encerra a turma</b>: as alunas continuam matriculadas. Aulas com chamada feita não são canceladas.</div></div>
            <div class="full"><label for="agMot">Motivo (aparece para as alunas na Recepção)</label><input id="agMot" class="soft-input" maxlength="300"></div></form>
            <div class="ag-erro" id="agErro"></div><div class="ag-acoes"><button type="button" class="soft-btn soft-btn-secondary" data-fechar>Voltar</button><button type="button" class="soft-btn soft-btn-danger" id="agOk">Cancelar aula</button></div>`, { sujo: () => sujo });
        $("#agF", m).addEventListener("input", () => { sujo = true; });
        $("#agOk", m).addEventListener("click", async () => {
            const motivo = $("#agMot", m).value.trim();
            if (!motivo) { $("#agErro", m).textContent = "Informe o motivo."; return; }
            const r = await post("/aulas/cancelar", { cursoId, data, escopo: $("input[name=esc]:checked", m).value, motivo });
            if (!r.ok) { $("#agErro", m).textContent = msg(r); return; }
            fechar(); depoisDeGravar("Aula cancelada. A turma continua ativa.");
        });
    }

    // ---------------- pendências ----------------
    async function acaoPendencia(acao, id) {
        const n = numero(id);
        if (acao === "recebida" || acao === "resultado" || acao === "ausOk" || acao === "ausNao") return acaoItem(acao, { id });
        if (acao === "cancelarAula") return abrirCancelarAula(id);
        if (acao === "chamada") { window.location.href = "professor.html"; return; }
        if (acao === "agendar") return abrirAgendamento({ assistidaId: n });
        if (acao === "assumir") return assumir(n, null);
        if (acao === "remarcar") {
            const r = await api("/itens/" + encodeURIComponent(id));
            if (!r.ok) { toast(msg(r)); return; }
            return abrirAgendamento({ remarcarDeId: n, assistidaId: r.corpo.item.assistidaId, tipo: r.corpo.item.tipo });
        }
        if (acao === "reavaliar") return abrirReavaliacao(n);
        if (acao === "verReav") return verReavaliacoes(n);
    }
    async function assumir(n, justificativa) {
        const r = await post(`/revisoes/${n}/assumir`, { justificativa });
        if (r.ok) return depoisDeGravar("Compromisso assumido: agora está na sua agenda.");
        if (r.status === 409) {
            const oc = r.corpo.ocorrencias[0];
            return mostrarConflitos([oc], false, res => assumir(n, res[0].justificativa));
        }
        toast(msg(r)); depoisDeGravar();
    }
    function abrirReavaliacao(aid) {
        let sujo = false;
        const { m, fechar } = modal(`<h3>Registrar reavaliação de risco<button type="button" data-fechar aria-label="Fechar">×</button></h3>
            <form class="ag-form" id="agF"><div class="full"><label for="agCl">Classificação atual</label><select id="agCl" class="soft-input"><option value="baixo">Baixo</option><option value="moderado">Moderado</option><option value="alto" selected>Alto</option><option value="iminente">Iminente</option></select><div class="ajuda">Registrar a reavaliação resolve a pendência mesmo com o risco igual. O alerta de risco do prontuário continua sendo retirado só por uma sessão posterior com risco menor.</div></div>
            <div class="full"><label for="agPx">Próxima ação</label><input id="agPx" class="soft-input" maxlength="300" placeholder="Ex.: sessão extra na sexta; contato com a rede"></div></form>
            <p class="ag-nota">Situações urgentes seguem o fluxo de atendimento da Casa (CAPS, SAMU 192, CVV 188).</p>
            <div class="ag-erro" id="agErro"></div><div class="ag-acoes"><button type="button" class="soft-btn soft-btn-secondary" data-fechar>Voltar</button><button type="button" class="soft-btn soft-btn-primary" id="agOk">Registrar</button></div>`, { sujo: () => sujo });
        $("#agF", m).addEventListener("input", () => { sujo = true; });
        $("#agOk", m).addEventListener("click", async () => {
            const proximaAcao = $("#agPx", m).value.trim();
            if (!proximaAcao) { $("#agErro", m).textContent = "Informe a próxima ação."; return; }
            const r = await post("/reavaliacoes", { assistidaId: aid, classificacao: $("#agCl", m).value, proximaAcao });
            if (!r.ok) { $("#agErro", m).textContent = msg(r); return; }
            fechar(); depoisDeGravar("Reavaliação registrada.");
        });
    }
    async function verReavaliacoes(aid) {
        const r = await api("/reavaliacoes/" + aid);
        if (!r.ok) { toast(msg(r)); return; }
        modal(`<h3>Reavaliações registradas<button type="button" data-fechar aria-label="Fechar">×</button></h3>
            ${r.corpo.length ? `<dl class="ag-det">${r.corpo.map(x => { const d = lerIso(x.em); return `<dt>${dm(d)} ${hm(d)}</dt><dd>${esc(x.classificacao)} · ${esc(x.profissional)}<br>Próxima ação: ${esc(x.proximaAcao)}</dd>`; }).join("")}</dl>` : `<div class="ag-vazio">Nenhuma reavaliação registrada.</div>`}
            <div class="ag-acoes"><button type="button" class="soft-btn soft-btn-secondary" data-fechar>Fechar</button></div>`);
    }

    // ---------------- conferência antes de salvar ----------------
    /**
     * Mostra os conflitos de cada data (vindos do servidor). Cada data é decidida separadamente:
     * bloqueio só passa com exceção da ADM (ligada a este compromisso); "precisa de confirmação" pede justificativa.
     * aoSalvar recebe [{ indice, justificativa, excecaoMotivo }] das datas incluídas.
     */
    function mostrarConflitos(ocs, serie, aoSalvar) {
        const adm = S.me.perfil === "adm";
        const titulos = { bloqueio: "Bloqueado", confirmar: "Precisa de confirmação", aviso: "Aviso" };
        const tem = (o, n) => o.conflitos.some(c => c.nivel === n);
        const lista = cf => `<ul class="ag-conf">${cf.map(c => `<li class="${c.nivel}"><b>${titulos[c.nivel]}</b>${esc(c.mensagem)}</li>`).join("")}</ul>`;
        const ctrl = (o, i) => {
            let h = "";
            if (tem(o, "bloqueio")) {
                if (adm && o.conflitos.some(c => c.unidadeFechada)) h += `<label class="linha-check"><input type="checkbox" data-exc="${i}"> Autorizar exceção só para este compromisso${serie ? " nesta data" : ""}</label><input type="text" class="soft-input" data-excm="${i}" maxlength="300" placeholder="Motivo da exceção (obrigatório)"><div class="ajuda">A unidade continua fechada para as demais pessoas. Para reabrir para todos, use Configuração da agenda.</div>`;
                else h += `<p class="ag-nota" style="margin:4px 0">${serie ? "Esta data não será criada." : "Escolha outro dia."} Só a ADM pode autorizar uma exceção, e ela vale só para o compromisso autorizado.</p>`;
            }
            if (tem(o, "confirmar")) h += `<input type="text" class="soft-input" data-just="${i}" maxlength="300" placeholder="Justificativa curta ${serie ? "para esta data" : "para confirmar"} (obrigatória)">`;
            return h;
        };
        const corpo = serie
            ? `<p>Conferi as <b>${ocs.length} datas</b>, uma por uma. A justificativa ou autorização de uma data não vale para as outras.</p>
               <table class="ag-serie">${ocs.map((o, i) => `<tr><td><label class="linha-check"><input type="checkbox" data-inc="${i}" ${tem(o, "bloqueio") ? "disabled" : "checked"}> ${esc(o.rotulo)}</label></td><td>${o.conflitos.length ? lista(o.conflitos) + ctrl(o, i) : `<span class="ok">Sem conflito</span>`}</td></tr>`).join("")}</table>`
            : lista(ocs[0].conflitos) + ctrl(ocs[0], 0) + (tem(ocs[0], "confirmar") ? `<p class="ag-nota">A confirmação e a justificativa ficam no histórico de ações.</p>` : "");
        const { m, fechar } = modal(`<h3>Antes de salvar<button type="button" data-fechar aria-label="Fechar">×</button></h3>${corpo}
            <div class="ag-acoes"><button type="button" class="soft-btn soft-btn-secondary" data-fechar>Voltar e escolher outro horário</button><button type="button" class="soft-btn soft-btn-primary" id="agCfOk"></button></div>`);
        const ok = $("#agCfOk", m);
        const incluida = i => serie ? $(`[data-inc="${i}"]`, m).checked : (!tem(ocs[0], "bloqueio") || !!($(`[data-exc="0"]`, m) || {}).checked);
        const atualizarBotao = () => {
            ocs.forEach((o, i) => { const ex = $(`[data-exc="${i}"]`, m), inc = $(`[data-inc="${i}"]`, m); if (inc && tem(o, "bloqueio")) { inc.disabled = !(ex && ex.checked); if (inc.disabled) inc.checked = false; } });
            const n = ocs.filter((o, i) => incluida(i)).length;
            ok.disabled = !n; ok.style.display = !serie && !n ? "none" : "";
            ok.textContent = serie ? `Salvar ${n} de ${ocs.length} datas` : tem(ocs[0], "confirmar") || tem(ocs[0], "bloqueio") ? "Confirmar mesmo assim" : "Salvar";
        };
        m.addEventListener("change", e => { const ex = e.target.closest("[data-exc]"); if (ex && serie) $(`[data-inc="${ex.dataset.exc}"]`, m).checked = ex.checked; atualizarBotao(); });
        atualizarBotao();
        const primeiro = $("input:not([disabled])", m) || (ok.style.display === "none" ? $(".ag-acoes [data-fechar]", m) : ok); primeiro.focus();
        ok.addEventListener("click", () => {
            const res = [];
            for (let i = 0; i < ocs.length; i++) {
                if (!incluida(i)) continue;
                const o = ocs[i], r0 = { indice: i };
                if (tem(o, "confirmar")) { const j = $(`[data-just="${i}"]`, m); r0.justificativa = j.value.trim(); if (!r0.justificativa) { j.focus(); return toast(`Escreva a justificativa${serie ? " de " + o.rotulo : ""}.`); } }
                const ex = $(`[data-exc="${i}"]`, m);
                if (ex && ex.checked) { const mo = $(`[data-excm="${i}"]`, m); if (!mo.value.trim()) { mo.focus(); return toast("Informe o motivo da exceção."); } r0.excecaoMotivo = mo.value.trim(); }
                res.push(r0);
            }
            fechar(); aoSalvar(res);
        });
    }
    /** Confere no servidor; se houver qualquer conflito (inclusive aviso), mostra antes de salvar. */
    async function conferirESalvar(base, ocs, salvar) {
        const r = await post("/conferir", Object.assign({}, base, { ocorrencias: ocs }));
        if (!r.ok) { toast(msg(r)); return; }
        const conf = r.corpo;
        if (conf.every(o => !o.conflitos.length)) return salvar(ocs);
        mostrarConflitos(conf, ocs.length > 1, res => salvar(res.map(x => Object.assign({}, ocs[x.indice], { justificativa: x.justificativa || null, excecaoMotivo: x.excecaoMotivo || null }))));
    }

    /** O servidor recusou algumas datas (conflito novo desde a conferência): decide só essas e reenvia. */
    function refazerComDecisoes(problemas, lista, salvar) {
        const chave = s => String(s || "").slice(0, 16);
        mostrarConflitos(problemas, lista.length > 1, res => {
            const decididas = {};
            res.forEach(x => { decididas[chave(problemas[x.indice].inicio)] = x; });
            const nova = [];
            lista.forEach(o => {
                const k = chave(o.inicio.length === 10 ? o.inicio + "T00:00" : o.inicio);
                const problema = problemas.some(p => chave(p.inicio) === k);
                if (!problema) { nova.push(o); return; }
                const d = decididas[k];
                if (d) nova.push(Object.assign({}, o, { justificativa: d.justificativa || o.justificativa || null, excecaoMotivo: d.excecaoMotivo || o.excecaoMotivo || null }));
            });
            if (nova.length) salvar(nova);
        });
    }

    // ---------------- novo compromisso ----------------
    async function origens() { if (!S.origens) { const r = await api("/origens"); S.origens = r.ok ? r.corpo : []; } return S.origens; }
    async function abrirNovo(pre) {
        const tipos = S.me.tiposManuais;
        const refs = await origens();
        let comOpcoes = [];
        if (tipos.some(t => t[0] === "comunicacao")) { const r = await api("/comunicacoes/opcoes"); comOpcoes = r.ok ? r.corpo : []; }
        const dt = pre.data && pre.data > S.hoje ? pre.data : somaDias(S.hoje, 1);
        const outras = S.me.pessoas || [];
        const html = `<h3>Novo compromisso<button type="button" data-fechar aria-label="Fechar">×</button></h3>
        <form class="ag-form" id="nF" autocomplete="off">
            <div class="full"><label for="nTipo">Tipo</label><select id="nTipo" class="soft-input">${tipos.map(t => `<option value="${t[0]}" ${pre.tipo === t[0] ? "selected" : ""}>${esc(t[1])}</option>`).join("")}</select></div>
            <div class="full" data-so="comunicacao"><label for="nAluna">Aluna</label><select id="nAluna" class="soft-input">${comOpcoes.map(a => `<option value="${a.assistidaId}">${esc(a.identificacao)}</option>`).join("") || `<option value="">Nenhuma aluna com aula nos próximos dias</option>`}</select></div>
            <div class="full" data-so="comunicacao"><label for="nAula">Aula</label><select id="nAula" class="soft-input"></select></div>
            <div class="full" data-so="comunicacao"><label for="nCom">O que ela comunicou</label><select id="nCom" class="soft-input"><option value="falta">Vai faltar</option><option value="atraso">Vai chegar atrasada</option></select><div class="ajuda">O motivo pessoal não é registrado aqui. A professora vê só quem, qual aula e o fato. Não marca a chamada.</div></div>
            <div class="full" data-nao="comunicacao"><label for="nTit">Título</label><input id="nTit" class="soft-input" maxlength="120" placeholder="Ex.: Volto às 15h"></div>
            <div class="full" data-so="ausencia"><label for="nNome">Em nome de</label><select id="nNome" class="soft-input"><option value="${esc(S.me.id)}">Eu mesma</option>${outras.map(p => `<option value="${esc(p.id)}">${esc(p.nome)} (${esc(p.area)})</option>`).join("")}</select><div class="ajuda">Aviso de colega fica como <b>informado</b> até ela (ou a ADM) confirmar. Não cancela compromissos nem bloqueia a agenda dela.</div></div>
            <div data-nao="comunicacao"><label for="nData">Data</label><input id="nData" type="date" class="soft-input" value="${ymd(dt)}"></div>
            <div data-nao="comunicacao"><label class="linha-check" style="margin-top:24px"><input type="checkbox" id="nTodo"> Dia inteiro</label><div class="ajuda" id="nTodoAj"></div></div>
            <div data-nao="comunicacao" data-hora><label for="nIni">Início</label><input id="nIni" type="time" class="soft-input" value="09:00" step="900"></div>
            <div data-nao="comunicacao" data-hora><label for="nFim">Fim</label><input id="nFim" type="time" class="soft-input" value="10:00" step="900"></div>
            <div class="full" data-nao="comunicacao"><label for="nLocal">Local (opcional)</label><input id="nLocal" class="soft-input" maxlength="120"><div class="ajuda">Informar a sala não reserva a sala.</div></div>
            ${refs.length ? `<div class="full" data-nao="comunicacao ausencia"><label for="nRef">Assistida vinculada (opcional)</label><select id="nRef" class="soft-input"><option value="">Nenhuma</option>${refs.map(r => `<option value="${r.assistidaId}">${esc(r.identificacao)} · ${esc(r.referencia || "")}</option>`).join("")}</select><div class="ajuda">Só aparecem casos e prontuários em que você tem permissão. Quem perder a permissão no caso deixa de ver o detalhe, mesmo tendo criado o compromisso.</div></div>` : ""}
            <div class="full ag-bloco" data-nao="comunicacao ausencia lembrete prazo"><label>Participantes</label><div style="display:flex;flex-wrap:wrap;gap:4px 14px">${outras.map(p => `<label class="linha-check"><input type="checkbox" data-npart="${esc(p.id)}"> ${esc(p.curto)}</label>`).join("")}</div>
                <div class="ajuda"><b>Participar ocupa a agenda</b>: o sistema confere a disponibilidade de você e de cada participante.</div>
                <button type="button" class="soft-btn soft-btn-secondary ag-btn-sm" id="nDisp" style="margin-top:6px">🗓 Ver disponibilidade</button></div>
            <div class="full" data-nao="comunicacao"><label for="nVis">Quem pode ver</label><select id="nVis" class="soft-input"><option value="eu">Só quem participa</option><option value="area">Minha área</option><option value="pessoas">Pessoas escolhidas</option><option value="equipe">Toda a equipe</option></select><div class="ajuda">Só ver <b>não ocupa</b> a agenda de ninguém e não entra na conferência de horários dela.</div></div>
            <div class="full" data-pessoas><label>Pessoas que podem ver</label><div style="display:flex;flex-wrap:wrap;gap:4px 14px">${outras.map(p => `<label class="linha-check"><input type="checkbox" data-np="${esc(p.id)}"> ${esc(p.curto)}</label>`).join("")}</div></div>
            <div data-nao="comunicacao ausencia"><label for="nRep">Repetir</label><select id="nRep" class="soft-input"><option value="">Não repete</option><option value="4">Toda semana, 4 vezes</option><option value="8">Toda semana, 8 vezes</option></select><div class="ajuda">Cada data é conferida separadamente antes de salvar.</div></div>
            <div class="full ag-nota" id="nAviso" style="margin:0"></div>
            <div class="full ag-erro" id="nErro"></div>
            <div class="full ag-acoes"><button type="button" class="soft-btn soft-btn-secondary" data-fechar>Cancelar</button><button type="submit" class="soft-btn soft-btn-primary">Salvar</button></div>
        </form>`;
        let sujo = false;
        const { m, fechar } = modal(html, { sujo: () => sujo });
        const f = $("#nF", m), V = q => $(q, m);
        f.addEventListener("input", () => { sujo = true; });
        const tipo = () => V("#nTipo").value;
        const ocupaTipo = t => !["lembrete", "prazo", "comunicacao"].includes(t);
        const ref = () => V("#nRef") ? V("#nRef").value : "";
        const ajustar = () => {
            const t = tipo(), vis = V("#nVis").value, todo = V("#nTodo").checked, emNome = V("#nNome").value !== S.me.id && t === "ausencia";
            $$("[data-so]", m).forEach(e => e.style.display = e.dataset.so === t ? "" : "none");
            $$("[data-nao]", m).forEach(e => e.style.display = e.dataset.nao.split(" ").includes(t) ? "none" : "");
            $$("[data-hora]", m).forEach(e => { if (t !== "comunicacao") e.style.display = todo ? "none" : ""; });
            $("[data-pessoas]", m).style.display = vis === "pessoas" && t !== "comunicacao" ? "" : "none";
            V("#nTodoAj").textContent = !todo ? "" : ocupaTipo(t) ? "Ocupa o dia inteiro na agenda." : "Fica como marco do dia (não ocupa horário).";
            const eq = V("#nVis option[value=equipe]");
            eq.disabled = !!ref() && t !== "ausencia";
            if (eq.disabled && vis === "equipe") V("#nVis").value = "pessoas";
            V("#nAviso").textContent = ref() && t !== "ausencia" ? "Com assistida vinculada, “Toda a equipe” fica indisponível e só vê o detalhe quem tem permissão no caso/prontuário." : emNome ? "Vai aparecer como ausência informada para " + (outras.find(p => p.id === V("#nNome").value) || {}).curto + ", registrada por você." : "";
            if (t === "ausencia" && !sujo) V("#nVis").value = "equipe";
            if (t === "comunicacao") preencherAulas();
        };
        const preencherAulas = () => {
            const a = comOpcoes.find(o => String(o.assistidaId) === V("#nAluna").value);
            V("#nAula").innerHTML = a ? a.aulas.map(x => `<option value="${esc(x[0])}">${esc(x[1])}</option>`).join("") : "";
        };
        ["#nTipo", "#nVis", "#nRef", "#nTodo", "#nNome"].forEach(q => { const e = V(q); if (e) e.addEventListener("change", ajustar); });
        V("#nAluna").addEventListener("change", preencherAulas);
        ajustar();
        const participantes = () => $$("[data-npart]:checked", m).map(x => x.dataset.npart);
        V("#nDisp").addEventListener("click", () => {
            const data = V("#nData").value, ini = lerData(data, V("#nIni").value), fim = lerData(data, V("#nFim").value);
            const dur = Math.max(15, Math.round((fim - ini) / 60000)) || 60;
            const ps = [S.me.id].concat(participantes());
            const r0 = refs.find(r => String(r.assistidaId) === ref());
            abrirCal({ visao: "semana", data: ini, acimaDeModal: true, escolha: {
                pessoas: ps, assistidaId: r0 ? r0.assistidaId : null, assistidaNome: r0 ? r0.identificacao : null, dur, desl: 0,
                rotulo: (V("#nTit").value.trim() || "Novo compromisso"), atual: fim > ini ? { ini, fim } : null,
                aoEscolher: d0 => { V("#nData").value = ymd(d0); V("#nIni").value = hm(d0); V("#nFim").value = hm(new Date(+d0 + dur * 60000)); V("#nTodo").checked = false; sujo = true; ajustar(); toast(`Horário escolhido: ${SEM[d0.getDay()]} ${dm(d0)} às ${hm(d0)}. Confira e clique em Salvar.`); }
            } });
        });
        f.addEventListener("submit", async e => {
            e.preventDefault();
            V("#nErro").textContent = "";
            const t = tipo();
            if (t === "comunicacao") {
                const r = await post("/comunicacoes", { assistidaId: +V("#nAluna").value, aulaId: V("#nAula").value, comunicacao: V("#nCom").value });
                if (!r.ok) { V("#nErro").textContent = msg(r); return; }
                fechar(); return depoisDeGravar("Comunicação registrada. A professora vê ao lado do nome na chamada.");
            }
            const todo = V("#nTodo").checked, data = V("#nData").value;
            if (!data) { V("#nErro").textContent = "Escolha a data."; return; }
            if (!todo && V("#nFim").value <= V("#nIni").value) { V("#nErro").textContent = "O fim precisa ser depois do início."; return; }
            const rep = t === "ausencia" ? 1 : (+V("#nRep").value || 1);
            const ocs = [];
            for (let i = 0; i < rep; i++) { const d0 = somaDias(lerData(data), 7 * i); ocs.push({ inicio: todo ? ymd(d0) : `${ymd(d0)}T${V("#nIni").value}`, fim: todo ? null : `${ymd(d0)}T${V("#nFim").value}` }); }
            const dono = t === "ausencia" ? V("#nNome").value : S.me.id;
            const vis = V("#nVis").value;
            const parts = ["ausencia", "lembrete", "prazo"].includes(t) ? [] : participantes();
            const assistidaId = t === "ausencia" || !ref() ? null : +ref();
            const corpo = { tipo: t, titulo: V("#nTit").value.trim(), donoId: dono, diaInteiro: todo, local: V("#nLocal").value.trim() || null, assistidaId, visibilidade: vis, participantes: parts, podemVer: vis === "pessoas" ? $$("[data-np]:checked", m).map(x => x.dataset.np) : [] };
            const salvar = async lista => {
                const r = await post("/compromissos", Object.assign({}, corpo, { ocorrencias: lista }));
                if (r.status === 409) return refazerComDecisoes(r.corpo.ocorrencias, lista, salvar);
                if (!r.ok) { V("#nErro").textContent = msg(r); return; }
                fechar();
                depoisDeGravar(dono !== S.me.id ? `Ausência informada para ${(outras.find(p => p.id === dono) || {}).curto}. Ela confirma ou corrige na Agenda dela.` : rep > 1 ? `${lista.length} de ${rep} datas salvas.` : "Compromisso salvo.");
            };
            if (!ocupaTipo(t) || t === "ausencia") return salvar(ocs);
            conferirESalvar({ donoId: dono, participantes: parts, assistidaId, diaInteiro: todo }, ocs, salvar);
        });
    }

    // ---------------- novo agendamento / remarcar ----------------
    async function abrirAgendamento(pre) {
        const tipos = S.me.tiposAgendamento;
        if (!tipos.length) { toast("Agendamentos são feitos pelo Jurídico, pela Psicologia ou pelo Serviço Social."); return; }
        const refs = await origens();
        if (!refs.length) { toast("Nenhuma assistida da sua área em que você tenha permissão."); return; }
        const dt = pre.data && pre.data > S.agora ? pre.data : somaDias(S.hoje, 1);
        const social = S.me.area === "servico_social";
        const html = `<h3>${pre.remarcarDeId ? "Remarcar" : "Novo agendamento"}<button type="button" data-fechar aria-label="Fechar">×</button></h3>
        <form class="ag-form" id="aF" autocomplete="off">
            <div class="full"><label for="aRef">Assistida</label><select id="aRef" class="soft-input" ${pre.remarcarDeId ? "disabled" : ""}>${refs.map(r => `<option value="${r.assistidaId}" data-restrito="${r.restrito ? 1 : 0}" ${pre.assistidaId === r.assistidaId ? "selected" : ""}>${esc(r.identificacao)} · ${esc(r.referencia || "")}${r.restrito ? " · restrito" : ""}</option>`).join("")}</select><div class="ajuda">Só aparecem casos e prontuários da sua área em que você tem permissão.</div></div>
            <div class="full"><label for="aTipo">Tipo</label><select id="aTipo" class="soft-input">${tipos.map(t => `<option value="${t[0]}" ${pre.tipo === t[0] ? "selected" : ""}>${esc(t[1])}</option>`).join("")}</select></div>
            <div><label for="aData">Data</label><input id="aData" type="date" class="soft-input" value="${ymd(dt)}"></div>
            <div><label for="aIni">Início</label><input id="aIni" type="time" class="soft-input" value="${pre.data ? hm(pre.data) : "09:00"}" step="900"></div>
            <div><label for="aDur">Duração (min)</label><input id="aDur" type="number" min="15" max="480" step="5" class="soft-input" value="${S.me.duracaoPadrao || 60}"></div>
            ${social ? `<div data-desl><label for="aDesl">Deslocamento (min, antes e depois)</label><input id="aDesl" type="number" min="0" max="240" step="5" class="soft-input" value="${S.me.deslocamentoPadrao || 30}"><div class="ajuda">Ocupa a agenda.</div></div>` : ""}
            <div class="full" data-pres><label class="linha-check"><input type="checkbox" id="aPres" checked> Presencial na unidade</label>
                <label class="linha-check"><input type="checkbox" id="aRec"> Avisar a Recepção no dia</label>
                <div class="ajuda" id="aRecAj">A Recepção vê só o nome de identificação, o código e o horário (“Atendimento com a equipe técnica”), no dia. Área e motivo nunca aparecem.</div></div>
            <div class="full"><label for="aObs">Observação operacional (opcional)</label><input id="aObs" class="soft-input" maxlength="300" placeholder="Ex.: trazer documentos"><div class="ajuda">Nunca escreva relato aqui.</div></div>
            <div class="full"><button type="button" class="soft-btn soft-btn-secondary ag-btn-sm" id="aDisp">🗓 Ver disponibilidade</button></div>
            <div class="full ag-erro" id="aErro"></div>
            <div class="full ag-acoes"><button type="button" class="soft-btn soft-btn-secondary" data-fechar>Cancelar</button><button type="submit" class="soft-btn soft-btn-primary">${pre.remarcarDeId ? "Remarcar" : "Agendar"}</button></div>
        </form>`;
        let sujo = false, recTocado = false;
        const { m, fechar } = modal(html, { sujo: () => sujo });
        const V = q => $(q, m);
        V("#aF").addEventListener("input", e => { sujo = true; if (e.target.id === "aRec") recTocado = true; });
        const visita = () => ["visita", "visita_institucional"].includes(V("#aTipo").value);
        const ajustar = () => {
            const restrito = V("#aRef").selectedOptions[0]?.dataset.restrito === "1";
            if (V("[data-desl]")) V("[data-desl]").style.display = visita() ? "" : "none";
            V("[data-pres]").style.display = visita() ? "none" : "";
            if (!recTocado) V("#aRec").checked = !restrito && V("#aPres").checked;
            V("#aRec").disabled = !V("#aPres").checked;
            V("#aRecAj").textContent = (restrito ? "Caso restrito: o aviso à Recepção vem desligado. " : "") + "A Recepção vê só o nome de identificação, o código e o horário (“Atendimento com a equipe técnica”), no dia. Área e motivo nunca aparecem." + (!V("#aRec").checked && V("#aPres").checked ? " Desligado: a assistida não aparece nas esperadas da Recepção." : "");
        };
        ["#aRef", "#aTipo", "#aPres", "#aRec"].forEach(q => V(q).addEventListener("change", ajustar));
        ajustar();
        const ler = () => {
            const ini = lerData(V("#aData").value, V("#aIni").value), dur = +V("#aDur").value || 60, desl = visita() && V("#aDesl") ? +V("#aDesl").value || 0 : 0;
            return { ini, dur, desl, aid: +V("#aRef").value };
        };
        V("#aDisp").addEventListener("click", () => {
            const x = ler(), r0 = refs.find(r => r.assistidaId === x.aid);
            abrirCal({ visao: "semana", data: x.ini, acimaDeModal: true, escolha: {
                pessoas: [S.me.id], assistidaId: x.aid, assistidaNome: r0 ? r0.identificacao : "", dur: x.dur, desl: x.desl, ignorar: pre.remarcarDeId ? "ag:" + pre.remarcarDeId : null,
                rotulo: V("#aTipo").selectedOptions[0].textContent + " · " + (r0 ? r0.identificacao : ""), atual: { ini: x.ini, fim: new Date(+x.ini + x.dur * 60000) },
                aoEscolher: d0 => { V("#aData").value = ymd(d0); V("#aIni").value = hm(d0); sujo = true; toast(`Horário escolhido: ${SEM[d0.getDay()]} ${dm(d0)} às ${hm(d0)}. Confira e clique em ${pre.remarcarDeId ? "Remarcar" : "Agendar"}.`); }
            } });
        });
        V("#aF").addEventListener("submit", async e => {
            e.preventDefault();
            V("#aErro").textContent = "";
            const x = ler();
            if (!V("#aData").value || !V("#aIni").value) { V("#aErro").textContent = "Escolha data e horário."; return; }
            const fim = new Date(+x.ini + x.dur * 60000);
            const oc = { inicio: isoLocal(x.ini), fim: isoLocal(fim) };
            const corpo = { assistidaId: x.aid, tipo: V("#aTipo").value, inicio: oc.inicio, duracaoMinutos: x.dur, deslocamentoMinutos: x.desl, presencial: !visita() && V("#aPres").checked, avisarRecepcao: !visita() && V("#aPres").checked && V("#aRec").checked, observacao: V("#aObs").value.trim() || null, remarcarDeId: pre.remarcarDeId || null };
            const salvar = async lista => {
                const o = lista[0];
                const r = await post("/agendamentos", Object.assign({}, corpo, { justificativa: o.justificativa || null, excecaoMotivo: o.excecaoMotivo || null }));
                if (r.status === 409) return mostrarConflitos(r.corpo.ocorrencias, false, res => salvar([Object.assign({}, o, res[0])]));
                if (!r.ok) { V("#aErro").textContent = msg(r); return; }
                fechar(); depoisDeGravar(pre.remarcarDeId ? "Remarcado. O horário antigo fica no histórico." : "Agendado.");
            };
            conferirESalvar({ assistidaId: x.aid, deslocamentoMinutos: x.desl, ignorarItemId: pre.remarcarDeId ? "ag:" + pre.remarcarDeId : null }, [oc], salvar);
        });
    }

    // ---------------- configuração (ADM) ----------------
    async function abrirConfig() {
        const r = await api("/configuracao");
        if (!r.ok) { toast(msg(r)); return; }
        const c = r.corpo;
        const cr = await CasaMulherAuth.apiFetch("/api/cursos").then(x => x.ok ? x.json() : []).catch(() => []);
        const cursos = (cr || []).filter(x => x.ativo);
        const tipoTxt = { feriado: "Feriado (aviso)", fechada: "Unidade fechada (bloqueia)", evento: "Evento da Casa" };
        const { m, fechar } = modal(`<h3>Configuração da agenda<button type="button" data-fechar aria-label="Fechar">×</button></h3>
            <form class="ag-form" id="cfF">
                <div><label for="cfLc">Aviso a partir de quantos compromissos no dia</label><input id="cfLc" type="number" min="1" max="30" class="soft-input" value="${c.limiteCompromissosDia}"></div>
                <div><label for="cfLh">ou de quantas horas ocupadas</label><input id="cfLh" type="number" min="1" max="16" class="soft-input" value="${c.limiteHorasDia}"></div>
                <div><label for="cfEi">Expediente: início</label><input id="cfEi" type="time" class="soft-input" value="${c.expedienteInicio}"></div>
                <div><label for="cfEf">Expediente: fim</label><input id="cfEf" type="time" class="soft-input" value="${c.expedienteFim}"></div>
                <div class="full ag-acoes" style="justify-content:flex-start"><button type="button" class="soft-btn soft-btn-primary ag-btn-sm" id="cfSalvar">Salvar limites e expediente</button></div>
            </form>
            <dl class="ag-det">
                <dt>Dias da Casa</dt><dd>${c.dias.length ? c.dias.map(d => `${dm(lerData(d.data))} · ${esc(tipoTxt[d.tipo] || d.tipo)} · ${esc(d.titulo)}${d.reaberta ? ` <span class="ag-tag" style="--bg:#E1F3EC;--cor:#24805F">reaberta para todos</span>` : d.tipo === "fechada" ? ` <button type="button" class="soft-btn soft-btn-warning ag-btn-sm" data-reabrir="${d.id}">Reabrir para todos…</button>` : ""} <button type="button" class="soft-btn soft-btn-secondary ag-btn-sm" data-remover="${d.id}">Remover</button>`).join("<br>") : "Nenhum."}
                    <span class="ag-nota" style="display:block">Unidade fechada bloqueia agendamentos. Exceção para UM compromisso: a ADM autoriza ao salvar aquele compromisso (vale só para ele, o horário e as pessoas dele). Reabrir para todos é outra ação, explícita.</span></dd>
                <dt>Novo dia</dt><dd><form class="ag-form" id="cfDia" style="margin:0"><div><input id="cfDd" type="date" class="soft-input" aria-label="Data"></div><div><select id="cfDt" class="soft-input" aria-label="Tipo"><option value="feriado">Feriado (aviso)</option><option value="fechada">Unidade fechada (bloqueia)</option><option value="evento">Evento da Casa</option></select></div><div class="full"><input id="cfDtit" class="soft-input" maxlength="120" placeholder="Título (ex.: Dedetização)" aria-label="Título"></div><div class="full"><input id="cfDn" class="soft-input" maxlength="300" placeholder="Observação (opcional)" aria-label="Observação"></div><div class="full"><button type="button" class="soft-btn soft-btn-secondary ag-btn-sm" id="cfDAdd">Adicionar dia</button></div></form></dd>
                <dt>Exceções autorizadas</dt><dd>${c.excecoes.length ? c.excecoes.map(esc).join("<br>") : "Nenhuma."}</dd>
                <dt>Ausências informadas</dt><dd>${c.ausenciasInformadas.length ? c.ausenciasInformadas.map(esc).join("<br>") + `<span class="ag-nota" style="display:block">Confirme em Agenda › Pendências.</span>` : "Nenhuma aguardando confirmação."}</dd>
                <dt>Horário das turmas</dt><dd><select id="cfCurso" class="soft-input" aria-label="Turma"><option value="">Escolha a turma…</option>${cursos.map(x => `<option value="${x.id}">${esc(x.nome)}</option>`).join("")}</select><div id="cfHor"></div></dd>
            </dl>
            <div class="ag-erro" id="cfErro"></div>
            <div class="ag-acoes"><button type="button" class="soft-btn soft-btn-secondary" data-fechar>Fechar</button></div>`);
        const erro = t => { $("#cfErro", m).textContent = t; };
        const recarregar = async t => { fechar(); await depoisDeGravar(t); abrirConfig(); };
        $("#cfSalvar", m).addEventListener("click", async () => {
            const x = await api("/configuracao", { method: "PUT", body: { limiteCompromissosDia: +$("#cfLc", m).value, limiteHorasDia: +$("#cfLh", m).value, expedienteInicio: $("#cfEi", m).value, expedienteFim: $("#cfEf", m).value } });
            if (!x.ok) return erro(msg(x));
            const me = await api("/me"); if (me.ok) S.me = me.corpo;
            recarregar("Configuração salva.");
        });
        $("#cfDAdd", m).addEventListener("click", async () => {
            const x = await post("/dias", { data: $("#cfDd", m).value, tipo: $("#cfDt", m).value, titulo: $("#cfDtit", m).value.trim(), nota: $("#cfDn", m).value.trim() || null });
            if (!x.ok) return erro(msg(x));
            recarregar("Dia adicionado.");
        });
        m.addEventListener("click", async e => {
            const rm = e.target.closest("[data-remover]");
            if (rm) { const x = await api("/dias/" + rm.dataset.remover, { method: "DELETE" }); if (!x.ok) return erro(msg(x)); return recarregar("Dia removido."); }
            const ra = e.target.closest("[data-reabrir]");
            if (ra) { const motivo = window.prompt("Reabrir a unidade para TODAS as pessoas neste dia. Motivo:"); if (!motivo) return; const x = await post(`/dias/${ra.dataset.reabrir}/reabrir`, { motivo }); if (!x.ok) return erro(msg(x)); return recarregar("Unidade reaberta para todos."); }
        });
        const DIAS = ["Domingo", "Segunda", "Terça", "Quarta", "Quinta", "Sexta", "Sábado"];
        $("#cfCurso", m).addEventListener("change", async e => {
            const id = e.target.value, box = $("#cfHor", m);
            if (!id) { box.innerHTML = ""; return; }
            const h = await api(`/cursos/${id}/horarios`);
            const linhas = h.ok ? h.corpo : [];
            const linha = x => `<div class="ag-form" data-hl style="margin:6px 0"><input type="hidden" data-hid value="${x.id || ""}"><div><select class="soft-input" data-hd aria-label="Dia">${DIAS.map((d, i) => `<option value="${i}" ${x.diaSemana === i ? "selected" : ""}>${d}</option>`).join("")}</select></div><div><input class="soft-input" data-hs placeholder="Sala" value="${esc(x.sala || "")}" aria-label="Sala"></div><div><input type="time" class="soft-input" data-hi value="${x.inicio || "09:00"}" aria-label="Início"></div><div><input type="time" class="soft-input" data-hf value="${x.fim || "11:00"}" aria-label="Fim"></div><div class="full"><button type="button" class="soft-btn soft-btn-secondary ag-btn-sm" data-hrm>Remover horário</button></div></div>`;
            box.innerHTML = `<div id="cfHl">${linhas.map(linha).join("")}</div><button type="button" class="soft-btn soft-btn-secondary ag-btn-sm" id="cfHadd">+ Horário</button> <button type="button" class="soft-btn soft-btn-primary ag-btn-sm" id="cfHsalvar">Salvar horários</button><p class="ag-nota">As aulas aparecem na agenda a partir destes horários. Horário novo ou alterado vale a partir de hoje.</p>`;
            $("#cfHadd", box).addEventListener("click", () => $("#cfHl", box).insertAdjacentHTML("beforeend", linha({ diaSemana: 1 })));
            box.addEventListener("click", ev => { const b = ev.target.closest("[data-hrm]"); if (b) b.closest("[data-hl]").remove(); });
            $("#cfHsalvar", box).addEventListener("click", async () => {
                const lista = $$("[data-hl]", box).map(l => ({ id: +$("[data-hid]", l).value || null, diaSemana: +$("[data-hd]", l).value, inicio: $("[data-hi]", l).value, fim: $("[data-hf]", l).value, sala: $("[data-hs]", l).value.trim() || null }));
                const x = await api(`/cursos/${id}/horarios`, { method: "PUT", body: lista });
                if (!x.ok) return erro(msg(x));
                limparCache(); toast("Horários salvos."); atualizarTudo();
            });
        });
    }

    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", iniciar); else iniciar();
})();
