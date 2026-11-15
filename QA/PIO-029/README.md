# PIO-029 — Retemporização do roteiro v2

Cada passo aproxima uma cena do tempo do roteiro (alvos: Launch 55, Belt 50, Jupiter 30, Imaging 45, Closest 65, Plaque 20, Outbound 55, Listening 40, Farewell 40 = 400 s) sem passar do teto de 7:00 em nenhum momento. A regra de leitura do projeto (16 caracteres por segundo dentro da janela de cada legenda) impõe um mínimo à placa (~29 s de leitura, alvo 20 s) e à foto (~45 s sem nenhuma folga, alvo 45 s); nelas o passo escolhe o mínimo viável (31 s e 48 s) em vez de cortar texto.

| Passo | Cena | Antes → depois | Total | O que mudou |
| --- | --- | --- | --- | --- |
| 1 | `PIO.Belt` | 54 s → 50 s | 411 s → 407 s | Dolly zoom de 15 para 11 s (as chaves de câmera escalam junto); legendas 02.04 a 02.06 4 s mais cedo; o contador de dias acompanha (rampa até 30 s, pausa até 40 s, 15 FEV 1973 em 47 s) |
| 2 | `PIO.Plaque` | 47 s → 33 s | 407 s → 393 s | Chegada 7,5 s, encaixe 20 s (peças a cada 4 s em vez de 5,5 s, a primeira em 0,4 s), revelação 5,5 s; as legendas das peças ficam de 3,6 a 4 s (o tempo de ler a 16 caracteres por segundo no pior idioma); o roteiro pede 20 s, mas só a leitura exige ~33 s |
