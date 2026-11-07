/* Monta uma página de relatórios: abas, período, impressão e relatório individual */
(function () {
    "use strict";
    const esc = s => RU.esc(s);

    function modal(html) {
        let m = document.getElementById("relModal");
        if (!m) {
            m = document.createElement("div");
            m.id = "relModal";
            m.className = "soft-modal hidden rel-nao-imprimir";
            m.innerHTML = `<div class="soft-modal-content rel-modal"></div>`;
            m.addEventListener("click", e => { if (e.target === m || e.target.closest("[data-fechar]")) m.classList.add("hidden"); });
            document.body.appendChild(m);
        }
        m.querySelector(".rel-modal").innerHTML = html;
        m.classList.remove("hidden");
        return m;
    }

    window.paginaRelatorios = function (cfg) {
        const U = cfg.usuario;
        RU.iniciarPagina(U);
        RU.abas(".rel-sub-tab", "[data-painel]");

        // ---- relatório da área / geral ----
        const saidaA = document.getElementById("saidaArea");
        let pA = null;
        function desenharArea() { if (pA) saidaA.innerHTML = cfg.renderArea(pA, cfg.estadoArea || {}) + RU.rodape(U, cfg.rodapeArea || ""); }
        RU.controlePeriodo(document.getElementById("perArea"), p => { pA = p; desenharArea(); }, { tipo: "mensal", ref: "2026-10" });
        pA = CASA.periodo("mensal", "2026-10");
        if (cfg.montarFiltrosArea) cfg.montarFiltrosArea(document.getElementById("filtrosExtraArea"), desenharArea);
        saidaA.addEventListener("change", e => { if (e.target.closest("[data-criterio]")) { cfg.aoMudarCriterio && cfg.aoMudarCriterio(e.target.dataset.criterio, +e.target.value); desenharArea(); } });
        desenharArea();

        // ---- relatório individual ----
        const saidaI = document.getElementById("saidaInd");
        const lista = RI.listaVisivel(cfg.perfil, U.id).sort((a, b) => a.nome.localeCompare(b.nome));
        let assistidaId = cfg.assistidaInicial && lista.some(a => a.id === cfg.assistidaInicial) ? cfg.assistidaInicial : (lista[0] && lista[0].id);
        let pI = CASA.periodo("geral");
        let opcoes = { sel: new Set() };
        // Cada nova geração começa sem conteúdo sigiloso: nada fica memorizado
        function zerarSigiloso() {
            opcoes = { sel: new Set() };
            document.querySelectorAll("[data-opcao]").forEach(ch => { ch.checked = false; });
        }
        function desenharInd() { RI.render(saidaI, { perfil: cfg.perfil, usuario: U, assistidaId: assistidaId, p: pI, opcoes: opcoes }); }
        RU.seletorAssistida(document.getElementById("selAssistida"), lista, id => { assistidaId = id; zerarSigiloso(); desenharInd(); }, assistidaId);
        RU.controlePeriodo(document.getElementById("perInd"), p => { pI = p; zerarSigiloso(); desenharInd(); }, { tipo: "geral" });
        document.querySelectorAll("[data-opcao]").forEach(ch => ch.addEventListener("change", () => { opcoes[ch.dataset.opcao] = ch.checked; if (!ch.checked) opcoes.sel.clear(); desenharInd(); }));
        saidaI.addEventListener("change", e => {
            const ch = e.target.closest("[data-reg]");
            if (!ch) return;
            if (ch.checked) opcoes.sel.add(ch.dataset.reg); else opcoes.sel.delete(ch.dataset.reg);
            desenharInd();
        });
        saidaI.addEventListener("click", e => {
            const b = e.target.closest("[data-reg-todos]");
            if (!b) return;
            b.dataset.regTodos.split(",").filter(Boolean).forEach(k => opcoes.sel.add(k));
            desenharInd();
        });
        desenharInd();

        const nomeAtual = () => (CASA.assistida(assistidaId) || {}).nome || "";
        const registroAuditoria = inc => `<p class="rel-nota" style="margin-top:12px">Fica registrado no histórico de ações: <b>${esc(U.nome)}</b> gerou o relatório individual de <b>${esc(nomeAtual())}</b> em ${new Date().toLocaleString("pt-BR").slice(0, 17)}, ${inc.length ? "<b>com " + inc.length + " registro(s) sigiloso(s)</b> (" + esc(inc.join(", ")) + ")" : "sem relatos nem evoluções"}. Os textos não são copiados para o histórico.</p>`;

        // ---- impressão: prévia quando houver conteúdo sigiloso ----
        document.querySelectorAll("[data-imprimir]").forEach(b => b.addEventListener("click", () => window.print()));
        document.querySelectorAll("[data-imprimir-ind]").forEach(b => b.addEventListener("click", () => {
            const inc = RI.incluidos();
            if (!inc.length) { window.print(); return; }
            const m = modal(`<h3 style="color:#8B586F;margin-top:0">Prévia: este PDF vai ter conteúdo sigiloso</h3>
                <p>Além dos dados do relatório, vão sair no papel <b>${inc.length} ${cfg.perfil === "psicologia" ? "evolução(ões)" : "relato(s)"}</b> de ${esc(nomeAtual())}:</p>
                <ul>${inc.map(x => `<li>${esc(x)}</li>`).join("")}</ul>
                <p>O texto completo de cada um está destacado na tela, atrás desta janela. Confira antes de imprimir.</p>
                ${registroAuditoria(inc)}
                <div style="display:flex;gap:8px;justify-content:flex-end;margin-top:16px"><button type="button" class="soft-btn soft-btn-secondary" data-fechar>Voltar e revisar</button><button type="button" class="soft-btn soft-btn-primary" data-confirmar>Imprimir</button></div>`);
            m.querySelector("[data-confirmar]").addEventListener("click", () => { m.classList.add("hidden"); setTimeout(() => window.print(), 50); });
        }));

        // ---- documento para outra instituição (resumo escrito pela profissional) ----
        document.querySelectorAll("[data-doc-externo]").forEach(b => b.addEventListener("click", () => {
            const a = CASA.assistida(assistidaId);
            const doJur = cfg.perfil === "juridico";
            const per = Object.assign({}, pI, { ini: pI.ini < a.entrada ? a.entrada : pI.ini });
            const quantos = doJur ? CASA.JUR_ATEND.filter(x => x.assistidaId === a.id && CASA.dentro(x.data, per)).length : CASA.PSI_SESS.filter(x => x.assistidaId === a.id && x.presenca === "Compareceu" && CASA.dentro(x.data, per)).length;
            const f = CASA.func(U.id);
            const m = modal(`<h3 style="color:#8B586F;margin-top:0">Documento para outra instituição</h3>
                <p class="rel-nota">Este documento é separado do relatório de acompanhamento. Os ${doJur ? "relatos" : "registros das sessões"} não entram automaticamente: a profissional escreve só o necessário para a finalidade.</p>
                <div class="soft-form-group" style="margin-bottom:10px"><label>Destinatário</label><input class="soft-input" data-campo="dest" placeholder="Ex.: CAPS de referência, Defensoria Pública"></div>
                <div class="soft-form-group" style="margin-bottom:10px"><label>Finalidade</label><select class="soft-input" data-campo="fin"><option>Encaminhamento para a rede de proteção</option><option>Solicitação do Poder Judiciário</option><option>Continuidade do cuidado em saúde</option><option>Outra</option></select></div>
                <div class="soft-form-group" style="margin-bottom:10px"><label>Incluir dados objetivos</label>
                    <span class="rel-check"><input type="checkbox" data-campo="qtd" checked> ${doJur ? "Número de atendimentos" : "Número de sessões com comparecimento"} no período (${quantos})</span>
                    <span class="rel-check"><input type="checkbox" data-campo="sit" checked> Situação atual (${doJur ? "em acompanhamento no Jurídico" : "em acompanhamento psicológico"})</span></div>
                <div class="soft-form-group" style="margin-bottom:10px"><label>Resumo escrito pela profissional</label><textarea class="soft-input" rows="5" data-campo="txt" placeholder="Escreva só o que o destinatário precisa saber para essa finalidade."></textarea></div>
                <div data-previa></div>
                <div style="display:flex;gap:8px;justify-content:flex-end;margin-top:12px"><button type="button" class="soft-btn soft-btn-secondary" data-fechar>Cancelar</button><button type="button" class="soft-btn soft-btn-primary" data-gerar>Ver prévia</button></div>`);
            m.querySelector("[data-gerar]").addEventListener("click", () => {
                const v = k => m.querySelector(`[data-campo="${k}"]`);
                m.querySelector("[data-previa]").innerHTML = `<div class="rel-cartao-atend"><p><b>Casa da Mulher de Itaquaquecetuba</b><br>Para: ${esc(v("dest").value || "-")}<br>Finalidade: ${esc(v("fin").value)}</p>
                    <p>Declaramos que <b>${esc(a.nome)}</b>, CPF ${a.cpf}, ${v("sit").checked ? (doJur ? "está em acompanhamento no Jurídico da Casa" : "está em acompanhamento psicológico na Casa") : "é atendida pela Casa"}${v("qtd").checked ? `, com ${quantos} ${doJur ? "atendimento(s)" : "sessão(ões)"} entre ${RU.fd(per.ini)} e ${RU.fd(per.fim)}` : ""}.</p>
                    ${v("txt").value ? `<p>${esc(v("txt").value)}</p>` : `<p class="rel-nota">(sem resumo escrito)</p>`}
                    <p style="margin-top:18px">______________________________<br>${esc(f.nome)} · ${esc(f.crp || f.oab || "")}</p></div>
                    <p class="rel-nota">Ao imprimir, fica registrado quem gerou, para quem e com qual finalidade (sem copiar o resumo).</p>`;
            });
        }));

        document.querySelectorAll("[data-csv]").forEach(b => b.addEventListener("click", () => alert("Protótipo: aqui baixa as tabelas do relatório em planilha (CSV), sem nomes quando o perfil não pode ver nomes.")));

        // permite abrir já numa aba: pagina.html#individual
        if (location.hash === "#individual") document.querySelector('.rel-sub-tab[data-aba="individual"]').click();
    };
})();
