# SUR-013 — validação focada

Unity 6000.6.7f1 / URP 17.6, Windows, Direct3D 11, NVIDIA GeForce RTX 3050 6GB Laptop GPU. Base develop `77ff4f6`, CORE-006 e UI-006 presentes. Revisão própria autorizada em AGENTS.md, sem aprovação humana simulada.

## Executar

Abrir `Assets/_Game/Chapters/Surveyor/Sequences/Visit/Preview/SurveyorVisitPreview.unity`, Play → Iniciar epílogo. Builder: `Abandoned.SpaceApps.EditorTools.SurveyorVisitBuilder.BuildFromCommandLine`. Testes: `-runTests -testPlatform EditMode -testFilter Abandoned.SpaceApps.Chapters.Surveyor.VisitTests -testResults QA/SUR-013/tests.xml`.

## Resultados

**9 testes aprovados**, oito de fluxo/input e um de integração em Play Mode com render real. O resultado completo está em `tests.xml`.

- Aviso antes da imagem; progressão aos 32/44/50 s, sem pular a leitura final.
- S29 32–38, S30 38–43 e S31 44–50, sem sobreposição.
- E mantido durante a cena não retorna ao arquivo; exige soltar e pressionar novamente. Botão separado só funciona depois dos 50 s e sem E mantido.
- Pausa na fotografia congela o tempo; retomar, reiniciar durante a luva, cancelar e voltar ao arquivo não deixam posse da câmera ou sequência em execução.
- Centro óptico imóvel, arquivo marcado como Archive e não PlayerCapture.
- Cena real com datas distintas, legendas PT/EN, foto NASA limitada a 1024 px, retorno, duas reproduções e descarte do RenderTexture local.
- 31 amostras da aproximação da luva entre 26 e 30,5 s verificam os vértices à frente do near plane. O corte para preto ocorre aos 31 s, sem representar uma retirada física atravessando a câmera.

Capturas 1280×720 desta pasta, inspecionadas visualmente: Intrepid descendo (8 s) e pousado parcialmente oculto pela borda (12 s), visitante (24 s), luva (29,5 s), fotografia real em EN (34 s), memorial em PT (45 s). A revisão encontrou o topo aberto das pernas em quadro; a malha foi prolongada para fora do enquadramento e os testes/capturas foram repetidos. Nenhum vídeo pré-renderizado, vento lunar ou passos aéreos foi adicionado.

## Limites

Não houve novo build Web nem medição de FPS, download ou memória do navegador. O teste renderiza quadros do Editor e não certifica a campanha, desempenho em GPU integrada ou compreensão de novatos. Integração de sequência/música e revisão artística de conjunto permanecem em SUR-015/INT-001. As malhas do LM e dos trajes são silhuetas procedurais simples; refinamento de traje/material é polimento, não um modelo oficial da missão.

Créditos, cronologia e contrato para o integrador em `Docs/Production/SURVEYOR_VISIT.md` e `ASSET_CREDITS.md`. As alterações locais anteriores de Marte e ProjectSettings foram preservadas e não pertencem ao PR.
