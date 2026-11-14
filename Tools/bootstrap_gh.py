"""Download official GitHub CLI locally; no installer or global changes."""
from pathlib import Path
import hashlib
import json
import urllib.request
import zipfile

ROOT=Path(__file__).resolve().parents[1]
def get(url):
    req=urllib.request.Request(url,headers={'User-Agent':'ABNF-SpaceApps-Planning'})
    with urllib.request.urlopen(req,timeout=60) as r:return r.read()

def main():
    release=json.loads(get('https://api.github.com/repos/cli/cli/releases/latest'))
    asset=next(x for x in release['assets'] if x['name'].endswith('_windows_amd64.zip'))
    url=asset['browser_download_url']
    if not url.startswith('https://github.com/cli/cli/releases/download/'):
        raise RuntimeError('Download source is not the official GitHub CLI repository.')
    data=get(url);sha=hashlib.sha256(data).hexdigest()
    if asset.get('digest') and asset['digest']!='sha256:'+sha:
        raise RuntimeError('Official asset SHA-256 mismatch.')
    target=ROOT/'Tools/Runtime/gh';target.mkdir(parents=True,exist_ok=True)
    archive=target/'official-gh.zip';archive.write_bytes(data)
    with zipfile.ZipFile(archive) as z:
        for name in z.namelist():
            resolved=(target/name).resolve()
            if not resolved.is_relative_to(target.resolve()):raise RuntimeError('Unsafe ZIP path.')
        z.extractall(target)
    (ROOT/'QA/Planning/github-cli.json').write_text(json.dumps(dict(version=release['tag_name'],url=url,sha256=sha,
        executable='Tools/Runtime/gh/bin/gh.exe',note='Official GitHub CLI, local untracked runtime, no global install.'),indent=2)+'\n',encoding='utf-8')
    print(release['tag_name'],sha,'official local CLI prepared.')

if __name__=='__main__':main()
