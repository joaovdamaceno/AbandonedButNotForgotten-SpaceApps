# Capítulo III — Surveyor 3 — R6

Reconstrução SUR-018: documentário jogável em tempo real, com planos externos, operação direta da pá e reencontro com a Apollo 12. Roteiro e limites: `Docs/Chapters/03_SURVEYOR_3.md`.

Abra com **Unity 6000.6.7f1**. Abra `Scenes/SurveyorArchive.unity`, aperte Play e inicie a missão. A cena `SurveyorChapter` isolada redireciona para esse arquivo para manter o ciclo correto de início, pausa, reinício e descarte. Não precisa montar GameObjects manualmente.

- Clique e arraste no monitor para baixar e deslocar a pá. Solte para levantá-la; repita sobre a mesma vala para aprofundá-la. Segurar sem deslocar não escava.
- Alternativa: mantenha **E + WASD**. Solte E antes de confirmar a etapa seguinte.
- Depois da escavação, examine as bordas, mantenha contato para o ensaio curto e fotografe o resultado real. A fotografia é apresentada na sequência seguinte.
- **P/Esc** ou o botão **P** pausam. Idioma, volumes, tamanho de texto e redução de movimento ficam nas configurações. O tratamento do monitor pode ser reduzido no botão da própria operação.

O menu **Abandoned Space Apps / Surveyor / Reconstruir capítulo completo** reconstrói a composição pelo builder existente. Os assets e referências já estão salvos; os geradores Blender só são necessários ao alterar os modelos. Veja `SourceAssets/Surveyor3/Documentary/ASSET_CREDITS.md`.

Para gerar a edição Web isolada, execute `QA/SUR-018/build_web.ps1` com o Editor fechado. O script usa o módulo Web instalado e preserva as configurações locais do projeto. A saída fica em `Builds/SurveyorWeb`; servir por HTTP com suporte a Brotli, nunca abrir `index.html` diretamente. O ZIP tem `index.html` na raiz. Não altera Boot/Dossier ou a campanha de outro capítulo.

Os testes estão em `Chapter/Tests/SurveyorChapterTests.cs` e `Core/Tests/EditMode/PrimaryInstrument*Tests.cs`. Evidências, comandos e limitações em `QA/SUR-018/README.md`. Testes técnicos não substituem a avaliação de animação em movimento nem as partidas de novatos.
