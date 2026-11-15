from pathlib import Path
import sys,os,json
root=Path(__file__).resolve().parents[2];sys.path.insert(0,str(root/'QA/Local/BrowserLib'));os.environ['PLAYWRIGHT_BROWSERS_PATH']=str(root/'QA/Local/Browsers')
from playwright.sync_api import sync_playwright
with sync_playwright() as p:
 browser=p.chromium.launch(headless=True,args=['--use-angle=d3d11']);context=browser.new_context(viewport={'width':1280,'height':720},device_scale_factor=1);page=context.new_page();page.bring_to_front();errors=[];stages=[]
 def console(m):
  if m.type=='error':errors.append(m.text)
  if 'SURVEYOR_STEP ' in m.text:stages.append(m.text)
 page.on('console',console);page.on('pageerror',lambda e:errors.append(str(e)));page.goto('http://127.0.0.1:8868/');page.wait_for_function('window.unityInstance',timeout=120000);page.wait_for_timeout(2000);print(page.evaluate('({focus:document.hasFocus(),visibility:document.visibilityState,rect:game.getBoundingClientRect().toJSON(),width:game.width,height:game.height,dpr:devicePixelRatio})'),flush=True);page.mouse.move(1180,175);page.wait_for_timeout(300)
 page.locator('#game').click(position={'x':1180,'y':175},delay=120);page.wait_for_timeout(600);page.screenshot(path=str(root/'QA/SUR-017/chrome-card-details.png'));page.locator('#game').click(position={'x':500,'y':367},delay=120);page.wait_for_timeout(400);page.mouse.move(650,450);page.mouse.wheel(0,230);page.wait_for_timeout(400);page.screenshot(path=str(root/'QA/SUR-017/chrome-card-open.png'))
 page.keyboard.press('Escape');page.wait_for_timeout(400);page.screenshot(path=str(root/'QA/SUR-017/chrome-card-return.png'));assert not errors and not stages,(errors,stages)
 report={'browser':browser.version,'coverage':'Real pointer click on Surveyor Details, click to expand optional card, scroll, Escape to archive. No chapter was started.','errors':errors,'chapterStages':stages};(root/'QA/SUR-017/card-web.json').write_text(json.dumps(report,indent=2),encoding='utf-8');print(json.dumps(report));browser.close()
