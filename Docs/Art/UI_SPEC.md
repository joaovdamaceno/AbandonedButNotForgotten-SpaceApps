# UI-001 — Sistema visual do dossiê e do HUD

Dono: R2. Escopo: linguagem visual compartilhada e layouts **estáticos**. Comportamento (carregar capítulo, prompt reagindo ao `InteractionService`, legendas, pausa) pertence a UI-002…UI-007; este documento é o contrato visual que essas tarefas consomem. Nada aqui foi testado com uma pessoa que não leu o briefing — essa revisão continua pendente (ver *Verificação*).

## Decisões

| Decisão | Escolha | Motivo |
| --- | --- | --- |
| Tecnologia | **UI Toolkit** (UXML/USS) nativo da Unity 6.6 | Sem pacote extra, estilos por classe reutilizáveis nos três capítulos, texto nítido em qualquer escala, funciona no WebGL. Nenhum uGUI/TextMeshPro no manifesto. |
| Escala | `PanelSettings`: Scale With Screen Size, referência **1280×720**, *match* = **1 (altura)** | A altura lógica é sempre 720 px: legenda e prompt têm o mesmo tamanho relativo em 960×600, 1280×720 e 4:3. Só a largura lógica varia: 4:3 → 960, 16:10 → 1152, 16:9 → 1280. |
| Responsivo | Classe `sa-layout--narrow` quando a largura lógica < **1100 px** | USS não tem `@media`. `ResponsiveLayoutClass` aplica a classe por `GeometryChangedEvent` (sem `Update`, sem alocação por quadro). |
| Fontes | **Outfit** (Regular, SemiBold, Bold) para UI/legendas; **IBM Plex Mono SemiBold** para datas, carimbos e telemetria | Indicação de `Docs/Art/DIRECTION.md` (Outfit) + uma família mono avaliada em nomes/datas. Ambas SIL OFL 1.1. |
| Fontes no USS | Referência direta ao `.ttf` (`-unity-font-definition: url(...ttf)`) | Na Unity 6.6 o sistema de texto avançado **rejeita FontAssets estáticos** (`AtlasPopulationMode.Static` está obsoleto; o log acusou “cannot use static font asset”). Usar o TTF evita atlas versionado e mudanças espúrias de arquivo. |
| Ícones | Desenhados com elementos (barras da pausa, seta de borda, pontos da fila) | As duas famílias **não têm** `⏸ ▶ ✔ ⟳` (verificado por cmap). `✓`, `← → ↑ ↓`, `·`, `–`, `…` e todos os diacríticos PT existem. |

## Tokens (`Assets/_Game/UI/Design/SpaceAppsTokens.uss`)

Cores e contraste. Coluna “sRGB” = cálculo WCAG das cores puras; coluna “renderizado” = medido nos PNGs gerados pela própria Unity (pior fundo testado), porque o projeto é Linear e a transparência das placas mistura em espaço linear.

| Token | Valor | Uso | Contraste sRGB | Renderizado (pior caso) |
| --- | --- | --- | ---: | ---: |
| `--sa-color-ink` / `paper` | #1F1A14 / #F2EADB | Texto do dossiê | 14,45:1 | — |
| `--sa-color-ink-muted` | #5A5045 | Texto secundário no papel | 6,58:1 | — |
| `--sa-color-mars` / `pioneer` / `surveyor` | #8E3B17 / #6B5216 / #36414D | Fundo do botão Iniciar (texto papel) e faixa da pasta | 6,30 / 6,18 / 8,70:1 | — |
| `--sa-color-plate` | rgba(12,14,18,**0,94**) | Placas do HUD/legenda | — | placa (63,56,49) sobre céu marciano #C99A6E |
| `--sa-color-on-plate` | #F5F1E8 | Texto principal no HUD | 11,61:1 | **10,23:1** |
| `--sa-color-on-plate-muted` | #C9C2B4 | Status/telemetria | 7,39:1 | **6,51:1** |
| `--sa-color-mars-hud` | #F0946A | Acento de Marte no HUD | 5,70:1 | **5,02:1** (pior de todos) |
| `--sa-color-pioneer-hud` / `surveyor-hud` | #E9C770 / #F2B84B | Acentos no HUD | 8,02 / 7,31:1 | 7,07 / 6,44:1 |
| `--sa-color-confirm` / `warn` | #8FD99A / #F2A541 | Concluído / pausado | 7,82 / — | 6,90 / 5,62:1 |
| `--sa-color-focus` | #FFD166 | Borda de foco (4 px) | 9,07:1 | — |
| `--sa-color-imagination` | #6FD6E8 | Selo IMAGINAÇÃO (Pioneer) | 7,76:1 | 6,84:1 |

**Achado durante a captura:** com o alfa inicial de 0,82, a placa renderizou em (107,94,81) — bem mais clara que o projetado — e o texto secundário cairia para ~3,5:1 e o acento de Marte para ~2,9:1 (reprovado). O alfa subiu para 0,94 (placas) e 0,97 (painel de instrumento). Regra: **medir contraste no render**, não só na tabela.

Espaçamento em grade de 4 px (`--sa-space-1…7` = 4, 8, 12, 16, 24, 32, 48). Margem segura 32 px. Alvo de clique mínimo 48 px de altura. Raios 4/8/12.

## Tipografia (px lógicos; altura lógica fixa em 720)

| Papel | Família | Tamanho | Em 960×600 (físico) | Observação |
| --- | --- | ---: | ---: | --- |
| Título do jogo | Outfit Bold | 40 (32 compacto) | 33 | Só no dossiê |
| Nome da missão | Outfit Bold | 30 (26 compacto) | 25 | |
| Pergunta da pasta | Outfit SemiBold | 24 (22 compacto) | 20 | Mais longa: SUR PT/EN, até 2 linhas |
| Legenda | Outfit Regular | 28 | 23 | Máx. 2 linhas, caixa máx. 880 px |
| Objetivo / prompt E | Outfit SemiBold | 22 | 18 | |
| Corpo / botões | Outfit SemiBold | 20–22 | 17–18 | |
| Carimbo / data / telemetria | Plex Mono SemiBold | 15 (13 compacto) | 12,5 | Caixa alta escrita no texto (USS não transforma) |
| Mínimo absoluto | — | 13 | 11 | Só carimbos secundários e dicas de tecla |

Medidas reais com as métricas da Outfit: a legenda mais longa do roteiro (MAR.C01, 100 caracteres PT / 99 EN) mede 1252 px a 28 px → duas linhas em 880 px. EN é mais longo que PT em 44 de 126 legendas e 28 de 68 textos de HUD. O objetivo mais longo (“Align the destination and send the correction”) mede 445 px a 22 px, dentro do cartão de 520 px (quebra em 2 linhas no 4:3, verificado na prévia).

Cobertura de glifos verificada com fontTools contra **todas** as tabelas PT/EN dos três roteiros: nenhum caractere ausente em Outfit ou Plex Mono.

## Componentes (`SpaceAppsComponents.uss`, prefixo `sa-`, BEM)

### Dossiê

- Três `sa-folder` lado a lado, mesma estrutura: rótulo do capítulo + **Detalhes** (opcional), foto 150 px (128 compacto, nunca encolhe), nome, carimbo destino·anos, **pergunta**, duração `5–7 MIN` e **Iniciar** (botão primário na cor do capítulo). Identidade local só por cor da faixa/botão e pela foto; a estrutura é idêntica.
- Iniciar fica sempre visível sem rolagem nas três resoluções (verificado nas prévias). A leitura detalhada fica em **Detalhes**, nunca bloqueia.
- Foto pendente usa `sa-photo--pending` (fundo blueprint + legenda “FOTO NASA · PENDENTE”) para nunca parecer imagem real. As fotos chegam por ART-001/PIO-007/SUR-001 com crédito.
- Rodapé: idioma, som/texto, créditos e fontes; aviso “Produção educativa independente · sem afiliação ou endosso da NASA” (encolhe e quebra linha, não sobrepõe botões). Sem logotipo NASA.

### HUD (camadas independentes)

| Camada | Classe | Posição | Regra |
| --- | --- | --- | --- |
| Objetivo persistente | `sa-objective` | Topo esquerdo, máx. 520 px | Carimbo de missão/local/ano + verbo e objeto + distância. Nunca dentro da legenda. |
| Pausa | `sa-pause` | Topo direito | Ícone desenhado + “Pausa” + “P / Esc”. Sempre clicável. |
| Confirmação | `sa-toast` | Abaixo da pausa | ✓ + resultado curto, ~2 s. |
| Indicador de borda | `sa-edge-cue` | Borda da tela | Seta + nome do alvo quando fora do quadro. |
| Prompt E | `sa-hold` | Centro inferior, acima da legenda | Ver estados abaixo. |
| Legenda | `sa-subtitle` | Base, máx. 880 px | Texto vazio ⇒ `sa-subtitle--hidden` (`display: none`): **sem placa vazia**. |

### Estados do prompt E (consumidos por UI-003 a partir de `InteractionService`)

Cada estado muda **texto + forma da tecla + cor**; nenhum depende só de cor.

| Estado | Classe | Gatilho do contrato | Tecla | Texto/status (ex.) |
| --- | --- | --- | --- | --- |
| Indisponível | — (prompt oculto) | sem candidato ou `Available = false` | — | nada é desenhado |
| Disponível | `sa-hold--available` | candidato ativo | contorno 3 px | “E · observar as esferas” / DISPONÍVEL · PRESSIONE E |
| Rearme | `sa-hold--rearm` | abriu a atividade; aguarda `InteractUp` | contorno fino, cinza | “Solte E para começar” / AGUARDANDO SOLTAR E |
| Segurando | `sa-hold--holding` | `HoldStarted` / `HoldProgress` | preenchida, levemente reduzida | “Segure E · esmerilhar” / OPERANDO · 60% + barra |
| Pausado (soltou) | `sa-hold--paused` | `HoldCanceled` com progresso > 0 | contorno âmbar | “Segure E para continuar” / PAUSADO · 60% PRESERVADO |
| Concluído | `sa-hold--done` | `HoldCompleted` | verde com ✓ | “Esmerilhado” / CONCLUÍDO |

Perda de foco/pausa: o prompt vai para **Pausado** e o menu de pausa assume (UI-006); retomar exige clique/soltura (rearme), conforme `03_ARCHITECTURE.md`.

### Painel de instrumento e fila de comando

`sa-instrument` (direita, 420 px; 380 compacto, placa 0,97): carimbo do instrumento, título, leituras `sa-readout` (rótulo/valor mono), resultado `sa-result` com ✓. A fila da Surveyor (`sa-command`) mostra `Preparado → Enviado → Comando em trânsito · 1,3 s → Executando → Comando confirmado`; etapa atual = ponto cheio maior + texto em destaque, concluídas = ponto cheio, futuras = contorno. Moldura do alvo no quadro com nome em carimbo (“PATA DA SONDA”) — o alvo é sempre nomeado, nunca um pixel a procurar.

### Identidade por capítulo

`.sa-chapter--mars|pioneer|surveyor` redefine `--sa-accent` (papel) e `--sa-accent-hud` (HUD). Selo **IMAGINAÇÃO / IMAGINATION** (`sa-seal-imagination`, moldura ciano) permanece visível durante toda a fantasia do cinturão (Pioneer).

## Variantes de resolução (prévias geradas pela Unity)

| Resolução | Largura lógica | Variante | Verificado nas prévias |
| --- | ---: | --- | --- |
| 960×600 (iframe padrão do itch.io) | 1152 | normal | Iniciar sem rolagem; pastas alinhadas; rodapé sem sobreposição |
| 1280×720 | 1280 | normal | referência |
| 1024×768 (4:3) | 960 | compacta | título 32, lede oculta, foto 128, pergunta 22, carimbo 13 |

Defeitos encontrados e corrigidos na primeira captura: (1) aviso do rodapé sobrepunha os botões em 960×600 e 4:3; (2) carimbo “JUPITER AND BEYOND · 1972–2003” vazava da pasta em 4:3; (3) pergunta da Surveyor em três linhas encolhia a foto e desalinhava as pastas em 960×600; (4) legendas da folha de componentes se sobrepunham; (5) contraste das placas (acima).

Prévias: `Docs/Art/UI_Previews/<Layout>_<L>x<A>.png` (6 layouts × 3 resoluções). Fundo liso substitui o 3D; cor clara de propósito para testar o pior contraste.

## Arquivos

| Caminho | Conteúdo |
| --- | --- |
| `Assets/_Game/UI/Design/SpaceAppsTokens.uss` | Variáveis de cor, espaço, tipo e raio |
| `Assets/_Game/UI/Design/SpaceAppsComponents.uss` | Componentes e estados |
| `Assets/_Game/UI/Design/SpaceAppsTheme.tss` / `SpaceAppsPanelSettings.asset` | Tema base e escala (gerados) |
| `Assets/_Game/UI/Design/Layouts/*.uxml` | Dossiê PT/EN, HUD PT/EN, painel de instrumento, folha de componentes |
| `Assets/_Game/UI/Design/Runtime/ResponsiveLayoutClass.cs` | Aplica `sa-layout--narrow` |
| `Assets/_Game/UI/Design/Editor/UIDesignAssetsBuilder.cs` | Menu **Abandoned Space Apps → UI-001 → Reconstruir assets de design** (idempotente) |
| `Assets/_Game/UI/Design/Editor/UIDesignCapture.cs` | Menu **… → Capturar prévias PNG** |
| `Assets/_Game/UI/Design/Preview/UIDesignPreview.unity` | Cena com um `UIDocument` por layout (só o dossiê ativo) |
| `Assets/_Game/Shared/Fonts/` | Outfit e IBM Plex Mono + `OFL.txt` (créditos em `ASSET_CREDITS.md`) |

Textos nos UXML são **amostras** para dimensionar layout, copiadas das tabelas dos roteiros; a fonte de texto do jogo será a localização (UI-004), sem frases em C#. Textos de interface propostos aqui e ainda sem revisão: “ARQUIVO · TRÊS MISSÕES”, “Escolha uma pasta. Cada capítulo começa e termina sozinho.”, metas “JÚPITER E ALÉM · 1972–2003”, rótulos de rodapé, e as traduções EN das perguntas de Marte (“Did Mars once have liquid water?”) e Pioneer (“What lay beyond Mars?”), que não existem nos roteiros.

## Para quem implementa (UI-002…UI-008)

1. Carregar `SpaceAppsTokens.uss` + `SpaceAppsComponents.uss` (os UXML já fazem isso) e usar `SpaceAppsPanelSettings.asset`.
2. Montar telas com as classes acima; trocar estado do prompt somente por classe (`EnableInClassList`), uma por vez.
3. Adicionar `ResponsiveLayoutClass` em todo `UIDocument` de tela cheia.
4. Não criar outra paleta por capítulo: só `sa-chapter--*`.
5. Ao mudar cor, alfa ou tamanho: recapturar as prévias e reconferir o contraste no render.

## Verificação

Executado nesta tarefa:

- Unity 6000.6.7f1 em batchmode compilou sem erros e importou USS/UXML sem avisos após a correção das fontes.
- Captura das 18 prévias por `UIDesignCapture` e inspeção visual de todas as variantes PT/EN; cinco defeitos encontrados e corrigidos.
- Contraste medido nos pixels renderizados; cobertura de glifos e largura de textos medidas com fontTools.

Ainda **não** executado (não declarar como feito):

- Revisão por pessoa que não leu o briefing (critério “compreensível em até 10 s”).
- Conferência no iframe real do itch.io e no navegador (o render offscreen do Editor não é o player WebGL).
- Leitura com fotos reais nas pastas e com o 3D atrás do HUD.
