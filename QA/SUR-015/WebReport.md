# Surveyor — verificação Web de integração

Unity 6000.6.7f1, URP, IL2CPP release, Brotli nativo, perfil Leve da QA-001. Duas cenas explícitas: SurveyorArchive e SurveyorChapter. Nenhuma cena global ou outro capítulo faz parte da seleção de cenas deste build. Sem serviços externos durante a partida.

Máquina: Windows, NVIDIA RTX 3050 Laptop 6 GB. Navegadores isolados em modo headless com GPU; não se usou o perfil pessoal. Chrome for Testing 159.0.8154.6, 960×600, contexto novo, cache desabilitado, 10 Mbps/20 ms. Firefox também recebe percurso com teclado em 1280×720, sem limitação artificial de rede.

## Primeira jornada Chrome

- Todas as operações por teclado: arquivo, quatro ordens de voo/imagem, três registros guiados, escavação, pressão, impacto, eclipse e retorno após memorial. Sem completar estados por API.
- 315,64 s ativos (5min16). Carregamento 16,37 s. Zero erros de console/runtime.
- Leitura ao final: WASM usado 172.629.280 bytes; capacidade 187.760.640 bytes. JS 39.600.000 bytes informado pelo runtime. Não é pico de memória total da aba/GPU.
- Métrica do runtime ao final: movingAverageFps 56,77; não certifica FPS sustentados em todos os atos ou GPU integrada.
- Canvas limitado a 1280×720 mesmo em viewport 3840×2160. Pausa centralizada e retorno ao dossiê inspecionados.
- Essa primeira compilação precede os ajustes finais de ampliação/exposição do eclipse e o diagnóstico explícito de retorno. A jornada Firefox e a suíte Editor verificam a versão posterior. O driver registra a disponibilidade do diagnóstico para não declarar uma verificação que a primeira build não instrumentou.

## Evidências e limites

Relatórios JSON e PNGs ficam neste diretório. GetMetricsInfo é lido pela API do loader; não foi acrescentado gravador por quadro ao capítulo. O driver atual registra também chamadas WebGL draw* por callback de apresentação (inclui UI e TV), sem confundir contagem de APIs com tempo de GPU. Logs privados, binários de navegador e dependências de automação ficam em QA/Local, ignorado.

Não houve upload itch.io nesta tarefa. Iframe, foco real de janela, dispositivos integrados, memória total prolongada e cinco novatos continuam pendentes. A integração global de três capítulos é responsabilidade de INT-001, ainda depende das demais entregas. O build local da Surveyor é reproduzível pelo builder documentado.


## Jornada Firefox — revisão final SUR-015

Firefox 159.0, 1280×720: 315,29 s ativos; carregamento local sem limite de rede 2,13 s; zero erros. Retorno explicitamente confirmado pelo diagnóstico do arquivo, depois de soltar E mantido através do fim e pressioná-lo de novo. WASM usado ao final: 172.366.552 bytes / capacidade 187.760.640. O navegador não forneceu heap JS numérico, registrado como null. Amostras de chamadas de desenho estão resumidas por estágio no JSON; não representam custo da GPU. Capturas do eclipse final, memorial e retorno foram revisadas.
