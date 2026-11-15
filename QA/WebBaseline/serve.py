"""Local-only static host with native Brotli MIME/encoding headers. No upload API."""
from http.server import ThreadingHTTPServer, SimpleHTTPRequestHandler
from pathlib import Path
import argparse

parser = argparse.ArgumentParser()
parser.add_argument('--root', default='Builds/WebBaseline')
parser.add_argument('--port', type=int, default=8866)
args = parser.parse_args()
root = Path(args.root).resolve()

class Handler(SimpleHTTPRequestHandler):
    def __init__(self, *a, **kw):
        super().__init__(*a, directory=str(root), **kw)
    def guess_type(self, path):
        if path.endswith('.br'):
            path = path[:-3]
        if path.endswith('.wasm'): return 'application/wasm'
        if path.endswith('.js'): return 'application/javascript'
        if path.endswith('.data'): return 'application/octet-stream'
        return super().guess_type(path)
    def end_headers(self):
        if self.path.split('?', 1)[0].endswith('.br'):
            self.send_header('Content-Encoding', 'br')
        self.send_header('Cache-Control', 'no-store')
        super().end_headers()
    def log_message(self, fmt, *values):
        pass

print(f'Serving {root} at http://127.0.0.1:{args.port}', flush=True)
ThreadingHTTPServer(('127.0.0.1', args.port), Handler).serve_forever()
