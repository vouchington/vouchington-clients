#!/usr/bin/env python3
"""Transport only the Swift UI test bundle, resources, and SwiftPM test metadata."""

import argparse
import hashlib
import json
import os
import platform
import posixpath
import subprocess
import tarfile
from pathlib import Path


DEBUG = ".build/arm64-apple-macosx/debug"
TEST_BUNDLE = f"{DEBUG}/VouchaUIPackageTests.xctest"
TEST_BINARY = f"{TEST_BUNDLE}/Contents/MacOS/VouchaUIPackageTests"
METADATA = (
    ".build/build.db",
    ".build/debug.yaml",
    ".build/workspace-state.json",
    ".build/debug",
    f"{DEBUG}/description.json",
    f"{DEBUG}/plugin-tools-description.json",
    f"{DEBUG}/VouchaUIPackageTests.product",
    f"{DEBUG}/VouchaUIPackageTests.derived",
)


def sha(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def command(*args: str) -> str:
    return subprocess.check_output(args, text=True).strip()


def identity(package: Path) -> dict[str, str]:
    return {
        "run": os.environ["GITHUB_RUN_ID"],
        "attempt": os.environ["GITHUB_RUN_ATTEMPT"],
        "revision": command("git", "-C", str(package), "rev-parse", "HEAD"),
        "xcode": command("xcodebuild", "-version"),
        "sdk": command("xcrun", "--sdk", "macosx", "--show-sdk-version"),
        "arch": platform.machine(),
        "resolved": sha(package / "Package.resolved"),
        "package": str(package),
    }


def selected(package: Path) -> list[str]:
    debug = package / DEBUG
    description = json.loads((debug / "description.json").read_text())
    expected_binary = str(package / TEST_BINARY)
    products = description.get("builtTestProducts")
    if products != [{"binaryPath": expected_binary, "packagePath": str(package), "productName": "VouchaUIPackageTests"}]:
        raise ValueError("SwiftPM test product metadata does not identify the exact UI bundle")
    if not (package / TEST_BINARY).is_file():
        raise ValueError("Missing compiled UI test executable")
    bundles = sorted(path.relative_to(package).as_posix() for path in debug.glob("*.bundle"))
    if f"{DEBUG}/VouchaUI_VouchaLocalization.bundle" not in bundles:
        raise ValueError("Missing UI localization resource bundle")
    paths = [TEST_BUNDLE, *bundles, *METADATA]
    if any(not (package / path).exists() for path in paths):
        raise ValueError("Missing SwiftPM test product or metadata")
    return paths


def resolve_link(name: str, entries: dict[str, tarfile.TarInfo]) -> None:
    link = entries[name]
    pending = link.linkname.split("/")
    stack = name.split("/")[:-1] if link.issym() else []
    hops = 0
    while pending:
        part = pending.pop(0)
        if part in ("", "."):
            continue
        if part == "..":
            if len(stack) <= 1:
                raise ValueError(f"Escaping Swift UI artifact link: {name}")
            stack.pop()
            continue
        stack.append(part)
        target = entries.get("/".join(stack))
        if target is not None and (target.issym() or target.islnk()):
            hops += 1
            if hops > len(entries):
                raise ValueError(f"Cyclic Swift UI artifact link: {name}")
            if target.islnk():
                stack = []
            else:
                stack.pop()
            pending = target.linkname.split("/") + pending
    if not "/".join(stack).startswith(".build/"):
        raise ValueError(f"Escaping Swift UI artifact link: {name}")


def validate(archive: Path, paths: list[str]) -> None:
    roots = tuple(paths)
    seen: dict[str, tarfile.TarInfo] = {}
    with tarfile.open(archive, "r:gz") as stream:
        for member in stream:
            name = member.name.rstrip("/")
            parts = name.split("/")
            if (not name.startswith(".build/") or any(part in ("", ".", "..") for part in parts)
                    or posixpath.normpath(name) != name
                    or not any(name == root or name.startswith(f"{root}/") for root in roots)):
                raise ValueError(f"Unsafe Swift UI artifact path: {member.name}")
            if name in seen:
                raise ValueError(f"Duplicate Swift UI artifact path: {name}")
            if any((ancestor := seen.get("/".join(parts[:index]))) is not None and not ancestor.isdir()
                   for index in range(1, len(parts))):
                raise ValueError(f"Swift UI artifact path descends through a link or file: {name}")
            if not member.isdir() and any(path.startswith(f"{name}/") for path in seen):
                raise ValueError(f"Swift UI artifact path replaces a directory: {name}")
            if member.issym() or member.islnk():
                if not member.linkname or member.linkname.startswith("/"):
                    raise ValueError(f"Unsafe Swift UI artifact link: {name}")
            elif not (member.isfile() or member.isdir()):
                raise ValueError(f"Unsupported Swift UI artifact entry: {name}")
            seen[name] = member
    if any(root not in seen for root in roots):
        raise ValueError("Swift UI artifact is missing a selected product")
    for name, member in seen.items():
        if member.issym() or member.islnk():
            resolve_link(name, seen)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("mode", choices=("create", "verify"))
    parser.add_argument("package", type=Path)
    parser.add_argument("archive", type=Path)
    parser.add_argument("manifest", type=Path)
    parser.add_argument("--expected-revision")
    args = parser.parse_args()
    package = args.package.resolve()
    current = identity(package)
    if args.expected_revision and current["revision"] != args.expected_revision:
        raise ValueError("Swift UI candidate revision differs from verified handoff")
    if current["arch"] != "arm64":
        raise ValueError("Swift UI test products require the hosted arm64 macOS runner")
    if args.mode == "create":
        paths = selected(package)
        with tarfile.open(args.archive, "w:gz") as stream:
            for path in paths:
                stream.add(package / path, arcname=path)
        validate(args.archive, paths)
        args.manifest.write_text(json.dumps({**current, "paths": paths, "archiveSha256": sha(args.archive)}, sort_keys=True))
    else:
        recorded = json.loads(args.manifest.read_text())
        if any(recorded.get(key) != value for key, value in current.items()):
            raise ValueError("Swift UI artifact identity differs from this run and toolchain")
        if recorded.get("archiveSha256") != sha(args.archive):
            raise ValueError("Swift UI artifact checksum mismatch")
        paths = recorded.get("paths")
        if not isinstance(paths, list) or not paths or any(not isinstance(path, str) for path in paths):
            raise ValueError("Invalid Swift UI artifact product list")
        validate(args.archive, paths)
        with tarfile.open(args.archive, "r:gz") as stream:
            stream.extractall(package)
        if selected(package) != paths:
            raise ValueError("Restored Swift UI test products differ from manifest")


if __name__ == "__main__":
    main()
