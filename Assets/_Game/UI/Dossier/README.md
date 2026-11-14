# Dossiê — UI-002 (R2)

Seleção das três pastas e carregamento explícito. Consome o `ChapterCatalog` e o `ChapterDirector` (CORE-005); não cria campanha linear: qualquer capítulo inicia sozinho, sem ler nada e sem outro concluído.

## Instalação (R1, INT-001)

1. Instancie o prefab `DossierScreen.prefab` na cena **Dossier** (só R1 edita `Dossier.unity`). **Mantenha o GameObject ativo**: a tela se esconde sozinha durante o capítulo e precisa estar viva para mostrar o carregamento ao entrar e ao voltar.
2. Depois de criar o `ChapterDirector`, o `SettingsStore` e o `InputRouter` do aplicativo, chame:

   ```csharp
   dossierScreen.Initialize(chapterDirector, settingsStore, inputRouter); // catálogo opcional (padrão: o do prefab)
   chapterDirector.OpenDossier();                                         // Boot → Dossier
   ```

   `inputRouter` é opcional: sem ele o dossiê responde só ao mouse. Com ele, setas navegam, Enter confirma e P/Esc fecha o painel aberto (a tela nunca lê tecla direto).
3. Se a cena Dossier tiver câmera, `AudioListener` ou áudio próprios, liste-os em `Menu Scene` (Inspector da `DossierScreen`). Eles são desligados enquanto um capítulo carrega/roda e religados ao voltar. A interface (UI Toolkit) **não precisa de câmera**; o prefab não contém câmera nem áudio.
4. Atribua a `ChapterDefinition` de Pioneer 10 e de Surveyor 3 no `Assets/_Game/Core/ChapterCatalog.asset` quando forem integradas (`chapterId` deve ser igual ao `slotId`: `pioneer10`, `surveyor3`). Enquanto a definição estiver vazia a pasta aparece como *em preparação*, sem botão ativo.
5. Rode **Abandoned Space Apps → UI-002 → Validar catálogo e textos do dossiê** depois de mexer no catálogo ou nos textos.

## Comportamento

| Requisito da tarefa | Como é atendido |
| --- | --- |
| Capítulos sem leitura nem pré-requisito | `DossierFlow.CanStart` não depende de outro capítulo nem de abrir *Detalhes*; um teste inicia os três em qualquer ordem, duas vezes. |
| Clique em Iniciar libera áudio e cursor | `AudioListener.pause = false` no gesto; `captureCursor` por pasta no catálogo (desligado: câmera manual é opcional). Evento `StartGestureReceived` para o AudioDirector. O cursor volta ao normal ao retornar ao dossiê. |
| Carregamento visível antes do relógio | Sobreposição com título, capítulo e barra animada; texto explica que o tempo da missão só começa quando a cena abre; após 8 s mostra "ainda carregando". Com *Reduzir movimento* a barra fica parada. O `SessionClock` só corre depois de `ChapterStarted`. |
| Sem entrada duplicada | Duas travas: `DossierFlow` (clique no mesmo quadro) e `ChapterDirector` (Loading/Unloading ignoram `Begin`). Botões e idioma ficam desabilitados durante o carregamento. |
| Idioma durante o carregamento | Recusado pelo fluxo; o idioma não muda e a cena abre uma vez. |
| Menu e gameplay sem câmera/áudio duplicados | Prefab sem câmera/áudio; `MenuSceneGate` desliga os do menu; aviso no Editor e em builds de desenvolvimento se houver mais de um `AudioListener` ativo. |
| Retorno, configurações, créditos, PT/EN, duração | Painéis *Detalhes*, *Configurações* e *Créditos e fontes* (botão Voltar, P/Esc); PT/EN gravado em `SettingsStore`; duração vem de `ChapterDefinition.TargetDurationSeconds` (`5–7 MIN` dentro da faixa editorial). |

## Arquivos

| Arquivo | Papel |
| --- | --- |
| `DossierFlow.cs` | Regras sem Unity: início único, idioma, painéis, foco (`FocusRing`), rótulos (`DossierFormat`). |
| `DossierDirectorLink.cs` | Única classe que chama `ChapterDirector.Begin`; liga fluxo e diretor. |
| `DossierScreen.cs` | Tela UI Toolkit (só desenha o estado e repassa ações). |
| `ChapterCatalog.cs` + `../../Core/ChapterCatalog.asset` | Pastas, ordem, classe de cor, tabela de textos, captura de cursor, crédito de capa. |
| `DossierTextCatalog.cs`, `DossierTextTables.cs`, `DossierKeys.cs`, `DossierContentCheck.cs` | Textos PT/EN por chave, leitura do JSON e validação de cobertura. |
| `MenuSceneGate.cs` | Desliga câmera/áudio do menu durante o capítulo. |
| `DossierScreen.uxml`, `DossierFolder.uxml`, `DossierScreen.uss` | Estrutura e estilos (só tokens da UI-001). |
| `DossierTexts.json` | Textos da interface; créditos são linhas `credits.line.*` — **acrescente uma linha por recurso importado** (fonte: `ASSET_CREDITS.md`). |
| `DossierScreen.prefab` | `UIDocument` + `ResponsiveLayoutClass` + `DossierScreen`, sem câmera nem áudio. |
| `Editor/DossierAssetsBuilder.cs` | Menu para recriar/completar catálogo e prefab e validar conteúdo (idempotente; não remove entradas alheias). |
| `Tests/` | `DossierFlowTests` (sem Unity), `DossierDirectorLinkTests` (diretor real, carregador falso), `DossierAssetsTests` (assets, UXML, prefab). |

## Textos e capas

- Título, descrição e pergunta vêm das chaves do `ChapterDefinition` (ex.: `MAR.TITLE`) na tabela JSON do capítulo (`chapterTexts` no catálogo; mesmo formato da seção `texts` de `MarsSubtitles.json`). Sem tabela, vale o texto de reserva `slot.<id>.*` de `DossierTexts.json`.
- Os textos novos de interface e as traduções EN marcadas como propostas em `Docs/Art/UI_SPEC.md` seguem **sem revisão editorial**.
- Capa: `ChapterDefinition.Cover` + `coverCredit` no catálogo (obrigatório; a validação recusa capa sem crédito). Sem capa aparece o marcador "FOTO NASA · PENDENTE".
- O símbolo `≈` (duração fora da faixa) só existe no IBM Plex Mono; a Outfit não o tem. A duração só usa classes mono (`sa-folder__duration`, `sa-stamp`).

## Limitações conhecidas

- Pioneer 10 e Surveyor 3 ainda não têm `ChapterDefinition` integrada: as pastas aparecem como *em preparação*.
- `Mars.unity` ainda não existe e as Build Settings estão vazias (INT-001): iniciar Marte no Play Mode mostra o erro de carregamento e volta ao arquivo.
- Nenhuma tela de conclusão pertence a esta tarefa: ao terminar o capítulo o dossiê fica oculto até `ReturnToDossier()`.
- *Configurações* oferece volumes, reduzir movimento, modo de segurar e assistência extra. Tamanho de texto e pausa em jogo pertencem à UI-006; os volumes só terão efeito audível quando o AudioDirector ler o `SettingsStore`.
- Sem verificação visual: a aparência no Editor/navegador e o contraste renderizado ainda não foram conferidos (ver *Ainda não executado* em `Docs/Art/UI_SPEC.md`).
