"""Generate reviewable task prompts and a dependency graph from authored data."""
from pathlib import Path
from collections import Counter
import json

ROOT=Path(__file__).resolve().parents[1]
URL='https://github.com/joaovdamaceno/AbandonedButNotForgotten-SpaceApps'
ROLES={
 'R1':'Integração, núcleo e distribuição', 'R2':'UX, localização e orientação',
 'R3':'Gameplay e integração de Marte', 'R4':'Arte e cinema de Marte',
 'R5':'Pioneer 10', 'R6':'Surveyor 3', 'R7':'Ciência, áudio, QA e desempenho'}

def final_tasks(tasks):
    def t(id,title,role,hours,deps,files,delivery,instructions,acceptance,tests,priority='P0'):
        return dict(id=id,title=title,role=role,hours=hours,depends_on=deps,files=files,
            owned_paths=files,priority=priority,context='Integração do novo jogo Web para sete pessoas: três capítulos independentes de 5–7 minutos, PT/EN, público leigo. O resultado precisa funcionar no navegador; relatório anterior não aprova esta edição.',
            deliverable=delivery,instructions=instructions,acceptance=acceptance,tests=tests,
            sources=['https://itch.io/docs/creators/html5'],cutline='Reduzir detalhe visual e esperas redundantes; preservar clareza, fatos, início e encerramento dos três capítulos.')
    p0=[x for x in tasks if x['priority']=='P0']
    leaves=[]
    for prefix in ['MAR-','PIO-','SUR-']:
        local=[x for x in p0 if x['id'].startswith(prefix)]
        leaves += [x['id'] for x in local if not any(x['id'] in y['depends_on'] for y in local)]
    return [
      t('INT-001','Integrar os três capítulos ao dossiê e às camadas globais','R1',.75,
        leaves+['CORE-006','UI-002','UI-004','UI-005','UI-006','UI-007','AUD-002'],
        ['Assets/_Game/Scenes/Boot.unity','Assets/_Game/Scenes/Dossier.unity','Assets/_Game/Core/ChapterCatalog.asset','ProjectSettings/EditorBuildSettings.asset'],
        'Um player com as três pastas, cada uma jogável e com retorno/reinício sem carregar os outros mundos ao mesmo tempo.',
        ['Integrar prefabs/builders aprovados, sem copiar cenas inteiras sobre as alterações dos colegas.',
         'Resolver IDs, eventos e referências com os contratos globais; apenas um dono de input/câmera/áudio em cada estado.',
         'Revisar nomes de cenas, ordem de build e descarte de memória entre capítulos.'],
        ['Qualquer pasta chega ao próprio final sem exigir outra.','Menu/reinício não duplicam UI, sons, evidências ou listeners.','Nenhum serviço externo é necessário durante o capítulo.'],
        ['Percurso de smoke com ordem Pioneer → Marte → Surveyor.','Reiniciar no meio de uma interação e retornar ao arquivo.']),
      t('SCI-001','Revisar o texto final e a proveniência das imagens dentro do jogo','R7',.75,
        ['UI-004','MAR-001','PIO-001','SUR-001'],['QA/Science/FINAL_AUDIT.md'],
        'Checklist de cada afirmação, legenda e imagem publicada contra as fontes e correções aprovadas.',
        ['Conferir a build/dados importados, não apenas o documento de roteiro.',
         'Verificar datas UTC/local, RAT/APXS/Mössbauer, recorde histórico, RTGs, canais do IPP e último sinal.',
         'Conferir visita imaginada da Surveyor, ausência de água inferida pela pá e créditos de fotos reais.'],
        ['Toda afirmação tem fonte ou qualificação de hipótese/reconstrução.','Nenhum valor de gameplay se apresenta como medição histórica literal.','Termos em PT e EN mantêm o mesmo significado.'],
        ['Roteiro contra captura de tela dos resultados finais.','Revisão cruzada por outra função antes de fechar.']),
      t('ART-002','Revisar continuidade, apoio no solo e feedback dos três capítulos','R3',.75,
        ['INT-001'],['QA/Art/CONTINUITY.md'],
        'Lista fechada de defeitos visuais impeditivos e evidência das correções no player atual.',
        ['Revisar cada corte do pouso e transição, incluindo os dois lados do split e câmera acima do chão.',
         'Olhar em ângulo baixo rochas, escudo, rodas, rastro e ferramenta recuando; encaminhar correções ao dono dos prefabs.',
         'Conferir tempestade ocultando rover, imagens/revelações e texto sem placa vazia; nenhum take duplicado sem função.'],
        ['Sem ator/fragmento essencial flutuante ou câmera dentro do terreno.','Toda ação de E tem feedback perceptível e alvo correto.','Defeitos remanescentes têm severidade e responsável, sem aprovação falsa.'],
        ['Capturas em tempo real nas câmeras finais.','Movimento reduzido e perfil leve não apagam informações.']),
      t('QA-003','Executar jornadas completas e registrar tempo ativo e falhas','R7',1.5,
        ['INT-001','QA-002'],['QA/Journeys/','Tools/run_journeys.py'],
        'Três jornadas de ponta a ponta, com logs de progresso, duração e erros do build identificado.',
        ['Dirigir/operar pelos inputs reais ou operador assistido documentado; não usar completar-etapa por relógio para alegar sucesso.',
         'Abortar em bloqueio fatal e registrar estado, alvo e dependências; corrigir com o dono e repetir apenas trecho afetado mais fluxo necessário.',
         'Separar resultados de operador automático de compreensão e duração humana.'],
        ['Três finais alcançados sem erros de runtime conhecidos.','Duração técnica de cada capítulo registrada, com pausas excluídas.','A assistência e os atalhos usados pelo teste estão declarados.'],
        ['E mantido entre etapas, pausa/cancelamento e retomada.','Fotografia escolhida, placa completa e saída da TV aparecem nos finais corretos.']),
      t('QA-004','Conduzir primeiras partidas com novatos e revisar orientação','R2',2.0,
        ['INT-001'],['QA/Novices/SESSIONS.csv','QA/Novices/RESULTS.md'],
        'Cinco primeiras sessões por capítulo, sem orientação oral durante a partida, com tempo e compreensão.',
        ['Recrutar pessoas não familiarizadas com o jogo; membros que o implementaram não substituem novatos.',
         'Registrar início/fim ativos, pausas, solicitações de ajuda e pontos de confusão; pedir que expliquem a descoberta com suas palavras.',
         'Buscar quatro de cinco entre 300–420 segundos por capítulo; ajustar esperas/orientação, sem cronômetro que resolva instrumentos sozinho.'],
        ['Resultados reais e anônimos, sem dados pessoais desnecessários.','Cada bloqueio de entendimento tem correção e nova verificação.','Amostra ausente continua pendente; não é substituída por automação.'],
        ['Checklist de compreensão de cada capítulo em Docs/04_WEB_AND_QA.md.','Revisar PT e EN e ao menos uma sessão sem mouse preciso.']),
      t('QA-005','Homologar navegador, GPU integrada, memória, foco e carregamento','R7',1.5,
        ['INT-001','QA-001'],['QA/Web/COMPATIBILITY.md','QA/Web/METRICS.json'],
        'Medições em dispositivo de referência real e testes locais/iframe com limites explícitos.',
        ['Testar Chrome/Edge e Firefox, cache frio, rede limitada a10Mbps e resolução interna até720p.',
         'Em GPU integrada, medir frametimes, bytes, pico de memória e duas reinicializações completas, mais troca dos três capítulos.',
         'Testar áudio por gesto, fullscreen, Esc/P, foco real, retorno, teclas retidas e transparência/depth da tempestade.'],
        ['Dispositivo/navegador/build/hash registrados.','Sem crescimento contínuo de fotos/RTs e sem input retido após retorno.','Falha ou ausência de máquina/teste é explícita, sem garantia genérica de PC fraco.'],
        ['Comparar Leve/Equilibrado com os mesmos pontos de captura.','Conferir capacidade WASM separada de heap em uso e memória da aba/GPU.']),
      t('REL-001','Gerar ZIPs finais, créditos e manifesto de distribuição','R1',.75,
        ['QA-003','QA-005','SCI-001','ART-002'],['Tools/package_release.py','QA/Release/MANIFEST.json','README.md','ASSET_CREDITS.md'],
        'ZIP Web com index.html na raiz e Windows de apoio, derivados do build validado.',
        ['Congelar o candidato de release, conferir licença de todos os assets e excluir diagnósticos/símbolos/fontes sem uso do player.',
         'Validar caminhos relativos, maiúsculas/minúsculas, headers Brotli e limites documentados do itch.io.',
         'Registrar tamanho e SHA-256 de cada arquivo/pacote; qualquer mudança de runtime após QA exige nova validação pertinente.'],
        ['ZIPs íntegros e manifestos identificam o build testado.','Créditos e fontes presentes no jogo/pacote.','Não há vídeos, modelo de laboratório excessivo ou serviços remotos acidentais.'],
        ['Descomprimir e comparar hashes.','Abrir Windows e servir Web a partir do pacote extraído.']),
      t('REL-002','Publicar candidato no itch.io e validar o endereço real','R1',1.0,
        ['REL-001','QA-004'],['Docs/Production/ITCH_PAGE.md','QA/Release/HOSTED_CHECK.md'],
        'Página curta, player Web e execução comprovada no iframe hospedado; link de apresentação.',
        ['Usar a conta autorizada da equipe; não criar conta/aceitar termos ou expor página sem o responsável quando necessário.',
         'Configurar HTMLGame desktop, resolução responsiva e fullscreen; texto de três linhas e controles, sem outra leitura longa antes do jogo.',
         'Testar URL frio, áudio após clique, capítulos, foco/fullscreen e headers; registrar problemas do host e só marcar entregue após verificação.'],
        ['Avaliador consegue escolher e iniciar um capítulo sem baixar executável.','Privacidade/publicação segue decisão da equipe.','URL hospedado, versão e limitações reais no relatório.'],
        ['Chrome e Firefox no iframe do itch.io.','Recarregar sem cache e concluir ao menos uma jornada hospedada.']),
      t('AUD-003','Refinar a mixagem final depois das jornadas','R7',1.0,['AUD-002','INT-001'],
        ['Assets/_Game/Shared/Audio/','QA/Audio/FINAL_MIX.md'],
        'Polimento de música/efeitos sem encobrir objetivos ou cansar em loops.',
        ['Ouvir cada capítulo inteiro no player e em volume baixo.','Reduzir vento contínuo e pulsação mecânica artificial; separar rádio e música.','Conferir crossfades e silêncios depois de reinício.'],
        ['Sem loop irritante ou clipping conhecido.','Sons essenciais têm alternativa visual.','Licenças não mudaram com a substituição.'],
        ['A/B com mix anterior e anotações humanas.','Volume zero e fones/alto-falantes.'],priority='P1')
    ]


def prompt(task, mapping=None):
    m=mapping or {}
    def link(id):
        item=m.get(id) or {}
        return f"[#{item['number']} — {id}]({item['url']})" if item.get('number') else id
    lines=[f"# {task['id']} — {task['title']}", '',
      f"**Função:** {task['role']} — {ROLES[task['role']]} · **Prioridade:** {task['priority']} · **Estimativa:** {task['hours']} h-pessoa (revisável).",'',
      '## Prompt para execução','',task['context'],'',
      f"Leia primeiro o [contexto mestre]({URL}/blob/main/Docs/01_MASTER_CONTEXT.md) e o [AGENTS.md]({URL}/blob/main/AGENTS.md). O novo jogo ainda precisa ser implementado; não presumir sistemas da edição anterior.", '',
      '## Dependências e disponibilidade','',
      ', '.join(link(id) for id in task['depends_on']) if task['depends_on'] else 'Sem bloqueadores de implementação. Pode começar quando houver responsável.',
      'Dependência só libera após integração e fechamento com evidência. Cartão movido manualmente não substitui PR integrado. Um responsável, uma tarefa em andamento por vez.', '',
      '## Entrega concreta','',task['deliverable'],'','## Arquivos sob sua responsabilidade','']
    lines += ['- `'+p+'`' for p in task['owned_paths']]
    lines += ['','Não editar fora desse escopo sem alinhar com o dono. Se uma pasta contiver arquivo de outro responsável, aplicar o contrato e entregar por prefab/componente próprio.','','## Passos de implementação','']
    lines += [f'{i+1}. {s}' for i,s in enumerate(task['instructions'])]
    lines += ['','## Critérios de aceite','']+['- [ ] '+s for s in task['acceptance']]
    lines += ['','## Como verificar','']+['- '+s for s in task['tests']]
    lines += ['','## Fontes e contexto específico','',f'- [Arquitetura]({URL}/blob/main/Docs/03_ARCHITECTURE.md)',f'- [Web e QA]({URL}/blob/main/Docs/04_WEB_AND_QA.md)']
    chapter={'MAR':'01_MARS_TWINS.md','PIO':'02_PIONEER_10.md','SUR':'03_SURVEYOR_3.md'}.get(task['id'][:3])
    if chapter:lines += [f'- [Roteiro completo e legendas]({URL}/blob/main/Docs/Chapters/{chapter})']
    lines += ['- '+s for s in task.get('sources',[])]
    if task.get('cutline'):lines+=['','## Limite e alternativa de escopo','',task['cutline']]
    lines+=['','## Ao concluir','',
      'Abra PR para `develop`, cite esta issue e anexe evidências. Informe comportamento, arquivos, testes realmente executados e limitações. Peça revisão de outra função. A automação considera concluída a dependência somente após integração; não feche tarefa apenas por gerar código.','']
    return '\n'.join(lines)


def main():
    tasks=[]
    for name in ['global','mars','pioneer','surveyor']:
        tasks += json.loads((ROOT/f'Data/Backlog/{name}.json').read_text(encoding='utf-8'))
    integration=final_tasks(tasks)
    (ROOT/'Data/Backlog/integration.json').write_text(json.dumps(integration,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    tasks+=integration
    (ROOT/'Data/backlog.json').write_text(json.dumps(tasks,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    mapping={}
    if (ROOT/'Data/github-map.json').exists():mapping=json.loads((ROOT/'Data/github-map.json').read_text(encoding='utf-8')).get('issues',{})
    out=ROOT/'Docs/Tasks';out.mkdir(parents=True,exist_ok=True)
    for task in tasks:(out/(task['id']+'.md')).write_text(prompt(task,mapping),encoding='utf-8')
    lines=['# Backlog completo','',f'{len(tasks)} tarefas. P0 é a história completa; P1/P2 só após as três experiências funcionais. Estimativas são hipóteses, não promessa de execução.','',
           '| ID | Tarefa | Função | Prioridade | Horas | Depende de |','| --- | --- | --- | --- | ---: | --- |']
    for t in tasks:
        lines.append(f"| [{t['id']}](../Tasks/{t['id']}.md) | {t['title']} | {t['role']} | {t['priority']} | {t['hours']} | {', '.join(t['depends_on']) or '—'} |")
    (ROOT/'Docs/Production/BACKLOG.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
    p0=[t for t in tasks if t['priority']=='P0'];stats={'tasks':len(tasks),'p0':len(p0),
        'p0Hours':sum(t['hours'] for t in p0),'roleP0Hours':{r:sum(t['hours'] for t in p0 if t['role']==r) for r in ROLES},
        'readyAtStart':[t['id'] for t in p0 if not t['depends_on']]}
    (ROOT/'Data/plan-summary.json').write_text(json.dumps(stats,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(stats,ensure_ascii=False,indent=2))


if __name__=='__main__':main()
