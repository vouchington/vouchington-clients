#!/usr/bin/env python3
"""Verify the complete staged .build directory graph, then replay producer mtimes."""

import hashlib
import json
import os
import re
import stat
import sys

MAX_TIMESTAMP_NS = 9_223_372_036_854_775_807
MAX_MEMBERS = 150_000
MAX_INPUT_BYTES = 16 * 1024 * 1024
MAX_CHILDREN = 150_000
FLAGS = os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW


def valid_path(path):
    return (
        isinstance(path, str)
        and len(path) <= 4096
        and (path == ".build" or path.startswith(".build/"))
        and "\\" not in path
        and all(part not in ("", ".", "..") for part in path.split("/"))
    )


def open_directory(root_fd, path):
    current = root_fd
    try:
        for part in path.split("/"):
            following = os.open(part, FLAGS, dir_fd=current)
            if current != root_fd:
                os.close(current)
            current = following
        return current
    except BaseException:
        if current != root_fd:
            os.close(current)
        raise


def children_digest(fd):
    members = []
    with os.scandir(fd) as children:
        for child in children:
            if len(members) >= MAX_CHILDREN:
                raise ValueError("Android state directory child cap exceeded")
            details = child.stat(follow_symlinks=False)
            if stat.S_ISDIR(details.st_mode):
                kind = b"directory"
            elif stat.S_ISREG(details.st_mode):
                kind = b"file"
            elif stat.S_ISLNK(details.st_mode):
                kind = b"symlink"
            else:
                raise ValueError("Android state directory contains a special member")
            name = os.fsencode(child.name)
            if b"\x00" in name:
                raise ValueError("Android state directory member name is invalid")
            members.append((name, kind))
    digest = hashlib.sha256()
    for name, kind in sorted(members):
        digest.update(kind + b"\x00" + name + b"\x00")
    return len(members), digest.hexdigest()


def main():
    entries = []
    seen = set()
    input_bytes = 0
    for line in sys.stdin.buffer:
        input_bytes += len(line)
        if len(entries) >= MAX_MEMBERS or input_bytes > MAX_INPUT_BYTES or len(line) > 8192:
            raise ValueError("Android state directory timestamp input exceeds bounds")
        member = json.loads(line)
        if not isinstance(member, dict) or set(member) != {
            "path", "mtimeNs", "mode", "children", "childrenSha256"
        }:
            raise ValueError("Android state directory timestamp record is invalid")
        path = member["path"]
        timestamp = member["mtimeNs"]
        mode = member["mode"]
        children = member["children"]
        digest = member["childrenSha256"]
        if (
            not valid_path(path)
            or path in seen
            or not isinstance(timestamp, str)
            or not re.fullmatch(r"(?:0|[1-9][0-9]{0,18})", timestamp)
            or int(timestamp) > MAX_TIMESTAMP_NS
            or type(mode) is not int or not 0 <= mode <= 0o777
            or type(children) is not int or not 0 <= children <= MAX_CHILDREN
            or not isinstance(digest, str)
            or not re.fullmatch(r"[0-9a-f]{64}", digest)
        ):
            raise ValueError("Android state directory timestamp member is unsafe")
        seen.add(path)
        entries.append(member)
    if not entries or ".build" not in seen:
        raise ValueError("Android state .build directory metadata is incomplete")
    root_fd = os.open(".", FLAGS)
    try:
        # Validate the entire private staging graph before changing any timestamp.
        # Keep only one no-follow directory descriptor open at a time; macOS CI
        # may have a low per-process descriptor limit.
        validated = []
        for member in entries:
            fd = open_directory(root_fd, member["path"])
            try:
                details = os.fstat(fd)
                count, digest = children_digest(fd)
                if (
                    stat.S_IMODE(details.st_mode) != member["mode"]
                    or count != member["children"]
                    or digest != member["childrenSha256"]
                ):
                    raise ValueError("Android state directory graph or mode differs from archive")
                validated.append((member, details.st_dev, details.st_ino, details.st_atime_ns))
            finally:
                os.close(fd)
        # Reopen by no-follow dirfd and verify the same inodes and graph before
        # each update. Any late race fails on private staging before root swap.
        for member, device, inode, original_atime in sorted(
            validated, key=lambda item: -item[0]["path"].count("/")
        ):
            fd = open_directory(root_fd, member["path"])
            try:
                current = os.fstat(fd)
                count, digest = children_digest(fd)
                if (
                    current.st_dev != device or current.st_ino != inode
                    or stat.S_IMODE(current.st_mode) != member["mode"]
                    or count != member["children"]
                    or digest != member["childrenSha256"]
                ):
                    raise ValueError("Android state directory changed after graph validation")
                timestamp = int(member["mtimeNs"])
                os.utime(fd, ns=(original_atime, timestamp))
                if os.fstat(fd).st_mtime_ns != timestamp:
                    raise ValueError("Android state directory timestamp precision was lost")
            finally:
                os.close(fd)
    finally:
        os.close(root_fd)


if __name__ == "__main__":
    main()
