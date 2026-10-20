/*
 * Tela do Jurídico: fila de encaminhadas, assistidas, cadastro direto, ficha com
 * atendimentos (protocolo do servidor), adendos, responsáveis, visibilidade,
 * acesso restrito e acesso de emergência. O relato nunca é guardado no navegador.
 */
document.addEventListener("DOMContentLoaded", setupJuridico);

const ROTULOS_TIPO = {
    medida_protetiva: "Medida protetiva",
    divorcio: "Divórcio",
    guarda: "Guarda",
    pensao: "Pensão alimentícia",
    partilha: "Partilha de bens",
    violencia_patrimonial: "Violência patrimonial",
    outro: "Outro"
};

const ROTULOS_ENCAMINHAMENTO = {
    defensoria: "Defensoria Pública",
    delegacia_mulher: "Delegacia da Mulher",
    creas: "CREAS",
    conselho_tutelar: "Conselho Tutelar",
    outro: "Outro"
};

const ROTULOS_SITUACAO = {
    em_andamento: "Em andamento",
    aguardando_documento: "Aguardando documento",
    encerrado: "Encerrado"
};

let juridicoUsuario = null;
let fichaAtual = null;
let profissionais = [];
let ultimoComprovante = null;
let enviando = false;

const AF = () => window.CasaMulherAssistidaForm;
const esc = (v) => AF().escapeHtml(v);

async function setupJuridico() {
    juridicoUsuario = await CasaMulherAuth.protegerPagina();
    if (!juridicoUsuario) return;

    if (!CasaMulherAuth.podeAcessar("juridico")) {
        alert("Você não tem permissão para acessar o Jurídico.");
        window.location.href = CasaMulherAuth.getPainelUrl(juridicoUsuario);
        return;
    }

    inicializarSoftSessionCard(juridicoUsuario);

    // A coordenação acompanha, mas não registra atendimento nem cadastra pelo Jurídico.
    if (juridicoUsuario.perfil === "adm") {
        document.querySelector('[data-tab="cadastro"]')?.classList.add("hidden");
    }

    AF().init(document.getElementById("cadastroForm"), mostrarMensagem);
    bindEventos();
    iniciarAvisoSessao();
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
    document.getElementById("btnEncaminharPsicologia").addEventListener("click", encaminharPsicologia);
    document.getElementById("btnRestricao").addEventListener("click", alternarRestricao);
    document.getElementById("btnAtribuir").addEventListener("click", atribuirResponsavel);
    document.getElementById("btnVisibilidade").addEventListener("click", salvarVisibilidade);
    document.getElementById("btnEmergencia").addEventListener("click", pedirEmergencia);
    document.getElementById("fichaResponsaveis").addEventListener("click", (e) => {
        const botao = e.target.closest("[data-remover]");
        if (botao) removerResponsavel(botao.dataset.remover);
    });

    document.getElementById("atendimentoForm").addEventListener("submit", registrarAtendimento);
    document.getElementById("tipoDemanda").addEventListener("change", (e) => {
        document.getElementById("tipoDemandaOutro").classList.toggle("hidden", e.target.value !== "outro");
    });
    document.getElementById("encaminhamentoOutroCheck").addEventListener("change", (e) => {
        document.getElementById("encaminhamentoOutro").classList.toggle("hidden", !e.target.checked);
    });
    document.getElementById("relato").addEventListener("input", atualizarContador);

    document.getElementById("fichaHistorico").addEventListener("click", (e) => {
        const botao = e.target.closest("[data-adendo]");
        if (botao) registrarAdendo(Number(botao.dataset.adendo));
    });

    document.getElementById("btnImprimirComprovante").addEventListener("click", () => window.print());
    document.getElementById("btnFecharComprovante").addEventListener("click", fecharComprovante);
    document.getElementById("btnOkComprovante").addEventListener("click", fecharComprovante);
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

/* ---------------- Sessão: avisa antes de expirar para não perder o relato ---------------- */
function iniciarAvisoSessao() {
    const atualizar = () => {
        const expira = new Date(CasaMulherAuth.getUsuario().expiraEm || 0).getTime();
        const aviso = document.getElementById("avisoSessao");
        if (!Number.isFinite(expira) || expira === 0) return;

        const restante = expira - Date.now();
        if (restante > 0 && restante <= 5 * 60 * 1000) {
            const minutos = Math.max(1, Math.ceil(restante / 60000));
            aviso.textContent = `Sua sessão termina em ${minutos} minuto(s). Salve o atendimento antes disso: o texto digitado não fica guardado no navegador.`;
            aviso.classList.remove("hidden");
        } else {
            aviso.classList.add("hidden");
        }
    };
    atualizar();
    setInterval(atualizar, 20000);
}

/* ---------------- Listas ---------------- */
function rotuloContatoCurto(a) {
    if (a.semTelefoneSeguro) return "Sem telefone seguro";
    if (a.contatoSeguro === "nao_ligar") return "Não ligar";
    return a.telefone || "-";
}

function cardResumo(a) {
    const badges = [];
    if (a.acessoRestrito) badges.push('<span class="soft-badge soft-badge--restrito">Acesso restrito</span>');
    if (a.origem === "juridico") badges.push('<span class="soft-badge soft-badge--juridico">Cadastrada pelo Jurídico</span>');
    if (a.situacao) badges.push(`<span class="soft-badge ${a.situacao === "encerrado" ? "soft-badge--ok" : ""}">${esc(ROTULOS_SITUACAO[a.situacao] || a.situacao)}</span>`);
    if (a.visibilidade === "responsaveis") badges.push('<span class="soft-badge">Somente responsáveis</span>');

    const responsaveis = a.responsaveis.length
        ? a.responsaveis.map((r) => esc(r.nome)).join(", ")
        : "Sem responsável";

    return `
        <div class="entity-card">
            <div class="entity-title">${esc(a.nome)}</div>
            <div class="entity-info">CPF: ${esc(a.cpfMascarado)}</div>
            <div class="entity-info">Contato: ${esc(rotuloContatoCurto(a))}</div>
            ${a.encaminhadaJuridicoEm ? `<div class="entity-info">Encaminhada em: ${esc(AF().formatarData(a.encaminhadaJuridicoEm))}</div>` : ""}
            <div class="entity-info">Com: ${responsaveis}</div>
            <div class="entity-info">Atendimentos: ${a.totalAtendimentos}${a.proximoRetorno ? ` · Retorno ${esc(AF().formatarData(a.proximoRetorno))}` : ""}</div>
            <div class="entity-info" style="display:flex; gap:6px; flex-wrap:wrap; margin-top:6px;">${badges.join("")}</div>
            <div class="entity-actions">
                <button type="button" class="soft-btn soft-btn-primary" data-abrir="${a.assistidaId}">Abrir ficha</button>
            </div>
        </div>`;
}

function renderizarLista(id, lista, vazio) {
    const el = document.getElementById(id);
    el.innerHTML = lista.length
        ? lista.map(cardResumo).join("")
        : `<div class="soft-empty-state">${esc(vazio)}</div>`;
}

async function carregarFila() {
    const response = await api("/api/juridico/assistidas?escopo=fila");
    if (!response.ok) return;
    renderizarLista("listaFila", await response.json(), "Nenhuma assistida aguardando o primeiro atendimento.");
}

async function carregarAssistidas() {
    const busca = document.getElementById("buscaAssistida").value.trim();
    const escopo = document.getElementById("escopoAssistidas").value;
    const params = new URLSearchParams({ escopo });
    if (busca) params.set("busca", busca);

    const response = await api(`/api/juridico/assistidas?${params}`);
    if (!response.ok) return;
    renderizarLista(
        "listaAssistidas",
        await response.json(),
        busca
            ? "Ninguém encontrado. Se ela veio direto ao Jurídico, use a aba Cadastrar assistida."
            : "Nenhum caso nesta lista.");
}

async function carregarProfissionais() {
    const response = await api("/api/juridico/profissionais");
    profissionais = response.ok ? await response.json() : [];
}

async function carregarIndicadores() {
    const response = await api("/api/juridico/indicadores");
    if (!response.ok) return;
    const ind = await response.json();
    document.getElementById("indFila").textContent = ind.fila;
    document.getElementById("indAndamento").textContent = ind.casosEmAndamento;
    document.getElementById("indMes").textContent = ind.atendimentosNoMes;
    document.getElementById("indRetornos").textContent = ind.retornosProximos7Dias;

    const tipos = Object.entries(ind.atendimentosPorTipo || {});
    document.getElementById("indPorTipo").innerHTML = tipos.length
        ? `<h3 class="soft-fieldset-title">Atendimentos do mês por tipo</h3>` + tipos
            .map(([tipo, total]) => `<span class="soft-badge" style="margin: 0 6px 6px 0;">${esc(ROTULOS_TIPO[tipo] || tipo)}: ${total}</span>`)
            .join("")
        : "";
}

/* ---------------- Cadastro direto pelo Jurídico ---------------- */
function cancelarCadastro() {
    AF().limpar(document.getElementById("cadastroForm"));
    document.getElementById("cadastroAssistidaId").value = "";
    document.getElementById("cadastroTitulo").textContent = "Cadastrar assistida";
    document.getElementById("cadastroSubmitLabel").textContent = "Cadastrar e abrir ficha";
    document.getElementById("cadastroAviso").classList.remove("hidden");

    if (fichaAtual) {
        mostrarAba("ficha");
    } else {
        mostrarAba("fila");
    }
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
        // Correção: mantém o que a Recepção preencheu e o Jurídico não vê neste formulário.
        dados.direcionamento = fichaAtual.assistida.direcionamento;
        dados.observacoes = fichaAtual.assistida.observacoes;
        dados.encaminharJuridico = true;
    }

    enviando = true;
    const response = await api(id ? `/api/assistidas/${id}` : "/api/assistidas", {
        method: id ? "PUT" : "POST",
        body: dados
    });
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
    const form = document.getElementById("cadastroForm");
    AF().preencher(form, fichaAtual.assistida);
    document.getElementById("cadastroAssistidaId").value = fichaAtual.assistida.id;
    document.getElementById("cadastroTitulo").textContent = `Corrigir dados de ${fichaAtual.assistida.nome}`;
    document.getElementById("cadastroSubmitLabel").textContent = "Salvar correção";
    document.getElementById("cadastroAviso").classList.add("hidden");
    mostrarAba("cadastro");
}

/* ---------------- Ficha ---------------- */
async function abrirFicha(assistidaId) {
    const response = await api(`/api/juridico/casos/${assistidaId}`);
    if (!response.ok) {
        mostrarMensagem(await erroDe(response, "Não foi possível abrir a ficha."), "error");
        return;
    }

    fichaAtual = await response.json();
    document.getElementById("tabFicha").classList.remove("hidden");
    document.getElementById("tabFicha").textContent = `Ficha: ${fichaAtual.assistida.nome.split(" ")[0]}`;
    renderizarFicha();
    mostrarAba("ficha");
    window.scrollTo({ top: 0, behavior: "smooth" });
}

function renderizarFicha() {
    const f = fichaAtual;
    const a = f.assistida;
    const ehJuridico = juridicoUsuario.perfil !== "adm";

    document.getElementById("fichaNome").textContent = a.nome;
    document.getElementById("fichaRestrito").classList.toggle("hidden", !a.acessoRestrito);

    const badges = [];
    if (a.acessoRestrito) badges.push(`<span class="soft-badge soft-badge--restrito">Acesso restrito · ${esc(AF().ROTULOS_MOTIVO_RESTRICAO[a.motivoRestricao] || "")}</span>`);
    if (a.origem === "juridico") badges.push('<span class="soft-badge soft-badge--juridico">Cadastrada pelo Jurídico</span>');
    if (f.atendimentos.length) badges.push(`<span class="soft-badge">${esc(ROTULOS_SITUACAO[f.situacao] || f.situacao)}</span>`);
    if (f.acompanhamentoPsicologico) badges.push('<span class="soft-badge soft-badge--psicologia">Em acompanhamento na Psicologia</span>');
    else if (f.encaminhadaPsicologia) badges.push('<span class="soft-badge soft-badge--psicologia">Encaminhada à Psicologia</span>');
    document.getElementById("fichaBadges").innerHTML = badges.join("");

    const dados = [
        ["CPF", a.cpfMascarado],
        ["Nascimento", AF().formatarData(a.dataNascimento)],
        ["Contato", AF().formatarContato(a)],
        ["Observação de contato", a.contatoSeguroObservacao || "-"],
        ["Endereço", AF().formatarEndereco(a)],
        ["E-mail", a.email || "-"],
        ["Cadastrada por", `${a.funcionarioAtendimentoNomeSnapshot || "-"}${({ juridico: " (Jurídico)", recepcao: " (Recepção)" })[a.origem] || ""}`],
        ["Encaminhada ao Jurídico", a.encaminhadaJuridicoEm ? AF().formatarData(a.encaminhadaJuridicoEm) : "-"]
    ];
    document.getElementById("fichaDados").innerHTML = dados
        .map(([rotulo, valor]) => `<div><dt>${esc(rotulo)}</dt><dd>${esc(valor)}</dd></div>`)
        .join("");

    document.getElementById("btnCorrigirDados").classList.toggle("hidden", !ehJuridico);
    document.getElementById("btnEncaminharPsicologia").classList.toggle("hidden", !ehJuridico || f.encaminhadaPsicologia);
    document.getElementById("fichaRiscoPsicologico").classList.toggle("hidden", !f.alertaRiscoPsicologico);
    const motivo = document.getElementById("motivoRestricao");
    const btnRestricao = document.getElementById("btnRestricao");
    motivo.value = a.motivoRestricao || "";
    motivo.classList.toggle("hidden", a.acessoRestrito);
    btnRestricao.textContent = a.acessoRestrito ? "Remover acesso restrito" : "Marcar acesso restrito";

    // Responsáveis
    document.getElementById("fichaResponsaveis").innerHTML = f.responsaveis.length
        ? f.responsaveis.map((r) => `
            <span class="juridico-responsavel">${esc(r.nome)} <small>(${esc(r.identificador)})</small>
                ${f.podeAtribuir ? `<button type="button" data-remover="${esc(r.userId)}" title="Remover responsável" aria-label="Remover ${esc(r.nome)}">&times;</button>` : ""}
            </span>`).join("")
        : '<p class="section-intro">Ainda sem responsável. Quem registrar o primeiro atendimento vira responsável automaticamente.</p>';

    const select = document.getElementById("selectProfissional");
    const disponiveis = profissionais.filter((p) => !f.responsaveis.some((r) => r.userId === p.userId));
    select.innerHTML = '<option value="">Escolha uma profissional do Jurídico...</option>'
        + disponiveis.map((p) => `<option value="${esc(p.userId)}">${esc(p.nome)} (${esc(p.identificador)})</option>`).join("");
    document.getElementById("blocoAtribuir").classList.toggle("hidden", !f.podeAtribuir);

    document.getElementById("selectVisibilidade").value = f.visibilidade;
    document.getElementById("blocoVisibilidade").classList.toggle("hidden", !f.podeAlterarVisibilidade);
    document.getElementById("blocoEmergencia").classList.toggle("hidden", !f.podePedirAcessoEmergencia);

    const emergencia = document.getElementById("fichaEmergenciaAtiva");
    const usandoEmergencia = f.acessoEmergenciaExpiraEm && !f.souResponsavel && f.visibilidade === "responsaveis";
    emergencia.classList.toggle("hidden", !usandoEmergencia);
    if (usandoEmergencia) {
        emergencia.textContent = `Você está com acesso de emergência até ${new Date(f.acessoEmergenciaExpiraEm).toLocaleString("pt-BR")}.`;
    }

    document.getElementById("cardNovoAtendimento").classList.toggle("hidden", !f.podeRegistrar);
    renderizarHistorico();
}

function renderizarHistorico() {
    const f = fichaAtual;
    const el = document.getElementById("fichaHistorico");

    if (!f.atendimentos.length) {
        el.innerHTML = '<div class="soft-empty-state">Nenhum atendimento registrado ainda.</div>';
        return;
    }

    el.innerHTML = f.atendimentos.map((at) => {
        const topo = `
            <div class="juridico-atendimento-topo">
                <span class="juridico-protocolo">${esc(at.protocolo)}</span>
                <span class="soft-badge">${esc(ROTULOS_SITUACAO[at.situacao] || at.situacao)}</span>
            </div>
            <div class="entity-info">${esc(new Date(at.criadoEm).toLocaleString("pt-BR"))} · ${esc(at.profissionalNome)} (${esc(at.profissionalIdentificador)})</div>`;

        if (at.conteudoOculto) {
            return `<div class="juridico-atendimento">${topo}
                <p class="section-intro" style="margin-top: 8px;">Conteúdo visível somente para quem cuida do caso.</p></div>`;
        }

        const tipo = at.tipoDemanda === "outro" ? `Outro: ${at.tipoDemandaOutro || ""}` : (ROTULOS_TIPO[at.tipoDemanda] || at.tipoDemanda);
        const medida = at.temMedidaProtetiva === true ? "Sim" : at.temMedidaProtetiva === false ? "Não" : "Não informado";
        const encaminhamentos = at.encaminhamentos
            .map((e) => (e === "outro" ? `Outro: ${at.encaminhamentoOutro || ""}` : ROTULOS_ENCAMINHAMENTO[e] || e))
            .join(", ") || "Nenhum";
        const adendos = at.adendos.map((ad) => `
            <div class="juridico-adendo"><strong>Adendo de ${esc(ad.autorNome)} (${esc(ad.autorIdentificador)}) em ${esc(new Date(ad.criadoEm).toLocaleString("pt-BR"))}:</strong>
${esc(ad.texto)}</div>`).join("");

        return `<div class="juridico-atendimento">${topo}
            <dl class="juridico-dados" style="margin-top: 12px;">
                <div><dt>Tipo de demanda</dt><dd>${esc(tipo)}</dd></div>
                <div><dt>Medida protetiva</dt><dd>${esc(medida)}</dd></div>
                <div><dt>Processo</dt><dd>${esc([at.numeroProcesso, at.vara].filter(Boolean).join(" · ") || "-")}</dd></div>
                <div><dt>Encaminhamentos</dt><dd>${esc(encaminhamentos)}</dd></div>
                <div><dt>Próximo passo</dt><dd>${esc(at.proximoPasso || "-")}</dd></div>
                <div><dt>Retorno</dt><dd>${esc(AF().formatarData(at.dataRetorno))}</dd></div>
            </dl>
            <div class="juridico-relato">${esc(at.relato)}</div>
            ${adendos}
            ${f.podeRegistrar ? `
                <textarea class="soft-input" id="adendoTexto${at.id}" maxlength="2000" placeholder="Correção ou complemento (adendo)" style="min-height: 50px; margin-top: 8px;"></textarea>
                <div class="juridico-linha-acoes" style="margin-top: 8px;">
                    <button type="button" class="soft-btn soft-btn-warning" data-adendo="${at.id}">Adicionar adendo</button>
                </div>` : ""}
        </div>`;
    }).join("");
}

function atualizarContador() {
    const total = document.getElementById("relato").value.length;
    const contador = document.getElementById("relatoContador");
    contador.textContent = `${total} / 5000 caracteres`;
    contador.classList.toggle("limite", total >= 4500);
}

async function registrarAtendimento(e) {
    e.preventDefault();
    if (enviando || !fichaAtual) return;

    const dados = {
        tipoDemanda: document.getElementById("tipoDemanda").value,
        tipoDemandaOutro: document.getElementById("tipoDemandaOutro").value.trim() || null,
        temMedidaProtetiva: document.getElementById("temMedidaProtetiva").value === "" ? null : document.getElementById("temMedidaProtetiva").value === "true",
        numeroProcesso: document.getElementById("numeroProcesso").value.trim() || null,
        vara: document.getElementById("vara").value.trim() || null,
        encaminhamentos: Array.from(document.querySelectorAll('input[name="encaminhamento"]:checked')).map((c) => c.value),
        encaminhamentoOutro: document.getElementById("encaminhamentoOutro").value.trim() || null,
        relato: document.getElementById("relato").value.trim(),
        proximoPasso: document.getElementById("proximoPasso").value.trim() || null,
        dataRetorno: document.getElementById("dataRetorno").value || null,
        situacao: document.getElementById("situacao").value
    };

    if (!dados.tipoDemanda) {
        mostrarMensagem("Escolha o tipo de demanda.", "error");
        return;
    }
    if (dados.relato.length < 15) {
        mostrarMensagem("Descreva o relato com mais detalhes (mínimo de 15 caracteres).", "error");
        return;
    }

    enviando = true;
    document.getElementById("btnSalvarAtendimento").disabled = true;
    const response = await api(`/api/juridico/casos/${fichaAtual.assistida.id}/atendimentos`, { method: "POST", body: dados });
    enviando = false;
    document.getElementById("btnSalvarAtendimento").disabled = false;

    if (!response.ok) {
        // O texto continua no formulário para não ser perdido.
        mostrarMensagem(await erroDe(response, "Não foi possível registrar o atendimento."), "error");
        return;
    }

    ultimoComprovante = await response.json();
    document.getElementById("atendimentoForm").reset();
    document.getElementById("tipoDemandaOutro").classList.add("hidden");
    document.getElementById("encaminhamentoOutro").classList.add("hidden");
    atualizarContador();
    mostrarComprovante(ultimoComprovante);
    mostrarMensagem(`Atendimento ${ultimoComprovante.protocolo} registrado.`, "success");
    await abrirFicha(fichaAtual.assistida.id);
}

function mostrarComprovante(c) {
    document.getElementById("comprovanteProtocolo").textContent = c.protocolo;
    document.getElementById("comprovanteAviso").textContent = c.avisoSnapshot || "";
    document.getElementById("impProtocolo").textContent = c.protocolo;
    document.getElementById("impData").textContent = new Date(c.criadoEm).toLocaleString("pt-BR");
    document.getElementById("impNome").textContent = c.assistidaNome;
    document.getElementById("impProfissional").textContent = c.profissionalNome;
    document.getElementById("impRetorno").textContent = c.dataRetorno ? AF().formatarData(c.dataRetorno) : "A combinar";
    document.getElementById("modalComprovante").classList.remove("hidden");
}

function fecharComprovante() {
    document.getElementById("modalComprovante").classList.add("hidden");
}

async function registrarAdendo(atendimentoId) {
    const campo = document.getElementById(`adendoTexto${atendimentoId}`);
    const texto = campo?.value.trim() || "";
    if (texto.length < 3) {
        mostrarMensagem("Escreva o adendo antes de salvar.", "error");
        return;
    }

    const response = await api(`/api/juridico/atendimentos/${atendimentoId}/adendos`, { method: "POST", body: { texto } });
    if (!response.ok) {
        mostrarMensagem(await erroDe(response, "Não foi possível salvar o adendo."), "error");
        return;
    }

    mostrarMensagem("Adendo registrado.", "success");
    await abrirFicha(fichaAtual.assistida.id);
}

async function alternarRestricao() {
    const a = fichaAtual.assistida;
    const restringir = !a.acessoRestrito;
    const motivo = document.getElementById("motivoRestricao").value;

    if (restringir && !motivo) {
        mostrarMensagem("Escolha o motivo do acesso restrito.", "error");
        return;
    }

    const pergunta = restringir
        ? "A assistida deixará de aparecer para Recepção, Professor e outros perfis. Confirmar?"
        : "A assistida voltará a aparecer para os outros perfis (só dados básicos). Confirmar?";
    if (!confirm(pergunta)) return;

    const response = await api(`/api/assistidas/${a.id}/restricao`, {
        method: "PUT",
        body: { acessoRestrito: restringir, motivo: restringir ? motivo : null }
    });

    if (!response.ok) {
        mostrarMensagem(await erroDe(response, "Não foi possível alterar o acesso restrito."), "error");
        return;
    }

    mostrarMensagem(restringir ? "Acesso restrito ativado." : "Acesso restrito removido.", "success");
    await abrirFicha(a.id);
}

async function atribuirResponsavel() {
    const userId = document.getElementById("selectProfissional").value;
    if (!userId) {
        mostrarMensagem("Escolha a profissional.", "error");
        return;
    }

    const response = await api(`/api/juridico/casos/${fichaAtual.assistida.id}/responsaveis`, { method: "POST", body: { userId } });
    if (!response.ok) {
        mostrarMensagem(await erroDe(response, "Não foi possível atribuir."), "error");
        return;
    }

    mostrarMensagem("Responsável atribuída.", "success");
    await abrirFicha(fichaAtual.assistida.id);
}

async function removerResponsavel(userId) {
    if (!confirm("Remover esta responsável do caso?")) return;

    const response = await api(`/api/juridico/casos/${fichaAtual.assistida.id}/responsaveis/${encodeURIComponent(userId)}`, { method: "DELETE" });
    if (!response.ok) {
        mostrarMensagem(await erroDe(response, "Não foi possível remover."), "error");
        return;
    }

    mostrarMensagem("Responsável removida.", "success");
    await abrirFicha(fichaAtual.assistida.id);
}

async function salvarVisibilidade() {
    const visibilidade = document.getElementById("selectVisibilidade").value;
    const response = await api(`/api/juridico/casos/${fichaAtual.assistida.id}/visibilidade`, { method: "PUT", body: { visibilidade } });
    if (!response.ok) {
        mostrarMensagem(await erroDe(response, "Não foi possível salvar a visibilidade."), "error");
        return;
    }

    mostrarMensagem(visibilidade === "responsaveis" ? "Agora só as responsáveis veem o conteúdo." : "Toda a equipe do Jurídico vê o conteúdo.", "success");
    await abrirFicha(fichaAtual.assistida.id);
}

async function pedirEmergencia() {
    const motivo = document.getElementById("motivoEmergencia").value.trim();
    if (motivo.length < 10) {
        mostrarMensagem("Explique o motivo (pelo menos 10 caracteres).", "error");
        return;
    }

    const response = await api(`/api/juridico/casos/${fichaAtual.assistida.id}/acesso-emergencia`, { method: "POST", body: { motivo } });
    if (!response.ok) {
        mostrarMensagem(await erroDe(response, "Não foi possível liberar o acesso."), "error");
        return;
    }

    document.getElementById("motivoEmergencia").value = "";
    mostrarMensagem("Acesso de emergência liberado por 12 horas. As responsáveis foram avisadas.", "success");
    await abrirFicha(fichaAtual.assistida.id);
}

async function encaminharPsicologia() {
    if (!fichaAtual) return;
    if (!confirm("Encaminhar esta assistida para a fila da Psicologia? Nada do atendimento jurídico é compartilhado.")) return;

    const response = await api(`/api/assistidas/${fichaAtual.assistida.id}/encaminhar`, { method: "POST", body: { area: "psicologia" } });
    if (!response.ok) {
        mostrarMensagem(await erroDe(response, "Não foi possível encaminhar."), "error");
        return;
    }

    mostrarMensagem("Assistida encaminhada à Psicologia.", "success");
    await abrirFicha(fichaAtual.assistida.id);
}
