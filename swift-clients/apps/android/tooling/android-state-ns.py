#!/usr/bin/env python3
"""Restore exact regular-file nanosecond mtimes before staged links are created."""

import json
import os
import re
import stat
import sys

MAX_TIMESTAMP_NS = 9_223_372_036_854_775_807
MAX_MEMBERS = 150_000
checked_directories = set()

for count, line in enumerate(sys.stdin, start=1):
    if count > MAX_MEMBERS or len(line) > 8192:
        raise ValueError("Android state timestamp metadata exceeds bounds")
    member = json.loads(line)
    path = member["path"]
    timestamp = member["mtimeNs"]
    if (
        not isinstance(path, str)
        or not path
        or len(path) > 4096
        or path.startswith("/")
        or "\\" in path
        or any(part in ("", ".", "..") for part in path.split("/"))
        or not isinstance(timestamp, str)
        or not re.fullmatch(r"[0-9]{1,19}", timestamp)
        or int(timestamp) > MAX_TIMESTAMP_NS
    ):
        raise ValueError("invalid Android state timestamp member")
    parts = path.split("/")
    for index in range(1, len(parts)):
        parent = "/".join(parts[:index])
        if parent not in checked_directories:
            if not stat.S_ISDIR(os.lstat(parent).st_mode):
                raise ValueError("Android state timestamp parent is not a real directory")
            checked_directories.add(parent)
    if not stat.S_ISREG(os.lstat(path).st_mode):
        raise ValueError("Android state timestamp target is not a regular file")
    ns = int(timestamp)
    os.utime(path, ns=(ns, ns), follow_symlinks=False)
    if os.lstat(path).st_mtime_ns != ns:
        raise ValueError("Android state timestamp precision was lost")
