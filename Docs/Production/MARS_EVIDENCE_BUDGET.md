# Marte — orçamento de evidências e das três fotos do jogador

Decisão proposta em 14/11/2026 (a partir do fechamento da MAR-006); **precisa de aceite de R1/R2**, que são donos do `EvidenceStore` e da galeria (CORE-005, UI-007). Não altera contrato: usa só os tipos já existentes.

## O problema

`EvidenceStore` aceita no máximo **3 fotografias do jogador** por capítulo (`MaxPlayerPhotos`, de `Docs/04_WEB_AND_QA.md`) e **lança** na quarta. O roteiro de Marte prevê cinco capturas:

| Etapa | Captura | Quem registra |
| --- | --- | --- |
| M17 | escudo descartado | MAR-006 |
| M19 | registro visual do meteorito | MAR-006 |
| M25 | Sol de referência | MAR-008 |
| M26 | vale (obrigatória, vai ao memorial) | MAR-008 |
| M28 | Sol atenuado pela poeira | MAR-008 |

## Regra

As **três fotos do jogador** de Marte são exatamente:

1. **Escudo** (M17) · `Photo` / `PlayerCapture`
2. **Meteorito** (M19) · `Photo` / `PlayerCapture`
3. **Vale** (M26) · `Photo` / `PlayerCapture` · **vaga reservada** (a MAR-008 não pode ficar sem ela: é a imagem do memorial)

As duas observações do Sol (M25 e M28) **não são fotos do jogador**: viram um único **registro científico de comparação**, `Observation` / `Reconstruction`, com miniatura de até 960×540 e as duas imagens lado a lado ("Os dois registros do Sol têm imagens e comparação", MAR-008). Isso casa com M30, onde a escolha da memória é entre **a foto do vale feita pelo jogador** e **o registro científico**.

Consequências:

- Imagens de arquivo (PIA07402, Heat Shield Rock) são `Photo` / `Archive` com crédito: **não contam** nas três.
- O MI das esferas (MAR-003) e a composição do meteorito (MAR-006) são `Observation` / `Composition`: **não contam**.
- A galeria (UI-007) continua mostrando até três fotos do jogador; o registro científico aparece como painel de resultado, não como "sua foto".
- Se a MAR-008 precisar de uma quarta foto do jogador, a regra deve ser revista aqui, com R1/R2, **antes** de implementar: a quarta lança `InvalidOperationException`.

## Resultado esperado no `EvidenceStore` ao fim do capítulo

| ID | Tipo | Autoria | Conta nas 3? |
| --- | --- | --- | --- |
| `mars.ev.spheres.soil`, `mars.ev.spheres.rock` | Observation | Reconstruction | não |
| `mars.ev.spheres.hematite` | Composition | Reconstruction | não |
| `mars.ev.shield.photo` | Photo | PlayerCapture | **sim (1)** |
| `mars.ev.shield.archive` | Photo | Archive | não |
| `mars.ev.meteorite.composition` | Composition | Reconstruction | não |
| `mars.ev.meteorite.photo` | Photo | PlayerCapture | **sim (2)** |
| `mars.ev.meteorite.archive` | Photo | Archive | não |
| _(MAR-008)_ foto do vale | Photo | PlayerCapture | **sim (3, reservada)** |
| _(MAR-008)_ registro científico do Sol | Observation | Reconstruction | não |

## Como a regra é protegida

`MarsEvidenceBudgetTests` (em `Assets/_Game/Chapters/Mars/Tests/`) registra no `EvidenceStore` real todos os registros que as atividades de Marte hoje produzem e exige que usem **no máximo duas** fotos do jogador (uma vaga fica para o vale). Quem adicionar outra foto do jogador antes da MAR-008 quebra o teste e precisa rever esta decisão.
