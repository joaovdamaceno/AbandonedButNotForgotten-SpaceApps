# SUR-020 — controles, pouso e reencontro

Base develop 123a30b66ab2f4aba25fe94fa4030174b8cecd28; issue #204. Unity 6000.6.7f1 / URP 17.6.0. Sem alteração de Core, Boot/Dossier ou outros capítulos. Modelo da equipe, trajes, áudios e fontes existentes; nenhum asset externo novo.

## Implementado

- Primeira etapa: **Clique e arraste**. E não opera a escavação. Ao concluir e soltar, passa automaticamente para **Segure E**. Mouse não opera a pressão. Recolhimento e foto automáticos; nenhuma confirmação ou enquadramento adicional.
- Alvos dos braços calculados pelo lado anatômico real importado, não pelo nome presumido do osso. Alvos limitados ao alcance. Posição do segundo astronauta próxima da pá; câmera recuperada preserva orientação e ponto de pega. Plano final pela frente/lado, continuando na fotografia histórica sem outro salto de câmera.
- Pouso com retrofoguete e três verniers: jatos estreitos com escala em metros, descarte e dois ressaltos de aproximadamente 10 m e 3 m. Arcos a 1,62 m/s², pequenos movimentos de acomodação e grãos balísticos. Sem fumaça atmosférica. Alturas históricas seguem NASA S3-01/03/04; tempos e empuxo são montagem editorial, não simulação da telemetria real.
- Eclipse com ingresso/ocultação/saída, arco atmosférico irregular e luz da superfície reagindo. A luz retorna antes da elipse de dias para a noite lunar. Datas de eclipse, noite e último contato mudam na tela.
- Cena, prefab, tracks e referências reconstruídos e salvos. Preferências e alterações locais anteriores preservadas pelo wrapper.

## Reproduzir

Abra `Assets/_Game/Chapters/Surveyor/Scenes/SurveyorArchive.unity` e Play. Com o Editor fechado: `run_editor.ps1 -Mode Scene`, `-Mode Tests` e `-Mode Web`. Testes precisam de GPU; não executar com -nographics. O wrapper preserva as configurações/materiais locais e não escreve a lista global de cenas.

`package_web.py` gera `Builds/Surveyor3-itch.zip` e inventário em package.json. Servir `Builds/SurveyorWeb` com `python QA/WebBaseline/serve.py --root Builds/SurveyorWeb --port 8868`, que aplica Brotli/MIME. `check_web.py --browser chromium --record` e `--browser firefox` percorrem o jogo com mouse/teclado, sem saltar estados. `check_lifecycle.py` verifica dois reinícios precoces em cada navegador. Ferramentas/vídeos locais em QA/Local ficam fora da distribuição.

## Evidências e limites

**13/13 testes nativos aprovados.** `editor-results.xml` verifica os dois comandos separados, avanço ao soltar, foto automática, fluxo, pausa/foco/reentrada, braços fora do tronco, rumo/apoio da passada, transferência/retorno temporal, pressão em vala escavada, deformação em 60/144 Hz, gravidade/alturas dos arcos, escala dos jatos e recuperação da luz antes da noite. Capturas numeradas, motion/ e film/ documentam os pontos revisados; são amostras, não aprovação humana de animação.

`validation-summary.json` reúne versões, GPU reportada, tempos, amostras de FPS/memória e hashes de build dos dois navegadores. `technical-journey.txt`/`science-seconds.txt` são relógio de automação, não primeiras partidas de novatos. Brotli e ZIP são testados localmente; nenhum upload itch.io foi realizado.

Ainda faltam avaliação humana de compreensão/animação/áudio, GPU integrada, memória total da aba e teste hospedado em iframe itch.io. O traje permanece estilizado e procedural, sem captura de movimento. FPS é amostra da máquina identificada, não promessa de desempenho sustentado. Heap WASM não inclui toda a GPU/aba. O teste headless de troca de aba pode não reportar página oculta; foco/rearme também têm cobertura nativa.

A regressão intermediária `editor-light-regression.xml` preserva 12/13: o novo teste revelou normalização incorreta da ocultação, que impedia escurecimento/recuperação completos. A função foi corrigida e a bateria final passou integralmente. Não se descartou a falha nem se reduziu o critério de aprovação.
