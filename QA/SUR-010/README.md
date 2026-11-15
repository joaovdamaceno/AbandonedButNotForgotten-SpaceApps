# SUR-010 — validação

Unity 6000.6.7f1 / URP17.6, Windows, D3D11, RTX 3050 Laptop 6GB. Base `8810332` (histórico remoto atualizado em 15/11). Cena `Assets/_Game/Chapters/Surveyor/Soil/Preview/SurveyorSoilPreview.unity`, Play → Iniciar.

**6 testes aprovados** em `tests.xml`: cinco de fluxo com teclado simulado e Core reais, um de Play Mode com renderização real. Cobrem E mantido entre escavar/pressionar/soltar, pausa e avanço parcial conservado, reinício durante o impacto, cancelamento sem resultado tardio, restrição de pose após recolhimento/folga/alcance, fotografias diferentes da mesma área, centro óptico imóvel e descarte das duas texturas.

Quatro capturas 1280×720 inspecionadas: pressão, impacto, comparação e arquivo em inglês. A câmera da primeira revisão mostrava o elo proximal sobre o sulco. Corrigido por uma convenção de visão técnica (elo proximal apenas lança sombra; elo distal/balde visíveis), preservando a mesma posição óptica e restaurando o renderer na saída. Marca e trincheira estão na mesma área, com fotos novas de cada partida. A foto NASA mostra o instrumento real em 1969, identificada separadamente; não é apresentada como foto dessa escavação.

Builder: `Abandoned.SpaceApps.EditorTools.SurveyorSoilBuilder.BuildFromCommandLine`. Suíte: `-runTests -testPlatform EditMode -testFilter Abandoned.SpaceApps.Chapters.Surveyor.SoilTests -testResults QA/SUR-010/tests.xml`.

Revisão própria autorizada pelo proprietário. Não foi feita revisão humana da R7 nem teste com novatos. Não foi criado novo Web intermediário: aceitação de campanha e custo no navegador serão testados na integração. Não se certifica FPS, geotecnia ou qualidade artística por esses testes. Forma do braço e marcas são reconstruções; a curva é qualitativa, sem kPa. Contrato e fontes em `Docs/Production/SURVEYOR_SOIL.md`; crédito em ASSET_CREDITS.md.
