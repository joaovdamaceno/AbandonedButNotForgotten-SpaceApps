# Áudio (AUD-002)

Dono: R7. Planilha de cues: `Docs/Art/AUDIO_CUE_SHEET.md`. **Nenhum arquivo de áudio foi adquirido ou importado:** a AUD-001 só listou candidatos. Todos os cues do catálogo nascem **pendentes** (sem clipe) e o diretor os trata como silêncio, sem erro. Cada clipe só entra depois de uma linha "Importado" em `ASSET_CREDITS.md`.

| Arquivo | Papel |
| --- | --- |
| `../../Presentation/AudioDirector.cs` | O diretor (MonoBehaviour). Um único dono do volume de cada voz |
| `AudioBus.cs` | Master / Music / Sfx / Ui |
| `AudioCueDefinition.cs` | Um cue por dados: bus, clipes (variações), loop, volume, variação de altura, teto de loop; `Validate()` |
| `AudioCatalog.cs` + `AudioCatalog.asset` | Catálogo por ID. O asset traz os IDs já pedidos pela Pioneer e pelo HoldAction, todos pendentes |
| `AudioDirector.prefab` | Diretor com o catálogo ligado, para a integração (INT-001) |
| `HoldSoundSet.cs` | Cues de um instrumento de E mantido: início, loop, conclusão e soltar |
| `IAudioVoice.cs` / `UnityAudioVoice.cs` | A "voz": o mínimo que o diretor pede a um `AudioSource` 2D (permite testar sem placa de som) |
| `Editor/AudioDirectorSetup.cs` | Cria/atualiza o catálogo (só acrescenta IDs, nunca apaga clipes) e o prefab. Menu Abandoned Space Apps → AUD-002 |
| `Tests/AudioDirectorTests.cs`, `AudioDirectorHoldIntegrationTests.cs` | 22 testes EditMode (3 com `InteractionService` e `InputRouter` reais) |

## O que ele faz

- **Música** (`SetCue(cueId, fade)`): crossfade de potência constante com fade limitado a **0,8–2 s** (0 = corte seco, só onde o roteiro pede silêncio). Pedir o cue que já toca **não reinicia**. `SetMusicLevel` (ex.: quase silêncio na virada do cinturão) e **ducking** de −6 dB (ataque 0,3 s, retorno 0,8 s) por motivo: vários motivos não se anulam.
- **Um dono por voz:** sem corrotinas. `Tick(activeDt, realDt)` escreve o volume de cada voz **uma vez por quadro**; pedir outro cue no meio de um fade não cria duas rotinas na mesma fonte (pool de 3 vozes de música; sem voz livre, a mais antiga é cortada).
- **Efeitos** (`PlayEffect`): alternam as variações sem repetir a mesma duas vezes seguidas, com variação de altura; pool de 8 vozes.
- **Loops** (`StartLoop`/`StopLoop`): sem duplicar; **teto de duração** (padrão 40 s; o de cada cue vale até 120 s) com fade de 0,5 s. Cama de música não tem teto. O catálogo recusa loop com clipe menor que 1 s (emenda vira clique) e cama de música menor que 30 s.
- **Instrumentos** (`RegisterHold` + `BindInteraction(IInteractionService)`): o som segue os **eventos reais** do HoldAction. Início → clique + loop; progresso → altura (0,9→1,3) e volume sobem; soltar → desacelera (altura a 0,7, volume some em 0,5 s) + som de soltar; concluir → loop para + confirmação. Sem hold, nada toca.
- **Silêncio real** (`Silence(s)`): música, ambiente e loops caem em 0,15 s, nada novo fora da interface toca, volta em 1 s. Conta tempo ativo (pausar congela).
- **Pausa por origem** (`SetPaused(origem, bool)`; o foco do aplicativo entra como origem "focus"): música, efeitos e ambiente congelam; **a interface (bus Ui) continua**.
- **Volumes** (`ApplySettings(SettingsData)`): Master × Music/Sfx/Ui, independentes, **suavizados** (sem degrau). Música em 0 não apaga os efeitos.
- **Web:** `Available` é falso até `Unlock()` (clique em Iniciar; AudioContext). A música pedida antes do clique é guardada e entra ao liberar; efeitos pedidos antes são descartados, não empilhados. Fora do Web, liberado desde `Initialize`.
- **Reinício / troca de capítulo:** `StopAll(fade)` e `ResetState()` (para tudo e zera ducking, silêncio, pausa e nível; o mesmo cue volta a tocar).
- Vozes são `AudioSource` **2D**, sem espacialização: som no espaço e na Lua é música e sonificação, nunca propagação no vácuo. O único `AudioListener` continua sendo o da câmera.

## Uso (integração)

```csharp
director.Initialize(catalog, sessionClock, settingsStore.Data);   // no Web: startUnlocked = false por padrão
director.BindInteraction(interactionService);
director.RegisterHold(new HoldSoundSet { CandidateId = "PIO.Launch.Rtg", StartCue = "SFX_HOLD_START", LoopCue = "SFX_HOLD_LOOP", DoneCue = "SFX_HOLD_DONE", ReleaseCue = "SFX_HOLD_RELEASE" });
// no clique em Iniciar:  director.Unlock();
// preferências alteradas: director.ApplySettings(settingsStore.Data);
// Pioneer: new PioneerAudioAdapter(director)  →  PioneerNarration.Configure(..., audioPort)
```

## Limitações

- **Sem áudio real:** nenhum clipe foi adquirido; tudo o que foi testado usa clipes sintéticos e vozes falsas. Falta ouvir (equilíbrio de volumes, emendas de loop, duração das camas) assim que houver arquivos.
- **Web não exercitado:** o desbloqueio do AudioContext é a lógica de `Unlock()`; ainda não foi verificado no navegador, nem o orçamento de 10 MB de áudio (QA-001).
- A ligação das preferências a eventos de mudança (UI-006) e do `Unlock()` ao botão Iniciar (UI-002/INT-001) ainda não existe; hoje são chamadas explícitas.
- O mapeamento candidato → cues de cada instrumento (`RegisterHold`) deve ser feito por cada capítulo; só o contrato existe.
- Mars e Surveyor têm áudio próprio (`AudioSource` direto nos scripts, com `SettingsData`); não foram migrados para o diretor.
- O `Silence` não restaura loops parados pelo teto; o teto é de loops de SFX e ambiente, não de música.
