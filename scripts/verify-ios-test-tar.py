#!/usr/bin/env python3
"""Reject iOS test-product archives that could write outside Voucha.xctestproducts."""

import posixpath
import os
import sys
import tarfile
import unicodedata


MAX_COMPRESSED_BYTES = 1024 * 1024 * 1024
MAX_UNCOMPRESSED_BYTES = 4 * 1024 * 1024 * 1024
MAX_MEMBERS = 10000


def path_key(path: str) -> str:
    """Compare archive paths as a case- and normalization-insensitive macOS volume does."""
    return unicodedata.normalize("NFD", unicodedata.normalize("NFD", path).casefold())


def inside_products(path: str) -> bool:
    return path == "Voucha.xctestproducts" or path.startswith("Voucha.xctestproducts/")


def resolve_link(name: str, entries: dict[str, tarfile.TarInfo]) -> None:
    link = entries[path_key(name)]
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
        target = entries.get(path_key(path))
        if target is not None and (target.issym() or target.islnk()):
            hops += 1
            if hops > len(entries):
                raise ValueError(f"Cyclic archive link: {name!r}")
            if target.islnk():
                stack = []
            else:
                stack.pop()
            pending = target.linkname.split("/") + pending
    if not inside_products("/".join(stack)):
        raise ValueError(f"Escaping archive link: {name!r}")


def validate(archive_path: str) -> None:
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
                not inside_products(name)
                or any(part in ("", ".", "..") for part in parts)
                or posixpath.normpath(name) != name
            ):
                raise ValueError(f"Unsafe archive path: {member.name!r}")
            key = path_key(name)
            if key in seen:
                raise ValueError(f"Duplicate archive path: {member.name!r}")
            for index in range(1, len(parts)):
                ancestor = seen.get(path_key("/".join(parts[:index])))
                if ancestor is not None and not ancestor.isdir():
                    raise ValueError(f"Archive path descends through a non-directory: {member.name!r}")
            if not member.isdir() and any(path.startswith(f"{key}/") for path in seen):
                raise ValueError(f"Archive path replaces a directory: {member.name!r}")
            if name == "Voucha.xctestproducts" and not member.isdir():
                raise ValueError("Voucha.xctestproducts root must be a directory")
            if member.issym() or member.islnk():
                target = member.linkname
                if not target or target.startswith("/"):
                    raise ValueError(f"Unsafe archive link: {member.name!r} -> {target!r}")
            elif not (member.isfile() or member.isdir()):
                raise ValueError(f"Unsupported archive entry: {member.name!r}")
            seen[key] = member
    for member in seen.values():
        if member.issym() or member.islnk():
            resolve_link(member.name.rstrip("/"), seen)


if __name__ == "__main__":
    if len(sys.argv) != 2:
        raise SystemExit("usage: verify-ios-test-tar.py ARCHIVE")
    try:
        validate(sys.argv[1])
    except (OSError, tarfile.TarError, ValueError) as error:
        raise SystemExit(str(error)) from error
