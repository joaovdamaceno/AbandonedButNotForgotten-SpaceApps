"""Local-only SUR-011 preview host: Brotli headers and responsive diagnostic HTML.

Does not modify the Unity build files or publish anything remotely.
"""
import argparse
import io
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import urlsplit

STYLE = """<style>
html,body { margin:0; background:#080c13; }
#unity-container.unity-desktop { position:relative; left:auto; top:auto; transform:none; margin:0 auto; width:min(100%,1280px,calc((100vh - 38px)*16/9)); }
#unity-fullscreen-container { width:100%!important; height:auto!important; }
#unity-canvas { position:relative!important; display:block; width:100%!important; height:auto!important; aspect-ratio:16/9; }
#unity-footer { position:relative!important; height:38px; background:#eee9dd; }
#sur011errors { position:relative; color:#fca; white-space:pre-wrap; }
</style><script>
window.addEventListener('error', function(e) {
 var p=document.getElementById('sur011errors');
 if(!p){p=document.createElement('pre');p.id='sur011errors';document.body.appendChild(p);}
 p.textContent += String(e.message)+'\\n';
});
</script>"""


class Handler(SimpleHTTPRequestHandler):
    def guess_type(self, path):
        if path.endswith('.br') or path.endswith('.gz'):
            path = path.rsplit('.', 1)[0]
        if path.endswith('.wasm'):
            return 'application/wasm'
        if path.endswith('.js'):
            return 'application/javascript'
        return super().guess_type(path)

    def end_headers(self):
        path = urlsplit(self.path).path
        if path.endswith('.br'):
            self.send_header('Content-Encoding', 'br')
        elif path.endswith('.gz'):
            self.send_header('Content-Encoding', 'gzip')
        self.send_header('Cache-Control', 'no-store')
        super().end_headers()

    def send_head(self):
        if urlsplit(self.path).path in ('/', '/index.html'):
            page = (Path(self.directory) / 'index.html').read_text(encoding='utf-8')
            page = page.replace('width=960', 'width=1280').replace('height=600', 'height=720')
            page = page.replace('width="960"', 'width="1280"').replace('height="600"', 'height="720"')
            data = page.replace('</head>', STYLE + '</head>').encode('utf-8')
            self.send_response(200)
            self.send_header('Content-Type', 'text/html; charset=utf-8')
            self.send_header('Content-Length', str(len(data)))
            self.end_headers()
            return io.BytesIO(data)
        return super().send_head()


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--root', default='Builds/SUR011Web')
    parser.add_argument('--port', type=int, default=8751)
    args = parser.parse_args()
    root = Path(args.root).resolve()
    if not (root / 'index.html').is_file():
        raise SystemExit('Build index.html not found; run CORE-006 with the SUR-011 preview scene first.')
    server = ThreadingHTTPServer(('127.0.0.1', args.port), lambda *a, **kw: Handler(*a, directory=str(root), **kw))
    print(f'SUR-011 local preview: http://127.0.0.1:{args.port}/?sur011qa=1', flush=True)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        server.server_close()
