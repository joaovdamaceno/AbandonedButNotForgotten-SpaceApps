# SUR-002 — validação da cena lunar

Base: `develop` **8dab860** (SUR-001 integrada pelo PR #94; última CORE: CORE-006). Branch `SUR-002`, responsável `joaovdamaceno`, issue #60. Execução em 14/11/2026, Windows, Unity **6000.6.7f1**.

## Entrega verificada

- Cena `Surveyor.unity`, prefabs `LunarSite` e `SurveyorVisibleRig`, materiais, malhas, importação do modelo da equipe e referências persistidas pelo Editor.
- Região próxima 40×40 m, contexto de cratera, rochas assentadas e oito âncoras locais. Apenas uma câmera, um AudioListener e uma luz direcional; sem partículas atmosféricas.
- Modelo recebido do usuário escolhido após comparação visual; originais preservados. Versão da superfície com **24.499 triângulos, seis partes e um material**, contra 66.222 triângulos/616 malhas/79 materiais do GLB original. Dois mapas de 1024 px. Versão com retrofoguete apenas arquivada para SUR-006.
- Cena completa: **127.467 triângulos, 83 malhas distintas, três materiais, quatro texturas e 431 renderers**, incluindo o contexto e todas as rochas, mesmo fora da vista. Esses números não são draw calls medidos.
- TV de 640×480 com ciclo de criação, reutilização e liberação testado; apresentação preserva proporção. CameraDirector comum reutilizado. Nenhum novo leitor de E ou controlador de missão.

## Testes executados

**127 testes EditMode passaram, zero falhas/ignorados**, processo Unity terminou com código 0. Evidência: `all-editmode.xml`, incluindo 103 testes de infraestrutura, 17 do catálogo científico e sete da cena. O teste da TV entra/sai de Play Mode e confirma o buffer criado, removido ao desabilitar e único após reativação.

Os testes da cena cobrem referências, âncoras locais, rejeição de pata/rocha suspensa e câmera enterrada, obstrução por collider, comparação do collider do terreno com a superfície, reconstrução preservando GUIDs/GlobalObjectIds não nulos e raiz de outro dono. Testes que adulteram a cena descartam suas alterações.

Houve uma falha inicial no setup do teste após entrar em Play Mode; corrigida para não abrir cenas pelo Editor durante o jogo. Não foi mascarada com exclusão do teste. A troca do modelo exigiu corrigir o ajuste de pose pelos três contatos e reposicionar os enquadramentos conforme os eixos realmente importados.

## Inspeção visual

Capturas em `Captures/`, renderizadas pela própria Unity via URP/Direct3D11, **NVIDIA GeForce RTX 3050 6GB Laptop GPU**:

- `LandingMarks-wide.png`, `CraterSlope-wide.png`, `Footpad-wide.png`: três alvos educativos.
- `Footpad-detail.png` e `Trench-wide.png`: detalhe e área preparada para a próxima interação.
- `Rig-inspection.png` e `Ground-support.png`: composição externa de inspeção e apoio visto de baixo; não são câmeras jogáveis do capítulo.

A inspeção encontrou e corrigiu uma emenda entre resoluções de terreno, grão com padrão de listras, marcas pouco legíveis e enquadramentos obstruídos após a importação. As capturas finais devem ser usadas para revisão artística, não como aprovação de filme ou avaliação com estudantes.

O primeiro pedido de renderização URP em batch produziu uma leitura incorreta de iluminação. O capturador agora aquece três renderizações antes do readback; a imagem final foi inspecionada. Essa ferramenta de evidência não é uma cinematic pré-renderizada usada no jogo.

Logs integrais, comparação Blender e fontes de pesquisa em `QA/Local/SUR-002/` (ignorada). O conversor e as alterações do modelo são versionados; originais intactos em `SourceAssets/Surveyor3/`. Alterações automáticas de QualitySettings/URP feitas pelo Editor/testes são descartadas da entrega.

## Limites reais

Não houve build Web, teste de FPS/frametime, carregamento ou GPU integrada nesta tarefa. O perfil ativo do Editor usa URP; não afirmar que a captura em RTX certifica o perfil Leve do navegador. Sombras distantes e detalhes pequenos ainda pedem revisão artística nos dispositivos de referência.

Sem fluxo completo do capítulo, tutorial, áudio, legendas, minigames, eclipse animado ou visita Apollo: são tarefas posteriores. A cena não foi adicionada às Build Settings, que pertencem à integração R1. Nenhuma raiz global ou arquivo do Unity antigo foi necessário.

Recriar pelo menu **Abandoned Space Apps → Surveyor → SUR-002**. O README da pasta de arte descreve os contratos para SUR-003/006/007/008/009/010/011/013. Manter a issue em revisão até integração; não liberar dependentes apenas por mover o cartão.
