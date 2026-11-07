/*
 * Tela do Serviço Social: fila de encaminhadas, assistidas, cadastro direto, prontuário social
 * (número do servidor), atendimentos numerados com adendos, estudo social com histórico e aviso
 * de vulnerabilidade, encaminhamentos à rede, plano de acompanhamento, documentos anexados
 * (criptografados no servidor), responsáveis, sigilo, acesso de emergência, encerramento e
 * declaração de comparecimento com CRESS. O relato nunca é guardado no navegador.
 * A coordenação vê só os indicadores.
 */
document.addEventListener("DOMContentLoaded", setupServicoSocial);

const ROTULOS_TIPO = { acolhimento: "Acolhimento", individual: "Atendimento individual", visita_domiciliar: "Visita domiciliar", contato_telefonico: "Contato telefônico", articulacao_rede: "Articulação com a rede", outro: "Outro" };
const ROTULOS_DESTINO = { cras: "CRAS", creas: "CREAS", defensoria: "Defensoria Pública", conselho_tutelar: "Conselho Tutelar", ubs_saude: "UBS / Saúde", caps: "CAPS", casa_abrigo: "Casa-abrigo", inss: "INSS", ddm_delegacia: "DDM / Delegacia", outro: "Outro" };
const ROTULOS_STATUS_REDE = { enviado: "Enviado", retorno_recebido: "Retorno recebido", sem_retorno: "Sem retorno", cancelado: "Cancelado" };
const ROTULOS_META = { pendente: "Pendente", em_andamento: "Em andamento", concluida: "Concluída" };
const ROTULOS_VULNERABILIDADE = { inseguranca_alimentar: "Insegurança alimentar", sem_renda: "Sem renda", sem_moradia_segura: "Sem moradia segura", dependencia_economica_agressor: "Dependência econômica do agressor" };
const ROTULOS_BENEFICIO = { bolsa_familia: "Bolsa Família", bpc: "BPC", aluguel_social: "Aluguel social", outro: "Outros" };
const ROTULOS_RENDA = { sem_renda: "Sem renda", ate_meio_sm: "Até ½ salário mínimo", meio_a_1_sm: "De ½ a 1 salário mínimo", "1_a_2_sm": "De 1 a 2 salários mínimos", "2_a_3_sm": "De 2 a 3 salários mínimos", acima_3_sm: "Acima de 3 salários mínimos", nao_informada: "Não informada" };
const ROTULOS_TRABALHO = { formal: "Trabalho formal", informal: "Trabalho informal", autonoma: "Autônoma", desempregada: "Desempregada", do_lar: "Do lar", aposentada_pensionista: "Aposentada / pensionista", estudante: "Estudante", outro: "Outro" };
const ROTULOS_MORADIA = { propria: "Própria", alugada: "Alugada", cedida: "Cedida", abrigo: "Abrigo", situacao_rua: "Situação de rua", outro: "Outro" };
const ROTULOS_ESCOLARIDADE = { sem_escolaridade: "Sem escolaridade", fundamental_incompleto: "Fundamental incompleto", fundamental_completo: "Fundamental completo", medio_incompleto: "Médio incompleto", medio_completo: "Médio completo", superior_incompleto: "Superior incompleto", superior_completo: "Superior completo" };
const ROTULOS_CADUNICO = { sim: "Sim", nao: "Não", nao_sabe: "Não sabe" };
const ROTULOS_PARENTESCO = { filho: "Filho", filha: "Filha", enteado: "Enteado(a)", neto: "Neto(a)", outro: "Outro" };
const ROTULOS_ENCERRAMENTO = { objetivos_alcancados: "Objetivos alcançados", desistencia: "Interrompido pela assistida", encaminhamento_externo: "Encaminhamento externo", mudanca_cidade: "Mudança de cidade", outro: "Outro" };
const ROTULOS_ANEXO = { rg: "RG", cpf: "CPF", certidao: "Certidão", comprovante_residencia: "Comprovante de residência", comprovante_renda: "Comprovante de renda", nis_cadunico: "NIS / CadÚnico", boletim_ocorrencia: "Boletim de ocorrência", medida_protetiva: "Medida protetiva", laudo_relatorio: "Laudo / relatório", outro: "Outro" };
const TAMANHO_MAXIMO = 5 * 1024 * 1024;

let socUsuario = null;
let fichaAtual = null;
let profissionais = [];
let ultimoAtendimento = null;
let enviando = false;
let previaUrl = null;

const AF = () => window.CasaMulherAssistidaForm;
const esc = (v) => AF().escapeHtml(v);
const $ = (id) => document.getElementById(id);

async function setupServicoSocial() {
    socUsuario = await CasaMulherAuth.protegerPagina();
    if (!socUsuario) return;

    if (!CasaMulherAuth.podeAcessar("social")) {
        alert("Você não tem permissão para acessar o Serviço Social.");
        window.location.href = CasaMulherAuth.getPainelUrl(socUsuario);
        return;
    }

    inicializarSoftSessionCard(socUsuario);
    $("sessionUserCress").textContent = socUsuario.cress || "não informado";
    bindEventos();
    iniciarAvisoSessao();

    // A coordenação acompanha o Serviço Social apenas pelos números.
    if (socUsuario.perfil === "adm") {
        document.querySelectorAll("[data-somente-social]").forEach((b) => b.classList.add("hidden"));
        mostrarAba("indicadores");
        return;
    }

    $("avisoCress").classList.toggle("hidden", Boolean(socUsuario.cress));
    AF().init($("cadastroForm"), mostrarMensagem);
    await Promise.all([carregarFila(), carregarProfissionais()]);
}

function bindEventos() {
    document.querySelectorAll("[data-tab]").forEach((b) => b.addEventListener("click", () => mostrarAba(b.dataset.tab)));
    document.querySelectorAll("[data-sec]").forEach((b) => b.addEventListener("click", () => mostrarSecao(b.dataset.sec)));

    $("btnBuscarAssistida").addEventListener("click", carregarAssistidas);
    $("escopoAssistidas").addEventListener("change", carregarAssistidas);
    $("buscaAssistida").addEventListener("keyup", (e) => { if (e.key === "Enter") carregarAssistidas(); });
    ["listaFila", "listaAssistidas"].forEach((id) => $(id).addEventListener("click", (e) => {
        const botao = e.target.closest("[data-abrir]");
        if (botao) abrirFicha(Number(botao.dataset.abrir));
    }));

    $("btnSalvarCress").addEventListener("click", salvarCress);
    $("cadastroForm").addEventListener("submit", salvarCadastro);
    $("btnCancelarCadastro").addEventListener("click", cancelarCadastro);

    $("btnAbrirProntuario").addEventListener("click", abrirProntuario);
    $("btnCorrigirDados").addEventListener("click", corrigirDados);
    $("btnEncaminharJuridico").addEventListener("click", () => encaminhar("juridico", "Jurídico"));
    $("btnEncaminharPsicologia").addEventListener("click", () => encaminhar("psicologia", "Psicologia"));
    $("btnRestricao").addEventListener("click", alternarRestricao);
    $("btnAtribuir").addEventListener("click", atribuirResponsavel);
    $("btnVisibilidade").addEventListener("click", salvarVisibilidade);
    $("btnEmergencia").addEventListener("click", pedirEmergencia);
    $("btnEncerrar").addEventListener("click", encerrarAcompanhamento);
    $("fichaResponsaveis").addEventListener("click", (e) => {
        const botao = e.target.closest("[data-remover]");
        if (botao) removerResponsavel(botao.dataset.remover);
    });

    $("atendimentoForm").addEventListener("submit", registrarAtendimento);
    $("tipoAtendimento").addEventListener("change", () => $("tipoOutro").classList.toggle("hidden", $("tipoAtendimento").value !== "outro"));
    $("relato").addEventListener("input", atualizarContador);
    $("fichaHistorico").addEventListener("click", (e) => {
        const adendo = e.target.closest("[data-adendo]");
        if (adendo) registrarAdendo(Number(adendo.dataset.adendo));
        const declaracao = e.target.closest("[data-declaracao]");
        if (declaracao) imprimirDeclaracao(Number(declaracao.dataset.declaracao));
        const anexar = e.target.closest("[data-anexar-atendimento]");
        if (anexar) irParaAnexo(Number(anexar.dataset.anexarAtendimento));
    });

    $("btnEditarEstudo").addEventListener("click", () => abrirFormEstudo(true));
    $("btnCancelarEstudo").addEventListener("click", () => abrirFormEstudo(false));
    $("btnAddDependente").addEventListener("click", () => adicionarDependente());
    $("listaDependentes").addEventListener("click", (e) => { if (e.target.closest("[data-remover-dependente]")) e.target.closest(".social-dependente").remove(); });
    $("beneficioOutroCheck").addEventListener("change", (e) => $("beneficioOutro").classList.toggle("hidden", !e.target.checked));
    $("estudoForm").addEventListener("submit", salvarEstudo);

    $("destinoRede").addEventListener("change", () => $("destinoOutro").classList.toggle("hidden", $("destinoRede").value !== "outro"));
    $("redeForm").addEventListener("submit", registrarRede);
    $("listaRede").addEventListener("click", (e) => {
        const botao = e.target.closest("[data-status-rede]");
        if (botao) atualizarRede(Number(botao.dataset.statusRede));
    });

    $("metaForm").addEventListener("submit", registrarMeta);
    $("listaMetas").addEventListener("change", (e) => {
        const select = e.target.closest("[data-meta]");
        if (select) atualizarMeta(Number(select.dataset.meta), select.value);
    });

    $("arquivoAnexo").addEventListener("change", () => {
        const arquivo = $("arquivoAnexo").files[0];
        $("nomeArquivoEscolhido").textContent = arquivo ? `${arquivo.name} (${formatarTamanho(arquivo.size)})` : "Nenhum arquivo escolhido";
    });
    $("anexoForm").addEventListener("submit", enviarAnexo);
    $("listaAnexos").addEventListener("click", (e) => {
        const ver = e.target.closest("[data-ver-anexo]");
        if (ver) verAnexo(Number(ver.dataset.verAnexo));
        const baixar = e.target.closest("[data-baixar-anexo]");
        if (baixar) baixarAnexo(Number(baixar.dataset.baixarAnexo));
        const excluir = e.target.closest("[data-excluir-anexo]");
        if (excluir) excluirAnexo(Number(excluir.dataset.excluirAnexo));
    });
    $("btnFecharPrevia").addEventListener("click", fecharPrevia);

    $("btnDeclaracaoModal").addEventListener("click", () => { if (ultimoAtendimento) imprimirDeclaracao(ultimoAtendimento.atendimentoId); });
    $("btnAnexarModal").addEventListener("click", () => { fecharModalAtendimento(); if (ultimoAtendimento) irParaAnexo(ultimoAtendimento.atendimentoId); });
    $("btnFecharModalAtendimento").addEventListener("click", fecharModalAtendimento);
    $("btnOkModalAtendimento").addEventListener("click", fecharModalAtendimento);
}

function mostrarAba(aba) {
    document.querySelectorAll("[data-tab]").forEach((b) => b.classList.toggle("active", b.dataset.tab === aba));
    document.querySelectorAll("[data-panel]").forEach((p) => p.classList.toggle("active", p.dataset.panel === aba));
    if (aba === "fila") carregarFila();
    if (aba === "assistidas") carregarAssistidas();
    if (aba === "indicadores") carregarIndicadores();
}

function mostrarSecao(secao) {
    document.querySelectorAll("[data-sec]").forEach((b) => b.classList.toggle("active", b.dataset.sec === secao));
    document.querySelectorAll("[data-secao]").forEach((s) => s.classList.toggle("active", s.dataset.secao === secao));
}

function mostrarMensagem(texto, tipo) {
    const el = $("mensagemSocial");
    el.textContent = texto || "";
    el.className = `message ${tipo || ""}`.trim();
}

async function api(url, opcoes) {
    return CasaMulherAuth.apiFetch(url, Object.assign({ mensagemElement: $("mensagemSocial") }, opcoes || {}));
}

async function erroDe(response, padrao) {
    return AF().lerMensagemErro(response, padrao);
}

function formatarDataHora(valor) {
    if (!valor) return "-";
    const data = new Date(valor);
    return Number.isNaN(data.getTime()) ? valor : data.toLocaleString("pt-BR", { dateStyle: "short", timeStyle: "short" });
}

function formatarDia(iso) {
    if (!iso) return "-";
    const [a, m, d] = String(iso).split("-");
    return d ? `${d}/${m}/${a}` : iso;
}

function formatarTamanho(bytes) {
    return bytes >= 1024 * 1024 ? `${(bytes / 1024 / 1024).toFixed(1).replace(".", ",")} MB` : `${Math.max(1, Math.round(bytes / 1024))} KB`;
}

/* ---------------- Sessão: avisa antes de expirar para não perder o relato ---------------- */
function iniciarAvisoSessao() {
    const atualizar = () => {
        const expira = new Date(CasaMulherAuth.getUsuario().expiraEm || 0).getTime();
        const aviso = $("avisoSessao");
        if (!Number.isFinite(expira) || expira === 0) return;
        const restante = expira - Date.now();
        if (restante > 0 && restante <= 5 * 60 * 1000) {
            aviso.textContent = `Sua sessão termina em ${Math.max(1, Math.ceil(restante / 60000))} minuto(s). Salve o atendimento antes disso: o texto digitado não fica guardado no navegador.`;
            aviso.classList.remove("hidden");
        } else {
            aviso.classList.add("hidden");
        }
    };
    atualizar();
    setInterval(atualizar, 20000);
}

async function salvarCress() {
    const cress = $("inputCress").value.trim();
    if (cress.length < 4) return mostrarMensagem("Informe o CRESS com região e número.", "error");
    const response = await api("/api/servico-social/meu-cress", { method: "PUT", body: { cress } });
    if (!response.ok) return mostrarMensagem(await erroDe(response, "Não foi possível salvar o CRESS."), "error");
    socUsuario.cress = cress;
    $("sessionUserCress").textContent = cress;
    $("avisoCress").classList.add("hidden");
    mostrarMensagem("CRESS salvo. Ele sai nas próximas declarações e documentos.", "success");
}

/* ---------------- Listas ---------------- */
function cardResumo(a) {
    const badges = [];
    if (a.vulnerabilidade) badges.push('<span class="soft-badge soft-badge--risco">Aviso de vulnerabilidade</span>');
    if (a.acessoRestrito) badges.push('<span class="soft-badge soft-badge--restrito">Acesso restrito</span>');
    if (a.origem === "servico_social") badges.push('<span class="soft-badge soft-badge--social">Cadastrada pelo Serviço Social</span>');
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
            ${a.encaminhadaServicoSocialEm ? `<div class="entity-info">Encaminhada em: ${esc(AF().formatarData(a.encaminhadaServicoSocialEm))}</div>` : ""}
            <div class="entity-info">Com: ${responsaveis}</div>
            <div class="entity-info">Atendimentos: ${a.totalAtendimentos}${a.ultimoAtendimentoEm ? ` · Último ${esc(formatarDataHora(a.ultimoAtendimentoEm))}` : ""}</div>
            <div class="entity-info" style="display:flex; gap:6px; flex-wrap:wrap; margin-top:6px;">${badges.join("")}</div>
            <div class="entity-actions"><button type="button" class="soft-btn soft-btn-primary" data-abrir="${a.assistidaId}">Abrir prontuário</button></div>
        </div>`;
}

function renderizarLista(id, lista, vazio) {
    $(id).innerHTML = lista.length ? lista.map(cardResumo).join("") : `<div class="soft-empty-state">${esc(vazio)}</div>`;
}

async function carregarFila() {
    const response = await api("/api/servico-social/assistidas?escopo=fila");
    if (!response.ok) return;
    renderizarLista("listaFila", await response.json(), "Nenhuma assistida aguardando o primeiro atendimento.");
}

async function carregarAssistidas() {
    const busca = $("buscaAssistida").value.trim();
    const params = new URLSearchParams({ escopo: $("escopoAssistidas").value });
    if (busca) params.set("busca", busca);
    const response = await api(`/api/servico-social/assistidas?${params}`);
    if (!response.ok) return;
    renderizarLista("listaAssistidas", await response.json(),
        busca ? "Ninguém encontrado. Se ela veio direto ao Serviço Social, use a aba Cadastrar assistida." : "Nenhum prontuário nesta lista.");
}

async function carregarProfissionais() {
    const response = await api("/api/servico-social/profissionais");
    profissionais = response.ok ? await response.json() : [];
}

async function carregarIndicadores() {
    const response = await api("/api/servico-social/indicadores");
    if (!response.ok) return;
    const ind = await response.json();
    $("indFila").textContent = ind.fila;
    $("indAcompanhamento").textContent = ind.emAcompanhamento;
    $("indAtendimentos").textContent = ind.atendimentosNoMes;
    $("indRede").textContent = ind.encaminhamentosRedeNoMes;
    $("indSemRetorno").textContent = ind.encaminhamentosSemRetorno;
    $("indVulnerabilidade").textContent = ind.comVulnerabilidade;
    const tipos = Object.entries(ind.atendimentosPorTipoNoMes || {});
    $("indTipos").innerHTML = tipos.length
        ? '<h3 class="soft-fieldset-title">Atendimentos do mês por tipo</h3>' + tipos.map(([k, v]) => `<span class="soft-badge" style="margin: 0 6px 6px 0;">${esc(ROTULOS_TIPO[k] || k)}: ${v}</span>`).join("")
        : "";
}

/* ---------------- Cadastro direto pelo Serviço Social ---------------- */
function cancelarCadastro() {
    AF().limpar($("cadastroForm"));
    $("cadastroAssistidaId").value = "";
    $("cadastroTitulo").textContent = "Cadastrar assistida";
    $("cadastroSubmitLabel").textContent = "Cadastrar e abrir ficha";
    $("cadastroAviso").classList.remove("hidden");
    mostrarAba(fichaAtual ? "ficha" : "fila");
}

async function salvarCadastro(e) {
    e.preventDefault();
    if (enviando) return;
    const form = e.currentTarget;
    const id = $("cadastroAssistidaId").value;
    const dados = AF().ler(form);
    const erro = AF().validar(dados);
    if (erro) return mostrarMensagem(erro, "error");

    if (id && fichaAtual) {
        // Correção: mantém o que as outras áreas preencheram e esta tela não mostra.
        dados.observacoes = fichaAtual.assistida.observacoes;
        dados.encaminharJuridico = fichaAtual.assistida.encaminhadaJuridico;
        dados.encaminharAssistenciaSocial = true;
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

    if (!response.ok) return mostrarMensagem(await erroDe(response, "Não foi possível salvar o cadastro."), "error");

    const salvo = await response.json();
    AF().limpar(form);
    $("cadastroAssistidaId").value = "";
    mostrarMensagem((id ? "Dados corrigidos." : "Assistida cadastrada.") + (salvo.avisoSnapshot ? ` ${salvo.avisoSnapshot}` : ""), salvo.avisoSnapshot ? "info" : "success");
    abrirFicha(salvo.id);
}

function corrigirDados() {
    if (!fichaAtual) return;
    AF().preencher($("cadastroForm"), fichaAtual.assistida);
    $("cadastroAssistidaId").value = fichaAtual.assistida.id;
    $("cadastroTitulo").textContent = `Corrigir dados de ${fichaAtual.assistida.nome}`;
    $("cadastroSubmitLabel").textContent = "Salvar correção";
    $("cadastroAviso").classList.add("hidden");
    mostrarAba("cadastro");
}

/* ---------------- Ficha / prontuário ---------------- */
async function abrirFicha(assistidaId, secao) {
    const response = await api(`/api/servico-social/prontuarios/${assistidaId}`);
    if (!response.ok) return mostrarMensagem(await erroDe(response, "Não foi possível abrir o prontuário."), "error");

    fichaAtual = await response.json();
    const tab = $("tabFicha");
    tab.classList.remove("hidden");
    tab.textContent = `Ficha: ${fichaAtual.assistida.nome.split(" ")[0]}`;
    renderizarFicha();
    mostrarAba("ficha");
    if (secao) mostrarSecao(secao);
    else window.scrollTo({ top: 0, behavior: "smooth" });
}

function renderizarFicha() {
    const f = fichaAtual;
    const a = f.assistida;
    const temProntuario = Boolean(f.prontuarioId);

    $("fichaNome").textContent = a.nome;
    $("fichaRestrito").classList.toggle("hidden", !a.acessoRestrito);
    const vuln = $("fichaVulnerabilidade");
    vuln.classList.toggle("hidden", !f.vulnerabilidades.length);
    vuln.innerHTML = f.vulnerabilidades.length
        ? `<strong>Aviso de vulnerabilidade (estudo social).</strong><ul>${f.vulnerabilidades.map((v) => `<li>${esc(ROTULOS_VULNERABILIDADE[v] || v)}</li>`).join("")}</ul>
           <div style="margin-top: 6px;">Avalie benefícios, rede de apoio e encaminhamentos (CRAS, CREAS, Defensoria, Casa-abrigo). Jurídico e Psicologia veem só que há um aviso, sem detalhes.</div>`
        : "";

    const badges = [];
    if (f.numeroProntuario) badges.push(`<span class="soft-badge soft-badge--social">Prontuário ${esc(f.numeroProntuario)}</span>`);
    if (f.situacao === "encerrado" && f.totalAtendimentos) badges.push(`<span class="soft-badge soft-badge--ok">Encerrado · ${esc(ROTULOS_ENCERRAMENTO[f.motivoEncerramento] || "")}</span>`);
    if (a.acessoRestrito) badges.push(`<span class="soft-badge soft-badge--restrito">Acesso restrito · ${esc(AF().ROTULOS_MOTIVO_RESTRICAO[a.motivoRestricao] || "")}</span>`);
    if (f.emAtendimentoJuridico) badges.push('<span class="soft-badge soft-badge--juridico">Em atendimento no Jurídico</span>');
    else if (a.encaminhadaJuridico) badges.push('<span class="soft-badge soft-badge--juridico">Encaminhada ao Jurídico</span>');
    if (f.emAcompanhamentoPsicologia) badges.push('<span class="soft-badge soft-badge--psicologia">Em acompanhamento na Psicologia</span>');
    $("fichaBadges").innerHTML = badges.join("");

    const origem = { recepcao: "Recepção", juridico: "Jurídico", servico_social: "Serviço Social", equipe_tecnica: "Equipe técnica" }[a.origem] || a.origem;
    const dados = [
        ["CPF", a.cpfMascarado],
        ["Nascimento", AF().formatarData(a.dataNascimento)],
        ["Contato", AF().formatarContato(a)],
        ["Observação de contato", a.contatoSeguroObservacao || "-"],
        ["Endereço", AF().formatarEndereco(a)],
        ["Cadastrada por", `${a.funcionarioAtendimentoNomeSnapshot || "-"} (${origem})`]
    ];
    $("fichaDados").innerHTML = dados.map(([r, v]) => `<div><dt>${esc(r)}</dt><dd>${esc(v)}</dd></div>`).join("");

    $("btnAbrirProntuario").classList.toggle("hidden", temProntuario);
    $("btnEncaminharJuridico").classList.toggle("hidden", a.encaminhadaJuridico);
    $("btnEncaminharPsicologia").classList.toggle("hidden", a.encaminhadaPsicologia || f.emAcompanhamentoPsicologia);
    const motivo = $("motivoRestricao");
    motivo.value = a.motivoRestricao || "";
    motivo.classList.toggle("hidden", a.acessoRestrito);
    $("btnRestricao").textContent = a.acessoRestrito ? "Remover acesso restrito" : "Marcar acesso restrito";

    $("fichaResponsaveis").innerHTML = f.responsaveis.length
        ? f.responsaveis.map((r) => `
            <span class="juridico-responsavel">${esc(r.nome)} <small>(${esc(r.identificador)}${r.cress ? " · " + esc(r.cress) : ""})</small>
                ${f.podeAtribuir ? `<button type="button" data-remover="${esc(r.userId)}" title="Remover responsável" aria-label="Remover ${esc(r.nome)}">&times;</button>` : ""}
            </span>`).join("")
        : '<p class="section-intro">Ainda sem responsável. Quem abrir o prontuário ou registrar o primeiro atendimento vira responsável automaticamente.</p>';

    const disponiveis = profissionais.filter((p) => !f.responsaveis.some((r) => r.userId === p.userId));
    $("selectProfissional").innerHTML = '<option value="">Escolha uma assistente social...</option>'
        + disponiveis.map((p) => `<option value="${esc(p.userId)}">${esc(p.nome)} (${esc(p.identificador)})</option>`).join("");
    $("blocoAtribuir").classList.toggle("hidden", !f.podeAtribuir || !temProntuario);
    $("selectVisibilidade").value = f.visibilidade;
    $("blocoVisibilidade").classList.toggle("hidden", !f.podeAlterarVisibilidade);
    $("blocoEmergencia").classList.toggle("hidden", !f.podePedirAcessoEmergencia);

    const emergencia = $("fichaEmergenciaAtiva");
    const usandoEmergencia = f.acessoEmergenciaExpiraEm && !f.souResponsavel && f.visibilidade === "responsaveis";
    emergencia.classList.toggle("hidden", !usandoEmergencia);
    if (usandoEmergencia) emergencia.textContent = `Você está com acesso de emergência até ${new Date(f.acessoEmergenciaExpiraEm).toLocaleString("pt-BR")}.`;

    // Sem acesso ao conteúdo, estudo, rede, plano e documentos nem aparecem.
    document.querySelectorAll("[data-sec]").forEach((b) => b.classList.toggle("hidden", b.dataset.sec !== "atendimentos" && (!f.podeVerConteudo || !temProntuario)));
    if (!f.podeVerConteudo || !temProntuario) mostrarSecao("atendimentos");

    $("cardNovoAtendimento").classList.toggle("hidden", !f.podeRegistrar);
    $("cardEncerrar").classList.toggle("hidden", !f.podeRegistrar || !f.totalAtendimentos || f.situacao === "encerrado");
    renderizarHistorico();
    if (f.podeVerConteudo && temProntuario) {
        renderizarEstudo();
        renderizarRede();
        renderizarPlano();
        renderizarAnexos();
    }
}

function renderizarHistorico() {
    const f = fichaAtual;
    const el = $("fichaHistorico");
    if (!f.atendimentos.length) {
        el.innerHTML = '<div class="soft-empty-state">Nenhum atendimento registrado ainda.</div>';
        return;
    }

    el.innerHTML = f.atendimentos.map((s) => {
        const topo = `
            <div class="juridico-atendimento-topo">
                <span class="juridico-protocolo">Atendimento ${s.numero}</span>
                ${s.tipo ? `<span class="soft-badge soft-badge--social">${esc(s.tipo === "outro" && s.tipoOutro ? "Outro: " + s.tipoOutro : ROTULOS_TIPO[s.tipo] || s.tipo)}</span>` : ""}
            </div>
            <div class="entity-info">${esc(formatarDataHora(s.realizadoEm))} · ${esc(s.profissionalNome)} (${esc(s.profissionalIdentificador)}${s.profissionalCress ? " · " + esc(s.profissionalCress) : ""})</div>`;

        if (s.conteudoOculto) {
            return `<div class="juridico-atendimento">${topo}<p class="section-intro" style="margin-top: 8px;">Conteúdo visível somente para quem cuida do caso.</p></div>`;
        }

        const adendos = s.adendos.map((ad) => `
            <div class="juridico-adendo"><strong>Adendo de ${esc(ad.autorNome)} (${esc(ad.autorIdentificador)}) em ${esc(formatarDataHora(ad.criadoEm))}:</strong>
${esc(ad.texto)}</div>`).join("");

        return `<div class="juridico-atendimento">${topo}
            <div class="juridico-relato">${esc(s.relato)}</div>
            ${s.providencias ? `<div class="entity-info"><strong>Providências:</strong> ${esc(s.providencias)}</div>` : ""}
            ${s.totalAnexos ? `<div class="entity-info">${s.totalAnexos} documento(s) anexado(s) a este atendimento</div>` : ""}
            ${adendos}
            ${fichaAtual.podeRegistrar ? `
                <textarea class="soft-input" id="adendoTexto${s.id}" maxlength="2000" placeholder="Correção ou complemento (adendo)" style="min-height: 50px; margin-top: 8px;"></textarea>
                <div class="juridico-linha-acoes" style="margin-top: 8px;">
                    <button type="button" class="soft-btn soft-btn-warning" data-adendo="${s.id}">Adicionar adendo</button>
                    ${s.podeDeclaracao ? `<button type="button" class="soft-btn soft-btn-warning" data-declaracao="${s.id}">Declaração de comparecimento</button>` : ""}
                    <button type="button" class="soft-btn soft-btn-warning" data-anexar-atendimento="${s.id}">Anexar documento</button>
                </div>` : ""}
        </div>`;
    }).join("");
}

function atualizarContador() {
    const total = $("relato").value.length;
    $("relatoContador").textContent = `${total} / 5000 caracteres`;
    $("relatoContador").classList.toggle("limite", total >= 4500);
}

async function abrirProntuario() {
    const response = await api(`/api/servico-social/prontuarios/${fichaAtual.assistida.id}/abrir`, { method: "POST" });
    if (!response.ok) return mostrarMensagem(await erroDe(response, "Não foi possível abrir o prontuário."), "error");
    const r = await response.json();
    mostrarMensagem(r.mensagem, "success");
    await abrirFicha(fichaAtual.assistida.id);
}

async function registrarAtendimento(e) {
    e.preventDefault();
    if (enviando || !fichaAtual) return;

    const dados = {
        tipo: $("tipoAtendimento").value,
        tipoOutro: $("tipoOutro").value.trim() || null,
        realizadoEm: $("realizadoEm").value || null,
        relato: $("relato").value.trim(),
        providencias: $("providencias").value.trim() || null,
        encaminharJuridico: $("encaminharJuridico").checked,
        encaminharPsicologia: $("encaminharPsicologia").checked
    };
    if (dados.relato.length < 15) return mostrarMensagem("Descreva o atendimento com mais detalhes (mínimo de 15 caracteres).", "error");
    if (dados.tipo === "outro" && !dados.tipoOutro) return mostrarMensagem("Descreva o tipo de atendimento.", "error");

    enviando = true;
    $("btnSalvarAtendimento").disabled = true;
    const response = await api(`/api/servico-social/prontuarios/${fichaAtual.assistida.id}/atendimentos`, { method: "POST", body: dados });
    enviando = false;
    $("btnSalvarAtendimento").disabled = false;

    // Se der erro, o texto continua no formulário para não ser perdido.
    if (!response.ok) return mostrarMensagem(await erroDe(response, "Não foi possível registrar o atendimento."), "error");

    ultimoAtendimento = await response.json();
    $("atendimentoForm").reset();
    $("tipoOutro").classList.add("hidden");
    atualizarContador();
    mostrarModalAtendimento(ultimoAtendimento);
    mostrarMensagem(`Atendimento ${ultimoAtendimento.numeroAtendimento} do prontuário ${ultimoAtendimento.numeroProntuario} registrado.`, "success");
    await abrirFicha(fichaAtual.assistida.id, "atendimentos");
}

function mostrarModalAtendimento(r) {
    $("modalProntuario").textContent = r.numeroProntuario;
    $("modalNumeroAtendimento").textContent = r.numeroAtendimento;
    $("modalAviso").textContent = r.avisoSnapshot || "";
    $("btnDeclaracaoModal").classList.toggle("hidden", !r.podeDeclaracao);
    $("modalAtendimento").classList.remove("hidden");
}

function fecharModalAtendimento() {
    $("modalAtendimento").classList.add("hidden");
}

async function imprimirDeclaracao(atendimentoId) {
    const response = await api(`/api/servico-social/atendimentos/${atendimentoId}/declaracao`);
    if (!response.ok) return mostrarMensagem(await erroDe(response, "Não foi possível gerar a declaração."), "error");
    const d = await response.json();
    $("impNome").textContent = d.assistidaNome;
    $("impData").textContent = formatarDataHora(d.data);
    $("impProfissional").textContent = `${d.profissionalNome} - Assistente Social`;
    $("impCress").textContent = d.profissionalCress || `ID ${d.profissionalIdentificador} (CRESS não informado no sistema)`;
    if (!d.profissionalCress) mostrarMensagem("A declaração saiu sem CRESS. Peça para a profissional preencher o CRESS no aviso do topo da tela.", "info");
    window.print();
}

async function registrarAdendo(atendimentoId) {
    const texto = $(`adendoTexto${atendimentoId}`)?.value.trim() || "";
    if (texto.length < 3) return mostrarMensagem("Escreva o adendo antes de salvar.", "error");
    const response = await api(`/api/servico-social/atendimentos/${atendimentoId}/adendos`, { method: "POST", body: { texto } });
    if (!response.ok) return mostrarMensagem(await erroDe(response, "Não foi possível salvar o adendo."), "error");
    mostrarMensagem("Adendo registrado.", "success");
    await abrirFicha(fichaAtual.assistida.id, "atendimentos");
}

async function encaminhar(area, rotulo) {
    if (!confirm(`Encaminhar esta assistida para a fila do ${rotulo}? Nada do Serviço Social é compartilhado.`)) return;
    const response = await api(`/api/assistidas/${fichaAtual.assistida.id}/encaminhar`, { method: "POST", body: { area } });
    if (!response.ok) return mostrarMensagem(await erroDe(response, "Não foi possível encaminhar."), "error");
    mostrarMensagem(`Assistida encaminhada: ${rotulo}.`, "success");
    await abrirFicha(fichaAtual.assistida.id);
}

async function encerrarAcompanhamento() {
    const motivo = $("motivoEncerramento").value;
    if (!motivo) return mostrarMensagem("Escolha o motivo do encerramento.", "error");
    if (!confirm("Encerrar o acompanhamento? O prontuário continua guardado e um novo atendimento o reabre.")) return;
    const response = await api(`/api/servico-social/prontuarios/${fichaAtual.assistida.id}/encerrar`, { method: "POST", body: { motivo } });
    if (!response.ok) return mostrarMensagem(await erroDe(response, "Não foi possível encerrar."), "error");
    mostrarMensagem("Acompanhamento encerrado.", "success");
    await abrirFicha(fichaAtual.assistida.id);
}

async function alternarRestricao() {
    const a = fichaAtual.assistida;
    const restringir = !a.acessoRestrito;
    const motivo = $("motivoRestricao").value;
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
    const userId = $("selectProfissional").value;
    if (!userId) return mostrarMensagem("Escolha a profissional.", "error");
    const response = await api(`/api/servico-social/prontuarios/${fichaAtual.assistida.id}/responsaveis`, { method: "POST", body: { userId } });
    if (!response.ok) return mostrarMensagem(await erroDe(response, "Não foi possível atribuir."), "error");
    mostrarMensagem("Responsável atribuída.", "success");
    await abrirFicha(fichaAtual.assistida.id);
}

async function removerResponsavel(userId) {
    if (!confirm("Remover esta responsável do prontuário?")) return;
    const response = await api(`/api/servico-social/prontuarios/${fichaAtual.assistida.id}/responsaveis/${encodeURIComponent(userId)}`, { method: "DELETE" });
    if (!response.ok) return mostrarMensagem(await erroDe(response, "Não foi possível remover."), "error");
    mostrarMensagem("Responsável removida.", "success");
    await abrirFicha(fichaAtual.assistida.id);
}

async function salvarVisibilidade() {
    const visibilidade = $("selectVisibilidade").value;
    const response = await api(`/api/servico-social/prontuarios/${fichaAtual.assistida.id}/visibilidade`, { method: "PUT", body: { visibilidade } });
    if (!response.ok) return mostrarMensagem(await erroDe(response, "Não foi possível salvar."), "error");
    mostrarMensagem(visibilidade === "equipe" ? "Agora toda a equipe do Serviço Social vê o conteúdo." : "Agora só as responsáveis veem o conteúdo.", "success");
    await abrirFicha(fichaAtual.assistida.id);
}

async function pedirEmergencia() {
    const motivo = $("motivoEmergencia").value.trim();
    if (motivo.length < 10) return mostrarMensagem("Explique o motivo (pelo menos 10 caracteres).", "error");
    const response = await api(`/api/servico-social/prontuarios/${fichaAtual.assistida.id}/acesso-emergencia`, { method: "POST", body: { motivo } });
    if (!response.ok) return mostrarMensagem(await erroDe(response, "Não foi possível liberar o acesso."), "error");
    $("motivoEmergencia").value = "";
    mostrarMensagem("Acesso de emergência liberado por 12 horas. As responsáveis foram avisadas.", "success");
    await abrirFicha(fichaAtual.assistida.id);
}

/* ---------------- Estudo social ---------------- */
function textoEstudo(e) {
    const dependentes = e.dependentes.length
        ? e.dependentes.map((d) => `${ROTULOS_PARENTESCO[d.parentesco] || d.parentesco}${d.idade != null ? ` (${d.idade} anos)` : ""}`).join(", ")
        : "Nenhum informado";
    const beneficios = e.beneficios.map((b) => (b === "outro" && e.beneficioOutro ? `Outro: ${e.beneficioOutro}` : ROTULOS_BENEFICIO[b] || b)).join(", ") || "Nenhum";
    const itens = [
        ["Pessoas no domicílio", e.pessoasNoDomicilio ?? "-"],
        ["Quem mora com ela", e.composicaoFamiliar || "-"],
        ["Dependentes", dependentes],
        ["Renda familiar", ROTULOS_RENDA[e.faixaRenda] || "-"],
        ["Trabalho", ROTULOS_TRABALHO[e.situacaoTrabalho] || "-"],
        ["Moradia", (ROTULOS_MORADIA[e.moradia] || "-") + (e.conviveComAgressor === true ? " · convive com o agressor" : e.conviveComAgressor === false ? " · não convive com o agressor" : "")],
        ["Escolaridade", ROTULOS_ESCOLARIDADE[e.escolaridade] || "-"],
        ["CadÚnico", (ROTULOS_CADUNICO[e.cadUnico] || "-") + (e.nis ? ` · NIS ${e.nis}` : "")],
        ["Benefícios", beneficios],
        ["Rede de apoio", e.redeApoio || "-"],
        ["Vulnerabilidades", e.vulnerabilidades.map((v) => ROTULOS_VULNERABILIDADE[v] || v).join(", ") || "Nenhuma"],
        ["Observações", e.observacoes || "-"]
    ];
    return `<dl class="juridico-dados">${itens.map(([r, v]) => `<div><dt>${esc(r)}</dt><dd>${esc(v)}</dd></div>`).join("")}</dl>
        <p class="section-intro" style="margin-top: 10px;">Versão ${e.versao} · ${esc(formatarDataHora(e.criadoEm))} · ${esc(e.profissionalNome)} (${esc(e.profissionalIdentificador)})</p>`;
}

function renderizarEstudo() {
    const f = fichaAtual;
    $("estudoAtual").innerHTML = f.estudo ? textoEstudo(f.estudo) : '<div class="soft-empty-state">Estudo social ainda não preenchido.</div>';
    $("btnEditarEstudo").textContent = f.estudo ? "Atualizar estudo social" : "Preencher estudo social";
    $("btnEditarEstudo").classList.toggle("hidden", !f.podeRegistrar);
    $("estudoHistorico").innerHTML = f.historicoEstudo.length
        ? f.historicoEstudo.map((e) => `<details class="juridico-atendimento"><summary><strong>Versão ${e.versao}</strong> · ${esc(formatarDataHora(e.criadoEm))} · ${esc(e.profissionalNome)}</summary>${textoEstudo(e)}</details>`).join("")
        : '<p class="section-intro">Nenhuma versão anterior.</p>';
    $("cardEstudoForm").classList.add("hidden");
}

function adicionarDependente(d) {
    const div = document.createElement("div");
    div.className = "social-dependente";
    div.innerHTML = `<select class="soft-input" data-parentesco>${Object.entries(ROTULOS_PARENTESCO).map(([k, v]) => `<option value="${k}">${v}</option>`).join("")}</select>
        <input type="number" class="soft-input" data-idade min="0" max="120" placeholder="Idade">
        <button type="button" class="soft-btn soft-btn-danger" data-remover-dependente aria-label="Remover dependente">&times;</button>`;
    if (d) {
        div.querySelector("[data-parentesco]").value = d.parentesco;
        div.querySelector("[data-idade]").value = d.idade ?? "";
    }
    $("listaDependentes").appendChild(div);
}

function abrirFormEstudo(abrir) {
    $("cardEstudoForm").classList.toggle("hidden", !abrir);
    if (!abrir) return;
    const e = fichaAtual.estudo || { dependentes: [], beneficios: [], vulnerabilidades: [] };
    $("pessoasDomicilio").value = e.pessoasNoDomicilio ?? "";
    $("composicaoFamiliar").value = e.composicaoFamiliar || "";
    $("listaDependentes").innerHTML = "";
    e.dependentes.forEach(adicionarDependente);
    $("faixaRenda").value = e.faixaRenda || "";
    $("situacaoTrabalho").value = e.situacaoTrabalho || "";
    $("moradia").value = e.moradia || "";
    $("conviveAgressor").value = e.conviveComAgressor === true ? "sim" : e.conviveComAgressor === false ? "nao" : "";
    $("escolaridade").value = e.escolaridade || "";
    $("cadUnico").value = e.cadUnico || "";
    $("nis").value = e.nis || "";
    document.querySelectorAll('#estudoForm input[name="beneficio"]').forEach((c) => { c.checked = e.beneficios.includes(c.value); });
    $("beneficioOutro").value = e.beneficioOutro || "";
    $("beneficioOutro").classList.toggle("hidden", !e.beneficios.includes("outro"));
    $("redeApoio").value = e.redeApoio || "";
    document.querySelectorAll('#estudoForm input[name="vulnerabilidade"]').forEach((c) => { c.checked = e.vulnerabilidades.includes(c.value); });
    $("observacoesEstudo").value = e.observacoes || "";
    $("cardEstudoForm").scrollIntoView({ behavior: "smooth" });
}

async function salvarEstudo(ev) {
    ev.preventDefault();
    const convive = $("conviveAgressor").value;
    const dados = {
        pessoasNoDomicilio: $("pessoasDomicilio").value ? Number($("pessoasDomicilio").value) : null,
        composicaoFamiliar: $("composicaoFamiliar").value.trim() || null,
        dependentes: Array.from(document.querySelectorAll("#listaDependentes .social-dependente")).map((d) => ({
            parentesco: d.querySelector("[data-parentesco]").value,
            idade: d.querySelector("[data-idade]").value === "" ? null : Number(d.querySelector("[data-idade]").value)
        })),
        faixaRenda: $("faixaRenda").value || null,
        situacaoTrabalho: $("situacaoTrabalho").value || null,
        moradia: $("moradia").value || null,
        conviveComAgressor: convive === "" ? null : convive === "sim",
        escolaridade: $("escolaridade").value || null,
        cadUnico: $("cadUnico").value || null,
        nis: $("nis").value.trim() || null,
        beneficios: Array.from(document.querySelectorAll('#estudoForm input[name="beneficio"]:checked')).map((c) => c.value),
        beneficioOutro: $("beneficioOutro").value.trim() || null,
        redeApoio: $("redeApoio").value.trim() || null,
        vulnerabilidades: Array.from(document.querySelectorAll('#estudoForm input[name="vulnerabilidade"]:checked')).map((c) => c.value),
        observacoes: $("observacoesEstudo").value.trim() || null
    };
    const response = await api(`/api/servico-social/prontuarios/${fichaAtual.assistida.id}/estudo`, { method: "POST", body: dados });
    if (!response.ok) return mostrarMensagem(await erroDe(response, "Não foi possível salvar o estudo social."), "error");
    const e = await response.json();
    mostrarMensagem(`Estudo social salvo (versão ${e.versao}).`, "success");
    await abrirFicha(fichaAtual.assistida.id, "estudo");
}

/* ---------------- Rede ---------------- */
function renderizarRede() {
    const f = fichaAtual;
    $("cardNovaRede").classList.toggle("hidden", !f.podeRegistrar);
    $("listaRede").innerHTML = f.encaminhamentosRede.length
        ? `<table class="social-tabela"><thead><tr><th>Data</th><th>Destino</th><th>Motivo</th><th>Situação</th><th></th></tr></thead><tbody>${f.encaminhamentosRede.map((e) => `
            <tr>
                <td>${esc(formatarDia(e.data))}</td>
                <td><strong>${esc(e.destino === "outro" && e.destinoOutro ? "Outro: " + e.destinoOutro : ROTULOS_DESTINO[e.destino] || e.destino)}</strong><br><small>${esc(e.profissionalNome)}</small></td>
                <td>${esc(e.motivo)}${e.observacaoStatus ? `<br><small>Retorno: ${esc(e.observacaoStatus)}</small>` : ""}</td>
                <td><span class="soft-badge ${e.status === "retorno_recebido" ? "soft-badge--ok" : e.status === "sem_retorno" ? "soft-badge--risco" : ""}">${esc(ROTULOS_STATUS_REDE[e.status] || e.status)}</span></td>
                <td>${f.podeRegistrar ? `<select class="soft-input" id="statusRede${e.id}" style="max-width: 180px;">${Object.entries(ROTULOS_STATUS_REDE).map(([k, v]) => `<option value="${k}" ${k === e.status ? "selected" : ""}>${v}</option>`).join("")}</select>
                    <input type="text" class="soft-input" id="obsRede${e.id}" maxlength="500" placeholder="Observação do retorno" style="max-width: 220px; margin-top: 6px;">
                    <button type="button" class="soft-btn soft-btn-warning" data-status-rede="${e.id}" style="margin-top: 6px;">Atualizar</button>` : ""}</td>
            </tr>`).join("")}</tbody></table>`
        : '<div class="soft-empty-state">Nenhum encaminhamento à rede.</div>';
}

async function registrarRede(ev) {
    ev.preventDefault();
    const dados = { destino: $("destinoRede").value, destinoOutro: $("destinoOutro").value.trim() || null, data: $("dataRede").value || null, motivo: $("motivoRede").value.trim() };
    if (!dados.destino) return mostrarMensagem("Escolha o destino do encaminhamento.", "error");
    if (dados.motivo.length < 5) return mostrarMensagem("Descreva o motivo do encaminhamento.", "error");
    const response = await api(`/api/servico-social/prontuarios/${fichaAtual.assistida.id}/encaminhamentos-rede`, { method: "POST", body: dados });
    if (!response.ok) return mostrarMensagem(await erroDe(response, "Não foi possível registrar o encaminhamento."), "error");
    $("redeForm").reset();
    $("destinoOutro").classList.add("hidden");
    mostrarMensagem("Encaminhamento à rede registrado.", "success");
    await abrirFicha(fichaAtual.assistida.id, "rede");
}

async function atualizarRede(id) {
    const body = { status: $(`statusRede${id}`).value, observacao: $(`obsRede${id}`).value.trim() || null };
    const response = await api(`/api/servico-social/encaminhamentos-rede/${id}`, { method: "PUT", body });
    if (!response.ok) return mostrarMensagem(await erroDe(response, "Não foi possível atualizar."), "error");
    mostrarMensagem("Situação do encaminhamento atualizada.", "success");
    await abrirFicha(fichaAtual.assistida.id, "rede");
}

/* ---------------- Plano ---------------- */
function renderizarPlano() {
    const f = fichaAtual;
    $("metaForm").classList.toggle("hidden", !f.podeRegistrar);
    const total = f.metas.length;
    const concluidas = f.metas.filter((m) => m.situacao === "concluida").length;
    $("resumoPlano").innerHTML = total
        ? `<span class="soft-badge soft-badge--social">${concluidas} de ${total} concluídas (${Math.round((concluidas * 100) / total)}%)</span>
           ${f.metas.some((m) => m.atrasada) ? `<span class="soft-badge soft-badge--risco">${f.metas.filter((m) => m.atrasada).length} com prazo vencido</span>` : ""}`
        : "";
    $("listaMetas").innerHTML = total
        ? f.metas.map((m) => `
            <div class="social-linha">
                <div><strong>${esc(m.descricao)}</strong><br><small>Prazo: ${esc(formatarDia(m.prazo))}${m.atrasada ? " · <span style=\"color:#A23B3B\">vencido</span>" : ""} · criada por ${esc(m.criadaPorNome)}${m.concluidaEm ? ` · concluída em ${esc(formatarDataHora(m.concluidaEm))}` : ""}</small></div>
                <div>${f.podeRegistrar
                    ? `<select class="soft-input" data-meta="${m.id}" aria-label="Situação da meta">${Object.entries(ROTULOS_META).map(([k, v]) => `<option value="${k}" ${k === m.situacao ? "selected" : ""}>${v}</option>`).join("")}</select>`
                    : `<span class="soft-badge">${esc(ROTULOS_META[m.situacao])}</span>`}</div>
            </div>`).join("")
        : '<div class="soft-empty-state">Nenhuma meta no plano ainda.</div>';
}

async function registrarMeta(ev) {
    ev.preventDefault();
    const descricao = $("descricaoMeta").value.trim();
    if (descricao.length < 3) return mostrarMensagem("Descreva a meta ou ação.", "error");
    const response = await api(`/api/servico-social/prontuarios/${fichaAtual.assistida.id}/metas`, { method: "POST", body: { descricao, prazo: $("prazoMeta").value || null } });
    if (!response.ok) return mostrarMensagem(await erroDe(response, "Não foi possível adicionar a meta."), "error");
    $("metaForm").reset();
    mostrarMensagem("Meta adicionada ao plano.", "success");
    await abrirFicha(fichaAtual.assistida.id, "plano");
}

async function atualizarMeta(id, situacao) {
    const response = await api(`/api/servico-social/metas/${id}`, { method: "PUT", body: { situacao } });
    if (!response.ok) return mostrarMensagem(await erroDe(response, "Não foi possível atualizar a meta."), "error");
    mostrarMensagem("Meta atualizada.", "success");
    await abrirFicha(fichaAtual.assistida.id, "plano");
}

/* ---------------- Documentos (anexos) ---------------- */
function renderizarAnexos() {
    const f = fichaAtual;
    $("cardAnexar").classList.toggle("hidden", !f.podeRegistrar || !f.anexosDisponiveis);
    $("atendimentoAnexo").innerHTML = '<option value="">Documento do prontuário (sem atendimento)</option>'
        + f.atendimentos.filter((a) => !a.conteudoOculto).map((a) => `<option value="${a.id}">Atendimento ${a.numero} · ${esc(formatarDataHora(a.realizadoEm))}</option>`).join("");

    $("listaAnexos").innerHTML = !f.anexosDisponiveis
        ? '<div class="soft-alert">Anexos indisponíveis neste servidor: a chave de criptografia não está configurada.</div>'
        : f.anexos.length
            ? `<table class="social-tabela"><thead><tr><th>Tipo</th><th>Arquivo</th><th>Tamanho</th><th>Enviado</th><th>Quem vê</th><th></th></tr></thead><tbody>${f.anexos.map((a) => `
                <tr>
                    <td><strong>${esc(ROTULOS_ANEXO[a.tipo] || a.tipo)}</strong>${a.descricao ? `<br><small>${esc(a.descricao)}</small>` : ""}${a.atendimentoNumero ? `<br><small>Atendimento ${a.atendimentoNumero}</small>` : ""}</td>
                    <td>${esc(a.nomeArquivo)}</td>
                    <td>${esc(formatarTamanho(a.tamanhoBytes))}</td>
                    <td>${esc(formatarDataHora(a.criadoEm))}<br><small>${esc(a.autorNome)} (${esc(a.autorIdentificador)})</small></td>
                    <td><span class="soft-badge ${a.visibilidade === "equipe_tecnica" ? "soft-badge--juridico" : "soft-badge--social"}">${a.visibilidade === "equipe_tecnica" ? "Equipe técnica" : "Só Serviço Social"}</span></td>
                    <td style="white-space: nowrap;">
                        <button type="button" class="soft-btn soft-btn-warning" data-ver-anexo="${a.id}">Ver</button>
                        <button type="button" class="soft-btn soft-btn-warning" data-baixar-anexo="${a.id}">Baixar</button>
                        ${a.podeExcluir ? `<button type="button" class="soft-btn soft-btn-danger" data-excluir-anexo="${a.id}">Excluir</button>` : ""}
                    </td>
                </tr>`).join("")}</tbody></table>`
            : '<div class="soft-empty-state">Nenhum documento anexado.</div>';
}

function irParaAnexo(atendimentoId) {
    mostrarSecao("documentos");
    $("atendimentoAnexo").value = String(atendimentoId);
    $("cardAnexar").scrollIntoView({ behavior: "smooth" });
}

async function enviarAnexo(ev) {
    ev.preventDefault();
    if (enviando) return;
    const arquivo = $("arquivoAnexo").files[0];
    const tipo = $("tipoAnexo").value;
    if (!tipo) return mostrarMensagem("Escolha o tipo do documento.", "error");
    if (!arquivo) return mostrarMensagem("Escolha o arquivo.", "error");
    if (!/\.(pdf|jpe?g|png)$/i.test(arquivo.name)) return mostrarMensagem("Envie só PDF, JPG ou PNG.", "error");
    if (arquivo.size > TAMANHO_MAXIMO) return mostrarMensagem("O arquivo passa de 5 MB. Reduza ou digitalize de novo com menos resolução.", "error");

    const dados = new FormData();
    dados.append("arquivo", arquivo);
    dados.append("tipo", tipo);
    dados.append("descricao", $("descricaoAnexo").value.trim());
    dados.append("visibilidade", $("visibilidadeAnexo").value);
    if ($("atendimentoAnexo").value) dados.append("atendimentoId", $("atendimentoAnexo").value);

    enviando = true;
    $("btnAnexar").disabled = true;
    const response = await api(`/api/anexos-sociais/prontuarios/${fichaAtual.assistida.id}`, { method: "POST", body: dados });
    enviando = false;
    $("btnAnexar").disabled = false;
    if (!response.ok) return mostrarMensagem(await erroDe(response, "Não foi possível anexar o documento."), "error");

    $("anexoForm").reset();
    $("nomeArquivoEscolhido").textContent = "Nenhum arquivo escolhido";
    mostrarMensagem("Documento anexado.", "success");
    await abrirFicha(fichaAtual.assistida.id, "documentos");
}

async function buscarArquivo(id, inline) {
    const response = await api(`/api/anexos-sociais/${id}/arquivo${inline ? "?inline=true" : ""}`);
    if (!response.ok) {
        mostrarMensagem(await erroDe(response, "Não foi possível abrir o documento."), "error");
        return null;
    }
    return response.blob();
}

async function verAnexo(id) {
    const anexo = fichaAtual.anexos.find((a) => a.id === id);
    const blob = await buscarArquivo(id, true);
    if (!blob) return;
    fecharPrevia();
    previaUrl = URL.createObjectURL(blob);
    $("previaTitulo").textContent = `${ROTULOS_ANEXO[anexo.tipo] || "Documento"} · ${anexo.nomeArquivo}`;
    $("previaConteudo").innerHTML = anexo.contentType === "application/pdf"
        ? `<iframe src="${previaUrl}" title="Prévia do PDF"></iframe>`
        : `<img src="${previaUrl}" alt="Prévia do documento">`;
    $("modalPrevia").classList.remove("hidden");
}

function fecharPrevia() {
    $("modalPrevia").classList.add("hidden");
    $("previaConteudo").innerHTML = "";
    if (previaUrl) URL.revokeObjectURL(previaUrl);
    previaUrl = null;
}

async function baixarAnexo(id) {
    const anexo = fichaAtual.anexos.find((a) => a.id === id);
    const blob = await buscarArquivo(id, false);
    if (!blob) return;
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = anexo.nomeArquivo;
    document.body.appendChild(link);
    link.click();
    link.remove();
    setTimeout(() => URL.revokeObjectURL(url), 2000);
}

async function excluirAnexo(id) {
    const motivo = prompt("Motivo da exclusão (fica registrado no histórico):");
    if (motivo === null) return;
    if (motivo.trim().length < 5) return mostrarMensagem("Explique o motivo da exclusão (pelo menos 5 caracteres).", "error");
    const response = await api(`/api/anexos-sociais/${id}/excluir`, { method: "POST", body: { motivo: motivo.trim() } });
    if (!response.ok) return mostrarMensagem(await erroDe(response, "Não foi possível excluir o documento."), "error");
    mostrarMensagem("Documento excluído. O registro e o motivo ficam no histórico.", "success");
    await abrirFicha(fichaAtual.assistida.id, "documentos");
}
