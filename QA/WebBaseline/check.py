"""Reproducible browser smoke. Isolated test browsers in QA/Local; no account/profile access."""
from pathlib import Path
import os, sys, json, argparse
root = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(root/'QA/Local/BrowserLib'))
os.environ['PLAYWRIGHT_BROWSERS_PATH'] = str(root/'QA/Local/Browsers')
from playwright.sync_api import sync_playwright
parser=argparse.ArgumentParser();parser.add_argument('--browser',default='chromium');parser.add_argument('--port',type=int,default=8866);args=parser.parse_args()
out=root/'QA/WebBaseline';out.mkdir(exist_ok=True)
with sync_playwright() as p:
    launcher=getattr(p,args.browser)
    options={'headless':True}
    if args.browser=='chromium':options['args']=['--use-angle=d3d11','--autoplay-policy=user-gesture-required']
    browser=launcher.launch(**options)
    context=browser.new_context(viewport={'width':960,'height':600},device_scale_factor=1)
    page=context.new_page();errors=[];warnings=[];headers={}
    page.on('pageerror',lambda e:errors.append(str(e)))
    page.on('console',lambda m:errors.append(m.text) if m.type=='error' else warnings.append(m.text) if m.type=='warning' else None)
    page.on('response',lambda r:headers.update({r.url.rsplit('/',1)[-1]:r.headers}) if '.br' in r.url else None)
    page.add_init_script("""window.qaContexts=[];window.qaAudioStarts=[];const Native=window.AudioContext;if(Native)window.AudioContext=class extends Native{constructor(...a){super(...a);window.qaContexts.push(this);}};const start=AudioBufferSourceNode.prototype.start;AudioBufferSourceNode.prototype.start=function(...a){if(this.buffer&&this.buffer.duration>.05)window.qaAudioStarts.push({time:performance.now(),duration:this.buffer.duration});return start.apply(this,a);};""")
    if args.browser=='chromium':
        cdp=context.new_cdp_session(page);cdp.send('Network.enable');cdp.send('Network.setCacheDisabled',{'cacheDisabled':True});cdp.send('Network.emulateNetworkConditions',{'offline':False,'latency':20,'downloadThroughput':1250000,'uploadThroughput':1250000})
    page.goto(f'http://127.0.0.1:{args.port}/?qa=1',wait_until='domcontentloaded')
    page.wait_for_function('window.unityInstance && window.webBaseline',timeout=120000)
    page.screenshot(path=str(out/f'{args.browser}-entry.png'))
    audio_before=page.evaluate('window.qaContexts.map(c=>c.state)')
    starts_before=page.evaluate('window.qaAudioStarts')
    page.locator('#game').click(position={'x':480,'y':300});page.wait_for_timeout(3000)
    page.keyboard.down('e');page.wait_for_timeout(6200);page.keyboard.up('e');page.wait_for_timeout(300)
    page.screenshot(path=str(out/f'{args.browser}-world.png'))
    sample=page.evaluate('window.webBaseline');audio_after=page.evaluate('window.qaContexts.map(c=>c.state)')
    starts_after=page.evaluate('window.qaAudioStarts')
    page.keyboard.press('p');page.wait_for_timeout(500);page.screenshot(path=str(out/f'{args.browser}-pause.png'))
    page.keyboard.press('p');page.wait_for_timeout(300)
    page.set_viewport_size({'width':3840,'height':2160});page.wait_for_timeout(600)
    size=page.evaluate('({width:game.width,height:game.height})');assert size['width']<=1280 and size['height']<=720,size
    report={'browser':args.browser,'version':browser.version,'viewport':'960x600','network':'10 Mbps,20 ms, empty browser context' if args.browser=='chromium' else 'local unthrottled, empty browser context','readyMs':page.evaluate('window.webReadyMs'),'sample':sample,'canvasAt4K':size,'audioBeforeClick':audio_before,'audioAfterClick':audio_after,'sourcesBeforeClick':starts_before,'sourcesAfterClick':starts_after,'errors':errors,'warnings':warnings,'headers':headers,'downloadResources':page.evaluate("performance.getEntriesByType('resource').map(r=>({name:r.name.split('/').pop(),encoded:r.encodedBodySize,transferred:r.transferSize}))")}
    (out/f'{args.browser}.json').write_text(json.dumps(report,indent=2,ensure_ascii=False),encoding='utf-8')
    assert not errors,errors
    assert not starts_before and starts_after,'Expected the first instrument sound only after Start/E.'
    assert all(v.get('content-encoding')=='br' for v in headers.values()),headers
    assert any(k.endswith('.wasm.br') and v.get('content-type')=='application/wasm' for k,v in headers.items()),headers
    print(json.dumps({k:report[k] for k in ['browser','version','readyMs','sample','canvasAt4K','audioBeforeClick','audioAfterClick','errors']},ensure_ascii=False),flush=True)
    browser.close()
