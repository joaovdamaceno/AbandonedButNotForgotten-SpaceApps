"""Export a small batch of authorized issue prompts for the GitHub connector."""
from pathlib import Path
import json
import sys

ROOT=Path(__file__).resolve().parents[1]
tasks=json.loads((ROOT/'Data/backlog.json').read_text(encoding='utf-8'))
state=json.loads((ROOT/'Data/github-map.json').read_text(encoding='utf-8'))
start=int(sys.argv[1]);count=int(sys.argv[2]);out=[]
for t in tasks[start:start+count]:
    checkpoint=ROOT/'QA/Local/Created'/f"{t['id']}.json"
    if checkpoint.exists():continue
    chapter={'MAR':'mars','PIO':'pioneer','SUR':'surveyor'}.get(t['id'][:3],'shared')
    status='backlog' if t['priority']!='P0' else 'blocked' if t['depends_on'] else 'ready'
    milestone='Opcional' if t['priority']!='P0' else 'M0' if t['id'].startswith(('CORE','UI','ART-001','AUD','QA-001','QA-002')) else 'M2' if t['id'].startswith(('QA','REL','SCI','ART-002')) else 'M1'
    out.append(dict(id=t['id'],title=t['id']+' · '+t['title'],
        body=(ROOT/'Docs/Tasks'/f"{t['id']}.md").read_text(encoding='utf-8'),
        labels=['role:'+t['role'],'priority:'+t['priority'],'chapter:'+chapter,'status:'+status],
        milestone=state['milestones'][milestone]))
print(json.dumps(out,ensure_ascii=False))
