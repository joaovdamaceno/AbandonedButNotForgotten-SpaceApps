"""Package the actual isolated build, with an auditable file inventory."""
from pathlib import Path
import hashlib, json, zipfile
ROOT = Path(__file__).resolve().parents[2]
source = ROOT / 'Builds/SurveyorWeb'
files = sorted(p for p in source.rglob('*') if p.is_file())
assert (source / 'index.html').is_file()
assert len(files) == 5, [str(p) for p in files]
assert all(p.stat().st_size > 0 for p in files)
assert not any(p.suffix.lower() in {'.mp4','.webm','.mov'} for p in files)
size = sum(p.stat().st_size for p in files)
assert size <= 25_000_000, size
output = ROOT / 'Builds/Surveyor3-itch.zip'
with zipfile.ZipFile(output, 'w', zipfile.ZIP_DEFLATED, compresslevel=1) as archive:
    for p in files: archive.write(p, p.relative_to(source).as_posix())
with zipfile.ZipFile(output) as archive:
    assert archive.testzip() is None
    assert 'index.html' in archive.namelist()
report = {'buildBytes':size, 'zipBytes':output.stat().st_size,
          'zipSha256':hashlib.sha256(output.read_bytes()).hexdigest(),
          'files':[{'path':p.relative_to(source).as_posix(),'bytes':p.stat().st_size,
                    'sha256':hashlib.sha256(p.read_bytes()).hexdigest()} for p in files],
          'hosting':'Serve .br as Content-Encoding: br with correct wasm/JS/octet-stream MIME. Actual itch.io upload not tested.'}
(ROOT / 'QA/SUR-018/package.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
print(json.dumps(report, indent=2))
