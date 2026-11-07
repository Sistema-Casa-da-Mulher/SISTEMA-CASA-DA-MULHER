/*
 * Aba "Relatórios" de cada perfil. Os números chegam prontos do servidor (/api/relatorios),
 * já filtrados pelas regras de sigilo; aqui só se desenha. Relatos e evoluções só aparecem
 * quando a responsável marca registro a registro, e a impressão fica no histórico de ações.
 */
(function () {
    "use strict";

    const S = {
        ctx: null,
        visao: null,
        perA: { tipo: "mensal", ref: null },
        filtro: "todos",
        crit: {},
        perI: { tipo: "geral", ref: null },
        lista: [],
        assistidaId: null,
        ind: null,
        incluir: false,
        sel: new Set(),
        conteudo: new Map(),
        geral: null
    };

    const CORES = {
        ok: "var(--rel-ok)", justificada: "var(--rel-justificada)", falta: "var(--rel-falta)", neutro: "var(--rel-neutro)",
        roxo: "var(--rel-roxo)", azul: "var(--rel-azul)", barra: "var(--rel-barra)",
        risco_baixo: "#8DBFA3", risco_moderado: "#E0B35E", risco_alto: "#D9787F", risco_iminente: "#A23B3B"
    };
    const BADGES = ["ok", "restrito", "psicologia", "juridico", "risco"];
    const MESES = ["janeiro", "fevereiro", "março", "abril", "maio", "junho", "julho", "agosto", "setembro", "outubro", "novembro", "dezembro"];

    const $ = (id) => document.getElementById(id);
    const esc = (v) => String(v == null ? "" : v).replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
    const fd = (s) => { if (!s) return "-"; const [a, m, d] = s.slice(0, 10).split("-"); return `${d}/${m}/${a}`; };
    const n = (v) => Number(v || 0).toLocaleString("pt-BR");
    const cor = (t) => CORES[t] || "";
    const novo = () => `<span class="rel-dado-novo" title="Dado que o sistema passou a gravar agora: períodos antigos aparecem sem registro">dado novo</span>`;
    const badge = (texto, tipo) => `<span class="soft-badge ${BADGES.includes(tipo) ? "soft-badge--" + tipo : ""}">${esc(texto)}</span>`;
    const vazio = (t) => `<p class="soft-empty-state">${esc(t || "Nada no período.")}</p>`;

    function mensagem(texto, tipo) {
        const el = $("mensagemRelatorios");
        el.textContent = texto || "";
        el.className = `message ${tipo || ""}`.trim();
    }

    async function api(url, opcoes) {
        const sep = url.includes("?") ? "&" : "?";
        const final = S.visao ? `${url}${sep}visao=${encodeURIComponent(S.visao)}` : url;
        return CasaMulherAuth.apiFetch(final, Object.assign({ mensagemElement: $("mensagemRelatorios") }, opcoes || {}));
    }

    async function erroDe(response, padrao) {
        try {
            const corpo = await response.json();
            return corpo.mensagem || padrao;
        } catch {
            return padrao;
        }
    }

    // ------------------------------------------------------------------ período

    function opcoesPeriodo(tipo) {
        const ini = S.ctx.inicioDados, hoje = S.ctx.hoje;
        const [ai, mi] = ini.split("-").map(Number), [ah, mh] = hoje.split("-").map(Number);
        const ops = [];
        if (tipo === "mensal") {
            for (let a = ah, m = mh; a > ai || (a === ai && m >= mi); m--) {
                if (m === 0) { m = 12; a--; if (a < ai) break; }
                ops.push([`${a}-${String(m).padStart(2, "0")}`, `${MESES[m - 1].replace(/^./, (c) => c.toUpperCase())} de ${a}`]);
            }
        } else if (tipo === "semestral") {
            for (let a = ah; a >= ai; a--) {
                [2, 1].forEach((s) => {
                    if (a === ah && s === 2 && mh <= 6) return;
                    if (a === ai && s === 1 && mi > 6) return;
                    ops.push([`${a}-${s}`, `${s}º semestre de ${a}`]);
                });
            }
        } else if (tipo === "anual") {
            for (let a = ah; a >= ai; a--) ops.push([String(a), String(a)]);
        }
        return ops;
    }

    function controlePeriodo(el, estado, aoMudar) {
        const rotulos = { mensal: "Mensal", semestral: "Semestral", anual: "Anual", geral: "Geral" };
        el.innerHTML = `
            <div class="soft-form-group"><label>Período</label>
                <div class="rel-periodo-tipo">${Object.keys(rotulos).map((t) => `<button type="button" data-tipo="${t}">${rotulos[t]}</button>`).join("")}</div>
            </div>
            <div class="soft-form-group rel-ref"><label>Referência</label><select class="soft-input"></select></div>`;
        const sel = el.querySelector("select"), grupo = el.querySelector(".rel-ref");
        function preencher() {
            const ops = opcoesPeriodo(estado.tipo);
            grupo.style.display = ops.length ? "" : "none";
            sel.innerHTML = ops.map((o) => `<option value="${esc(o[0])}">${esc(o[1])}</option>`).join("");
            if (estado.ref && ops.some((o) => o[0] === estado.ref)) sel.value = estado.ref;
            estado.ref = ops.length ? sel.value : null;
            el.querySelectorAll("[data-tipo]").forEach((b) => b.classList.toggle("active", b.dataset.tipo === estado.tipo));
        }
        el.querySelectorAll("[data-tipo]").forEach((b) => b.addEventListener("click", () => { estado.tipo = b.dataset.tipo; estado.ref = null; preencher(); aoMudar(); }));
        sel.addEventListener("change", () => { estado.ref = sel.value; aoMudar(); });
        preencher();
    }

    const qsPeriodo = (p) => `periodo=${encodeURIComponent(p.tipo)}${p.ref ? "&ref=" + encodeURIComponent(p.ref) : ""}`;

    // ------------------------------------------------------------------ peças de desenho

    function kpis(lista) {
        if (!lista || !lista.length) return "";
        return `<div class="rel-kpis">${lista.map((k) => {
            let delta = "";
            if (k.anterior != null && k.valorNum != null) {
                const d = Math.round((k.valorNum - k.anterior) * 10) / 10;
                const cls = d > 0 ? "sobe" : d < 0 ? "desce" : "";
                delta = `<span class="rel-kpi-delta ${cls}">${d === 0 ? "igual ao" : (d > 0 ? "+" : "") + n(d) + (k.sufixoDelta || "") + " vs"} período anterior</span>`;
            } else if (k.ajuda) {
                delta = `<span class="rel-kpi-delta">${esc(k.ajuda)}</span>`;
            }
            return `<div class="rel-kpi ${k.alerta ? "rel-kpi--alerta" : ""}" ${k.titulo ? `title="${esc(k.titulo)}"` : ""}><span class="rel-kpi-valor">${esc(k.valor)}</span><span class="rel-kpi-rotulo">${esc(k.rotulo)}${k.dadoNovo ? novo() : ""}</span>${delta}</div>`;
        }).join("")}</div>`;
    }

    function barras(b) {
        const itens = b.itens || [];
        if (!itens.length || itens.every((i) => !i.valor)) return vazio(b.vazio);
        const max = b.max || Math.max(1, ...itens.map((i) => i.valor));
        const suf = b.sufixo || "";
        return `<div class="rel-barras">${itens.map((i) => `
            <div class="rel-barra-linha" title="${esc(i.rotulo)}: ${n(i.valor)}${esc(suf)}">
                <span class="rel-barra-rotulo">${esc(i.rotulo)}</span>
                <span class="rel-barra-trilho"><span class="rel-barra-valor-fill" style="width:${Math.max(0.5, i.valor * 100 / max)}%;${i.cor && cor(i.cor) ? "background:" + cor(i.cor) : ""}"></span></span>
                <span class="rel-barra-num">${n(i.valor)}${esc(suf)}${i.extra ? ` <small>${esc(i.extra)}</small>` : ""}</span>
            </div>`).join("")}</div>`;
    }

    function colunas(b) {
        const itens = b.itens || [];
        if (!itens.length) return vazio(b.vazio);
        const max = Math.max(1, ...itens.map((i) => i.valor));
        const iMax = itens.findIndex((i) => i.valor === max);
        const suf = b.sufixo || "";
        return `<div class="rel-colunas">${itens.map((i, k) => `
            <div class="rel-coluna ${k === iMax || k === itens.length - 1 ? "mostrar" : ""}" title="${esc(i.rotulo)}: ${n(i.valor)}${esc(suf)}">
                <span class="rel-coluna-valor">${n(i.valor)}${esc(suf)}</span>
                <span class="rel-coluna-fill" style="height:${i.valor * 100 / max}%;${b.cor && cor(b.cor) ? "background:" + cor(b.cor) : ""}"></span>
            </div>`).join("")}</div>
            <div class="rel-colunas-rotulos">${itens.map((i) => `<span>${esc(i.rotulo)}</span>`).join("")}</div>`;
    }

    function pilha(b) {
        const itens = b.itens || [];
        const tot = itens.reduce((s, x) => s + x.valor, 0);
        const pct = (v) => tot ? Math.round(v * 100 / tot) : 0;
        const corpo = !tot ? vazio(b.vazio) : `<div class="rel-pilha">${itens.filter((s) => s.valor).map((s) => `<span style="width:${s.valor * 100 / tot}%;background:${cor(s.cor) || "var(--rel-barra)"}" title="${esc(s.rotulo)}: ${n(s.valor)} (${pct(s.valor)}%)"></span>`).join("")}</div>
            <div class="rel-legenda">${itens.map((s) => `<span><i style="background:${cor(s.cor) || "var(--rel-barra)"}"></i>${esc(s.rotulo)}: <b>${n(s.valor)}</b> (${pct(s.valor)}%)</span>`).join("")}</div>`;
        return corpo + (b.texto ? `<p style="margin:14px 0 0;font-size:0.88rem;color:var(--rel-tinta)">${esc(b.texto)}</p>` : "");
    }

    function celula(c, col) {
        if (!c) return `<td></td>`;
        const cls = [col && col.num ? "num" : "", c.calor != null ? "rel-calor" : ""].join(" ").trim();
        const estilo = c.calor != null && c.calor > 0 ? ` style="background:rgba(196,139,165,${(c.calor * 0.85).toFixed(2)});color:${c.calor > 0.5 ? "#fff" : "inherit"}"` : "";
        let txt = c.badge ? badge(c.texto, c.badge) : esc(c.texto);
        if (c.destaque) txt = `<span class="rel-destaque">${txt}</span>`;
        if (c.sub) txt += `<span class="rel-celula-sub">${esc(c.sub)}</span>`;
        return `<td class="${cls}"${estilo}>${txt}</td>`;
    }

    function tabela(b) {
        const linhas = b.linhas || [];
        if (!linhas.length) return vazio(b.vazio);
        const cols = b.colunas || [];
        return `<div class="rel-tabela-wrap"><table class="rel-tabela"><thead><tr>${cols.map((c) => `<th class="${c.num ? "num" : ""}">${esc(c.t)}${c.dadoNovo ? novo() : ""}</th>`).join("")}</tr></thead>
            <tbody>${linhas.map((l) => `<tr>${l.map((c, i) => celula(c, cols[i])).join("")}</tr>`).join("")}</tbody></table></div>`;
    }

    function podio(b) {
        const itens = (b.itens || []).slice();
        const mais = itens.slice(0, 5), menos = itens.slice().reverse().slice(0, 5);
        const li = (x) => `<li><b>${esc(x.rotulo)}</b>: ${n(x.valor)} <small>(${esc((x.tipo || "").toLowerCase())})</small></li>`;
        return (itens.some((i) => i.valor > 0) ? `<div class="rel-podio"><div class="rel-podio-card"><h4>Mais procurados</h4><ol>${mais.map(li).join("")}</ol></div>
            <div class="rel-podio-card rel-podio-card--menos"><h4>Menos procurados</h4><ol>${menos.map(li).join("")}</ol></div></div>` : "")
            + barras({ itens, vazio: b.vazio })
            + legenda(b.legenda);
    }

    function legenda(itens) {
        if (!itens || !itens.length) return "";
        return `<div class="rel-legenda">${itens.map((i) => `<span>${i.cor ? `<i style="background:${cor(i.cor)}"></i>` : ""}${esc(i.rotulo)}</span>`).join("")}</div>`;
    }

    function marcos(b) {
        const ms = b.marcos || [];
        if (!ms.length) return vazio(b.vazio);
        return `<ul class="rel-tempo">${ms.map((m) => `<li class="${m.area ? "area-" + esc(m.area) : ""}"><span class="rel-tempo-data">${fd(m.data)}</span>${esc(m.texto)}${m.quem ? ` <span class="rel-tempo-quem">· ${esc(m.quem)}</span>` : ""}</li>`).join("")}</ul>`;
    }

    function chamada(b) {
        const letra = { presente: "P", faltou: "F", justificada: "J" };
        const itens = b.itens || [];
        return `<div class="rel-chamada">${itens.length ? itens.map((i) => `<span class="rel-dia ${esc(i.tipo)}" title="${esc(i.extra)}"><b>${letra[i.tipo] || "-"}</b>${esc(i.rotulo)}</span>`).join("") : `<span class="soft-empty-state">${esc(b.vazio || "Sem aulas no período.")}</span>`}</div>
            <div class="rel-legenda"><span><i style="background:var(--rel-ok)"></i>P = presente</span><span><i style="background:var(--rel-justificada)"></i>J = justificada</span><span><i style="background:var(--rel-falta)"></i>F = faltou</span></div>`;
    }

    function risco(b) {
        const itens = b.itens || [];
        if (!itens.length) return vazio("Nenhuma sessão no período.");
        const classe = (c) => ({ risco_baixo: "risco-baixo", risco_moderado: "risco-moderado", risco_alto: "risco-alto", risco_iminente: "risco-iminente" }[c] || "risco-nulo");
        return `<div class="rel-risco-linha">${itens.map((i) => `<span class="${classe(i.cor)}" title="${esc(i.rotulo)}"></span>`).join("")}</div>${legenda(b.legenda)}`;
    }

    function pares(lista) {
        if (!lista || !lista.length) return "";
        return `<dl class="rel-ficha" style="margin-top:0">${lista.map((p) => `<div><dt>${esc(p.rotulo)}${p.dadoNovo ? novo() : ""}</dt><dd>${esc(p.valor)}${p.sub ? `<br><small>${esc(p.sub)}</small>` : ""}</dd></div>`).join("")}</dl>`;
    }

    function registros(b) {
        const regs = b.registros || [];
        const ind = S.ind;
        if (!regs.length) return vazio(b.vazio);
        let aviso = "";
        if (ind && S.incluir) {
            const chaves = regs.filter((r) => r.chave).map((r) => r.chave);
            aviso = ind.podeIncluirSigiloso && chaves.length
                ? `<div class="rel-sigilo rel-nao-imprimir" style="margin:12px 0">Escolha abaixo quais ${ind.tipoSigiloso === "evolucoes" ? "evoluções" : "relatos"} entram no PDF. Nada vem marcado: inclua só o necessário para a finalidade. <button type="button" class="soft-btn soft-btn-secondary" style="margin-left:8px;padding:5px 12px" data-reg-todos="${esc(chaves.join(","))}">Marcar todos do período</button></div>`
                : "";
        }
        return aviso + regs.map((r) => {
            const podeMarcar = S.incluir && r.chave && ind && ind.podeIncluirSigiloso;
            const marcado = podeMarcar && S.sel.has(r.chave);
            const conteudo = marcado ? S.conteudo.get(r.chave) : null;
            return `<div class="rel-cartao-atend"><header><b>${esc(r.titulo)}</b><span>${esc(r.subtitulo || "")}</span></header>
                <div class="rel-badges">${(r.badges || []).map((x) => badge(x.texto, x.tipo)).join("")}</div>
                <dl>${(r.campos || []).map((c) => `<div><dt>${esc(c.rotulo)}</dt><dd>${esc(c.valor)}</dd></div>`).join("")}</dl>
                ${podeMarcar ? `<label class="rel-check rel-nao-imprimir rel-sel-reg"><input type="checkbox" data-reg="${esc(r.chave)}" ${marcado ? "checked" : ""}> Incluir ${ind.tipoSigiloso === "evolucoes" ? "a evolução desta sessão" : "o relato deste atendimento"}</label>` : ""}
                ${marcado && !conteudo ? `<p class="rel-carregando">Carregando o texto...</p>` : ""}
                ${conteudo ? conteudo.textos.map((t) => `<div class="rel-texto-sigiloso"><b>${esc(t.rotulo)}:</b> ${esc(t.valor)}</div>`).join("") : ""}
            </div>`;
        }).join("");
    }

    function corpoBloco(b) {
        switch (b.tipo) {
            case "barras": return barras(b);
            case "colunas": return colunas(b);
            case "pilha": return pilha(b);
            case "tabela": return tabela(b);
            case "kpis": return kpis(b.kpis) || vazio(b.vazio);
            case "podio": return podio(b);
            case "marcos": return marcos(b);
            case "chamada": return chamada(b);
            case "risco": return risco(b);
            case "registros": return registros(b);
            case "sigilo": return `<div class="rel-sigilo">${esc(b.texto)}</div>`;
            case "pares": {
                const situacao = (b.itens || []).map((i) => badge(i.rotulo, { concluiu: "ok", interrompido: "restrito", cursando: "psicologia" }[i.cor] || ""));
                if (b.texto) situacao.push(badge(b.texto, "restrito"));
                return (situacao.length ? `<div class="rel-badges" style="margin-bottom:10px">${situacao.join("")}</div>` : "") + pares(b.pares);
            }
            default: return b.texto ? `<p style="margin:0">${esc(b.texto)}</p>` : "";
        }
    }

    function bloco(b, dentro) {
        const extras = (b.extras || []).map((x) => {
            const titulo = x.titulo ? `<h4>${esc(x.titulo)}${x.dadoNovo ? novo() : ""}</h4>${x.nota ? `<p class="rel-nota">${esc(x.nota)}</p>` : ""}` : "";
            return `<div>${titulo}${corpoBloco(x)}${(x.extras || []).map((y) => bloco(y, true)).join("")}</div>`;
        }).join("");
        const corpo = corpoBloco(b) + (extras ? `<div class="rel-sub-blocos" style="margin-top:14px">${extras}</div>` : "");
        if (dentro) return `<div style="margin-top:12px">${b.titulo ? `<h4>${esc(b.titulo)}</h4>` : ""}${corpo}</div>`;
        return `<div class="rel-bloco ${b.largo ? "rel-bloco--largo" : ""}">${b.titulo ? `<h3>${esc(b.titulo)}${b.dadoNovo ? novo() : ""}</h3>` : ""}${b.nota ? `<p class="rel-nota">${esc(b.nota)}</p>` : ""}${corpo}</div>`;
    }

    function avisos(lista, criterios) {
        return (lista || []).map((a) => {
            const classe = { info: "soft-alert--info", risco: "soft-alert--risco", restrito: "soft-alert--restrito" }[a.tipo] || "";
            if (a.tipo === "sigilo") return `<div class="rel-sigilo" style="margin-bottom:1.2rem">${esc(a.texto)}</div>`;
            const pontos = a.pontos && a.pontos.length ? `<ul class="rel-pontos">${a.pontos.map((p) => `<li><b>${esc(p.titulo)}:</b> ${esc(p.texto)}${p.quem ? `<span class="rel-ponto-quem">Quem age: ${esc(p.quem)}. Ação: ${esc(p.acao)}</span>` : ""}</li>`).join("")}</ul>` : "";
            const crit = a.pontos && criterios ? `<div class="rel-criterios rel-nao-imprimir">Critérios (exemplos, configuráveis pela Casa):
                <label>prazo da fila <input type="number" min="1" max="365" data-criterio="prazoFila" value="${criterios.prazoFila}"> dias</label>
                <label>faltas seguidas <input type="number" min="1" max="50" data-criterio="faltasSeguidas" value="${criterios.faltasSeguidas}"></label>
                <label>retorno vencido há <input type="number" min="1" max="365" data-criterio="diasRetorno" value="${criterios.diasRetornoVencido}"> dias</label>
                <label>ocupação baixa abaixo de <input type="number" min="1" max="100" data-criterio="ocupacaoBaixa" value="${criterios.ocupacaoBaixa}">%</label></div>` : "";
            const notaPontos = a.pontos && a.titulo ? ` <span class="rel-nota">(até 5, sem nomes e sem conteúdo de atendimentos)</span>` : "";
            return `<div class="soft-alert ${classe}" style="margin:0 0 1.2rem">${a.titulo ? `<b>${esc(a.titulo)}</b>${notaPontos} ` : ""}${a.texto ? esc(a.texto) : ""}${pontos}${a.itens && a.itens.length ? `<ul>${a.itens.map((i) => `<li>${esc(i)}</li>`).join("")}</ul>` : ""}${a.nota ? `<p class="rel-nota" style="margin:10px 0 0">${esc(a.nota)}</p>` : ""}${crit}</div>`;
        }).join("");
    }

    function secoes(lista) {
        return (lista || []).map((s, i) => `${s.titulo ? `<h3 style="color:#8B586F;margin:2rem 0 0.8rem">${esc(s.titulo)}${s.subtitulo ? ` <span style="font-size:0.8rem;color:var(--rel-tinta-3);font-weight:600">${esc(s.subtitulo)}</span>` : ""}</h3>` : ""}
            <div class="rel-grade" style="${!s.titulo && i ? "margin-top:1.4rem" : ""}">${s.blocos.map((b) => bloco(b)).join("")}</div>`).join("");
    }

    function cabecalho(r) {
        const p = r.periodo;
        return `<div class="rel-cabecalho"><h2>${esc(r.titulo)}</h2><span class="rel-periodo">${esc(p.rotulo)}${p.tipo !== "geral" || r.assistidaId ? " · " + fd(p.ini) + " a " + fd(p.fim) : ""}${r.comparacao && !r.assistidaId ? " · " + esc(r.comparacao) : ""}</span></div>`;
    }

    // ------------------------------------------------------------------ relatório da área

    async function carregarGeral() {
        const saida = $("saidaArea");
        saida.innerHTML = `<p class="rel-carregando">Calculando...</p>`;
        const crit = Object.keys(S.crit).map((k) => `&${k}=${encodeURIComponent(S.crit[k])}`).join("");
        const response = await api(`/api/relatorios/geral?${qsPeriodo(S.perA)}&filtro=${encodeURIComponent(S.filtro)}${crit}`);
        if (!response.ok) {
            saida.innerHTML = `<p class="rel-erro">${esc(await erroDe(response, "Não foi possível gerar o relatório."))}</p>`;
            return;
        }
        const r = await response.json();
        S.geral = r;
        saida.innerHTML = cabecalho(r) + avisos(r.avisos, r.criterios) + kpis(r.kpis) + secoes(r.secoes) + `<div class="rel-rodape">${esc(r.rodape)}</div>`;
    }

    // ------------------------------------------------------------------ relatório individual

    function seletorAssistida() {
        const el = $("selAssistida");
        el.innerHTML = `<label>Assistida</label><div class="rel-busca"><input type="search" class="soft-input" placeholder="Buscar por nome ou CPF..." autocomplete="off"><div class="rel-busca-lista" hidden></div></div>`;
        const inp = el.querySelector("input"), box = el.querySelector(".rel-busca-lista");
        const atual = S.lista.find((a) => a.id === S.assistidaId);
        if (atual) inp.value = atual.nome;
        function mostrar() {
            const q = inp.value.trim().toLowerCase();
            const qd = q.replace(/\D/g, "");
            const r = S.lista.filter((a) => !q || a.nome.toLowerCase().includes(q) || (qd.length >= 3 && a.cpfMascarado.replace(/\D/g, "").includes(qd))).slice(0, 40);
            box.innerHTML = r.length
                ? r.map((a) => `<button type="button" data-id="${a.id}">${esc(a.nome)} <small>· CPF ${esc(a.cpfMascarado)} · entrada ${fd(a.entrada)}</small></button>`).join("")
                : `<div style="padding:10px 14px;color:#AD859B">Nenhuma assistida encontrada.</div>`;
            box.hidden = false;
        }
        inp.addEventListener("focus", () => { inp.select(); mostrar(); });
        inp.addEventListener("input", mostrar);
        box.addEventListener("click", (e) => {
            const b = e.target.closest("button");
            if (!b) return;
            const a = S.lista.find((x) => x.id === Number(b.dataset.id));
            inp.value = a.nome;
            box.hidden = true;
            S.assistidaId = a.id;
            zerarSigiloso();
            carregarIndividual();
        });
        document.addEventListener("click", (e) => { if (!el.contains(e.target)) box.hidden = true; });
    }

    // Cada nova geração começa sem conteúdo sigiloso: nada fica memorizado.
    function zerarSigiloso() {
        S.incluir = false;
        S.sel.clear();
        S.conteudo.clear();
        $("chkSigiloso").checked = false;
    }

    async function carregarListaAssistidas() {
        const response = await api("/api/relatorios/assistidas");
        S.lista = response.ok ? await response.json() : [];
        seletorAssistida();
        if (!S.lista.length) $("saidaInd").innerHTML = vazio("Nenhuma assistida disponível para este perfil.");
    }

    async function carregarIndividual() {
        const saida = $("saidaInd");
        if (!S.assistidaId) { saida.innerHTML = vazio("Escolha uma assistida."); return; }
        saida.innerHTML = `<p class="rel-carregando">Gerando...</p>`;
        const response = await api(`/api/relatorios/assistidas/${S.assistidaId}?${qsPeriodo(S.perI)}`);
        if (!response.ok) {
            S.ind = null;
            saida.innerHTML = `<p class="rel-erro">${esc(await erroDe(response, "Não foi possível gerar o relatório."))}</p>`;
            return;
        }
        S.ind = await response.json();
        const grupo = $("grupoSigiloso");
        grupo.classList.toggle("hidden", !S.ind.tipoSigiloso);
        $("btnDocExterno").classList.toggle("hidden", !(S.ctx.documentoExterno && S.ind.podeDocumentoExterno));
        desenharIndividual();
    }

    function desenharIndividual() {
        const r = S.ind;
        if (!r) return;
        const avisoSig = S.incluir && r.avisoSigiloso
            ? `<div class="soft-alert rel-nao-imprimir" style="margin:0 0 1.2rem">${esc(r.avisoSigiloso)}</div>` : "";
        const incluidos = [...S.sel].filter((k) => S.conteudo.has(k)).map((k) => S.conteudo.get(k).rotulo);
        const quais = r.tipoSigiloso === "evolucoes" ? "evolução(ões)" : "relato(s)";
        const rodapeSig = incluidos.length
            ? ` <b>Conteúdo sigiloso incluído: ${incluidos.length} ${quais} (${esc(incluidos.join(", "))}).</b>`
            : (r.tipoSigiloso ? " Sem relatos nem evoluções." : "");
        $("saidaInd").innerHTML = cabecalho(r)
            + `<p class="rel-nome">${esc(r.nome)}</p><div class="rel-badges">${r.badges.map((b) => badge(b.texto, b.tipo)).join("")}</div>`
            + `<dl class="rel-ficha">${r.ficha.map((f) => `<div><dt>${esc(f.rotulo)}</dt><dd>${esc(f.valor)}</dd></div>`).join("")}</dl>`
            + `<div style="margin-top:14px">${avisos(r.avisos)}</div>` + avisoSig
            + `<div style="margin-top:20px">${kpis(r.kpis)}</div>`
            + secoes(r.secoes)
            + `<div class="rel-rodape">${esc(r.rodape)}${rodapeSig}</div>`;
    }

    async function buscarConteudo(chaves) {
        const faltam = chaves.filter((k) => !S.conteudo.has(k));
        if (!faltam.length) return true;
        const response = await api(`/api/relatorios/assistidas/${S.assistidaId}/sigilosos`, { method: "POST", body: { registros: faltam } });
        if (!response.ok) {
            mensagem(await erroDe(response, "Não foi possível carregar o texto."), "error");
            faltam.forEach((k) => S.sel.delete(k));
            return false;
        }
        (await response.json()).forEach((c) => S.conteudo.set(c.chave, c));
        return true;
    }

    // ------------------------------------------------------------------ impressão

    function modal(html) {
        const m = $("relModal");
        $("relModalConteudo").innerHTML = html;
        m.classList.remove("hidden");
        return m;
    }

    function fecharModal() {
        $("relModal").classList.add("hidden");
    }

    async function registrarImpressao(corpo) {
        const response = await api("/api/relatorios/impressao", { method: "POST", body: Object.assign({ visao: S.visao }, corpo) });
        if (!response.ok) {
            mensagem(await erroDe(response, "Não foi possível registrar a impressão. Nada foi impresso."), "error");
            return false;
        }
        return true;
    }

    async function imprimirArea() {
        if (!S.geral) return;
        if (await registrarImpressao({ tipo: "geral", periodo: S.perA.tipo, ref: S.perA.ref })) window.print();
    }

    async function imprimirIndividual() {
        const r = S.ind;
        if (!r) return;
        const chaves = [...S.sel].filter((k) => S.conteudo.has(k));
        const corpo = { tipo: "individual", assistidaId: r.assistidaId, periodo: S.perI.tipo, ref: S.perI.ref, registros: chaves };
        if (!chaves.length) {
            if (await registrarImpressao(corpo)) window.print();
            return;
        }
        const usuario = CasaMulherAuth.getUsuario();
        const rotulos = chaves.map((k) => S.conteudo.get(k).rotulo);
        const quais = r.tipoSigiloso === "evolucoes" ? "evolução(ões)" : "relato(s)";
        const m = modal(`<h3 style="color:#8B586F;margin-top:0">Prévia: este PDF vai ter conteúdo sigiloso</h3>
            <p>Além dos dados do relatório, vão sair no papel <b>${chaves.length} ${quais}</b> de ${esc(r.nome)}:</p>
            <ul>${rotulos.map((x) => `<li>${esc(x)}</li>`).join("")}</ul>
            <p>O texto completo de cada um está destacado na tela, atrás desta janela. Confira antes de imprimir.</p>
            <p class="rel-nota" style="margin-top:12px">Fica registrado no histórico de ações: <b>${esc(usuario.nomeCompleto)}</b> imprimiu o relatório individual desta assistida, com os registros listados acima. Os textos não são copiados para o histórico.</p>
            <div style="display:flex;gap:8px;justify-content:flex-end;margin-top:16px"><button type="button" class="soft-btn soft-btn-secondary" data-fechar>Voltar e revisar</button><button type="button" class="soft-btn soft-btn-primary" data-confirmar>Imprimir</button></div>`);
        m.querySelector("[data-confirmar]").addEventListener("click", async () => {
            fecharModal();
            if (await registrarImpressao(corpo)) setTimeout(() => window.print(), 50);
        });
    }

    function documentoExterno() {
        const r = S.ind;
        if (!r) return;
        const doJur = S.ctx.perfil === "juridico";
        const m = modal(`<h3 style="color:#8B586F;margin-top:0">Documento para outra instituição</h3>
            <p class="rel-nota">Este documento é separado do relatório de acompanhamento. Os ${doJur ? "relatos" : "registros das sessões"} não entram automaticamente: escreva só o necessário para a finalidade. O resumo não é enviado ao servidor nem guardado.</p>
            <div class="soft-form-group" style="margin-bottom:10px"><label>Destinatário</label><input class="soft-input" data-campo="dest" maxlength="160" placeholder="Ex.: CAPS de referência, Defensoria Pública"></div>
            <div class="soft-form-group" style="margin-bottom:10px"><label>Finalidade</label><select class="soft-input" data-campo="fin"><option>Encaminhamento para a rede de proteção</option><option>Solicitação do Poder Judiciário</option><option>Continuidade do cuidado em saúde</option><option>Outra</option></select></div>
            <div class="soft-form-group" style="margin-bottom:10px"><label>Incluir dados objetivos</label>
                <span class="rel-check"><input type="checkbox" data-campo="qtd" checked> ${doJur ? "Número de atendimentos" : "Número de sessões com comparecimento"} no período do relatório</span>
                <span class="rel-check"><input type="checkbox" data-campo="sit" checked> Situação atual</span></div>
            <div class="soft-form-group" style="margin-bottom:10px"><label>Resumo escrito pela profissional</label><textarea class="soft-input" rows="5" data-campo="txt" maxlength="3000" placeholder="Escreva só o que o destinatário precisa saber para essa finalidade."></textarea></div>
            <div data-previa></div>
            <p class="rel-erro" data-erro></p>
            <div style="display:flex;gap:8px;justify-content:flex-end;margin-top:12px"><button type="button" class="soft-btn soft-btn-secondary" data-fechar>Cancelar</button><button type="button" class="soft-btn soft-btn-secondary" data-gerar>Ver prévia</button><button type="button" class="soft-btn soft-btn-primary" data-imprimir-doc>Imprimir</button></div>`);
        const v = (k) => m.querySelector(`[data-campo="${k}"]`);

        async function obter(confirmar) {
            m.querySelector("[data-erro]").textContent = "";
            const response = await api(`/api/relatorios/assistidas/${r.assistidaId}/documento-externo`, {
                method: "POST",
                body: { destinatario: v("dest").value, finalidade: v("fin").value, periodo: S.perI.tipo, ref: S.perI.ref, confirmar }
            });
            if (!response.ok) {
                m.querySelector("[data-erro]").textContent = await erroDe(response, "Não foi possível gerar o documento.");
                return null;
            }
            return response.json();
        }

        function montar(doc) {
            const quant = v("qtd").checked ? `, com ${doc.quantidade} ${esc(doc.rotuloQuantidade)} entre ${esc(doc.periodoIni)} e ${esc(doc.periodoFim)}` : "";
            const sit = v("sit").checked ? esc(doc.situacao) : "é atendida pela Casa";
            const texto = v("txt").value.trim();
            return `<div class="rel-doc-externo"><p><b>${esc(doc.instituicao)}</b><br>Para: ${esc(v("dest").value || "-")}<br>Finalidade: ${esc(v("fin").value)}</p>
                <p>Declaramos que <b>${esc(doc.nome)}</b>, CPF ${esc(doc.cpf)}, ${sit}${quant}.</p>
                ${texto ? texto.split(/\n+/).map((t) => `<p>${esc(t)}</p>`).join("") : `<p class="rel-nota">(sem resumo escrito)</p>`}
                <p style="margin-top:28px">Itaquaquecetuba, ${new Date().toLocaleDateString("pt-BR", { day: "numeric", month: "long", year: "numeric" })}.</p>
                <p style="margin-top:28px">______________________________<br>${esc(doc.profissionalNome)} · ${esc(doc.profissionalRegistro)}</p></div>`;
        }

        m.querySelector("[data-gerar]").addEventListener("click", async () => {
            const doc = await obter(false);
            if (doc) m.querySelector("[data-previa]").innerHTML = montar(doc) + `<p class="rel-nota">Ao imprimir, fica registrado quem gerou, para quem e com qual finalidade (sem copiar o resumo).</p>`;
        });
        m.querySelector("[data-imprimir-doc]").addEventListener("click", async () => {
            const doc = await obter(true);
            if (!doc) return;
            $("relDocImpressao").innerHTML = montar(doc);
            fecharModal();
            document.body.classList.add("rel-imprimindo-doc");
            setTimeout(() => window.print(), 50);
        });
    }

    window.addEventListener("afterprint", () => {
        document.body.classList.remove("rel-imprimindo-doc");
        $("relDocImpressao").innerHTML = "";
    });

    // ------------------------------------------------------------------ início

    function montarFiltroArea() {
        const el = $("filtroArea");
        if (!S.ctx.filtroRotulo) { el.innerHTML = ""; return; }
        el.innerHTML = `<div class="soft-form-group"><label>${esc(S.ctx.filtroRotulo)}</label><select class="soft-input">${S.ctx.filtroOpcoes.map((o) => `<option value="${esc(o.valor)}">${esc(o.rotulo)}</option>`).join("")}</select></div>`;
        el.querySelector("select").addEventListener("change", (e) => { S.filtro = e.target.value; carregarGeral(); });
    }

    function montarVisao() {
        const el = $("relVisao");
        const visoes = S.ctx.visoesDisponiveis || [];
        if (!visoes.length) { el.classList.add("hidden"); return; }
        const nomes = { adm: "ADM / Coordenação", recepcao: "Recepção", juridico: "Jurídico", psicologia: "Psicologia", professor: "Professora" };
        el.classList.remove("hidden");
        el.innerHTML = `<b>Equipe de desenvolvimento:</b> ver os relatórios como <select class="soft-input" style="max-width:240px;padding:6px 10px">${visoes.map((v) => `<option value="${v}" ${v === S.ctx.perfil ? "selected" : ""}>${nomes[v] || v}</option>`).join("")}</select>`;
        el.querySelector("select").addEventListener("change", (e) => {
            const url = new URL(location.href);
            url.searchParams.set("visao", e.target.value);
            location.href = url.toString();
        });
    }

    async function iniciar() {
        const usuario = await CasaMulherAuth.protegerPagina();
        if (!usuario) return;
        inicializarSoftSessionCard(usuario);

        S.visao = new URLSearchParams(location.search).get("visao");
        const response = await api("/api/relatorios/contexto");
        if (!response.ok) {
            $("saidaArea").innerHTML = `<p class="rel-erro">${esc(await erroDe(response, "Você não tem acesso aos relatórios."))}</p>`;
            return;
        }
        S.ctx = await response.json();
        if (S.ctx.visoesDisponiveis && S.ctx.visoesDisponiveis.length) S.visao = S.ctx.perfil;

        document.title = `Relatórios - ${S.ctx.perfilNome} - Casa da Mulher`;
        $("relSubtitulo").textContent = S.ctx.perfilNome;
        $("relTitulo").textContent = `Relatórios ${S.ctx.perfil === "adm" ? "da Coordenação" : "· " + S.ctx.perfilNome}`;
        $("relIntro").textContent = S.ctx.intro;
        $("relAbaArea").textContent = S.ctx.tituloArea;
        if (S.ctx.voltarUrl) $("relVoltar").setAttribute("href", S.ctx.voltarUrl);
        if (S.ctx.rotuloSigiloso) $("rotuloSigiloso").textContent = S.ctx.rotuloSigiloso;
        montarVisao();

        document.querySelectorAll(".rel-sub-tab").forEach((b) => b.addEventListener("click", () => {
            document.querySelectorAll(".rel-sub-tab").forEach((x) => x.classList.toggle("active", x === b));
            document.querySelectorAll("[data-painel]").forEach((p) => { p.hidden = p.dataset.painel !== b.dataset.aba; });
            if (b.dataset.aba === "individual" && !S.lista.length) carregarListaAssistidas();
        }));

        controlePeriodo($("perArea"), S.perA, carregarGeral);
        montarFiltroArea();
        controlePeriodo($("perInd"), S.perI, () => { zerarSigiloso(); carregarIndividual(); });

        $("saidaArea").addEventListener("change", (e) => {
            const campo = e.target.closest("[data-criterio]");
            if (!campo) return;
            const v = Number(campo.value);
            if (v > 0) { S.crit[campo.dataset.criterio] = v; carregarGeral(); }
        });

        $("chkSigiloso").addEventListener("change", (e) => {
            S.incluir = e.target.checked;
            if (!S.incluir) { S.sel.clear(); }
            desenharIndividual();
        });

        $("saidaInd").addEventListener("change", async (e) => {
            const ch = e.target.closest("[data-reg]");
            if (!ch) return;
            if (ch.checked) {
                S.sel.add(ch.dataset.reg);
                desenharIndividual();
                await buscarConteudo([ch.dataset.reg]);
            } else {
                S.sel.delete(ch.dataset.reg);
            }
            desenharIndividual();
        });

        $("saidaInd").addEventListener("click", async (e) => {
            const b = e.target.closest("[data-reg-todos]");
            if (!b) return;
            const chaves = b.dataset.regTodos.split(",").filter(Boolean);
            chaves.forEach((k) => S.sel.add(k));
            desenharIndividual();
            await buscarConteudo(chaves);
            desenharIndividual();
        });

        $("relModal").addEventListener("click", (e) => { if (e.target.id === "relModal" || e.target.closest("[data-fechar]")) fecharModal(); });
        $("btnImprimirArea").addEventListener("click", imprimirArea);
        $("btnImprimirInd").addEventListener("click", imprimirIndividual);
        $("btnDocExterno").addEventListener("click", documentoExterno);

        await carregarGeral();
        if (location.hash === "#individual") document.querySelector('.rel-sub-tab[data-aba="individual"]').click();
    }

    document.addEventListener("DOMContentLoaded", iniciar);
})();
