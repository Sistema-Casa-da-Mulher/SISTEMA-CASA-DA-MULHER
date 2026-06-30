document.addEventListener('DOMContentLoaded', () => {
    if (typeof CasaMulherAuth !== 'undefined' && !CasaMulherAuth.estaLogado()) {
        window.location.href = 'equipe-login.html';
        return;
    }

    // DOM Elements
    const segments = document.querySelectorAll('.segment');
    const tabContents = document.querySelectorAll('.tab-content');
    const dropZone = document.getElementById('dropZone');
    const fileInput = document.getElementById('fileInput');
    const fileSelectedCard = document.getElementById('fileSelectedCard');
    const fileNameDisplay = document.getElementById('fileNameDisplay');
    const fileSizeDisplay = document.getElementById('fileSizeDisplay');
    const btnRemoveFile = document.getElementById('btnRemoveFile');
    
    // Views
    const viewOrigem = document.getElementById('viewOrigem');
    const viewAnalise = document.getElementById('viewAnalise');
    const viewLoading = document.getElementById('viewLoading');
    const viewSuccess = document.getElementById('viewSuccess');
    const viewError = document.getElementById('viewError');

    // Action Buttons
    const btnAnalisar = document.getElementById('btnAnalisar');
    const btnLimpar = document.getElementById('btnLimpar');
    const btnCriarPr = document.getElementById('btnCriarPr');
    const btnVoltarAnalise = document.getElementById('btnVoltarAnalise');
    const btnTentarNovamente = document.getElementById('btnTentarNovamente');
    const btnNovoEnvio = document.getElementById('btnNovoEnvio');
    const actionsFooter = document.getElementById('mainActionsFooter'); // Action footer global for analisar
    
    // Storage DOM Elements
    const storageList = document.getElementById('storageList');
    const storageForm = document.getElementById('storageForm');
    const myDraftsContainer = document.getElementById('myDraftsContainer');
    const sharedDraftsContainer = document.getElementById('sharedDraftsContainer');
    const btnNovoRascunho = document.getElementById('btnNovoRascunho');
    const btnSalvarStorage = document.getElementById('btnSalvarStorage');
    const btnCancelarStorage = document.getElementById('btnCancelarStorage');
    const draftDropzone = document.getElementById('draftDropzone');
    const draftFileInput = document.getElementById('draftFileInput');
    const btnSelectDraftZip = document.getElementById('btnSelectDraftZip');
    const draftSelectedFileContainer = document.getElementById('draftSelectedFileContainer');
    const draftFileNameDisplay = document.getElementById('draftFileNameDisplay');
    const draftFileSizeDisplay = document.getElementById('draftFileSizeDisplay');
    const btnRemoveDraftFile = document.getElementById('btnRemoveDraftFile');
    
    let draftSelectedFile = null;
    
    let currentMode = 'upload'; // 'upload' ou 'branch'
    let selectedFile = null;

    // --- Stepper Logic ---
    function setStep(stepNum) {
        document.querySelectorAll('.step').forEach((el, index) => {
            const step = index + 1;
            el.classList.remove('active', 'completed');
            if (step < stepNum) {
                el.classList.add('completed');
            } else if (step === stepNum) {
                el.classList.add('active');
            }
        });
    }

    // --- View Navigation ---
    function showView(view) {
        viewOrigem.style.display = 'none';
        viewAnalise.style.display = 'none';
        viewLoading.style.display = 'none';
        viewSuccess.style.display = 'none';
        viewError.style.display = 'none';
        view.style.display = 'block';
    }

    function showLoading(text, step) {
        setStep(step);
        document.getElementById('loadingText').textContent = text;
        showView(viewLoading);
    }

    function showError(text) {
        document.getElementById('errorText').textContent = text;
        showView(viewError);
    }

    // --- Segmented Control ---
    segments.forEach(segment => {
        segment.addEventListener('click', () => {
            segments.forEach(s => s.classList.remove('active'));
            segment.classList.add('active');
            
            const target = segment.getAttribute('data-target');
            tabContents.forEach(content => {
                if(content.id === target) {
                    content.classList.add('active');
                } else {
                    content.classList.remove('active');
                }
            });

            currentMode = target.replace('-mode', '');
            
            // Esconde actions-footer de "Analisar" caso esteja no storage
            if (currentMode === 'storage') {
                if (actionsFooter) actionsFooter.style.display = 'none';
                carregarRascunhos();
            } else {
                if (actionsFooter) actionsFooter.style.display = 'flex';
            }
        });
    });

    // --- Query params check ---
    const urlParams = new URLSearchParams(window.location.search);
    if (urlParams.get('aba') === 'storage') {
        const storageSegment = document.querySelector('[data-target="storage-mode"]');
        if (storageSegment) storageSegment.click();
    }

    // Update download link
    document.getElementById('btnDownloadBase').href = `${window.API_BASE_URL}/api/equipe-pr/base/download`;

    // --- Drag and Drop ---
    dropZone.addEventListener('click', () => fileInput.click());
    dropZone.addEventListener('dragover', (e) => { e.preventDefault(); dropZone.classList.add('dragover'); });
    dropZone.addEventListener('dragleave', () => dropZone.classList.remove('dragover'));
    dropZone.addEventListener('drop', (e) => {
        e.preventDefault();
        dropZone.classList.remove('dragover');
        if (e.dataTransfer.files.length > 0) handleFileSelection(e.dataTransfer.files[0]);
    });
    fileInput.addEventListener('change', (e) => {
        if (e.target.files.length > 0) handleFileSelection(e.target.files[0]);
    });

    function handleFileSelection(file) {
        if (!file.name.toLowerCase().endsWith('.zip')) {
            alert('Por favor, selecione um arquivo .ZIP');
            return;
        }
        selectedFile = file;
        fileNameDisplay.textContent = file.name;
        
        let sizeText = '';
        if (file.size < 1024 * 1024) {
            sizeText = `${(file.size / 1024).toFixed(1)} KB`;
        } else {
            sizeText = `${(file.size / 1024 / 1024).toFixed(2)} MB`;
        }
        fileSizeDisplay.textContent = `Tamanho: ${sizeText}`;
        
        dropZone.style.display = 'none';
        fileSelectedCard.style.display = 'flex';
    }

    btnRemoveFile.addEventListener('click', () => {
        selectedFile = null;
        fileInput.value = '';
        dropZone.style.display = 'block';
        fileSelectedCard.style.display = 'none';
    });

    // --- BotÃ£o Limpar ---
    btnLimpar.addEventListener('click', () => {
        btnRemoveFile.click();
        document.getElementById('branchRepoUrl').value = '';
        document.getElementById('branchName').value = '';
    });

    btnTentarNovamente.addEventListener('click', () => {
        setStep(1);
        showView(viewOrigem);
    });

    btnVoltarAnalise.addEventListener('click', () => {
        setStep(1);
        showView(viewOrigem);
    });

    btnNovoEnvio.addEventListener('click', () => {
        btnLimpar.click();
        document.getElementById('prTitulo').value = '';
        document.getElementById('prDescricao').value = '';
        document.getElementById('chkRevisado').checked = false;
        document.getElementById('chkSegredos').checked = false;
        document.getElementById('chkForaPrototipo').checked = false;
        setStep(1);
        showView(viewOrigem);
    });

    // --- Analisar ---
    btnAnalisar.addEventListener('click', async () => {
        try {
            showLoading('Analisando arquivos e comparando com a main...', 2);
            
            let result;
            if (currentMode === 'upload') {
                if (!selectedFile) throw new Error("Selecione um arquivo ZIP primeiro.");
                const formData = new FormData();
                formData.append('ArquivoZip', selectedFile);
                
                const response = await CasaMulherAuth.apiFetch('/api/equipe-pr/analisar-upload', {
                    method: 'POST',
                    body: formData
                });
                if (!response.ok) {
                    const text = await response.text();
                    if (!text) throw new Error(`Erro HTTP ${response.status} (Sem detalhes. Verifique se a branch existe ou se vocÃª tem permissÃ£o).`);
                    try {
                        const json = JSON.parse(text);
                        throw new Error(json.mensagem || json.title || text);
                    } catch (e) {
                        if (e.name === 'SyntaxError') throw new Error(text || `Erro HTTP ${response.status}`);
                        throw e;
                    }
                }
                result = await response.json();
            } else {
                const repoUrl = document.getElementById('branchRepoUrl').value;
                const branchName = document.getElementById('branchName').value;
                if (!repoUrl || !branchName) throw new Error("Preencha a URL do repositÃ³rio e o nome da branch.");

                const response = await CasaMulherAuth.apiFetch('/api/equipe-pr/analisar-branch', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ RepositorioUrl: repoUrl, Branch: branchName })
                });
                if (!response.ok) {
                    const text = await response.text();
                    if (!text) throw new Error(`Erro HTTP ${response.status} (Sem detalhes. Verifique se a branch existe ou se vocÃª tem permissÃ£o).`);
                    try {
                        const json = JSON.parse(text);
                        throw new Error(json.mensagem || json.title || text);
                    } catch (e) {
                        if (e.name === 'SyntaxError') throw new Error(text || `Erro HTTP ${response.status}`);
                        throw e;
                    }
                }
                result = await response.json();
            }
            renderAnalysis(result);
        } catch (error) {
            showError(error.message || 'Erro desconhecido de conexÃ£o (CORS ou Servidor fora do ar).');
        }
    });

    function renderAnalysis(result) {
        setStep(3); // RevisÃ£o
        
        // Atualiza Cards de Stats
        document.getElementById('valNovos').textContent = result.totalNovos;
        document.getElementById('valMod').textContent = result.totalModificados;
        document.getElementById('valRem').textContent = result.totalRemovidos;
        document.getElementById('valIgn').textContent = result.arquivos.filter(x => x.status === 'Ignorado' || x.status === 'Identico').length;
        document.getElementById('valBlk').textContent = result.totalBloqueados;
        document.getElementById('valFora').textContent = result.totalForaPrototipo;

        // Renderiza listas
        const grpPrototipo = document.getElementById('fileGroupPrototipo');
        const listPrototipo = document.getElementById('listPrototipo');
        const grpFora = document.getElementById('fileGroupFora');
        const listFora = document.getElementById('listFora');
        const grpBlk = document.getElementById('fileGroupBlk');
        const listBlk = document.getElementById('listBlk');

        listPrototipo.innerHTML = '';
        listFora.innerHTML = '';
        listBlk.innerHTML = '';

        let countPrototipo = 0;
        let countFora = 0;
        let countBlk = 0;

        result.arquivos.forEach(f => {
            if (f.status === 'Ignorado' || f.status === 'Identico') return;

            const div = document.createElement('div');
            div.className = 'file-item';
            
            const badge = document.createElement('span');
            badge.className = 'badge';
            if (f.status === 'Novo') { badge.style.backgroundColor = '#d4edda'; badge.style.color = '#155724'; }
            else if (f.status === 'Modificado') { badge.style.backgroundColor = '#cce5ff'; badge.style.color = '#004085'; }
            else if (f.status === 'Removido') { badge.style.backgroundColor = '#fff3cd'; badge.style.color = '#856404'; }
            else if (f.status === 'Bloqueado') { badge.className = 'badge badge-danger'; }
            
            badge.textContent = f.status.toUpperCase();
            
            const path = document.createElement('span');
            path.textContent = f.caminho;

            div.appendChild(badge);
            div.appendChild(path);

            if (f.status === 'Bloqueado') {
                const motive = document.createElement('span');
                motive.style.color = 'var(--cm-danger)';
                motive.textContent = `(${f.motivoBloqueio})`;
                div.appendChild(motive);
                listBlk.appendChild(div);
                countBlk++;
            } else if (f.emPrototipo) {
                listPrototipo.appendChild(div);
                countPrototipo++;
            } else {
                listFora.appendChild(div);
                countFora++;
            }
        });

        grpPrototipo.style.display = countPrototipo > 0 ? 'block' : 'none';
        grpFora.style.display = countFora > 0 ? 'block' : 'none';
        grpBlk.style.display = countBlk > 0 ? 'block' : 'none';

        // LÃ³gica de Checklist e Bloqueio
        const boxFora = document.getElementById('boxForaPrototipo');
        const chkFora = document.getElementById('chkForaPrototipo');

        if (countFora > 0) {
            boxFora.style.display = 'flex';
        } else {
            boxFora.style.display = 'none';
            chkFora.checked = false;
        }

        if (countBlk > 0 || (!result.validoParaEnvio && countBlk === 0)) {
            btnCriarPr.disabled = true;
            btnCriarPr.title = 'Existem bloqueios ou nÃ£o hÃ¡ alteraÃ§Ãµes vÃ¡lidas';
        } else {
            btnCriarPr.disabled = false;
            btnCriarPr.title = '';
        }

        showView(viewAnalise);
    }

    // --- Submit PR ---
    btnCriarPr.addEventListener('click', async () => {
        const titulo = document.getElementById('prTitulo').value;
        const descricao = document.getElementById('prDescricao').value;
        const chkRevisado = document.getElementById('chkRevisado').checked;
        const chkSegredos = document.getElementById('chkSegredos').checked;
        const chkFora = document.getElementById('chkForaPrototipo').checked;

        if (!titulo || !chkRevisado || !chkSegredos) {
            alert('Preencha o tÃ­tulo e marque os itens obrigatÃ³rios da revisÃ£o.');
            return;
        }

        const boxFora = document.getElementById('boxForaPrototipo');
        if (boxFora.style.display === 'flex' && !chkFora) {
            alert('VocÃª precisa confirmar a caixa extra sobre arquivos fora de protÃ³tipos.');
            return;
        }

        try {
            showLoading('Criando fork, branch, commit e Pull Request...', 4);

            let result;
            if (currentMode === 'upload') {
                const formData = new FormData();
                formData.append('ArquivoZip', selectedFile);
                formData.append('Titulo', titulo);
                formData.append('Descricao', descricao);
                formData.append('ConfirmouSemSegredos', chkSegredos);
                formData.append('ConfirmouRevisaoArquivos', chkRevisado);
                formData.append('ConfirmouRevisaoExtraForaPrototipos', chkFora);

                const response = await CasaMulherAuth.apiFetch('/api/equipe-pr/criar-upload', {
                    method: 'POST',
                    body: formData
                });
                if (!response.ok) {
                    const text = await response.text();
                    if (!text) throw new Error(`Erro HTTP ${response.status} (Sem detalhes. Verifique se a branch existe ou se vocÃª tem permissÃ£o).`);
                    try {
                        const json = JSON.parse(text);
                        throw new Error(json.mensagem || json.title || text);
                    } catch (e) {
                        if (e.name === 'SyntaxError') throw new Error(text || `Erro HTTP ${response.status}`);
                        throw e;
                    }
                }
                result = await response.json();
            } else {
                const payload = {
                    RepositorioUrl: document.getElementById('branchRepoUrl').value,
                    Branch: document.getElementById('branchName').value,
                    Titulo: titulo,
                    Descricao: descricao,
                    ConfirmouSemSegredos: chkSegredos,
                    ConfirmouRevisaoArquivos: chkRevisado,
                    ConfirmouRevisaoExtraForaPrototipos: chkFora
                };

                const response = await CasaMulherAuth.apiFetch('/api/equipe-pr/criar-branch', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(payload)
                });
                if (!response.ok) {
                    const text = await response.text();
                    if (!text) throw new Error(`Erro HTTP ${response.status} (Sem detalhes. Verifique se a branch existe ou se vocÃª tem permissÃ£o).`);
                    try {
                        const json = JSON.parse(text);
                        throw new Error(json.mensagem || json.title || text);
                    } catch (e) {
                        if (e.name === 'SyntaxError') throw new Error(text || `Erro HTTP ${response.status}`);
                        throw e;
                    }
                }
                result = await response.json();
            }
            
            if (result.sucesso) {
                setStep(4);
                document.getElementById('linkPr').href = result.pullRequestUrl;
                showView(viewSuccess);
            } else {
                showError(result.mensagem);
            }
        } catch (error) {
            showError(error.message);
        }
    });

    // --- LÃ“GICA DO STORAGE ---
    btnNovoRascunho?.addEventListener('click', () => {
        storageList.style.display = 'none';
        storageForm.style.display = 'block';
    });

    btnCancelarStorage?.addEventListener('click', () => {
        storageForm.style.display = 'none';
        storageList.style.display = 'block';
        document.getElementById('draftTitle').value = '';
        document.getElementById('draftDesc').value = '';
        document.getElementById('draftType').value = 'ProtÃ³tipo';
        document.getElementById('draftShared').checked = false;
        draftSelectedFile = null;
        updateDraftFileDisplay();
    });

    draftDropzone?.addEventListener('click', () => draftFileInput.click());
    btnSelectDraftZip?.addEventListener('click', (e) => { e.stopPropagation(); draftFileInput.click(); });

    draftDropzone?.addEventListener('dragover', (e) => {
        e.preventDefault();
        draftDropzone.classList.add('dragover');
    });

    draftDropzone?.addEventListener('dragleave', () => {
        draftDropzone.classList.remove('dragover');
    });

    draftDropzone?.addEventListener('drop', (e) => {
        e.preventDefault();
        draftDropzone.classList.remove('dragover');
        if (e.dataTransfer.files.length) {
            handleDraftFile(e.dataTransfer.files[0]);
        }
    });

    draftFileInput?.addEventListener('change', (e) => {
        if (e.target.files.length) handleDraftFile(e.target.files[0]);
    });

    btnRemoveDraftFile?.addEventListener('click', (e) => {
        e.stopPropagation();
        draftSelectedFile = null;
        draftFileInput.value = '';
        updateDraftFileDisplay();
    });

    function handleDraftFile(file) {
        if (!file.name.endsWith('.zip')) {
            alert('Por favor, selecione um arquivo .zip');
            return;
        }
        draftSelectedFile = file;
        updateDraftFileDisplay();
    }

    function updateDraftFileDisplay() {
        if (draftSelectedFile) {
            draftDropzone.querySelector('.dropzone-content').style.display = 'none';
            draftSelectedFileContainer.style.display = 'flex';
            draftFileNameDisplay.textContent = draftSelectedFile.name;
            let size = draftSelectedFile.size;
            draftFileSizeDisplay.textContent = size < 1024 * 1024 ? `${(size / 1024).toFixed(2)} KB` : `${(size / (1024 * 1024)).toFixed(2)} MB`;
        } else {
            draftDropzone.querySelector('.dropzone-content').style.display = 'block';
            draftSelectedFileContainer.style.display = 'none';
        }
    }

    btnSalvarStorage?.addEventListener('click', async () => {
        const title = document.getElementById('draftTitle').value.trim();
        if (!title) return alert('O tÃ­tulo Ã© obrigatÃ³rio.');
        if (!draftSelectedFile) return alert('Selecione um arquivo ZIP para salvar.');

        const formData = new FormData();
        formData.append('Titulo', title);
        formData.append('Descricao', document.getElementById('draftDesc').value.trim());
        formData.append('Tipo', document.getElementById('draftType').value);
        formData.append('CompartilhadoEquipe', document.getElementById('draftShared').checked);
        formData.append('ArquivoZip', draftSelectedFile);

        try {
            const btn = btnSalvarStorage;
            btn.disabled = true;
            btn.textContent = 'Salvando...';

            const response = await CasaMulherAuth.apiFetch('/api/equipe-storage/salvar', {
                method: 'POST',
                body: formData
            });

            if (!response.ok) {
                const text = await response.text();
                try {
                    const json = JSON.parse(text);
                    throw new Error(json.mensagem || text);
                } catch {
                    throw new Error(text || `Erro HTTP ${response.status}`);
                }
            }

            alert('Rascunho salvo com sucesso no Storage!');
            btnCancelarStorage.click(); // Volta para a lista
            carregarRascunhos();
        } catch (error) {
            alert(`Erro ao salvar: ${error.message}`);
        } finally {
            btnSalvarStorage.disabled = false;
            btnSalvarStorage.textContent = 'Salvar no Storage';
        }
    });

    async function carregarRascunhos() {
        myDraftsContainer.innerHTML = '<div class="empty-state">Carregando meus rascunhos...</div>';
        sharedDraftsContainer.innerHTML = '<div class="empty-state">Carregando rascunhos compartilhados...</div>';

        try {
            const [meusResp, compResp] = await Promise.all([
                CasaMulherAuth.apiFetch('/api/equipe-storage/meus-rascunhos'),
                CasaMulherAuth.apiFetch('/api/equipe-storage/compartilhados')
            ]);

            if (meusResp.ok) {
                const meus = await meusResp.json();
                renderizarRascunhos(meus, myDraftsContainer, true);
            }
            if (compResp.ok) {
                const comp = await compResp.json();
                renderizarRascunhos(comp, sharedDraftsContainer, false);
            }
        } catch (e) {
            myDraftsContainer.innerHTML = `<div class="empty-state">Erro ao carregar: ${e.message}</div>`;
            sharedDraftsContainer.innerHTML = `<div class="empty-state">Erro ao carregar: ${e.message}</div>`;
        }
    }

    function renderizarRascunhos(lista, container, isMine) {
        if (!lista || lista.length === 0) {
            container.innerHTML = '<div class="empty-state">Nenhum rascunho encontrado.</div>';
            return;
        }

        container.innerHTML = lista.map(r => {
            const data = new Date(r.criadoEm).toLocaleString('pt-BR');
            const sizeMb = (r.tamanhoTotalBytes / (1024 * 1024)).toFixed(2);
            let badges = `<span style="background: #eef2ff; color: #4f46e5; padding: 2px 6px; border-radius: 4px; font-size: 11px;">${r.tipo}</span>`;
            if (r.compartilhadoEquipe) badges += ` <span style="background: #dcfce7; color: #166534; padding: 2px 6px; border-radius: 4px; font-size: 11px;">Compartilhado</span>`;
            if (r.temArquivosForaPrototipos) badges += ` <span style="background: #fef08a; color: #854d0e; padding: 2px 6px; border-radius: 4px; font-size: 11px;">Fora de ProtÃ³tipos</span>`;
            
            return `
                <div class="draft-card" style="border: 1px solid #e2e8f0; border-radius: 8px; padding: 15px; margin-bottom: 10px; background: white;">
                    <div style="display: flex; justify-content: space-between; align-items: flex-start;">
                        <div>
                            <h4 style="margin: 0 0 5px 0; font-size: 16px;">${r.titulo}</h4>
                            <div style="font-size: 12px; color: #64748b; margin-bottom: 8px;">
                                Salvo em ${data} por ${r.autor?.nome} <br>
                                ${r.totalArquivos} arquivos (${sizeMb} MB)
                            </div>
                            <div style="margin-bottom: 10px;">${badges}</div>
                            <p style="margin: 0; font-size: 13px; color: #334155;">${r.descricao || ''}</p>
                        </div>
                        <div style="display: flex; flex-direction: column; gap: 8px;">
                            <button class="btn btn-secondary btn-sm" onclick="baixarRascunho('${r.id}')" style="padding: 4px 10px; font-size: 12px;">Baixar ZIP</button>
                            <button class="btn btn-primary btn-sm" onclick="criarPrRascunho('${r.id}')" style="padding: 4px 10px; font-size: 12px;">Criar PR</button>
                        </div>
                    </div>
                </div>
            `;
        }).join('');
    }

    window.baixarRascunho = (id) => {
        window.open(`${window.API_BASE_URL}/api/equipe-storage/download?id=${id}&access_token=${CasaMulherAuth.getToken()}`, '_blank');
    };

    window.criarPrRascunho = async (id) => {
        if (!confirm('Deseja criar um Pull Request oficial a partir deste rascunho?')) return;
        
        try {
            showLoading('Criando Pull Request a partir do rascunho...', 4);
            const response = await CasaMulherAuth.apiFetch('/api/equipe-storage/criar-pr', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ id })
            });

            if (!response.ok) {
                const text = await response.text();
                try {
                    const json = JSON.parse(text);
                    throw new Error(json.mensagem || text);
                } catch {
                    throw new Error(text || `Erro HTTP ${response.status}`);
                }
            }

            const result = await response.json();
            if (result.sucesso) {
                setStep(4);
                document.getElementById('linkPr').href = result.pullRequestUrl;
                showView(viewSuccess);
            } else {
                showError(result.mensagem);
            }
        } catch (error) {
            showError(error.message);
        }
    };

    // Iniciar
    setStep(1);
    showView(viewOrigem);
});