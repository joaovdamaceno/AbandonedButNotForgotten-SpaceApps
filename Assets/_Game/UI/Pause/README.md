# Pausa e preferências (UI-006 · R2)

| Arquivo | O que faz |
| --- | --- |
| `../PauseController.cs` | Regras: quem pode pausar, P/Esc/botão, perda de foco, retomada deliberada (`Continue`). Sem interface. |
| `../PauseMenu.cs` | Botão de pausa + menu centralizado (Continuar, Reiniciar, Voltar ao arquivo; sem aba de preferências). |
| `../SettingsPanel.cs` | Volumes (geral, música, efeitos, interface), idioma, sensibilidade, movimento reduzido e qualidade, via `SettingsStore`. |
| `../PauseKeys.cs`, `PauseTexts.json` | Textos PT/EN; nenhuma frase em C#. |
| `../../Presentation/FocusPause.cs` | Foco do navegador: solta teclas, pausa, congela o áudio até a retomada. |
| `Pause.uxml`, `Pause.uss` | Layout; só estende os tokens e componentes da UI-001. |
| `Editor/PauseMenuAssetsBuilder.cs` | Menu *Abandoned Space Apps > UI-006*: gera o prefab `PauseMenu.prefab` e valida textos e UXML. |
| `Tests/PauseTests.cs` | EditMode: pausa, foco, rearme, persistência, textos, UXML. |
| `Preview/PausePreviewHost.cs`, `Editor/PausePreviewSceneBuilder.cs` | Só para teste manual: menu *UI-006 > Criar cena de prévia da pausa* gera `Preview/PausePreview.unity` (cubo com WASD, E, pausa). Não é a integração. |

## Instalação (R1, INT-001)

1. O `PauseMenu.prefab` já está versionado (gerado por **Abandoned Space Apps > UI-006 > Reconstruir prefab da pausa**, idempotente; exige os assets da UI-001).
2. Instancie `PauseMenu` no aplicativo e chame `Initialize(director, inputRouter, settingsStore, missionHud)`.
   Com o `MissionHUD` já ligado, o botão vai para `RightColumn`; senão fica no canto superior direito do próprio documento
   (`AttachToHud` move depois).
3. Adicione `FocusPause` e chame `Initialize(pauseMenu.Controller, director, audioDirector)`.
4. Use **uma** inscrição em `InputRouter.PauseDown` para a pausa global (a do `PauseMenu`). Capítulos não pausam por conta própria.

## Comportamento

- P, Esc (se o navegador não o consumir) e o botão pausam. P/Esc retomam uma pausa aberta pelo menu.
- Perda de foco: `InputRouter.NotifyFocusLost()` solta E/WASD, o capítulo pausa e o áudio congela. Voltar ao foco **não** retoma;
  só o clique em Continuar. P/Esc são ignorados nesse estado.
- Retomar não reaplica nada: o `InputRouter` zera o movimento ao trocar de contexto e só aceita E depois de soltar.
- Reiniciar e Voltar ao arquivo pedem um segundo clique. O `ChapterDirector` zera relógio, pausas, evidências e contextos.
- Tela cheia é um botão (gesto do navegador); a captura do cursor não é usada e é liberada na perda de foco se alguém a tiver ligado.
- O menu não mostra preferências (a aba saiu: só enchia linguiça). O que já estava salvo no `SettingsStore` continua valendo e a qualidade é
  aplicada ao ligar (`SettingsPanel.ApplyEngineSettings`). `SettingsPanel` continua no projeto, com seus testes, sem tela que o use.
- Um só item destacado: o do teclado (↑ ↓) ou o que o mouse tocou (`PointerEnterEvent` move o destaque). O CSS dos itens não usa `:hover` nem `:focus`.

## Verificação manual (pendente de uma pessoa com o Editor e o navegador)

Nada abaixo foi executado por quem escreveu o código.

- Prévia em 960×600, 1280×720, 1152×720 (16:10) e 4:3: o cartão fica centralizado.
- Build Web (iframe do itch.io): fullscreen, redimensionar, trocar de aba real, Esc e P.
- Manter W e E, trocar de aba, voltar: nada se move e nenhuma ferramenta liga; só Continuar retoma.
- Mudar idioma/volume, recarregar a página: valores mantidos. Reiniciar capítulo: sem estado do anterior.
