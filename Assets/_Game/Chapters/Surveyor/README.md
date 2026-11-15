# Capítulo III — Surveyor 3 — R6

Edição SUR-019: documentário jogável em tempo real, operação curta assistida, marcha com apoio e reencontro com a Apollo 12. Roteiro e limites: `Docs/Chapters/03_SURVEYOR_3.md`.

Abra com **Unity 6000.6.7f1**. Abra `Scenes/SurveyorArchive.unity`, aperte Play e inicie a missão. A cena `SurveyorChapter` isolada redireciona para esse arquivo para manter o ciclo correto de início, pausa, reinício e descarte. Não precisa montar GameObjects manualmente.

- Clique e arraste seguindo a seta para baixar e deslocar a pá. Solte para levantar. Progresso, cor do corte, grãos, cursor e som mostram o efeito.
- Alternativa acessível: **segure E**, sem precisar mover outra tecla. O braço executa o corte assistido e continua dependendo de contato/movimento reais. E + WASD permite ajustar manualmente.
- Quando aparecer **Vala aberta**, solte e segure E novamente para pressionar a borda. Depois use E ou o botão para fotografar. O registro é da geometria desta partida.
- **P/Esc** ou o botão **P** pausam. Idioma, volumes, tamanho de texto e redução de movimento/efeitos ficam nas configurações. O monitor não tem rótulo ou botão de efeito sobre a imagem.

O menu **Abandoned Space Apps / Surveyor / Reconstruir capítulo completo** reconstrói a composição pelo builder existente. Os assets e referências já estão salvos; os geradores Blender só são necessários ao alterar os modelos. Veja `SourceAssets/Surveyor3/Documentary/ASSET_CREDITS.md`.

Para gerar a edição Web isolada, execute `QA/SUR-019/run_editor.ps1 -Mode Web` com o Editor fechado. O script usa o módulo Web instalado e preserva as configurações locais do projeto. A saída fica em `Builds/SurveyorWeb`; servir por HTTP com suporte a Brotli, nunca abrir `index.html` diretamente. O ZIP tem `index.html` na raiz. Não altera Boot/Dossier ou a campanha de outro capítulo.

Execute `QA/SUR-019/run_editor.ps1 -Mode Tests` após reconstruir (`-Mode Scene`). Testes em `Chapter/Tests/SurveyorChapterTests.cs` e `Core/Tests/EditMode/PrimaryInstrument*Tests.cs`. Evidências e limites em `QA/SUR-019/README.md`. Testes técnicos não substituem a avaliação de animação em movimento nem as partidas de novatos.
