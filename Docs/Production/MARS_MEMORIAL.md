# MAR-019 — Memorial das duas máquinas (M31–M32, 34 s)

`Prefabs/MarsMemorialStage.prefab`, `Timelines/MarsMemorial_M31.asset` (30 s) e `MarsMemorial_M32.asset` (4 s), `Art/Memorial/`. Tudo procedural; os rovers reusam `SpiritVisual` e `OpportunityVisual` (MAR-011).

## Estrutura
Blocos de 6 s: 0–6 mecanismos da Opportunity param na poeira (antes de 5,5 s); 6–12 data e contato, com a retirada do HUD (6,3 s); 12–18 retrato em fundo preto com luz de recorte sem sombra; 18–24 imagem escolhida; 24–30 díptico imóvel (Spirit/Gusev e Opportunity/Perseverance Valley). Depois, 4 s de título (SPIRIT · 2004–2010 / OPPORTUNITY · 2004–2018) sobre o mesmo díptico, sem retorno a Marte. Só a região do bloco atual fica ativa; uma câmera (a do CameraDirector); uma luz de recorte que só acende no retrato.

## Contratos
- **Memória escolhida:** `MarsMemorialDirector.SetMemory(EvidenceRecord)` recebe `MarsTransmission.SelectedRecord` (MAR-008). Mostra `Thumbnail` do registro; sem miniatura ou registro nulo, mostra o quadro de reserva e expõe `MAR.MEM.FALLBACK`. Foto do jogador só é apresentada como tal se `CanBePresentedAsPlayers`. A legenda da memória vem do `ResultKey` do registro.
- **Controles globais:** `ControlsReady` dispara uma vez, só depois do fim completo de M32 (nunca ao cancelar); a UI/ChapterDirector expõe VOLTAR AO ARQUIVO / REPETIR CAPÍTULO.
- **Textos:** `Art/Memorial/MarsMemorialTexts.json` (`MAR.MEM.*`). Nenhum texto é fala do rover. 45,16 km é "recorde na época" (MS28); anos operacionais não são datas de encerramento das buscas (MS25, MS27).

## Limitações
- Composição artística, não imagem atual literal; o díptico usa os modelos dos rovers em plataformas simples.
- Sem validação em WebGL nem em perfil leve real; legibilidade nas resoluções da UI ainda sem revisão humana; revisão visual por outra função pendente.
- Poeira do primeiro bloco é só fundo e luz (sem partículas); a atmosfera dinâmica é da MAR-016.
- Sem teste automatizado de reconstrução de cena.
