# PIO-014 — evidência de validação

Executado em 14/11/2026 sobre `feature/PIO-014-rota-completa`, base `develop` com PIO-012 (e depois mesclada com `211ab0d`, correção de `Tools/sync_readiness.py` e documentos de QA), Unity **6000.6.7f1** / URP 17.6, Windows, batchmode (Unity fechada).

| Verificação | Resultado |
| --- | --- |
| Testes da tarefa (`PioneerFlowTests`) | **19/19** passaram |
| Suíte EditMode completa (antes da mescla final, que só trouxe `Tools/` e documentos) | **788 passaram, 18 falharam de 806** (`editmode.xml`). As 18 falhas são **todas** `MarsEagleTests` (MAR-012, de outra pessoa): `Assets/_Game/Chapters/Mars/Prefabs/EagleEnvironment.prefab` nunca foi commitado. Não é desta tarefa; nada da MAR-012 foi alterado |
| Builder `pioneer10.zchapter` reexecutado | Arquivos idênticos (cena, `PioneerChapter.asset`, sequência do Júpiter) |
| Build WebGL da cena `Pioneer10.unity` (cache limpo, base mesclada) | Sucesso: 17 arquivos, 14,74 MiB, shaders 3/3, hashes conferidos |

**Como os testes provam a rota sem injetar conclusões.** Usam o `ChapterDirector`, o `SessionClock`, o `InputRouter` e o `InteractionService` **reais**; só o `SceneManager` é trocado por um carregador instantâneo que, ao descartar a cena, faz o que o `OnDestroy` faria. A cena `Pioneer10` é a montada pelos builders. Um operador só mexe no teclado (E mantido 3,4 s, solto, toque de 0,1 s, solto) e a rota atravessa os oito estados: nenhum teste chama `CompleteStep` nem `Completed`. Cobertura: tabela de etapas igual ao roteiro, com as chaves do dossiê; sequência de cada estado com a duração nominal (soma 375 s) e a cena toda ligada; prévias fora do pacote e das Build Settings; os oito estados na ordem, cada um concluindo uma vez e o capítulo terminando uma vez (estado Completion, memorial aberto); tempo ativo por estado vindo do `SessionClock`, relatado como do operador (`CountsAsHumanResult` falso); nada conclui por tempo (2 min parado no Launch) e a pausa congela o relógio do estado; Voltar ao arquivo chega ao dossiê com tudo solto; Repetir recarrega a cena e recomeça do Launch; **reiniciar em cada um dos oito estados** (volta ao começo, relatório vazio, nenhuma atividade antiga rodando, nenhum lease antigo, mesmo número de GameObjects e um só `StepEntered`); reiniciar duas vezes não duplica nenhuma conclusão; destruir a cena solta os ouvintes; o estado 3 é só cinema e conclui pelo fim da sequência de 27 s.

**Não executado:** Play Mode e a cena carregada de verdade pelo `SceneManager`; a ligação do dossiê ao capítulo (é do shell da R2: o slot `pioneer10` do `ChapterCatalog` segue vazio, passo documentado no README) e a inclusão da cena nas Build Settings (R1); o enquadramento da sequência do estado 3 (`PIO_JupiterApproach`, nenhuma outra tarefa a montava) não foi visto no 3D (visto depois, ver "Correção do enquadramento" abaixo); teste humano; o áudio real. A duração medida é do operador, não um resultado humano.

## Correção do enquadramento (3D conferido depois da integração)

Os planos das sequências `PIO_Farewell` e `PIO_JupiterApproach` foram montados sobre os anchors da PIO-003 **sem** terem sido vistos no 3D. Eles foram renderizados depois (câmera real, anchors, luzes por anchor, rotação da sonda, `SequenceDirector` com tempo fixo, 1280×720) em instantes escolhidos; os quadros estão em `framing/`. A ferramenta que os gera é de desenvolvimento local e **não foi integrada**: as imagens são evidência de quadro, não um teste automatizado.

| O que se viu | Correção |
| --- | --- |
| `PIO08.Receding`: a sonda **crescia** (de ~145 px de diâmetro aos 21 s para o refletor ocupando o quadro aos 29 s) porque o keyframe final avançava +13 m no eixo do rig, em direção a ela | Recuo de 25 m (`z = -25`) e, no `PIO08.Memorial`, mais ~7 m (`z = -32`): a sonda passa de ~145 px a ~65 px de diâmetro; teste `TheRecedingAndMemorialShotsMoveTheCameraAwayFromThePioneerNeverTowardIt` |
| `PIO03.Instrument`: o fotopolarímetro estava no quadro só no primeiro instante; aos 3,5 s já saíra, porque a sonda continuava girando | `SpinScale = 0` no anchor `PIO.JupiterApproach.Instrument` (como o da placa); teste `TheInstrumentCloseUpKeepsTheInstrumentTowardJupiterForTheWholeShot` |

O que **não** é defeito da cena e foi só da ferramenta (documentado para não repetir o diagnóstico): a silhueta DSN só vai ao `DishSocket` no `Initialize` do controlador, e o céu só acompanha a câmera no `LateUpdate`; fora do Play Mode é preciso chamar `AttachToStage` e `SyncToCamera` à mão.

**Continua pendente:** Júpiter não "nasce de um ponto" (a abertura 70°→44° o amplia ~1,7×); o plano da placa (`PIO.Plaque`) tem um bloco escuro grande à esquerda e céu marrom pela luz de apresentação da PIO-003, e a PIO-009 sobrepõe o painel; o Play Mode inteiro (com a UI) continua não exercitado.

| Verificação | Resultado |
| --- | --- |
| Builders `pioneer10.stage`, `pioneer10.stage.farewell`, `pioneer10.zchapter` reexecutados | Só mudaram `SpaceStage.prefab` (um campo de anchor) e `PIO_Farewell.asset` (dois keyframes) |
| Testes EditMode filtrados por `Pioneer` | 186 de 186 passaram (os dois testes novos incluídos; 184 de 184 antes deles), inclusive a reconstrução idêntica do palco. A suíte completa não foi reexecutada nesta correção |

## Modelo definitivo da sonda no palco (15/11/2026)

O palco da PIO-003 ainda usava a `ProvisionalSilhouette`: o `PioneerActors.prefab` da PIO-002 existia, mas ninguém o instanciou (a troca estava só descrita no README). Agora o `PioneerStageBuilder` o instancia dentro de `ProbeSpin` e usa os sockets do modelo. O close da placa passou a ser calculado a partir da placa do modelo (a câmera fica no plano radial/axial da placa, porque a rotação alinha o azimute da placa ao da câmera).

| Verificação | Resultado |
| --- | --- |
| Builders `pioneer10.*` reexecutados (sonda → palco → atividades → capítulo) | Sem erro; a validação do palco acusou a câmera da placa a 0,43 m da sonda (mínimo 0,5 m) e a distância foi aumentada para 1,3 m |
| Testes EditMode filtrados por `Pioneer` | 186 de 186 passaram (um teste de `PioneerProbeTests` foi atualizado: os sockets do palco são os do modelo) |
| Build WebGL da cena `Pioneer10.unity` (cache limpo) | Sucesso: 17 arquivos, **15,67 MiB** (era 14,74), shaders 3/3, hashes conferidos |
| Capturas `Docs/Art/Pioneer10_Stage/` e `STAGE_STATS.md` | Regeneradas com o modelo novo; a ferramenta de captura agora pendura as silhuetas DSN e do Atlas nos sockets (como o controlador), senão elas apareciam como caixas pretas na origem |

**Limites:** o modelo não tem peça identificada como fotopolarímetro (socket hipotético); o ônibus e os RTGs não foram comparados com fotografias da missão; o custo é de build, não medido no navegador; o polimento de refletor e placa (normal maps, PIO-016) fica para o fim.
