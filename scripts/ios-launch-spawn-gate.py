#!/usr/bin/env python3
"""Install and spawn the iOS launch-smoke app, then stop it, with a budget per operation."""

import subprocess
import sys
import time
from pathlib import Path

# Install and launch share one budget; p95 is 52 s and 78 s, and a gate that runs past it
# still lets xcodebuild start.
BUDGET_SECONDS = 150
# `simctl terminate` has its own budget. Measured over 140 passing gates it takes p50 4 s,
# p95 37 s, max 104 s; when it hangs it does not return, so 60 s separates the two.
TERMINATE_SECONDS = 60
# One shared budget for the uninstall and install that replace a process terminate left alive.
# Runs 45060434211 (iPhone sign-in) and 45082704348 (iPad sign-in, ejected a queued PR) stalled the
# Simulator for about 2 minutes and then recovered, so 60 s failed both; 120 s covers that stall.
# Worst case: 150 install and launch + 60 terminate + 15 liveness + 120 recovery = 345 s, which
# leaves 75 s of the 420 s iPhone step cap and 195 s of the 540 s iPad cap for xcodebuild.
RECOVERY_SECONDS = 120
# Its own small budget: a stalled check counts as "still alive" and keeps the gate failing closed.
LIVENESS_SECONDS = 15
BUNDLE_ID = "ai.voucha.ios"
LIVENESS_MARKER = f"UIKitApplication:{BUNDLE_ID}"


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


def app_alive(simulator_id: str) -> bool:
    """Whether the app process is still running; a stalled or failed check counts as alive."""
    started = time.monotonic()
    try:
        listed = run(
            ["xcrun", "simctl", "spawn", simulator_id, "launchctl", "list"],
            time.monotonic() + LIVENESS_SECONDS,
            check=True,
        )
    except subprocess.TimeoutExpired:
        log(f"liveness-timeout after {time.monotonic() - started:.0f}s; assuming alive")
        return True
    except subprocess.CalledProcessError as error:
        log(f"liveness-failed exit={error.returncode}; assuming alive")
        return True
    return LIVENESS_MARKER in listed.stdout


def reset_app(simulator_id: str, app: str) -> bool:
    """Kill a pre-launched app that ignored terminate by removing and reinstalling it."""
    deadline = time.monotonic() + RECOVERY_SECONDS
    log("reset")
    for step, args in (
        ("uninstall", ["xcrun", "simctl", "uninstall", simulator_id, BUNDLE_ID]),
        ("install", ["xcrun", "simctl", "install", simulator_id, app]),
    ):
        started = time.monotonic()
        try:
            run(args, deadline, check=True)
        except subprocess.TimeoutExpired:
            elapsed = time.monotonic() - started
            print(
                f"::warning::spawn-gate reset {step} timed out after {elapsed:.0f}s "
                f"of the {RECOVERY_SECONDS}s budget",
                flush=True,
            )
            return False
        except subprocess.CalledProcessError as error:
            elapsed = time.monotonic() - started
            detail = (error.stderr or "").strip() or "no stderr"
            print(
                f"::warning::spawn-gate reset {step} failed exit={error.returncode} "
                f"after {elapsed:.0f}s: {detail}",
                flush=True,
            )
            return False
        log(f"reset-{step} {time.monotonic() - started:.0f}s")
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
            run(
                ["xcrun", "simctl", "terminate", simulator_id, bundle_id],
                time.monotonic() + TERMINATE_SECONDS,
                check=False,
            )
        except subprocess.TimeoutExpired:
            log("terminate-budget-exhausted")
            if not app_alive(simulator_id):
                log("stopped-late")
                return 0
            if not reset_app(simulator_id, app):
                # Erasing the device would shut it down, and xcodebuild would then cold boot it
                # and repeat the data migration inside the same step cap. Fail closed instead.
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
