"""Targeted Web smoke of the unchanged flight route and new UI/material package."""
from pathlib import Path
import os,sys,json,time
root=Path(__file__).resolve().parents[2];sys.path.insert(0,str(root/'QA/Local/BrowserLib'));os.environ['PLAYWRIGHT_BROWSERS_PATH']=str(root/'QA/Local/Browsers')
from playwright.sync_api import sync_playwright
out=root/'QA/SUR-016'
with sync_playwright() as p:
 b=p.chromium.launch(headless=True,args=['--use-angle=d3d11']);page=b.new_page(viewport={'width':960,'height':600});errors=[];stages=[]
 def console(m):
  if m.type=='error':errors.append(m.text)
  if 'SURVEYOR_STEP ' in m.text:stages.append(m.text);print(m.text,flush=True)
 page.on('console',console);page.on('pageerror',lambda e:errors.append(str(e)))
 def wait(s):page.wait_for_timeout(int(s*1000))
 def stage(name):
  until=time.monotonic()+60
  while not any('SURVEYOR_STEP '+name+' ' in s for s in stages):
   assert time.monotonic()<until,(name,stages,errors);wait(.25)
 def hold(s):page.keyboard.down('e');wait(s);page.keyboard.up('e');wait(.2)
 page.goto('http://127.0.0.1:8868/');page.wait_for_function('window.unityInstance',timeout=120000);wait(2);page.locator('#game').click(position={'x':849,'y':488});stage('Launch');wait(4);page.keyboard.press('p');wait(.5);page.screenshot(path=str(out/'web-pause.png'));page.keyboard.press('p');wait(14);hold(2.8);stage('Cruise');wait(10.5);page.keyboard.press('h');wait(6.5);hold(3.3);stage('Descent');wait(14.3);hold(3.3);stage('FirstImage');hold(2.3);wait(7);page.screenshot(path=str(out/'web-image.png'));assert not errors,errors
 report={'browser':b.version,'coverage':'Dossier, actual flight orders, pause, first received TV image. Later geometry checked with GPU comparison and vertex audit; full route unchanged and covered by SUR-015.','stages':stages,'errors':errors};(out/'web-smoke.json').write_text(json.dumps(report,indent=2),encoding='utf-8');print(json.dumps(report),flush=True);b.close()
