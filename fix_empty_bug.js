const fs = require('fs');
let c = fs.readFileSync('projetocasadamulher/telas/equipe-ide.js', 'utf8');

c = c.replace(
`    function salvarRascunhoLocal() {
        // Puxa do editor o valor do arquivo atual para o objeto antes de salvar
        const file = rascunhoAtual.arquivoAtivo || 'index.html';
        if (editorInstance) {
            rascunhoAtual.arquivos[file] = getEditorValue();
        }`,
`    function salvarRascunhoLocal() {
        // Puxa do editor o valor do arquivo atual para o objeto antes de salvar
        if (rascunhoAtual.arquivoAtivo && rascunhoAtual.arquivoAtivo.trim() !== '') {
            if (editorInstance) {
                rascunhoAtual.arquivos[rascunhoAtual.arquivoAtivo] = getEditorValue();
            }
        }`);

c = c.replace(
`        if (rascunhoAtual.arquivos['undefined'] !== undefined) {
            delete rascunhoAtual.arquivos['undefined'];
        }
        if (rascunhoAtual.arquivos['null'] !== undefined) {
            delete rascunhoAtual.arquivos['null'];
        }`,
`        if (rascunhoAtual.arquivos['undefined'] !== undefined) delete rascunhoAtual.arquivos['undefined'];
        if (rascunhoAtual.arquivos['null'] !== undefined) delete rascunhoAtual.arquivos['null'];
        if (rascunhoAtual.arquivos[''] !== undefined) delete rascunhoAtual.arquivos[''];`);

c = c.replace(
`    function atualizarPreview() {
        if (!rascunhoAtual || !rascunhoAtual.arquivos) return;
        
        // Atualiza a memoria primeiro
        rascunhoAtual.arquivos[rascunhoAtual.arquivoAtivo] = getEditorValue();`,
`    function atualizarPreview() {
        if (!rascunhoAtual || !rascunhoAtual.arquivos) return;
        
        // Atualiza a memoria primeiro
        if (rascunhoAtual.arquivoAtivo && rascunhoAtual.arquivoAtivo.trim() !== '') {
            rascunhoAtual.arquivos[rascunhoAtual.arquivoAtivo] = getEditorValue();
        }`);

c = c.replace(
`                if (statusBarLang) statusBarLang.textContent = '-';
                renderizarArvoreArquivos();
                renderizarAbas();`,
`                if (statusBarLang) statusBarLang.textContent = '-';
                if (rascunhoAtual.arquivos[''] !== undefined) delete rascunhoAtual.arquivos[''];
                renderizarArvoreArquivos();
                renderizarAbas();`);

c = c.replace(
`                if (isInvalid) {
                    console.warn("Rascunho antigo inválido ou corrompido, carregando vazio.");
                    localStorage.removeItem(DRAFT_KEY);
                } else {
                    rascunhoAtual = salvo;
                    
                    // Fallbacks extras`,
`                if (isInvalid) {
                    console.warn("Rascunho antigo inválido ou corrompido, carregando vazio.");
                    localStorage.removeItem(DRAFT_KEY);
                } else {
                    rascunhoAtual = salvo;
                    if (rascunhoAtual.arquivos && rascunhoAtual.arquivos[''] !== undefined) {
                        delete rascunhoAtual.arquivos[''];
                    }
                    
                    // Fallbacks extras`);

c = c.replace(
`    function renderizarArvoreArquivos() {`,
`    function getIconForFile(path) {
        if (path.endsWith('.html')) return '<svg viewBox="0 0 384 512" width="14" height="14" fill="#e34c26"><path d="M0 32l34.9 395.8L191.5 480l157.6-52.2L384 32H0zm308.2 127.9H124.4l4.1 49.4h175.6l-13.6 148.4-97.9 27v.3h-1.1l-98.7-27.3-6-75.8h47.7L138 320l53.5 14.5 53.7-14.5 6-62.2H84.3L71.5 112.2h241.1l-4.4 47.7z"/></svg>';
        if (path.endsWith('.css')) return '<svg viewBox="0 0 384 512" width="14" height="14" fill="#264de4"><path d="M0 32l34.9 395.8L192 480l157.1-52.2L384 32H0zm308.2 127.9H124.4l4.1 49.4h175.6l-13.6 148.4-97.9 27v.3h-1.1l-98.7-27.3-6-75.8h47.7L138 320l53.5 14.5 53.7-14.5 6-62.2H84.3L71.5 112.2h241.1l-4.4 47.7z"/></svg>';
        if (path.endsWith('.js')) return '<svg viewBox="0 0 448 512" width="14" height="14" fill="#f0db4f"><path d="M0 32v448h448V32H0zm243.8 349.4c0 43.6-25.6 63.5-62.9 63.5-33.7 0-53.2-17.4-63.2-38.5l34.3-20.7c6.6 11.7 12.6 21.6 27.1 21.6 13.8 0 22.6-5.4 22.6-26.5V237.7h42.1v143.7zm99.6 63.5c-39.1 0-64.4-18.6-76.7-43l34.3-19.8c9 14.7 20.8 25.6 41.5 25.6 17.4 0 28.6-8.7 28.6-20.8 0-14.4-11.4-19.5-30.7-28l-10.5-4.5c-30.4-12.9-50.5-29.2-50.5-63.5 0-31.6 24.1-55.6 61.6-55.6 26.8 0 46 9.3 59.8 33.7L368 290c-7.2-12.9-15-18-27.1-18-12.3 0-20.1 7.8-20.1 18 0 12.6 7.8 17.7 25.9 25.6l10.5 4.5c35.8 15.3 55.9 31 55.9 66.2 0 37.8-29.8 58.6-69.7 58.6z"/></svg>';
        if (path.endsWith('.json')) return '<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="#cb3837" stroke-width="2"><ellipse cx="12" cy="5" rx="9" ry="3"></ellipse><path d="M21 12c0 1.66-4 3-9 3s-9-1.34-9-3"></path><path d="M3 5v14c0 1.66 4 3 9 3s9-1.34 9-3V5"></path></svg>';
        return '<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="#ccc" stroke-width="2"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"></path><polyline points="14 2 14 8 20 8"></polyline></svg>';
    }

    function renderizarArvoreArquivos() {`);

const oldColorLogic1 = `            let color = '#ccc';
            if (path.endsWith('.html')) color = '#e34c26';
            else if (path.endsWith('.css')) color = '#264de4';
            else if (path.endsWith('.js')) color = '#f0db4f';
            else if (path.endsWith('.cs')) color = '#178600';
            else if (path.endsWith('.json')) color = '#cb3837';
            else if (path.endsWith('.md')) color = '#fff';
            
            const btnName = document.createElement('span');
            btnName.style.display = 'flex';
            btnName.style.alignItems = 'center';
            btnName.style.gap = '6px';
            btnName.style.cursor = 'pointer';
            btnName.style.flex = '1';
            btnName.style.overflow = 'hidden';
            btnName.style.textOverflow = 'ellipsis';
            btnName.style.whiteSpace = 'nowrap';
            btnName.innerHTML = \\\`<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="\${color}" stroke-width="2"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"></path><polyline points="14 2 14 8 20 8"></polyline></svg> <span style="overflow:hidden; text-overflow:ellipsis;">\${path}</span>\\\`;`;

const newColorLogic1 = `            const btnName = document.createElement('span');
            btnName.style.display = 'flex';
            btnName.style.alignItems = 'center';
            btnName.style.gap = '6px';
            btnName.style.cursor = 'pointer';
            btnName.style.flex = '1';
            btnName.style.overflow = 'hidden';
            btnName.style.textOverflow = 'ellipsis';
            btnName.style.whiteSpace = 'nowrap';
            btnName.innerHTML = \\\`\${getIconForFile(path)} <span style="overflow:hidden; text-overflow:ellipsis;">\${path}</span>\\\`;`;

c = c.replace(oldColorLogic1, newColorLogic1);

const oldColorLogic2 = `            let color = '#ccc';
            if (path.endsWith('.html')) color = '#e34c26';
            else if (path.endsWith('.css')) color = '#264de4';
            else if (path.endsWith('.js')) color = '#f0db4f';
            else if (path.endsWith('.cs')) color = '#178600';
            else if (path.endsWith('.json')) color = '#cb3837';
            else if (path.endsWith('.md')) color = '#fff';
            
            const btnName = document.createElement('span');
            btnName.style.cursor = 'pointer';
            btnName.style.display = 'flex';
            btnName.style.alignItems = 'center';
            btnName.style.gap = '6px';
            btnName.innerHTML = \\\`<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="\${color}" stroke-width="2"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"></path><polyline points="14 2 14 8 20 8"></polyline></svg> <span style="white-space:nowrap; overflow:hidden; text-overflow:ellipsis; max-width: 150px;">\${path}</span>\\\`;`;

const newColorLogic2 = `            const btnName = document.createElement('span');
            btnName.style.cursor = 'pointer';
            btnName.style.display = 'flex';
            btnName.style.alignItems = 'center';
            btnName.style.gap = '6px';
            btnName.innerHTML = \\\`\${getIconForFile(path)} <span style="white-space:nowrap; overflow:hidden; text-overflow:ellipsis; max-width: 150px;">\${path}</span>\\\`;`;

c = c.replace(oldColorLogic2, newColorLogic2);

fs.writeFileSync('projetocasadamulher/telas/equipe-ide.js', c);
console.log('Done fixing JS');
