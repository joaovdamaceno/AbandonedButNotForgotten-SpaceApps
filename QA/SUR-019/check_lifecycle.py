"""Two early real-UI restarts; this does not claim two complete novice playthroughs."""
import argparse, json, os, sys
from pathlib import Path
ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'QA/Local/BrowserLib'))
os.environ['PLAYWRIGHT_BROWSERS_PATH'] = str(ROOT / 'QA/Local/Browsers')
from playwright.sync_api import sync_playwright
ap = argparse.ArgumentParser(); ap.add_argument('--browser', default='chromium'); args = ap.parse_args()
out = ROOT / 'QA/SUR-019' / args.browser; out.mkdir(parents=True, exist_ok=True)
with sync_playwright() as p:
    options = {'headless': True}
    if args.browser == 'chromium': options['args'] = ['--use-angle=d3d11']
    browser = getattr(p, args.browser).launch(**options)
    context = browser.new_context(viewport={'width':1280, 'height':720})
    page = context.new_page(); messages = []; errors = []; metrics = []
    page.on('console', lambda m: messages.append(m.text))
    page.on('pageerror', lambda e: errors.append(str(e)))
    def wait(s): page.wait_for_timeout(int(s * 1000))
    def prologues(): return sum('SURVEYOR_DOC_STEP Prologue ' in m for m in messages)
    def until(predicate):
        for _ in range(240):
            if predicate(): return
            wait(.25)
        raise AssertionError(messages[-8:])
    def sample(): return page.evaluate('JSON.parse(JSON.stringify(unityInstance.GetMetricsInfo()))')
    try:
        page.goto('http://127.0.0.1:8868/'); page.wait_for_function('window.unityInstance', timeout=120000)
        until(lambda:any('SURVEYOR_ARCHIVE_READY' in m for m in messages));wait(2)
        page.screenshot(path=str(out/'lifecycle-start-archive.png'))
        page.mouse.click(1130,586); until(lambda:prologues()==1); wait(3)
        for index in range(2):
            page.mouse.click(1220,73); wait(.5)
            page.screenshot(path=str(out/f'lifecycle-{index}-pause.png'))
            page.mouse.click(640,441); wait(.15); page.mouse.click(640,441)
            until(lambda:prologues()==index+2); wait(4); metrics.append(sample())
        # An actual browser tab switch, with visibility recorded rather than assumed.
        other = context.new_page(); other.goto('about:blank'); other.bring_to_front(); wait(1)
        hidden = page.evaluate('document.hidden'); page.bring_to_front(); wait(1)
        page.screenshot(path=str(out/'lifecycle-focus.png'))
        # Re-open via P if the browser did not report a hidden tab in headless mode.
        if not hidden: page.keyboard.press('p'); wait(.3)
        before=sum('SURVEYOR_ARCHIVE_READY' in m for m in messages)
        page.mouse.click(640,497); wait(.15); page.mouse.click(640,497)
        until(lambda:sum('SURVEYOR_ARCHIVE_READY' in m for m in messages)==before+1);wait(1)
        page.set_viewport_size({'width':960,'height':600}); wait(.4)
        page.screenshot(path=str(out/'lifecycle-small-archive.png'))
        assert not errors, errors
        report={'browser':args.browser, 'restarts':2, 'prologues':prologues(), 'metricsAfterRestart':metrics,
                'tabReportedHidden':hidden, 'pageErrors':errors,
                'scope':'Two early UI restarts and a tab switch, not two complete chapters or proof of bounded long-term memory.'}
        (out/'lifecycle.json').write_text(json.dumps(report, indent=2), encoding='utf-8'); print(json.dumps(report), flush=True)
    finally: context.close(); browser.close()
