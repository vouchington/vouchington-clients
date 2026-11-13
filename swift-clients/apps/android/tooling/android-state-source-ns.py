#!/usr/bin/env python3
"""Validate every tracked source through no-follow fds, then restore exact mtimes."""

import hashlib
import json
import os
import re
import resource
import stat
import sys
import time

MAX_ENTRIES = 10_000
MAX_BYTES = 512 * 1024 * 1024
MAX_INPUT = 4 * 1024 * 1024
MAX_NS = 9_223_372_036_854_775_807
MAX_SOURCE_DIRECTORIES = 256
SOURCE_ROOT = "swift-clients/apps/android/Sources"
started = time.monotonic()
deadline = started + 60
phase_started = started
phase_times = {}
progress = {"phase": "startup", "files": 0, "directories": 0, "bytes": 0,
            "revalidatedFiles": 0, "applied": 0, "rolledBack": 0}


def report(phase):
    global phase_started
    now = time.monotonic()
    previous = progress["phase"]
    phase_times[previous] = round((now - phase_started) * 1000)
    event = dict(progress, phase=phase, elapsedMs=round((now - started) * 1000),
                 phaseMs=dict(phase_times))
    if phase == "failed":
        event["during"] = previous
    print("Android source metadata progress: " + json.dumps(event, separators=(",", ":")),
          file=sys.stderr, flush=True)
    progress["phase"] = phase
    phase_started = now


def checked_time():
    if time.monotonic() > deadline:
        raise ValueError("Android source metadata validation deadline exceeded")


class SourceFiles:
    """Reuse one no-follow parent during scanning, then revalidate all paths."""

    def __init__(self, root_fd):
        self.root_fd = root_fd
        self.parent_fd = None
        self.parent_path = None
        self.parents = {}

    def open(self, path):
        parent, name = path.rsplit("/", 1)
        if parent != self.parent_path:
            self.close()
            self.parent_fd = checked_directory(self.root_fd, parent)
            self.parent_path = parent
            info = os.fstat(self.parent_fd)
            identity = (info.st_dev, info.st_ino)
            record = self.parents.setdefault(parent, (identity, []))
            if record[0] != identity:
                raise ValueError("Android source parent changed during validation")
        fd = os.open(name, os.O_RDONLY | os.O_NOFOLLOW, dir_fd=self.parent_fd)
        if not stat.S_ISREG(os.fstat(fd).st_mode):
            os.close(fd)
            raise ValueError(f"Android source is not a regular file: {path}")
        return fd

    def remember(self, path, fd, info):
        self.parents[self.parent_path][1].append((path.rsplit("/", 1)[1], fd, info))

    def revalidate(self):
        self.close()
        for parent, (identity, entries) in self.parents.items():
            checked_time()
            directory = checked_directory(self.root_fd, parent)
            try:
                info = os.fstat(directory)
                if (info.st_dev, info.st_ino) != identity:
                    raise ValueError("Android source parent changed before replay")
                for name, fd, before in entries:
                    checked_time()
                    current = os.stat(name, dir_fd=directory, follow_symlinks=False)
                    retained = os.fstat(fd)
                    expected = self.fingerprint(before)
                    if self.fingerprint(current) != expected or self.fingerprint(retained) != expected:
                        raise ValueError("Android source file changed before replay")
                    progress["revalidatedFiles"] += 1
            finally:
                os.close(directory)

    @staticmethod
    def fingerprint(info):
        return (info.st_dev, info.st_ino, info.st_mode, info.st_size, info.st_mtime_ns)

    def close(self):
        if self.parent_fd is not None:
            os.close(self.parent_fd)
            self.parent_fd = None
            self.parent_path = None


def checked_directory(root_fd, path):
    directory = os.dup(root_fd)
    try:
        for part in path.split("/"):
            next_dir = os.open(part, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW, dir_fd=directory)
            os.close(directory)
            directory = next_dir
        return directory
    except Exception:
        os.close(directory)
        raise


def checked_path(path, previous):
    if (not isinstance(path, str) or not path.startswith("swift-clients/") or
            len(path) > 4096 or path <= previous or "\\" in path or
            any(part in ("", ".", "..", ".build", "node_modules") for part in path.split("/")) or
            any(ord(char) < 32 or ord(char) == 127 for char in path)):
        raise ValueError("Android source metadata path invalid")


def hash_fd(fd, size, path):
    actual = hashlib.sha256()
    read_bytes = 0
    while True:
        checked_time()
        chunk = os.read(fd, min(64 * 1024, size - read_bytes + 1))
        if not chunk:
            break
        read_bytes += len(chunk)
        if read_bytes > size:
            raise ValueError(f"Android source grew during validation: {path}")
        actual.update(chunk)
    if read_bytes != size:
        raise ValueError(f"Android source shrank during validation: {path}")
    return actual.hexdigest()


def checked_directory_member(entry):
    if (not isinstance(entry, dict) or set(entry) != {"path", "childrenSha256"} and
            set(entry) != {"path", "childrenSha256", "mode", "mtimeNs"}):
        raise ValueError("Android source directory metadata member invalid")
    path = entry["path"]
    checked_path(path, "")
    if path != SOURCE_ROOT and not path.startswith(SOURCE_ROOT + "/"):
        raise ValueError("Android source directory outside admitted tree")
    digest = entry["childrenSha256"]
    if not isinstance(digest, str) or re.fullmatch(r"[0-9a-f]{64}", digest) is None:
        raise ValueError("Android source directory digest invalid")
    if "mtimeNs" in entry:
        stamp = entry["mtimeNs"]
        mode = entry["mode"]
        if (not isinstance(stamp, str) or
                re.fullmatch(r"(?:0|[1-9][0-9]{0,18})", stamp) is None or
                int(stamp) > MAX_NS or type(mode) is not int or mode < 0 or mode > 0o777):
            raise ValueError("Android source directory timestamp or mode invalid")
    return path


def children_digest(fd):
    children = []
    with os.scandir(fd) as directory:
        for child in directory:
            checked_time()
            if len(children) >= MAX_ENTRIES:
                raise ValueError("Android source directory child cap exceeded")
            info = os.stat(child.name, dir_fd=fd, follow_symlinks=False)
            kind = "directory" if stat.S_ISDIR(info.st_mode) else "file" if stat.S_ISREG(info.st_mode) else None
            if kind is None:
                raise ValueError("Android source directory contains a link or special member")
            children.append((child.name, kind))
    children.sort(key=lambda child: child[0].encode("utf8"))
    encoded = json.dumps(children, ensure_ascii=False, separators=(",", ":")).encode("utf8")
    return hashlib.sha256(encoded).hexdigest()


def main():
    raw = sys.stdin.buffer.read(MAX_INPUT + 1)
    if len(raw) > MAX_INPUT:
        raise ValueError("Android source metadata exceeds input cap")
    request = json.loads(raw)
    if not isinstance(request, dict) or set(request) not in ({"files", "directories"}, {"entries", "directories"}):
        raise ValueError("Android source metadata request invalid")
    inventory = len(sys.argv) == 2 and sys.argv[1] == "inventory"
    entries = request.get("files") if inventory else request.get("entries")
    directories = request.get("directories")
    if (not isinstance(entries, list) or not entries or not isinstance(directories, list) or
            len(directories) > MAX_SOURCE_DIRECTORIES or len(entries) + len(directories) > MAX_ENTRIES):
        raise ValueError("Android source metadata entry count invalid")
    soft, hard = resource.getrlimit(resource.RLIMIT_NOFILE)
    needed = (64 if inventory else len(entries) + len(directories) + 64)
    if soft < needed:
        if hard < needed:
            raise ValueError("Android source metadata requires more file descriptors")
        resource.setrlimit(resource.RLIMIT_NOFILE, (needed, hard))
    root_fd = os.open(".", os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW)
    progress.update(expectedFiles=len(entries), expectedDirectories=len(directories),
                    operation="inventory" if inventory else "replay")
    report("validation")
    files = SourceFiles(root_fd)
    opened = []
    opened_directories = []
    emitted = []
    total = 0
    previous = ""
    try:
        # Keep all verified file descriptors alive until the last hash matches.
        for entry in entries:
            checked_time()
            if inventory:
                path = entry
            elif isinstance(entry, dict) and set(entry) == {"path", "size", "sha256", "mtimeNs"}:
                path = entry["path"]
            else:
                raise ValueError("Android source metadata member invalid")
            checked_path(path, previous)
            if not inventory:
                size, digest, timestamp = (entry[key] for key in ("size", "sha256", "mtimeNs"))
            if not inventory and (type(size) is not int or size < 0 or
                                  not isinstance(digest, str) or re.fullmatch(r"[0-9a-f]{64}", digest) is None or
                                  not isinstance(timestamp, str) or
                                  re.fullmatch(r"(?:0|[1-9][0-9]{0,18})", timestamp) is None or
                                  int(timestamp) > MAX_NS):
                raise ValueError("Android source metadata path or value invalid")
            previous = path
            # Inventory closes each file immediately; preserve its full path walk.
            if inventory:
                files.close()
            fd = files.open(path)
            before = os.fstat(fd)
            if inventory:
                size = before.st_size
            total += size
            if total > MAX_BYTES or size < 0:
                raise ValueError("Android source metadata byte cap exceeded")
            if before.st_size != size:
                raise ValueError(f"Android source size mismatch: {path}")
            actual = hash_fd(fd, size, path)
            after = os.fstat(fd)
            if ((not inventory and actual != digest) or
                    after.st_dev != before.st_dev or after.st_ino != before.st_ino or
                    after.st_size != before.st_size or after.st_mtime_ns != before.st_mtime_ns):
                raise ValueError(f"Android source bytes differ from producer: {path}")
            progress["files"] += 1
            progress["bytes"] = total
            if inventory:
                if after.st_mtime_ns < 0 or after.st_mtime_ns > MAX_NS:
                    raise ValueError(f"Android source timestamp out of range: {path}")
                emitted.append({"path": path, "size": size, "sha256": actual,
                                "mtimeNs": str(after.st_mtime_ns)})
                os.close(fd)
            else:
                files.remember(path, fd, before)
                opened.append((fd, path, int(timestamp), before.st_mtime_ns, before.st_atime_ns))
        # All tracked file hashes are valid before examining any directory. The
        # complete immediate-child graph rejects untracked or generated members.
        previous = ""
        for entry in directories:
            checked_time()
            path = checked_directory_member(entry)
            if path <= previous:
                raise ValueError("Android source directories duplicated or unordered")
            previous = path
            fd = checked_directory(root_fd, path)
            before = os.fstat(fd)
            actual_digest = children_digest(fd)
            after = os.fstat(fd)
            mode = stat.S_IMODE(before.st_mode)
            if (actual_digest != entry["childrenSha256"] or
                    after.st_dev != before.st_dev or after.st_ino != before.st_ino or
                    after.st_mtime_ns != before.st_mtime_ns or stat.S_IMODE(after.st_mode) != mode or
                    (not inventory and mode != entry["mode"])):
                os.close(fd)
                raise ValueError("Android source directory graph or mode differs from producer")
            progress["directories"] += 1
            if inventory:
                if after.st_mtime_ns < 0 or after.st_mtime_ns > MAX_NS:
                    os.close(fd)
                    raise ValueError("Android source directory timestamp out of range")
                emitted.append({"path": path, "childrenSha256": actual_digest,
                                "mode": mode, "mtimeNs": str(after.st_mtime_ns)})
                os.close(fd)
            else:
                opened_directories.append((fd, path, int(entry["mtimeNs"]), before.st_mtime_ns,
                                           before.st_atime_ns, before.st_dev, before.st_ino, mode,
                                           actual_digest))
        checked_time()
        if inventory:
            file_count = len(entries)
            sys.stdout.write(json.dumps({"entries": emitted[:file_count], "directories": emitted[file_count:]},
                                        separators=(",", ":")))
        else:
            report("revalidation")
            files.revalidate()
            report("replay")
            applied = []
            try:
                for fd, path, timestamp, original_mtime, original_atime in opened:
                    checked_time()
                    applied.append((fd, original_atime, original_mtime))
                    os.utime(fd, ns=(original_atime, timestamp))
                    if os.fstat(fd).st_mtime_ns != timestamp:
                        raise ValueError(f"Android source timestamp precision lost: {path}")
                    progress["applied"] += 1
                for record in sorted(opened_directories, key=lambda item: (-item[1].count("/"), item[1])):
                    checked_time()
                    fd, path, timestamp, original_mtime, original_atime, device, inode, mode, digest = record
                    reopened = checked_directory(root_fd, path)
                    try:
                        current = os.fstat(reopened)
                        if (current.st_dev != device or current.st_ino != inode or
                                stat.S_IMODE(current.st_mode) != mode or children_digest(reopened) != digest):
                            raise ValueError("Android source directory changed during replay")
                    finally:
                        os.close(reopened)
                    applied.append((fd, original_atime, original_mtime))
                    os.utime(fd, ns=(original_atime, timestamp))
                    if os.fstat(fd).st_mtime_ns != timestamp:
                        raise ValueError(f"Android source directory timestamp precision lost: {path}")
                    progress["applied"] += 1
            except Exception:
                for fd, original_atime, original_mtime in reversed(applied):
                    os.utime(fd, ns=(original_atime, original_mtime))
                    progress["rolledBack"] += 1
                raise
    finally:
        files.close()
        for fd, *_ in opened_directories:
            os.close(fd)
        for fd, *_ in opened:
            os.close(fd)
        os.close(root_fd)


if __name__ == "__main__":
    try:
        main()
    except Exception:
        report("failed")
        raise
    report("complete")
