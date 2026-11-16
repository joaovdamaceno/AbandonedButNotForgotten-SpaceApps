"""NAR-001: align supplied English recordings with current subtitle keys.

Original WAVs and local ASR transcripts stay in QA/Local. Alignment is reviewed
in Data/Narration before exporting naturally paced, mono Vorbis speech clips.
No speech synthesis, speed changes, or transcription service uploads.
"""
import argparse
import hashlib
import json
import os
import re
import shutil
import subprocess
import unicodedata
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
LOCAL = ROOT / 'QA/Local/ChapterNarration'
FFMPEG = os.environ.get('FFMPEG') or shutil.which('ffmpeg') or str(ROOT / 'QA/Local/TrailerTools/ffmpeg.exe')
OUT = ROOT / 'Data/Narration'
DESTINATIONS = {
    'surveyor': 'Assets/_Game/Chapters/Surveyor/Narration',
    'pioneer': 'Assets/_Game/Chapters/Pioneer10/Narration',
    'mars': 'Assets/_Game/Chapters/Mars/Narration',
}


def read(path):
    return json.loads((ROOT / path).read_text(encoding='utf-8-sig'))


def tokens(text):
    text = unicodedata.normalize('NFKD', text.lower()).replace('’', "'")
    text = text.replace('kilometres', 'kilometers').replace('travelled', 'traveled')
    return re.findall(r"[a-z0-9]+(?:'[a-z]+)?", text)


def references(chapter):
    if chapter == 'surveyor':
        return [{'key':e['key'], 'text':e['en']} for e in
                read('Assets/_Game/Chapters/Surveyor/Documentary/DocumentaryTexts.json')['texts']
                if e.get('group') == 'subtitle']
    if chapter == 'pioneer':
        return [{'key':e['key'], 'text':e['text']} for e in
                read('Data/Localization/Pioneer10.en.json')['entries']
                if e.get('group') == 'narrative']
    opening = read('Assets/_Game/Chapters/Mars/Data/MarsSubtitles.json')['cues']
    result = [{'key':e['textKey'], 'text':e['en']} for e in opening if e['stepId'] in ['M01','M02','M03']]
    local = {e['key']:e['en'] for e in read('Assets/_Game/Chapters/Mars/Opportunity/Resources/Localization.json')['entries']}
    keys = ['v18.landing.descent','v15.landing.opp','v14.berries.water','v14.berries.doubt',
            'route.outcrop','v13.discovery.evidence','v14.discovery.bridge','v13.hardware.arrival',
            'v13.meteorite.result','v13.years.work','v15.storm.frequent','v15.storm.dark',
            'v15.spirit.death','v17.spirit.signal','v15.spirit.alone','v13.years.valley',
            'v14.sun.reason','v15.storm.again','nar.mars.last.image','v13.last.power',
            'v16.last.contact','v16.farewell.legacy','v17.farewell.opportunity',
            'v16.farewell.end','v16.farewell.memory']
    result += [{'key':key, 'text':local[key]} for key in keys if key in local]
    return result


def align(reference, words):
    """Semi-global token edit distance: complete reference vs any ASR substring.

    Independent matches tolerate the reordered recordings; a global comparison
    of JSON entry order would associate some lines with the wrong chapter beat.
    """
    ref = tokens(reference)
    actual = [w['token'] for w in words]
    n, m = len(ref), len(actual)
    scores = [[0]*(m+1) for _ in range(n+1)]
    paths = [bytearray(m+1) for _ in range(n+1)]
    for i in range(1,n+1):
        scores[i][0]=i; paths[i][0]=1
        for j in range(1,m+1):
            costs=(scores[i-1][j-1]+(ref[i-1]!=actual[j-1]),scores[i-1][j]+1,scores[i][j-1]+1)
            op=min(range(3),key=costs.__getitem__)
            scores[i][j]=costs[op];paths[i][j]=op
    end=min(range(1,m+1),key=lambda j:scores[n][j])
    i,j=n,end; matched=0
    while i:
        op=paths[i][j]
        if op==0:
            matched+=ref[i-1]==actual[j-1];i-=1;j-=1
        elif op==1:i-=1
        else:j-=1
    start=j
    score=matched/max(n,end-start,1)
    return start,end,score


def analyze(chapter):
    asr=read(f'QA/Local/ChapterNarration/Transcripts/{chapter}.json')
    words=[]
    for segment in asr['segments']:
        for word in segment['words']:
            for token in tokens(word['word']):
                words.append(dict(token=token,start=word['start'],end=word['end'],word=word['word'].strip()))
    rows=[]
    for ref in references(chapter):
        a,b,confidence=align(ref['text'],words)
        accepted=confidence>=.56 and b>a
        start=max(0,words[a]['start']-.08);end=min(asr['source_duration_seconds'],words[b-1]['end']+.10)
        row={'key':ref['key'],'subtitle_en':ref['text'],'alignment_score':round(confidence,3),
             'status':'matched' if accepted else 'not_recorded_or_needs_review',
             'source_in_seconds':round(start,3) if accepted else None,
             'source_out_seconds':round(end,3) if accepted else None,
             'asr_excerpt':' '.join(w['word'] for w in words[a:b]),
             'clip':f'{DESTINATIONS[chapter]}/Clips/{ref["key"]}.ogg' if accepted else None}
        rows.append(row)
    edits_path=ROOT/'Data/Narration/edits.json'
    edits=json.loads(edits_path.read_text(encoding='utf-8')).get(chapter,{}) if edits_path.exists() else {}
    for row in rows:
        if row['key'] in edits:
            row.update(edits[row['key']])
            if row['status']=='matched':row['clip']=f'{DESTINATIONS[chapter]}/Clips/{row["key"]}.ogg'
    matched=[x for x in rows if x['status']=='matched']
    # Supplemental recordings have their own zero-based source clock.
    # Only resolve overlaps within the same physical recording.
    for source_name in sorted({x.get('source_file',asr['source_file']) for x in matched}):
        group=sorted([x for x in matched if x.get('source_file',asr['source_file'])==source_name],key=lambda x:x['source_in_seconds'])
        for previous,current in zip(group,group[1:]):
            if previous['source_out_seconds']>current['source_in_seconds']:
                mid=round((previous['source_out_seconds']+current['source_in_seconds'])/2,3)
                previous['source_out_seconds']=mid;current['source_in_seconds']=mid
    result={'chapter':chapter,'source_file':asr['source_file'],'source_sha256':asr['source_sha256'],
            'language':'en','processing':'Natural speed, mono 32 kHz, high-pass 70 Hz, chapter-level loudness normalization, Vorbis q4.',
            'alignment':'Local small.en ASR with word times + token alignment against current develop text; review required.',
            'lines':rows}
    OUT.mkdir(exist_ok=True,parents=True)
    (OUT/f'{chapter}.json').write_text(json.dumps(result,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
    print(chapter,len(matched),'matched of',len(rows))
    for row in rows:
        if row['status']!='matched' or row['alignment_score'] is None or row['alignment_score']<.75:
            print(row['status'],row['key'],row['alignment_score'],row['asr_excerpt'])


def ffmpeg(args):
    return subprocess.run([str(FFMPEG),'-nostdin','-hide_banner','-y',*map(str,args)],
                          check=True,capture_output=True,text=True,encoding='utf-8',errors='replace')


def normalized_master(chapter, source_name, source_hash, supplemental=False):
    source=LOCAL/'Originals'/source_name
    if hashlib.sha256(source.read_bytes()).hexdigest()!=source_hash:
        raise ValueError('Original recording hash differs: '+source_name)
    master=LOCAL/(f'{chapter}_{source_hash[:12]}_master.wav' if supplemental else f'{chapter}_master.wav')
    if not master.exists():
        # Explicit averaging precedes normalization: an implicit stereo downmix
        # after limiting can add 3 dB to these dual-mono voice recordings.
        mono='pan=mono|c0=0.5*c0+0.5*c1,highpass=f=70'
        first=ffmpeg(['-i',source,'-af',mono+',loudnorm=I=-18:TP=-3:LRA=9:print_format=json','-f','null','-'])
        stats=json.loads(re.findall(r'\{\s*"input_i".*?\}',first.stderr,re.S)[-1])
        loud=(f'loudnorm=I=-18:TP=-3:LRA=9:linear=true:measured_I={stats["input_i"]}:'
              f'measured_TP={stats["input_tp"]}:measured_LRA={stats["input_lra"]}:'
              f'measured_thresh={stats["input_thresh"]}:offset={stats["target_offset"]}')
        ffmpeg(['-i',source,'-af',mono+','+loud,'-ac','1','-ar','32000','-c:a','pcm_f32le',master])
        master.with_suffix('.normalization.json').write_text(json.dumps(stats,indent=2),encoding='utf-8')
    return master


def export(chapter, keys=None):
    data=read(f'Data/Narration/{chapter}.json')
    selected=[row for row in data['lines'] if row['status']=='matched' and (keys is None or row['key'] in keys)]
    if keys and set(keys)!={row['key'] for row in selected}:
        raise ValueError('Unknown or unmatched export keys: '+str(set(keys)-{row['key'] for row in selected}))
    masters={}
    for row in selected:
        name=row.get('source_file',data['source_file'])
        digest=row.get('source_sha256',data['source_sha256'])
        if name not in masters:masters[name]=normalized_master(chapter,name,digest,name!=data['source_file'])
        master=masters[name]
        dest=ROOT/row['clip'];dest.parent.mkdir(parents=True,exist_ok=True)
        a,b=row['source_in_seconds'],row['source_out_seconds']
        ranges=row.get('retained_ranges_seconds',[[a,b]])
        length=sum(end-start for start,end in ranges)-.006*(len(ranges)-1)
        assert length>0.25 and length<45,(row['key'],length)
        if len(ranges)==1:
            ffmpeg(['-ss',a,'-i',master,'-af',f'atrim=duration={length},asetpts=PTS-STARTPTS,afade=t=in:d=0.008,afade=t=out:st={length-.015}:d=0.015',
                    '-ac','1','-ar','32000','-c:a','libvorbis','-q:a','4','-map_metadata','-1',dest])
        else:
            assert len(ranges)==2
            graph=';'.join(f'[0:a]atrim=start={start}:end={end},asetpts=PTS-STARTPTS[a{i}]' for i,(start,end) in enumerate(ranges))
            graph+=f';[a0][a1]acrossfade=d=0.006:c1=tri:c2=tri,afade=t=in:d=0.008,afade=t=out:st={length-.015}:d=0.015[a]'
            ffmpeg(['-i',master,'-filter_complex',graph,'-map','[a]','-ac','1','-ar','32000','-c:a','libvorbis','-q:a','4','-map_metadata','-1',dest])
        row['duration_seconds']=round(length,3)
    (OUT/f'{chapter}.json').write_text(json.dumps(data,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
    print('Exported',chapter,len(selected),'clips',flush=True)


if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--export',action='store_true');parser.add_argument('--chapter',choices=DESTINATIONS)
    parser.add_argument('--keys',nargs='+',help='Export only these reviewed subtitle keys; requires --export and --chapter.')
    args=parser.parse_args()
    if args.keys and (not args.export or not args.chapter):parser.error('--keys requires --export and --chapter')
    for chapter in ([args.chapter] if args.chapter else DESTINATIONS):
        if args.export:export(chapter,args.keys)
        else:analyze(chapter)
