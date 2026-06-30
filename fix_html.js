const fs = require('fs');
let c = fs.readFileSync('projetocasadamulher/telas/equipe-ide.html', 'utf8');

c = c.replace(
`    <!-- Lógica IDE -->
    <script src="equipe-ide-validacoes.js?v=4" defer></script>
    <script src="equipe-ide.js?v=4" defer></script>`,
`    <!-- Iconify (Ícones Oficiais) -->
    <script src="https://code.iconify.design/iconify-icon/3.0.2/iconify-icon.min.js" defer></script>

    <!-- Lógica IDE -->
    <script src="equipe-ide-validacoes.js?v=7" defer></script>
    <script src="equipe-ide.js?v=7" defer></script>`);

c = c.replace(
`                <button class="ide-activity-icon" id="btnIdeSearch" title="Buscar no Arquivo (Ctrl+F)" aria-label="Buscar">
                    <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="11" cy="11" r="8"></circle><line x1="21" y1="21" x2="16.65" y2="16.65"></line></svg>
                </button>`,
`                <button class="ide-activity-icon" id="btnTabSearch" title="Buscar no Workspace" aria-label="Buscar">
                    <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="11" cy="11" r="8"></circle><line x1="21" y1="21" x2="16.65" y2="16.65"></line></svg>
                </button>`);

c = c.replace(
`            <!-- SIDEBAR EXPLORER E MAPA -->
            <aside class="ide-sidebar">
                
                <!-- PAINEL EXPLORADOR -->`,
`            <!-- SIDEBAR EXPLORER E MAPA -->
            <aside class="ide-sidebar">
                
                <!-- PAINEL DE BUSCA -->
                <div id="panelSearch" class="ide-sidebar-panel hidden">
                    <div class="ide-sidebar-header">Busca no Workspace</div>
                    <div class="ide-sidebar-content" style="padding: 12px 8px;">
                        <input type="text" id="ideSearchInput" class="ide-input" placeholder="Buscar texto (Enter)..." style="width: 100%; margin-bottom: 8px;">
                        <div id="ideSearchResults" class="ide-file-tree">
                            <div style="color:var(--ide-text-dimmed); font-size:12px; padding: 8px;">Digite algo para buscar em todos os arquivos.</div>
                        </div>
                    </div>
                </div>

                <!-- PAINEL EXPLORADOR -->`);

fs.writeFileSync('projetocasadamulher/telas/equipe-ide.html', c);
console.log('Fixed HTML');
