/*
 * Tela da Psicologia: fila de encaminhadas, assistidas, cadastro direto, prontuário com
 * sessões (número do servidor), classificação de risco, adendos, responsáveis, sigilo,
 * acesso de emergência, encerramento e declaração de comparecimento.
 * A evolução nunca é guardada no navegador. A coordenação vê só os indicadores.
 */
document.addEventListener("DOMContentLoaded", setupPsicologia);

const ROTULOS_TIPO = { acolhimento: "Acolhimento / triagem", individual: "Sessão individual", crise: "Atendimento em crise" };
const ROTULOS_PRESENCA = { compareceu: "Compareceu", faltou: "Faltou", faltou_justificou: "Faltou e justificou", remarcou: "Remarcou" };
const ROTULOS_MODALIDADE = { presencial: "Presencial", online: "Online", telefone: "Telefone" };
const ROTULOS_RISCO = { baixo: "Baixo", moderado: "Moderado", alto: "Alto", iminente: "Iminente" };
const ROTULOS_ENCAMINHAMENTO = { caps: "CAPS", ubs: "UBS", cras_creas: "CRAS / CREAS", abrigo: "Abrigo / casa de passagem", outro: "Outro" };
const ROTULOS_ENCERRAMENTO = { alta: "Alta", desistencia: "Desistência", encaminhamento_externo: "Encaminhamento externo", mudanca_cidade: "Mudança de cidade", outro: "Outro" };

let psiUsuario = null;
let fichaAtual = null;
let profissionais = [];
let ultimaSessaoRegistrada = null;
let enviando = false;

const AF = () => window.CasaMulherAssistidaForm;
const esc = (v) => AF().escapeHtml(v);

async function setupPsicologia() {
    psiUsuario = await CasaMulherAuth.protegerPagina();
    if (!psiUsuario) return;

    if (!CasaMulherAuth.podeAcessar("psicologia")) {
        alert("Você não tem permissão para acessar a Psicologia.");
        window.location.href = CasaMulherAuth.getPainelUrl(psiUsuario);
        return;
    }

    inicializarSoftSessionCard(psiUsuario);
    bindEventos();
    iniciarAvisoSessao();

    // A coordenação acompanha a Psicologia apenas pelos números.
    if (psiUsuario.perfil === "adm") {
        document.querySelectorAll("[data-somente-psicologia]").forEach((b) => b.classList.add("hidden"));
        mostrarAba("indicadores");
        return;
    }

    AF().init(document.getElementById("cadastroForm"), mostrarMensagem);
    await Promise.all([carregarFila(), carregarProfissionais()]);
}

function bindEventos() {
    document.querySelectorAll("[data-tab]").forEach((botao) => {
        botao.addEventListener("click", () => mostrarAba(botao.dataset.tab));
    });

    document.getElementById("btnBuscarAssistida").addEventListener("click", carregarAssistidas);
    document.getElementById("escopoAssistidas").addEventListener("change", carregarAssistidas);
    document.getElementById("buscaAssistida").addEventListener("keyup", (e) => {
        if (e.key === "Enter") carregarAssistidas();
    });

    ["listaFila", "listaAssistidas"].forEach((id) => {
        document.getElementById(id).addEventListener("click", (e) => {
            const botao = e.target.closest("[data-abrir]");
            if (botao) abrirFicha(Number(botao.dataset.abrir));
        });
    });

    document.getElementById("cadastroForm").addEventListener("submit", salvarCadastro);
    document.getElementById("btnCancelarCadastro").addEventListener("click", cancelarCadastro);

    document.getElementById("btnCorrigirDados").addEventListener("click", corrigirDados);
    document.getElementById("btnEncaminharJuridico").addEventListener("click", encaminharJuridico);
    document.getElementById("btnRestricao").addEventListener("click", alternarRestricao);
    document.getElementById("btnAtribuir").addEventListener("click", atribuirResponsavel);
    document.getElementById("btnVisibilidade").addEventListener("click", salvarVisibilidade);
    document.getElementById("btnEmergencia").addEventListener("click", pedirEmergencia);
    document.getElementById("btnEncerrar").addEventListener("click", encerrarAcompanhamento);
    document.getElementById("fichaResponsaveis").addEventListener("click", (e) => {
        const botao = e.target.closest("[data-remover]");
        if (botao) removerResponsavel(botao.dataset.remover);
    });

    document.getElementById("sessaoForm").addEventListener("submit", registrarSessao);
    document.getElementById("presenca").addEventListener("change", atualizarCamposPresenca);
    document.getElementById("encaminhamentoOutroCheck").addEventListener("change", (e) => {
        document.getElementById("encaminhamentoOutro").classList.toggle("hidden", !e.target.checked);
    });
    document.getElementById("evolucao").addEventListener("input", atualizarContador);

    document.getElementById("fichaHistorico").addEventListener("click", (e) => {
        const adendo = e.target.closest("[data-adendo]");
        if (adendo) registrarAdendo(Number(adendo.dataset.adendo));
        const declaracao = e.target.closest("[data-declaracao]");
        if (declaracao) imprimirDeclaracao(Number(declaracao.dataset.declaracao));
    });

    document.getElementById("btnDeclaracaoModal").addEventListener("click", () => {
        if (ultimaSessaoRegistrada) imprimirDeclaracao(ultimaSessaoRegistrada.sessaoId);
    });
    document.getElementById("btnFecharModalSessao").addEventListener("click", fecharModalSessao);
    document.getElementById("btnOkModalSessao").addEventListener("click", fecharModalSessao);
}

function mostrarAba(aba) {
    document.querySelectorAll("[data-tab]").forEach((b) => b.classList.toggle("active", b.dataset.tab === aba));
    document.querySelectorAll("[data-panel]").forEach((p) => p.classList.toggle("active", p.dataset.panel === aba));

    if (aba === "fila") carregarFila();
    if (aba === "assistidas") carregarAssistidas();
    if (aba === "indicadores") carregarIndicadores();
}

function mostrarMensagem(texto, tipo) {
    const el = document.getElementById("mensagemJuridico");
    el.textContent = texto || "";
    el.className = `message ${tipo || ""}`.trim();
}

async function api(url, opcoes) {
    return CasaMulherAuth.apiFetch(url, Object.assign({ mensagemElement: document.getElementById("mensagemJuridico") }, opcoes || {}));
}

async function erroDe(response, padrao) {
    return AF().lerMensagemErro(response, padrao);
}

function formatarDataHora(valor) {
    if (!valor) return "-";
    const data = new Date(valor);
    return Number.isNaN(data.getTime()) ? valor : data.toLocaleString("pt-BR", { dateStyle: "short", timeStyle: "short" });
}

/* ---------------- Sessão: avisa antes de expirar para não perder a evolução ---------------- */
function iniciarAvisoSessao() {
    const atualizar = () => {
        const expira = new Date(CasaMulherAuth.getUsuario().expiraEm || 0).getTime();
        const aviso = document.getElementById("avisoSessao");
        if (!Number.isFinite(expira) || expira === 0) return;

        const restante = expira - Date.now();
        if (restante > 0 && restante <= 5 * 60 * 1000) {
            const minutos = Math.max(1, Math.ceil(restante / 60000));
            aviso.textContent = `Sua sessão termina em ${minutos} minuto(s). Salve a sessão antes disso: o texto digitado não fica guardado no navegador.`;
            aviso.classList.remove("hidden");
        } else {
            aviso.classList.add("hidden");
        }
    };
    atualizar();
    setInterval(atualizar, 20000);
}

/* ---------------- Listas ---------------- */
function cardResumo(a) {
    const badges = [];
    if (a.riscoElevado) badges.push('<span class="soft-badge soft-badge--risco">Risco alto</span>');
    if (a.acessoRestrito) badges.push('<span class="soft-badge soft-badge--restrito">Acesso restrito</span>');
    if (a.origem === "psicologia") badges.push('<span class="soft-badge soft-badge--psicologia">Cadastrada pela Psicologia</span>');
    if (a.situacao === "encerrado") badges.push('<span class="soft-badge soft-badge--ok">Encerrado</span>');
    if (a.visibilidade === "equipe") badges.push('<span class="soft-badge">Visível para a equipe</span>');

    const contato = a.semTelefoneSeguro ? "Sem telefone seguro" : a.contatoSeguro === "nao_ligar" ? "Não ligar" : (a.telefone || "-");
    const responsaveis = a.responsaveis.length ? a.responsaveis.map((r) => esc(r.nome)).join(", ") : "Sem responsável";

    return `
        <div class="entity-card">
            <div class="entity-title">${esc(a.nome)}</div>
            <div class="entity-info">CPF: ${esc(a.cpfMascarado)}</div>
            <div class="entity-info">Contato: ${esc(contato)}</div>
            ${a.numeroProntuario ? `<div class="entity-info">Prontuário: ${esc(a.numeroProntuario)}</div>` : ""}
            ${a.encaminhadaPsicologiaEm ? `<div class="entity-info">Encaminhada em: ${esc(AF().formatarData(a.encaminhadaPsicologiaEm))}</div>` : ""}
            <div class="entity-info">Com: ${responsaveis}</div>
            <div class="entity-info">Sessões: ${a.totalSessoes}${a.proximaSessao ? ` · Próxima ${esc(formatarDataHora(a.proximaSessao))}` : ""}</div>
            <div class="entity-info" style="display:flex; gap:6px; flex-wrap:wrap; margin-top:6px;">${badges.join("")}</div>
            <div class="entity-actions">
                <button type="button" class="soft-btn soft-btn-primary" data-abrir="${a.assistidaId}">Abrir prontuário</button>
            </div>
        </div>`;
}

function renderizarLista(id, lista, vazio) {
    const el = document.getElementById(id);
    el.innerHTML = lista.length ? lista.map(cardResumo).join("") : `<div class="soft-empty-state">${esc(vazio)}</div>`;
}

async function carregarFila() {
    const response = await api("/api/psicologia/assistidas?escopo=fila");
    if (!response.ok) return;
    renderizarLista("listaFila", await response.json(), "Nenhuma assistida aguardando o primeiro atendimento.");
}

async function carregarAssistidas() {
    const busca = document.getElementById("buscaAssistida").value.trim();
    const params = new URLSearchParams({ escopo: document.getElementById("escopoAssistidas").value });
    if (busca) params.set("busca", busca);

    const response = await api(`/api/psicologia/assistidas?${params}`);
    if (!response.ok) return;
    renderizarLista(
        "listaAssistidas",
        await response.json(),
        busca ? "Ninguém encontrado. Se ela veio direto à Psicologia, use a aba Cadastrar assistida." : "Nenhum prontuário nesta lista.");
}

async function carregarProfissionais() {
    const response = await api("/api/psicologia/profissionais");
    profissionais = response.ok ? await response.json() : [];
}

async function carregarIndicadores() {
    const response = await api("/api/psicologia/indicadores");
    if (!response.ok) return;
    const ind = await response.json();
    document.getElementById("indFila").textContent = ind.fila;
    document.getElementById("indAcompanhamento").textContent = ind.emAcompanhamento;
    document.getElementById("indSessoes").textContent = ind.sessoesNoMes;
    document.getElementById("indFaltas").textContent = ind.faltasNoMes;
    document.getElementById("indRisco").textContent = ind.riscoElevado;

    const encaminhamentos = Object.entries(ind.encaminhamentosNoMes || {});
    document.getElementById("indEncaminhamentos").innerHTML = encaminhamentos.length
        ? '<h3 class="soft-fieldset-title">Encaminhamentos externos no mês</h3>' + encaminhamentos
            .map(([chave, total]) => `<span class="soft-badge" style="margin: 0 6px 6px 0;">${esc(ROTULOS_ENCAMINHAMENTO[chave] || chave)}: ${total}</span>`)
            .join("")
        : "";
}

/* ---------------- Cadastro direto pela Psicologia ---------------- */
function cancelarCadastro() {
    AF().limpar(document.getElementById("cadastroForm"));
    document.getElementById("cadastroAssistidaId").value = "";
    document.getElementById("cadastroTitulo").textContent = "Cadastrar assistida";
    document.getElementById("cadastroSubmitLabel").textContent = "Cadastrar e abrir ficha";
    document.getElementById("cadastroAviso").classList.remove("hidden");
    mostrarAba(fichaAtual ? "ficha" : "fila");
}

async function salvarCadastro(e) {
    e.preventDefault();
    if (enviando) return;

    const form = e.currentTarget;
    const id = document.getElementById("cadastroAssistidaId").value;
    const dados = AF().ler(form);
    const erro = AF().validar(dados);
    if (erro) {
        mostrarMensagem(erro, "error");
        return;
    }

    if (id && fichaAtual) {
        // Correção: mantém o que a Recepção preencheu e a Psicologia não vê neste formulário.
        dados.observacoes = fichaAtual.assistida.observacoes;
        dados.encaminharJuridico = fichaAtual.assistida.encaminhadaJuridico;
        dados.encaminharPsicologia = true;
    }

    enviando = true;
    const response = await api(id ? `/api/assistidas/${id}` : "/api/assistidas", { method: id ? "PUT" : "POST", body: dados });
    enviando = false;

    if (response.status === 409) {
        const conflito = await response.json().catch(() => ({}));
        mostrarMensagem(conflito.mensagem || "Já existe cadastro com este CPF.", "error");
        if (conflito.assistidaId && confirm("Esta assistida já tem cadastro. Abrir a ficha existente?")) {
            AF().limpar(form);
            abrirFicha(conflito.assistidaId);
        }
        return;
    }

    if (!response.ok) {
        mostrarMensagem(await erroDe(response, "Não foi possível salvar o cadastro."), "error");
        return;
    }

    const salvo = await response.json();
    AF().limpar(form);
    document.getElementById("cadastroAssistidaId").value = "";
    mostrarMensagem((id ? "Dados corrigidos." : "Assistida cadastrada.") + (salvo.avisoSnapshot ? ` ${salvo.avisoSnapshot}` : ""), salvo.avisoSnapshot ? "info" : "success");
    abrirFicha(salvo.id);
}

function corrigirDados() {
    if (!fichaAtual) return;
    AF().preencher(document.getElementById("cadastroForm"), fichaAtual.assistida);
    document.getElementById("cadastroAssistidaId").value = fichaAtual.assistida.id;
    document.getElementById("cadastroTitulo").textContent = `Corrigir dados de ${fichaAtual.assistida.nome}`;
    document.getElementById("cadastroSubmitLabel").textContent = "Salvar correção";
    document.getElementById("cadastroAviso").classList.add("hidden");
    mostrarAba("cadastro");
}

/* ---------------- Ficha / prontuário ---------------- */
async function abrirFicha(assistidaId) {
    const response = await api(`/api/psicologia/prontuarios/${assistidaId}`);
    if (!response.ok) {
        mostrarMensagem(await erroDe(response, "Não foi possível abrir o prontuário."), "error");
        return;
    }

    fichaAtual = await response.json();
    const tab = document.getElementById("tabFicha");
    tab.classList.remove("hidden");
    tab.textContent = `Ficha: ${fichaAtual.assistida.nome.split(" ")[0]}`;
    renderizarFicha();
    mostrarAba("ficha");
    window.scrollTo({ top: 0, behavior: "smooth" });
}

function renderizarFicha() {
    const f = fichaAtual;
    const a = f.assistida;

    document.getElementById("fichaNome").textContent = a.nome;
    document.getElementById("fichaRisco").classList.toggle("hidden", !f.riscoElevado);
    document.getElementById("fichaRestrito").classList.toggle("hidden", !a.acessoRestrito);

    const badges = [];
    if (f.numeroProntuario) badges.push(`<span class="soft-badge soft-badge--psicologia">Prontuário ${esc(f.numeroProntuario)}</span>`);
    if (f.riscoElevado) badges.push(`<span class="soft-badge soft-badge--risco">Risco ${esc(ROTULOS_RISCO[f.riscoAtual] || f.riscoAtual)}</span>`);
    if (f.situacao === "encerrado") badges.push(`<span class="soft-badge soft-badge--ok">Encerrado · ${esc(ROTULOS_ENCERRAMENTO[f.motivoEncerramento] || "")}</span>`);
    if (a.acessoRestrito) badges.push(`<span class="soft-badge soft-badge--restrito">Acesso restrito · ${esc(AF().ROTULOS_MOTIVO_RESTRICAO[a.motivoRestricao] || "")}</span>`);
    if (f.emAtendimentoJuridico) badges.push('<span class="soft-badge soft-badge--juridico">Em atendimento no Jurídico</span>');
    else if (a.encaminhadaJuridico) badges.push('<span class="soft-badge soft-badge--juridico">Encaminhada ao Jurídico</span>');
    document.getElementById("fichaBadges").innerHTML = badges.join("");

    const origem = { recepcao: "Recepção", juridico: "Jurídico", psicologia: "Psicologia" }[a.origem] || a.origem;
    const dados = [
        ["CPF", a.cpfMascarado],
        ["Nascimento", AF().formatarData(a.dataNascimento)],
        ["Contato", AF().formatarContato(a)],
        ["Observação de contato", a.contatoSeguroObservacao || "-"],
        ["Endereço", AF().formatarEndereco(a)],
        ["Cadastrada por", `${a.funcionarioAtendimentoNomeSnapshot || "-"} (${origem})`]
    ];
    document.getElementById("fichaDados").innerHTML = dados
        .map(([rotulo, valor]) => `<div><dt>${esc(rotulo)}</dt><dd>${esc(valor)}</dd></div>`)
        .join("");

    document.getElementById("btnEncaminharJuridico").classList.toggle("hidden", a.encaminhadaJuridico);
    const motivo = document.getElementById("motivoRestricao");
    motivo.value = a.motivoRestricao || "";
    motivo.classList.toggle("hidden", a.acessoRestrito);
    document.getElementById("btnRestricao").textContent = a.acessoRestrito ? "Remover acesso restrito" : "Marcar acesso restrito";

    document.getElementById("fichaResponsaveis").innerHTML = f.responsaveis.length
        ? f.responsaveis.map((r) => `
            <span class="juridico-responsavel">${esc(r.nome)} <small>(${esc(r.identificador)}${r.crp ? " · CRP " + esc(r.crp) : ""})</small>
                ${f.podeAtribuir ? `<button type="button" data-remover="${esc(r.userId)}" title="Remover responsável" aria-label="Remover ${esc(r.nome)}">&times;</button>` : ""}
            </span>`).join("")
        : '<p class="section-intro">Ainda sem responsável. Quem registrar a primeira sessão vira responsável automaticamente.</p>';

    const select = document.getElementById("selectProfissional");
    const disponiveis = profissionais.filter((p) => !f.responsaveis.some((r) => r.userId === p.userId));
    select.innerHTML = '<option value="">Escolha uma profissional da Psicologia...</option>'
        + disponiveis.map((p) => `<option value="${esc(p.userId)}">${esc(p.nome)} (${esc(p.identificador)})</option>`).join("");
    document.getElementById("blocoAtribuir").classList.toggle("hidden", !f.podeAtribuir || !f.prontuarioId);

    document.getElementById("selectVisibilidade").value = f.visibilidade;
    document.getElementById("blocoVisibilidade").classList.toggle("hidden", !f.podeAlterarVisibilidade);
    document.getElementById("blocoEmergencia").classList.toggle("hidden", !f.podePedirAcessoEmergencia);

    const emergencia = document.getElementById("fichaEmergenciaAtiva");
    const usandoEmergencia = f.acessoEmergenciaExpiraEm && !f.souResponsavel && f.visibilidade === "responsaveis";
    emergencia.classList.toggle("hidden", !usandoEmergencia);
    if (usandoEmergencia) {
        emergencia.textContent = `Você está com acesso de emergência até ${new Date(f.acessoEmergenciaExpiraEm).toLocaleString("pt-BR")}.`;
    }

    document.getElementById("cardNovaSessao").classList.toggle("hidden", !f.podeRegistrar);
    document.getElementById("blocoConsentimento").classList.toggle("hidden", f.consentimentoRegistrado);
    document.getElementById("cardEncerrar").classList.toggle("hidden", !f.podeRegistrar || !f.totalSessoes || f.situacao === "encerrado");
    atualizarCamposPresenca();
    renderizarHistorico();
}

function renderizarHistorico() {
    const f = fichaAtual;
    const resumo = [`<span class="soft-badge">${f.totalSessoes} sessão(ões)</span>`];
    if (f.podeVerConteudo) {
        resumo.push(`<span class="soft-badge">${f.totalFaltas} falta(s)</span>`);
        if (f.faltasSeguidas >= 2) resumo.push(`<span class="soft-badge soft-badge--risco">${f.faltasSeguidas} faltas seguidas</span>`);
    }
    document.getElementById("fichaResumoSessoes").innerHTML = f.totalSessoes ? resumo.join("") : "";

    const el = document.getElementById("fichaHistorico");
    if (!f.sessoes.length) {
        el.innerHTML = '<div class="soft-empty-state">Nenhuma sessão registrada ainda.</div>';
        return;
    }

    el.innerHTML = f.sessoes.map((s) => {
        const topo = `
            <div class="juridico-atendimento-topo">
                <span class="juridico-protocolo">Sessão ${s.numero}</span>
                ${s.presenca ? `<span class="soft-badge ${s.presenca === "compareceu" ? "" : "soft-badge--risco"}">${esc(ROTULOS_PRESENCA[s.presenca] || s.presenca)}</span>` : ""}
            </div>
            <div class="entity-info">${esc(formatarDataHora(s.criadoEm))} · ${esc(s.profissionalNome)} (${esc(s.profissionalIdentificador)}${s.profissionalCrp ? " · CRP " + esc(s.profissionalCrp) : ""})</div>`;

        if (s.conteudoOculto) {
            return `<div class="juridico-atendimento">${topo}
                <p class="section-intro" style="margin-top: 8px;">Conteúdo visível somente para quem cuida do caso.</p></div>`;
        }

        const encaminhamentos = s.encaminhamentosExternos
            .map((e) => (e === "outro" ? `Outro: ${s.encaminhamentoExternoOutro || ""}` : ROTULOS_ENCAMINHAMENTO[e] || e))
            .join(", ") || "Nenhum";
        const adendos = s.adendos.map((ad) => `
            <div class="juridico-adendo"><strong>Adendo de ${esc(ad.autorNome)} (${esc(ad.autorIdentificador)}) em ${esc(formatarDataHora(ad.criadoEm))}:</strong>
${esc(ad.texto)}</div>`).join("");

        return `<div class="juridico-atendimento">${topo}
            <dl class="juridico-dados" style="margin-top: 12px;">
                <div><dt>Tipo</dt><dd>${esc(ROTULOS_TIPO[s.tipoAtendimento] || s.tipoAtendimento)}</dd></div>
                <div><dt>Modalidade</dt><dd>${esc(ROTULOS_MODALIDADE[s.modalidade] || "-")}</dd></div>
                <div><dt>Risco</dt><dd>${esc(ROTULOS_RISCO[s.risco] || "-")}</dd></div>
                <div><dt>Demanda</dt><dd>${esc(s.demanda || "-")}</dd></div>
                <div><dt>Plano</dt><dd>${esc(s.plano || "-")}</dd></div>
                <div><dt>Próxima sessão</dt><dd>${esc(formatarDataHora(s.proximaSessao))}</dd></div>
                <div><dt>Encaminhamentos</dt><dd>${esc(encaminhamentos)}</dd></div>
            </dl>
            ${s.evolucao ? `<div class="juridico-relato">${esc(s.evolucao)}</div>` : ""}
            ${adendos}
            ${f.podeRegistrar ? `
                <textarea class="soft-input" id="adendoTexto${s.id}" maxlength="2000" placeholder="Correção ou complemento (adendo)" style="min-height: 50px; margin-top: 8px;"></textarea>
                <div class="juridico-linha-acoes" style="margin-top: 8px;">
                    <button type="button" class="soft-btn soft-btn-warning" data-adendo="${s.id}">Adicionar adendo</button>
                    ${s.presenca === "compareceu" ? `<button type="button" class="soft-btn soft-btn-warning" data-declaracao="${s.id}">Declaração de comparecimento</button>` : ""}
                </div>` : ""}
        </div>`;
    }).join("");
}

function atualizarCamposPresenca() {
    const compareceu = document.getElementById("presenca").value === "compareceu";
    document.getElementById("blocoComparecimento").classList.toggle("hidden", !compareceu);
    document.getElementById("evolucaoLabel").textContent = compareceu ? "Evolução da sessão *" : "Observação (opcional)";
    if (fichaAtual) {
        document.getElementById("blocoConsentimento").classList.toggle("hidden", fichaAtual.consentimentoRegistrado || !compareceu);
    }
}

function atualizarContador() {
    const total = document.getElementById("evolucao").value.length;
    const contador = document.getElementById("evolucaoContador");
    contador.textContent = `${total} / 5000 caracteres`;
    contador.classList.toggle("limite", total >= 4500);
}

async function registrarSessao(e) {
    e.preventDefault();
    if (enviando || !fichaAtual) return;

    const compareceu = document.getElementById("presenca").value === "compareceu";
    const dados = {
        tipoAtendimento: document.getElementById("tipoAtendimento").value,
        presenca: document.getElementById("presenca").value,
        modalidade: compareceu ? document.getElementById("modalidade").value : null,
        risco: compareceu ? document.getElementById("risco").value || null : null,
        demanda: compareceu ? document.getElementById("demanda").value.trim() || null : null,
        evolucao: document.getElementById("evolucao").value.trim() || null,
        plano: document.getElementById("plano").value.trim() || null,
        proximaSessao: document.getElementById("proximaSessao").value || null,
        encaminhamentosExternos: Array.from(document.querySelectorAll('#sessaoForm input[name="encaminhamento"]:checked')).map((c) => c.value),
        encaminhamentoExternoOutro: document.getElementById("encaminhamentoOutro").value.trim() || null,
        consentimentoSigilo: document.getElementById("consentimentoSigilo").checked,
        encaminharJuridico: document.getElementById("encaminharJuridico").checked
    };

    if (compareceu) {
        if (!dados.risco) return mostrarMensagem("Classifique o risco.", "error");
        if ((dados.evolucao || "").length < 15) return mostrarMensagem("Descreva a evolução com mais detalhes (mínimo de 15 caracteres).", "error");
        if (!fichaAtual.consentimentoRegistrado && !dados.consentimentoSigilo) {
            return mostrarMensagem("No primeiro atendimento, confirme que a assistida foi informada sobre o sigilo e seus limites.", "error");
        }
    }

    enviando = true;
    document.getElementById("btnSalvarSessao").disabled = true;
    const response = await api(`/api/psicologia/prontuarios/${fichaAtual.assistida.id}/sessoes`, { method: "POST", body: dados });
    enviando = false;
    document.getElementById("btnSalvarSessao").disabled = false;

    if (!response.ok) {
        // O texto continua no formulário para não ser perdido.
        mostrarMensagem(await erroDe(response, "Não foi possível registrar a sessão."), "error");
        return;
    }

    ultimaSessaoRegistrada = await response.json();
    document.getElementById("sessaoForm").reset();
    document.getElementById("encaminhamentoOutro").classList.add("hidden");
    atualizarContador();
    mostrarModalSessao(ultimaSessaoRegistrada);
    mostrarMensagem(`Sessão ${ultimaSessaoRegistrada.numeroSessao} do prontuário ${ultimaSessaoRegistrada.numeroProntuario} registrada.`, "success");
    await abrirFicha(fichaAtual.assistida.id);
}

function mostrarModalSessao(r) {
    document.getElementById("modalProntuario").textContent = r.numeroProntuario;
    document.getElementById("modalNumeroSessao").textContent = r.numeroSessao;
    document.getElementById("modalAviso").textContent = r.avisoSnapshot || "";
    document.getElementById("btnDeclaracaoModal").classList.toggle("hidden", r.presenca !== "compareceu");
    document.getElementById("modalSessao").classList.remove("hidden");
}

function fecharModalSessao() {
    document.getElementById("modalSessao").classList.add("hidden");
}

async function imprimirDeclaracao(sessaoId) {
    const response = await api(`/api/psicologia/sessoes/${sessaoId}/declaracao`);
    if (!response.ok) {
        mostrarMensagem(await erroDe(response, "Não foi possível gerar a declaração."), "error");
        return;
    }

    const d = await response.json();
    document.getElementById("impNome").textContent = d.assistidaNome;
    document.getElementById("impData").textContent = formatarDataHora(d.data);
    document.getElementById("impProfissional").textContent = d.profissionalNome;
    document.getElementById("impCrp").textContent = d.profissionalCrp ? `Psicóloga - CRP ${d.profissionalCrp}` : "Psicóloga";
    window.print();
}

async function registrarAdendo(sessaoId) {
    const texto = document.getElementById(`adendoTexto${sessaoId}`)?.value.trim() || "";
    if (texto.length < 3) return mostrarMensagem("Escreva o adendo antes de salvar.", "error");

    const response = await api(`/api/psicologia/sessoes/${sessaoId}/adendos`, { method: "POST", body: { texto } });
    if (!response.ok) return mostrarMensagem(await erroDe(response, "Não foi possível salvar o adendo."), "error");

    mostrarMensagem("Adendo registrado.", "success");
    await abrirFicha(fichaAtual.assistida.id);
}

async function encaminharJuridico() {
    if (!confirm("Encaminhar esta assistida para a fila do Jurídico? Nada das sessões é compartilhado.")) return;

    const response = await api(`/api/assistidas/${fichaAtual.assistida.id}/encaminhar`, { method: "POST", body: { area: "juridico" } });
    if (!response.ok) return mostrarMensagem(await erroDe(response, "Não foi possível encaminhar."), "error");

    mostrarMensagem("Assistida encaminhada ao Jurídico.", "success");
    await abrirFicha(fichaAtual.assistida.id);
}

async function encerrarAcompanhamento() {
    const motivo = document.getElementById("motivoEncerramento").value;
    if (!motivo) return mostrarMensagem("Escolha o motivo do encerramento.", "error");
    if (!confirm("Encerrar o acompanhamento? O prontuário continua guardado e uma nova sessão o reabre.")) return;

    const response = await api(`/api/psicologia/prontuarios/${fichaAtual.assistida.id}/encerrar`, { method: "POST", body: { motivo } });
    if (!response.ok) return mostrarMensagem(await erroDe(response, "Não foi possível encerrar."), "error");

    mostrarMensagem("Acompanhamento encerrado.", "success");
    await abrirFicha(fichaAtual.assistida.id);
}

async function alternarRestricao() {
    const a = fichaAtual.assistida;
    const restringir = !a.acessoRestrito;
    const motivo = document.getElementById("motivoRestricao").value;

    if (restringir && !motivo) return mostrarMensagem("Escolha o motivo do acesso restrito.", "error");

    const pergunta = restringir
        ? "A assistida deixará de aparecer para Recepção, Professor e outros perfis. Confirmar?"
        : "A assistida voltará a aparecer para os outros perfis (só dados básicos). Confirmar?";
    if (!confirm(pergunta)) return;

    const response = await api(`/api/assistidas/${a.id}/restricao`, { method: "PUT", body: { acessoRestrito: restringir, motivo: restringir ? motivo : null } });
    if (!response.ok) return mostrarMensagem(await erroDe(response, "Não foi possível alterar o acesso restrito."), "error");

    mostrarMensagem(restringir ? "Acesso restrito ativado." : "Acesso restrito removido.", "success");
    await abrirFicha(a.id);
}

async function atribuirResponsavel() {
    const userId = document.getElementById("selectProfissional").value;
    if (!userId) return mostrarMensagem("Escolha a profissional.", "error");

    const response = await api(`/api/psicologia/prontuarios/${fichaAtual.assistida.id}/responsaveis`, { method: "POST", body: { userId } });
    if (!response.ok) return mostrarMensagem(await erroDe(response, "Não foi possível atribuir."), "error");

    mostrarMensagem("Responsável atribuída.", "success");
    await abrirFicha(fichaAtual.assistida.id);
}

async function removerResponsavel(userId) {
    if (!confirm("Remover esta responsável do prontuário?")) return;

    const response = await api(`/api/psicologia/prontuarios/${fichaAtual.assistida.id}/responsaveis/${encodeURIComponent(userId)}`, { method: "DELETE" });
    if (!response.ok) return mostrarMensagem(await erroDe(response, "Não foi possível remover."), "error");

    mostrarMensagem("Responsável removida.", "success");
    await abrirFicha(fichaAtual.assistida.id);
}

async function salvarVisibilidade() {
    const visibilidade = document.getElementById("selectVisibilidade").value;
    const response = await api(`/api/psicologia/prontuarios/${fichaAtual.assistida.id}/visibilidade`, { method: "PUT", body: { visibilidade } });
    if (!response.ok) return mostrarMensagem(await erroDe(response, "Não foi possível salvar."), "error");

    mostrarMensagem(visibilidade === "equipe" ? "Agora toda a equipe de Psicologia vê as anotações." : "Agora só as responsáveis veem as anotações.", "success");
    await abrirFicha(fichaAtual.assistida.id);
}

async function pedirEmergencia() {
    const motivo = document.getElementById("motivoEmergencia").value.trim();
    if (motivo.length < 10) return mostrarMensagem("Explique o motivo (pelo menos 10 caracteres).", "error");

    const response = await api(`/api/psicologia/prontuarios/${fichaAtual.assistida.id}/acesso-emergencia`, { method: "POST", body: { motivo } });
    if (!response.ok) return mostrarMensagem(await erroDe(response, "Não foi possível liberar o acesso."), "error");

    document.getElementById("motivoEmergencia").value = "";
    mostrarMensagem("Acesso de emergência liberado por 12 horas. As responsáveis foram avisadas.", "success");
    await abrirFicha(fichaAtual.assistida.id);
}
