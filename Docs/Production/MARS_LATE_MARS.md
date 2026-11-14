# MAR-016 — Victoria (2007) e Perseverance Valley (2018)

Palcos procedurais gerados por `LateMarsBuilder` (módulo `mars.lateMars`): `Prefabs/VictoriaStage.prefab`,
`Prefabs/PerseveranceValleyStage.prefab` e `Art/LateMars/MarsStormProfile.asset`. Nenhum asset externo foi importado.

## Fontes dos fatos geométricos (somente leitura; nenhuma imagem copiada para o repositório)

| Fato usado | Fonte |
|---|---|
| Victoria ≈ 800 m de diâmetro, borda recortada, promontórios de penhasco alternados com baías | NASA/JPL-Caltech/Cornell, PIA09104 (sóis 970–991) |
| Perseverance Valley numa reentrância ampla da borda oeste de Endeavour, declive ~15–17°, "dois campos de futebol" de comprimento | JPL, 15/5/2017; Navcam PIA21495, NASA/JPL-Caltech |
| Poeira de 2007 e 2018: o vento não danifica o equipamento; em 2007 os rovers entraram em modo de sobrevivência | páginas de missão da NASA/JPL |

Créditos de imagem seguem a política do JPL ("Courtesy NASA/JPL-Caltech"). Os palcos são interpretação estilizada, não
reconstrução topográfica: alturas, canais e textura são inventados dentro dessas medidas.

## Limites do perfil (validados em `MarsStormProfileData.Validate` e nos testes)

- Névoa a 6 m (rover) ≤ 50%; a 40 m (disco solar) ≤ 70%; luz mínima 20% da limpa; luminância da poeira ≤ 0,75.
- Poeira muda no máximo 0,35/s; tremor ≤ 2 cm, só como pulso, removido por movimento reduzido.
- Partículas ≤ 300 (normal) / 120 (baixo), alfa ≤ 0,3.
- 2007 termina com poeira ≤ 0,2 (recupera); 2018 termina ≥ 0,8 (sem sinal).

## Limitações

Não houve validação em WebGL nem em GPU de baixo desempenho. As capturas foram feitas em lote no Unity 6000.6.8f1 (o
projeto fixa 6.7f1). O julgamento visual da borda de Victoria e do vale fica para a revisão.
