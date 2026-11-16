# NAR-001 — narração humana em inglês

Pedido: issue #223. Base de implementação: `develop` em `5b67617`.
Branch `NAR-001`; integração em develop depende da autorização do proprietário.

## Conteúdo integrado

| Capítulo | Falas | Catálogo configurado |
| --- | ---: | --- |
| Surveyor 3 | 25 | `Assets/_Game/Chapters/Surveyor/Narration/EnglishNarration.asset` |
| Pioneer 10 | 59 | `Assets/_Game/Chapters/Pioneer10/Narration/EnglishNarration.asset` |
| Spirit & Opportunity | 28 | `Assets/_Game/Chapters/Mars/Narration/EnglishNarration.asset` |

Os 112 OGG ocupam 5,73 MiB de arquivos-fonte e contêm 617,078 segundos
somados de voz. Isso não representa a duração da campanha nem o tamanho final
da build: a Unity recodifica o áudio conforme a plataforma.

O áudio é sempre inglês. A escolha PT/EN muda as legendas, não troca a voz.
Não há síntese de fala ou serviço de transcrição durante a partida.

Foram configuradas e salvas as cenas reais `SurveyorChapter`, `Pioneer10`,
`Mars` e `Opportunity`. Para executar o conjunto, abrir
`Assets/_Game/Scenes/Boot.unity` e selecionar uma missão no dossiê.
O player standalone de Pioneer também foi ligado à mesma narração.

## Fonte e edição

O usuário forneceu `narracao.zip`, contendo três gravações WAV e arquivos de
sessão `.asd`. Somente os WAV foram processados. Os originais permanecem
inalterados em `QA/Local/ChapterNarration/Originals`, fora da distribuição/Git.
Nomes e SHA-256 estão nos manifestos `Data/Narration/*.json`.

Foi usada transcrição local `faster-whisper`/`small.en`, com tempos por palavra,
seguida de alinhamento independente por chave e revisão dos limites com o
roteiro atual. O campo `asr_excerpt` registra a hipótese bruta do reconhecedor,
inclusive erros em nomes próprios; não é uma transcrição humana certificada.
O texto de referência está em `subtitle_en`. Os manifestos guardam entrada,
saída, duração, arquivo, origem e edições de cada fala.

Tratamento: média explícita dos canais para mono antes da normalização,
passa-altas em 70 Hz, normalização EBU R128 por capítulo com alvo -18 LUFS e
limite de -3 dBTP no mestre, amostragem em 32 kHz, Vorbis q4 e fades curtos de
8/15 ms nas bordas. Não se acelerou a voz nem se alterou o tom. Os valores
individuais medidos após a compressão estão em `QA/NAR-001/audio-validation.json`;
normalizar o conjunto não torna toda frase isolada exatamente -18 LUFS.

Edições pontuais registradas em `Data/Narration/edits.json`:

- Retirada de repetição de “kilometers” e recuperação dos finais de frases que
  o reconhecimento automático deixou fora do primeiro alinhamento.
- O reconhecedor interpretou o dia de junho da passagem da Pioneer em 1983
  como “twentieth”. Para não incorporar uma data possivelmente incorreta,
  a fala começa em “1983”; a legenda mantém a data histórica do roteiro.
- Retirada de “only” na afirmação sobre formação de hematita. O texto PT/EN
  de `MAR.C04` acompanha a edição: hematita também pode se formar sem água,
  conforme a fonte MS04 já registrada no capítulo.
- A frase gravada sobre fazer a última imagem chegar à Terra foi preservada
  na chave `nar.mars.last.image`, apresentada ao começar a tempestade final.

## Trechos sem gravação

Quatro legendas do roteiro atualizado não foram localizadas nos WAV:
`PIO.05.01b`, `PIO.05.01c`, `v13.years.work` e `v15.storm.dark`.
Permanecem como legendas sem voz. Não foram apagadas nem preenchidas por IA.
Texto pronto para gravação: [MISSING_RECORDINGS_EN.txt](../../QA/NAR-001/MISSING_RECORDINGS_EN.txt).

## Funcionamento e reconstrução

`NarrationCatalog` associa chave de legenda ao clipe inglês. Há um
`NarrationPlayer` por cena de capítulo. Ele acompanha `SubtitlePresenter.BodyKeyChanged`,
interrompe ao descarregar a cena, evita repetição na simples troca de idioma,
respeita as razões independentes de pausa/foco e atenua a música durante a voz.
A reprodução Unity usa o playhead real do `AudioSource`, incluindo o atraso
de início do decoder. O canal não é espacial: a voz permanece inteligível
independentemente da posição da câmera.

Surveyor, Pioneer e a abertura de Marte usam `SessionClock`. O trecho já
existente da Opportunity usa um adaptador para seu tempo ativo, respeitando
a pausa do capítulo. `NarrationTimeMap` amplia janelas narrativas quando a
gravação natural requer mais tempo, mantendo câmera e atores no mesmo tempo
autorado. As sequências compartilhadas também têm janelas de legenda ajustadas.
O áudio permanece em pitch 1. A operação científica da Surveyor deixa as falas
de cada etapa terminarem antes de apresentar a explicação seguinte.

Nota de integração: o controlador Pioneer atual reinicializa sua etapa ao
trocar idioma. Essa reinicialização pode voltar a apresentar a fala; a garantia
de não repetir aplica-se à atualização de legenda, não a um reinício explícito
da etapa pelo controlador.

Importação: mono, `CompressedInMemory`, `loadInBackground`, sem preload de
todas as falas. A solicitação padrão é 32 kHz/Vorbis; o backend Web da Unity
traduz para AAC e a frequência efetiva depende da plataforma/dispositivo.
Não aplicar um override Web explícito Vorbis/32 kHz, incompatível com este Editor.
O player libera dados que carregou quando a fala acaba ou é cancelada.

Menu: **Abandoned Space Apps → NAR-001 → Install and validate English narration**.
Entrada batch: `Abandoned.SpaceApps.EditorTools.NarrationSetup.InstallAndValidate`.
Os builders SurveyorDocumentary, PioneerChapter e MarsChapterAssembly reaplicam
os catálogos ao reconstruir. A cena legada Opportunity é preservada e recebe
sua referência pelo instalador. Executar o instalador após reconstrução manual
do conjunto. A validação abre as quatro cenas e exige exatamente um player
configurado em cada uma; ela não altera a arte de outro capítulo.

Para recortar novamente, é necessário restaurar os WAV originais na pasta local,
usar Python 3 e FFmpeg (`FFMPEG` ou PATH), e executar
`python Tools/prepare_chapter_narration.py --export`. Os manifestos revisados são
a fonte dos cortes. Para refazer o alinhamento, também são necessários os JSON
de ASR locais; isso não é necessário para abrir ou distribuir o jogo.
`python Tools/validate_chapter_narration.py` requer NumPy e decodifica todos os
clipes, medindo sinal, clipping, duração e hash.

## Validação e limites

Evidências e resultados reais em [QA/NAR-001](../../QA/NAR-001/README.md).
Os testes de comportamento não substituem escuta humana da mixagem e dos
cortes, nem uma partida integral em cada capítulo. Não se declara aprovação
artística ou de inteligibilidade por uma transcrição automática.
