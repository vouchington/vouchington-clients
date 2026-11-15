#!/usr/bin/env python3
"""Install and spawn the iOS launch-smoke app, then stop it, inside a fixed budget."""

import subprocess
import sys
import time
from pathlib import Path

BUDGET_SECONDS = 150
RECOVERY_SECONDS = 60
BUNDLE_ID = "ai.voucha.ios"


def log(message: str) -> None:
    stamp = time.strftime("%H:%M:%S", time.gmtime())
    print(f"spawn-gate {message} {stamp}", flush=True)


def run(args: list[str], deadline: float, *, check: bool) -> subprocess.CompletedProcess[str]:
    remaining = deadline - time.monotonic()
    if remaining <= 0:
        raise subprocess.TimeoutExpired(args, BUDGET_SECONDS)
    return subprocess.run(
        args,
        check=check,
        capture_output=True,
        text=True,
        timeout=remaining,
    )


def reset_app(simulator_id: str, app: str) -> bool:
    """Kill a pre-launched app that ignored terminate by removing and reinstalling it."""
    deadline = time.monotonic() + RECOVERY_SECONDS
    log("reset")
    try:
        run(["xcrun", "simctl", "uninstall", simulator_id, BUNDLE_ID], deadline, check=True)
        run(["xcrun", "simctl", "install", simulator_id, app], deadline, check=True)
    except (subprocess.TimeoutExpired, subprocess.CalledProcessError):
        return False
    return True


def main() -> int:
    if len(sys.argv) != 3:
        print("usage: ios-launch-spawn-gate.py SIMULATOR_ID APP", file=sys.stderr)
        return 2
    simulator_id, app = sys.argv[1], sys.argv[2]
    deadline = time.monotonic() + BUDGET_SECONDS
    try:
        log("install")
        run(["xcrun", "simctl", "install", simulator_id, app], deadline, check=True)
        bundle_id = run(
            [
                "/usr/libexec/PlistBuddy",
                "-c",
                "Print :CFBundleIdentifier",
                str(Path(app) / "Info.plist"),
            ],
            deadline,
            check=True,
        ).stdout.strip()
        if bundle_id != BUNDLE_ID:
            print(
                f"::error::Refusing to launch an unexpected bundle id: {bundle_id}",
                file=sys.stderr,
            )
            return 1
        log("launch")
        launched = run(
            ["xcrun", "simctl", "launch", simulator_id, bundle_id],
            deadline,
            check=True,
        )
        fields = launched.stdout.split()
        pid = fields[-1] if fields else ""
        if not pid.isdigit():
            detail = launched.stdout.strip() or "no process id"
            print(f"::error::Installed app did not start a process: {detail}", file=sys.stderr)
            return 1
        log(f"pid={pid}")
        try:
            run(["xcrun", "simctl", "terminate", simulator_id, bundle_id], deadline, check=False)
        except subprocess.TimeoutExpired:
            log("terminate-budget-exhausted")
            if not reset_app(simulator_id, app):
                print(
                    "::error::Could not stop the pre-launched app; refusing to start the UI tests "
                    "against a leftover process",
                    file=sys.stderr,
                )
                return 1
        log("stopped")
    except subprocess.TimeoutExpired:
        print("::warning::Spawn gate exceeded 150s; continuing to xcodebuild", file=sys.stderr)
        return 0
    except subprocess.CalledProcessError as error:
        if error.stdout:
            print(error.stdout, end="")
        if error.stderr:
            print(error.stderr, file=sys.stderr, end="")
        return error.returncode or 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
