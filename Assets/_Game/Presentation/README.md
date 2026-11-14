# Presentation

| Arquivo | Tarefa / dono |
| --- | --- |
| `SequenceDirector.cs`, `CameraDirector.cs`, `SequenceClock.cs` | CORE-004 · R1 — **implementado** |
| `LocalizationService.cs`, `SubtitlePresenter.cs` | UI-004 · R2 |
| `FocusPause.cs` | UI-006 · R2 |
| `PhotoCapture.cs` | UI-007 · R2 |
| `AudioDirector.cs` | AUD-002 · R7 — **implementado** (README em `Shared/Audio/`; sem arquivos de áudio ainda) |

## Sequências e câmera (CORE-004)

- `SequenceDirector.Play(sequence, skippable)` valida dados **e** referências da cena (câmera, rigs, alvos, atores) antes de qualquer efeito. Registre rigs/alvos no `CameraDirector` e atores no `SequenceDirector` antes de tocar.
- Um único relógio (`SequenceClock`, tempo ativo do `IActiveTimeSource`): câmera, atores, legendas e eventos nomeados. Eventos disparam quando o relógio cruza o instante (mesma ordem a 30 e 60 FPS). No mesmo instante: legenda que termina → eventos/planos em ordem → legenda que começa.
- Durante a execução a sequência possui a câmera e o contexto de entrada `Cinematic`. Término, `Skip()` (só se permitido; dispara os eventos restantes sem mostrar legendas) e `Cancel()` passam pelo mesmo caminho: controle devolvido e `Finished` **uma vez**.
- Legendas por gatilho (`Trigger(id)`) seguram o término até o tempo mínimo de leitura.
- `ShotDefinition.ContinueFromPreviousShot`: o plano parte da pose/FOV atuais da câmera (fim do plano anterior ou câmera de gameplay), sem salto.
- `CameraDirector`: rig `world` implícito; proteção de altura por raycast na `groundMask` (configure só a camada do terreno); um único `AudioListener` (o da câmera); posse exclusiva da câmera (`AcquireControl`).
- `Preview(sequence, t)` mostra qualquer instante sem eventos nem posse de controle (estados anteriores e seguintes).
