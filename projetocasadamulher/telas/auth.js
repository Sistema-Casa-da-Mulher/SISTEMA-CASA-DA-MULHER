(function () {
    window.API_BASE_URL = window.API_BASE_URL || "http://localhost:5001";

    const STORAGE_KEYS = [
        "token",
        "expiraEm",
        "perfil",
        "email",
        "emailRecuperacao",
        "emailRecuperacaoConfirmado",
        "nomeCompleto",
        "identificadorFuncionario",
        "doisFatoresObrigatorio",
        "doisFatoresAtivado",
        "deveTrocarSenha"
    ];

    const PERMISSOES_POR_AREA = {
        convites: ["adm"],
        equipe: ["equipe"],
        equipeConvites: ["adm", "equipe"],
        funcionarios: ["adm"],
        auditoria: ["adm"],
        emails: ["adm"],
        recepcao: ["adm", "recepcao"],
        cursos: ["adm", "professor"],
        social: ["adm", "as_social"],
        juridico: ["adm", "juridico"],
        relatorios: ["adm", "as_social", "juridico"]
    };

    const AREAS_EQUIPE_DEV = [
        "convites",
        "funcionarios",
        "auditoria",
        "emails",
        "recepcao",
        "cursos",
        "social",
        "juridico",
        "relatorios"
    ];

    function getToken() {
        return localStorage.getItem("token");
    }

    function getPerfil() {
        return localStorage.getItem("perfil");
    }

    function getUsuario() {
        return {
            token: getToken(),
            expiraEm: localStorage.getItem("expiraEm") || "",
            perfil: getPerfil(),
            email: localStorage.getItem("email") || "",
            emailRecuperacao: localStorage.getItem("emailRecuperacao") || "",
            emailRecuperacaoConfirmado: localStorage.getItem("emailRecuperacaoConfirmado") === "true",
            nomeCompleto: localStorage.getItem("nomeCompleto") || "",
            identificadorFuncionario: localStorage.getItem("identificadorFuncionario") || "",
            doisFatoresObrigatorio: localStorage.getItem("doisFatoresObrigatorio") === "true",
            doisFatoresAtivado: localStorage.getItem("doisFatoresAtivado") === "true",
            deveTrocarSenha: localStorage.getItem("deveTrocarSenha") === "true"
        };
    }

    function getPainelUrl(usuario) {
        const perfil = usuario?.perfil || getPerfil();
        return perfil === "equipe" ? "equipe-painel.html" : "painel.html";
    }

    function atualizarLinksPainel(usuario) {
        const painelUrl = getPainelUrl(usuario);

        document.querySelectorAll("[data-painel-link]").forEach(function (link) {
            link.setAttribute("href", painelUrl);
        });
    }

    function estaLogado() {
        return Boolean(getToken()) && !sessaoExpirada();
    }

    function sessaoExpirada() {
        const expiraEm = localStorage.getItem("expiraEm");

        if (!expiraEm) {
            return false;
        }

        const timestamp = new Date(expiraEm).getTime();

        if (!Number.isFinite(timestamp)) {
            return false;
        }

        return timestamp <= Date.now();
    }

    function podeAcessar(area) {
        const perfil = getPerfil();
        const perfisPermitidos = PERMISSOES_POR_AREA[area];

        if (!perfil || !perfisPermitidos) {
            return podeAcessarEquipeDev(area);
        }

        return perfisPermitidos.includes(perfil) || podeAcessarEquipeDev(area);
    }

    function ehPerfilEquipe(usuario) {
        const perfil = usuario?.perfil || getPerfil();
        return perfil === "equipe";
    }

    function podeAcessarEquipeDev(area) {
        return ehPerfilEquipe() && AREAS_EQUIPE_DEV.includes(area);
    }

    async function protegerArea(area, options) {
        const settings = options || {};
        const usuario = await protegerPagina(settings);

        if (!usuario) {
            return null;
        }

        if (!podeAcessar(area)) {
            if (settings.conteudoElement) {
                settings.conteudoElement.classList.add("hidden");
            }

            if (settings.restritoElement) {
                settings.restritoElement.classList.remove("hidden");
            }

            mostrarMensagem(settings.mensagemElement, "Você não tem permissão para acessar esta área.", "error");
            return false;
        }

        return usuario;
    }

    function mostrarMensagem(element, text, type) {
        if (!element) {
            return;
        }

        element.textContent = text;
        element.className = `message ${type || ""}`.trim();
    }

    function limparSessao() {
        STORAGE_KEYS.forEach(function (key) {
            localStorage.removeItem(key);
        });

        sessionStorage.removeItem("loginTemporario2fa");
    }

    function logout(message) {
        if (typeof limparExpedienteSessaoAtual === "function") {
            limparExpedienteSessaoAtual();
        }
        limparSessao();

        if (message) {
            sessionStorage.setItem("mensagemLogin", message);
        }

        window.location.href = "index.html";
    }

    function salvarSessao(resultado) {
        localStorage.setItem("token", resultado.token);

        if (resultado.expiraEm) {
            localStorage.setItem("expiraEm", resultado.expiraEm);
        } else {
            localStorage.removeItem("expiraEm");
        }

        salvarUsuario(resultado);
    }

    function saveToken(tokenOuResultado) {
        if (tokenOuResultado && typeof tokenOuResultado === "object") {
            salvarSessao(tokenOuResultado);
            return;
        }

        localStorage.setItem("token", tokenOuResultado || "");
        localStorage.removeItem("expiraEm");
    }

    function salvarUsuario(usuario) {
        localStorage.setItem("perfil", usuario.perfil || "");
        localStorage.setItem("email", usuario.email || "");
        localStorage.setItem("emailRecuperacao", usuario.emailRecuperacao || "");
        localStorage.setItem("emailRecuperacaoConfirmado", String(Boolean(usuario.emailRecuperacaoConfirmado)));
        localStorage.setItem("nomeCompleto", usuario.nomeCompleto || "");
        localStorage.setItem("identificadorFuncionario", usuario.identificadorFuncionario || "");
        localStorage.setItem("doisFatoresObrigatorio", String(Boolean(usuario.doisFatoresObrigatorio)));
        localStorage.setItem("doisFatoresAtivado", String(Boolean(usuario.doisFatoresAtivado)));
        localStorage.setItem("deveTrocarSenha", String(Boolean(usuario.deveTrocarSenha)));
        atualizarLinksPainel(usuario);
    }

    function getAuthHeaders(includeJson) {
        const headers = {};

        if (includeJson) {
            headers["Content-Type"] = "application/json";
        }

        const token = getToken();

        if (token) {
            headers.Authorization = `Bearer ${token}`;
        }

        return headers;
    }

    async function apiFetch(url, options) {
        const settings = options || {};
        const mensagemElement = settings.mensagemElement;
        const forbiddenMessage = settings.forbiddenMessage || "Você não tem permissão para acessar esta área.";
        const fetchOptions = Object.assign({}, settings);

        delete fetchOptions.mensagemElement;
        delete fetchOptions.forbiddenMessage;

        const headers = new Headers(fetchOptions.headers || {});
        const token = getToken();

        if (!token || sessaoExpirada()) {
            logout("Sua sessão expirou por segurança. Faça login novamente.");
            return new Response(null, { status: 401 });
        }

        headers.set("Authorization", `Bearer ${token}`);

        if (fetchOptions.body && typeof fetchOptions.body === "object" && !(fetchOptions.body instanceof FormData)) {
            if (!headers.has("Content-Type")) {
                headers.set("Content-Type", "application/json");
            }

            fetchOptions.body = JSON.stringify(fetchOptions.body);
        }

        fetchOptions.headers = headers;

        const requestUrl = url.startsWith("http") ? url : `${window.API_BASE_URL}${url}`;
        let response;

        try {
            response = await fetch(requestUrl, fetchOptions);
        } catch (error) {
            const tempoEsgotado = error?.name === "AbortError";
            const textoErro = tempoEsgotado
                ? "A API demorou demais para responder. Confira a lista antes de tentar novamente."
                : "Não foi possível conectar à API.";

            mostrarMensagem(mensagemElement, textoErro, "error");
            throw new Error(textoErro, { cause: error });
        }

        if (response.status === 401) {
            logout("Sua sessão expirou por segurança. Faça login novamente.");
            return response;
        }

        if (response.status === 403) {
            if (await termoSigiloPendenteNaResposta(response)) {
                window.location.href = "termo-sigilo.html";
                return response;
            }

            mostrarMensagem(mensagemElement, forbiddenMessage, "error");
        }

        return response;
    }

    async function termoSigiloPendenteNaResposta(response) {
        try {
            const corpo = await response.clone().json();
            return corpo?.erro === "TERMO_SIGILO_PENDENTE";
        } catch {
            return false;
        }
    }

    async function carregarUsuarioAtual(options) {
        const settings = options || {};

        if (!getToken()) {
            if (settings.redirect !== false) {
                if (window.location.pathname.endsWith("seguranca.html")) {
                    sessionStorage.setItem("redirectAfterLogin", "seguranca.html");
                }
                logout("Sua sessão expirou por segurança. Faça login novamente.");
            }

            return null;
        }

        if (sessaoExpirada()) {
            if (settings.redirect !== false) {
                if (window.location.pathname.endsWith("seguranca.html")) {
                    sessionStorage.setItem("redirectAfterLogin", "seguranca.html");
                }
                logout("Sua sessão expirou por segurança. Faça login novamente.");
            }

            return null;
        }

        let response;

        try {
            response = await apiFetch("/api/auth/me", {
                mensagemElement: settings.mensagemElement
            });
        } catch {
            return null;
        }

        if (!response.ok) {
            return null;
        }

        const usuario = await response.json();
        salvarUsuario(usuario);
        return usuario;
    }

    async function protegerPagina(options) {
        const settings = options || {};
        const usuario = await carregarUsuarioAtual(settings);

        if (!usuario) {
            return null;
        }

        const paginaTrocaSenha = window.location.pathname.endsWith("trocar-senha.html");
        const paginaSeguranca = window.location.pathname.endsWith("seguranca.html");
        const paginaTermoSigilo = window.location.pathname.endsWith("termo-sigilo.html");

        if (usuario.deveTrocarSenha && !paginaTrocaSenha && settings.permitirTrocaSenhaPendente !== true) {
            window.location.href = "trocar-senha.html";
            return null;
        }

        if (usuario.termoSigiloPendente && !paginaTermoSigilo && settings.permitirTermoPendente !== true) {
            window.location.href = "termo-sigilo.html";
            return null;
        }

        if (usuario.securitySetupRequired && !paginaSeguranca && settings.permitirSegurancaPendente !== true) {
            window.location.href = "seguranca.html";
            return null;
        }

        return usuario;
    }

    async function protegerPerfil(perfil, options) {
        const settings = options || {};
        const usuario = await protegerPagina(settings);

        if (!usuario) {
            return null;
        }

        if (usuario.perfil !== perfil) {
            if (settings.conteudoElement) {
                settings.conteudoElement.classList.add("hidden");
            }

            if (settings.restritoElement) {
                settings.restritoElement.classList.remove("hidden");
            }

            mostrarMensagem(settings.mensagemElement, "Você não tem permissão para acessar esta área.", "error");
            return null;
        }

        return usuario;
    }

    window.CasaMulherAuth = {
        apiFetch,
        carregarUsuarioAtual,
        estaLogado,
        getAuthHeaders,
        getPerfil,
        getPainelUrl,
        getToken,
        getUsuario,
        limparSessao,
        logout,
        podeAcessar,
        podeAcessarEquipeDev,
        protegerPagina,
        protegerArea,
        protegerPerfil,
        saveToken,
        salvarSessao,
        salvarUsuario,
        sessaoExpirada
    };

    atualizarLinksPainel(getUsuario());
})();
