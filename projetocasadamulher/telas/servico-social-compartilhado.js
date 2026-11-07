/*
 * Parte do Serviço Social que aparece nas fichas do Jurídico e da Psicologia:
 * só os documentos que o Serviço Social abriu para a equipe técnica, e só para quem é
 * responsável pela assistida. Nada do conteúdo do prontuário social vem para cá.
 */
(function () {
    "use strict";

    const ROTULOS_ANEXO = { rg: "RG", cpf: "CPF", certidao: "Certidão", comprovante_residencia: "Comprovante de residência", comprovante_renda: "Comprovante de renda", nis_cadunico: "NIS / CadÚnico", boletim_ocorrencia: "Boletim de ocorrência", medida_protetiva: "Medida protetiva", laudo_relatorio: "Laudo / relatório", outro: "Outro" };

    function esc(v) {
        return String(v ?? "").replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[c]);
    }

    function tamanho(bytes) {
        return bytes >= 1024 * 1024 ? `${(bytes / 1024 / 1024).toFixed(1).replace(".", ",")} MB` : `${Math.max(1, Math.round(bytes / 1024))} KB`;
    }

    async function abrirArquivo(anexo, baixar) {
        const response = await CasaMulherAuth.apiFetch(`/api/anexos-sociais/${anexo.id}/arquivo${baixar ? "" : "?inline=true"}`);
        if (!response.ok) {
            alert("Você não tem acesso a este documento.");
            return;
        }

        const url = URL.createObjectURL(await response.blob());
        if (baixar) {
            const link = document.createElement("a");
            link.href = url;
            link.download = anexo.nomeArquivo;
            document.body.appendChild(link);
            link.click();
            link.remove();
        } else {
            window.open(url, "_blank", "noopener");
        }

        setTimeout(() => URL.revokeObjectURL(url), 60000);
    }

    /** Mostra o card só quando há documento compartilhado que a pessoa pode ver. */
    async function carregarDocumentos(assistidaId, cardId) {
        const card = document.getElementById(cardId);
        if (!card) return;
        card.classList.add("hidden");

        let response;
        try {
            response = await CasaMulherAuth.apiFetch(`/api/anexos-sociais/assistidas/${assistidaId}`);
        } catch {
            return;
        }

        if (!response.ok) return;
        const dados = await response.json();
        if (!dados.anexos.length) return;

        const lista = card.querySelector("[data-lista]");
        lista.innerHTML = dados.anexos.map((a, i) => `
            <div class="entity-info" style="display:flex; gap:10px; align-items:center; flex-wrap:wrap; margin-bottom:8px;">
                <strong>${esc(ROTULOS_ANEXO[a.tipo] || a.tipo)}</strong>
                <span>${esc(a.nomeArquivo)} · ${esc(tamanho(a.tamanhoBytes))} · ${esc(new Date(a.criadoEm).toLocaleDateString("pt-BR"))}</span>
                ${a.descricao ? `<small>${esc(a.descricao)}</small>` : ""}
                <button type="button" class="soft-btn soft-btn-warning" data-ver="${i}">Ver</button>
                <button type="button" class="soft-btn soft-btn-warning" data-baixar="${i}">Baixar</button>
            </div>`).join("");
        lista.onclick = (e) => {
            const ver = e.target.closest("[data-ver]");
            const baixar = e.target.closest("[data-baixar]");
            if (ver) abrirArquivo(dados.anexos[Number(ver.dataset.ver)], false);
            if (baixar) abrirArquivo(dados.anexos[Number(baixar.dataset.baixar)], true);
        };
        card.classList.remove("hidden");
    }

    window.CasaMulherServicoSocial = { carregarDocumentos };
})();
