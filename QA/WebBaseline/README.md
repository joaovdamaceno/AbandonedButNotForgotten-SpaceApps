# QA-001 — baseline Web real

Base `0e4ec19`, Unity **6000.6.7f1 / URP17.6**, Windows, notebook com RTX 3050 Laptop 6GB. Build Release/IL2CPP, Brotli nativo, sem fallback JavaScript, testado em **Chrome for Testing 159.0.8154.6** e **Firefox 159.0**, perfis descartáveis headless. Browsers/ferramentas em QA/Local, sem acesso ao perfil/contas da pessoa e fora do Git.

## Entrega e medidas

Perfil próprio `Assets/_Game/Settings/Web/WebLight.asset`: escala .7, HDR/MSAA desligados, 1 luz principal com sombras 1024 / 2 cascatas / 30m, sem luzes adicionais, sem SSAO ou DOF. Renderer próprio conserva profundidade, sem alterar o URP compartilhado. Template `Assets/WebGLTemplates/SpaceApps` limita canvas a 1280×720, apresenta progresso/erro e fullscreen opcional. Build usa uma cópia da prévia de solo para provar geometria opaca, iluminação, relevo, varredura TV, E/hold, áudio e pausa, sem criar outra gameplay.

| Medida observada | Chrome | Firefox |
| --- | --- | --- |
| Inicialização | **13,55 s**, rede fria 10 Mbps / 20ms | **2,12 s**, rede local sem limitação |
| Amostra de 256 frames durante operação | 60,01 FPS, P95 18 ms | 137,04 FPS, P95 11 ms |
| Allocator Unity em uso | 78.091.886 bytes | 79.337.166 bytes |
| Memória reservada pelo allocator | 108.920.832 bytes | 108.920.832 bytes |
| Janela 3840×2160 | canvas 1280×720 | canvas 1280×720 |
| Erros de página/console observados | 0 | 0 |

**Download final: 14.711.695 bytes (14,71 MB / 14,03 MiB)**, soma dos arquivos efetivamente gerados. `build-result.txt`: sucesso, 0 erros, 1 aviso, build incremental em 1m45. O primeiro build limpo levou 13m23. JSONs incluem versões, amostras, headers e ResourceTiming. O Firefox mascara o nome da GPU como GTX980 “or similar”; isso não identifica o hardware real.

Headers conferidos: `.wasm.br` como `application/wasm`, framework como `application/javascript`, data como `application/octet-stream`, todos com `Content-Encoding: br`. HTTP em loopback é origem local; hospedagem pública deve oferecer HTTPS e os headers corretos. Não foi feito upload para itch.io.

## Comportamento verificado

- Clique em Iniciar entra na partida; E movimenta a pá e deforma o sulco. Capturas `*-world.png` inspecionadas: geometria, sombra e relevo visíveis, sem mundo invisível/magenta ou cor uniforme. O cinza é intencional do shader de TV, não um teste cromático dos outros capítulos.
- P abre o menu compartilhado centralizado; P retoma. Capturas `*-pause.png` inspecionadas.
- Nenhum buffer sonoro de duração >50ms disparou antes do clique. Depois de Iniciar/E houve disparo do loop mecânico. O AudioContext do backend já podia estar `running` antes; isso não significa que uma gravação estava tocando. Não houve escuta humana da mixagem.
- Canvas realmente redimensionado a 1280×720 em janela 4K, sem alterar o tamanho CSS para forçar 4K interno.

A primeira cópia do baseline mantinha o nome de cena do ChapterDefinition original: clicar Iniciar voltava ao arquivo. A inspeção das imagens detectou o erro, corrigido com WebBaselineChapter próprio e novo build. As capturas/JSONs versionados são da execução corrigida, não daquela tentativa.

## Reproduzir

Unity fechado: iniciar batch com `-buildTarget WebGL -executeMethod Abandoned.SpaceApps.EditorTools.WebBaselineBuilder.BuildFromCommandLine`. O método exige alvo Web importado antes de BuildPlayer. Build passa explicitamente a cena e não edita Boot, Dossier ou BuildSettings. Conservar/restaurar as configurações locais ao usar o builder fora do wrapper; o wrapper desta execução preservou byte a byte todos os ProjectSettings anteriores.

Servir: `python QA/WebBaseline/serve.py --port 8867`. Executar `check.py --browser chromium --port 8867` e `--browser firefox`. A biblioteca Playwright fica em `QA/Local/BrowserLib`, browsers em `QA/Local/Browsers`. `?qa=1` mostra o probe; ele só está na cena baseline, não nos prefabs dos capítulos.

## Limites de aprovação

Estas medidas são do baseline e desta GPU dedicada, em amostras curtas. Não certificam 30 FPS em GPU integrada, memória total da aba/GPU, tempo da campanha, cinco novatos, foco real em janela nativa ou os três capítulos. A memória do allocator não equivale ao total do processo do navegador. A edição integrada será medida separadamente na SUR-015; UI/arte e fontes compartilhadas continuam com seus donos.

Referências: [deploy Unity 6000.6](https://docs.unity.com/en-us/engine/6000.6/manual/platform-specific/webgl/building-distribution/deploying), manual local do template Minimal do Editor e `Docs/04_WEB_AND_QA.md`. Sem assets externos novos: o baseline referencia a entrega SUR-010 com os créditos existentes.
