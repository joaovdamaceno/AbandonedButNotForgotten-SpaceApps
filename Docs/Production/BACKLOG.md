# Backlog completo

84 tarefas. P0 é a história completa; P1/P2 só após as três experiências funcionais. Estimativas são hipóteses, não promessa de execução.

| ID | Tarefa | Função | Prioridade | Horas | Depende de |
| --- | --- | --- | --- | ---: | --- |
| [CORE-001](../Tasks/CORE-001.md) | Inicializar o projeto Unity e fixar Editor, URP, pacotes e pastas | R1 | P0 | 0.75 | — |
| [CORE-002](../Tasks/CORE-002.md) | Implementar e congelar os tipos e contratos globais | R1 | P0 | 0.75 | CORE-001 |
| [CORE-003](../Tasks/CORE-003.md) | Criar InputRouter e interação única com E mantido e rearme | R1 | P0 | 1.75 | CORE-002 |
| [CORE-004](../Tasks/CORE-004.md) | Implementar o diretor de sequências com um único relógio | R1 | P0 | 1.5 | CORE-002 |
| [CORE-005](../Tasks/CORE-005.md) | Criar o fluxo do aplicativo, o relógio ativo e o ciclo dos capítulos | R1 | P0 | 1.25 | CORE-002 |
| [CORE-006](../Tasks/CORE-006.md) | Automatizar montagem de cenas e builds sem sobrescrever colegas | R1 | P0 | 0.75 | CORE-001, CORE-002 |
| [ART-001](../Tasks/ART-001.md) | Auditar modelos e referências e definir o pipeline de assets | R4 | P0 | 0.75 | — |
| [UI-001](../Tasks/UI-001.md) | Definir o sistema visual do dossiê e do HUD em layouts estáticos | R2 | P0 | 1.25 | — |
| [UI-002](../Tasks/UI-002.md) | Implementar seleção por dossiês e carregamento explícito | R2 | P0 | 1.5 | CORE-005, UI-001 |
| [UI-003](../Tasks/UI-003.md) | Implementar HUD, orientação e prompt E compartilhado | R2 | P0 | 1.25 | CORE-003, UI-001 |
| [UI-004](../Tasks/UI-004.md) | Implementar PT/EN e legendas sem caixas vazias | R2 | P0 | 1.25 | CORE-004, UI-001 |
| [UI-005](../Tasks/UI-005.md) | Criar tutorial por ações e assistência para quem não joga | R2 | P0 | 1.25 | CORE-003, UI-003 |
| [UI-006](../Tasks/UI-006.md) | Centralizar pausa e configurar preferências e foco no navegador | R2 | P0 | 1.25 | CORE-005, UI-003 |
| [UI-007](../Tasks/UI-007.md) | Apresentar evidências, limitar fotografias e permitir a escolha final | R2 | P0 | 1.0 | CORE-005, UI-003 |
| [AUD-001](../Tasks/AUD-001.md) | Selecionar música e efeitos licenciados por intenção narrativa | R7 | P0 | 0.75 | — |
| [AUD-002](../Tasks/AUD-002.md) | Implementar canais, crossfades e sonificação das ações | R7 | P0 | 1.25 | CORE-004, AUD-001 |
| [QA-001](../Tasks/QA-001.md) | Gerar o primeiro build Web e provar o perfil Leve antes do polimento | R7 | P0 | 1.25 | CORE-001 |
| [QA-002](../Tasks/QA-002.md) | Automatizar progressão, tecla mantida, pausa, cancelamento e descarte | R7 | P0 | 1.5 | CORE-003, CORE-004, CORE-005 |
| [UI-008](../Tasks/UI-008.md) | Refinar ornamentos, transições do arquivo e fotografia editorial | R2 | P1 | 1.5 | UI-002, UI-004 |
| [MAR-001](../Tasks/MAR-001.md) | Definir dados e fluxo exclusivo dos 32 acontecimentos de Marte | R3 | P0 | 1 | CORE-002, CORE-004, CORE-005 |
| [MAR-002](../Tasks/MAR-002.md) | Criar controle da Opportunity e tutorial por ações reais | R3 | P0 | 1 | CORE-002, CORE-003, MAR-001 |
| [MAR-003](../Tasks/MAR-003.md) | Implementar observação guiada das blueberries sem caça à mira | R3 | P0 | 1 | CORE-003, MAR-001 |
| [MAR-004](../Tasks/MAR-004.md) | Construir RAT acessível com cavidade rasa e feedback contínuo | R3 | P0 | 1.75 | CORE-003, MAR-001 |
| [MAR-005](../Tasks/MAR-005.md) | Encadear APXS e Mössbauer na mesma cavidade | R3 | P0 | 1 | CORE-003, MAR-004 |
| [MAR-006](../Tasks/MAR-006.md) | Fazer escudo e meteorito como descobertas distintas | R3 | P0 | 1.5 | CORE-003, MAR-001 |
| [MAR-007](../Tasks/MAR-007.md) | Criar conservação interativa e recuperação na tempestade de2007 | R3 | P0 | 1 | CORE-003, MAR-001 |
| [MAR-008](../Tasks/MAR-008.md) | Montar Sol, vale, antena e escolha da última imagem | R3 | P0 | 1.75 | CORE-003, CORE-005, MAR-001 |
| [MAR-009](../Tasks/MAR-009.md) | Integrar o capítulo sem alterar o controlador global | R3 | P0 | 1 | MAR-001, MAR-002, MAR-003, MAR-005, MAR-006, MAR-007, MAR-008, MAR-018 |
| [MAR-010](../Tasks/MAR-010.md) | Validar primeira experiência deMarte e regressões visuais | R3 | P0 | 0.75 | MAR-009, QA-001 |
| [MAR-011](../Tasks/MAR-011.md) | Auditar modeloNASA e preparar rig/material compartilhado dos gêmeos | R4 | P0 | 0.75 | ART-001, CORE-001 |
| [MAR-012](../Tasks/MAR-012.md) | Construir Eagle orgânica com percurso e rocha assentados | R4 | P0 | 1.5 | ART-001, MAR-011 |
| [MAR-013](../Tasks/MAR-013.md) | Autorar laboratório, mapa e lançamentos curtos com propósito | R4 | P0 | 1.5 | CORE-004, MAR-011 |
| [MAR-014](../Tasks/MAR-014.md) | Criar pouso split com continuidade deescudo, módulo eairbags | R4 | P0 | 2 | CORE-004, MAR-011 |
| [MAR-015](../Tasks/MAR-015.md) | Assentar campo do escudo e meteorito usando imagens reais | R4 | P0 | 1 | ART-001 |
| [MAR-016](../Tasks/MAR-016.md) | Criar Victoria e vale com dois estados de poeira eficientes | R4 | P0 | 1.25 | ART-001 |
| [MAR-017](../Tasks/MAR-017.md) | Montar Spirit em45s sem apagarsuaciência ou inventarseufim | R4 | P0 | 1.5 | CORE-004, MAR-011 |
| [MAR-018](../Tasks/MAR-018.md) | Integrar edição cinematográfica e entrega de câmeras deMarte | R4 | P0 | 1 | CORE-004, MAR-013, MAR-014, MAR-017, MAR-019 |
| [MAR-019](../Tasks/MAR-019.md) | Criar memorial das duas máquinas com foto real do jogador | R4 | P0 | 1 | CORE-004, MAR-011 |
| [MAR-020](../Tasks/MAR-020.md) | Conferir apoios, macroinstrumento e integração do rover no Web | R4 | P0 | 0.75 | MAR-012, MAR-015, MAR-016, MAR-018, QA-001 |
| [MAR-021](../Tasks/MAR-021.md) | Polir microtextura e recortes de luz das amostras | R4 | P1 | 1 | MAR-020 |
| [MAR-022](../Tasks/MAR-022.md) | Refinar assinatura sonora e pausas emocionais deMarte | R4 | P1 | 1 | MAR-018, MAR-019 |
| [PIO-001](../Tasks/PIO-001.md) | Congelar dados científicos, estados e legendas PT/EN da Pioneer | R7 | P0 | 0.5 | — |
| [PIO-002](../Tasks/PIO-002.md) | Preparar prefab leve da Pioneer com referência NASA e créditos | R5 | P0 | 0.75 | ART-001 |
| [PIO-003](../Tasks/PIO-003.md) | Montar cena espacial única e apresentação de escala | R5 | P0 | 1 | CORE-001, CORE-002, ART-001 |
| [PIO-004](../Tasks/PIO-004.md) | Dirigir lançamento nativo de 40 segundos e primeiro hold | R5 | P0 | 0.75 | PIO-003, CORE-003, CORE-004 |
| [PIO-005](../Tasks/PIO-005.md) | Implementar fantasia do cinturão e cinco registros guiados | R5 | P0 | 1 | PIO-003, CORE-003, CORE-004 |
| [PIO-006](../Tasks/PIO-006.md) | Construir varredura IPP em oito faixas e dois canais | R5 | P0 | 1.5 | PIO-003, CORE-003, CORE-004 |
| [PIO-007](../Tasks/PIO-007.md) | Selecionar fotos históricas exatas e registrar proveniência | R7 | P0 | 0.5 | — |
| [PIO-008](../Tasks/PIO-008.md) | Integrar recuperação de radiação e revelação fotográfica | R5 | P0 | 0.5 | PIO-006, PIO-007, CORE-003 |
| [PIO-009](../Tasks/PIO-009.md) | Implementar placa de cinco símbolos com teclado e snap | R5 | P0 | 1 | PIO-003, CORE-003, CORE-004 |
| [PIO-010](../Tasks/PIO-010.md) | Montar percurso orbital e síntese de economia de energia | R5 | P0 | 0.5 | PIO-003, CORE-003, CORE-004 |
| [PIO-011](../Tasks/PIO-011.md) | Criar escuta DSN com três contatos e silêncio inevitável | R5 | P0 | 1.25 | PIO-003, CORE-003, CORE-004 |
| [PIO-012](../Tasks/PIO-012.md) | Dirigir despedida e conclusão nativas de 38 segundos | R5 | P0 | 0.75 | PIO-003, CORE-004 |
| [PIO-013](../Tasks/PIO-013.md) | Vincular todas as legendas e cues sonoros aos estados | R5 | P0 | 0.75 | PIO-001, CORE-004 |
| [PIO-014](../Tasks/PIO-014.md) | Integrar a rota completa Pioneer e reset por capítulo | R5 | P0 | 1 | PIO-002, PIO-004, PIO-005, PIO-008, PIO-009, PIO-010, PIO-011, PIO-012, PIO-013, CORE-005 |
| [PIO-015](../Tasks/PIO-015.md) | Homologar primeiro uso e custo Web do capítulo Pioneer | R7 | P0 | 0.75 | PIO-014, QA-001 |
| [PIO-016](../Tasks/PIO-016.md) | Polir antena, metal e transições com luz de recorte | R5 | P1 | 2 | PIO-014 |
| [PIO-017](../Tasks/PIO-017.md) | Expandir arquivo histórico com marcos opcionais verificados | R5 | P2 | 0.5 | PIO-001 |
| [SUR-001](../Tasks/SUR-001.md) | Travar os fatos, referências visuais e inventário da Surveyor 3 | R6 | P0 | 0.75 | — |
| [SUR-002](../Tasks/SUR-002.md) | Montar o cenário único e os apoios da sonda na encosta | R6 | P0 | 1.25 | CORE-001, CORE-002, ART-001, SUR-001 |
| [SUR-003](../Tasks/SUR-003.md) | Criar o lançamento por telemetria e confirmar Centaur | R6 | P0 | 0.75 | CORE-004, CORE-005, SUR-001 |
| [SUR-004](../Tasks/SUR-004.md) | Integrar a fila didática de comandos Terra–Lua | R6 | P0 | 1 | CORE-002, CORE-003, CORE-005 |
| [SUR-005](../Tasks/SUR-005.md) | Implementar a correção de meio de curso guiada | R6 | P0 | 1 | SUR-003, SUR-004, CORE-003 |
| [SUR-006](../Tasks/SUR-006.md) | Encenar os três contatos e o corte dos motores sem falha por habilidade | R6 | P0 | 1.25 | SUR-002, SUR-004, CORE-004 |
| [SUR-007](../Tasks/SUR-007.md) | Revelar a primeira imagem por TV sem tornar o texto ilegível | R6 | P0 | 0.75 | SUR-002, CORE-004 |
| [SUR-008](../Tasks/SUR-008.md) | Guiar três observações com espelho, lentes e alternativa H | R6 | P0 | 0.75 | SUR-004, SUR-007, CORE-003 |
| [SUR-009](../Tasks/SUR-009.md) | Abrir uma trincheira com deformação, pá e recolhimento seguros | R6 | P0 | 1.5 | SUR-002, SUR-004, CORE-003 |
| [SUR-010](../Tasks/SUR-010.md) | Executar um teste de carga e um impacto, com registro histórico separado | R6 | P0 | 0.75 | SUR-009, CORE-004 |
| [SUR-011](../Tasks/SUR-011.md) | Enquadrar o eclipse de 24 de abril com atmosfera terrestre | R6 | P0 | 0.5 | SUR-007, SUR-004, CORE-004 |
| [SUR-012](../Tasks/SUR-012.md) | Encerrar 1967 e avançar 31 meses sem repetição longa | R6 | P0 | 0.5 | SUR-002, CORE-004, CORE-005 |
| [SUR-013](../Tasks/SUR-013.md) | Montar a visita imaginada da Apollo 12 e a fotografia histórica final | R6 | P0 | 0.75 | SUR-002, SUR-012, CORE-004, CORE-005 |
| [SUR-014](../Tasks/SUR-014.md) | Importar os 31 cues e textos PT/EN do capítulo | R6 | P0 | 0.5 | SUR-001, CORE-004 |
| [SUR-015](../Tasks/SUR-015.md) | Validar Surveyor completa no Web e publicar evidências de aceitação | R6 | P0 | 0.75 | QA-001, SUR-003, SUR-005, SUR-006, SUR-008, SUR-010, SUR-011, SUR-013, SUR-014 |
| [SUR-016](../Tasks/SUR-016.md) | Refinar materiais, sombras e animação curta da visita | R6 | P1 | 1.5 | SUR-015 |
| [SUR-017](../Tasks/SUR-017.md) | Adicionar a peça retornada ao arquivo sem confundir câmera de teste | R6 | P1 | 0.5 | SUR-001, SUR-015 |
| [INT-001](../Tasks/INT-001.md) | Integrar os três capítulos ao dossiê e às camadas globais | R1 | P0 | 0.75 | MAR-010, MAR-020, PIO-015, SUR-015, CORE-006, UI-002, UI-004, UI-005, UI-006, UI-007, AUD-002 |
| [SCI-001](../Tasks/SCI-001.md) | Revisar o texto final e a proveniência das imagens dentro do jogo | R7 | P0 | 0.75 | UI-004, MAR-001, PIO-001, SUR-001 |
| [ART-002](../Tasks/ART-002.md) | Revisar continuidade, apoio no solo e feedback dos três capítulos | R3 | P0 | 0.75 | INT-001 |
| [QA-003](../Tasks/QA-003.md) | Executar jornadas completas e registrar tempo ativo e falhas | R7 | P0 | 1.5 | INT-001, QA-002 |
| [QA-004](../Tasks/QA-004.md) | Conduzir primeiras partidas com novatos e revisar orientação | R2 | P0 | 2.0 | INT-001 |
| [QA-005](../Tasks/QA-005.md) | Homologar navegador, GPU integrada, memória, foco e carregamento | R7 | P0 | 1.5 | INT-001, QA-001 |
| [REL-001](../Tasks/REL-001.md) | Gerar ZIPs finais, créditos e manifesto de distribuição | R1 | P0 | 0.75 | QA-003, QA-005, SCI-001, ART-002 |
| [REL-002](../Tasks/REL-002.md) | Publicar candidato no itch.io e validar o endereço real | R1 | P0 | 1.0 | REL-001, QA-004 |
| [AUD-003](../Tasks/AUD-003.md) | Refinar a mixagem final depois das jornadas | R7 | P1 | 1.0 | AUD-002, INT-001 |
