# NAR-001 — evidências

- `audio-validation.json`: 116 arquivos integralmente decodificados; duração
  compatível com os cortes revisados, sinal não vazio, amostras finitas e
  sem clipping digital. 5,95 MiB de OGG; 641,358 s de voz somada.
- `installation.json`: três catálogos e quatro cenas reais validados.
  `installation-initial.json` preserva a primeira passagem de ajuste de tempos.
- `editmode-results.xml`: **54/54 aprovados**, filtro de narração, localização,
  SequenceDirector e MarsOpeningTrackTests. Inclui pausa/foco, PT/EN, cancelamento, reentrada,
  descarte, importação e ajuste idempotente de janelas.
- `playmode-results.xml`: **2/2 aprovados**, com renderização habilitada.
  Carrega Boot e abre os três capítulos com legendas em português, aciona
  seus players, verifica pausa e liberação ao voltar ao arquivo. A repetição
  após incorporar `faef090` também passou.
- `MISSING_RECORDINGS_EN.txt`: nenhuma lacuna no manifesto congelado, após
  a inclusão das quatro gravações complementares fornecidas pelo proprietário.

## Complementos

`supplement-editmode-results.xml`: **14/14 aprovados**, catálogo com 25 falas
Surveyor, 61 Pioneer e 30 Marte, importação Web e referências nas quatro cenas.
`supplement-playmode-results.xml`: **1/1 aprovado**, com reprodução real dos
quatro novos clipes até o fim, pitch 1 e acionamento observado no AudioSource.
Não foi apenas uma chamada a Play: o teste esperou cada gravação terminar.
Os 112 OGG anteriores mantêm seus SHA-256; apenas quatro clipes foram exportados.

| Chave | Origem complementar | Intervalo (s) | Clipe (s) |
| --- | --- | --- | ---: |
| PIO.05.01b | dublagem complementar piooner.wav | 0–4,58 | 4,58 |
| PIO.05.01c | dublagem complementar piooner.wav | 4,78–8,75 | 3,97 |
| v13.years.work | dublagem complementar opportunity.wav | 0,27–6,65 | 6,38 |
| v15.storm.dark | dublagem complementar opportunity.wav | 6,97–16,32 | 9,35 |

Sem mudança no player de runtime nem atualização de develop durante a edição
dos complementos. Na etapa final, o proprietário autorizou integrar o PR #232,
resolvendo apenas os conflitos necessários com a base da equipe.

Ambiente: Windows, Unity 6000.6.7f1. Os logs locais registraram
`NAR_AUDIO_PLAYING` para `PIO.01.01`, `MAR.C01`, `v18.landing.descent` e
`sur.doc.intro.1`, indicando reprodução real no AudioSource.
Uma primeira tentativa PlayMode sem gráficos falhou em criação de RenderTexture;
o teste foi repetido com gráficos e passou. Não se ocultou essa falha por filtro.

Integração final autorizada contra `06f99ab`: preservados os novos menus,
interações, animações e áudio da equipe. A interface atual dessa base é somente
em inglês; os testes anteriores com legendas PT são registros da revisão
anterior. A narração segue os mesmos IDs e continua independente dos dados PT
preservados. Relatórios após resolver conflitos usam o prefixo `merge-`.
`merge-editmode-results.xml`: **54/54 aprovados**. `merge-playmode-results.xml`:
**3/3 aprovados**, incluindo abertura dos três capítulos, pausa/saída e os
quatro complementos reproduzidos até o fim. A fórmula de música combina a
calibração nova da equipe com o ducking da voz; no campo, o master da narração
é aplicado apenas pelo AudioListener, evitando atenuação duplicada.

## Build Web

Os relatórios Web abaixo correspondem à revisão `ac37672`, anterior aos quatro
complementos. A inclusão dos complementos foi validada por importação e
reprodução na Unity; não se repetiu o empacotamento Web nesta alteração de áudio.

A pré-verificação release herdada de CORE-006 bloqueou a tentativa antes de
compilar. Ela rejeita pacotes já presentes na base (`gltfast 6.20.0`,
`timeline 6.6.0`, `ugui 2.6.0`, `modules.director`) e exige a configuração
antiga de renderer/perfil WebLight. Não foram alterados o manifesto, os
renderers, o guard ou as configurações gráficas da equipe para satisfazer isso.

`NarrationSetup.BuildWebSmoke` continua respeitando o bloqueio. A rotina
separada `BuildWebRuntimeProbe` compila apenas para diagnóstico de runtime,
com as mesmas cinco cenas habilitadas e as configurações atuais. Um sucesso
nessa rotina não aprova uma release nem substitui o gate CORE.
O resultado efetivo da tentativa é registrado em `web-runtime-probe.json`.

A primeira build de diagnóstico compilou com sucesso, sem compressão e em
modo Development (231.579.653 bytes informados pelo BuildReport). Não é uma
medida de tamanho de release. O teste inicial em Chromium headless registrou
voz real nos três capítulos, inclusive `v18.landing.descent` e
`v15.landing.opp` na troca de cena de Marte, sem exceções JavaScript ou erros
de console. Antes do primeiro clique o AudioContext estava suspenso; após
Iniciar ficou ativo. Esse teste usa SwiftShader, portanto não valida FPS/GPU.
Registro preservado em `browser-smoke-initial.json`; arquivo de bloqueio do
gate em `release-preflight.txt`.

Repetição na build final, código `ac37672`: **aprovada** para o escopo do smoke.
`browser-smoke.json` registra seis inícios de fala entre os três capítulos,
voz inglesa com legendas em português, AudioContext ativo e nenhum erro
JavaScript/console. Pausa e retorno pelo dossiê foram usados entre as missões.
Três quadros de evidência: `surveyor-pt-en-voice.png`, `pioneer-pt-en-voice.png`
e `mars-pt-en-voice.png`. A observação foi de quadros e estados de reprodução,
não de escuta. O teste não percorre os capítulos inteiros.
`web-runtime-probe.json` registra a segunda compilação bem-sucedida, duração,
tamanho (231.590.438 bytes, Development sem compressão) e hashes dos arquivos.

Uma repetição dos testes teve falha de inicialização do serviço de licença
da Unity. Somente os processos daquela tentativa foram encerrados; a repetição
seguinte recompilou e aprovou os 54 testes EditMode e os dois PlayMode.

## Preservação do workspace

Alterações locais anteriores em configurações do Editor/material e PackageManager
foram preservadas nos dois stashes `Preserve local ... before narration ...` e
em `QA/Local/ChapterNarration/BeforeUpdate`. Não se reaplicaram configurações
antigas sobre a develop atualizada. `Deliverables/` permaneceu intocado e fora
do commit. As alterações automáticas de serialização do céu/URP durante os
testes foram copiadas para a pasta local de QA e revertidas; não fazem parte do PR.

## Ainda exige revisão humana

Escuta dos cortes, pronúncia e mixagem; reprodução completa dos três capítulos
com interação humana; compreensão e ritmo com jogadores. Não há garantia de
FPS, orçamento final de download ou aprovação artística nesta tarefa.
