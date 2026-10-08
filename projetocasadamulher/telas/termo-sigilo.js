(function () {
    let termoAtual = null;
    let leituraConcluida = false;

    function normalizarNome(nome) {
        return (nome || "")
            .normalize("NFD")
            .replace(/[̀-ͯ]/g, "")
            .trim()
            .toUpperCase()
            .split(/\s+/)
            .filter(Boolean)
            .join(" ");
    }

    function formatarDataHora(valor) {
        const data = new Date(valor);
        return Number.isFinite(data.getTime())
            ? data.toLocaleString("pt-BR", { dateStyle: "short", timeStyle: "medium" })
            : "";
    }

    function criarElemento(tag, texto, classe) {
        const elemento = document.createElement(tag);
        if (texto) elemento.textContent = texto;
        if (classe) elemento.className = classe;
        return elemento;
    }

    function renderizarTermo(termo) {
        document.getElementById("termoTitulo").textContent = termo.titulo;
        document.getElementById("termoIdentNome").textContent = termo.nomeCompleto;
        document.getElementById("termoIdentId").textContent = termo.identificadorFuncionario;
        document.getElementById("termoIdentVersao").textContent = termo.versao;

        const resumo = document.getElementById("termoResumo");
        resumo.replaceChildren(...termo.resumo.map(item => criarElemento("li", item)));

        const texto = document.getElementById("termoTexto");
        texto.replaceChildren();
        termo.secoes.forEach(function (secao) {
            texto.appendChild(criarElemento("h3", secao.titulo));
            secao.paragrafos.forEach(paragrafo => texto.appendChild(criarElemento("p", paragrafo)));
        });

        const confirmacoes = document.getElementById("termoConfirmacoes");
        confirmacoes.querySelectorAll(".termo-confirmacao").forEach(item => item.remove());
        termo.confirmacoes.forEach(function (confirmacao, indice) {
            const label = criarElemento("label", null, "termo-confirmacao");
            const checkbox = document.createElement("input");
            checkbox.type = "checkbox";
            checkbox.className = "soft-checkbox";
            checkbox.id = `termoConfirmacao${indice}`;
            checkbox.addEventListener("change", atualizarBotaoAceite);
            label.htmlFor = checkbox.id;
            label.appendChild(checkbox);
            label.appendChild(criarElemento("span", confirmacao));
            confirmacoes.appendChild(label);
        });

        document.getElementById("termoNomeAjuda").textContent =
            `Digite exatamente como está no seu cadastro: ${termo.nomeCompleto}`;

        document.getElementById("termoConteudo").classList.remove("hidden");
    }

    function liberarAceite() {
        if (leituraConcluida) return;

        leituraConcluida = true;
        document.getElementById("termoConfirmacoes").disabled = false;
        document.getElementById("termoNomeAssinatura").disabled = false;

        const aviso = document.getElementById("termoAvisoLeitura");
        aviso.textContent = "Leitura concluída. Agora confirme as declarações e assine com seu nome.";
        aviso.classList.add("concluido");
    }

    function observarLeitura() {
        const texto = document.getElementById("termoTexto");

        function verificar() {
            if (texto.scrollTop + texto.clientHeight >= texto.scrollHeight - 8) {
                liberarAceite();
            }
        }

        texto.addEventListener("scroll", verificar);
        // Em telas grandes o texto pode caber inteiro sem rolagem.
        requestAnimationFrame(verificar);
    }

    function todasConfirmadas() {
        const checkboxes = document.querySelectorAll("#termoConfirmacoes input[type='checkbox']");
        return checkboxes.length > 0 && Array.from(checkboxes).every(item => item.checked);
    }

    function nomeConfere() {
        const digitado = document.getElementById("termoNomeAssinatura").value;
        return termoAtual && normalizarNome(digitado) === normalizarNome(termoAtual.nomeCompleto);
    }

    function atualizarBotaoAceite() {
        document.getElementById("btnAceitarTermo").disabled = !(leituraConcluida && todasConfirmadas() && nomeConfere());
    }

    function mostrarAceito(termo, nomeAssinado, avisoSnapshot) {
        document.getElementById("formTermoSigilo").classList.add("hidden");
        document.getElementById("termoAvisoLeitura").classList.add("hidden");
        document.getElementById("termoSubtitulo").textContent = "Você aceitou este termo. Guarde uma cópia.";

        document.getElementById("termoAceitoNome").textContent = termo.nomeCompleto;
        document.getElementById("termoAceitoId").textContent = termo.identificadorFuncionario;
        document.getElementById("termoAceitoData").textContent = formatarDataHora(termo.aceitoEm);
        document.getElementById("termoAceitoVersao").textContent = termo.versao;
        document.getElementById("termoAceitoHash").textContent = termo.hash;

        const aviso = document.getElementById("termoAceitoAviso");
        if (avisoSnapshot) {
            setMessage(aviso, avisoSnapshot, "error");
        } else if (nomeAssinado) {
            // Só logo após assinar: a cópia por e-mail é enviada no aceite.
            setMessage(aviso, "Uma cópia deste termo foi enviada para o seu e-mail cadastrado.", "success");
        }

        document.getElementById("termoAceito").classList.remove("hidden");
    }

    async function aceitar(event) {
        event.preventDefault();
        const mensagem = document.getElementById("mensagemTermo");

        if (!leituraConcluida || !todasConfirmadas() || !nomeConfere()) {
            setMessage(mensagem, "Leia o termo até o final, marque todas as declarações e digite seu nome completo.", "error");
            return;
        }

        const botao = document.getElementById("btnAceitarTermo");
        botao.disabled = true;
        setMessage(mensagem, "Registrando seu aceite...", "info");

        const nomeAssinado = document.getElementById("termoNomeAssinatura").value.trim();

        try {
            const response = await CasaMulherAuth.apiFetch("/api/termo-sigilo/aceitar", {
                method: "POST",
                body: {
                    versao: termoAtual.versao,
                    nomeAssinado: nomeAssinado,
                    confirmacoes: Array.from(document.querySelectorAll("#termoConfirmacoes input[type='checkbox']")).map(item => item.checked)
                },
                mensagemElement: mensagem
            });

            if (!response.ok) {
                setMessage(mensagem, await readApiMessage(response), "error");
                atualizarBotaoAceite();
                return;
            }

            termoAtual = await response.json();
            setMessage(mensagem, "", "");
            mostrarAceito(termoAtual, nomeAssinado, termoAtual.avisoSnapshot);
            document.getElementById("btnContinuarTermo").focus();
        } catch {
            setMessage(mensagem, "Não foi possível conectar à API. Tente novamente.", "error");
            atualizarBotaoAceite();
        }
    }

    function recusar() {
        const confirmou = window.confirm(
            "Sem aceitar o termo você não poderá usar o sistema. Se tiver dúvidas sobre o termo, fale com a coordenação antes de aceitar.\n\nDeseja sair agora?"
        );

        if (confirmou) {
            CasaMulherAuth.logout("Para acessar o sistema é obrigatório aceitar o Termo de Confidencialidade e Proteção de Dados.");
        }
    }

    async function iniciar() {
        if (!document.getElementById("formTermoSigilo")) return;

        const usuario = await CasaMulherAuth.protegerPagina({ permitirTermoPendente: true });
        if (!usuario) return;

        const mensagemCarregar = document.getElementById("mensagemCarregarTermo");

        try {
            const response = await CasaMulherAuth.apiFetch("/api/termo-sigilo", { mensagemElement: mensagemCarregar });

            if (!response.ok) {
                setMessage(mensagemCarregar, await readApiMessage(response), "error");
                return;
            }

            termoAtual = await response.json();
        } catch {
            setMessage(mensagemCarregar, "Não foi possível carregar o termo. Recarregue a página.", "error");
            return;
        }

        renderizarTermo(termoAtual);
        document.getElementById("btnImprimirTermo").addEventListener("click", () => window.print());
        document.getElementById("btnContinuarTermo").addEventListener("click", function () {
            redirectAfterLogin(CasaMulherAuth.getUsuario());
        });

        if (termoAtual.aceito) {
            mostrarAceito(termoAtual, null, null);
            return;
        }

        if (!termoAtual.obrigatorio) {
            document.getElementById("termoSubtitulo").textContent = "Seu perfil não precisa aceitar este termo.";
            document.getElementById("formTermoSigilo").classList.add("hidden");
            document.getElementById("termoAvisoLeitura").classList.add("hidden");
            return;
        }

        document.getElementById("termoSubtitulo").textContent =
            "Leitura obrigatória. Antes de usar o sistema, leia com atenção e confirme que entendeu suas responsabilidades.";

        document.getElementById("formTermoSigilo").addEventListener("submit", aceitar);
        document.getElementById("btnRecusarTermo").addEventListener("click", recusar);
        document.getElementById("termoNomeAssinatura").addEventListener("input", atualizarBotaoAceite);
        // Impede colar o nome: a assinatura precisa ser digitada.
        document.getElementById("termoNomeAssinatura").addEventListener("paste", event => event.preventDefault());
        observarLeitura();
    }

    document.addEventListener("DOMContentLoaded", iniciar);
})();
