"""Decode every exported recording and verify duration, signal and asset identity."""
from pathlib import Path
from concurrent.futures import ThreadPoolExecutor
import hashlib
import json
import os
import shutil
import subprocess
import numpy as np

ROOT=Path(__file__).resolve().parents[1]
FF=os.environ.get('FFMPEG') or shutil.which('ffmpeg') or str(ROOT/'QA/Local/TrailerTools/ffmpeg.exe')


def verify(item):
    chapter,line=item
    p=ROOT/line['clip']
    raw=subprocess.run([str(FF),'-v','error','-i',str(p),'-f','f32le','-ac','1','-ar','32000','pipe:1'],check=True,capture_output=True).stdout
    samples=np.frombuffer(raw,dtype=np.float32);duration=len(samples)/32000
    assert np.isfinite(samples).all(),line['key']
    rms=float(np.sqrt(np.mean(samples.astype(np.float64)**2)))
    peak=float(np.max(np.abs(samples)))
    assert abs(duration-line['duration_seconds'])<.08,(line['key'],duration,line['duration_seconds'])
    assert rms>.001,(line['key'],'empty/very quiet speech')
    assert peak<1,(line['key'],'clipping')
    return {'chapter':chapter,'key':line['key'],'duration_seconds':round(duration,3),
            'rms_dbfs':round(20*np.log10(rms),2),'peak_dbfs':round(20*np.log10(peak),2),
            'bytes':p.stat().st_size,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()}


def main():
    items=[];missing=[]
    for chapter in ['surveyor','pioneer','mars']:
        data=json.loads((ROOT/f'Data/Narration/{chapter}.json').read_text(encoding='utf-8'))
        for line in data['lines']:
            if line['status']=='matched':items.append((chapter,line))
            else:missing.append({'chapter':chapter,'key':line['key'],'subtitle_en':line['subtitle_en']})
    with ThreadPoolExecutor(max_workers=4) as pool:results=list(pool.map(verify,items))
    report={'validation':'All exported clips fully decoded; nonempty signal, no sample clipping, natural-speed durations match reviewed source edits.',
            'listening_review':'not_performed','clips':results,'missing_recordings':missing,
            'total_bytes':sum(x['bytes'] for x in results),'total_speech_seconds':round(sum(x['duration_seconds'] for x in results),3)}
    out=ROOT/'QA/NAR-001';out.mkdir(exist_ok=True,parents=True)
    (out/'audio-validation.json').write_text(json.dumps(report,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
    (out/'MISSING_RECORDINGS_EN.txt').write_text('\n\n'.join(x['chapter']+' / '+x['key']+'\n'+x['subtitle_en'] for x in missing)+'\n',encoding='utf-8')
    print('Decoded',len(results),'speech clips;',round(report['total_bytes']/1048576,2),'MiB;',report['total_speech_seconds'],'seconds;',len(missing),'unrecorded subtitle keys.')


if __name__=='__main__':main()
