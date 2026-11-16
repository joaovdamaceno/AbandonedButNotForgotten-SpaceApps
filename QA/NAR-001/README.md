# NAR-001 — evidências

- `audio-validation.json`: 112 arquivos integralmente decodificados; duração
  compatível com os cortes revisados, sinal não vazio, amostras finitas e
  sem clipping digital. 5,73 MiB de OGG; 617,078 s de voz somada.
- `installation.json`: três catálogos e quatro cenas reais validados.
  `installation-initial.json` preserva a primeira passagem de ajuste de tempos.
- `editmode-results.xml`: **50/50 aprovados**, filtro de narração, localização
  e SequenceDirector. Inclui pausa/foco, PT/EN, cancelamento, reentrada,
  descarte, importação e ajuste idempotente de janelas.
- `playmode-results.xml`: **2/2 aprovados**, com renderização habilitada.
  Carrega Boot e abre os três capítulos com legendas em português, aciona
  seus players, verifica pausa e liberação ao voltar ao arquivo.
- `MISSING_RECORDINGS_EN.txt`: quatro falas presentes nas legendas atuais,
  ausentes nas gravações fornecidas; seguem sem dublagem.

Ambiente: Windows, Unity 6000.6.7f1. Os logs locais registraram
`NAR_AUDIO_PLAYING` para `PIO.01.01`, `MAR.C01`, `v18.landing.descent` e
`sur.doc.intro.1`, indicando reprodução real no AudioSource.
Uma primeira tentativa PlayMode sem gráficos falhou em criação de RenderTexture;
o teste foi repetido com gráficos e passou. Não se ocultou essa falha por filtro.

## Build Web

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

## Ainda exige revisão humana

Escuta dos cortes, pronúncia e mixagem; reprodução completa dos três capítulos
com interação humana; compreensão e ritmo com jogadores. Não há garantia de
FPS, orçamento final de download ou aprovação artística nesta tarefa.
