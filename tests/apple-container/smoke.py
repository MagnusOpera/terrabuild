"""Exercise the real Apple runtime through Terrabuild, including cancellation.

Run with make smoke-test-apple on an Apple silicon Mac with container running.
All workspaces are temporary; cleanup removes only containers created by this test.
"""

import json
import os
from pathlib import Path
import shutil
import signal
import subprocess
import sys
import tempfile
import time


def run(args, **kwargs):
    return subprocess.run(args, check=True, text=True, capture_output=True, **kwargs)


def wait_for(predicate, description, timeout=30):
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        if predicate():
            return
        time.sleep(0.2)
    raise AssertionError(f"Timed out waiting for {description}")


def main():
    binary = Path(sys.argv[1]).resolve()
    fixture = Path(__file__).resolve().parent
    run(["container", "list", "--quiet"])
    with tempfile.TemporaryDirectory(prefix="terrabuild apple ") as directory:
        workspace = Path(directory).resolve()
        profile = workspace / "home"
        profile.mkdir()
        shutil.copy(fixture / "WORKSPACE", workspace)
        for name in ["app one", "app two"]:
            project = workspace / name
            project.mkdir()
            for filename in ["PROJECT", "verify.sh"]:
                shutil.copy(fixture / filename, project)
        run(["git", "init", "-q"], cwd=workspace)
        run(["git", "add", "."], cwd=workspace)
        run(["git", "-c", "user.name=Smoke test", "-c", "user.email=smoke@example.invalid",
             "commit", "-qm", "Fixture"], cwd=workspace)
        env = dict(os.environ, HOME=str(profile), TB_APPLE_SAMPLE="$TERRABUILD_HOME/cache with spaces")
        command = ["dotnet", str(binary), "run"]
        options = ["--engine", "apple", "--local-only", "--force", "--debug", "--parallel", "2"]

        def execute(target):
            result = subprocess.run(command + [target] + options, cwd=workspace, env=env,
                                    text=True, capture_output=True, timeout=120)
            print(result.stdout)
            print(result.stderr, file=sys.stderr)
            return result

        def records():
            return list((profile / ".terrabuild" / "containers").glob("*.json"))

        def cleanup_records():
            for record in records():
                data = json.loads(record.read_text(encoding="utf-8-sig"))
                subprocess.run(["container", "rm", "-f", data["name"]], capture_output=True)

        try:
            assert execute("build").returncode == 0, "Apple build failed"
            for name in ["app one", "app two"]:
                assert (workspace / name / ".out/result with spaces.txt").read_text() == "apple container output\n"
            debug = json.loads((workspace / "terrabuild-debug.json").read_text())
            operations = [op for execution in debug["executions"] for op in execution["operations"]]
            assert len(operations) == 2
            assert all(op["command"] == "container" and op["exitCode"] == 0 for op in operations)
            wait_for(lambda: not records(), "successful container cleanup")

            assert execute("fail").returncode != 0, "Failed container reported success"
            debug = json.loads((workspace / "terrabuild-debug.json").read_text())
            operations = [op for execution in debug["executions"] for op in execution["operations"]]
            assert operations and all(op["exitCode"] == 7 for op in operations)
            wait_for(lambda: not records(), "failed container cleanup")

            with (workspace / "cancel.log").open("w") as log:
                process = subprocess.Popen(command + ["wait"] + options, cwd=workspace, env=env,
                                           stdout=log, stderr=log)
                try:
                    wait_for(lambda: all((workspace / name / ".out/started").exists()
                                         for name in ["app one", "app two"]), "both containers to start")
                    names = [json.loads(record.read_text(encoding="utf-8-sig"))["name"] for record in records()]
                    assert len(names) == 2
                    process.send_signal(signal.SIGINT)
                    process.wait(timeout=30)
                    assert process.returncode != 0
                    wait_for(lambda: not records(), "cancelled container cleanup")
                    remaining = run(["container", "list", "--all", "--quiet"]).stdout.splitlines()
                    assert not set(names).intersection(remaining), "Cancelled containers still exist"
                finally:
                    if process.poll() is None:
                        process.kill()
                        process.wait()
                    print((workspace / "cancel.log").read_text())
        finally:
            cleanup_records()
    print("Apple container smoke tests passed")


if __name__ == "__main__":
    main()
