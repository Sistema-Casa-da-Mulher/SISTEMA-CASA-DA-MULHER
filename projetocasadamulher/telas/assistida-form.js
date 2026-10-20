/*
 * Formulário compartilhado do cadastro da assistida (Recepção e Jurídico).
 * Um único cadastro por mulher: os dois perfis usam os mesmos campos e validações.
 */
(function () {
    "use strict";

    const ROTULOS_DIRECIONAMENTO = {
        Culinaria: "Culinária",
        Danca: "Dança",
        Juridica: "Jurídica",
        Psicologa: "Psicóloga",
        AssistenteSocial: "Assistente social"
    };

    const ROTULOS_CONTATO = {
        pode_ligar: "Pode ligar",
        nao_ligar: "Não ligar",
        horario: "Só no horário combinado"
    };

    const ROTULOS_MOTIVO_RESTRICAO = {
        segredo_justica: "Processo em segredo de justiça",
        casa_abrigo: "Casa-abrigo de endereço sigiloso",
        risco_grave: "Medida protetiva / risco grave",
        pedido_assistida: "Pedido da própria assistida"
    };

    function somenteDigitos(valor) {
        return String(valor || "").replace(/\D/g, "");
    }

    function validarCPF(cpf) {
        const clean = somenteDigitos(cpf);
        if (clean.length !== 11 || /^(\d)\1{10}$/.test(clean)) return false;

        function digito(quantidade) {
            let soma = 0;
            for (let i = 0; i < quantidade; i += 1) {
                soma += Number(clean[i]) * (quantidade + 1 - i);
            }
            const resto = (soma * 10) % 11;
            return resto === 10 ? 0 : resto;
        }

        return digito(9) === Number(clean[9]) && digito(10) === Number(clean[10]);
    }

    function maskCPF(input) {
        let value = somenteDigitos(input.value).slice(0, 11);
        value = value.replace(/^(\d{3})(\d)/, "$1.$2");
        value = value.replace(/^(\d{3})\.(\d{3})(\d)/, "$1.$2.$3");
        value = value.replace(/\.(\d{3})(\d)/, ".$1-$2");
        input.value = value;
    }

    function maskPhone(input) {
        const digitos = somenteDigitos(input.value).slice(0, 11);
        let value = digitos.replace(/^(\d{2})(\d)/, "($1) $2");
        value = digitos.length <= 10
            ? value.replace(/(\d{4})(\d{1,4})$/, "$1-$2")
            : value.replace(/(\d{5})(\d{1,4})$/, "$1-$2");
        input.value = value;
    }

    function maskCep(input) {
        input.value = somenteDigitos(input.value).slice(0, 8).replace(/^(\d{5})(\d)/, "$1-$2");
    }

    function campo(form, id) {
        return form.querySelector(`#${id}`);
    }

    async function buscarCep(form, avisar) {
        const cepInput = campo(form, "cep");
        const cep = somenteDigitos(cepInput?.value);
        if (cep.length !== 8) return;

        try {
            const response = await fetch(`https://viacep.com.br/ws/${cep}/json/`);
            const data = await response.json();
            if (data.erro) {
                avisar?.("CEP não encontrado. Preencha o endereço manualmente.", "info");
                return;
            }
            if (campo(form, "endereco") && !campo(form, "endereco").value) campo(form, "endereco").value = data.logradouro || "";
            if (campo(form, "bairro")) campo(form, "bairro").value = data.bairro || "";
            if (campo(form, "cidade")) campo(form, "cidade").value = data.localidade || "";
            if (campo(form, "uf")) campo(form, "uf").value = data.uf || "";
            campo(form, "enderecoNumero")?.focus();
        } catch {
            // Sem internet ou ViaCEP fora do ar: o preenchimento manual continua disponível.
        }
    }

    function atualizarEstados(form) {
        const semTelefone = campo(form, "semTelefoneSeguro")?.checked;
        const telefone = campo(form, "telefone");
        const contato = campo(form, "contatoSeguro");
        if (telefone) {
            telefone.disabled = Boolean(semTelefone);
            if (semTelefone) telefone.value = "";
        }
        if (contato) {
            contato.disabled = Boolean(semTelefone);
            if (semTelefone) contato.value = "nao_ligar";
        }

        const sigiloso = campo(form, "enderecoSigiloso")?.checked;
        form.querySelectorAll("[data-endereco-bloco]").forEach(function (bloco) {
            bloco.classList.toggle("hidden", Boolean(sigiloso));
        });
    }

    function init(form, avisar) {
        campo(form, "cpf")?.addEventListener("input", function (e) { maskCPF(e.target); });
        campo(form, "telefone")?.addEventListener("input", function (e) { maskPhone(e.target); });
        campo(form, "cep")?.addEventListener("input", function (e) { maskCep(e.target); });
        campo(form, "cep")?.addEventListener("blur", function () { buscarCep(form, avisar); });
        campo(form, "btnBuscarCep")?.addEventListener("click", function () { buscarCep(form, avisar); });
        campo(form, "semTelefoneSeguro")?.addEventListener("change", function () { atualizarEstados(form); });
        campo(form, "enderecoSigiloso")?.addEventListener("change", function () { atualizarEstados(form); });
        atualizarEstados(form);
    }

    function valor(form, id) {
        const el = campo(form, id);
        if (!el) return undefined;
        if (el.type === "checkbox") return el.checked;
        return el.value.trim();
    }

    function ler(form) {
        return {
            nome: valor(form, "nome") || "",
            cpf: somenteDigitos(valor(form, "cpf")),
            dataNascimento: valor(form, "dataNascimento") || null,
            telefone: valor(form, "telefone") || null,
            semTelefoneSeguro: Boolean(valor(form, "semTelefoneSeguro")),
            contatoSeguro: valor(form, "contatoSeguro") || "pode_ligar",
            contatoSeguroObservacao: valor(form, "contatoSeguroObservacao") || null,
            email: valor(form, "email") || null,
            enderecoSigiloso: Boolean(valor(form, "enderecoSigiloso")),
            cep: somenteDigitos(valor(form, "cep")) || null,
            endereco: valor(form, "endereco") || null,
            enderecoNumero: valor(form, "enderecoNumero") || null,
            bairro: valor(form, "bairro") || null,
            cidade: valor(form, "cidade") || null,
            uf: valor(form, "uf") || null,
            direcionamento: valor(form, "curso") || null,
            encaminharJuridico: Boolean(valor(form, "encaminharJuridico")),
            observacoes: valor(form, "observacoes") || null
        };
    }

    /** Retorna a mensagem de erro ou null. Nome com hífen/apóstrofo é aceito. */
    function validar(dados) {
        const partes = dados.nome.split(/\s+/).filter(Boolean);
        if (partes.length < 2) return "Informe o nome completo (nome e sobrenome).";
        if (!validarCPF(dados.cpf)) return "CPF inválido. Confira os números informados.";
        const tel = somenteDigitos(dados.telefone);
        if (!dados.semTelefoneSeguro && tel.length > 0 && tel.length !== 10 && tel.length !== 11) {
            return "Telefone inválido. Informe DDD + número.";
        }
        if (!dados.enderecoSigiloso && dados.cep && dados.cep.length !== 8) return "CEP inválido (8 dígitos).";
        return null;
    }

    function preencher(form, a) {
        const set = function (id, v) {
            const el = campo(form, id);
            if (!el) return;
            if (el.type === "checkbox") el.checked = Boolean(v);
            else el.value = v == null ? "" : v;
        };
        set("nome", a.nome);
        set("cpf", a.cpf);
        set("dataNascimento", a.dataNascimento);
        set("telefone", a.telefone);
        set("semTelefoneSeguro", a.semTelefoneSeguro);
        set("contatoSeguro", a.contatoSeguro || "pode_ligar");
        set("contatoSeguroObservacao", a.contatoSeguroObservacao);
        set("email", a.email);
        set("enderecoSigiloso", a.enderecoSigiloso);
        set("cep", a.cep ? String(a.cep).replace(/^(\d{5})(\d)/, "$1-$2") : "");
        set("endereco", a.endereco);
        set("enderecoNumero", a.enderecoNumero);
        set("bairro", a.bairro);
        set("cidade", a.cidade);
        set("uf", a.uf);
        set("curso", a.direcionamento);
        set("encaminharJuridico", a.encaminhadaJuridico);
        set("observacoes", a.observacoes);
        atualizarEstados(form);
    }

    function limpar(form) {
        form.reset();
        atualizarEstados(form);
    }

    function escapeHtml(value) {
        return String(value == null ? "" : value)
            .replaceAll("&", "&amp;")
            .replaceAll("<", "&lt;")
            .replaceAll(">", "&gt;")
            .replaceAll('"', "&quot;")
            .replaceAll("'", "&#039;");
    }

    function formatarData(iso) {
        if (!iso) return "-";
        const partes = String(iso).split("T")[0].split("-");
        return partes.length === 3 ? `${partes[2]}/${partes[1]}/${partes[0]}` : iso;
    }

    function formatarEndereco(a) {
        if (a.enderecoSigiloso) return "Endereço sigiloso (não informado)";
        if (!a.endereco) return "Não informado";
        let texto = a.endereco;
        if (a.enderecoNumero) texto += `, ${a.enderecoNumero}`;
        if (a.bairro) texto += ` - ${a.bairro}`;
        if (a.cidade) texto += `, ${a.cidade}${a.uf ? "/" + a.uf : ""}`;
        return texto;
    }

    function formatarContato(a) {
        if (a.semTelefoneSeguro) return "Sem telefone seguro";
        const tel = a.telefone || "-";
        const regra = ROTULOS_CONTATO[a.contatoSeguro];
        return a.contatoSeguro && a.contatoSeguro !== "pode_ligar" ? `${tel} (${regra})` : tel;
    }

    async function lerMensagemErro(response, padrao) {
        try {
            const data = await response.json();
            return data.mensagem || data.title || padrao;
        } catch {
            return padrao;
        }
    }

    window.CasaMulherAssistidaForm = {
        ROTULOS_DIRECIONAMENTO,
        ROTULOS_CONTATO,
        ROTULOS_MOTIVO_RESTRICAO,
        somenteDigitos,
        validarCPF,
        init,
        ler,
        validar,
        preencher,
        limpar,
        escapeHtml,
        formatarData,
        formatarEndereco,
        formatarContato,
        lerMensagemErro,
        formatarDirecionamento: function (v) { return ROTULOS_DIRECIONAMENTO[v] || v || "A definir"; }
    };
})();
