# SUR-004 — comandos Terra–Lua

Abra `Preview/CommandDelayPreview.unity` na Unity **6000.6.7f1** e aperte Play. Clique em **Iniciar verificação**. Setas/botões movem o contorno; segurar E ou o botão prepara o comando. Soltar conserva o preparo. A marca recebida só muda depois do trânsito. P/Esc ou o botão pausa; Reiniciar e Voltar ao início invalidam ordens antigas. PT/EN e a preferência comum de alternância são suportados.

Este é um banco de verificação do adaptador, com diagrama e um `ReceivedMirrorPivot` real. Não é o capítulo completo, a TV lunar, um log histórico nem um novo dossiê. A câmera/instrumento final continua na SUR-007. Nenhuma cena global ou BuildSettings foi modificada.

## Componentes e integração

- `SurveyorCommandDelay`: uma operação pendente; relógio acumulado do SessionClock; geração por etapa; envio síncrono e entrega após `OneWayDelaySeconds` (1,3 s ativos). Não adquire input, não cria relógio alternativo e não avança missão.
- `SurveyorCommandInteraction`: oferece um candidato ao InteractionService comum, preserva o preparo e captura o alvo quando o hold termina. Não lê teclado; mouse e modo alternância passam pelo mesmo BeginHold. Reentrada/saída limpa a preparação; a pausa a preserva.
- `CommandTransitView` + `UI/CommandTransit.uxml`: widget reutilizável com contorno desejado, marca recebida, preparo, sinal em trânsito e textos por chave. Sem rotação de atores ou mudança de etapa na view.
- `SurveyorCommandPreviewHost`: entrada local com os Core reais, loader restrito à cena realmente aberta, preferências e pausa por foco. O prefab tem serviços para executar sozinho; **não duplicar esses Core no Boot**. R1 deve ligar os componentes aos serviços já existentes no capítulo.

Exemplo para o responsável de uma etapa:

```csharp
queue.Configure(chapterDirector, sessionClock);
action.Configure(queue, interactionService, inputRouter);
queue.BeginState(chapterDirector.CurrentStepId);
action.Offer(candidate, () =>
{
    Vector2 target = desiredOrientationDegrees; // Captura no aceite, não quando o rádio chega.
    return () => mirror.SetOrientation(target);
});
```

`candidate` é o contrato Core com ID, verbo/objeto localizados, âncora e HoldSeconds próprios da ação. O preview usa **1,5 s de preparo**; os próximos instrumentos escolhem sua duração. `Offer` rejeita uma segunda oferta enquanto a primeira existir; use `ClearAction` se substituir a atividade. Não deve ser chamado a cada frame. Após `Received`, o consumidor pode oferecer a próxima ação. O serviço exige a soltura de E antes de aceitá-la se a tecla ainda estiver mantida.

`TrySend(id, Action)` também atende comandos sem hold, como a assistência histórica explícita de corte dos motores. É rejeitado fora da etapa ativa, pausado, com ID inválido, callback nulo ou outra ordem pendente. Não autoriza completar ciência automaticamente. O callback inicia a ação física; `Received` confirma entrega, **não** que uma animação longa terminou ou que houve resposta completa do rádio. Só o proprietário do capítulo decide transições.

Configure antes de BeginState. A fila escuta mudança de etapa, descarregamento, conclusão e desativação para invalidar callbacks. `BeginState` inicia uma geração nova; consumidor deve chamá-lo uma vez ao entrar, inclusive na reentrada. A desativação/reconfiguração não reativa automaticamente o estado anterior. Não chamar Tick manualmente além do Update em produção.

## Tempo, segurança e UI

O instante de envio usa `SessionClock.ActiveSeconds`. Isso evita que a ordem consuma o delta do frame de aceite apenas porque o Update da fila aconteceu depois do Update do instrumento. Pausa e inatividade não acumulam trânsito. A tolerância numérica é 0,1 ms; a chegada efetiva ocorre no primeiro frame ativo que alcança a duração.

O callback é retirado da fila **antes** de executar. Reentrada no Tick não o repete; reiniciar/trocar de etapa dentro dele não publica uma confirmação antiga na etapa nova. Uma exceção do instrumento fica visível, sem repetir o callback a cada frame.

`Changed` publica Enviada e atualiza o binding no mesmo frame de `TrySend`. `LastFeedbackFrames` mede frames até esse binding, não latência de apresentação no monitor. Mouse, setas, navegação, pausa e feedback não entram na fila. O alvo desejado pode mudar enquanto outra ordem viaja, mas a operação recebida usa o alvo capturado no envio.

`TextResolver` permite ligar a localização global quando UI-003 estiver integrada. Até lá, `CommandTransitTexts.json` contém PT/EN, utilizando o leitor de catálogo já integrado na SUR-003. O widget não depende do prefab/cena Launch ou de sua sequência.

## Construção e evidência

Menu **Abandoned Space Apps → Surveyor → SUR-004 Reconstruir verificação de comandos**. Módulo `surveyor.commandTransit` usa o framework CORE-006 e só escreve a própria raiz/pasta. O builder valida referências e não troca GUIDs em reconstrução.

Batch: `-executeMethod Abandoned.SpaceApps.EditorTools.SurveyorCommandBuilder.BuildFromCommandLine`. Capturas com gráficos: `-executeMethod Abandoned.SpaceApps.EditorTools.SurveyorCommandCapture.CaptureFromCommandLine`. Testes e resultados em `QA/SUR-004/` na raiz do repositório.

1,3 s representa didaticamente **Terra → Lua**, segundo a matriz `Docs/Chapters/03_SURVEYOR_SOURCES.md`; a distância real varia e ida + resposta exigem mais tempo. Não é a latência da sonda próxima da Terra no lançamento. O ato Launch da SUR-003 conserva sua montagem de fonte única; este adaptador atende as ordens dos instrumentos subsequentes. Sem novos assets externos, pacotes, áudio ou serviços de runtime. Fontes/tokens existentes da UI-001 preservam suas licenças.
