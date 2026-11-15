# SUR-001 — catálogo científico e referências

Tarefa: [#59](https://github.com/joaovdamaceno/AbandonedButNotForgotten-SpaceApps/issues/59). Base de implementação: `develop` em `934a2fd`, com CORE-001–006 e ART-001 integradas. Branch: `SUR-001`.

Esta entrega contém dados e ferramentas de validação. Não monta a cena, não importa modelos/fotos para a distribuição e não substitui a SUR-014 de localização. A revisão cruzada R7 permanece pendente; não fechar a dependência antes da revisão e integração.

## Usar e reconstruir

- Fonte autoral única: `../Art/ReferenceManifest.json`. Contém fontes, decisões PT/EN, 31 vínculos com o roteiro, dez referências visuais, oito itens de inventário e checklist R7.
- `SurveyorScience.asset` é um **ScriptableObject tipado**, gerado desse JSON. Pode ser referenciado pelo Inspector nos consumidores do capítulo, sem internet em runtime.
- `SurveyorScience.cs` fornece `TryGetFact(id, out fact)`, listas de fontes/fatos/cues e `Validate()`. IDs `SUR.F.*` são decisões utilizáveis; `SUR.C.*` preservam conflitos bloqueados. `approvedForDisplay` indica a seleção editorial do catálogo, **não aprovação humana R7**. Consumidores devem respeitar esse campo e o tratamento de reconstrução.
- No Editor: **Abandoned Space Apps → Surveyor → SUR-001 → Gerar e validar catálogo científico**. A reconstrução conserva o `.meta`/GUID existente e recusa substituir um arquivo de outro tipo.
- Via batch: Unity 6000.6.7f1, `-batchmode -nographics -projectPath <raiz> -executeMethod Abandoned.SpaceApps.Chapters.Surveyor.EditorTools.SurveyorScienceBuilder.BuildFromCommandLine -logFile <log>`. O método salva e encerra com código 0/1; não abre/altera cenas ou Build Settings.
- Testes: Test Runner, EditMode, assembly `Abandoned.SpaceApps.Surveyor.Tests`. Em batch, usar `-runTests -testPlatform EditMode -testFilter Abandoned.SpaceApps.Chapters.Surveyor.Tests -testResults <xml>`. Não acrescentar `-quit` ao comando de testes.

Editar o JSON, reconstruir o asset e executar os testes no mesmo PR. Não editar o YAML gerado isoladamente. Legendas e runtime usam seus próprios contratos/chaves; os resumos científicos deste catálogo não são automaticamente exibidos como legendas.

## Decisões que mudam a implementação

| Tema | Saída segura | Evidência e ressalva |
| --- | --- | --- |
| Pouso | `20 ABR 1967 · 00:04 UTC · OCEANO DAS TEMPESTADES` | Part I, PDF p.13: `:17`; Part II, p.8, e NSSDCA: `:53`. Não afirmar que a diferença foi reconciliada como primeiro/último toque. |
| Coordenadas | Região e cratera, sem par numérico híbrido | Part II: 2,94° S / 23,34° O; NSSDCA: 3,01° S / 336,66° L. Converter longitude não resolve a latitude. |
| Ensaios | Quatro trincheiras; testes de resistência e impacto | **Divergência nova em relação ao passo 3 da issue:** NASA Science lista 4 carga/13 impacto; Part I p.61 e Part II p.9 listam 8/14. Não publicar contador exato antes da reconciliação R7. |
| Imagens | Total retrospectivo adotado: 6.326, atribuído ao resumo NASA | Part II p.9 cita 6.315 no período 20/4–3/5. Não atribuir a diferença a categorias específicas sem prova. Não é um contador da partida. |
| Resistência do solo | Comportamento qualitativo, local | Não publicar 98 kPa; 10 psi ≈ 68,95 kPa. Método, área e tamanho de apoio importam. Coesão não demonstra água. |
| Encosta | Cratera ~200 m, parede leste, sonda inclinada | Part II pp.8/11; Fig.IV-12 de Part I. Inclinação exata e massa também divergem; omitidas do HUD. |
| Pá | Braço pantográfico, escavação/pressão/impacto | Part II p.8 e Fig.IV-68. Foto da trincheira em SP-284 p.186 mostra parede ~6 cm; não usar profundidade máxima em toda operação. |
| Eclipse | 24/04/1967; Terra oculta o Sol | NASA 67-H-483; luz avermelhada explicada pela atmosfera terrestre. Não citar as falas da Apollo em outro eclipse como se fossem desta observação. |
| Silêncio | Último contato 04/05/1967; sem resposta posterior | Part I p.62 descreve desligamento comandado e tentativas na lunação seguinte. Não animar uma falha interna inventada como fato observado. |
| Apollo | Pouso em 19/11/1969 UTC; visita em 20/11 | NASA History e foto AS12-48-7134; cerca de 31 meses, não mais de três anos. |
| Visita vista da câmera | Reconstrução anunciada antes da sequência (`SUR-S26`) | A câmera estava inativa. Sem `REC` que sugira registro original de 1969; a foto real posterior é identificada como arquivo. |
| Peça recuperada | Câmera e pá de voo, além de outras peças | SP-284 PDF p.14, figuras 2–4. Inventário Smithsonian A19761264000 é artigo de teste, não substituir a peça de voo. Exposição atual da pá não verificada. |

Todos os cues `SUR-S01`–`SUR-S31` estão ligados a decisões com citações. `SUR-S05` e a frase final são conteúdo editorial; a visita é reconstrução; instruções de teclado não são alegações sobre interfaces históricas.

## Proveniência e inspeção

Fontes e locators completos estão no manifesto, com URLs canônicas e SHA-256 dos três PDFs consultados. A paginação anotada é a **página do arquivo PDF, contada a partir de 1**, acompanhada do identificador da figura. Não confundir com a página impressa.

- [JPL TR 32-1177 Part I](https://ntrs.nasa.gov/citations/19670028267): introdução, voo, operações e figuras IV-1/12/13/68. O antigo link ALSJ presente no planejamento retornou 404; o NTRS é o endereço canônico usado nesta entrega.
- [Part II](https://ntrs.nasa.gov/citations/19670028832): introdução/síntese nas páginas PDF 8–11, incluindo topografia, solo e divergências de contagem. Leitura seletiva, não declaração de auditoria integral das 219 páginas.
- [NASA SP-284](https://ntrs.nasa.gov/citations/19720019081): PDF p.14, câmera/pá retornadas; p.186, comparação do apoio e trincheira. Figuras inspecionadas visualmente.
- [NASA History — eclipses](https://www.nasa.gov/history/eclipses-near-and-far/): imagem 67-H-483 inspecionada em pesquisa local. [NASA Science — física dos eclipses](https://science.nasa.gov/moon/eclipses/) sustenta a explicação do vermelho.
- [Apollo 12](https://www.nasa.gov/history/55-years-ago-apollo-12-makes-a-pinpoint-landing-on-the-moon/) e [fotografia AS12-48-7134](https://science.nasa.gov/resource/apollo-12-and-surveyor-3/): cronologia e referência final do LM/Surveyor.

PDFs, extrações e renders de pesquisa estão em `QA/Local/SUR-001/`, ignorada pelo Git e fora da build. **Não há imagens, modelos ou texturas externos importados nesta tarefa.** Os destinos do manifesto são propostas para as tarefas de arte; `localPath` vazio e `reference-only` são intencionais. Os créditos/termos serão confirmados por arquivo quando houver importação, conforme as [diretrizes NASA](https://www.nasa.gov/nasa-brand-center/images-and-media/). O registro Smithsonian inacessível não sustenta fatos publicáveis nem imagens liberadas.

Código desta tarefa é novo. O roteiro e a matriz de planejamento do próprio projeto (commit `3f8d1d3`) foram auditados/adaptados como referência editorial; nenhum código, modelo ou teste do projeto Unity anterior foi copiado. O histórico não foi reescrito.

## Revisão antes de integrar

R7 deve revisar os conflitos de segundos/coordenadas, 4/13 versus 8/14, o tratamento das imagens e a identificação da reconstrução de 1969. Precisão de massa/inclinação continua opcional e bloqueada. Automação verifica estrutura, referências e regras editoriais; não substitui essa revisão científica humana.

As tarefas dependentes só ficam liberadas após merge/fechamento com evidência. Esta entrega não aprova gameplay, duração com novatos, gráficos, navegação ou build Web do futuro capítulo.

## SUR-014 — textos e vínculo dos 31 cues

- `../Localization/surveyor.pt-BR.json` e `surveyor.en.json`: 77 chaves iguais nos dois idiomas (31 cues `SUR-S01–S31`, 26 páginas `.p1/.p2` de 13 frases de dois períodos, 20 textos de UI/objetivos com prefixo `sur.`). Texto literal do roteiro; editar aqui, nunca em C#.
- `SurveyorSubtitles.asset` (`SurveyorSubtitles.cs`): para cada cue, ato, estado, sequência/clip, janela local **ou** gatilho, leitura mínima, cue anterior e páginas. `bindingStatus = built` repete o vínculo já montado pelos atos (conferido por teste contra os assets de sequência); `proposed` são IDs/janelas que o dono do ato ainda precisa adotar: gatilhos `surveyor.scoop.bearing/impact/result/comparison` (SUR-010) (SUR-010). S26–S31 já são `built`: a SUR-013 montou `surveyor.seq.Visit` com as janelas 0/6/20/32/38/44 s, terminando em 50 s.
- Uso: `catalog.BuildTableJson(errors)` → `LocalizationService.AddTable(json, "surveyor")`; `catalog.BuildCues(sequenceId, paged)` devolve `SubtitleCue` comuns; com fonte ampliada (`paged = true`) cada frase de dois períodos vira duas telas que somam o mesmo tempo (o gatilho da segunda é `gatilho + ".next"`, disparado pelo dono ao fim da leitura mínima da primeira).
- Os JSONs locais dos atos (`UI/*Texts.json`, `Sequences/*/…Texts.json`) continuam válidos: os `SUR-Sxx` que eles repetem (25 na última conferência) têm o mesmo texto (teste). O cue extra `SUR-S08-PLAYER` (recibo do envio pelo jogador) é do ato de descida e não faz parte dos 31.
- Testes: `Tests/EditMode/SurveyorSubtitlesTests.cs` (assembly `Abandoned.SpaceApps.Surveyor.Tests`).
