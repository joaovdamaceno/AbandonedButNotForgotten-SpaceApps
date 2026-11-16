# Scenes — INT-001 (R1)

`Boot.unity`. Somente R1 edita esta cena e `EditorBuildSettings`. Cenas de capítulo ficam na pasta de cada capítulo.

**Não existe `Dossier.unity`** (decisão do dono em 15/11/2026): o dossiê é instanciado pelo `GameHost` dentro do Boot e o AudioListener/câmera do Boot fazem o papel de "cena do menu".

## Build Settings (exatamente estas cinco, nesta ordem)

| # | Cena | Quem carrega |
| --- | --- | --- |
| 0 | `Scenes/Boot.unity` | Player (shell, serviços e dossiê) |
| 1 | `Chapters/Mars/Scenes/Mars.unity` | `ChapterDirector` (definição `mars`) |
| 2 | `Chapters/Mars/Opportunity/Scenes/Opportunity.unity` | `MarsFieldHandoff`, em modo aditivo, dentro de Marte |
| 3 | `Chapters/Pioneer10/Scenes/Pioneer10.unity` | `ChapterDirector` (definição `pioneer10`) |
| 4 | `Chapters/Surveyor/Scenes/SurveyorChapter.unity` | `ChapterDirector` (definição `surveyor3`) |

Ficam **fora**: `Pioneer10_Playtest.unity` (host de teste do Editor), `SurveyorArchive.unity` (entrada isolada: cria os próprios serviços e duplicaria os do `GameHost`), `Surveyor.unity` (ambiente do SUR-002), qualquer `*Preview.unity` e `ContractExample.unity`.

## Como regravar

`Abandoned Space Apps > INT-001 > Montar Boot jogável (Dossiê → três capítulos)` (ou `-executeMethod Abandoned.SpaceApps.Integration.EditorTools.PlayableBootBuilder.BuildFromCommandLine`). O builder é idempotente: monta `Boot.unity`, atribui `PioneerChapter.asset` e `SurveyorChapter.asset` ao `ChapterCatalog.asset` (sem tocar na pasta de Marte nem em entradas de outras pessoas), grava as cinco cenas e roda a validação do dossiê (`DossierAssetsBuilder.Validate`).

## Como o Boot liga cada capítulo (`Assets/_Game/Integration/`)

- `GameHost` instala os serviços únicos da sessão (relógio, entrada, interação, evidências, diretor, configurações, localização, HUD, legendas, guia, pausa, áudio, dossiê).
- `ChapterSceneLoader` carrega a cena do capítulo em modo aditivo (uma só por vez) e dá um gancho **antes do primeiro Start** da cena.
- `PioneerBindings` chama `PioneerChapterController.Initialize(...)` com o áudio global e liga a legenda global à `SequenceDirector` da Pioneer; `Teardown()` ao descarregar.
- `SurveyorBindings` entrega os serviços ao `SurveyorDocumentaryHost` no gancho (sem isso o host se trata como execução isolada e troca a cena inteira pelo `SurveyorArchive`).
- Um só `AudioListener` ativo: o do Boot liga no dossiê e na abertura de Marte (que não traz ouvinte) e desliga quando um capítulo ativa o seu.
- Um só menu de pausa ouvindo o P: o Surveyor traz o dele, então o do Boot fica suspenso enquanto ele roda.
