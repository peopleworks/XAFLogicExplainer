"""Speak MCP to a server over stdio and check that it answers.

A server that builds is not a server that runs. This performs the exchange every MCP client
performs on connect -- initialize, tools/list, one real tool call -- and fails loudly if any of it
is missing, which is also the shape of the automated check MCP directories run against a container.

    python .github/scripts/mcp-handshake.py docker run --rm -i xaflogic-mcp

Anything after the script name is the command that launches the server.
"""
import json
import subprocess
import sys
import threading

# Long enough for a cold container and a Roslyn pass over a fourteen-entity application; short
# enough that a hung server fails the job instead of occupying a runner for six hours.
TIMEOUT_SECONDS = 120

EXPECTED_TOOLS = {
    "xaf_overview", "xaf_search", "xaf_entity", "xaf_controller", "xaf_rules",
    "xaf_model", "xaf_editors", "xaf_migrations", "xaf_refresh",
}


def main(command: list[str]) -> int:
    if not command:
        print("usage: mcp-handshake.py <command to launch the server>", file=sys.stderr)
        return 2

    proc = subprocess.Popen(command, stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                            stderr=subprocess.PIPE, text=True, bufsize=1)
    stdin, stdout, stderr = proc.stdin, proc.stdout, proc.stderr
    if stdin is None or stdout is None or stderr is None:
        print("could not open pipes to the server", file=sys.stderr)
        return 2

    # The server's own diagnostics go to stderr. Draining it in the background keeps a chatty
    # server from filling the pipe and blocking on a write nobody is reading.
    diagnostics: list[str] = []
    threading.Thread(target=lambda: diagnostics.extend(stderr), daemon=True).start()

    timer = threading.Timer(TIMEOUT_SECONDS, proc.kill)
    timer.start()
    try:
        return exchange(stdin, stdout, diagnostics)
    finally:
        timer.cancel()
        proc.kill()


def exchange(stdin, stdout, diagnostics: list[str]) -> int:
    def send(message: dict) -> None:
        stdin.write(json.dumps(message) + "\n")
        stdin.flush()

    def receive() -> dict:
        for line in stdout:
            # Only JSON-RPC belongs on stdout, but a stray banner from a dependency should be a
            # skipped line rather than a crash with an unreadable message.
            if line.lstrip().startswith("{"):
                return json.loads(line)
        raise SystemExit(fail("the server closed stdout without answering", diagnostics))

    send({"jsonrpc": "2.0", "id": 1, "method": "initialize",
          "params": {"protocolVersion": "2025-06-18", "capabilities": {},
                     "clientInfo": {"name": "ci-handshake", "version": "1"}}})
    info = receive()["result"]["serverInfo"]
    print(f"initialize   {info['name']} {info['version']}")

    send({"jsonrpc": "2.0", "method": "notifications/initialized"})

    send({"jsonrpc": "2.0", "id": 2, "method": "tools/list"})
    found = {tool["name"] for tool in receive()["result"]["tools"]}
    print(f"tools/list   {len(found)}: {' '.join(sorted(found))}")

    if missing := EXPECTED_TOOLS - found:
        return fail(f"missing tools: {' '.join(sorted(missing))}", diagnostics)

    # A server can list tools it cannot run. Calling one proves extraction actually happened.
    send({"jsonrpc": "2.0", "id": 3, "method": "tools/call",
          "params": {"name": "xaf_overview", "arguments": {}}})
    result = receive()["result"]
    text = result["content"][0]["text"]
    print(f"xaf_overview {len(text)} chars, first line: {text.splitlines()[0][:70]}")

    if result.get("isError") or len(text) < 200:
        return fail("xaf_overview answered with an error or with nothing", diagnostics)

    print("handshake ok")
    return 0


def fail(reason: str, diagnostics: list[str]) -> int:
    print(f"handshake failed: {reason}", file=sys.stderr)
    if diagnostics:
        print("--- server stderr ---", file=sys.stderr)
        sys.stderr.writelines(diagnostics[-40:])
    return 1


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
