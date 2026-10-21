/*
 * Gestão de cursos (ADM): criar, renomear, desativar/reativar, apagar sem histórico
 * e definir os cursos de cada professora.
 */
document.addEventListener("DOMContentLoaded", setupCursos);

let cursos = [];
const escCursos = (v) => window.CasaMulherAssistidaForm.escapeHtml(v);

async function setupCursos() {
    const usuario = await CasaMulherAuth.protegerPagina();
    if (!usuario) return;

    if (usuario.perfil !== "adm" && usuario.perfil !== "equipe") {
        window.location.href = CasaMulherAuth.getPainelUrl(usuario);
        return;
    }

    inicializarSoftSessionCard(usuario);

    document.getElementById("formNovoCurso").addEventListener("submit", criarCurso);
    document.getElementById("listaCursos").addEventListener("click", acaoCurso);
    document.getElementById("listaProfessoras").addEventListener("click", salvarProfessora);

    await recarregar();
}

function avisar(texto, tipo) {
    const el = document.getElementById("mensagemCursos");
    el.textContent = texto || "";
    el.className = `message ${tipo || ""}`.trim();
}

async function lerErro(response, padrao) {
    try {
        return (await response.json()).mensagem || padrao;
    } catch {
        return padrao;
    }
}

async function recarregar() {
    const response = await CasaMulherAuth.apiFetch("/api/cursos?todos=true");
    cursos = response.ok ? await response.json() : [];
    renderizarCursos();
    await carregarProfessoras();
}

function renderizarCursos() {
    const el = document.getElementById("listaCursos");
    if (!cursos.length) {
        el.innerHTML = '<div class="soft-empty-state">Nenhum curso cadastrado.</div>';
        return;
    }

    el.innerHTML = cursos.map((c) => `
        <div class="entity-card" data-id="${c.id}">
            <input type="text" class="soft-input" data-campo="nome" value="${escCursos(c.nome)}" maxlength="120" aria-label="Nome do curso">
            <div class="entity-info" style="margin-top: 8px;">Alunas: ${c.totalAlunas} · Professoras: ${c.totalProfessoras}</div>
            <div class="entity-info"><span class="soft-badge ${c.ativo ? "soft-badge--ok" : "soft-badge--restrito"}">${c.ativo ? "Ativo" : "Desativado"}</span></div>
            <div class="entity-actions">
                <button type="button" class="soft-btn soft-btn-primary" data-acao="renomear">Salvar nome</button>
                ${c.ativo
                    ? '<button type="button" class="soft-btn soft-btn-warning" data-acao="desativar">Desativar</button>'
                    : '<button type="button" class="soft-btn soft-btn-warning" data-acao="reativar">Reativar</button>'}
                ${c.podeApagar ? '<button type="button" class="soft-btn soft-btn-danger" data-acao="apagar">Apagar</button>' : ""}
            </div>
        </div>`).join("");
}

async function criarCurso(e) {
    e.preventDefault();
    const nome = document.getElementById("novoCursoNome").value.trim();
    if (!nome) return avisar("Digite o nome do curso.", "error");

    const response = await CasaMulherAuth.apiFetch("/api/cursos", { method: "POST", body: { nome } });
    if (!response.ok) return avisar(await lerErro(response, "Não foi possível criar o curso."), "error");

    document.getElementById("novoCursoNome").value = "";
    avisar(`Curso "${nome}" adicionado.`, "success");
    await recarregar();
}

async function acaoCurso(e) {
    const botao = e.target.closest("[data-acao]");
    if (!botao) return;

    const card = botao.closest("[data-id]");
    const id = card.dataset.id;
    const curso = cursos.find((c) => String(c.id) === id);
    const acao = botao.dataset.acao;
    let response;

    if (acao === "renomear") {
        const nome = card.querySelector('[data-campo="nome"]').value.trim();
        response = await CasaMulherAuth.apiFetch(`/api/cursos/${id}`, { method: "PUT", body: { nome } });
    } else if (acao === "desativar") {
        if (!confirm(`Desativar "${curso.nome}"? Ele sai das listas da Recepção e das professoras, mas o histórico continua.`)) return;
        response = await CasaMulherAuth.apiFetch(`/api/cursos/${id}/desativar`, { method: "POST" });
    } else if (acao === "reativar") {
        response = await CasaMulherAuth.apiFetch(`/api/cursos/${id}/reativar`, { method: "POST" });
    } else if (acao === "apagar") {
        if (!confirm(`Apagar "${curso.nome}" de vez? Ele nunca teve alunas nem aulas.`)) return;
        response = await CasaMulherAuth.apiFetch(`/api/cursos/${id}`, { method: "DELETE" });
    }

    if (!response.ok) return avisar(await lerErro(response, "Não foi possível concluir."), "error");
    avisar(await lerErro(response, "Feito."), "success");
    await recarregar();
}

async function carregarProfessoras() {
    const el = document.getElementById("listaProfessoras");
    const response = await CasaMulherAuth.apiFetch("/api/cursos/professoras");
    const professoras = response.ok ? await response.json() : [];
    const ativos = cursos.filter((c) => c.ativo);

    if (!professoras.length) {
        el.innerHTML = '<div class="soft-empty-state">Nenhuma professora cadastrada.</div>';
        return;
    }

    el.innerHTML = professoras.map((p) => {
        const ids = p.cursos.map((c) => c.id);
        const desativados = p.cursos.filter((c) => !c.ativo);
        return `
        <div class="entity-card" data-professora="${escCursos(p.userId)}">
            <div class="entity-title">${escCursos(p.nome)}</div>
            <div class="entity-info">${escCursos(p.identificador)}${p.ativo ? "" : " · conta desativada"}</div>
            <div class="cursos-checks">
                ${ativos.map((c) => `<label class="soft-check"><input type="checkbox" value="${c.id}" ${ids.includes(c.id) ? "checked" : ""}> ${escCursos(c.nome)}</label>`).join("") || '<span class="section-intro">Nenhum curso ativo.</span>'}
            </div>
            ${desativados.length ? `<div class="entity-info">Cursos desativados (histórico): ${desativados.map((c) => escCursos(c.nome)).join(", ")}</div>` : ""}
            <div class="entity-actions">
                <button type="button" class="soft-btn soft-btn-primary" data-salvar-professora>Salvar cursos</button>
            </div>
        </div>`;
    }).join("");
}

async function salvarProfessora(e) {
    const botao = e.target.closest("[data-salvar-professora]");
    if (!botao) return;

    const card = botao.closest("[data-professora]");
    const cursoIds = Array.from(card.querySelectorAll("input[type=checkbox]:checked")).map((c) => Number(c.value));
    const response = await CasaMulherAuth.apiFetch(`/api/cursos/professoras/${encodeURIComponent(card.dataset.professora)}`, {
        method: "PUT",
        body: { cursoIds }
    });

    if (!response.ok) return avisar(await lerErro(response, "Não foi possível salvar os cursos."), "error");
    avisar("Cursos da professora atualizados.", "success");
    await recarregar();
}
