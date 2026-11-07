/* =====================================================================
   PROTÓTIPO - DADOS FICTÍCIOS
   Gera, sempre do mesmo jeito (semente fixa), uma Casa com ~240 assistidas
   desde set/2025: cadastro, direcionamento, cursos e chamada, Jurídico,
   Psicologia e fila da Assistente social. Todas as telas de relatório leem
   desta mesma base, então os números batem entre ADM e cada área.
   Os campos seguem o que o sistema já grava; o que ainda NÃO existe no
   sistema está marcado como "dado novo" nas telas.
   ===================================================================== */
(function () {
    "use strict";

    const HOJE = "2026-10-23";

    // ---------- utilidades de data (sempre texto AAAA-MM-DD) ----------
    function d2s(d) { return d.getFullYear() + "-" + String(d.getMonth() + 1).padStart(2, "0") + "-" + String(d.getDate()).padStart(2, "0"); }
    function s2d(s) { const [a, m, d] = s.split("-").map(Number); return new Date(a, m - 1, d); }
    function addDias(s, n) { const d = s2d(s); d.setDate(d.getDate() + n); return d2s(d); }
    function diffDias(a, b) { return Math.round((s2d(b) - s2d(a)) / 86400000); }
    function diaSemana(s) { return s2d(s).getDay(); }
    function diaUtil(s) { let x = s; while (diaSemana(x) === 0 || diaSemana(x) === 6) x = addDias(x, 1); return x; }
    function minS(a, b) { return a < b ? a : b; }

    // ---------- aleatório com semente fixa ----------
    let seed = 20261023;
    function R() { seed |= 0; seed = seed + 0x6D2B79F5 | 0; let t = Math.imul(seed ^ seed >>> 15, 1 | seed); t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t; return ((t ^ t >>> 14) >>> 0) / 4294967296; }
    // segunda semente, só para os campos da versão 2 (não muda os dados da versão 1)
    let seed2 = 7112026;
    function R2() { seed2 |= 0; seed2 = seed2 + 0x6D2B79F5 | 0; let t = Math.imul(seed2 ^ seed2 >>> 15, 1 | seed2); t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t; return ((t ^ t >>> 14) >>> 0) / 4294967296; }
    const pick = a => a[Math.floor(R() * a.length)];
    const chance = p => R() < p;
    const entre = (a, b) => a + Math.floor(R() * (b - a + 1));
    function wpick(pares) { const tot = pares.reduce((s, p) => s + p[1], 0); let r = R() * tot; for (const p of pares) { r -= p[1]; if (r <= 0) return p[0]; } return pares[pares.length - 1][0]; }

    // ---------- equipe ----------
    const EQUIPE = [
        { id: "ADM-000001", nome: "Coordenação Geral", perfil: "adm" },
        { id: "REC-000001", nome: "Carla Mendes", perfil: "recepcao" },
        { id: "REC-000002", nome: "Daniela Rocha", perfil: "recepcao" },
        { id: "JUR-000001", nome: "Dra. Helena Prado", perfil: "juridico", oab: "OAB/SP 000.001" },
        { id: "JUR-000002", nome: "Dra. Júlia Nunes", perfil: "juridico", oab: "OAB/SP 000.002" },
        { id: "JUR-000003", nome: "Jurídico Teste", perfil: "juridico", oab: "OAB/SP 000.003" },
        { id: "PSI-000001", nome: "Psicóloga Um", perfil: "psicologia", crp: "CRP 06/000001" },
        { id: "PSI-000002", nome: "Marina Alves", perfil: "psicologia", crp: "CRP 06/000002" },
        { id: "PRO-000001", nome: "Ana Lima", perfil: "professor" },
        { id: "PRO-000002", nome: "Beatriz Souza", perfil: "professor" },
        { id: "PRO-000003", nome: "Cláudia Reis", perfil: "professor" },
        { id: "PRO-000004", nome: "Denise Faria", perfil: "professor" }
    ];
    const func = id => EQUIPE.find(f => f.id === id) || { id: id, nome: id };
    const doPerfil = p => EQUIPE.filter(f => f.perfil === p);

    // ---------- cursos (nomes iguais aos da homologação + alguns novos) ----------
    const CURSOS = [
        { id: 1, nome: "Informática e Inclusão Digital", prof: "PRO-000001", dia: 1, vagas: 20, inicio: "2025-09-01", peso: 10, ativo: true },
        { id: 2, nome: "Culinária e Autonomia", prof: "PRO-000002", dia: 3, vagas: 16, inicio: "2025-09-03", peso: 8.5, ativo: true },
        { id: 3, nome: "Costura e Bordado", prof: "PRO-000001", dia: 2, vagas: 15, inicio: "2025-09-02", peso: 5.5, ativo: true },
        { id: 4, nome: "Empoderamento Feminino", prof: "PRO-000003", dia: 4, vagas: 25, inicio: "2025-09-04", peso: 6, ativo: true },
        { id: 5, nome: "Maquiagem e Autoestima", prof: "PRO-000004", dia: 5, vagas: 15, inicio: "2025-09-05", peso: 4.5, ativo: true },
        { id: 6, nome: "Artesanato e Renda Extra", prof: "PRO-000002", dia: 2, vagas: 15, inicio: "2025-10-07", peso: 2.6, ativo: true },
        { id: 7, nome: "Educação Financeira", prof: "PRO-000003", dia: 1, vagas: 20, inicio: "2026-03-02", peso: 1.6, ativo: true },
        { id: 8, nome: "Dança Circular", prof: "PRO-000004", dia: 3, vagas: 20, inicio: "2025-09-03", peso: 1.1, ativo: true },
        { id: 9, nome: "Defesa Pessoal", prof: "PRO-000004", dia: 5, vagas: 20, inicio: "2026-05-08", peso: 0.8, ativo: true },
        { id: 10, nome: "Cabeleireiro Básico", prof: "PRO-000002", dia: 4, vagas: 12, inicio: "2025-09-04", fim: "2026-02-26", peso: 2.4, ativo: false }
    ];
    const curso = id => CURSOS.find(c => c.id === id);

    const TEMAS = {
        1: ["Ligando o computador e usando o mouse", "Criando um e-mail", "Navegando com segurança", "Golpes pelo WhatsApp", "Currículo no editor de texto", "Gov.br e serviços online", "Planilha básica", "Pix e banco no celular", "Videochamada", "Revisão e prática livre"],
        2: ["Higiene e segurança na cozinha", "Pães caseiros", "Marmitas para vender", "Custos e preço de venda", "Doces de festa", "Salgados assados", "Aproveitamento integral", "Embalagem e divulgação"],
        3: ["Pontos básicos à mão", "Máquina reta", "Barra e ajustes", "Bordado livre", "Molde de saia", "Customização de roupas", "Acabamentos"],
        4: ["Conhecendo a Lei Maria da Penha", "Ciclo da violência", "Autoestima e autocuidado", "Rede de proteção do município", "Direitos trabalhistas", "Comunicação e limites", "Projeto de vida"],
        5: ["Cuidados com a pele", "Maquiagem do dia a dia", "Maquiagem para entrevista", "Higiene de pincéis", "Automaquiagem prática"],
        6: ["Reciclagem criativa", "Crochê básico", "Bijuterias", "Precificação do artesanato", "Feirinha da Casa"],
        7: ["Orçamento doméstico", "Dívidas e negociação", "Conta própria e Pix", "MEI na prática", "Reserva de emergência"],
        8: ["Roda de abertura", "Danças tradicionais", "Respiração e movimento", "Roda de encerramento"],
        9: ["Postura e prevenção", "Saídas de agarre", "Voz e limite", "Prática em duplas"],
        10: ["Corte básico", "Escova e chapinha", "Coloração", "Atendimento ao cliente"]
    };

    // feriados e recessos sem aula
    const SEM_AULA = [["2025-11-20", "2025-11-20"], ["2025-12-19", "2026-01-11"], ["2026-02-16", "2026-02-18"], ["2026-04-03", "2026-04-03"], ["2026-04-21", "2026-04-21"], ["2026-06-04", "2026-06-04"], ["2026-07-13", "2026-07-24"], ["2026-09-07", "2026-09-07"], ["2026-10-12", "2026-10-12"]];
    const semAula = s => SEM_AULA.some(([a, b]) => s >= a && s <= b);

    // ---------- listas para os textos ----------
    const NOMES = ["Ana", "Adriana", "Aline", "Amanda", "Andreia", "Bianca", "Bruna", "Camila", "Carolina", "Cícera", "Cláudia", "Cristiane", "Daiane", "Débora", "Edna", "Elaine", "Eliane", "Fabiana", "Fátima", "Francisca", "Gabriela", "Gisele", "Helena", "Ingrid", "Ivone", "Jaqueline", "Jéssica", "Josefa", "Joana", "Kátia", "Kelly", "Larissa", "Letícia", "Lúcia", "Luana", "Márcia", "Marli", "Mirian", "Natália", "Neide", "Patrícia", "Paula", "Priscila", "Raquel", "Regina", "Rita", "Rosângela", "Sandra", "Sílvia", "Simone", "Sueli", "Tatiane", "Tereza", "Vanessa", "Vera", "Viviane", "Yasmin"];
    const SOBRENOMES = ["Silva", "Santos", "Oliveira", "Souza", "Lima", "Pereira", "Ferreira", "Alves", "Gomes", "Ribeiro", "Carvalho", "Rodrigues", "Almeida", "Nascimento", "Araújo", "Barbosa", "Cardoso", "Moreira", "Teixeira", "Cavalcante", "Mendes", "Rocha", "Monteiro", "Freitas", "Batista", "Vieira", "Correia", "Pinto", "Moura", "Dias"];
    const BAIRROS = [["Centro", 9], ["Vila Virgínia", 7], ["Jardim Caiuby", 6], ["Parque Piratininga", 6], ["Jardim Odete", 5], ["Vila Monte Belo", 4], ["Jardim Napoli", 4], ["Pequeno Coração", 3], ["Jardim Altos de Itaquá", 3], ["Jardim Moraes", 3], ["Jardim Aracaré", 2], ["Vila São Carlos", 2]];
    const CIDADES = [["Itaquaquecetuba/SP", 88], ["Poá/SP", 5], ["Ferraz de Vasconcelos/SP", 4], ["Suzano/SP", 3]];
    const RUAS = ["Rua Duque de Caxias", "Rua Benedito Feliciano", "Av. Ítalo Adami", "Rua Uberaba", "Rua Capela", "Estrada dos Índios", "Rua Joaquim Osório", "Rua Luiz Gama"];
    const MOTIVOS_DESISTENCIA = [["Conseguiu emprego no horário do curso", 6], ["Sem dinheiro para a condução", 4], ["Sem com quem deixar os filhos", 5], ["Mudou de cidade", 2], ["Problema de saúde", 2], ["Não informou", 4]];
    const JUSTIFICATIVAS = ["Consulta médica", "Filho doente", "Trabalho no dia", "Sem dinheiro para a condução", "Compromisso em outro serviço da rede", "Chuva forte, não conseguiu sair", "Cuidando de familiar"];
    const OBS_PRESENTE = ["Muito participativa hoje", "Chegou atrasada (ônibus)", "Ajudou as colegas na atividade", "Concluiu a atividade prática", "Pediu para sair mais cedo", "Trouxe dúvida da aula anterior, resolvida", "Parecia abatida; conversamos no fim da aula", "Ótima evolução na prática", "Trouxe o filho, ficou na brinquedoteca"];
    const OBS_FALTA = ["Não avisou", "Avisou pelo grupo depois da aula", "Segunda falta seguida, vou ligar (se puder ligar)"];
    const DEMANDAS_JUR = [["Medida protetiva", 30], ["Pensão alimentícia", 20], ["Divórcio", 15], ["Guarda", 12], ["Violência patrimonial", 8], ["Partilha de bens", 7], ["Outro", 8]];
    const ENC_JUR = ["Delegacia da Mulher", "Defensoria Pública", "CREAS", "Conselho Tutelar", "Outro"];
    const VARAS = ["1ª Vara Criminal de Itaquaquecetuba", "Vara de Família de Itaquaquecetuba", "2ª Vara Cível de Itaquaquecetuba", "Juizado de Violência Doméstica"];
    const PROXIMOS_JUR = ["Juntar documentos dos filhos", "Aguardar decisão da medida protetiva", "Retorno para assinar procuração", "Acompanhar audiência de conciliação", "Levar comprovante de renda", "Orientada a registrar BO na Delegacia da Mulher", "Aguardar contato da Defensoria"];
    const RELATOS_JUR = [
        "Assistida relata episódios de agressão verbal e ameaças. Orientada sobre medida protetiva de urgência e rede de apoio.",
        "Busca orientação sobre pensão para os dois filhos. Explicado o procedimento e documentos necessários.",
        "Deseja se divorciar; casamento sem bens a partilhar. Orientada sobre divórcio consensual e litigioso.",
        "Retorno: medida protetiva deferida. Orientada sobre como agir em caso de descumprimento.",
        "Ex-companheiro reteve documentos e cartão do benefício. Orientada sobre violência patrimonial e BO.",
        "Retorno com documentos. Petição em elaboração.",
        "Audiência marcada. Explicado como funciona e que pode pedir para não ficar na mesma sala."
    ];
    const DEMANDAS_PSI = ["Ansiedade após separação", "Luto e reorganização da rotina", "Sintomas após episódio de violência", "Autoestima e retomada do trabalho", "Conflitos com os filhos", "Insônia e medo de sair de casa"];
    const EVOLUCOES_PSI = [
        "Acolhimento inicial. Assistida informada sobre o sigilo e seus limites. Escuta da demanda principal e combinados do acompanhamento.",
        "Relata melhora no sono. Trabalhamos estratégias para momentos de ansiedade.",
        "Sessão centrada no plano de segurança; identificou pessoas de confiança e contatos da rede.",
        "Trouxe lembranças do episódio de violência; acolhimento e validação. Combinado retorno em uma semana.",
        "Fala sobre a volta aos estudos e o curso na Casa; percebe mais autonomia.",
        "Semana difícil, conflito com o ex-companheiro na entrega das crianças. Reforçado plano de segurança.",
        "Mais estável. Discutimos a possibilidade de espaçar as sessões."
    ];
    const PLANOS_PSI = ["Manter sessões semanais", "Reavaliar risco na próxima sessão", "Espaçar para quinzenal", "Articular com CAPS de referência", "Trabalhar rede de apoio familiar"];
    const ENC_PSI = ["CAPS", "UBS", "CRAS / CREAS", "Abrigo / casa de passagem", "Outro"];
    const MOTIVOS_RESTRITO = ["Processo em segredo de justiça", "Casa-abrigo de endereço sigiloso", "Medida protetiva / risco grave", "Pedido da própria assistida"];

    // ---------- assistidas em destaque (usadas como exemplo nas telas) ----------
    const DESTAQUES = [
        { nome: "Fernanda Lopes Ribeiro", nasc: "1993-08-11", entrada: "2025-10-06", hora: 10, bairro: "Centro", cidade: "Itaquaquecetuba/SP", rua: "Rua Duque de Caxias, 80", contato: "Só no horário combinado", obsContato: "Só depois das 18h",
          cursos: [{ id: 1, inicio: "2025-10-13", fim: "2026-03-30", sit: "concluiu", pres: 0.82 }, { id: 2, inicio: "2026-08-05", sit: "cursando", pres: 0.9 }],
          enc: ["juridico", "psicologia"], jurDemanda: "Medida protetiva", jurAtend: 30, psiSess: 80, psiRiscoFinal: "Alto", recepcionista: "REC-000001", jurResp: "JUR-000003", psiResp: "PSI-000001" },
        { nome: "Juliana Martins Costa", nasc: "1988-02-27", entrada: "2026-02-09", hora: 14, bairro: "Vila Virgínia", cidade: "Itaquaquecetuba/SP", rua: "Rua Uberaba, 512", contato: "Pode ligar",
          cursos: [{ id: 3, inicio: "2026-02-10", fim: "2026-05-12", sit: "desistiu", motivo: "Conseguiu emprego no horário do curso", pres: 0.55 }, { id: 4, inicio: "2026-06-11", sit: "cursando", pres: 0.86 }, { id: 1, inicio: "2026-08-03", sit: "cursando", pres: 0.78 }],
          enc: ["assistente_social"], recepcionista: "REC-000002" },
        { nome: "Rosana Pereira Dias", nasc: "1979-12-03", entrada: "2026-04-14", hora: 9, bairro: "Jardim Caiuby", cidade: "Itaquaquecetuba/SP", rua: "", contato: "Não ligar", semTel: true, sigiloso: true,
          cursos: [], enc: ["juridico", "psicologia"], restrito: "Casa-abrigo de endereço sigiloso", jurDemanda: "Medida protetiva", jurAtend: 30, jurVis: "responsaveis", psiSess: 80, psiRiscoFinal: "Moderado", recepcionista: "REC-000001", jurResp: "JUR-000001", psiResp: "PSI-000002" },
        { nome: "Cícera Gomes Nascimento", nasc: "1965-05-19", entrada: "2025-09-15", hora: 11, bairro: "Parque Piratininga", cidade: "Itaquaquecetuba/SP", rua: "Rua Capela, 33", contato: "Pode ligar",
          cursos: [{ id: 10, inicio: "2025-09-18", fim: "2026-02-26", sit: "encerrado", pres: 0.93 }, { id: 2, inicio: "2026-03-04", fim: "2026-07-08", sit: "concluiu", pres: 0.95 }, { id: 6, inicio: "2026-08-04", sit: "cursando", pres: 0.97 }],
          enc: ["psicologia"], psiSess: 12, psiEncerrar: "Alta", psiRiscoFinal: "Baixo", recepcionista: "REC-000001", psiResp: "PSI-000002" }
    ];

    // ---------- geração ----------
    const ASSISTIDAS = [], MATRICULAS = [], AULAS = [], JUR_FILA = [], JUR_ATEND = [], JUR_CASOS = [], PSI_FILA = [], PSI_PRONT = [], PSI_SESS = [], SOCIAL_FILA = [], REC_EVENTOS = [], AUDITORIA = [];
    const nomesUsados = new Set();
    const TOTAL_DIAS = diffDias("2025-09-01", HOJE);

    function novoNome() {
        let n;
        do { n = pick(NOMES) + " " + pick(SOBRENOMES) + " " + pick(SOBRENOMES); } while (nomesUsados.has(n));
        nomesUsados.add(n); return n;
    }
    function cpfMascarado() { return "***." + entre(100, 999) + "." + entre(100, 999) + "-**"; }

    const N = 236;
    for (let i = 0; i < N; i++) {
        const dst = DESTAQUES[i] || null;
        let entrada = dst ? dst.entrada : diaUtil(addDias("2025-09-01", Math.floor(TOTAL_DIAS * Math.pow(R(), 0.75))));
        if (entrada > HOJE) entrada = HOJE;
        if (semAula(entrada) && !dst) entrada = diaUtil(addDias(entrada, 10));
        if (entrada > HOJE) entrada = "2026-10-22";
        const idade = dst ? null : wpick([[entre(18, 24), 14], [entre(25, 34), 30], [entre(35, 44), 28], [entre(45, 59), 20], [entre(60, 75), 8]]);
        const nasc = dst ? dst.nasc : addDias(addDias(entrada, -idade * 365), -entre(0, 360));
        const origem = dst ? "recepcao" : wpick([["recepcao", 85], ["juridico", 10], ["psicologia", 5]]);
        const cadPor = dst && dst.recepcionista ? dst.recepcionista : origem === "recepcao" ? wpick([["REC-000001", 60], ["REC-000002", 40]]) : origem === "juridico" ? pick(["JUR-000001", "JUR-000002", "JUR-000003"]) : pick(["PSI-000001", "PSI-000002"]);
        const contato = dst ? dst.contato : wpick([["Pode ligar", 60], ["Só no horário combinado", 25], ["Não ligar", 15]]);
        const a = {
            id: i + 1,
            nome: dst ? dst.nome : novoNome(),
            cpf: dst && i === 0 ? "***.685.620-**" : cpfMascarado(),
            nasc: nasc,
            entrada: entrada,
            horaEntrada: dst ? dst.hora : wpick([[8, 6], [9, 12], [10, 14], [11, 10], [13, 8], [14, 12], [15, 10], [16, 6], [17, 3]]),
            bairro: dst ? dst.bairro : wpick(BAIRROS),
            cidade: dst ? dst.cidade : wpick(CIDADES),
            rua: dst ? dst.rua : pick(RUAS) + ", " + entre(10, 990),
            contato: contato,
            obsContato: dst && dst.obsContato ? dst.obsContato : (contato === "Só no horário combinado" ? pick(["Só depois das 18h", "Só de manhã", "Mandar mensagem antes"]) : ""),
            semTelefoneSeguro: dst ? !!dst.semTel : chance(0.08),
            enderecoSigiloso: dst ? !!dst.sigiloso : chance(0.06),
            origem: origem,
            cadastradaPor: cadPor,
            restrito: dst ? (dst.restrito || null) : (chance(0.04) ? pick(MOTIVOS_RESTRITO) : null),
            encaminhamentos: [],
            cursosDirecionados: []
        };
        if (a.semTelefoneSeguro) a.contato = "Não ligar";
        ASSISTIDAS.push(a);
        REC_EVENTOS.push({ assistidaId: a.id, data: entrada, tipo: origem === "recepcao" ? "Acolhimento inicial" : "Cadastro direto em outra área", por: cadPor, detalhe: origem === "recepcao" ? "Ficha criada na Recepção" : "Cadastrada direto no " + (origem === "juridico" ? "Jurídico" : "atendimento da equipe técnica") });

        // direcionamento
        let enc = [];
        if (dst) enc = dst.enc.slice();
        else {
            if (origem === "juridico" || chance(0.42)) enc.push("juridico");
            if (origem === "psicologia" || chance(0.36)) enc.push("psicologia");
            if (chance(0.28)) enc.push("assistente_social");
        }
        let planoCursos = [];
        if (dst) planoCursos = dst.cursos;
        else {
            const qtd = origem !== "recepcao" ? wpick([[0, 6], [1, 3]]) : wpick([[0, 25], [1, 45], [2, 22], [3, 8]]);
            const disponiveis = CURSOS.filter(c => c.inicio <= addDias(entrada, 20) && (!c.fim || c.fim > addDias(entrada, 20)));
            const escolhidos = new Set();
            for (let k = 0; k < qtd && escolhidos.size < disponiveis.length; k++) {
                escolhidos.add(wpick(disponiveis.filter(c => !escolhidos.has(c.id)).map(c => [c.id, c.peso])));
            }
            escolhidos.forEach(cid => planoCursos.push({ id: cid }));
        }
        if (!enc.length && !planoCursos.length) {
            if (chance(0.5)) enc.push("assistente_social"); else planoCursos.push({ id: wpick(CURSOS.filter(c => c.ativo && c.inicio <= entrada).map(c => [c.id, c.peso])) });
        }

        // encaminhamentos: alguns acontecem depois da entrada
        enc.forEach(tipo => {
            const tardio = !dst && chance(0.15);
            const data = tardio ? minS(diaUtil(addDias(entrada, entre(20, 120))), HOJE) : entrada;
            const por = tardio ? (tipo === "psicologia" && enc.includes("juridico") ? pick(["JUR-000001", "JUR-000002", "JUR-000003"]) : cadPor) : cadPor;
            a.encaminhamentos.push({ tipo: tipo, data: data, por: por });
            if (func(por).perfil === "recepcao") REC_EVENTOS.push({ assistidaId: a.id, data: data, tipo: tipo === "psicologia" ? "Encaminhada à equipe técnica" : tipo === "juridico" ? "Encaminhada ao Jurídico" : "Encaminhada à Assistente social", por: por, detalhe: tardio ? "Encaminhamento feito em retorno à Recepção" : "No acolhimento", encTipo: tipo });
        });

        // cursos e matrícula
        planoCursos.forEach((pc, idx) => {
            const c = curso(pc.id);
            const inicio = pc.inicio || diaUtil(addDias(entrada, entre(1, 14) + idx * entre(0, 30)));
            if (inicio > HOJE) return;
            let sit = pc.sit, fim = pc.fim || null, motivo = pc.motivo || null;
            if (!sit) {
                const destino = wpick([["concluiu", 72], ["desistiu", 28]]);
                const dur = destino === "desistiu" ? entre(14, 75) : entre(100, 150);
                const f = addDias(inicio, dur);
                if (c.fim && f > c.fim) { sit = "encerrado"; fim = c.fim; }
                else if (f >= HOJE) { sit = "cursando"; }
                else { sit = destino; fim = f; if (sit === "desistiu") motivo = wpick(MOTIVOS_DESISTENCIA); }
                if (c.fim && sit === "cursando") { sit = "encerrado"; fim = c.fim; }
            }
            const base = pc.pres || (sit === "desistiu" ? 0.4 + R() * 0.25 : 0.6 + R() * 0.36);
            MATRICULAS.push({ id: MATRICULAS.length + 1, assistidaId: a.id, cursoId: c.id, inicio: inicio, fim: fim, situacao: sit, motivoSaida: motivo, presencaBase: base, viaDirecionamento: true, direcionadaEm: idx === 0 ? entrada : inicio });
            a.cursosDirecionados.push({ cursoId: c.id, data: idx === 0 ? entrada : inicio });
            if (idx > 0 && inicio !== entrada) REC_EVENTOS.push({ assistidaId: a.id, data: inicio, tipo: "Direcionamento alterado", por: cadPor.startsWith("REC") ? cadPor : "REC-000001", detalhe: "Incluído o curso " + c.nome });
        });

        if (!dst && chance(0.3)) {
            const dt = diaUtil(addDias(entrada, entre(15, 200)));
            if (dt <= HOJE) REC_EVENTOS.push({ assistidaId: a.id, data: dt, tipo: "Dados de contato atualizados", por: pick(["REC-000001", "REC-000002"]), detalhe: pick(["Telefone novo", "Mudou de endereço", "Pediu para não receber ligações", "Incluído horário combinado"]) });
        }
    }

    // ---------- aulas e chamada ----------
    CURSOS.forEach(c => {
        const temas = TEMAS[c.id];
        let d = c.inicio, n = 0;
        while (diaSemana(d) !== c.dia) d = addDias(d, 1);
        const ate = c.fim ? minS(c.fim, HOJE) : HOJE;
        while (d <= ate) {
            if (!semAula(d) && !chance(0.03)) {
                const aula = { id: AULAS.length + 1, cursoId: c.id, data: d, tema: temas[n % temas.length], professora: c.prof, registros: [], observacoes: chance(0.25) ? pick(["Turma animada, atividade rendeu bem", "Faltou material, adaptei a atividade", "Duas alunas novas hoje", "Aula prática no laboratório", "Revisão por causa das faltas da semana passada"]) : "" };
                n++;
                MATRICULAS.filter(m => m.cursoId === c.id && m.inicio <= d && (!m.fim || m.fim >= d)).forEach(m => {
                    let p = m.presencaBase;
                    if (m.situacao === "desistiu" && m.fim && diffDias(d, m.fim) < 21) p -= 0.3;
                    let status, obs = "";
                    if (R() < p) { status = "presente"; if (chance(0.07)) obs = pick(OBS_PRESENTE); }
                    else if (chance(0.45)) { status = "justificada"; obs = pick(JUSTIFICATIVAS); }
                    else { status = "faltou"; if (chance(0.2)) obs = pick(OBS_FALTA); }
                    aula.registros.push({ assistidaId: m.assistidaId, matriculaId: m.id, status: status, observacao: obs });
                });
                // algumas chamadas não lançadas (mais nas últimas semanas): entram em "qualidade dos registros"
                if (R2() < (d > addDias(HOJE, -45) ? 0.08 : 0.01)) { aula.registros = []; aula.semChamada = true; }
                AULAS.push(aula);
            }
            d = addDias(d, 7);
        }
    });

    // assistidas em destaque: mais observações da professora, para o exemplo ficar rico
    AULAS.forEach(au => au.registros.forEach(r => {
        if (r.assistidaId > DESTAQUES.length || r.observacao) return;
        if (r.status === "presente" && chance(0.3)) r.observacao = pick(OBS_PRESENTE);
        else if (r.status === "faltou" && chance(0.5)) r.observacao = pick(OBS_FALTA);
    }));

    // ---------- Jurídico ----------
    ASSISTIDAS.forEach((a, i) => {
        const dst = DESTAQUES[i] || null;
        const e = a.encaminhamentos.find(x => x.tipo === "juridico");
        if (!e) return;
        let espera = a.origem === "juridico" ? 0 : entre(1, 18);
        if (!dst && espera > 0 && R2() < 0.15) espera += 15 + Math.floor(R2() * 30);
        let atendida = diaUtil(addDias(e.data, espera));
        // situação operacional do encaminhamento (o que a Recepção vê)
        const fila = { assistidaId: a.id, encaminhadaEm: e.data, por: e.por, atendidaEm: null, agendadoEm: null, agendadoPara: null, canceladoEm: null };
        JUR_FILA.push(fila);
        if (espera > 0) { fila.agendadoEm = minS(addDias(e.data, 1), HOJE); fila.agendadoPara = atendida; }
        if (!dst && R2() < 0.035 && addDias(e.data, 2) <= HOJE) { fila.canceladoEm = addDias(e.data, 2); fila.agendadoPara = null; return; }
        if (atendida > HOJE) {
            if (R2() < 0.45) { fila.agendadoEm = null; fila.agendadoPara = null; }
            atendida = null;
        }
        fila.atendidaEm = atendida;
        if (!atendida) return;
        const resp = dst && dst.jurResp ? dst.jurResp : wpick([["JUR-000001", 45], ["JUR-000002", 35], ["JUR-000003", 20]]);
        const demanda = dst && dst.jurDemanda ? dst.jurDemanda : wpick(DEMANDAS_JUR);
        const caso = { id: JUR_CASOS.length + 1, assistidaId: a.id, responsaveis: [resp], visibilidade: dst && dst.jurVis ? dst.jurVis : (chance(0.18) ? "responsaveis" : "equipe"), demanda: demanda, abertoEm: atendida, processo: chance(0.45) ? entre(1000000, 1099999) + "-" + entre(10, 99) + ".2026.8.26.0278" : "", vara: "" };
        if (caso.processo) caso.vara = pick(VARAS);
        if (chance(0.15)) caso.responsaveis.push(pick(["JUR-000001", "JUR-000002", "JUR-000003"].filter(x => x !== resp)));
        JUR_CASOS.push(caso);
        const qtd = dst && dst.jurAtend ? dst.jurAtend : wpick([[1, 30], [2, 25], [3, 20], [4, 12], [5, 8], [6, 5]]);
        let d = atendida;
        for (let k = 0; k < qtd && d <= HOJE; k++) {
            const ultimo = k === qtd - 1 || addDias(d, 15) > HOJE;
            let sit = "Em andamento";
            if (ultimo) sit = diffDias(d, HOJE) > 60 ? wpick([["Encerrado", 70], ["Em andamento", 20], ["Aguardando documento", 10]]) : wpick([["Em andamento", 60], ["Aguardando documento", 25], ["Encerrado", 15]]);
            else if (chance(0.2)) sit = "Aguardando documento";
            if (dst && sit === "Encerrado") sit = "Em andamento";
            const retorno = sit === "Encerrado" ? "" : diaUtil(addDias(d, entre(10, 35)));
            const medida = demanda === "Medida protetiva" ? (k === 0 ? "Não" : "Sim") : wpick([["Não", 70], ["Sim", 15], ["Não informado", 15]]);
            const encs = [];
            if (k === 0 && chance(0.5)) encs.push(wpick([["Delegacia da Mulher", 35], ["Defensoria Pública", 35], ["CREAS", 15], ["Conselho Tutelar", 8], ["Outro", 7]]));
            const prof = caso.responsaveis.length > 1 && chance(0.3) ? caso.responsaveis[1] : resp;
            const at = { assistidaId: a.id, casoId: caso.id, data: d, hora: entre(9, 16) + ":" + pick(["00", "15", "30", "45"]), profissional: prof, tipoDemanda: demanda, medidaProtetiva: medida, processo: caso.processo, vara: caso.vara, encaminhamentos: encs, relato: k === 0 ? RELATOS_JUR[demanda === "Pensão alimentícia" ? 1 : demanda === "Divórcio" ? 2 : demanda === "Violência patrimonial" ? 4 : 0] : pick(RELATOS_JUR.slice(3)), proximoPasso: sit === "Encerrado" ? "Caso encerrado" : pick(PROXIMOS_JUR), retorno: retorno, situacao: sit, adendos: [] };
            if (chance(0.06) || (dst && k === 1)) at.adendos.push({ data: addDias(d, 1), por: prof, texto: "Correção: o número de filhos é 3, não 2." });
            JUR_ATEND.push(at);
            if (!retorno) break;
            d = retorno;
        }
    });
    JUR_ATEND.sort((x, y) => (x.data + x.hora).localeCompare(y.data + y.hora));
    const seqAno = {};
    JUR_ATEND.forEach(at => { const ano = at.data.slice(0, 4); seqAno[ano] = (seqAno[ano] || 0) + 1; at.protocolo = "CJ-" + ano + "-" + String(seqAno[ano]).padStart(6, "0"); });

    // ---------- Psicologia ----------
    ASSISTIDAS.forEach((a, i) => {
        const dst = DESTAQUES[i] || null;
        const e = a.encaminhamentos.find(x => x.tipo === "psicologia");
        if (!e) return;
        const espera = a.origem === "psicologia" ? 0 : entre(3, 26);
        let abertura = diaUtil(addDias(e.data, espera));
        if (abertura > HOJE) abertura = null;
        PSI_FILA.push({ assistidaId: a.id, encaminhadaEm: e.data, por: e.por, atendidaEm: abertura });
        if (!abertura) return;
        const resp = dst && dst.psiResp ? dst.psiResp : wpick([["PSI-000001", 55], ["PSI-000002", 45]]);
        const pr = { assistidaId: a.id, abertura: abertura, responsavel: resp, visibilidade: dst ? "responsavel" : (chance(0.15) ? "equipe" : "responsavel"), consentimento: abertura, encerramento: null, demanda: pick(DEMANDAS_PSI) };
        PSI_PRONT.push(pr);
        const alvo = dst && dst.psiSess ? dst.psiSess : entre(3, 22);
        let risco = wpick([["Baixo", 42], ["Moderado", 36], ["Alto", 16], ["Iminente", 6]]);
        const ordem = ["Baixo", "Moderado", "Alto", "Iminente"];
        let d = abertura, n = 0, total = 0;
        while (d <= HOJE && n < alvo) {
            n++;
            const presenca = n === 1 ? "Compareceu" : wpick([["Compareceu", 78], ["Faltou", 10], ["Faltou e justificou", 7], ["Remarcou", 5]]);
            const tipo = n === 1 ? "Acolhimento / triagem" : (risco === "Iminente" || (risco === "Alto" && chance(0.3)) ? "Atendimento em crise" : (chance(0.05) ? "Encaminhamento externo" : "Sessão individual"));
            if (presenca === "Compareceu" && n > 1) {
                const idx = ordem.indexOf(risco);
                const r = R();
                if (r < 0.35 && idx > 0) risco = ordem[idx - 1];
                else if (r > 0.93 && idx < 3) risco = ordem[idx + 1];
            }
                const encs = [];
            if ((risco === "Alto" || risco === "Iminente") && chance(0.35)) encs.push(wpick([["CAPS", 60], ["UBS", 20], ["Abrigo / casa de passagem", 20]]));
            else if (chance(0.05)) encs.push(pick(ENC_PSI));
            const intervalo = risco === "Iminente" ? entre(2, 4) : risco === "Alto" ? entre(5, 8) : entre(7, 15);
            PSI_SESS.push({ assistidaId: a.id, numero: n, data: d, hora: entre(8, 17) + ":" + pick(["00", "30"]), tipo: tipo, presenca: presenca, modalidade: wpick([["Presencial", 78], ["Online", 13], ["Telefone", 9]]), risco: presenca === "Compareceu" ? risco : null, demanda: n === 1 ? pr.demanda : "", evolucao: presenca === "Compareceu" ? (n === 1 ? EVOLUCOES_PSI[0] : pick(EVOLUCOES_PSI.slice(1))) : (presenca === "Remarcou" ? "Remarcou por telefone." : "Não compareceu."), plano: pick(PLANOS_PSI), proxima: addDias(d, intervalo), encaminhamentos: encs, psicologa: resp, adendos: [] });
            total++;
            d = diaUtil(addDias(d, intervalo));
        }
        if (dst && dst.psiRiscoFinal) {
            const ult = PSI_SESS[PSI_SESS.length - 1];
            if (ult.presenca !== "Compareceu") ult.evolucao = EVOLUCOES_PSI[6]; ult.presenca = "Compareceu"; ult.risco = dst.psiRiscoFinal; ult.tipo = dst.psiRiscoFinal === "Alto" ? "Atendimento em crise" : ult.tipo;
            if (dst.psiRiscoFinal === "Alto") { ult.evolucao = "Relata nova ameaça do ex-companheiro na semana. Risco reavaliado como alto; revisado plano de segurança e orientada sobre CVV 188 e SAMU 192. Informado ao Jurídico apenas o alerta de risco."; ult.encaminhamentos = ["CAPS"]; ult.plano = "Reavaliar risco na próxima sessão"; ult.proxima = addDias(ult.data, 4); }
        }
        if (dst && dst.psiEncerrar) pr.encerramento = { data: PSI_SESS[PSI_SESS.length - 1].data, motivo: dst.psiEncerrar };
        else if (!dst && n >= alvo && diffDias(d, HOJE) > 0) {
            pr.encerramento = { data: PSI_SESS[PSI_SESS.length - 1].data, motivo: wpick([["Alta", 50], ["Desistência", 25], ["Encaminhamento externo", 15], ["Mudança de cidade", 10]]) };
        }
    });
    PSI_PRONT.sort((x, y) => x.abertura.localeCompare(y.abertura));
    const seqPsi = {};
    PSI_PRONT.forEach(p => { const ano = p.abertura.slice(0, 4); seqPsi[ano] = (seqPsi[ano] || 0) + 1; p.numero = "PSI-" + ano + "-" + String(seqPsi[ano]).padStart(6, "0"); });
    PSI_SESS.forEach(s => { s.declaracao = s.presenca === "Compareceu" && chance(0.12); });

    // ---------- Assistente social (perfil ainda não existe: só fila) ----------
    ASSISTIDAS.forEach(a => {
        const e = a.encaminhamentos.find(x => x.tipo === "assistente_social");
        if (e) SOCIAL_FILA.push({ assistidaId: a.id, encaminhadaEm: e.data, por: e.por, atendidaEm: null });
    });

    // ---------- auditoria / sigilo (contagens) ----------
    const AUD_TIPOS = [["Login", 0], ["Termo de sigilo assinado", 0], ["Acesso de emergência (Jurídico)", 0], ["Acesso de emergência (Psicologia)", 0], ["Passar responsabilidades", 0], ["Ficha aberta", 0]];
    for (let k = 0; k < 900; k++) {
        const data = diaUtil(addDias("2025-09-01", Math.floor(TOTAL_DIAS * Math.pow(R(), 0.6))));
        if (data > HOJE) continue;
        const t = wpick([["Login", 70], ["Ficha aberta", 26], ["Termo de sigilo assinado", 1.6], ["Acesso de emergência (Jurídico)", 0.6], ["Acesso de emergência (Psicologia)", 0.5], ["Passar responsabilidades", 0.3]]);
        AUDITORIA.push({ data: data, tipo: t, por: pick(EQUIPE).id });
    }

    // ---------- períodos ----------
    const MESES = ["janeiro", "fevereiro", "março", "abril", "maio", "junho", "julho", "agosto", "setembro", "outubro", "novembro", "dezembro"];
    function ultimoDia(a, m) { return d2s(new Date(a, m, 0)); }
    function periodo(tipo, ref) {
        // tipo: mensal | semestral | anual | geral ; ref: "2026-10" | "2026-2" | "2026"
        if (tipo === "geral") return { tipo: tipo, ini: "2025-09-01", fim: HOJE, rotulo: "Geral (desde o início)", curto: "Geral" };
        if (tipo === "anual") { const a = +ref; return { tipo: tipo, ref: ref, ini: a + "-01-01", fim: minS(a + "-12-31", HOJE), rotulo: "Ano de " + a, curto: String(a) }; }
        if (tipo === "semestral") { const [a, s] = ref.split("-").map(Number); return { tipo: tipo, ref: ref, ini: s === 1 ? a + "-01-01" : a + "-07-01", fim: minS(s === 1 ? a + "-06-30" : a + "-12-31", HOJE), rotulo: s + "º semestre de " + a, curto: s + "º sem/" + a }; }
        const [a, m] = ref.split("-").map(Number);
        return { tipo: "mensal", ref: ref, ini: a + "-" + String(m).padStart(2, "0") + "-01", fim: minS(ultimoDia(a, m), HOJE), rotulo: MESES[m - 1].replace(/^./, c => c.toUpperCase()) + " de " + a, curto: MESES[m - 1].slice(0, 3) + "/" + String(a).slice(2) };
    }
    function anterior(p) {
        if (p.tipo === "geral") return null;
        if (p.tipo === "anual") return (+p.ref - 1) >= 2025 ? periodo("anual", String(+p.ref - 1)) : null;
        if (p.tipo === "semestral") { const [a, s] = p.ref.split("-").map(Number); const r = s === 1 ? (a - 1) + "-2" : a + "-1"; return r >= "2025-2" ? periodo("semestral", r) : null; }
        const [a, m] = p.ref.split("-").map(Number); const pa = m === 1 ? a - 1 : a, pm = m === 1 ? 12 : m - 1;
        const r = pa + "-" + String(pm).padStart(2, "0");
        return r >= "2025-09" ? periodo("mensal", r) : null;
    }
    function opcoes(tipo) {
        if (tipo === "anual") return [["2026", "2026"], ["2025", "2025 (desde setembro)"]];
        if (tipo === "semestral") return [["2026-2", "2º semestre de 2026"], ["2026-1", "1º semestre de 2026"], ["2025-2", "2º semestre de 2025"]];
        if (tipo === "mensal") { const r = []; let a = 2026, m = 10; while (a > 2025 || m >= 9) { r.push([a + "-" + String(m).padStart(2, "0"), MESES[m - 1].replace(/^./, c => c.toUpperCase()) + " de " + a]); m--; if (m === 0) { m = 12; a--; } } return r; }
        return [];
    }
    function mesesDe(p) { const r = []; let [a, m] = p.ini.split("-").map(Number); const [fa, fm] = p.fim.split("-").map(Number); while (a < fa || (a === fa && m <= fm)) { r.push({ chave: a + "-" + String(m).padStart(2, "0"), rotulo: MESES[m - 1].slice(0, 3) + "/" + String(a).slice(2) }); m++; if (m === 13) { m = 1; a++; } } return r; }
    const dentro = (s, p) => s && s >= p.ini && s <= p.fim;

    window.CASA = {
        HOJE, EQUIPE, CURSOS, ASSISTIDAS, MATRICULAS, AULAS, JUR_FILA, JUR_ATEND, JUR_CASOS, PSI_FILA, PSI_PRONT, PSI_SESS, SOCIAL_FILA, REC_EVENTOS, AUDITORIA,
        func, doPerfil, curso, periodo, anterior, opcoes, mesesDe, dentro, addDias, diffDias, s2d, d2s, MESES,
        assistida: id => ASSISTIDAS.find(a => a.id === id),
        idade: (nasc, ref) => { const r = s2d(ref || HOJE), n = s2d(nasc); let i = r.getFullYear() - n.getFullYear(); if (r.getMonth() < n.getMonth() || (r.getMonth() === n.getMonth() && r.getDate() < n.getDate())) i--; return i; }
    };
})();
