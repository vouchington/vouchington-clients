#!/usr/bin/env python3
"""Reject native test-product archives that could write outside their selected root."""

import posixpath
import os
import sys
import tarfile
from typing import Optional


MAX_COMPRESSED_BYTES = 1024 * 1024 * 1024
MAX_UNCOMPRESSED_BYTES = 4 * 1024 * 1024 * 1024
MAX_MEMBERS = 10000


def inside_root(path: str, root: str) -> bool:
    return path == root or path.startswith(f"{root}/")


def resolve_link(name: str, entries: dict[str, tarfile.TarInfo], root: str) -> None:
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
                raise ValueError(f"Escaping archive link: {name!r}")
            stack.pop()
            continue
        stack.append(part)
        path = "/".join(stack)
        target = entries.get(path)
        if target is not None and (target.issym() or target.islnk()):
            hops += 1
            if hops > len(entries):
                raise ValueError(f"Cyclic archive link: {name!r}")
            if target.islnk():
                stack = []
            else:
                stack.pop()
            pending = target.linkname.split("/") + pending
    if not inside_root("/".join(stack), root):
        raise ValueError(f"Escaping archive link: {name!r}")


def validate(archive_path: str, root: str = "Voucha.xctestproducts", allowed_paths: Optional[tuple[str, ...]] = None) -> None:
    if not root or "/" in root or root in (".", ".."):
        raise ValueError("Invalid archive root")
    if allowed_paths is not None and any(not inside_root(path, root) for path in allowed_paths):
        raise ValueError("Selected product outside archive root")
    if os.path.getsize(archive_path) > MAX_COMPRESSED_BYTES:
        raise ValueError("Archive exceeds compressed size limit")
    seen: dict[str, tarfile.TarInfo] = {}
    uncompressed_bytes = 0
    with tarfile.open(archive_path, "r:gz") as archive:
        for member in archive:
            if len(seen) >= MAX_MEMBERS:
                raise ValueError("Archive exceeds member limit")
            if member.size < 0:
                raise ValueError("Archive member has negative size")
            uncompressed_bytes += member.size
            if uncompressed_bytes > MAX_UNCOMPRESSED_BYTES:
                raise ValueError("Archive exceeds uncompressed size limit")
            name = member.name.rstrip("/")
            parts = name.split("/")
            if (
                not inside_root(name, root)
                or any(part in ("", ".", "..") for part in parts)
                or posixpath.normpath(name) != name
                or (allowed_paths is not None and name != root and not any(
                    name == path or name.startswith(f"{path}/")
                    or path.startswith(f"{name}/") for path in allowed_paths
                ))
            ):
                raise ValueError(f"Unsafe archive path: {member.name!r}")
            if name in seen:
                raise ValueError(f"Duplicate archive path: {member.name!r}")
            for index in range(1, len(parts)):
                ancestor = seen.get("/".join(parts[:index]))
                if ancestor is not None and not ancestor.isdir():
                    raise ValueError(f"Archive path descends through a non-directory: {member.name!r}")
            if not member.isdir() and any(path.startswith(f"{name}/") for path in seen):
                raise ValueError(f"Archive path replaces a directory: {member.name!r}")
            if name == root and not member.isdir():
                raise ValueError(f"{root} root must be a directory")
            if member.issym() or member.islnk():
                target = member.linkname
                if not target or target.startswith("/"):
                    raise ValueError(f"Unsafe archive link: {member.name!r} -> {target!r}")
            elif not (member.isfile() or member.isdir()):
                raise ValueError(f"Unsupported archive entry: {member.name!r}")
            seen[name] = member
    if allowed_paths is not None and any(path not in seen for path in allowed_paths):
        raise ValueError("Archive is missing a selected product")
    for name, member in seen.items():
        if member.issym() or member.islnk():
            resolve_link(name, seen, root)


if __name__ == "__main__":
    if len(sys.argv) != 2:
        raise SystemExit("usage: verify-ios-test-tar.py ARCHIVE")
    try:
        validate(sys.argv[1])
    except (OSError, tarfile.TarError, ValueError) as error:
        raise SystemExit(str(error)) from error
