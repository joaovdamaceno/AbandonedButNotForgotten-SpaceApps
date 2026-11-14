# AUD-002 — evidência de validação

Executado em 14/11/2026 sobre `feature/AUD-002-diretor-audio` (base `develop` a2dc54a), Unity **6000.6.7f1** / URP 17.6, Windows, Editor aberto (Unity MCP, EditMode). A suíte completa **não** foi executada: só as classes de áudio e as da Pioneer (a única consumidora do diretor).

| Verificação | Resultado |
| --- | --- |
| `AudioDirectorTests` (19) + `AudioDirectorHoldIntegrationTests` (3, com `InteractionService` e `InputRouter` reais e teclado simulado) | **22/22 passaram** |
| Essas duas classes + as 12 classes de teste da Pioneer (incluindo `PioneerPlaqueTests`) | **169/169 passaram** |
| `AudioDirectorSetup.Create()` | Catálogo e prefab criados; reexecutar não duplica; o catálogo valida e todos os cues planejados estão pendentes |

A primeira execução achou **um bug real** (o clipe era sorteado duas vezes: a variação "nunca repete" só valia para o sorteio descartado) e três erros dos próprios testes (duração de clipes menor que a espera). Todos corrigidos.

Cobertura: catálogo (IDs, buses, loop curto, cama de música abaixo de 30 s, duplicados, cue pendente não é erro); crossfade de potência constante, fade limitado a 0,8–2 s e corte seco só com 0; o mesmo cue não reinicia; **uma escrita de volume por voz por quadro**, inclusive com pedidos no meio de um fade; cue pendente e cue desconhecido sem exceção; nível da música e ducking de −6 dB com ataque, retorno e motivos múltiplos; buses independentes e Master multiplicando; mudança de volume em rampa; liberação do áudio só após `Unlock()` (música pedida antes entra, efeitos pedidos antes são descartados); variações sem repetição e altura dentro da faixa; loops sem duplicar e com teto (12 s declarado, 40 s padrão, música sem teto); silêncio real (música e loop caem a zero, efeito novo descartado, interface responde, pausa congela, volta em 1 s); pausa por várias origens (interface não pausa); `StopAll`/`ResetState` e o mesmo cue voltando a tocar; sons do instrumento nos eventos reais do hold (liga, altura sobe, desacelera ao soltar, para ao concluir, ignora o que não registrou, nada sem hold, pausa do shell); adaptador da Pioneer repassando a porta; 15 `AudioSource` 2D e nenhum `AudioListener`.

**Não executado / limitações:** nenhum áudio real (nenhum arquivo foi adquirido; a AUD-001 só tem candidatos), logo nada foi **ouvido**: equilíbrio entre buses, emendas de loop, duração das camas e o "som agressivo depois de 30 s" só têm a garantia estrutural do teto de duração. **Web (AudioContext) e Play Mode não foram exercitados**, nem o orçamento de 10 MB. A ligação do `Unlock()` ao botão Iniciar, das preferências a eventos de mudança e do mapeamento candidato → cues de cada capítulo é da integração. Mars e Surveyor continuam com áudio próprio.
