"""Summarize observed artifacts; never turn sample FPS into a sustained/integrated-GPU claim."""
from pathlib import Path
import json, xml.etree.ElementTree as ET
ROOT=Path(__file__).resolve().parents[2]; out=ROOT/'QA/SUR-019'
package=json.loads((out/'package.json').read_text()); suite=ET.parse(out/'editor-results.xml').getroot()
assert suite.attrib['failed']=='0' and suite.attrib['passed']=='10'
expected={f['path'].split('/')[-1]:f['sha256'] for f in package['files'] if f['path'].endswith('.br')}
result={'unity':'6000.6.7f1','urp':'17.6.0','editorTests':{'passed':10,'failed':0},
        'buildBytes':package['buildBytes'],'zipBytes':package['zipBytes'],'browsers':[],
        'limits':['Not a novice study or artistic/audio approval.','Not tested on integrated GPU or hosted itch.io.',
                  'WASM heap excludes GPU and much of the browser.','FPS values are samples, not a sustained benchmark.',
                  'The headless tab switch did not report a hidden page; focus handling is covered by native tests, not proven by this switch.',
                  'Firefox masks the GPU name; its GTX 980 string is not hardware identification.',
                  'Nonfatal Unity warnings include compressed-audio metadata/pitch and unavailable FSR postprocessing; the custom documentary treatment remains active.']}
for browser in ['chromium','firefox']:
    r=json.loads((out/browser/'report.json').read_text()); lifecycle=json.loads((out/browser/'lifecycle.json').read_text())
    assert not r['errors'] and not lifecycle['pageErrors'];assert r['buildSha256']==expected
    samples=[v for k,v in r['metrics'].items() if k[:2].isdigit() and 4<=int(k[:2])<=12 and v]
    completion=float(r['completion'][0].split('active=')[1].strip())
    result['browsers'].append({'browser':browser,'version':r['version'],'reportedRenderer':r['renderer'],
        'activeSeconds':completion,'coldReadyMs':r['readyMs'],'network':r['network'],
        'sampleFpsMin':min(v['fps'] for v in samples),'sampleFpsMax':max(v['fps'] for v in samples),
        'peakObservedUsedWASMMiB':max(v['usedWASMHeapSize'] for v in r['metrics'].values() if v)/1048576,
        'earlyRestarts':lifecycle['restarts'],'usedWASMBytesAfterEachRestart':[v['usedWASMHeapSize'] for v in lifecycle['metricsAfterRestart']],
        'canvasAt4K':r['canvasAt4K'],'runtimeErrors':0})
(out/'validation-summary.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8');print(json.dumps(result,ensure_ascii=False,indent=2))
