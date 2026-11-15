# SUR-011 — Eclipse de 24 de abril de 1967

Entrega isolada da etapa `Eclipse → LunarNight`. Não integra Boot/Dossier nem implementa a noite ou a visita Apollo. Branch `SUR-011`, base `5014f0a` em develop; CORE-006 e UI-004 presentes.

## Executar e integrar

Abra `Assets/_Game/Chapters/Surveyor/Sequences/Eclipse/Preview/SurveyorEclipsePreview.unity`, pressione Play, Iniciar sequência e segure E (ou o botão equivalente). A preparação é de 0,8 s; soltar preserva preparo. A fila comum confirma imediatamente e executa depois de 1,3 s ativos. P/Esc/botão pausa. O menu permite reiniciar e trocar PT/EN.

O integrador injeta os serviços existentes em `SurveyorEclipse.Configure`. Reutilizar a mesma câmera, bindings da superfície, fila e espelho; não instalar o host de prévia na cena publicada. Conectar o `SequenceDirector` ao `SubtitlePresenter` existente e importar `EclipseTexts.json` no `LocalizationService`. A transição explícita termina em `LunarNight`; essa etapa terminal da prévia é somente um ponto de entrega.

Reconstrução: menu **Abandoned Space Apps → Surveyor → SUR-011 Reconstruir eclipse**, ou batch `-executeMethod Abandoned.SpaceApps.EditorTools.SurveyorEclipseBuilder.BuildFromCommandLine`. Não altera Build Settings.

## Montagem de 26 segundos

| Intervalo ativo | Imagem / legenda |
| --- | --- |
| 0–4 s | Espelho sobe; continuidade do enquadramento anterior. Data sai em 2 s. |
| 2–8 s | SUR-S20, Terra diante do Sol. |
| 4–22 s | TV em cores; exposição controlada. Recorte digital da imagem, sem mudar posição ou lente. |
| 8–14 s | SUR-S21, atmosfera terrestre. |
| 14–22 s | Oito segundos sem texto; halo vermelho irregular. |
| 22–26 s | Recorte desfeito e retorno à área da própria trincheira. |

Os 26 s são a reprodução após a recepção da ordem; a preparação do jogador e o trânsito não são limitados por um prazo. Câmera, espelho, imagem e legendas consomem exclusivamente o relógio da sequência, alimentado pelo SessionClock. O eixo óptico fica fixo e o corpo não gira. Movimento reduzido conserva o deslocamento físico necessário, mas troca o zoom digital por um recorte estático.

## Base científica e simplificações

- **S3-06:** [NASA, Eclipses Near and Far](https://www.nasa.gov/history/eclipses-near-and-far/), consultado 14/11/2026. Data e geometria: visto da Lua, é a Terra que oculta o Sol.
- **S3-05:** [NASA SP-184, Surveyor Program Results](https://ntrs.nasa.gov/citations/19690027073), seção de televisão, pp. 119–121, figuras 3-97/98. Inclinação favorável da sonda, limite superior do espelho e imagem perto da borda. Halo desigual, com regiões mais claras e lacunas; não um anel anular branco uniforme. Tabela 1-1: limite de elevação de 35 graus; seção topográfica: eixo da câmera inclinado 23,5 graus. URL antiga no roteiro retorna 404; texto oficial lido pelo endpoint NTRS.
- **Referência visual:** imagem NASA 67-H-483 já arquivada na pesquisa SUR-001, consultada novamente durante esta tarefa.

A cena é uma reconstrução, não uma transmissão histórica reproduzida. Um cartão de céu representa diâmetros aparentes aproximados de 1,9° para a Terra e 0,53° para o Sol; distâncias planetárias reais não são modeladas. O trajeto e a duração do eclipse são comprimidos para o roteiro. O espelho usa o limite documentado, mas não reproduz a calibração mecânica/óptica exata. O recorte digital facilita observar uma imagem pequena no alto da visão ampla; não sugere que o equipamento tivesse um zoom ilimitado. A borda usa um shader opaco simples, sem volumetria, HDR ou bloom.

A prévia enquadra a âncora SUR.Trench do cenário integrado; o sulco jogável pertence à SUR-009, em andamento com outro responsável. Esta tarefa não fabrica a conclusão daquela dependência.

## Recursos e custo

Materiais, shader e montagem novos de autoria do projeto, sem textura externa nova. Cena reutiliza cenário lunar e modelo da equipe (SUR-002), varredura/buffers TV (SUR-007), fila/hold (SUR-004), confirmação original de SUR-008 e UI/localização comuns. Créditos/licenças originais continuam aplicáveis; ver REUSE.md e os registros de origem dessas entregas.

Duas RenderTextures fixas de 640×480, uma câmera e uma luz principal. Nenhuma captura persistente de fotografia, vídeo ou chamada externa em runtime. O cartão tem dois triângulos. A confirmação é uma sonificação de interface, não som propagado no vácuo. O módulo não adiciona trilha musical: a direção de áudio global fornece a cama do capítulo.

Validação e limitações reais: `QA/SUR-011/README.md`.
