# Áudio: silêncio ao voltar ao menu e música com volume parecido nos três capítulos (15/11/2026)

## 1. Nada toca no menu principal
`GameHost` (Integration): ao descarregar um capítulo, `AudioDirector.ResetState()` (para música, loops e efeitos do shell e zera pausa/ducking) e `AudioListener.volume = 1`; ao entrar no estado Dossiê o volume global volta a 1 de novo (a cena de Marte, pré-carregada, ainda escrevia o "geral" dela, 0,8, até ser descarregada). O áudio das cenas dos capítulos some com a cena.

Verificado em Play (Boot, Unity sem foco, `runInBackground`): Pioneer tocando (música `MUS_PIO_LANCAMENTO` + 1 efeito) → `ReturnToDossier` → 0 vozes de música/loop/efeito, 0 `AudioSource` tocando, volume global 1. Marte (abertura, `a-kind-of-hope` em 0,51) → `ReturnToDossier` → 0 fontes tocando, volume global 1.

## 2. Volume da música parecido
Loudness integrado (ffmpeg `ebur128`) das faixas × cadeia de ganho com os volumes padrão (geral 1, música 0,8):

| Capítulo | Faixas (LUFS) | Antes (efetivo) | Depois (efetivo) |
|---|---|---|---|
| Pioneer | −17,8 a −20,2 | ≈ −21,5 | ≈ −32 |
| Surveyor (documentário) | Departure −26,7 · Science −27,4 · Return −25,9 | ≈ −39 a −44 | ≈ −30 a −35 |
| Marte | a-kind-of-hope −21,2 · the-things −24,8 · the-distant-sun −33,1 | ≈ −37 (abertura) · −45 (exploração) · −59/−67 (tempestade/último contato) | ≈ −27 (abertura/fim) · −32 (exploração) · −45/−53 (tempestade/último contato) |

O que mudou:
- **Pioneer:** volumes dos 7 cues × 0,3 (fonte: `Data/Assets/PioneerMusic.json`, catálogo regenerado pelo importador, só os volumes mudaram).
- **Surveyor (documentário, o usado no jogo):** departure 0,29→0,68, science 0,18→0,42, return 0,34→0,80 (`SurveyorDocumentaryBuilder` e `DocumentaryAudio.asset`).
- **Marte:** o fator fixo 0,5 da música em `MissionAudio` saiu (ganhos dos 12 cues reajustados na cena `Opportunity`, mantendo a dinâmica entre eles; os silêncios da tempestade e do último contato continuam silenciosos); `MarsOpeningMusic.BaseGain` 0,25→0,64; os volumes de Marte (geral, música, efeitos, interface) agora seguem os do shell em vez de PlayerPrefs próprios (antes o controle de música do aplicativo não valia em Marte).

## Limitações
- Medido por loudness das faixas e pela cadeia de ganho, não ouvido com fones. Só música: efeitos e instrumentos não foram nivelados entre capítulos. Na Pioneer a música ficou ~10 dB mais baixa e os efeitos (foguete, instrumentos) não mudaram, então eles aparecem mais; em Marte os efeitos passam a seguir o controle do shell (padrão 0,9 em vez de 0,65, ≈ +3 dB).
- Marte: as faixas `the-distant-sun` (tempestade e último contato) são muito baixas na origem (−33 LUFS) e continuam bem abaixo dos outros capítulos, como já era o desenho.
- Surveyor legado (`SurveyorAudio.asset`, host `SurveyorChapterHost`) não entra no jogo integrado e não foi alterado.
