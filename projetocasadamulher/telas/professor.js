document.addEventListener("DOMContentLoaded", setupProfessor);

let professorUsuario = null;
let cursoVinculado = null;   // nome do curso selecionado
let cursoIdAtual = null;     // id do curso selecionado
let meusCursos = [];
let alunasDoCurso = [];

// Plano de aula e chamada ficam no servidor e pertencem ao curso
// (seguem quem assumir a turma). Este cache guarda o que veio da API.
let aulasCache = { planos: [], chamadas: [], atividadesSugeridas: [] };

let atividadesSelecionadasPlano = [];
let atividadesSelecionadasChamada = [];
let aulaIdAtualChamada = null;

async function setupProfessor() {
    professorUsuario = await CasaMulherAuth.protegerPagina();

    if (!professorUsuario) return;

    if (professorUsuario.perfil !== "professor") {
        window.location.href = "painel.html";
        return;
    }

    inicializarSoftSessionCard(professorUsuario);

    try {
        const response = await CasaMulherAuth.apiFetch("/api/cursos/meus");
        meusCursos = response.ok ? await response.json() : [];
    } catch {
        meusCursos = [];
    }

    if (meusCursos.length === 0) {
        document.getElementById("professorEmptyState").classList.remove("hidden");
        return;
    }

    document.getElementById("professorNomeDisplay").textContent = professorUsuario.nomeCompleto || "-";
    document.getElementById("professorIdDisplay").textContent = professorUsuario.identificadorFuncionario || "-";

    const select = document.getElementById("professorCursoSelect");
    if (meusCursos.length > 1) {
        select.innerHTML = meusCursos.map(c => `<option value="${c.id}">${escapeHtml(c.nome)}</option>`).join("");
        select.classList.remove("hidden");
        document.getElementById("professorCursoDisplay").classList.add("hidden");
        select.addEventListener("change", () => selecionarCurso(Number(select.value)));
    }

    document.getElementById("professorShell").classList.remove("hidden");

    setupChipsEvents();
    setupTabs();
    setupChamada();
    setupPlanoEnsino();
    await selecionarCurso(meusCursos[0].id);
}

async function selecionarCurso(cursoId) {
    const curso = meusCursos.find(c => c.id === cursoId);
    if (!curso) return;

    cursoIdAtual = curso.id;
    cursoVinculado = curso.nome;
    document.getElementById("professorCursoDisplay").textContent = curso.nome;

    await Promise.all([carregarAlunas(), carregarAulas()]);
    renderizarDatalistAtividades();
    popularDatalistAulas();
    renderizarPlanoEnsino();
    carregarChamadaDoDia(document.getElementById("dataChamada").value);
}

async function carregarAulas() {
    try {
        const response = await CasaMulherAuth.apiFetch(`/api/cursos/${cursoIdAtual}/aulas`);
        const dados = response.ok ? await response.json() : null;
        aulasCache = dados
            ? { planos: dados.planos || [], chamadas: dados.chamadas || [], atividadesSugeridas: dados.atividadesSugeridas || [] }
            : { planos: [], chamadas: [], atividadesSugeridas: [] };
    } catch {
        aulasCache = { planos: [], chamadas: [], atividadesSugeridas: [] };
    }
}

function normalizarTexto(texto) {
    if (!texto) return "";
    return texto.normalize("NFD").replace(/[\u0300-\u036f]/g, "").trim().toLowerCase();
}

function setupTabs() {
    const tabs = document.querySelectorAll(".professor-tab");
    tabs.forEach(tab => {
        tab.addEventListener("click", () => {
            // Remove active from all tabs
            tabs.forEach(t => t.classList.remove("active"));
            document.querySelectorAll(".professor-panel").forEach(p => p.classList.remove("active"));
            
            // Add active to clicked tab
            tab.classList.add("active");
            const tabId = tab.getAttribute("data-tab");
            document.getElementById(`tab-${tabId}`).classList.add("active");
        });
    });
}

function obterNomeCurso(idOuNome) {
    if (typeof CURSOS_RECEPCAO !== 'undefined') {
        const cursoEncontrado = CURSOS_RECEPCAO.find(c => c.id === idOuNome || c.nome === idOuNome);
        if (cursoEncontrado) return cursoEncontrado.nome;
    }
    return idOuNome;
}

/* ==========================================================================
   ATIVIDADES E CHIPS
   ========================================================================== */
function renderizarDatalistAtividades() {
    const datalist = document.getElementById("atividadesCursoList");
    if (!datalist) return;
    datalist.innerHTML = "";
    aulasCache.atividadesSugeridas.forEach(a => {
        const option = document.createElement("option");
        option.value = a;
        datalist.appendChild(option);
    });
}

function atividadeJaExiste(lista, nome) {
    const nomeNorm = normalizarTexto(nome);
    return lista.some(a => normalizarTexto(a) === nomeNorm);
}

function adicionarAtividadeGlobal(nomeAtividade) {
    // A sugestão fica disponível já; no servidor ela passa a existir quando a aula ou a chamada é salva.
    if (!atividadeJaExiste(aulasCache.atividadesSugeridas, nomeAtividade)) {
        aulasCache.atividadesSugeridas.push(nomeAtividade.trim());
        renderizarDatalistAtividades();
    }
}

function setupChipsEvents() {
    const btnAddPlano = document.getElementById("btnAdicionarAtividadePlano");
    const inputPlano = document.getElementById("novaAtividadePlano");
    const containerPlano = document.getElementById("chipsAtividadesPlano");
    
    if(btnAddPlano) {
        btnAddPlano.addEventListener("click", () => {
            const val = inputPlano.value.trim();
            if (val && !atividadeJaExiste(atividadesSelecionadasPlano, val)) {
                atividadesSelecionadasPlano.push(val);
                adicionarAtividadeGlobal(val);
                inputPlano.value = "";
                renderizarChips(atividadesSelecionadasPlano, containerPlano, 'plano');
            }
        });
        inputPlano.addEventListener("keypress", (e) => {
            if (e.key === "Enter") { e.preventDefault(); btnAddPlano.click(); }
        });
    }
    
    const btnAddChamada = document.getElementById("btnAdicionarAtividadeChamada");
    const inputChamada = document.getElementById("novaAtividadeChamada");
    const containerChamada = document.getElementById("chipsAtividadesChamada");
    
    if(btnAddChamada) {
        btnAddChamada.addEventListener("click", () => {
            const val = inputChamada.value.trim();
            if (val && !atividadeJaExiste(atividadesSelecionadasChamada, val)) {
                atividadesSelecionadasChamada.push(val);
                adicionarAtividadeGlobal(val);
                inputChamada.value = "";
                renderizarChips(atividadesSelecionadasChamada, containerChamada, 'chamada');
            }
        });
        inputChamada.addEventListener("keypress", (e) => {
            if (e.key === "Enter") { e.preventDefault(); btnAddChamada.click(); }
        });
    }
}

window.removerChip = function(index, tipo) {
    if (tipo === 'plano') {
        atividadesSelecionadasPlano.splice(index, 1);
        renderizarChips(atividadesSelecionadasPlano, document.getElementById("chipsAtividadesPlano"), 'plano');
    } else {
        atividadesSelecionadasChamada.splice(index, 1);
        renderizarChips(atividadesSelecionadasChamada, document.getElementById("chipsAtividadesChamada"), 'chamada');
    }
}

function renderizarChips(lista, container, tipo) {
    container.innerHTML = "";
    lista.forEach((atv, i) => {
        const chip = document.createElement("div");
        chip.className = "professor-chip selected";
        chip.innerHTML = `
            ${escapeHtml(atv)}
            <button type="button" class="professor-chip-remove" onclick="removerChip(${i}, '${tipo}')">&times;</button>
        `;
        container.appendChild(chip);
    });
}

/* ==========================================================================
   ALUNAS
   ========================================================================== */
async function carregarAlunas() {
    // Alunas do curso selecionado, vindas do servidor (restritas nunca aparecem).
    let todasAssistidas = [];
    try {
        const response = await CasaMulherAuth.apiFetch(`/api/cursos/${cursoIdAtual}/alunas`);
        todasAssistidas = response.ok ? await response.json() : [];
    } catch {
        todasAssistidas = [];
    }

    alunasDoCurso = Array.isArray(todasAssistidas) ? todasAssistidas : [];
    
    renderizarAlunas(alunasDoCurso);

    const inputBusca = document.getElementById("buscaAlunas");
    if (inputBusca && !inputBusca.dataset.bound) {
        inputBusca.dataset.bound = "true";
        inputBusca.addEventListener("input", (e) => {
            const termo = normalizarTexto(e.target.value);
            const filtradas = alunasDoCurso.filter(a => normalizarTexto(a.nome).includes(termo));
            renderizarAlunas(filtradas);
        });
    }
}

function renderizarAlunas(lista) {
    const grid = document.getElementById("alunasGrid");
    const empty = document.getElementById("alunasEmpty");
    
    grid.innerHTML = "";
    
    if (lista.length === 0) {
        grid.classList.add("hidden");
        empty.classList.remove("hidden");
        return;
    }
    
    grid.classList.remove("hidden");
    empty.classList.add("hidden");
    
    lista.forEach(aluna => {
        const card = document.createElement("div");
        card.className = "professor-aluna-card";
        
        const iconeTelefone = `<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="#D2AAB9" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="flex-shrink: 0;"><path d="M22 16.92v3a2 2 0 0 1-2.18 2 19.79 19.79 0 0 1-8.63-3.07 19.5 19.5 0 0 1-6-6 19.79 19.79 0 0 1-3.07-8.67A2 2 0 0 1 4.11 2h3a2 2 0 0 1 2 1.72 12.84 12.84 0 0 0 .7 2.81 2 2 0 0 1-.45 2.11L8.09 9.91a16 16 0 0 0 6 6l1.27-1.27a2 2 0 0 1 2.11-.45 12.84 12.84 0 0 0 2.81.7A2 2 0 0 1 22 16.92z"></path></svg>`;
        const iconeData = `<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="#D2AAB9" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="flex-shrink: 0;"><rect x="3" y="4" width="18" height="18" rx="2" ry="2"></rect><line x1="16" y1="2" x2="16" y2="6"></line><line x1="8" y1="2" x2="8" y2="6"></line><line x1="3" y1="10" x2="21" y2="10"></line></svg>`;

        const telefone = aluna.telefone ? `<div class="professor-aluna-detail">${iconeTelefone} <span style="font-weight: 500;">${escapeHtml(aluna.telefone)}</span></div>` : "";
        const dataCadastro = aluna.dataAcolhimento ? `<div class="professor-aluna-detail">${iconeData} <span>Cadastrada em: ${new Date(aluna.dataAcolhimento).toLocaleDateString('pt-BR')}</span></div>` : "";
        
        card.innerHTML = `
            <div class="professor-aluna-name">${escapeHtml(aluna.nome)}</div>
            ${telefone}
            ${dataCadastro}
        `;
        grid.appendChild(card);
    });
}

/* ==========================================================================
   CHAMADA
   ========================================================================== */
function popularDatalistAulas() {
    const list = document.getElementById("aulasPlanejadasList");
    if(!list) return;
    list.innerHTML = "";
    
    const planos = carregarPlanosDoStorage();
    const aulasDoProfessor = planos.filter(p => true);
    
    aulasDoProfessor.sort((a, b) => a.data.localeCompare(b.data));
    aulasDoProfessor.forEach(aula => {
        const formatData = new Date(aula.data + "T00:00:00").toLocaleDateString('pt-BR');
        const option = document.createElement("option");
        option.value = aula.tema;
        option.textContent = `${formatData} — ${aula.tema}`;
        list.appendChild(option);
    });
}

function setupChamada() {
    const inputData = document.getElementById("dataChamada");
    const inputTema = document.getElementById("temaAulaInput");
    const btnSalvar = document.getElementById("btnSalvarChamada");
    
    const agora = new Date();
    const hoje = `${agora.getFullYear()}-${String(agora.getMonth() + 1).padStart(2, "0")}-${String(agora.getDate()).padStart(2, "0")}`;
    inputData.value = hoje;
    
    inputData.addEventListener("change", () => carregarChamadaDoDia(inputData.value));
    
    inputTema.addEventListener("change", () => {
        const planos = carregarPlanosDoStorage();
        const aulaEncontrada = planos.find(p => normalizarTexto(p.tema) === normalizarTexto(inputTema.value));
        if (aulaEncontrada) {
            aulaIdAtualChamada = aulaEncontrada.id;
            atividadesSelecionadasChamada = [...(aulaEncontrada.atividades || [])];
            renderizarChips(atividadesSelecionadasChamada, document.getElementById("chipsAtividadesChamada"), 'chamada');
        } else {
            aulaIdAtualChamada = null;
        }
    });

    btnSalvar.addEventListener("click", salvarChamada);
}

function carregarChamadasDoStorage() {
    return aulasCache.chamadas;
}

function carregarChamadaDoDia(data) {
    const chamadas = carregarChamadasDoStorage();
    const chamadaDoDia = chamadas.find(c => c.data === data);
    
    const inputTema = document.getElementById("temaAulaInput");
    
    if (chamadaDoDia) {
        inputTema.value = chamadaDoDia.tema || "";
        aulaIdAtualChamada = chamadaDoDia.planoAulaId || null;
        atividadesSelecionadasChamada = [...(chamadaDoDia.atividades || [])];
        renderizarChips(atividadesSelecionadasChamada, document.getElementById("chipsAtividadesChamada"), 'chamada');
        // Junta quem já tinha registro com as alunas atuais do curso (novas matrículas entram em branco).
        const registros = (chamadaDoDia.registros || []).map(r => ({ alunaId: r.assistidaId, nome: r.nome || "", status: r.status, observacao: r.observacao || "" }));
        alunasDoCurso.forEach(a => {
            if (!registros.some(r => r.alunaId === a.id)) registros.push({ alunaId: a.id, nome: a.nome, status: null, observacao: "" });
        });
        renderizarListaChamada(registros);
    } else {
        const planos = carregarPlanosDoStorage();
        const aulaPlanejada = planos.find(p => p.data === data);
        
        if (aulaPlanejada) {
            inputTema.value = aulaPlanejada.tema;
            aulaIdAtualChamada = aulaPlanejada.id;
            atividadesSelecionadasChamada = [...(aulaPlanejada.atividades || [])];
        } else {
            inputTema.value = "";
            aulaIdAtualChamada = null;
            atividadesSelecionadasChamada = [];
        }
        renderizarChips(atividadesSelecionadasChamada, document.getElementById("chipsAtividadesChamada"), 'chamada');

        const registrosEmBranco = alunasDoCurso.map(a => ({
            alunaId: a.id,
            nome: a.nome,
            status: null,
            observacao: ""
        }));
        renderizarListaChamada(registrosEmBranco);
    }
    
    atualizarAvisoChamada();
}

function renderizarListaChamada(registros) {
    const list = document.getElementById("chamadaList");
    list.innerHTML = "";
    
    if (registros.length === 0) {
        list.innerHTML = `<div style="text-align: center; color: #A26D85; padding: 20px;">Nenhuma aluna matriculada neste curso para fazer chamada.</div>`;
        return;
    }
    
    registros.forEach(reg => {
        const card = document.createElement("div");
        card.className = "professor-chamada-card";
        card.dataset.alunaId = reg.alunaId;
        
        card.innerHTML = `
            <div style="flex: 1; min-width: 200px;">
                <div style="font-weight: 700; color: #8A3D66; font-size: 1.05rem;">${escapeHtml(reg.nome)}</div>
                <input type="text" class="soft-input obs-input" placeholder="Observação (opcional)" value="${escapeHtml(reg.observacao || '')}" style="margin-top: 8px; width: 100%; max-width: 300px; padding: 4px 8px; font-size: 0.85rem;">
            </div>
            <div class="professor-status-group">
                <button type="button" class="professor-status-button ${reg.status === 'presente' ? 'active' : ''}" data-status="presente">Presente</button>
                <button type="button" class="professor-status-button ${reg.status === 'faltou' ? 'active' : ''}" data-status="faltou">Faltou</button>
                <button type="button" class="professor-status-button ${reg.status === 'justificada' ? 'active' : ''}" data-status="justificada">Justificada</button>
            </div>
        `;
        
        const btns = card.querySelectorAll(".professor-status-button");
        btns.forEach(btn => {
            btn.addEventListener("click", () => {
                btns.forEach(b => b.classList.remove("active"));
                btn.classList.add("active");
                atualizarAvisoChamada();
            });
        });
        
        list.appendChild(card);
    });
}

function atualizarAvisoChamada() {
    const aviso = document.getElementById("chamadaAviso");
    const list = document.getElementById("chamadaList");
    const cards = list.querySelectorAll(".professor-chamada-card");
    
    let todosPreenchidos = true;
    cards.forEach(card => {
        const ativo = card.querySelector(".professor-status-button.active");
        if (!ativo) todosPreenchidos = false;
    });
    
    if (cards.length > 0 && !todosPreenchidos) {
        aviso.classList.remove("hidden");
    } else {
        aviso.classList.add("hidden");
    }
}

async function salvarChamada() {
    const data = document.getElementById("dataChamada").value;
    const tema = document.getElementById("temaAulaInput").value.trim();
    const feedback = document.getElementById("chamadaFeedback");

    if (!data) {
        feedback.textContent = "Selecione a data da aula.";
        feedback.style.color = "#F44336";
        return;
    }

    const registros = [];
    document.querySelectorAll("#chamadaList .professor-chamada-card").forEach(card => {
        const btnAtivo = card.querySelector(".professor-status-button.active");
        registros.push({
            assistidaId: Number(card.dataset.alunaId),
            status: btnAtivo ? btnAtivo.dataset.status : null,
            observacao: card.querySelector(".obs-input").value
        });
    });

    const response = await CasaMulherAuth.apiFetch(`/api/cursos/${cursoIdAtual}/chamadas/${data}`, {
        method: "PUT",
        body: { tema, planoAulaId: aulaIdAtualChamada, atividades: [...atividadesSelecionadasChamada], registros }
    });

    if (!response.ok) {
        let mensagem = "Não foi possível salvar a chamada.";
        try { mensagem = (await response.json()).mensagem || mensagem; } catch { /* sem corpo */ }
        feedback.textContent = mensagem;
        feedback.style.color = "#F44336";
        return;
    }

    const salva = await response.json();
    aulaIdAtualChamada = salva.planoAulaId || null;
    await carregarAulas();
    renderizarDatalistAtividades();
    popularDatalistAulas();
    renderizarPlanoEnsino();

    feedback.textContent = "Chamada salva com sucesso!";
    feedback.style.color = "#4CAF50";
    setTimeout(() => { feedback.textContent = ""; }, 3000);
}

/* ==========================================================================
   PLANO DE ENSINO
   ========================================================================== */
let editandoPlanoId = null;

function setupPlanoEnsino() {
    const btnNovaAula = document.getElementById("btnNovaAula");
    const formPlano = document.getElementById("formPlanoEnsino");
    const btnCancelar = document.getElementById("btnCancelarAula");
    
    btnNovaAula.addEventListener("click", () => {
        formPlano.reset();
        editandoPlanoId = null;
        atividadesSelecionadasPlano = [];
        renderizarChips(atividadesSelecionadasPlano, document.getElementById("chipsAtividadesPlano"), 'plano');
        document.getElementById("tituloFormPlano").textContent = "Nova aula";
        formPlano.classList.remove("hidden");
    });
    
    btnCancelar.addEventListener("click", () => {
        formPlano.classList.add("hidden");
    });
    
    formPlano.addEventListener("submit", (e) => {
        e.preventDefault();
        salvarAulaPlano();
    });
}

function carregarPlanosDoStorage() {
    return aulasCache.planos;
}

async function salvarAulaPlano() {
    const corpo = {
        data: document.getElementById("aulaData").value,
        tema: document.getElementById("aulaTema").value,
        atividades: [...atividadesSelecionadasPlano],
        observacoes: document.getElementById("aulaObservacoes").value
    };

    const url = editandoPlanoId ? `/api/cursos/${cursoIdAtual}/planos/${editandoPlanoId}` : `/api/cursos/${cursoIdAtual}/planos`;
    const response = await CasaMulherAuth.apiFetch(url, { method: editandoPlanoId ? "PUT" : "POST", body: corpo });
    if (!response.ok) {
        let mensagem = "Não foi possível salvar a aula.";
        try { mensagem = (await response.json()).mensagem || mensagem; } catch { /* sem corpo */ }
        alert(mensagem);
        return;
    }

    await carregarAulas();
    renderizarDatalistAtividades();
    popularDatalistAulas();
    document.getElementById("formPlanoEnsino").classList.add("hidden");
    renderizarPlanoEnsino();
}

window.excluirAulaPlano = async function(id) {
    if (!confirm("Tem certeza que deseja excluir esta aula do plano de ensino?")) return;

    const response = await CasaMulherAuth.apiFetch(`/api/cursos/${cursoIdAtual}/planos/${id}`, { method: "DELETE" });
    if (!response.ok) {
        alert("Não foi possível excluir a aula.");
        return;
    }

    await carregarAulas();
    popularDatalistAulas();
    renderizarPlanoEnsino();
}

window.editarAulaPlano = function(id) {
    const planos = carregarPlanosDoStorage();
    const aula = planos.find(p => String(p.id) === String(id));
    if (!aula) return;
    
    editandoPlanoId = aula.id;
    document.getElementById("tituloFormPlano").textContent = "Editar aula";
    document.getElementById("aulaData").value = aula.data;
    document.getElementById("aulaTema").value = aula.tema;
    document.getElementById("aulaObservacoes").value = aula.observacoes || aula.conteudo || "";
    
    atividadesSelecionadasPlano = [...(aula.atividades || [])];
    renderizarChips(atividadesSelecionadasPlano, document.getElementById("chipsAtividadesPlano"), 'plano');
    
    document.getElementById("formPlanoEnsino").classList.remove("hidden");
}

function renderizarPlanoEnsino() {
    const grid = document.getElementById("planoGrid");
    const empty = document.getElementById("planoEmpty");
    
    const planos = carregarPlanosDoStorage();
    const aulasDoProfessor = planos.filter(p => true);
    
    aulasDoProfessor.sort((a, b) => a.data.localeCompare(b.data));
    
    grid.innerHTML = "";
    
    if (aulasDoProfessor.length === 0) {
        grid.classList.add("hidden");
        empty.classList.remove("hidden");
        return;
    }
    
    grid.classList.remove("hidden");
    empty.classList.add("hidden");
    
    aulasDoProfessor.forEach(aula => {
        const card = document.createElement("div");
        card.className = "professor-aula-card";
        
        const formatData = new Date(aula.data + "T00:00:00").toLocaleDateString('pt-BR');
        
        let chipsHtml = "";
        if (aula.atividades && aula.atividades.length > 0) {
            chipsHtml = `<div class="professor-chip-list" style="margin-top: 12px;">` + 
                aula.atividades.map(a => `<div class="professor-chip">${escapeHtml(a)}</div>`).join("") +
            `</div>`;
        }
        
        let obsTexto = aula.observacoes || aula.conteudo || "";
        let obsHtml = obsTexto ? `<div style="font-size: 0.9rem; color: #666; margin-top: 12px;"><strong>Observações:</strong> ${escapeHtml(obsTexto)}</div>` : '';
        
        let origemHtml = aula.origem === "criado_pela_chamada" ? `<div class="professor-origin-badge">Criada pela chamada</div>` : '';

        card.innerHTML = `
            <div class="professor-aula-header">
                <div>
                    <div class="professor-aula-data">${formatData}</div>
                    <div class="professor-aula-tema">${escapeHtml(aula.tema)}</div>
                    ${origemHtml}
                </div>
                <div style="display: flex; gap: 8px;">
                    <button type="button" class="professor-action-secondary" style="padding: 4px 12px; font-size: 0.8rem;" onclick="editarAulaPlano('${aula.id}')">Editar</button>
                    <button type="button" class="professor-action-danger" style="padding: 4px 12px; font-size: 0.8rem;" onclick="excluirAulaPlano('${aula.id}')">Excluir</button>
                </div>
            </div>
            ${chipsHtml}
            ${obsHtml}
        `;
        grid.appendChild(card);
    });
}
