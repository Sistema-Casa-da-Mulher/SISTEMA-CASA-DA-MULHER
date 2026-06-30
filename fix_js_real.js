const fs = require('fs');
let c = fs.readFileSync('projetocasadamulher/telas/equipe-ide.js', 'utf8');

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

// Insert helpers before `let rascunhoAtual = {`
c = c.replace(/    let rascunhoAtual = \{/, helpers + '\n    let rascunhoAtual = {');

// Fix carregarRascunhoSalvo
c = c.replace(
`                if (isInvalid) {
                    console.warn("Rascunho antigo inválido ou corrompido, carregando vazio.");
                    localStorage.removeItem(DRAFT_KEY);
                } else {
                    rascunhoAtual = salvo;
                    
                    // Fallbacks extras
                    if (!rascunhoAtual.arquivosBase) rascunhoAtual.arquivosBase = JSON.parse(JSON.stringify(rascunhoAtual.arquivos));
                    if (!rascunhoAtual.pastas) rascunhoAtual.pastas = [];
                    if (!rascunhoAtual.abasAbertas) rascunhoAtual.abasAbertas = ["index.html", "style.css", "script.js"];
                    if (!rascunhoAtual.arquivoAtivo) rascunhoAtual.arquivoAtivo = 'index.html';
                    if (!rascunhoAtual.tarefa) rascunhoAtual.tarefa = TAREFA_PADRAO;`,
`                if (isInvalid) {
                    console.warn("Rascunho antigo inválido ou corrompido, carregando vazio.");
                    localStorage.removeItem(DRAFT_KEY);
                    // Garante a UI mesmo com vazio
                    renderizarArvoreArquivos();
                    renderizarAbas();
                } else {
                    rascunhoAtual = normalizarEstadoWorkspaceIde(salvo);`);

fs.writeFileSync('projetocasadamulher/telas/equipe-ide.js', c);
console.log('Fixed JS!');
