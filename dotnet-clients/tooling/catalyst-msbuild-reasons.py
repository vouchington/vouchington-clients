#!/usr/bin/env python3
"""Report bounded MSBuild incremental reasons without retaining diagnostic logs."""

import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import tempfile
import threading


TARGET = re.compile(r'Target "(CoreCompile|XamlC|GenerateTargetFrameworkMonikerAttribute): \(TargetId:\d+\)".* from project "([^"]+)"')
NEWER = re.compile(r'Input file "([^"]+)" is newer than output file "([^"]+)"\.')
MISSING = re.compile(r'Output file "([^"]+)" does not exist\.')
TARGET_ACTION = re.compile(r'(?:Building|Skipping) target "(CoreCompile|XamlC|GenerateTargetFrameworkMonikerAttribute)"')
MAX_BYTES = 256 * 1024 * 1024
MAX_LINE = 16 * 1024
MAX_RECORDS = 8


def sdk_root():
    try:
        result = subprocess.run(["dotnet", "--list-sdks"], capture_output=True, text=True, check=True)
        for line in result.stdout.splitlines():
            match = re.fullmatch(r"10\.0\.401 \[([^\]]+)/sdk\]", line)
            if match:
                return Path(match.group(1)).resolve()
    except (OSError, subprocess.CalledProcessError):
        pass
    return Path("/nonexistent-dotnet-root")


def classify(path, project, root, sdk):
    raw = Path(path)
    resolved = (raw if raw.is_absolute() else Path(project).parent / raw).resolve(strict=False)
    roots = (
        (root, "repo"),
        (Path(os.environ.get("ArtifactsPath", "/nonexistent-artifacts")).resolve(), "artifacts"),
        (Path(os.environ.get("NUGET_PACKAGES", root / ".nuget/packages")).resolve(), "nuget"),
        (sdk, "sdk"),
        (Path(os.environ.get("RUNNER_TEMP", tempfile.gettempdir())).resolve(), "runner-temp"),
    )
    for base, label in roots:
        try:
            relative = resolved.relative_to(base)
        except ValueError:
            continue
        safe_path = f"{label}/{relative.as_posix()}"
        if len(safe_path) > 256:
            digest = hashlib.sha256(os.fsencode(resolved)).hexdigest()[:16]
            safe_path = f"{label}/sha256:{digest}"
        return resolved, safe_path, True
    digest = hashlib.sha256(os.fsencode(resolved)).hexdigest()[:16]
    return resolved, f"other/sha256:{digest}", False


def stat(path, known):
    if not known:
        return None
    try:
        info = path.stat()
    except OSError:
        return None
    return {"mtime_ns": info.st_mtime_ns, "size": info.st_size}


class ReasonParser:
    def __init__(self, root, sdk):
        self.root = root.resolve()
        self.sdk = sdk.resolve()
        self.buffer = bytearray()
        self.bytes_seen = 0
        self.lines_after_target = 0
        self.context = None
        self.records = []
        self.truncated = False
        self.discard_line = False

    def feed(self, data):
        self.bytes_seen += len(data)
        if self.bytes_seen > MAX_BYTES:
            self.truncated = True
            return
        segments = data.split(b"\n")
        for index, segment in enumerate(segments):
            if not self.discard_line:
                if len(self.buffer) + len(segment) <= MAX_LINE:
                    self.buffer.extend(segment)
                else:
                    self.buffer.clear()
                    self.discard_line = True
                    self.context = None
            if index < len(segments) - 1:
                if not self.discard_line:
                    self.line(self.buffer.decode("utf-8", "replace"))
                self.buffer.clear()
                self.discard_line = False

    def line(self, line):
        match = TARGET.search(line)
        if match:
            self.context = (match.group(1), match.group(2))
            self.lines_after_target = 0
            return
        if self.context is None:
            return
        self.lines_after_target += 1
        if self.lines_after_target > 16:
            self.context = None
            return
        action = TARGET_ACTION.search(line)
        if action and action.group(1) != self.context[0]:
            self.context = None
            return
        if len(self.records) >= MAX_RECORDS:
            return
        newer = NEWER.search(line)
        missing = MISSING.search(line)
        _, project_label, _ = classify(self.context[1], self.context[1], self.root, self.sdk)
        if newer:
            input_path, input_label, input_known = classify(newer.group(1), self.context[1], self.root, self.sdk)
            output_path, output_label, output_known = classify(newer.group(2), self.context[1], self.root, self.sdk)
            input_stat = stat(input_path, input_known)
            output_stat = stat(output_path, output_known)
            self.records.append({
                "target": self.context[0], "project": project_label, "reason": "newer-input", "input": input_label,
                "input_stat": input_stat, "output": output_label, "output_stat": output_stat,
                "input_newer_ns": input_stat["mtime_ns"] - output_stat["mtime_ns"]
                if input_stat is not None and output_stat is not None else None,
            })
            self.context = None
        elif missing:
            output_path, output_label, output_known = classify(missing.group(1), self.context[1], self.root, self.sdk)
            self.records.append({"target": self.context[0], "project": project_label, "reason": "missing-output",
                                 "output": output_label, "output_stat": stat(output_path, output_known)})
            self.context = None

    def summary(self):
        print(f"MSBuild incremental reasons: {len(self.records)} selected, {self.bytes_seen} diagnostic bytes streamed")
        for record in self.records:
            print(json.dumps(record, sort_keys=True))
        if self.truncated:
            print(f"MSBuild diagnostic parser capped at {MAX_BYTES} bytes")
        elif not self.records:
            print("No managed compile or target-framework attribute incremental reason found in diagnostic stream")


def run(root, command):
    parser = ReasonParser(root, sdk_root())
    with tempfile.TemporaryDirectory(prefix="catalyst-msbuild-reasons-", dir=os.environ.get("RUNNER_TEMP")) as temporary:
        fifo = Path(temporary) / "diagnostic.fifo"
        os.mkfifo(fifo, 0o600)
        started = threading.Event()
        done = threading.Event()
        failures = []

        def read_log():
            try:
                started.set()
                with fifo.open("rb", buffering=0) as stream:
                    while chunk := stream.read(65536):
                        if not failures:
                            try:
                                parser.feed(chunk)
                            except Exception as error:
                                failures.append(type(error).__name__)
            except Exception as error:
                failures.append(type(error).__name__)
            finally:
                done.set()

        reader = threading.Thread(target=read_log, daemon=True)
        reader.start()
        started.wait()
        process = subprocess.run(
            [*command, f"-flp:LogFile={fifo};Verbosity=diagnostic"],
            check=False,
        )
        if not done.wait(timeout=5):
            # If MSBuild failed before opening the logger, unblock the FIFO reader.
            try:
                writer = os.open(fifo, os.O_WRONLY | os.O_NONBLOCK)
                os.close(writer)
            except OSError as error:
                failures.append(type(error).__name__)
        reader.join(timeout=5)
        if reader.is_alive():
            failures.append("diagnostic reader did not finish")
        parser.summary()
        for failure in failures:
            print(f"MSBuild diagnostic reader error: {failure}", file=sys.stderr)
        return process.returncode


def main():
    arguments = argparse.ArgumentParser(description=__doc__)
    arguments.add_argument("--root", type=Path, required=True)
    arguments.add_argument("command", nargs=argparse.REMAINDER)
    options = arguments.parse_args()
    command = options.command
    if command and command[0] == "--":
        command = command[1:]
    if not command:
        arguments.error("a build command is required after --")
    return run(options.root, command)


if __name__ == "__main__":
    raise SystemExit(main())
