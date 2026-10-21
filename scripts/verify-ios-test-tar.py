#!/usr/bin/env python3
"""Reject iOS test-product archives that could write outside Products."""

import posixpath
import sys
import tarfile


def inside_products(path: str) -> bool:
    return path == "Products" or path.startswith("Products/")


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
    if not inside_products("/".join(stack)):
        raise ValueError(f"Escaping archive link: {name!r}")


def validate(archive_path: str) -> None:
    seen: dict[str, tarfile.TarInfo] = {}
    with tarfile.open(archive_path, "r:gz") as archive:
        for member in archive:
            name = member.name.rstrip("/")
            parts = name.split("/")
            if (
                not inside_products(name)
                or any(part in ("", ".", "..") for part in parts)
                or posixpath.normpath(name) != name
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
            if name == "Products" and not member.isdir():
                raise ValueError("Products root must be a directory")
            if member.issym() or member.islnk():
                target = member.linkname
                if not target or target.startswith("/"):
                    raise ValueError(f"Unsafe archive link: {member.name!r} -> {target!r}")
            elif not (member.isfile() or member.isdir()):
                raise ValueError(f"Unsupported archive entry: {member.name!r}")
            seen[name] = member
    for name, member in seen.items():
        if member.issym() or member.islnk():
            resolve_link(name, seen)


if __name__ == "__main__":
    if len(sys.argv) != 2:
        raise SystemExit("usage: verify-ios-test-tar.py ARCHIVE")
    try:
        validate(sys.argv[1])
    except (OSError, tarfile.TarError, ValueError) as error:
        raise SystemExit(str(error)) from error
