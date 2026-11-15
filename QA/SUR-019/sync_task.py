"""Only SUR-019's Project fields. Uses the official API for Projects absent from connector."""
import json,sys,argparse
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];sys.path.insert(0,str(ROOT/'Tools'))
from github_setup import Client
ap=argparse.ArgumentParser();ap.add_argument('--done',action='store_true');args=ap.parse_args()
state=json.loads((ROOT/'Data/github-map.json').read_text(encoding='utf-8-sig'));project=state['project'];client=Client()
node=client.graphql('query { repository(owner:"joaovdamaceno",name:"AbandonedButNotForgotten-SpaceApps") { issue(number:201) { id state } } }')['repository']['issue']
if args.done and node['state']!='CLOSED':raise RuntimeError('Do not mark Project done before confirmed issue closure.')
item=client.graphql('mutation($p:ID!,$i:ID!){addProjectV2ItemById(input:{projectId:$p,contentId:$i}){item{id}}}',{'p':project['id'],'i':node['id']})['addProjectV2ItemById']['item']['id']
fields=[(project['statusFieldId'],project['statusOptions']['done' if args.done else 'doing'])]
for name,option in [('Função','R6'),('Capítulo','Surveyor 3'),('Prioridade','P0')]:fields.append((project['fields'][name]['id'],project['fields'][name]['options'][option]))
for field,option in fields:
    client.graphql('mutation($p:ID!,$i:ID!,$f:ID!,$o:String!){updateProjectV2ItemFieldValue(input:{projectId:$p,itemId:$i,fieldId:$f,value:{singleSelectOptionId:$o}}){projectV2Item{id}}}',{'p':project['id'],'i':item,'f':field,'o':option})
result={'issue':201,'task':'SUR-019','item':item,'status':'Concluída' if args.done else 'Em andamento'}
(ROOT/'QA/SUR-019/project.json').write_text(json.dumps(result,ensure_ascii=False,indent=2));print(json.dumps(result,ensure_ascii=False))
