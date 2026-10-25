"""Original bounded pipe bridge inside one explicitly constrained Linux container. No host mounts or network."""
import json
import os
from pathlib import Path
import signal
import subprocess
import sys
import threading
import time

MAXIMUM = 16777216
child = None
stopping = threading.Event()
failed = threading.Event()


def number(name, minimum, maximum):
    value = int(os.environ[name])
    if not minimum <= value <= maximum:
        raise ValueError("Host extent")
    return value


def main():
    global child
    memory = number("FLUENT_PDF_MEMORY", 268435456, 2147483648)
    pids = number("FLUENT_PDF_PIDS", 8, 512)
    seconds = number("FLUENT_PDF_SECONDS", 1, 600)
    if sys.platform != "linux" or os.getuid() == 0:
        raise ValueError("Nonroot Linux container required")
    status = dict(line.split(":", 1) for line in Path("/proc/self/status").read_text().splitlines() if ":" in line)
    if int(status["CapEff"].strip(), 16) != 0 or status["NoNewPrivs"].strip() != "1" or status["Seccomp"].strip() != "2":
        raise ValueError("Required isolation unavailable")
    if {line.split(":")[0].strip() for line in Path("/proc/net/dev").read_text().splitlines()[2:]} != {"lo"}:
        raise ValueError("Offline namespace required")
    if not os.statvfs("/").f_flag & os.ST_RDONLY:
        raise ValueError("Read-only root required")
    cgroup = Path("/sys/fs/cgroup")
    if (cgroup / "memory.max").read_text().strip() != str(memory) or (cgroup / "pids.max").read_text().strip() != str(pids):
        raise ValueError("Exact cgroup v2 limits required")
    Path("/tmp/browser-home").mkdir(mode=0o700)
    profile = Path("/tmp/private-profile")
    profile.mkdir(mode=0o700)
    to_read, to_write = os.pipe()
    from_read, from_write = os.pipe()
    def descriptors():
        os.dup2(to_read, 3)
        os.dup2(from_write, 4)
        if from_write not in (3, 4): os.close(from_write)
    child = subprocess.Popen([
        "/usr/bin/chromium", "--headless", "--disable-gpu", "--renderer-process-limit=1", "--no-startup-window", "--no-first-run", "--no-default-browser-check",
        "--disable-background-networking", "--disable-component-update", "--disable-default-apps", "--disable-sync", "--no-pings",
        "--metrics-recording-only", "--disable-breakpad", "--disable-crash-reporter", "--host-resolver-rules=MAP * ~NOTFOUND",
        "--proxy-server=http://127.0.0.1:9", "--proxy-bypass-list=<-loopback>", "--remote-debugging-pipe", "--user-data-dir=" + str(profile)
    ], stdin=subprocess.DEVNULL, stdout=subprocess.DEVNULL, stderr=subprocess.PIPE, pass_fds=tuple(set((to_read, to_write, from_write, 3, 4))), preexec_fn=descriptors,
        start_new_session=True, env={"PATH": "/usr/bin:/bin", "LANG": "C.UTF-8", "HOME": "/tmp/browser-home", "TMPDIR": "/tmp", "FONTCONFIG_FILE": "/opt/fluent-pdf/browser/fontconfig.xml"})
    os.close(to_read)
    os.close(from_write)
    def input_loop():
        try:
            with os.fdopen(to_write, "wb", buffering=0) as output:
                while not stopping.is_set():
                    line = sys.stdin.buffer.readline(MAXIMUM + 2)
                    if not line: break
                    if not line.endswith(b"\n") or len(line) > MAXIMUM + 1 or b"\0" in line or b"\r" in line:
                        raise ValueError("Input extent")
                    frame = memoryview(line[:-1] + b"\0")
                    while frame:
                        written = output.write(frame)
                        if not written: raise ValueError("Input transport")
                        frame = frame[written:]
        except BaseException:
            failed.set()
        finally:
            stopping.set()
    def output_loop():
        try:
            with os.fdopen(from_read, "rb", buffering=0) as source:
                pending = bytearray()
                while not stopping.is_set():
                    chunk = source.read(65536)
                    if not chunk: break
                    if len(pending) + len(chunk) > MAXIMUM: raise ValueError("Output extent")
                    pending.extend(chunk)
                    while (at := pending.find(0)) >= 0:
                        if at == 0 or b"\n" in pending[:at] or b"\r" in pending[:at]: raise ValueError("Output framing")
                        sys.stdout.buffer.write(pending[:at] + b"\n")
                        sys.stdout.buffer.flush()
                        del pending[:at + 1]
        except BaseException:
            failed.set()
        finally:
            stopping.set()
    def errors():
        while child.stderr.read(1024): pass
    print(json.dumps({"fluentPdfBrowserHost": 1, "memory": memory, "pids": pids, "isolated": True}), flush=True)
    for action in (input_loop, output_loop, errors):
        threading.Thread(target=action, daemon=True).start()
    started = time.monotonic()
    while not stopping.wait(.01):
        if child.poll() is not None or time.monotonic() - started >= seconds:
            failed.set()
            break
    if failed.is_set(): raise ValueError("Private browser transport failed")


def terminate(*args):
    stopping.set()
    if child is not None:
        try: os.killpg(child.pid, signal.SIGKILL)
        except ProcessLookupError: pass


if __name__ == "__main__":
    signal.signal(signal.SIGTERM, terminate)
    signal.signal(signal.SIGINT, terminate)
    try:
        main()
    except BaseException:
        print("Private browser host prerequisite or transport failed.", file=sys.stderr)
        sys.exitcode = 78
    finally:
        terminate()
        if child is not None:
            try: child.wait(timeout=1)
            except subprocess.TimeoutExpired: sys.exitcode = 79
    sys.exit(getattr(sys, "exitcode", 0))
