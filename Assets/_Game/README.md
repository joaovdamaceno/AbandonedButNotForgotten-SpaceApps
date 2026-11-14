# Assets/_Game — raiz do jogo

Criada por CORE-001 (R1). Toda pasta tem dono; consulte o README da área antes de criar arquivos.
Assemblies: `Abandoned.SpaceApps` (runtime, esta pasta), `Abandoned.SpaceApps.Editor` (`Editor/`).
**Qualquer pasta nova chamada `Editor` dentro de `_Game` precisa de um `.asmref` para `Abandoned.SpaceApps.Editor`**,
senão o código de Editor entra no assembly de runtime e quebra o build Web. O validador (`Abandoned Space Apps/Validar configuração do projeto`) acusa esse erro.
