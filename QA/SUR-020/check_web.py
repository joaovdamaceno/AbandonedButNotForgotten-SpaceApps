"""SUR-020 real browser controls, no Unity SendMessage/state jumps. Not a novice study."""
from pathlib import Path
import os,sys,json,argparse,time,hashlib
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'QA/Local/BrowserLib'))
os.environ['PLAYWRIGHT_BROWSERS_PATH']=str(ROOT/'QA/Local/Browsers')
from playwright.sync_api import sync_playwright
ap=argparse.ArgumentParser();ap.add_argument('--browser',default='chromium');ap.add_argument('--port',type=int,default=8868);ap.add_argument('--record',action='store_true');args=ap.parse_args()
OUT=ROOT/'QA/SUR-020'/args.browser;OUT.mkdir(exist_ok=True,parents=True)
with sync_playwright() as p:
    options={'headless':True}
    if args.browser=='chromium':options['args']=['--use-angle=d3d11','--autoplay-policy=user-gesture-required']
    browser=getattr(p,args.browser).launch(**options);context_options={'viewport':{'width':1280,'height':720},'device_scale_factor':1}
    if args.record:context_options.update(record_video_dir=str(ROOT/'QA/Local/SUR020Videos'/args.browser),record_video_size={'width':1280,'height':720})
    context=browser.new_context(**context_options)
    page=context.new_page();messages=[];errors=[];stages=[];metrics={}
    def console(m):
        messages.append(m.text)
        if m.type=='error':errors.append(m.text)
        if 'SURVEYOR_DOC_' in m.text:print(m.text,flush=True)
        if 'SURVEYOR_DOC_STEP ' in m.text:stages.append(m.text)
    page.on('console',console);page.on('pageerror',lambda e:errors.append(str(e)))
    if args.browser=='chromium':
        cdp=context.new_cdp_session(page);cdp.send('Network.enable');cdp.send('Network.setCacheDisabled',{'cacheDisabled':True})
        cdp.send('Network.emulateNetworkConditions',{'offline':False,'latency':20,'downloadThroughput':1250000,'uploadThroughput':1250000})
    def wait(s):page.wait_for_timeout(int(s*1000))
    def shot(name):
        page.screenshot(path=str(OUT/(name+'.png')))
        metrics[name]=page.evaluate('window.unityInstance.GetMetricsInfo ? JSON.parse(JSON.stringify(window.unityInstance.GetMetricsInfo())) : null')
    def stage(name,timeout=120):
        deadline=time.monotonic()+timeout
        while not any(('SURVEYOR_DOC_STEP '+name+' ') in s for s in stages):
            assert time.monotonic()<deadline,('Timed out',name,stages[-3:],errors[-3:]);wait(.25)
    def has(text):return any(text in m for m in messages)
    try:
        page.goto(f'http://127.0.0.1:{args.port}/',wait_until='domcontentloaded');page.wait_for_function('window.unityInstance',timeout=120000)
        deadline=time.monotonic()+60
        while not has('SURVEYOR_ARCHIVE_READY'):
            assert time.monotonic()<deadline,'Archive was not ready';wait(.15)
        wait(2);shot('00-archive')
        page.mouse.move(1130,586);page.mouse.click(1130,586);stage('Prologue');wait(3);shot('01-opening')
        page.keyboard.press('p');wait(.4);shot('02-pause');page.keyboard.press('p');wait(.4)
        stage('Arrival');wait(12);shot('03-separation');wait(9.5);shot('03a-lunar-rebound');stage('Science');wait(3);shot('04-worksite')
        page.locator('#game').focus()
        page.mouse.move(515,373);page.mouse.down();wait(.8)
        for sweep in range(4):
            for tick in range(48):
                x=515+250*(tick/47 if sweep%2==0 else 1-tick/47)
                page.mouse.move(x,373);wait(.08)
            if has('SURVEYOR_DOC_CUT_READY'):break
        assert has('SURVEYOR_DOC_CUT_READY'),'Mouse strokes did not prepare the trench'
        page.mouse.up();wait(.3);shot('05-trench')
        assert has('SURVEYOR_DOC_PRESS_READY'),'Release must advance directly to hold E'
        page.keyboard.down('e')
        deadline=time.monotonic()+12
        while not has('SURVEYOR_DOC_SCIENCE 3'):
            assert time.monotonic()<deadline,'E hold did not complete pressure';wait(.15)
        page.keyboard.up('e');wait(.4);shot('06-pressure')
        stage('Findings',25);wait(10);shot('07-player-photo')
        stage('Night');wait(5);shot('08a-ingress');wait(5);shot('08-eclipse');wait(6.5);shot('08b-egress');stage('Reunion');wait(23);shot('09-reunion');wait(16);shot('10-recovery');stage('Legacy');wait(4);shot('11-archive-photo')
        wait(3);page.keyboard.down('e');stage('End',30);wait(2);shot('12-memorial')
        before=len([m for m in messages if 'SURVEYOR_ARCHIVE_READY' in m]);assert has('SURVEYOR_DOC_COMPLETE')
        page.keyboard.up('e');wait(.4);page.mouse.click(640,600);wait(.5);shot('12b-credits');page.mouse.click(640,565);wait(.4);page.keyboard.press('e');wait(3);shot('13-returned')
        assert len([m for m in messages if 'SURVEYOR_ARCHIVE_READY' in m])==before+1
        page.set_viewport_size({'width':3840,'height':2160});wait(.5);canvas=page.evaluate('({width:game.width,height:game.height})');assert canvas['width']<=1280 and canvas['height']<=720
        renderer=page.evaluate("(()=>{let g=game.getContext('webgl2');let e=g.getExtension('WEBGL_debug_renderer_info');return e?g.getParameter(e.UNMASKED_RENDERER_WEBGL):g.getParameter(g.RENDERER);})()")
        hashes={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in (ROOT/'Builds/SurveyorWeb/Build').glob('*.br')}
        report={'browser':args.browser,'version':browser.version,'renderer':renderer,'videoRecorded':args.record,'stages':stages,'completion':[m for m in messages if 'SURVEYOR_DOC_COMPLETE' in m],'readyMs':page.evaluate('window.webReadyMs'),'metrics':metrics,'canvasAt4K':canvas,'errors':errors,'network':'10Mbps 20ms cold' if args.browser=='chromium' else 'localhost cold','method':'Real mouse-only cut, release to advance, E-only pressure and automatic photograph in headless GPU browser. No state skipping; not novice timing, integrated-GPU validation or hosted itch test.','buildSha256':hashes}
        (OUT/'report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2));assert not errors,errors
        print(json.dumps({'result':'passed','completion':report['completion'],'errors':errors}),flush=True)
    except Exception:
        shot('failure');(OUT/'console.log').write_text('\n'.join(messages),encoding='utf-8');(OUT/'failure-metrics.json').write_text(json.dumps(metrics,indent=2));raise
    finally:context.close();browser.close()
