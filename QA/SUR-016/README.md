# SUR-016 — acabamento da visita e integração do solo

Base: SUR-015 integrada em `38a7c1c`; mantém o capítulo completo e o mesmo tempo. Rotina própria `SurveyorArtPolish.Apply`, chamada pelo builder do capítulo. Nenhuma cena nova, pacote, shader, câmera ou ParticleSystem.

- Regolito: textura procedural 1024 com variação macro/intermediária/fina, conservando as três marcas de apoio e o mesmo Lit/normal map. Mesmo material no terreno e no remendo deformável evita mudança de cor nas bordas.
- Luva: palma elíptica contínua, dedos articulados com pontas arredondadas, punho e dobras; material de tecido com baixa reflexão. O gesto entra antes no enquadramento e conserva o limite final seguro.
- Sombras: viés do sol reduzido apenas no prefab do capítulo para melhorar contato visual sem alterar o perfil global.
- A composição esconde as pausas e ferramentas redundantes das prévias; o menu comum continua sendo a única pausa. A legenda não disputa leitura com o menu aberto.

`before/after-leve/equilibrado.png` usam a mesma óptica, t=28,5 s da visita e duas qualidades. Auditoria amostra todos os vértices da luva durante o alcance e verifica que os grãos já se assentaram. Quantidades e teste ficam nos arquivos deste diretório. São capturas GPU no Editor, não novos testes de dispositivos integrados. A jornada Web de SUR-015 permanece documentada; alterações de arte recebem smoke no pacote final após SUR-017.

A foto NASA original permanece como prova documental. A luva, o solo e a visita continuam reconstituições artísticas leves; não são CAD/fotogrametria.

Validação concluída: duas regressões de capítulo aprovadas; build Web release com zero erros; smoke Chrome com ordens reais até FirstImage, pausa e imagem recebida, sem erros. Arquivos principais: 18.463.533 bytes (cerca de +0,22 MB sobre a revisão anterior). Luva de 412 para 1.232 triângulos; menor distância axial amostrada de 0,222 m, acima do near clip de 0,025 m. A comparação fixa também a iluminação ambiente, para não atribuir cache de iluminação à mudança de materiais.
