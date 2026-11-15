# SUR-0XX — Polir animação da Surveyor 3: retirada da câmera e quique do pouso

**Função:** R6 — Surveyor 3 · **Prioridade:** P1 (polimento) · **Tipo:** ajuste visual, sem mudança de contrato

## Contexto

Na cena documental da Apollo 12 (20 nov 1969), dois trechos da animação da Surveyor 3 estão com aparência ruim:

1. **Retirada da câmera.** Os astronautas pegam a câmera da sonda, mas ela sai pela metade e a animação fica feia. A câmera aparece cortada ou atravessando o corpo da sonda.
2. **Quique inicial no pouso.** O "kick" da Surveyor 3 ao tocar o solo parece cartoon. Precisa ficar realista, como um pouso em gravidade lunar.

## O que fazer

### A. Retirada da câmera (`SurveyorApolloCast`)

- Hoje, em `CameraPickupSeconds` (27 s), as partes da câmera embutidas na sonda são desligadas (`cameraParts`). Um objeto separado (`recoveredCamera`) aparece e é interpolado em cerca de 0,8 s até a mão do astronauta. Essa troca por clone é a provável causa do visual "pela metade". Pontos a verificar:
  - o clone e a câmera original não coincidem na pose, na escala nem no pivô;
  - a câmera é desacoplada do suporte sem haver separação do mastro/fixação;
  - ela atravessa o mastro ou a estrutura durante o `Lerp`.
- Fazer a câmera se soltar do suporte, se afastar pelo caminho livre e só então ir para a mão, sem recorte nem atravessamento.
- Garantir que não restem partes duplicadas ou faltando na sonda depois da retirada.
- Manter os tempos já sincronizados com legenda e áudio (câmera aos 27 s, pá aos 35 s, pose final aos 44 s). Mudar um tempo só com ajuste explícito no mesmo PR.

### B. Quique do pouso (`SurveyorLandingMotion`)

- Revisar `ContactTilt`: a oscilação senoidal rápida (`sin(age*9)`) com amortecimento forte lê como "mola de desenho animado".
- Substituir por uma resposta mais física:
  - balanço lento e assimétrico;
  - pernas e amortecedores absorvendo o impacto;
  - assentamento gradual a cada contato.
- Revisar `Height` e o assentamento final para que o toque não pareça elástico.
- **Manter os fatos do roteiro:** a Surveyor 3 pousou em três contatos, com dois saltos (valores atuais ~10 m e ~3 m). Gravidade lunar de 1,62 m/s². Não inventar números nem alegar reconstrução de telemetria. O movimento continua editorial, como o comentário da classe já diz.
- Se houver efeito de poeira ou áudio ligado ao contato (`SurveyorLandingEffects`, `SurveyorDescentAudio`), conferir que continua sincronizado.

## Critérios de aceite

- [ ] A câmera sai inteira do suporte, sem partes cortadas, duplicadas ou atravessando a estrutura, em nenhum ângulo da câmera da cena.
- [ ] O pouso não mostra oscilação de "mola"; os 3 contatos têm resposta distinta e plausível.
- [ ] Tempos de legenda, áudio e pose final continuam sincronizados.
- [ ] Sem alocação por frame nova; o desempenho Web não regride.
- [ ] Testes existentes de Documentary e Surveyor passam. Adicionar teste para altura e inclinação nos contatos, se a fórmula mudar.

## Como verificar

- Rodar a cena documental no Editor e gravar um clipe curto do pouso e da retirada da câmera (antes e depois).
- Rodar o smoke Web e anexar a evidência ao PR.
- Rodar os EditMode/PlayMode do capítulo Surveyor.

## Arquivos prováveis (dono: R6)

- `Assets/_Game/Chapters/Surveyor/Documentary/SurveyorApolloCast.cs`
- `Assets/_Game/Chapters/Surveyor/Documentary/SurveyorLandingMotion.cs`
- `Assets/_Game/Chapters/Surveyor/Documentary/SurveyorLandingEffects.cs` e `SurveyorDocumentaryBuilder.cs` (somente se a configuração da câmera precisar de ajuste)

Não editar cenas globais, Boot ou Dossier (são de R1).

## Fora de escopo

Mudar roteiro, legendas ou duração do capítulo, e trocar os modelos 3D. Se o problema da câmera estiver no modelo, registrar isso no PR.

## Observação

As causas indicadas no item A são hipóteses a partir da leitura do código; confirmar rodando a cena antes de corrigir.
