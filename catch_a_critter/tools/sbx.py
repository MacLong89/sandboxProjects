"""Tiny s&box editor MCP client for debugging. Usage:
  python sbx.py <tool> [json-args]
"""
import io
import json
import sys
import urllib.request

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

URL = "http://127.0.0.1:7269/mcp"


def call(name, arguments=None):
    payload = {
        "jsonrpc": "2.0", "id": 1, "method": "tools/call",
        "params": {"name": name, "arguments": arguments or {}},
    }
    req = urllib.request.Request(
        URL, json.dumps(payload).encode(),
        headers={"Content-Type": "application/json",
                 "Accept": "application/json, text/event-stream"})
    with urllib.request.urlopen(req, timeout=120) as r:
        body = r.read().decode("utf-8", errors="replace")
    d = json.loads(body)
    if "error" in d:
        return f"RPC ERROR: {d['error']}"
    out = []
    for c in d["result"].get("content", []):
        if c.get("type") == "text":
            out.append(c["text"])
    if d["result"].get("isError"):
        out.insert(0, "TOOL ERROR:")
    return "\n".join(out)


if __name__ == "__main__":
    tool = sys.argv[1]
    args = json.loads(sys.argv[2]) if len(sys.argv) > 2 else {}
    print(call(tool, args))
