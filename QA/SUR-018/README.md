# SUR-018 — reconstrução implementada e evidências

Base: develop `273c838`, com SUR-017 e PIO-036 já integradas. Unity 6000.6.7f1 / URP 17.6.0. Tarefa sucessora #199; preserva o histórico SUR-001…017. Não modifica Boot/Dossier/BuildSettings globais nem as cenas de outros capítulos. A extensão pequena do InputRouter foi autorizada no plano aprovado.

## Implementado

- Cena e prefab publicados montados pelo builder: Prologue → Arrival → Activation → Science → Findings → Night → Reunion → Legacy → End. Planos externos mostram a máquina; operação usa seu ponto óptico. Sem vídeos, laboratório, narração, confirmação de trajetória ou antigas tarefas de espera.
- Pouso com a mesma sonda e retroestágio entre os planos, separação, frenagem, contatos e acomodação. Um relógio ativo dirige câmera, luz, mecanismos e atores.
- Pá direta: clique e arraste ou E + WASD; baixar antes de deslocar, levantar antes de recolher, limite de alcance, deformação da trajetória real, acumulações, grãos balísticos, som por contato/carga e ensaio curto ao lado da vala. Segurar parado não escava.
- Uma fotografia 720×540 da geometria da própria partida, apresentada depois da operação; posse/liberação por EvidenceStore. Sem três fotos obrigatórias.
- Astronautas A7L completos com esqueleto e clipes Walk/Inspect/Stand, apoio dos pés e alcance das mãos. Câmera recuperada troca entre suporte e mão; pá recuperada deixa o mecanismo. A câmera inativa da Surveyor não é apresentada como registro da visita.
- Modelo da equipe com câmera separada; retroestágio da equipe; módulo lunar NASA convertido; mapa LROC; materiais e relevo, iluminação sem vento lunar, monitor monocromático e UI nítida após o efeito. Música própria em três cues, silêncio e transições.
- Roteiro causal PT/EN, legendas separadas dos comandos, datas/31 meses explícitos, foto Apollo identificada, fontes e créditos no final. Pausa/configurações preservadas, retorno e reinício pelo ciclo compartilhado.

Créditos e geração reproduzível: `SourceAssets/Surveyor3/Documentary/ASSET_CREDITS.md`. Direção e fontes: `Docs/Chapters/03_SURVEYOR_3.md` e `03_SURVEYOR_SOURCES.md`.

## Testes técnicos

- `journey-results.xml`: percurso completo aprovado, solo deformado, evidência própria, fases e final, retorno e limpeza. Relógio controlado: **309,15 s**; não é medição de novatos.
- `final-editor-results.xml`: 5/6 naquele momento; teste de dispositivo corrigido e reexecutado em `mouse-results.xml` (1/1). As três verificações de capítulo passaram, incluindo duas reentradas/solo novo, foco e amostras de animação com corpos completos. Não esconder a falha intermediária.
- `final-regression-results.xml`: 4/4, dispositivos reais do Input System, rearme por contexto/foco e equivalência de escavação a **60/144 Hz**. O teste de taxa de quadros foi acrescentado após uma falha real no Firefox: um limiar de 0,8 mm por quadro impedia a escavação em taxas altas. Agora apenas deslocamentos efetivamente nulos são descartados.
- `final-suite-results.xml`: **8/8 aprovados** após as correções, incluindo o percurso completo e uma vala que cruza a posição do ensaio. A pressão usa o apoio real da pá na borda, em vez de exigir que a ponta alcance o fundo. `headless-without-gpu-results.xml` registra uma tentativa inadequada de executar testes gráficos com `-nographics`; três falharam por RenderTexture indisponível. A execução final usa `-batchmode -force-d3d11 -buildTarget WebGL -runTests -testPlatform EditMode -testFilter "PrimaryInstrument;SurveyorSoilFrameRate;SurveyorChapterTests"`.
- `core-and-first-pass.xml`: ensaio amplo inicial, 586/588; duas falhas do harness de reconstrução/EnterPlayMode corrigidas nas execuções posteriores. Não declarar esse arquivo como suíte totalmente aprovada.
- `01…12*.png`, `motion/` e capturas dos navegadores: inspeção de enquadramentos, apoio, escavação, legendas, pausa e final. O piloto amostra movimento e não avança o jogador; a gravação técnica do navegador fica em QA/Local, fora da distribuição.
- `asset-dependencies.json`: dependências recursivas das duas cenas publicadas; sem VideoClip ou assets antigos de visita fora da fotografia de arquivo. Fontes e previews legados continuam no repositório.

Os relatórios dos navegadores (`chromium/report.json`, `firefox/report.json`) identificam versão, GPU, tempos, memória e método. Execução com teclado real por Playwright, sem SendMessage, salto de estados ou conclusão automática. `check_web.py` executa a história completa; `check_lifecycle.py` cobre **dois reinícios precoces**, pausa pelo botão e troca de aba, não duas partidas completas.

Resultado final, vinculado aos hashes do pacote por `make_report.py` / `validation-summary.json`:

| Medida | Chromium 159.0.8154.6 | Firefox 159.0 |
|---|---:|---:|
| Percurso completo com mouse e teclado | 316,24 s | 314,41 s |
| Erros de runtime registrados | 0 | 0 |
| Inicialização sem cache | 17,65 s a 10 Mbps/20 ms | 3,05 s, localhost |
| Pico observado do heap WASM utilizado | 163,66 MiB | 163,41 MiB |
| Reinícios precoces pela UI | 2 | 2 |
| Heap usado após cada reinício | 170.299.064 / 170.299.064 bytes | 170.298.440 / 170.298.440 bytes |

Máquina Windows com RTX 3050 6 GB identificada pelo Chromium. O Firefox mascara o nome como GTX 980 “or similar”; não é identificação de outra placa. Amostras de FPS durante a história: aproximadamente 59–60 no Chromium e 60–101 no Firefox, com outras ferramentas de QA concorrentes. Não são benchmark sustentado. A tela 4K manteve canvas limitado a 1280×720. Capturas de pausa em 1280×720 e arquivo em 960×600 estão nos relatórios. O teste headless de troca de aba **não reportou página oculta**; não prova sozinho a pausa por visibilidade. Foco e rearme foram aprovados nos testes nativos.

Build final: **20.201.961 bytes**, ZIP **20.001.745 bytes**, cinco arquivos, CRC aprovado, index.html na raiz. O build terminou com zero erros e três avisos; há avisos não fatais de metadados/pitch de áudio comprimido e FSR indisponível no navegador. O tratamento próprio do documentário permanece ativo. Primeiras falhas de escavação/pressão estão separadas em `firefox-first-attempt/` e `attempt-pressure/`; os relatórios atuais são da versão corrigida. Logs completos do Editor são gerados localmente e ignorados pelo Git.

## Build e reprodução

Com o Editor fechado: `./QA/SUR-018/build_web.ps1`. Usa WebGL instalado, recompõe a cena e preserva as configurações locais do projeto. `build-result.txt` registra resultado/avisos; `build-web.log` contém detalhes. `package_web.py` audita os cinco arquivos finais e cria `Builds/Surveyor3-itch.zip`, com index.html na raiz; tamanho, CRC e SHA-256 em `package.json`.

Servir para teste: `python QA/WebBaseline/serve.py --root Builds/SurveyorWeb --port 8868`. Esse servidor local configura Brotli/MIME. Bibliotecas de navegador instaladas em QA/Local são ferramentas de QA, não dependências do jogo. Scripts de teste precisam de Playwright e dos navegadores no caminho documentado no próprio script.

## Limites e revisão ainda necessária

Não houve avaliação de cinco novatos, GPU integrada nem upload real em iframe do itch.io. FPS na RTX 3050 não comprova a meta em notebooks simples. Memória WASM reportada não inclui toda a aba/GPU; dois reinícios precoces não provam ausência de vazamento numa sessão longa. Testes headless não aprovam a mixagem pela escuta.

Os astronautas são reconstruções estilizadas próprias, com animação curta e assistência por IK; não equivalem a captura de movimento ou modelos fotogramétricos. Julgamento de naturalidade, contato fino das mãos, passagem de peças, ritmo de leitura e força artística exige revisão humana contínua. Descida, iluminação e terreno são reconstituições editoriais, não reprodução métrica de todo o voo/local. O controle local simplifica comandos planejados da missão real.

O capítulo isolado está preparado para desktop com mouse/teclado. Integração final dos três capítulos e orçamento conjunto pertencem à integração R1. Não há prerender, serviço de terceiros em runtime nem build Windows novo nesta entrega.
