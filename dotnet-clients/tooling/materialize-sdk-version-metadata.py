#!/usr/bin/env python3
"""Restore genuine metadata to byte-identical SDK and reference-pack trees."""

import hashlib
import os
import shutil
import stat
import sys
import tarfile
import tempfile
import time

VERSION = "10.0.401"
REF_PACK_VERSION = "10.0.12"
ROOT_PREFIXES = (f"sdk/{VERSION}", f"packs/Microsoft.NETCore.App.Ref/{REF_PACK_VERSION}")
MAX_ARCHIVE_BYTES = 300_000_000
MAX_EXPANDED_BYTES = 1_600_000_000
MAX_SELECTED_BYTES = 800_000_000
MAX_MEMBERS = 10_000
MAX_SELECTED = 5_000
DEADLINE_SECONDS = 180


def fail(message):
    raise ValueError(f"verified .NET SDK materialization: {message}")


def checked_time(deadline):
    if time.monotonic() > deadline:
        fail("deadline exceeded")


def sha256(path, expected_size, deadline):
    digest = hashlib.sha256()
    size = 0
    with open(path, "rb") as source:
        while chunk := source.read(64 * 1024):
            checked_time(deadline)
            size += len(chunk)
            if size > expected_size:
                fail(f"file grew while comparing: {path}")
            digest.update(chunk)
    if size != expected_size:
        fail(f"file shrank while comparing: {path}")
    return digest.digest()


def validate_archive(source, expected_sha512, deadline):
    before = os.fstat(source.fileno())
    if not stat.S_ISREG(before.st_mode) or before.st_size > MAX_ARCHIVE_BYTES:
        fail("archive missing or exceeds compressed bound")
    digest = hashlib.sha512()
    while chunk := source.read(1024 * 1024):
        checked_time(deadline)
        digest.update(chunk)
    after = os.fstat(source.fileno())
    if (before.st_dev, before.st_ino, before.st_size, before.st_mtime_ns) != (
            after.st_dev, after.st_ino, after.st_size, after.st_mtime_ns):
        fail("archive changed during SHA-512 validation")
    if digest.hexdigest() != expected_sha512:
        fail("official archive SHA-512 mismatch")
    source.seek(0)


def validate_members(tar, deadline):
    members = {prefix: [] for prefix in ROOT_PREFIXES}
    seen = set()
    seen_root = False
    member_count = 0
    expanded = selected_bytes = selected_count = 0
    for member in tar:
        checked_time(deadline)
        member_count += 1
        if member_count > MAX_MEMBERS:
            fail("archive member cap exceeded")
        raw = member.name
        if raw == ".":
            if seen_root or not member.isdir() or member.size != 0:
                fail("duplicate or unsafe archive root")
            seen_root = True
            continue
        path = raw[2:] if raw.startswith("./") else raw
        if (not raw.startswith("./") or not path or path.startswith("/") or "\\" in path or
                any(part in ("", ".", "..") for part in path.split("/")) or
                any(ord(char) < 32 or ord(char) == 127 for char in path) or
                path in seen or not (member.isfile() or member.isdir()) or
                member.mode & 0o7000 or member.mtime != int(member.mtime)):
            fail(f"unsafe or duplicate archive member: {raw}")
        seen.add(path)
        expanded += member.size
        if expanded > MAX_EXPANDED_BYTES:
            fail("archive expanded-byte cap exceeded")
        for prefix in ROOT_PREFIXES:
            if path == prefix or path.startswith(prefix + "/"):
                if path == prefix and not member.isdir():
                    fail(f"archive root is not a directory: {prefix}")
                selected_count += 1
                selected_bytes += member.size
                if selected_count > MAX_SELECTED or selected_bytes > MAX_SELECTED_BYTES:
                    fail("selected SDK and reference-pack trees exceed bounds")
                members[prefix].append((path, member))
                break
    for prefix in ROOT_PREFIXES:
        if prefix not in seen or len(members[prefix]) < 2:
            fail(f"archive subtree missing: {prefix}")
    return members


def extract_selected(tar, members, prefix, stage, deadline):
    directories = []
    for path, member in members:
        checked_time(deadline)
        relative = path.removeprefix(prefix).lstrip("/")
        target = stage if not relative else os.path.join(stage, *relative.split("/"))
        if member.isdir():
            os.mkdir(target, 0o700)
            directories.append((target, member))
        else:
            if not os.path.isdir(os.path.dirname(target)):
                fail(f"SDK archive parent missing: {path}")
            source = tar.extractfile(member)
            if source is None:
                fail(f"SDK archive file missing: {path}")
            size = 0
            with open(target, "xb") as output:
                while chunk := source.read(64 * 1024):
                    checked_time(deadline)
                    size += len(chunk)
                    if size > member.size:
                        fail(f"SDK archive file grew: {path}")
                    output.write(chunk)
            if size != member.size:
                fail(f"SDK archive file shrank: {path}")
            os.chmod(target, member.mode & 0o777)
            ns = int(member.mtime) * 1_000_000_000
            os.utime(target, ns=(ns, ns), follow_symlinks=False)
    for directory, member in reversed(directories):
        os.chmod(directory, member.mode & 0o777)
        ns = int(member.mtime) * 1_000_000_000
        os.utime(directory, ns=(ns, ns), follow_symlinks=False)


def tree_entries(root, deadline):
    result = {}
    pending = [("", root)]
    total = 0
    while pending:
        checked_time(deadline)
        path, absolute = pending.pop()
        info = os.lstat(absolute)
        if stat.S_ISDIR(info.st_mode):
            result[path] = ("directory", info.st_mode & 0o777, 0, None)
            with os.scandir(absolute) as iterator:
                for child in iterator:
                    if len(result) + len(pending) >= MAX_SELECTED:
                        fail("SDK tree pending-member cap exceeded")
                    relative = f"{path}/{child.name}" if path else child.name
                    pending.append((relative, child.path))
        elif stat.S_ISREG(info.st_mode):
            if info.st_size > MAX_SELECTED_BYTES:
                fail("installed SDK file exceeds bound")
            total += info.st_size
            if total > MAX_SELECTED_BYTES:
                fail("SDK tree total file bytes exceed bound")
            result[path] = ("file", info.st_mode & 0o777, info.st_size,
                            sha256(absolute, info.st_size, deadline))
        else:
            fail(f"SDK tree contains a link or special file: {path}")
        if len(result) > MAX_SELECTED:
            fail("SDK tree member cap exceeded")
    return result


class RollbackFailed(Exception):
    """The original trees remain in the private staging directory."""


def replace_verified_roots(roots, deadline):
    changed = []
    try:
        for _, target, stage, backup in roots:
            checked_time(deadline)
            os.rename(target, backup)
            changed.append([target, stage, backup, False])
            os.rename(stage, target)
            changed[-1][3] = True
    except Exception as original:
        rollback_errors = []
        for target, stage, backup, installed in reversed(changed):
            try:
                if installed:
                    os.rename(target, stage)
                os.rename(backup, target)
            except Exception as error:
                rollback_errors.append(error)
        if rollback_errors:
            raise RollbackFailed("SDK/reference-pack swap rollback failed; private backups retained") from original
        raise


def main():
    if len(sys.argv) != 4:
        fail("expected installed root, archive, and release SHA-512")
    root_arg, archive, expected_sha512 = sys.argv[1:]
    if len(expected_sha512) != 128 or any(c not in "0123456789abcdef" for c in expected_sha512):
        fail("invalid release SHA-512")
    deadline = time.monotonic() + DEADLINE_SECONDS
    root = os.path.realpath(root_arg)
    if not os.path.isdir(root):
        fail("installed .NET root is unsafe or missing")
    targets = []
    for prefix in ROOT_PREFIXES:
        current = root
        for component in prefix.split("/"):
            current = os.path.join(current, component)
            if os.path.islink(current) or not os.path.isdir(current):
                fail(f"installed SDK/reference-pack path is unsafe or missing: {prefix}")
        targets.append(current)
    if any(os.stat(target).st_dev != os.stat(root).st_dev for target in targets):
        fail("installed SDK/reference-pack roots must share the staging filesystem")
    archive_fd = os.open(archive, os.O_RDONLY | os.O_NOFOLLOW)
    with os.fdopen(archive_fd, "rb") as source:
        validate_archive(source, expected_sha512, deadline)
        tar = tarfile.open(fileobj=source, mode="r:gz")

        members = validate_members(tar, deadline)
        temp = tempfile.mkdtemp(prefix=".verified-sdk-", dir=root)
        roots = [(prefix, target, os.path.join(temp, f"new-{index}"),
                  os.path.join(temp, f"old-{index}"))
                 for index, (prefix, target) in enumerate(zip(ROOT_PREFIXES, targets))]
        rollback_safe = True
        try:
            for prefix, _, stage, _ in roots:
                extract_selected(tar, members[prefix], prefix, stage, deadline)
            counts = []
            for prefix, target, stage, _ in roots:
                staged = tree_entries(stage, deadline)
                installed = tree_entries(target, deadline)
                if staged != installed:
                    fail(f"installed files/types/bytes differ from official archive: {prefix}")
                counts.append(len(staged))
            replace_verified_roots(roots, deadline)
            print(f"verified .NET SDK {VERSION} and reference pack {REF_PACK_VERSION}: "
                  f"{sum(counts)} members, official archive metadata preserved")
        except RollbackFailed:
            rollback_safe = False
            raise
        finally:
            if rollback_safe:
                shutil.rmtree(temp)


if __name__ == "__main__":
    main()
