#!/usr/bin/env python3
"""Transport only the Swift UI test bundle, resources, and SwiftPM test metadata."""

import argparse
import hashlib
import json
import os
import platform
import subprocess
import tarfile
import importlib.util
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


def validate(archive: Path, paths: list[str]) -> None:
    spec = importlib.util.spec_from_file_location(
        "native_test_tar", Path(__file__).with_name("verify-ios-test-tar.py")
    )
    if spec is None or spec.loader is None:
        raise ValueError("Native test archive validator is unavailable")
    validator = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(validator)
    validator.validate(str(archive), root=".build", allowed_paths=tuple(paths))


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
