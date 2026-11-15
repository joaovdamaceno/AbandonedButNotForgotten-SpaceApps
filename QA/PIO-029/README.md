# PIO-029 — Retemporização do roteiro v2

Cada passo aproxima uma cena do tempo do roteiro (alvos: Launch 55, Belt 50, Jupiter 30, Imaging 45, Closest 65, Plaque 20, Outbound 55, Listening 40, Farewell 40 = 400 s) sem passar do teto de 7:00 em nenhum momento. A regra de leitura do projeto (16 caracteres por segundo dentro da janela de cada legenda) impõe um mínimo à placa (~29 s de leitura, alvo 20 s) e à foto (~45 s sem nenhuma folga, alvo 45 s); nelas o passo escolhe o mínimo viável (31 s e 48 s) em vez de cortar texto.

| Passo | Cena | Antes → depois | Total | O que mudou |
| --- | --- | --- | --- | --- |
| 1 | `PIO.Belt` | 54 s → 50 s | 411 s → 407 s | Dolly zoom de 15 para 11 s (as chaves de câmera escalam junto); legendas 02.04 a 02.06 4 s mais cedo; o contador de dias acompanha (rampa até 30 s, pausa até 40 s, 15 FEV 1973 em 47 s) |
| 2 | `PIO.Plaque` | 47 s → 33 s | 407 s → 393 s | Chegada 7,5 s, encaixe 20 s (peças a cada 4 s em vez de 5,5 s, a primeira em 0,4 s), revelação 5,5 s; as legendas das peças ficam de 3,6 a 4 s (o tempo de ler a 16 caracteres por segundo no pior idioma); o roteiro pede 20 s, mas só a leitura exige ~33 s |
| 3 | `PIO.Imaging` | 74 s → 64 s | 393 s → 383 s | Aprender 16 para 10 s, passagens 32 para 28 s (marcos em 11, 17, 21,5 e 27 s, e as legendas dos passos com o tempo exato de leitura: 6, 4,5 e 5,5 s) e a revelação segue 26 s (amarrada à imagem histórica); o roteiro pede 45 s, mas só ler os textos exige ~45 s e as legendas dos passos precisam de leitura em sequência (mínimo estrutural ~56 s sem cortar texto) |
| 4 | `PIO.Listening` | 52 s → 40 s | 383 s → 371 s | Ensino 6 s, contatos 5,5 / 5 / 7,5 s, tentativa 7 s, respiração final 9 s; as legendas dos contatos com o tempo de leitura do pior idioma (4,8 / 4,5 / 6,8 s); é o tempo do roteiro (40 s) |
| 5 | `PIO.Farewell` | 38 s → 40 s | 371 s → 373 s | Memorial de 8 para 10 s e botões em 38 s (nos últimos 2 s dos 40 s); é o tempo do roteiro (40 s) |
| 6 | `PIO.JupiterApproach` | 27 s → 30 s | 373 s → 376 s | Plano aberto de 10 para 13 s; a Terra de comparação sai em 22 s e a legenda do tamanho (11 vezes mais largo) cai na entrada dela (9 a 15 s); é o tempo do roteiro (30 s) |
| 7 | `PIO.Closest` | 40 s → 65 s | 376 s → 401 s | Luas 12 s (4 s cada), radiação 15 s (12 a 27 s), passagem 14 s, curva 14 s e saída 10 s; as sete legendas com janelas de leitura de 6 a 8 s; o HUD acompanha (medidor de 12 a 27 s, barras e velocidade de 35 a 42 s); é o tempo do roteiro (65 s) |
