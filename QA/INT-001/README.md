# QA/INT-001 — Integração dos três capítulos ao Boot (PR 1)

Escopo desta branch: blocos **A, B, C, D e H** do card INT-001 (catálogo, Build Settings, `GameHost` com Pioneer 10 e Surveyor 3, dono único de câmera/áudio/pausa, descarte e testes) **mais o início do G**: inglês como idioma padrão nos três capítulos, com troca pelas Configurações. O dono decidiu unificar isso na mesma branch (o card previa dois PRs).

**Ainda não feito:** o bloco **F** (QA absorvidos de MAR-010, MAR-020 e PIO-015) e a unificação visual (ver "Idioma" abaixo). UI-007 está fora do card.

## Idioma (bloco G)

- `SettingsData.languageCode` padrão e o fallback de valor inválido agora são `en`. Quem já tem preferência salva em `pt` continua em português.
- `DossierFlow` cai em `en` quando o idioma inicial é inválido.
- `EnglishOnlyChapters` saiu do `GameHost`; Marte segue a escolha. A parte da `Opportunity` guarda o idioma à parte (`GameSettings.English`, PlayerPrefs): o `MarsFieldHandoff` agora copia a escolha do dossiê em vez de forçar inglês.
- Testes ajustados ao novo padrão: `ChapterFlowTests`, `DossierFlowTests`, `PauseTests`, `GuidanceUiTests`.
- **Estes ajustes ainda não foram executados**: o Editor estava aberto e bloqueou a execução em lote. Rodar o Test Runner (EditMode inteiro e `BootPlayTests`) antes de commitar.
- **Ajustes de UI pedidos após o primeiro teste manual:** "Tela cheia (opcional)" saiu do menu de pausa e virou linha de Preferências (`SettingsPanel`, chave `prefs.fullscreen`); legenda do Surveyor de 23 px para 28 px, igual ao token `--sa-text-subtitle` das outras; rodapé do dossiê só "Independent educational production" / "Produção educativa independente" (a nota de não afiliação à NASA continua nos créditos).
- **Resultado do Test Runner do dono (1111 testes, 14 falhas)**: 4 eram texto em português com o novo padrão em inglês (corrigidos nos testes), 5 são antigas (`ProjectSetupTests` ×2, eclipse, 2 do roteiro do Surveyor), 4 de tempo real do Surveyor (Descent, Launch, Midcourse, CommandDelay) e `PioneerStageTests` (`PIO_EarthCompare.mat`) ainda sem causa confirmada; suspeita de perda de foco da janela do Editor durante a execução.
- **Identidade visual:** a Pioneer 10, o dossiê, o HUD, a pausa e o guia usam os tokens `--sa-*`; **nenhum** `.uss` do Surveyor (13 arquivos) nem a UI da Opportunity (`MissionUI.uss`) usa. Não reestilizei: seria um redesenho visual sem como conferir a tela daqui.

## Base

- Branch `int-001-integracao-das-3-cenas`, a partir de `develop` em `c3cfbe3` (merge do PR #209).
- Unity 6000.6.7f1. As medições abaixo são do **Editor** (Play Mode em lote), não do navegador.

## O que mudou

| Arquivo | Mudança |
| --- | --- |
| `Assets/_Game/Core/ChapterCatalog.asset` | `pioneer10` e `surveyor3` agora têm `ChapterDefinition`; o Surveyor traz `ChapterTexts.json` e o crédito da capa `NASA · AS12-48-7134` |
| `ProjectSettings/EditorBuildSettings.asset` | Exatamente: Boot, Mars, Opportunity, Pioneer10, SurveyorChapter |
| `Assets/_Game/Scenes/Boot.unity` | Regerado pelo builder: `Boot Camera` com `AudioListener` e a referência `bootCamera` no `GameHost` |
| `Integration/GameHost.cs` | Carregador próprio, ligações por capítulo, um único `AudioListener`, pausa suspensa durante o Surveyor |
| `Integration/ChapterSceneLoader.cs` (novo) | Carrega a cena aditiva e dá um gancho **antes do primeiro `Start`** |
| `Integration/PioneerBindings.cs`, `SurveyorBindings.cs`, `ShellServices.cs` (novos) | Ligação de cada capítulo ao shell, sem editar as cenas dos donos |
| `Integration/Editor/PlayableBootBuilder.cs` | Catálogo, textos da Pioneer, Build Settings das cinco cenas, validação do dossiê |
| `Integration/Tests/` (novo) | 12 testes de EditMode e 4 de PlayMode |
| `Assets/_Game/Scenes/README.md` | Cenas, decisão de não criar `Dossier.unity`, como regravar |

Nenhuma cena de capítulo, prefab de capítulo ou script de capítulo foi editado.

## Decisões registradas

- **`Dossier.unity` não foi criado.** O dossiê continua instanciado pelo `GameHost` dentro do Boot (decisão do dono, 15/11/2026). O `Docs/Tasks/INT-001.md` oficial ainda lista essa cena e precisa ser atualizado no PR.
- **O Surveyor roda com o `SurveyorDocumentaryHost`** da cena `SurveyorChapter` (edição SUR-020). Sem receber os serviços antes do primeiro `Start`, esse host trata a si mesmo como execução isolada e troca a cena inteira pelo `SurveyorArchive`, o que derrubaria o Boot. Por isso o carregador do Boot injeta no gancho `BeforeStart`, como o `SurveyorArchiveHost` já fazia.
- **Pausa:** o Surveyor traz menu de pausa próprio; os dois ouvindo o P se anulariam (um pausa, o outro retoma). O menu do Boot fica suspenso enquanto o Surveyor roda e volta depois. Pioneer e Marte usam o menu do Boot.
- **Áudio:** Pioneer e Marte usam o `AudioDirector` e o `AudioCatalog` globais (que já têm os cues `MUS_PIO_*`). O Surveyor mantém o `AudioDirector` do capítulo (`DocumentaryAudio.asset`); o do Boot fica ocioso durante o capítulo.
- **AudioListener:** o Boot tem um; o `GameHost` o liga no dossiê e na abertura de Marte (que não traz ouvinte) e o desliga quando um capítulo ativa o seu. A troca roda ao abrir qualquer cena, antes de descarregar um capítulo e a cada 0,25 s.
- **Idioma no meio da Pioneer:** os painéis da Pioneer recebem o texto na inicialização, então trocar o idioma no meio refaz a ligação e o estado atual recomeça no novo idioma.

## Cenas carregadas por estado (Play Mode, cenas reais)

Medido por `Integration/Tests/PlayMode/BootPlayTests.cs`; dados brutos em `playmode-*.json`.

| Estado | Cenas | AudioListener ativos |
| --- | --- | --- |
| Dossiê | `Boot` | 1 |
| Pioneer 10 (jogando, após 2 reinícios) | `Boot`, `Pioneer10` | 1 |
| Marte (jogando, após 2 reinícios) | `Boot`, `Mars`, `Opportunity` | 1 |
| Surveyor 3 (jogando, após 2 reinícios) | `Boot`, `SurveyorChapter` | 1 |
| Volta ao dossiê (de cada capítulo) | `Boot` | 1 |

**Atenção, Marte:** a `Opportunity` já está carregada desde o começo de Marte (o `MarsFieldHandoff` a pré-carrega para a passagem). Ou seja, durante o capítulo de Marte há dois mundos 3D na memória. É o desenho atual do dono de Marte; neste card só se confirmou que a `Opportunity` sai no reinício e na volta ao dossiê (a contagem volta a 1) e que ela não é duplicada.

## Testes executados

| Suíte | Resultado |
| --- | --- |
| `BootIntegrationTests` (EditMode, 12 testes) | 12/12 passaram (`editmode-results.xml`) |
| `BootPlayTests` (PlayMode, 4 testes, cenas reais) | 4/4 passaram (`playmode-results.xml`) |
| Suíte EditMode inteira do projeto (1111 testes) | 1097 passaram, 14 falharam (detalhe abaixo) |

O PlayMode reprova o teste se qualquer `Debug.LogError`/exceção aparecer no console e se o Unity avisar de AudioListener ausente ou duplicado.

### As 14 falhas da suíte inteira (nenhuma toca arquivos deste PR)

- 9 eram do modo `-nographics` (`RenderTexture.Create failed` etc., testes de renderização do Surveyor); com GPU, **passaram**.
- 5 continuam falhando com GPU e não dependem de Boot, catálogo nem Build Settings: `ProjectSetupTests` ×2 (dependências do `manifest.json` e renderer URP), `EclipseIntegrationTests` (borda vermelha do eclipse na saída da GPU), `SurveyorScienceTests.EveryScriptCueHasPublishableEvidence` e `SurveyorSubtitlesTests.TextsAreTheScriptTableTranscribedWithoutChange` (esperam 31 linhas SUR-Sxx no roteiro e leem 0). **Não rodei a suíte em `develop` limpa para provar que já falhavam**; a leitura acima é por inspeção dos testes. Quem tiver a árvore limpa pode confirmar.

## Build Web: pré-verificação bloqueada (não é causa deste PR)

`python Tools/build_release.py --preflight-only --targets webgl` terminou com **falha**. As cenas passaram (nenhum problema de cena listado); os bloqueios são do projeto:

- `Dependência direta não aprovada no manifesto`: `com.unity.cloud.gltfast 6.20.0`, `com.unity.timeline 6.6.0`, `com.unity.ugui 2.6.0`, `com.unity.modules.director 1.0.0`.
- `O asset URP deve ter exatamente um renderer: SpaceApps_URP_Renderer.`
- `O WebGL usa SpaceApps_URP.asset, mas o perfil Web da QA-001 é WebLight.asset` (nível de qualidade padrão do WebGL).

Por isso **não há build Web completo nem smoke no navegador** neste PR. Os testes de Play Mode acima exercitam as mesmas cenas e o mesmo carregador no Editor, mas não substituem o navegador.

## O que NÃO foi testado

- **Build Web e smoke Pioneer → Marte → Surveyor no navegador** (bloqueados pela pré-verificação, ver acima).
- Memória no navegador (só há medição de Editor, em `playmode-*.json`; o número serve de referência de crescimento entre reinícios, não de orçamento Web).
- Aparência: nenhuma captura de tela. Não foi verificado se o HUD global (prompt de E, objetivo) coincide com a interface própria da Pioneer e do Surveyor.
- Reinício no meio de interação mantida (E, comando pendente, minigame) — o teste reinicia no começo de cada capítulo, não durante uma interação.
- Foco do navegador (perder foco pausa e limpa E/WASD) com o novo carregador.
- Pessoas, GPU integrada, iframe do itch.io: pendentes de QA humano (QA-003/004/005).
