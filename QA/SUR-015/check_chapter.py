"""Real keyboard journey in an isolated headless browser. Does not skip chapter states."""
from pathlib import Path
import os,sys,json,argparse,re,time,statistics,math
root=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(root/'QA/Local/BrowserLib'));os.environ['PLAYWRIGHT_BROWSERS_PATH']=str(root/'QA/Local/Browsers')
from playwright.sync_api import sync_playwright
ap=argparse.ArgumentParser();ap.add_argument('--browser',default='chromium');ap.add_argument('--port',type=int,default=8868);ap.add_argument('--out',default='QA/SUR-015');ap.add_argument('--dossier-details',action='store_true');args=ap.parse_args();out=root/args.out;out.mkdir(exist_ok=True,parents=True)
with sync_playwright() as p:
    opts={'headless':True}
    if args.browser=='chromium':opts['args']=['--use-angle=d3d11','--autoplay-policy=user-gesture-required']
    browser=getattr(p,args.browser).launch(**opts);ctx=browser.new_context(viewport={'width':1280 if args.browser=='firefox' else 960,'height':720 if args.browser=='firefox' else 600},device_scale_factor=1);page=ctx.new_page();errors=[];stages=[];messages=[]
    def console(m):
        messages.append(m.text)
        if 'SURVEYOR_STEP ' in m.text:
            stages.append(m.text);print(m.text,flush=True)
        if m.type=='error':errors.append(m.text)
    page.on('console',console);page.on('pageerror',lambda e:errors.append(str(e)))
    page.add_init_script("""window.qaDrawCalls=0;window.qaFrames=[];for(const cls of [window.WebGLRenderingContext,window.WebGL2RenderingContext]){if(!cls)continue;for(const name of ['drawArrays','drawElements','drawArraysInstanced','drawElementsInstanced']){const old=cls.prototype[name];if(old)cls.prototype[name]=function(...a){window.qaDrawCalls++;return old.apply(this,a);};}}let last=performance.now();function sample(t){window.qaFrames.push({ms:t-last,drawCalls:window.qaDrawCalls});if(window.qaFrames.length>240)window.qaFrames.shift();window.qaDrawCalls=0;last=t;requestAnimationFrame(sample);}requestAnimationFrame(sample);""")
    render_samples={}
    def summarize(frames):
        calls=sorted(f['drawCalls'] for f in frames);times=sorted(f['ms'] for f in frames)
        return {'frames':len(frames),'drawMedian':statistics.median(calls),'drawP95':calls[int((len(calls)-1)*.95)],'drawMax':max(calls),'presentationMsMedian':statistics.median(times)} if frames else {}

    if args.browser=='chromium':
        cdp=ctx.new_cdp_session(page);cdp.send('Network.enable');cdp.send('Network.setCacheDisabled',{'cacheDisabled':True});cdp.send('Network.emulateNetworkConditions',{'offline':False,'latency':20,'downloadThroughput':1250000,'uploadThroughput':1250000})
    def wait(sec):page.wait_for_timeout(int(sec*1000))
    def shot(name):
        page.screenshot(path=str(out/(args.browser+'-'+name+'.png')));frames=page.evaluate('window.qaFrames');render_samples[name]=summarize(frames)
    def hold(sec):page.keyboard.down('e');wait(sec);page.keyboard.up('e');wait(.2)
    def stage(name,timeout=60):
        deadline=time.monotonic()+timeout
        while not any(('SURVEYOR_STEP '+name+' ') in s for s in stages):
            assert time.monotonic()<deadline,('Timed out waiting '+name,stages[-3:],errors)
            wait(.3)
    try:
        page.goto(f'http://127.0.0.1:{args.port}/',wait_until='domcontentloaded');page.wait_for_function('window.unityInstance',timeout=120000);wait(2);shot('archive');size=page.viewport_size
        if args.dossier_details:
            page.locator('#game').click(position={'x':size['width']*.924,'y':size['height']*.244});wait(.5);shot('details');page.keyboard.press('Escape');wait(.4)
        page.locator('#game').click(position={'x':size['width']*.884,'y':size['height']*.814});stage('Launch');wait(4);shot('launch')
        page.keyboard.press('p');wait(.5);shot('pause');page.keyboard.press('p');wait(.5)
        wait(14);hold(2.8);stage('Cruise');wait(10.5);page.keyboard.press('h');wait(6.5);hold(3.3);stage('Descent');wait(14.3);hold(3.3);stage('FirstImage');hold(2.3);wait(7);shot('first-image');stage('Observe')
        for n in range(3):
            page.keyboard.press('h');wait(7.5);hold(1.7);wait(1.8);shot('observation-'+str(n));hold(.3);wait(.4)
        stage('Sample');wait(2.7);hold(12.4);wait(4.5);hold(4.3);wait(3.6);hold(2.3);wait(5);shot('soil');stage('Eclipse');hold(1.2);wait(10);shot('eclipse');stage('LunarNight');stage('ImaginedVisit');wait(3);shot('imagined-notice');wait(25);shot('glove');stage('HistoricalReceipt');wait(2);shot('archive-photo');stage('End');wait(1);shot('memorial')
        # E starts while the cinematic still owns input and remains held across its end.
        before_return=len([m for m in messages if 'SURVEYOR_ARCHIVE_READY' in m]);page.keyboard.down('e');wait(7);assert len([m for m in messages if 'SURVEYOR_ARCHIVE_READY' in m])==before_return;page.keyboard.up('e');wait(.4)
        completed=[m for m in messages if 'SURVEYOR_COMPLETE active=' in m];assert completed,stages
        metrics=page.evaluate('window.unityInstance.GetMetricsInfo ? JSON.parse(JSON.stringify(window.unityInstance.GetMetricsInfo())) : null')
        page.keyboard.press('e');wait(3);shot('returned');assert len([m for m in messages if 'SURVEYOR_STEP Launch ' in m])==1;assert before_return==0 or len([m for m in messages if 'SURVEYOR_ARCHIVE_READY' in m])==before_return+1
        page.set_viewport_size({'width':3840,'height':2160});wait(.5);canvas=page.evaluate('({width:game.width,height:game.height})');assert canvas['width']<=1280 and canvas['height']<=720
        report={'browser':args.browser,'version':browser.version,'viewport':size,'readyMs':page.evaluate('window.webReadyMs'),'stages':stages,'completed':completed,'metrics':metrics,'renderSamples':render_samples,'drawCallMethod':'WebGL draw entrypoints per presentation callback, including UI and TV. Not GPU profiler timings.','returnDiagnosticAvailable':before_return>0,'canvasAt4K':canvas,'errors':errors,'network':'10Mbps 20ms cold' if args.browser=='chromium' else 'localhost unthrottled cold','automation':'Real key events. Headless GPU browser. No state skipping. Not novice timing or integrated-GPU certification.'}
        (out/(args.browser+'-chapter.json')).write_text(json.dumps(report,indent=2,ensure_ascii=False),encoding='utf-8');assert not errors,errors;print(json.dumps({k:v for k,v in report.items() if k!='renderSamples'},ensure_ascii=False),flush=True)
    except Exception:
        shot('failure');(out/(args.browser+'-failure.log')).write_text('\n'.join(messages[-160:]),encoding='utf-8');raise
    finally:browser.close()
