# SUR-012 — validação focada

Base develop `55bb487`, Unity 6000.6.7f1 / URP 17.6. A tarefa entrega LunarNight → TimePassage → ImaginedVisit, em 30 segundos. Revisão própria autorizada pelo proprietário em AGENTS.md; nenhuma aprovação humana foi simulada.

## Executar

Abrir `Assets/_Game/Chapters/Surveyor/Sequences/Night/Preview/SurveyorNightPreview.unity`, Play e Iniciar. Pausa/preferências/foco reutilizam a UI-006. Builder: `Abandoned.SpaceApps.EditorTools.SurveyorNightBuilder.BuildFromCommandLine`.

Suíte: `-runTests -testPlatform EditMode -testFilter Abandoned.SpaceApps.Chapters.Surveyor.NightTests -testResults QA/SUR-012/tests.xml`.

## Evidências

- Dez testes focados: progressão única em 30s, câmera imóvel, leitura dos quatro cues, pausa antes da elipse, reinício, cancelamento, descarte sem sobrescrever o próximo consumidor, calendário, quatro transições de luz com movimento reduzido e liberação de fotos preservando metadados/arquivo.
- Play Mode real: último contato em PT, calendário 1968, novembro de 1969 em EN, HUD oculto, pausa retomada, duas reproduções e liberação dos buffers. Capturas PNG desta pasta; resultados em tests.xml.
- Reconstrução repetida preserva os GUIDs das duas sequências, prefab e cena.

## Limites

Não foi gerado um novo build Web nesta tarefa: ela reutiliza câmera, TV, materiais e UI existentes e acrescenta sequências/iluminação. A validação ampla no navegador e da campanha completa pertence à SUR-015/INT-001. Desempenho Web e entendimento por pessoas sem experiência não estão aprovados por esses testes de Editor.

A prévia mostra a área da trincheira com a pá recolhida; o sulco jogável é da SUR-009, ainda com outro responsável. O epílogo Apollo é da SUR-013. Música continua responsabilidade do áudio global. Fatos, tempos e contrato de integração: `Docs/Production/SURVEYOR_NIGHT.md`.
