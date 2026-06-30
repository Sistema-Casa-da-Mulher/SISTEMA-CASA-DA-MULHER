const fs = require('fs');
let c = fs.readFileSync('projetocasadamulher/telas/equipe-ide.js', 'utf8');

// 1. Add Helper functions at the top (after consts)
const helpers = `
function caminhoWorkspaceValido(caminho) {
  if (typeof caminho !== "string") return false;
  const valor = caminho.trim();
  if (!valor) return false;
  if (valor === ".") return false;
  if (valor.includes("../")) return false;
  if (valor.includes("..\\\\")) return false;
  if (valor.startsWith("/")) return false;
  if (valor.startsWith("\\\\")) return false;
  if (/^[a-zA-Z]:\\\\/.test(valor)) return false;
  return true;
}

function normalizarEstadoWorkspaceIde(rascunho) {
  if (!rascunho) return rascunho;

  rascunho.arquivos = rascunho.arquivos || {};
  rascunho.arquivosBase = rascunho.arquivosBase || {};
  rascunho.pastas = Array.isArray(rascunho.pastas) ? rascunho.pastas : [];
  rascunho.abasAbertas = Array.isArray(rascunho.abasAbertas) ? rascunho.abasAbertas : [];

  rascunho.arquivos = Object.fromEntries(
    Object.entries(rascunho.arquivos).filter(([caminho]) => caminhoWorkspaceValido(caminho))
  );

  rascunho.arquivosBase = Object.fromEntries(
    Object.entries(rascunho.arquivosBase).filter(([caminho]) => caminhoWorkspaceValido(caminho))
  );

  rascunho.pastas = rascunho.pastas.filter(caminhoWorkspaceValido);
  rascunho.abasAbertas = rascunho.abasAbertas.filter((caminho) => caminhoWorkspaceValido(caminho) && rascunho.arquivos[caminho] !== undefined);

  if (!caminhoWorkspaceValido(rascunho.arquivoAtivo) || rascunho.arquivos[rascunho.arquivoAtivo] === undefined) {
    rascunho.arquivoAtivo = rascunho.abasAbertas[0] || Object.keys(rascunho.arquivos)[0] || null;
  }

  if (rascunho.arquivoAtivo && !rascunho.abasAbertas.includes(rascunho.arquivoAtivo)) {
    rascunho.abasAbertas.unshift(rascunho.arquivoAtivo);
  }

  return rascunho;
}

function obterIconeArquivoIde(caminho, opcoes = {}) {
  if (opcoes.pasta) {
    return opcoes.aberta ? "vscode-icons:default-folder-opened" : "vscode-icons:default-folder";
  }
  const nome = String(caminho || "").split("/").pop().toLowerCase();
  const ext = nome.includes(".") ? nome.split(".").pop() : "";

  const porNome = {
    "readme.md": "vscode-icons:file-type-readme",
    "appsettings.json": "vscode-icons:file-type-config",
    "appsettings.development.json": "vscode-icons:file-type-config"
  };

  const porExtensao = {
    html: "vscode-icons:file-type-html",
    css: "vscode-icons:file-type-css",
    js: "vscode-icons:file-type-js",
    json: "vscode-icons:file-type-json",
    md: "vscode-icons:file-type-markdown",
    txt: "vscode-icons:default-file",
    cs: "vscode-icons:file-type-csharp",
    cshtml: "vscode-icons:file-type-razor"
  };

  return porNome[nome] || porExtensao[ext] || "vscode-icons:default-file";
}
`;

c = c.replace('let rascunhoAtual = null;', 'let rascunhoAtual = null;\n' + helpers);

// 2. Normalizar loading (regex ajustado para garantir match seguro)
c = c.replace(/rascunhoAtual = salvo;\s+if \(rascunhoAtual\.arquivos && rascunhoAtual\.arquivos\[''\] !== undefined\) \{\s+delete rascunhoAtual\.arquivos\[''\];\s+\}\s+\/\/ Fallbacks extras\s+if \(!rascunhoAtual\.abasAbertas\)/, 
`rascunhoAtual = normalizarEstadoWorkspaceIde(salvo);
                    if (!rascunhoAtual.abasAbertas)`);

// 3. Normalizar atualizarPreview and salvarRascunhoLocal
c = c.replace(
`    function atualizarPreview() {
        if (!rascunhoAtual || !rascunhoAtual.arquivos) return;
        
        // Atualiza a memoria primeiro
        if (rascunhoAtual.arquivoAtivo && rascunhoAtual.arquivoAtivo.trim() !== '') {
            rascunhoAtual.arquivos[rascunhoAtual.arquivoAtivo] = getEditorValue();
        }`,
`    function atualizarPreview() {
        if (!rascunhoAtual || !rascunhoAtual.arquivos) return;
        const caminho = rascunhoAtual.arquivoAtivo;
        if (caminhoWorkspaceValido(caminho) && rascunhoAtual.arquivos[caminho] !== undefined) {
            rascunhoAtual.arquivos[caminho] = getEditorValue();
        }`);

c = c.replace(
`    function salvarRascunhoLocal() {
        // Puxa do editor o valor do arquivo atual para o objeto antes de salvar
        if (rascunhoAtual.arquivoAtivo && rascunhoAtual.arquivoAtivo.trim() !== '') {
            if (editorInstance) {
                rascunhoAtual.arquivos[rascunhoAtual.arquivoAtivo] = getEditorValue();
            }
        }`,
`    function salvarRascunhoLocal() {
        if (!rascunhoAtual) return;
        const caminho = rascunhoAtual.arquivoAtivo;
        if (caminhoWorkspaceValido(caminho) && rascunhoAtual.arquivos[caminho] !== undefined && editorInstance) {
            rascunhoAtual.arquivos[caminho] = getEditorValue();
        }
        rascunhoAtual = normalizarEstadoWorkspaceIde(rascunhoAtual);`);

// 4. Update Icons (remove getIconForFile)
c = c.replace(/function getIconForFile\(path\) \{[\s\S]*?return '<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="#ccc" stroke-width="2"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"><\/path><polyline points="14 2 14 8 20 8"><\/polyline><\/svg>';\s+\}/, '');

// Tree rendering:
c = c.replace(/<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" style="margin-right:6px;"><path d="M22 19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2z"><\/path><\/svg> \$\{pasta\}\//g, 
              `<iconify-icon class="ide-file-icon" icon="\${obterIconeArquivoIde(pasta, {pasta:true})}" aria-hidden="true" style="margin-right:6px; font-size:16px; transform:translateY(2px);"></iconify-icon> \${pasta}/`);

c = c.replace(/\$\{getIconForFile\(path\)\}/g, `<iconify-icon class="ide-file-icon" icon="\${obterIconeArquivoIde(path)}" aria-hidden="true" style="font-size:16px; margin-top:2px;"></iconify-icon>`);

// 5. fecharAba needs null instead of ''
c = c.replace(/rascunhoAtual\.arquivoAtivo = '';/g, "rascunhoAtual.arquivoAtivo = null;");

// 6. Fix Workspace Search
const searchLogic = `
    const btnTabExplorer = document.getElementById('btnTabExplorer');
    const btnTabSearch = document.getElementById('btnTabSearch');
    const panelExplorer = document.getElementById('panelExplorer');
    const panelSearch = document.getElementById('panelSearch');
    const ideSearchInput = document.getElementById('ideSearchInput');
    const ideSearchResults = document.getElementById('ideSearchResults');

    if (btnTabExplorer && btnTabSearch && panelExplorer && panelSearch) {
        btnTabExplorer.addEventListener('click', () => {
            btnTabExplorer.classList.add('active');
            btnTabSearch.classList.remove('active');
            panelExplorer.classList.remove('hidden');
            panelExplorer.classList.add('active');
            panelSearch.classList.add('hidden');
            panelSearch.classList.remove('active');
        });
        
        btnTabSearch.addEventListener('click', () => {
            btnTabSearch.classList.add('active');
            btnTabExplorer.classList.remove('active');
            panelSearch.classList.remove('hidden');
            panelSearch.classList.add('active');
            panelExplorer.classList.add('hidden');
            panelExplorer.classList.remove('active');
            if (ideSearchInput) ideSearchInput.focus();
        });
    }

    if (ideSearchInput) {
        ideSearchInput.addEventListener('keydown', (e) => {
            if (e.key === 'Enter') {
                const term = ideSearchInput.value.trim().toLowerCase();
                if (!term) {
                    ideSearchResults.innerHTML = '<div style="color:var(--ide-text-dimmed); font-size:12px; padding: 8px;">Digite algo para buscar.</div>';
                    return;
                }
                
                ideSearchResults.innerHTML = '';
                let foundAny = false;
                
                Object.keys(rascunhoAtual.arquivos).forEach(path => {
                    const content = (rascunhoAtual.arquivos[path] || '').toLowerCase();
                    const pathLower = path.toLowerCase();
                    
                    if (pathLower.includes(term) || content.includes(term)) {
                        foundAny = true;
                        
                        const resDiv = document.createElement('div');
                        resDiv.style.padding = '6px 8px';
                        resDiv.style.cursor = 'pointer';
                        resDiv.style.borderBottom = '1px solid var(--ide-border)';
                        resDiv.innerHTML = \`<div style="display:flex; align-items:center; gap:6px; font-weight:600;"><iconify-icon icon="\${obterIconeArquivoIde(path)}"></iconify-icon> \${path}</div>\`;
                        
                        if (content.includes(term)) {
                            const idx = content.indexOf(term);
                            const snippet = (rascunhoAtual.arquivos[path] || '').substring(Math.max(0, idx - 15), idx + term.length + 15);
                            const snipDiv = document.createElement('div');
                            snipDiv.style.fontSize = '11px';
                            snipDiv.style.opacity = '0.7';
                            snipDiv.style.marginTop = '4px';
                            snipDiv.style.whiteSpace = 'nowrap';
                            snipDiv.style.overflow = 'hidden';
                            snipDiv.style.textOverflow = 'ellipsis';
                            snipDiv.textContent = '...' + snippet.replace(/\\n/g, ' ') + '...';
                            resDiv.appendChild(snipDiv);
                        }
                        
                        resDiv.onclick = () => {
                            abrirArquivo(path);
                        };
                        
                        ideSearchResults.appendChild(resDiv);
                    }
                });
                
                if (!foundAny) {
                    ideSearchResults.innerHTML = '<div style="color:var(--ide-text-dimmed); font-size:12px; padding: 8px;">Nenhum resultado encontrado.</div>';
                }
            }
        });
    }
`;

c = c.replace(/const btnIdeSearch = document.getElementById\('btnIdeSearch'\);[\s\S]*?\}\);\n    \}/, searchLogic);

fs.writeFileSync('projetocasadamulher/telas/equipe-ide.js', c);
