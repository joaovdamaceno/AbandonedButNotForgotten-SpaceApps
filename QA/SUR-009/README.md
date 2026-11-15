# SUR-009 — pá, trincheira e recolhimento seguros (14/11/2026)

Base: `5f316f5` (develop em 14/11/2026), branch `sur-009-abrir-trincheira`. Unity **6000.6.9f1** (o projeto fixa 6000.6.7f1; é o único Editor instalado nesta máquina), URP 17.6, Windows/D3D11.

**Estado: comportamento, prefabs e testes concluídos e verificados no Editor; evidência visual só do Editor. A série automática de capturas WebGL não foi concluída, e o capítulo ainda não está integrado.** Não declarar a issue fechada: ver "O que falta".

## O que foi entregue

- `Assets/_Game/Chapters/Surveyor/Scripts/SurveyorScoop.cs`: fases Approaching → Ready → Digging → Retracting → Complete; hold de E de 12 s com 4 passes e 12 mordidas de 1 s; soltar E ou pausar congela deformação e áudio e conserva o avanço; profundidade máxima 18 cm; recolhimento em três fases (sobe 8 cm, recua de lado, repouso); `Cancel()` e sair da etapa usam a pose segura; grãos balísticos (máx. 24, gravidade lunar); cues SUR-S14/S15; `ValidateSetup`, `ConfigurePreview`, `PreviewDig`, `PreviewRetract`.
- `Assets/_Game/Chapters/Surveyor/Art/Trench.prefab` e `Art/ScoopRig.prefab` (+ `Art/ScoopGenerated/`: malhas, dois materiais derivados do regolito, laço de motor sintetizado), gerados por `Scoop/Editor/SurveyorScoopBuilder.cs`. Reconstruir preserva GUIDs.
- Apoio: `Scoop/Tests/` (27 testes), `Scoop/Preview/` (cena de preview e driver de URL), `Scoop/Editor/SurveyorScoopPreviewBuilder.cs` (cena, capturas no Editor, build WebGL), `Scoop/README.md` (contrato de uso).

## Verificação realizada

**Compilação e testes no Unity (batch).** O Smart App Control do Windows bloqueava o `Bee.DotNet.dll` do Unity e foi desativado pela pessoa responsável; depois disso o Unity compilou o projeto. [`editmode.xml`](editmode.xml): **27/27 passaram** (13 de matemática pura e 14 de fluxo com `ChapterDirector`, `SessionClock`, `InputRouter` e `InteractionService` reais, E simulado pelo Input System). Cobrem: quatro passes por 12 s de hold; soltar congela e conserva o avanço; pausa e retomada exigem novo pressionamento; E mantido da ação anterior não inicia a escavação; mudança visível a cada ≤ 1,2 s e grãos em orçamento; a ponta e o balde nunca ficam abaixo do relevo deformado ao escavar e recolher; recolhimento vertical → lateral → repouso; cancelar em contato e a 90 %; sair da etapa mantém o sulco e reiniciar restaura o terreno; S14 e S15 em ordem sem sobreposição; âncora e centro da foto. Antes dessa execução os 13 de matemática também rodaram fora do Unity (`math-harness-output.txt`, Roslyn do Unity), com o mesmo resultado.

**Prefabs.** `SurveyorScoopBuilder.BuildFromCommandLine` gerou `Trench.prefab` e `ScoopRig.prefab` e a validação do builder passou: 4 blend shapes (`Pass1..4`), remendo ≤ 4 000 vértices, 24 grãos, motor com clipe em laço, nenhum material ausente, caminhos de nomes do contrato. O builder também confere que o sulco não toca a borda do remendo.

**Capturas do Editor.** [`Captures/Editor/`](Captures/Editor/): 44 PNGs 960×540, escavação e recolhimento a cada 10 %, com câmera baixa e câmera da TV; [`capture-report.txt`](Captures/Editor/capture-report.txt) lista profundidade (0,180 m no fim), folga da ponta (≥ 0,0005 m durante a escavação) e erro de alcance (0). Inspeção visual feita: cavidade com bordas elevadas, sem emenda visível com o terreno depois de igualar o material do remendo ao regolito.

**WebGL.** Um build WebGL só da cena de preview concluiu com sucesso (17,5 MB, 0 erros, 3 avisos, ~21 min, IL2CPP). Rodou no navegador embutido com D3D11/ANGLE: a cena carregou, o console registrou `SUR-009 preview dig=0.30 cam=low depth=0.060 clearance=0.0005 reach=0.0000 ground=Chunk_2_2` e a trincheira e o braço aparecem. O aviso `Edge Adaptive Spatial Upsampling ... stripped` (FSR) vem do perfil de URP existente. **Não há série de capturas WebGL**: o painel do navegador era pequeno demais para screenshots úteis, e o segundo build (com captura automática dentro do jogo, `?auto=1`) foi interrompido para liberar o projeto. O build já gerado em `Builds/SUR-009-Web` (ignorado pelo git) não tem a captura automática.

## Correções feitas durante a validação

- O asmdef dos testes não referenciava `Unity.InputSystem`/`Unity.InputSystem.TestFramework` (erro de compilação real).
- Soltar e apertar E de novo não retomava o avanço: `OnHoldStarted` só aceitava a fase `Ready`. Corrigido para aceitar `Digging` (os dois testes que falharam passaram a verde).
- `PreviewRetract` usava passes do instante anterior; agora fixa 12 s (afetava só a leitura de profundidade e folga das capturas).
- O remendo do sulco estava mais escuro que o terreno e formava um retângulo visível; o material agora é o do regolito.
- Valor inicial do balde penetrava o relevo em 0,2 µm; adicionada margem de 0,5 mm e mais amostras do balde.

## Critérios do card

| Critério | Situação |
| --- | --- |
| Antes/depois com cavidade e bordas consistentes | Evidência nas capturas do Editor; série WebGL pendente; revisão humana pendente |
| Ferramenta não atravessa a superfície ao recolher | Testado (balde inteiro, 2 400 instantes + todos os 10 % de recolhimento); os elos do braço não são testados contra o terreno |
| Sem avanço com E solto | Testado |
| Foto de resultado na mesma área escavada | Só a base: `PhotoTarget` no centro do sulco e `FootprintWorldBounds`; a foto é da SUR-010 |
| Capturar poses a cada 10 % | Feito no Editor (44 imagens); WebGL pendente |
| Cancelar e pausar em contato e a 90 % | Testado |
| Inspecionar em ângulo baixo | Feito no Editor |

## O que falta antes de fechar a issue

1. Série de capturas WebGL (rodar o build do menu `SUR-009 → Build WebGL do preview` e o preview com `?auto=1` servido por `tools/serve-web.ps1`, que grava as imagens em `Captures/Web/`), com o painel do navegador visível.
2. **Integração:** ligar `SurveyorScoop.Configure` na cena do capítulo (R1/SUR-015) com o chunk `NearGround_40m/Chunk_2_2` e `Team_ScoopLinkage`/`Team_ScoopTool`. Nada disso está na cena `Surveyor.unity`.
3. **Decisão visual a aprovar:** o braço do `ScoopRig` é uma reconstrução leve de dois elos com cinemática inversa que esconde a pá nativa do modelo da equipe (SUR-002), porque a malha nativa é rígida e não alcança os pontos de escavação e recolhimento. A alternativa é manter a pá nativa como balde.
4. A câmera da TV está atrás do braço: nas capturas `tv` os elos tapam parte da vista (composição de SUR-008, não da escavação).
5. Nunca verificados: som do motor (só a regra `MotorShouldPlay`), aparência dos grãos em imagem, textos PT/EN de S14/S15 (SUR-014), desempenho em GPU integrada e revisão humana.
6. Efeitos colaterais do Unity 6000.6.9f1 no git que não pertencem à SUR-009: `ProjectSettings/ProjectVersion.txt`, `GraphicsSettings.asset`, `QualitySettings.asset` e `ProjectAuditorSettings.asset`. Não commitar.

## Reproduzir

- Menu **Abandoned Space Apps → Surveyor → SUR-009**: *Reconstruir pá e trincheira*, *Criar cena de preview*, *Capturar poses no Editor*, *Trocar plataforma para WebGL*, *Build WebGL do preview*.
- Batch (feche o Editor antes): `tools/unity-batch.ps1 -Method <método> -LogName <log>`; testes: `Unity -batchmode -projectPath <raiz> -runTests -testPlatform EditMode -assemblyNames Abandoned.SpaceApps.Surveyor.Scoop.Tests -testResults QA/SUR-009/editmode.xml`.
- `tools/compile-check.ps1`, `tools/run.ps1` e `tools/flowcheck.ps1` foram a verificação fora do Unity enquanto o Windows bloqueava o compilador; têm caminhos da máquina em que foram escritos.
- `logs/` guarda os logs dos batches deste dia.

## Fontes

Roteiro `Docs/Chapters/03_SURVEYOR_3.md` (ato 4, 02:31–03:13; SUR-S14/S15), relatório JPL TR 32-1177 Part I (<https://www.nasa.gov/wp-content/uploads/static/history/alsj/a12/Surveyor-III-MIssionRpt1967028267.pdf>) e <https://science.nasa.gov/mission/surveyor-3/>. A forma do sulco, o braço de dois elos e as medidas de arrasto são reconstrução própria; não são medidas da missão nem CAD. Nenhuma foto histórica da pá/trincheira (R-SUR-02) foi localizada ou importada. Não foi publicado nada no itch.io.
