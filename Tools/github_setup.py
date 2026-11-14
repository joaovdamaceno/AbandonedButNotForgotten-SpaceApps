"""Use GitHub's official API for repository/Projects capabilities absent in connector.

Credential Manager credentials are used in memory only, never logged or written.
All writes target the explicitly authorized public repository and private Project.
"""
from pathlib import Path
import argparse
import json
import os
import re
import subprocess
import urllib.request
import urllib.error

ROOT = Path(__file__).resolve().parents[1]
REPO = 'joaovdamaceno/AbandonedButNotForgotten-SpaceApps'
EXPECTED_PRIVATE = False  # Explicit owner decision for this new repository.
TITLE = 'Abandoned, But Not Forgotten — produção 14–15 nov 2026'
STATE = ROOT/'Data/github-map.json'


class Client:
    def __init__(self, token=None):
        env = os.environ.copy()
        env.update(GIT_TERMINAL_PROMPT='0', GCM_INTERACTIVE='never')
        self.token=token or env.get('GH_TOKEN') or env.get('GITHUB_TOKEN')
        gh=ROOT/'Tools/Runtime/gh/bin/gh.exe'
        if not self.token and gh.exists():
            result=subprocess.run([str(gh),'auth','token','--hostname','github.com'],capture_output=True,text=True)
            if result.returncode==0:self.token=result.stdout.strip()
        if self.token:return
        result = subprocess.run(['git','-c','credential.interactive=never','credential','fill'],
            input='protocol=https\nhost=github.com\n\n', capture_output=True, text=True, env=env, check=True)
        fields = dict(line.split('=',1) for line in result.stdout.splitlines() if '=' in line)
        self.token = fields.get('password')
        if not self.token:
            raise RuntimeError('No cached GitHub credential. Use the official account sign-in flow.')

    def request(self, path, body=None, method=None):
        url = 'https://api.github.com/'+path.lstrip('/')
        req = urllib.request.Request(url, data=None if body is None else json.dumps(body).encode(),
            headers={'Authorization':'Bearer '+self.token, 'Accept':'application/vnd.github+json',
                     'Content-Type':'application/json', 'User-Agent':'ABNF-SpaceApps-Planning',
                     'X-GitHub-Api-Version':'2022-11-28'}, method=method)
        try:
            with urllib.request.urlopen(req, timeout=45) as r:
                self.scopes=r.headers.get('X-OAuth-Scopes','')
                data=r.read()
                return json.loads(data) if data else None
        except urllib.error.HTTPError as e:
            raw=e.read().decode(errors='replace')
            try:message=json.loads(raw).get('message','request failed')
            except (ValueError,AttributeError):message='non-JSON response; retry this idempotent operation if transient'
            raise RuntimeError(f"GitHub HTTP {e.code}: {message}") from None

    def graphql(self, query, variables=None):
        result=self.request('graphql',{'query':query,'variables':variables or {}})
        if result.get('errors'):
            raise RuntimeError('GraphQL: '+'; '.join(e['message'] for e in result['errors']))
        return result['data']


def sync_project(client, state, statuses):
    # Repository GITHUB_TOKEN cannot mutate a user's Project by itself.
    # Never copy the operator's broad OAuth credential into Actions secrets.
    if os.environ.get('GITHUB_ACTIONS')=='true' and not os.environ.get('PROJECTS_TOKEN'):
        return {'skipped':'Labels/dependências atualizadas. Colunas do Project exigem sincronização local autorizada ou PROJECTS_TOKEN configurado pelo titular.'}
    from project_board import sync
    return sync(client,state,statuses)


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('action', choices=['probe','create-repo','setup-metadata','dependencies','schema','verify-issues'])
    args=parser.parse_args()
    c=Client();user=c.request('user')
    if user['login'] != REPO.split('/')[0]:
        raise RuntimeError('Authenticated identity differs from the authorized repository owner.')
    if args.action == 'verify-issues':
        from collections import Counter
        state=json.loads(STATE.read_text(encoding='utf-8'))
        tasks=json.loads((ROOT/'Data/backlog.json').read_text(encoding='utf-8'))
        repo=c.request('repos/'+REPO)
        remote=c.request('repos/'+REPO+'/issues?state=all&per_page=100')
        by_number={i['number']:i for i in remote if 'pull_request' not in i}
        errors=[];edges=0;counts=Counter()
        for index,t in enumerate(tasks):
            entry=state['issues'][t['id']];issue=by_number[entry['number']]
            if issue['state']!='open':errors.append(t['id']+' incorrectly closed')
            names={l['name'] for l in issue['labels']}
            for label in ['role:'+t['role'],'priority:'+t['priority']]:
                if label not in names:errors.append(t['id']+' missing '+label)
            expected='backlog' if t['priority']!='P0' else 'blocked' if t['depends_on'] else 'ready'
            if 'status:'+expected not in names:errors.append(t['id']+' readiness label differs')
            counts[expected]+=1
            deps=c.request(f"repos/{REPO}/issues/{issue['number']}/dependencies/blocked_by")
            if not isinstance(deps,list):deps=deps.get('blocked_by',deps.get('issues',[]))
            expected_ids={state['issues'][d]['database_id'] for d in t['depends_on']}
            if {d['id'] for d in deps}!=expected_ids:errors.append(t['id']+' native dependency mismatch')
            edges+=len(deps)
            if t['depends_on'] and '](https://github.com/'+REPO+'/issues/' not in issue['body']:errors.append(t['id']+' lacks linked prerequisite prompts')
            if (index+1)%20==0:print(f"Verified issues/dependencies: {index+1}/{len(tasks)}",flush=True)
        if repo['private'] is not EXPECTED_PRIVATE:errors.append('Repository visibility differs from the authorized setting')
        result={'repository':repo['html_url'],'private':repo['private'],'issues':len(by_number),
                'dependencyEdges':edges,'readiness':dict(counts),'allImplementationTasksOpen':not any(i['state']!='open' for i in by_number.values()),'errors':errors}
        (ROOT/'QA/Planning/github-verification.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
        print(json.dumps(result,ensure_ascii=False,indent=2))
        if errors:raise RuntimeError('Issue verification failed.')
        return
    if args.action == 'schema':
        names=['ProjectV2ViewConfigurationInput','ProjectV2View','UpdateProjectV2FieldInput','ProjectV2ViewLayout']
        schema={}
        for name in names:
            schema[name]=c.graphql('query($n:String!) { __type(name:$n) { name inputFields { name type { kind name ofType { kind name ofType { kind name } } } } fields { name } enumValues { name } } }',{'n':name})['__type']
        (ROOT/'QA/Planning/projects-schema.json').write_text(json.dumps(schema,indent=2)+'\n',encoding='utf-8')
        print(json.dumps(schema,indent=2))
        return
    if args.action == 'dependencies':
        from assemble_plan import prompt
        tasks=json.loads((ROOT/'Data/backlog.json').read_text(encoding='utf-8'))
        remote=c.request('repos/'+REPO+'/issues?state=all&per_page=100')
        state=json.loads(STATE.read_text(encoding='utf-8'))
        by_id={}
        for issue in remote:
            if 'pull_request' in issue:continue
            match=re.match(r'^([A-Z]+-\d{3})\s',issue['title'])
            if match:
                id=match[1]
                if id in by_id:raise RuntimeError('Duplicate remote task '+id)
                by_id[id]=issue
        if {t['id'] for t in tasks} != set(by_id):raise RuntimeError('Remote task IDs differ from the approved backlog.')
        for id,i in by_id.items():
            entry=state['issues'].setdefault(id,{})
            entry.update(number=i['number'],url=i['html_url'],node_id=i['node_id'],database_id=i['id'])
        STATE.write_text(json.dumps(state,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
        edges=[]
        for index,t in enumerate(tasks):
            issue=by_id[t['id']];path=f"repos/{REPO}/issues/{issue['number']}"
            current=c.request(path+'/dependencies/blocked_by')
            if not isinstance(current,list):current=current.get('blocked_by',current.get('issues',[]))
            present={x['id'] for x in current}
            for dep in t['depends_on']:
                blocker=by_id[dep]
                if blocker['id'] not in present:c.request(path+'/dependencies/blocked_by',{'issue_id':blocker['id']},'POST')
                edges.append({'task':t['id'],'blockedBy':dep})
            body=prompt(t,state['issues'])
            if body!=issue['body']:c.request(path,{'body':body},'PATCH')
            if (index+1)%10==0:print(f"Dependencies/prompts: {index+1}/{len(tasks)}",flush=True)
        report={'issues':len(by_id),'edges':len(edges),'relationships':edges,'nativeDependenciesApplied':True}
        (ROOT/'QA/Planning/github-dependencies.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
        print(json.dumps({k:v for k,v in report.items() if k!='relationships'}))
        return
    if args.action == 'setup-metadata':
        repo=c.request('repos/'+REPO)
        if repo['private'] is not EXPECTED_PRIVATE:raise RuntimeError('Repository visibility differs from the authorized setting.')
        labels={
          'priority:P0':('b60205','História completa e critérios essenciais'),
          'priority:P1':('fbca04','Polimento após o MVP dos três capítulos'),
          'priority:P2':('cfd3d7','Fora do compromisso do fim de semana'),
          'status:blocked':('d73a4a','Depende de entrega ainda não integrada'),
          'status:ready':('0e8a16','Bloqueadores integrados; pode assumir'),
          'status:doing':('1d76db','Uma pessoa está executando'),
          'status:review':('5319e7','PR e evidência aguardam revisão'),
          'status:backlog':('cfd3d7','Trabalho opcional não iniciado'),
          'integrated':('006b75','Entrega integrada em develop e verificada'),
          'external-blocker':('e99695','Bloqueio externo documentado'),
          'chapter:shared':('bfd4f2','Infraestrutura e integração comuns'),
          'chapter:mars':('d4a373','Spirit e Opportunity'),
          'chapter:pioneer':('d4c5f9','Pioneer 10'),
          'chapter:surveyor':('cccccc','Surveyor 3')}
        for r,desc in [('R1','Integração e núcleo'),('R2','UX e orientação'),('R3','Gameplay Marte'),('R4','Arte e cinema Marte'),('R5','Pioneer'),('R6','Surveyor'),('R7','Ciência áudio QA performance')]:
            labels['role:'+r]=('ededed',desc)
        existing={x['name'] for x in c.request('repos/'+REPO+'/labels?per_page=100')}
        for name,(color,description) in labels.items():
            if name not in existing:c.request('repos/'+REPO+'/labels',dict(name=name,color=color,description=description),'POST')
        milestones=c.request('repos/'+REPO+'/milestones?state=all&per_page=100')
        for title in ['M0 — Fundação Web e contratos','M1 — Três capítulos completos','M2 — Validação e entrega','Opcional — Depois do MVP']:
            if not any(x['title']==title for x in milestones):
                milestones.append(c.request('repos/'+REPO+'/milestones',dict(title=title,description='Tarefas planejadas; implementação e testes ainda pendentes.'),'POST'))
        state=json.loads(STATE.read_text(encoding='utf-8'))
        state['milestones']={x['title'].split(' — ')[0]:x['number'] for x in milestones}
        STATE.write_text(json.dumps(state,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
        print(json.dumps({'labels':len(labels),'milestones':state['milestones']},ensure_ascii=False))
        return
    if args.action == 'create-repo':
        try:
            repo=c.request('repos/'+REPO)
            if repo['private'] is not EXPECTED_PRIVATE or repo.get('size',0):
                raise RuntimeError('Existing repository has a different visibility or content; will not overwrite it.')
        except RuntimeError as e:
            if 'HTTP 404:' not in str(e):raise
            repo=c.request('user/repos',{'name':REPO.split('/')[1], 'private':EXPECTED_PRIVATE,
                'description':'Novo projeto: Spirit & Opportunity, Pioneer 10 e Surveyor 3 — planejamento e produção 14–15 nov 2026.',
                'has_issues':True,'auto_init':False,'delete_branch_on_merge':True}, 'POST')
        state={'repository':REPO,'url':repo['html_url'],'repositoryId':repo['node_id'],
               'private':repo['private'],'issues':{},'project':None}
        STATE.write_text(json.dumps(state,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
        print(json.dumps({k:v for k,v in state.items() if k!='issues'},ensure_ascii=False,indent=2))
        return
    result={'login':user['login'],'scopes':c.scopes}
    try:
        data=c.graphql('query { viewer { id projectsV2(first:20) { nodes { id title number url public } } } }')
        result['projects']=data['viewer'];result['projectsAccessible']=True
    except RuntimeError as e:
        result['projectsAccessible']=False;result['projectsError']=str(e)
    try:
        data=c.graphql('query { __type(name:"CreateProjectV2ViewInput") { name inputFields { name } } }')
        result['viewInput']=data['__type']
    except RuntimeError as e:result['schemaError']=str(e)
    p=ROOT/'QA/Planning/github-probe.json';p.parent.mkdir(parents=True,exist_ok=True)
    p.write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(result,ensure_ascii=False,indent=2))


if __name__=='__main__':main()
