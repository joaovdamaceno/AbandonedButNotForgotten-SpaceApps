"""Configure the authorized private Projects V2 board with official GraphQL API."""
import argparse
import json
import os
import time
from collections import Counter
from github_setup import Client, ROOT, REPO, STATE, TITLE, EXPECTED_PRIVATE

STATUSES=[('backlog','Backlog','GRAY'),('blocked','Bloqueada','RED'),('ready','Pronta','GREEN'),
          ('doing','Em andamento','BLUE'),('review','Revisão','PURPLE'),('done','Concluída','GREEN')]
FIELD_FRAGMENT='... on ProjectV2Field { id name dataType } ... on ProjectV2SingleSelectField { id name options { id name } }'

def save(state):
    STATE.write_text(json.dumps(state,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')

def field_list(c,pid):
    return c.graphql('query($p:ID!) { node(id:$p) { ... on ProjectV2 { fields(first:50) { nodes { '+FIELD_FRAGMENT+' } } } } }',{'p':pid})['node']['fields']['nodes']

def project_client(client):
    return Client(token=os.environ['PROJECTS_TOKEN']) if os.environ.get('PROJECTS_TOKEN') else client

def setup(c,state):
    c=project_client(c)
    repo=c.request('repos/'+REPO)
    if repo['private'] is not EXPECTED_PRIVATE:raise RuntimeError('Repository visibility differs from the authorized setting.')
    viewer=c.graphql('query { viewer { id login projectsV2(first:100) { nodes { id title number url public } } } }')['viewer']
    if viewer['login']!=REPO.split('/')[0]:raise RuntimeError('Project owner identity differs.')
    project=state.get('project')
    if not project:
        project=next((p for p in viewer['projectsV2']['nodes'] if p['title']==TITLE),None)
    if not project:
        project=c.graphql('mutation($i:CreateProjectV2Input!) { createProjectV2(input:$i) { projectV2 { id title number url public } } }',
            {'i':{'ownerId':viewer['id'],'title':TITLE,'repositoryId':state['repositoryId']}})['createProjectV2']['projectV2']
    state['project']=project;save(state);pid=project['id']
    c.graphql('mutation($i:UpdateProjectV2Input!) { updateProjectV2(input:$i) { projectV2 { id public } } }',
        {'i':{'projectId':pid,'public':False,'shortDescription':'84 tarefas para sete pessoas: Gêmeas, Pioneer 10 e Surveyor 3. P0 primeiro; dependências integradas antes de iniciar.',
              'readme':'Leia o contexto mestre e escolha uma tarefa Pronta. Uma tarefa por pessoa. Dependência exige issue fechada + integrated após merge. Repositório público; quadro de produção privado.\n\nhttps://github.com/'+REPO+'/blob/main/Docs/Production/KANBAN.md'}})
    fields=field_list(c,pid);byname={f['name']:f for f in fields}
    status=byname['Status']
    if {x['name'] for x in status['options']}!={s[1] for s in STATUSES}:
        status=c.graphql('mutation($i:UpdateProjectV2FieldInput!) { updateProjectV2Field(input:$i) { projectV2Field { ... on ProjectV2SingleSelectField { id name options { id name } } } } }',
           {'i':{'fieldId':status['id'],'singleSelectOptions':[{'name':name,'color':color,'description':key} for key,name,color in STATUSES]}})['updateProjectV2Field']['projectV2Field']
    specs=[('Função','SINGLE_SELECT',[f'R{i}' for i in range(1,8)]),
           ('Prioridade','SINGLE_SELECT',['P0','P1','P2']),('Horas','NUMBER',None),
           ('Capítulo','SINGLE_SELECT',['Compartilhado','Gêmeas','Pioneer 10','Surveyor 3'])]
    for name,kind,options in specs:
        if name not in byname:
            data={'projectId':pid,'name':name,'dataType':kind}
            if options:data['singleSelectOptions']=[{'name':x,'color':'GRAY','description':x} for x in options]
            field=c.graphql('mutation($i:CreateProjectV2FieldInput!) { createProjectV2Field(input:$i) { projectV2Field { '+FIELD_FRAGMENT+' } } }',{'i':data})['createProjectV2Field']['projectV2Field']
            byname[name]=field
    project.update(statusFieldId=status['id'],statusOptions={key:next(x['id'] for x in status['options'] if x['name']==name) for key,name,_ in STATUSES},fields={})
    for name,_,_ in specs:
        f=byname[name];project['fields'][name]={'id':f['id'],'options':{o['name']:o['id'] for o in f.get('options',[])}}
    project['public']=False;save(state)
    visible=[byname['Title']['id'],status['id']]+[byname[name]['id'] for name,_,_ in specs]
    views=c.graphql('query($p:ID!) { node(id:$p) { ... on ProjectV2 { views(first:30) { nodes { id name layout number filter } } } } }',{'p':pid})['node']['views']['nodes']
    view_specs=[('Kanban geral','BOARD_LAYOUT',''),('Prontas · P0','TABLE_LAYOUT','label:"status:ready" label:"priority:P0"'),
                ('Por função','TABLE_LAYOUT',''),('Opcional','TABLE_LAYOUT','-label:"priority:P0"')]
    view_specs += [(f'R{i}','BOARD_LAYOUT',f'label:"role:R{i}"') for i in range(1,8)]
    for name,layout,filter in view_specs:
        found=next((v for v in views if v['name']==name),None)
        if not found:
            found=c.graphql('mutation($i:CreateProjectV2ViewInput!) { createProjectV2View(input:$i) { projectV2View { id name layout number filter } } }',
                {'i':{'projectId':pid,'name':name,'layout':layout,'configuration':{'visibleFieldIds':visible}}})['createProjectV2View']['projectV2View']
            views.append(found)
        c.graphql('mutation($i:UpdateProjectV2ViewInput!) { updateProjectV2View(input:$i) { projectV2View { id filter } } }',
                  {'i':{'viewId':found['id'],'filter':filter,'layout':layout,'configuration':{'visibleFieldIds':visible}}})
    project['views']={v['name']:{'id':v['id'],'number':v['number'],'layout':v['layout']} for v in views};save(state)
    print('Private Project configured: '+project['url'],flush=True)
    # addProjectV2ItemById returns the existing item for a repeated contentId.
    entries=list(state['issues'].items())
    for start in range(0,len(entries),12):
        chunk=entries[start:start+12];parts=[]
        for i,(_,issue) in enumerate(chunk):
            parts.append(f"a{i}:addProjectV2ItemById(input:{{projectId:{json.dumps(pid)},contentId:{json.dumps(issue['node_id'])}}}) {{ item {{ id }} }}")
        result=c.graphql('mutation { '+' '.join(parts)+' }')
        for i,(id,issue) in enumerate(chunk):issue['projectItemId']=result[f'a{i}']['item']['id']
        save(state);print(f'Project items: {min(start+12,len(entries))}/{len(entries)}',flush=True)
    tasks=json.loads((ROOT/'Data/backlog.json').read_text(encoding='utf-8'))
    sync(c,state,{t['id']:'backlog' if t['priority']!='P0' else 'blocked' if t['depends_on'] else 'ready' for t in tasks},include_metadata=True)

def sync(client,state,statuses,include_metadata=False):
    if not state.get('project'):return {'skipped':'Project not configured.'}
    c=project_client(client);p=state['project'];parts=[]
    # Não regravar todos os cartões a cada comando: isso polui o histórico com
    # movimentações em nome da pessoa cujo token executou a sincronização.
    current={};after=None
    while True:
        page=c.graphql('query($p:ID!,$after:String) { node(id:$p) { ... on ProjectV2 { items(first:100,after:$after) { pageInfo { hasNextPage endCursor } nodes { id fieldValueByName(name:"Status") { ... on ProjectV2ItemFieldSingleSelectValue { optionId } } } } } } }',
                       {'p':p['id'],'after':after})['node']['items']
        current.update({item['id']:(item.get('fieldValueByName') or {}).get('optionId') for item in page['nodes']})
        if not page['pageInfo']['hasNextPage']:break
        after=page['pageInfo']['endCursor']
    updated=0
    tasks={t['id']:t for t in json.loads((ROOT/'Data/backlog.json').read_text(encoding='utf-8'))}
    def add(item,field,value):
        parts.append('a'+str(len(parts))+':updateProjectV2ItemFieldValue(input:{projectId:'+json.dumps(p['id'])+',itemId:'+json.dumps(item)+',fieldId:'+json.dumps(field)+',value:{'+value+'}}) { projectV2Item { id } }')
    for id,status in statuses.items():
        item=state['issues'].get(id,{}).get('projectItemId')
        if not item:continue
        if current.get(item)!=p['statusOptions'][status]:
            add(item,p['statusFieldId'],'singleSelectOptionId:'+json.dumps(p['statusOptions'][status]))
            updated+=1
        if include_metadata:
            t=tasks[id];chapter={'MAR':'Gêmeas','PIO':'Pioneer 10','SUR':'Surveyor 3'}.get(id[:3],'Compartilhado')
            for name,value in [('Função',t['role']),('Prioridade',t['priority']),('Capítulo',chapter)]:
                f=p['fields'][name];add(item,f['id'],'singleSelectOptionId:'+json.dumps(f['options'][value]))
            add(item,p['fields']['Horas']['id'],'number:'+str(t['hours']))
    for start in range(0,len(parts),10):
        query='mutation { '+' '.join(parts[start:start+10])+' }'
        for attempt in range(3):
            try:
                c.graphql(query);break
            except RuntimeError as e:
                if attempt==2 or not any(code in str(e) for code in ['HTTP 502','HTTP 503','HTTP 504','HTTP 429']):raise
                time.sleep(2+attempt*2)
        if include_metadata and start%100==0:print(f'Project field updates: {min(start+10,len(parts))}/{len(parts)}',flush=True)
    return {'updated':updated,'unchanged':len(statuses)-updated,'project':p['url']}

def verify(c,state):
    c=project_client(c);pid=state['project']['id']
    query='''query($p:ID!,$after:String) { node(id:$p) { ... on ProjectV2 { id title public url repositories(first:10) { nodes { nameWithOwner } } items(first:100,after:$after) { pageInfo { hasNextPage endCursor } nodes { id content { ... on Issue { number state title } } fieldValues(first:15) { nodes { ... on ProjectV2ItemFieldSingleSelectValue { name field { ... on ProjectV2SingleSelectField { name } } } ... on ProjectV2ItemFieldNumberValue { number field { ... on ProjectV2Field { name } } } } } } } views(first:30) { nodes { name layout filter verticalGroupByFields(first:10) { nodes { ... on ProjectV2SingleSelectField { name } } } } } } } }'''
    items=[];after=None
    while True:
        p=c.graphql(query,{'p':pid,'after':after})['node'];page=p['items'];items+=page['nodes']
        if not page['pageInfo']['hasNextPage']:break
        after=page['pageInfo']['endCursor']
    tasks=json.loads((ROOT/'Data/backlog.json').read_text(encoding='utf-8'))
    expected={e['number'] for e in state['issues'].values()}
    actual={(i['content'] or {}).get('number') for i in items}
    if expected!=actual or len(items)!=len(tasks):raise RuntimeError('Project items do not match the 84 approved tasks.')
    counts=Counter();errors=[]
    for i in items:
        values={x['field']['name']:x.get('name',x.get('number')) for x in i['fieldValues']['nodes'] if x}
        counts[values.get('Status','Missing')]+=1
        for name in ['Status','Função','Prioridade','Horas','Capítulo']:
            if name not in values:errors.append(f"#{i['content']['number']}: {name} missing")
        if i['content']['state']!='OPEN':errors.append('Implementation task was closed prematurely.')
    if p['public']:errors.append('Project is not private.')
    if not any(v['name']=='Kanban geral' and v['layout']=='BOARD_LAYOUT' for v in p['views']['nodes']):errors.append('No board view.')
    board=next(v for v in p['views']['nodes'] if v['name']=='Kanban geral')
    if not any(f['name']=='Status' for f in board['verticalGroupByFields']['nodes']):errors.append('Board columns are not grouped by Status.')
    if REPO not in [r['nameWithOwner'] for r in p['repositories']['nodes']]:errors.append('Project is not linked to the new repository.')
    report={'url':p['url'],'private':not p['public'],'items':len(items),'statusCounts':dict(counts),
            'views':p['views']['nodes'],'allFieldsPopulated':not errors,'errors':errors}
    (ROOT/'QA/Planning/project-verification.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(report,ensure_ascii=False,indent=2))
    if errors:raise RuntimeError('Project verification failed.')
    return report

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('action',choices=['setup','fill','verify','tidy']);args=parser.parse_args()
    state=json.loads(STATE.read_text(encoding='utf-8'));c=Client()
    if args.action=='setup':setup(c,state)
    if args.action=='fill':
        tasks=json.loads((ROOT/'Data/backlog.json').read_text(encoding='utf-8'))
        sync(c,state,{t['id']:'backlog' if t['priority']!='P0' else 'blocked' if t['depends_on'] else 'ready' for t in tasks},include_metadata=True)
    if args.action=='tidy':
        # Only remove the untouched default view created by our setup, never issues/items.
        views=state['project'].get('views',{})
        if 'View 1' in views and 'Kanban geral' in views:
            c.graphql('mutation($i:DeleteProjectV2ViewInput!) { deleteProjectV2View(input:$i) { projectV2View { id } } }',{'i':{'viewId':views['View 1']['id']}})
            del views['View 1'];save(state)
    verify(c,state)
