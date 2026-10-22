#!/usr/bin/env python3
"""Preserve verified candidate input bytes and their actual producer modification times."""

import hashlib
import os
from pathlib import Path, PurePosixPath
import stat
import subprocess
import sys
import tarfile

MAX_FILES = 3000
MAX_BYTES = 32 * 1024 * 1024
MAX_ARCHIVE_BYTES = 8 * 1024 * 1024
SCOPES = ("dotnet-clients", ".editorconfig", "global.json")
EXCLUDED_PARTS = {".git", ".nuget", "node_modules", "obj", "bin", "TestResults"}


def fail(message: str) -> None:
    raise SystemExit(message)


def git(root: Path, *args: str) -> bytes:
    return subprocess.check_output(("git", "-C", str(root), *args))


def tracked_paths(root: Path) -> list[str]:
    raw = git(root, "ls-files", "-z", "--", *SCOPES)
    paths = [os.fsdecode(part) for part in raw.split(b"\0") if part]
    if not 0 < len(paths) <= MAX_FILES:
        fail("invalid source member count")
    if ".editorconfig" not in paths or "global.json" not in paths:
        fail("missing root compile configuration")
    if len({path.casefold() for path in paths}) != len(paths):
        fail("ambiguous case-folded source paths")
    for path in paths:
        parts = PurePosixPath(path).parts
        if not parts or path.startswith("/") or ".." in parts or EXCLUDED_PARTS.intersection(parts):
            fail("unsafe source path")
        if path not in SCOPES[1:] and parts[0] != "dotnet-clients":
            fail("source path outside allowed scope")
        current = root
        for part in parts:
            current = current / part
            if current.is_symlink():
                fail("symlink in candidate source path")
        if not current.is_file() or not stat.S_ISREG(current.stat().st_mode):
            fail("missing or special candidate source file")
    return paths


def digest(data: bytes) -> bytes:
    return hashlib.sha256(data).digest()


def main() -> None:
    if len(sys.argv) != 5 or sys.argv[1] not in ("pack", "overlay"):
        fail("usage: catalyst-source-snapshot.py pack|overlay CANDIDATE_ROOT ARCHIVE REVISION")
    mode, root_arg, archive_arg, revision = sys.argv[1:]
    root = Path(root_arg).resolve(strict=True)
    archive = Path(archive_arg)
    if git(root, "rev-parse", "HEAD").decode().strip() != revision:
        fail("candidate revision does not match exact run")
    if subprocess.run(("git", "-C", str(root), "diff", "--quiet", "HEAD", "--", *SCOPES), check=False).returncode:
        fail("candidate source differs from its verified revision")
    paths = tracked_paths(root)
    total = sum((root / path).stat().st_size for path in paths)
    if total > MAX_BYTES:
        fail("source snapshot exceeds unpacked size bound")

    if mode == "pack":
        with tarfile.open(archive, "w:gz", format=tarfile.PAX_FORMAT) as tar:
            for path in paths:
                tar.add(root / path, arcname=path, recursive=False)
        if archive.stat().st_size > MAX_ARCHIVE_BYTES:
            fail("source snapshot exceeds compressed size bound")
        print(f"source snapshot: {len(paths)} files, {total} bytes, {archive.stat().st_size} compressed bytes")
        return

    if archive.is_symlink() or not archive.is_file() or archive.stat().st_size > MAX_ARCHIVE_BYTES:
        fail("missing, linked, or oversized source archive")
    with tarfile.open(archive, "r:gz") as tar:
        members = tar.getmembers()
        if len(members) != len(paths) or [member.name for member in members] != paths:
            fail("source archive has missing, extra, or reordered members")
        if sum(member.size for member in members) != total or sum(member.size for member in members) > MAX_BYTES:
            fail("source archive has unexpected unpacked size")
        payloads = []
        for member, path in zip(members, paths, strict=True):
            if not member.isfile() or member.name != path:
                fail("source archive contains a link or special member")
            source = tar.extractfile(member)
            if source is None:
                fail("source archive member cannot be read")
            data = source.read(MAX_BYTES + 1)
            if len(data) != member.size or digest(data) != digest((root / path).read_bytes()):
                fail("source archive differs from independent candidate checkout")
            payloads.append((root / path, data, member.mtime))

    # All paths and bytes were checked before changing any candidate modification time.
    for path, data, producer_mtime in payloads:
        path.write_bytes(data)
        os.utime(path, (producer_mtime, producer_mtime))
    if subprocess.run(("git", "-C", str(root), "diff", "--quiet", "HEAD", "--", *SCOPES), check=False).returncode:
        fail("source overlay changed candidate content")
    print(f"verified source snapshot: {len(paths)} byte-identical files, producer mtimes restored")


if __name__ == "__main__":
    main()
