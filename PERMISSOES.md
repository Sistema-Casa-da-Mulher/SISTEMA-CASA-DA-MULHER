# Matriz de Permissoes

Este documento organiza o que cada perfil pode acessar no sistema Casa da Mulher.

Importante: esconder botoes no front-end melhora a usabilidade, mas nao e a seguranca principal. A seguranca real fica no back-end, com policies e autorizacao por perfil.

## Perfis

| Perfil | Uso esperado |
| --- | --- |
| `adm` | Coordenacao/administracao, com acesso total. |
| `recepcao` | Primeiro atendimento, cadastro inicial e triagem sem prontuarios confidenciais. |
| `professor` | Atividades de curso, frequencia e informacoes educacionais. |
| `as_social` | Acompanhamento social/psicologico e prontuario social. |
| `juridico` | Atendimento juridico e orientacao legal. |
| `psicologia` | Atendimento psicologico e prontuario psicologico (CRP obrigatorio no convite). |

## Matriz

| Funcionalidade | ADM | Recepcao | Professor | Social/Psicologia | Juridico |
| --- | --- | --- | --- | --- | --- |
| Criar convites de funcionarios | Sim | Nao | Nao | Nao | Nao |
| Gerenciar funcionarios | Sim | Nao | Nao | Nao | Nao |
| Ver historico de acoes | Sim | Nao | Nao | Nao | Nao |
| Ver logs de e-mail | Sim | Nao | Nao | Nao | Nao |
| Cadastrar usuaria/atendimento inicial | Sim | Sim | Nao | Sim | Sim |
| Ver dados basicos | Sim | Sim | Sim | Sim | Sim |
| Ver prontuario social | Sim | Nao | Nao | Sim | Nao |
| Ver atendimento juridico (conteudo) | Nao, so metadados | Nao | Nao | Nao | Sim (conforme visibilidade do caso) |
| Registrar atendimento juridico / adendo | Nao | Nao | Nao | Nao | Sim |
| Marcar acesso restrito | Sim | Nao | Nao | Nao (quando houver modulo) | Sim |
| Ver assistida com acesso restrito | Sim | Nao | Nao | Nao | Sim |
| Ver frequencia/curso | Sim | Nao | Sim | Nao | Nao |
| Gerar relatorios gerais | Sim | Nao | Nao | Sim | Sim |

## Policies do back-end

| Policy | Perfis permitidos | Uso |
| --- | --- | --- |
| `SomenteAdm` | `adm` | Administracao, convites, funcionarios e historico de acoes. |
| `AcessoRecepcao` | `adm`, `recepcao` | Cadastro inicial, triagem e recepcao. |
| `AcessoCursos` | `adm`, `professor` | Cursos, frequencia e atividades educacionais. |
| `AcessoProntuarioSocial` | `adm`, `as_social` | Prontuario social e acompanhamento. |
| `AcessoJuridico` | `adm`, `juridico` | Atendimento juridico. |
| `AcessoPsicologia` | `adm`, `psicologia` | Psicologia. A coordenacao so acessa os indicadores. |
| `AcessoRelatorios` | `adm`, `as_social`, `juridico` | Relatorios gerais permitidos a coordenacao e areas tecnicas. |
| `CadastroAssistidas` | `adm`, `recepcao`, `as_social`, `juridico`, `psicologia` | Cadastro unico das assistidas (`/api/assistidas`). |

## Areas do front-end

O helper `CasaMulherAuth.podeAcessar(area)` usa as mesmas areas conceituais:

| Area | Perfis permitidos |
| --- | --- |
| `convites` | `adm` |
| `funcionarios` | `adm` |
| `auditoria` | `adm` |
| `emails` | `adm` |
| `recepcao` | `adm`, `recepcao` |
| `cursos` | `adm`, `professor` |
| `social` | `adm`, `as_social` |
| `juridico` | `adm`, `juridico` |
| `psicologia` | `adm`, `psicologia` |
| `relatorios` | `adm`, `as_social`, `juridico` |

## Cadastro unico e Juridico

- As assistidas ficam no servidor (`/api/assistidas`), nao mais no `localStorage` do navegador. Recepcao e Juridico usam a mesma ficha; quem cadastrou fica registrado (`origem`: `recepcao` ou `juridico`).
- A professora so recebe as alunas do curso vinculado (`/api/assistidas/curso`), sem assistidas com acesso restrito e sem telefone de quem pediu para nao receber ligacao.
- **Acesso restrito**: a assistida some das listas e buscas da Recepcao, Professor e demais perfis. Se alguem digitar o CPF dela ao cadastrar, o sistema so avisa que existe cadastro restrito, sem mostrar dados. Marcar e desmarcar exige motivo e fica na auditoria.
- **Responsaveis e visibilidade**: cada caso juridico pode ter uma ou mais responsaveis. As responsaveis escolhem se o conteudo e visivel para toda a equipe do Juridico (padrao) ou somente para elas.
- **Acesso de emergencia**: em caso "somente responsaveis", outra profissional do Juridico pode pedir acesso informando o motivo. O acesso vale 12 horas, fica na auditoria e as responsaveis recebem aviso por e-mail (sem nome da assistida).
- **Coordenacao (ADM)** ve apenas metadados dos atendimentos (protocolo, data, profissional, situacao), nunca o relato.
- Atendimentos tem protocolo sequencial gerado no servidor (`CJ-AAAA-NNNNNN`) e nao podem ser editados nem apagados; correcoes entram como adendo.
- Leitura de atendimentos juridicos tambem e auditada (`JURIDICO_CASO_VISUALIZADO`).

## Psicologia

- Perfil proprio `psicologia` (ID `PSI-000001`), separado do `as_social`. O convite exige o CRP, que vai para a conta e para a declaracao de comparecimento.
- Recepcao, Juridico e a propria Psicologia podem cadastrar e encaminhar; o cadastro e a mesma ficha unica.
- Prontuario por assistida com numero gerado no servidor (`PSI-AAAA-NNNNNN`) e sessoes numeradas. Sessao salva nao e editada nem apagada; correcoes entram como adendo. O prontuario nunca e excluido (guarda minima de 5 anos), so encerrado.
- **Visibilidade padrao: somente a psicologa responsavel.** A responsavel pode abrir para a equipe de Psicologia. Outras psicologas so veem que a sessao existiu (numero, data, profissional) e podem pedir acesso de emergencia (12 horas, auditado, responsaveis avisadas).
- Primeira sessao com comparecimento exige registrar que a assistida foi informada sobre o sigilo e seus limites.
- Classificacao de risco em cada sessao. Risco alto ou iminente gera alerta na ficha com condutas e contatos (CAPS, SAMU 192, CVV 188); o alerta sai quando um risco menor e registrado. Nada e enviado para fora do sistema.
- **Juridico** ve apenas "em acompanhamento na Psicologia" e o alerta de risco, sem conteudo. **Recepcao** nao ve nada da Psicologia. **Coordenacao** ve so os indicadores (numeros sem nome).
- Declaracao de comparecimento impressa sem nenhum conteudo da sessao.
- Sem campo de diagnostico/CID; grupos de apoio ficam para uma fase futura.
