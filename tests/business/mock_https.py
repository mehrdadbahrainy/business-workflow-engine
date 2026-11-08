"""Loopback-only HTTPS service to exercise the workflow HTTP integration worker.

For CI use only. Never bind to a public interface or use production credentials.
"""
import json
import ssl
import sys
import threading
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

attempts = {}
lock = threading.Lock()


class Handler(BaseHTTPRequestHandler):
    def do_POST(self):
        with lock:
            count = attempts.get(self.path, 0) + 1
            attempts[self.path] = count

        length = int(self.headers.get("Content-Length", "0"))
        body = self.rfile.read(length).decode("utf-8") if length else "{}"
        try:
            parsed = json.loads(body)
        except ValueError:
            parsed = {}

        if self.path.startswith("/retry/") and count == 1:
            status = 503
        elif self.path.startswith("/always-fail/"):
            status = 503
        else:
            status = 200

        result = {
            "status": "ok" if status == 200 else "temporary-failure",
            "attempt": count,
            "reference": parsed.get("reference"),
            "idempotencyKey": self.headers.get("Idempotency-Key"),
        }
        payload = json.dumps(result).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(payload)))
        self.end_headers()
        self.wfile.write(payload)

    def log_message(self, fmt, *args):
        sys.stderr.write("mock-https: " + fmt % args + "\n")


if __name__ == "__main__":
    if len(sys.argv) != 3:
        raise SystemExit("Usage: python mock_https.py CERT KEY")
    context = ssl.SSLContext(ssl.PROTOCOL_TLS_SERVER)
    context.load_cert_chain(sys.argv[1], sys.argv[2])
    server = ThreadingHTTPServer(("127.0.0.1", 443), Handler)
    server.socket = context.wrap_socket(server.socket, server_side=True)
    print("Mock HTTPS integration listening on localhost:443", flush=True)
    server.serve_forever()
