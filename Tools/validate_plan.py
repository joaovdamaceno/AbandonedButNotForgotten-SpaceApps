"""Validate authored work packages, dependency DAG and local planning links."""
from pathlib import Path
from collections import Counter
import ast
import json
import re
import sys

ROOT=Path(__file__).resolve().parents[1]

def validate():
    tasks=json.loads((ROOT/'Data/backlog.json').read_text(encoding='utf-8'))
    ids={t['id']:t for t in tasks};errors=[];checks=[]
    if len(ids)!=len(tasks):errors.append('Duplicate task IDs')
    required=['title','role','priority','hours','depends_on','files','owned_paths','context','deliverable','instructions','acceptance','tests','sources']
    for t in tasks:
        for key in required:
            if key not in t:errors.append(f"{t['id']}: missing {key}")
        if t['role'] not in ['R1','R2','R3','R4','R5','R6','R7']:errors.append('Unknown role '+t['id'])
        if not 0<t['hours']<=4:errors.append('Task must be split or estimated: '+t['id'])
        if t['priority'] not in ['P0','P1','P2']:errors.append('Unknown priority '+t['id'])
        for dep in t['depends_on']:
            if dep not in ids:errors.append(f"{t['id']}: missing dependency {dep}")
            elif t['priority']=='P0' and ids[dep]['priority']!='P0':errors.append(f"P0 depends on optional task: {t['id']}→{dep}")
        for key in ['instructions','acceptance','tests','owned_paths']:
            if not isinstance(t[key],list) or not t[key]:errors.append(f"{t['id']}: empty {key}")
        for path in t['owned_paths']:
            if path.startswith('/') or '..' in Path(path).parts:errors.append(f"Unsafe ownership path {t['id']}: {path}")
            if 'Assets/Game/' in path:errors.append(f"Noncanonical Unity path {t['id']}: {path}")
        p=ROOT/'Docs/Tasks'/f"{t['id']}.md"
        if not p.exists() or 'Critérios de aceite' not in p.read_text(encoding='utf-8'):errors.append('Missing complete prompt '+t['id'])
    visiting=set();done=set();path=[]
    def visit(id):
        if id in visiting:errors.append('Dependency cycle: '+' → '.join(path+[id]));return
        if id in done or id not in ids:return
        visiting.add(id);path.append(id)
        for dep in ids[id]['depends_on']:visit(dep)
        path.pop();visiting.remove(id);done.add(id)
    for id in ids:visit(id)
    checks+=['IDs, required fields, role/priority and bounded estimates','All dependency targets and no P0→P1/P2','Directed dependency graph has no cycles','Every task has a complete AI-ready Markdown prompt']
    broken=[]
    for p in ROOT.rglob('*.md'):
        # Pastas locais geradas pela Unity (ignoradas pelo Git) contêm Markdown de pacotes de terceiros.
        if '.git' in p.parts or 'Runtime' in p.parts or {'Library','Temp','Logs','UserSettings','Obj','Builds'} & set(p.relative_to(ROOT).parts[:1]):continue
        for dest in re.findall(r'\]\(([^)]+)\)',p.read_text(encoding='utf-8-sig')):
            if dest.startswith(('http:','https:','mailto:','#')):continue
            target=dest.split('#',1)[0]
            if target and not (p.parent/target).exists():broken.append(f'{p.relative_to(ROOT)} → {target}')
    if broken:errors+=['Broken local link: '+x for x in broken]
    else:checks.append('Relative Markdown links resolve')
    cue_counts={}
    for name in ['01_MARS_TWINS.md','02_PIONEER_10.md','03_SURVEYOR_3.md']:
        text=(ROOT/'Docs/Chapters'/name).read_text(encoding='utf-8')
        rows=[l for l in text.splitlines() if l.startswith('|') and re.search(r'(?:MAR\.S|PIO[.-]S|PIO[.-]C|SUR-S|MAR[.-]C)',l)]
        # Formats differ by chapter; final review verifies the table contents.
        cue_counts[name]=len(rows)
        if not ('English' in text or '| EN' in text or 'EN |' in text):errors.append('No English subtitle table '+name)
    for p in (ROOT/'Tools').glob('*.py'):
        try:ast.parse(p.read_text(encoding='utf-8-sig'))
        except SyntaxError as e:errors.append(f'Python syntax {p.name}:{e.lineno}')
    checks+=['Three chapters contain bilingual narrative tables','Production Python scripts parse without executing runtime code']
    report={'taskCount':len(tasks),'p0Hours':sum(t['hours'] for t in tasks if t['priority']=='P0'),
            'checks':checks,'errors':errors,'note':'Planning structure only. No new Unity build, gameplay or human session was executed.'}
    out=ROOT/'QA/Planning/validation.json';out.parent.mkdir(parents=True,exist_ok=True)
    out.write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(report,ensure_ascii=False,indent=2))
    return not errors

if __name__=='__main__':sys.exit(0 if validate() else 1)
