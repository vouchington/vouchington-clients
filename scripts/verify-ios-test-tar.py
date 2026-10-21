#!/usr/bin/env python3
"""Reject iOS test-product archives that could write outside Products."""

import posixpath
import sys
import tarfile


def inside_products(path: str) -> bool:
    return path == "Products" or path.startswith("Products/")


def validate(archive_path: str) -> None:
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
            if member.issym() or member.islnk():
                target = member.linkname
                if not target or target.startswith("/"):
                    raise ValueError(f"Unsafe archive link: {member.name!r} -> {target!r}")
                source = posixpath.dirname(name) if member.issym() else ""
                resolved = posixpath.normpath(posixpath.join(source, target))
                if not inside_products(resolved):
                    raise ValueError(f"Escaping archive link: {member.name!r} -> {target!r}")
            elif not (member.isfile() or member.isdir()):
                raise ValueError(f"Unsupported archive entry: {member.name!r}")


if __name__ == "__main__":
    if len(sys.argv) != 2:
        raise SystemExit("usage: verify-ios-test-tar.py ARCHIVE")
    try:
        validate(sys.argv[1])
    except (OSError, tarfile.TarError, ValueError) as error:
        raise SystemExit(str(error)) from error
