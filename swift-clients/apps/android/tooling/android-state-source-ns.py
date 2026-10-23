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
deadline = time.monotonic() + 60


def checked_time():
    if time.monotonic() > deadline:
        raise ValueError("Android source metadata validation deadline exceeded")


def checked_file(root_fd, path):
    parts = path.split("/")
    directory = root_fd
    try:
        for part in parts[:-1]:
            next_dir = os.open(part, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW, dir_fd=directory)
            if directory != root_fd:
                os.close(directory)
            directory = next_dir
        fd = os.open(parts[-1], os.O_RDONLY | os.O_NOFOLLOW, dir_fd=directory)
        if not stat.S_ISREG(os.fstat(fd).st_mode):
            os.close(fd)
            raise ValueError(f"Android source is not a regular file: {path}")
        return fd
    finally:
        if directory != root_fd:
            os.close(directory)


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


def main():
    raw = sys.stdin.buffer.read(MAX_INPUT + 1)
    if len(raw) > MAX_INPUT:
        raise ValueError("Android source metadata exceeds input cap")
    entries = json.loads(raw)
    if not isinstance(entries, list) or not entries or len(entries) > MAX_ENTRIES:
        raise ValueError("Android source metadata entry count invalid")
    soft, hard = resource.getrlimit(resource.RLIMIT_NOFILE)
    inventory = len(sys.argv) == 2 and sys.argv[1] == "inventory"
    needed = (64 if inventory else len(entries) + 64)
    if soft < needed:
        if hard < needed:
            raise ValueError("Android source metadata requires more file descriptors")
        resource.setrlimit(resource.RLIMIT_NOFILE, (needed, hard))
    root_fd = os.open(".", os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW)
    opened = []
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
            fd = checked_file(root_fd, path)
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
            if inventory:
                if after.st_mtime_ns < 0 or after.st_mtime_ns > MAX_NS:
                    raise ValueError(f"Android source timestamp out of range: {path}")
                emitted.append({"path": path, "size": size, "sha256": actual,
                                "mtimeNs": str(after.st_mtime_ns)})
                os.close(fd)
            else:
                opened.append((fd, path, int(timestamp), before.st_mtime_ns, after.st_atime_ns))
        checked_time()
        if inventory:
            sys.stdout.write(json.dumps(emitted, separators=(",", ":")))
        else:
            applied = []
            try:
                for fd, path, timestamp, original_mtime, original_atime in opened:
                    checked_time()
                    applied.append((fd, original_atime, original_mtime))
                    os.utime(fd, ns=(original_atime, timestamp))
                    if os.fstat(fd).st_mtime_ns != timestamp:
                        raise ValueError(f"Android source timestamp precision lost: {path}")
            except Exception:
                for fd, original_atime, original_mtime in reversed(applied):
                    os.utime(fd, ns=(original_atime, original_mtime))
                raise
    finally:
        for fd, *_ in opened:
            os.close(fd)
        os.close(root_fd)


if __name__ == "__main__":
    main()
