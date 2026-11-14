# MAR-012 — Cratera Eagle (M05–M14)

Ambiente modular de Eagle: bacia pequena, afloramentos em degraus, esporão que esconde a McKittrick, rochas assentadas e uma rota curta orientada com dois alvos. Tudo procedural, sem imagem ou malha externa.

## Estado desta entrega (honesto)
- **Código escrito sem Unity disponível:** nada foi compilado, os testes **não foram executados**, os prefabs e os assets gerados **ainda não existem** e não há capturas. Quem tiver Unity 6000.6.7f1 precisa gerar e conferir.
- Gerar: menu `Abandoned Space Apps/Marte/MAR-012 Reconstruir Eagle` (ou `EagleBuilder` por linha de comando). Cria `Art/Eagle/Generated/`, `Art/Eagle/Preview/EaglePreview.unity` e os prefabs `EagleEnvironment`, `BlueberriesVisual` e `McKittrickVisual`. Depois rodar `MarsEagleTests` (EditMode) e `EagleCapture` (variável `EAGLE_CAPTURE_DIR`, 11 capturas) e comparar com as referências da NASA.

## Arquivos
`Art/Eagle/EagleAnchors.cs`, `EagleTargetAnchors.cs` (runtime); `Art/Eagle/Editor/EagleGeometry.cs`, `EagleBuilder.cs`, `EagleCapture.cs`; `Tests/MarsEagleTests.cs`. Reusa `ShieldFieldGeometry.Draft/Icosphere` (MAR-015).

## Contrato para a integração (MAR-003/004/005/009 e montagem)
- Quadro: origem no ponto de parada do pouso, y = 0 na planície, mesmo perfil de bacia da MAR-014 (profundidade 2,9 m, piso de raio 3 m, borda de raio 11 m).
- `EagleAnchors`: rota amostrada a ~0,5 m já assentada no terreno, índices das duas estações, câmera/alvo de visão geral, `ValidateSetup()` (corredor ≥ 2,4 m, 8,5–11,5 m entre estações, alvos a ≥ 5 m).
- `BlueberriesVisual`: `PromptAnchor`→`BlueberriesActivity.outcropAnchor`, `SoilView`→`soilView`, `RockView`→`rockView`, `StationAnchor`.
- `McKittrickVisual`: `RatSurfaceMount` (eixo Y = normal do disco, inclinado 20°, escala 1, disco de 45 mm para `RatCavitySurface`), `PromptAnchor`, `StationAnchor`, `MacroView`.
- Rota: rover de raio 0,8 m, limite de 35°, corredor de 3,2 m. A McKittrick fica oculta atrás do esporão e aparece após a curva (≈ 10 m entre as estações, M10).

## Decisões
- Solo com ondulação em quatro escalas (~22 m, 5 m, 1,2 m, 0,3 m), ripples e borda de ejecta; detalhe fino sobre o mapa de 3 cm/px.
- Afloramentos como escadas (degraus e saias enterradas), sem bancada recortada no terreno; textura de estratos única, não atlas.
- Rochas assentadas por um plano de base abaixo de todas as amostras da pegada.
- Rochas grandes têm LOD e colisor, ficam fora do corredor; pequenas só renderizam.

## Aproximações e limitações
- **Esferas ("blueberries") exageradas** (raio 5–8 mm, ilustrativo). As reais de El Capitan têm 1–2 mm e as do Berry Bowl cerca de 4 mm; não apresentar como medida.
- Alturas dos degraus, tamanhos de pedra e distâncias não vêm de fonte: são escolhas de composição.
- **Não verificado:** inclinação máxima da rota (estimada em ~25° num protótipo Python com constantes antigas; precisa ser conferida com as finais), tempos de build, orçamentos reais de triângulos, comparação visual com PIA05445, PIA05563, PIA05163, PIA05672 e o panorama "Lion King".
- Revisão por outro papel e validação WebGL pendentes.
