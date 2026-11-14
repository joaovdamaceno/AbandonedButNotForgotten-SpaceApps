# SUR-002 — ambiente e rig da Surveyor 3

Cena: `Assets/_Game/Chapters/Surveyor/Scenes/Surveyor.unity`. Abrir na Unity 6000.6.7f1 e apertar Play: a cena isolada apresenta a vista da TV em 640×480, ajustada à janela sem distorcer a proporção. **É a entrega de ambiente/rig; ainda não implementa os seis atos, os minigames ou a seleção no dossiê.**

## Reconstruir e enquadrar

Menu **Abandoned Space Apps → Surveyor → SUR-002 → Reconstruir cena lunar**, ou `SurveyorSiteBuilder.BuildFromCommandLine` em batch. O módulo `surveyor.site` usa o framework CORE-006, sua raiz `[R6 SUR-002] Lunar Site`, a cena do capítulo e a pasta de arte própria. Não altera Boot, Dossier, pacotes, Build Settings ou o builder global. Reconstruções preservam os GUIDs e os IDs da instância principal, bem como raízes de outros donos.

Os menus **Enquadramentos** mostram marcas, encosta e pata, mais o detalhe da pata. São ferramentas de inspeção; não salvam outro enquadramento automaticamente. O builder restaura a vista inicial. Capturas automáticas: `SurveyorSiteAudit.CaptureFromCommandLine`, em batch com gráficos (sem `-nographics`). Imagens e relatório em `QA/SUR-002/`.

## Modelo da equipe

O usuário forneceu `files.zip` em 14/11/2026, com `surveyor3.glb` e `surveyor3_com_retrofoguete.glb`. Ambos estão preservados em `SourceAssets/Surveyor3/`, **fora de Assets e da dependência da build**. O autor individual não foi informado; crédito à equipe fornecedora, não à NASA como autora desse arquivo. As fontes NASA/JPL orientam a comparação científica.

O modelo foi comparado visualmente à reconstrução procedural inicial e escolhido por sua estrutura, treliças, câmera, cabos, painéis e braço mais detalhados. A cena usa a versão **sem retrofoguete**. A outra está apenas arquivada para SUR-006: não incluir o motor descartado na configuração assentada.

| Modelo para a superfície | Original | Cópia para Unity/Web |
| --- | ---: | ---: |
| Triângulos | 66.222 | 24.499 |
| Malhas/partes de renderização | 616 | 6 |
| Materiais | 79 | 1 |
| Imagens/mapas | 19 | 2 × 1024 px |

Conversão reproduzível: `Editor/prepare_team_model.py`, Blender 5.2.1, importação glTF, união por parte mecânica, redução de geometria, UVs de atlas separadas das UVs originais e bake dos canais. `TeamModel/conversion.json` registra os parâmetros. Atlas albedo/alpha e metallic/smoothness (R/A), material URP/Lit com recorte para as treliças. FBX em metros, conversão de eixos no importador, Read/Write desligado; sem dependência glTF em runtime ou pacote adicional.

Pivôs do espelho e da pá ficam separados da geometria estática; os três contatos ajustam a pose ao plano real da encosta. Isso é reconstrução de cena, não um modelo CAD certificado nem uma afirmação de inclinação histórica exata. Os GLBs originais não são sobrescritos pela redução.

## Cenário e contratos locais

- `LunarSite.prefab`: 40×40 m de solo próximo dividido em chunks, contexto de cratera ~210 m de diâmetro, crateras menores, marcas e rochas parcialmente enterradas. Forma autoral baseada na região; não é DEM/topografia medida. Bordas próximas são costuradas ao LOD distante.
- Texturas próprias de regolito com variação macro e micro; materiais compartilhados. Uma luz direcional com sombras, sem atmosfera, vento, poeira suspensa, SSAO ou DOF local.
- `SurveyorVisibleRig.prefab`: modelo da equipe, pivôs e âncoras em coordenadas locais. Sem laboratório, rover ou referência ao antigo Unity.
- `SurveyorSiteBindings`: disponibiliza `SUR.LandingMarks`, `SUR.CraterSlope`, `SUR.Footpad`, `SUR.Trench`, `SUR.Bearing`, `SUR.Impact`, `SUR.Eclipse`, `SUR.Visit`. Registra rigs/alvos no **CameraDirector existente**. Não lê teclas nem avança missão.
- `SurveyorSiteViewport`: um buffer de TV 640×480, liberado ao desabilitar/descarregar. Para SUR-007 substituir a apresentação, usar `DrawToGameView=false` e consumir `Texture`; desabilitar o componente libera o buffer. Não inserir HUD ou legendas dentro da textura da cena.

As referências da SUR-001 são um inventário da pesquisa naquele momento. Este documento e `TeamModel/conversion.json` registram os recursos efetivamente construídos/importados na SUR-002.

## Proveniência

- Diagramas NASA/JPL: [TR 32-1177 Part I](https://ntrs.nasa.gov/citations/19670028267), PDF pp.42, 60–61, 121 e Apêndice B, pp.183–186. Relação de painel, antena, câmera, pá, pernas e configuração pós-pouso; apêndice inspecionado antes da modelagem inicial.
- Solo/cratera: [Part II](https://ntrs.nasa.gov/citations/19670028832), PDF pp.8–11, e [SP-284](https://ntrs.nasa.gov/citations/19720019081), PDF p.186. Não usar cor/coesão como evidência de água lunar.
- Modelo e texturas incorporadas: arquivos fornecidos pelo usuário/equipe; hashes em `SourceAssets/Surveyor3/README.md`.
- Terreno, rochas e texturas de solo: geração própria nesta tarefa. Não foram baixadas imagens ou modelos de terceiros para compor o ambiente.

Resultados de testes, capturas e limitações estão em `QA/SUR-002/README.md`. Contagem geométrica e render no Editor não certificam 30 FPS em GPU integrada ou qualidade Web; esses testes continuam em QA-001/SUR-015.
