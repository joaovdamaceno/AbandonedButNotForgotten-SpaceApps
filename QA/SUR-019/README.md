# SUR-019 — operação curta e revisão da apresentação

Base develop fcfa4ce, sucessora da SUR-018; issue #201. Unity 6000.6.7f1 / URP 17.6.0. Sem mudanças em Core, Boot, Dossier global ou outros capítulos. Modelos, fontes, músicas e sons existentes reutilizados; não foram baixados assets nesta iteração.

## Mudanças reais

- Retirados botão/texto “Monitor: efeito de época”. TV 4:3 com moldura, vidro, curvatura, cantos, varredura e resposta monocromática mais visíveis. Redução dos efeitos permanece nas configurações.
- Corte curto, aproximadamente uma passada e parte da volta. E sozinho oferece assistência; mouse/arrasto e E + WASD continuam disponíveis. Assistência desloca o braço e precisa de contato real, sem completar a ciência por tempo parado.
- Seta na área alcançável, progresso de atividade, contraste do solo exposto, grãos, cursor de contato, estados de trabalho e feedback mecânico/conclusão. Não mostrar o comando de pressão antes de terminar o corte. Pressão reduzida de 3,5 para 1,6 s de contato; o mesmo novo hold avança e opera a etapa.
- Marcha reescrita: frente anatômica obtida dos ossos do pé, rumo aplicado depois da pose, pé de apoio parado no mundo, balanço suave dos braços e acomodação. Transições de alcance e retirada de peças interpoladas; seek para trás restaura os objetos.
- Montagem: 183 s de cinematics contra 215 s. Menos cortes redundantes no pouso, entrada pelo instrumento mais direta, inspeção visível e reencontro mais curto. Eclipse, noite e contato continuam distintos. Legendas PT/EN reescritas com fatos e relações explícitos; sem frases de encerramento genéricas.

O ritmo atual permite uma partida assistida abaixo de cinco minutos. Não houve inserção de espera para preservar o antigo mínimo. O dossiê estima 270 s; primeiros jogadores ainda precisam ser cronometrados.

## Validação nativa

`editor-results.xml`: **10/10 aprovados**. Inclui marcha orientada no sentido do deslocamento, apoio sem deslizamento no trecho medido, início suave da retirada da câmera, retorno temporal da pá, pressão sobre vala, escavação equivalente em 60/144 Hz, assistência por E sem WASD, pausa/foco, foto real, fluxo completo e reentrada.

Percurso com relógio controlado: **212,60 s**; operação/pressão/registro **26,75 s**. Não são tempos de novatos. Capturas 01…12 e amostras em motion/ foram revisadas. `editor-first-attempt.xml` preserva 9/10: a falha era uma consulta de UIDocument no objeto errado no teste, corrigida sem ignorar a exceção.

## Executar e reproduzir

Abra `Assets/_Game/Chapters/Surveyor/Scenes/SurveyorArchive.unity` e Play. Com Editor fechado: `run_editor.ps1 -Mode Scene`, depois `-Mode Tests` ou `-Mode Web`. O wrapper preserva as configurações locais e os dois materiais/configurações alterados automaticamente pelo Editor. Geradores de cena e referências continuam salvos.

O Web sai em Builds/SurveyorWeb. `package_web.py` gera Builds/Surveyor3-itch.zip, com index.html na raiz; relatório de tamanho, CRC e SHA em package.json. Servir com `python QA/WebBaseline/serve.py --root Builds/SurveyorWeb --port 8868` (Brotli/MIME corretos). Playwright e navegadores em QA/Local são ferramentas de QA, fora do jogo.

Relatórios atuais dos navegadores, se presentes, identificam build por SHA, versões, GPU reportada, tempos, erros e amostras de memória/FPS. `check_web.py` usa mouse e teclado reais, sem saltos de estado, e grava o capítulo no Chromium como evidência local. `check_lifecycle.py` verifica dois reinícios precoces; não equivale a duas partidas completas. Logs completos são locais e ignorados pelo Git.

## Resultado Web final

| Medida observada | Chromium 159.0.8154.6 | Firefox 159.0 |
|---|---:|---:|
| Percurso completo | 208,17 s | 206,76 s |
| Erros de runtime | 0 | 0 |
| Reinícios precoces | 2 | 2 |
| Heap WASM usado após cada reinício | 170.410.200 / 170.410.200 bytes | 170.409.576 / 170.409.576 bytes |
| Pico de heap WASM utilizado nas amostras | 163,52 MiB | 163,52 MiB |
| Inicialização sem cache | 18,49 s, 10 Mbps/20 ms | 5,58 s, localhost |

RTX 3050 6 GB reportada pelo Chromium; Firefox mascara o nome como GTX 980 “or similar”. Amostras durante o capítulo: aproximadamente 57–60 FPS no Chromium e 42–76 no Firefox, com QA concorrente. O canvas continua 1280×720 numa janela 4K. Não são medições em GPU integrada nem benchmark sustentado.

Build final: **20.208.693 bytes**, ZIP **20.008.667 bytes**, cinco arquivos, CRC aprovado. Zero erros de build, um aviso no build incremental final. `package.json` e `validation-summary.json` vinculam os hashes do pacote aos dois percursos completos. A correção final de uma legenda distingue a chegada da tripulação do pouso do LM; o pacote foi reconstruído antes desses testes.

## Limites

Ainda sem avaliação de novatos, GPU integrada, escuta/crítica artística por pessoas ou upload real no iframe itch.io. Movimento e encenação foram corrigidos e revisados tecnicamente; isso não representa aprovação humana de animação cinematográfica. A malha continua sendo a reconstrução estilizada do projeto, sem captura de movimento.

FPS é amostrado na máquina identificada; não é garantia sustentada. Heap WASM não inclui toda a aba/GPU. O teste headless de troca de aba pode não ocultar a página; foco/rearme também são verificados nativamente. Avisos conhecidos de metadados/pitch de áudio comprimido e FSR não implicam que os efeitos próprios de TV deixaram de executar.
