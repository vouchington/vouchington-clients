#!/usr/bin/env python3
"""Bounded, read-only SwiftPM input snapshot for exact-run Android CI diagnosis."""

import argparse
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import sqlite3
import stat
import struct
import sys
import time
from urllib.parse import quote

MAX_DB_BYTES = 64 * 1024 * 1024
MAX_KEYS = 50_000
MAX_INPUTS = 20_000
MAX_COMMANDS = 10_000
MAX_KEY_BYTES = 256 * 1024
MAX_REPORT_BYTES = 4 * 1024 * 1024
MAX_SECONDS = 45
MAX_SAMPLES = 30
STAGES = {"producer-after-pack", "consumer-after-restore", "consumer-after-resolve", "consumer-after-gradle"}


def inside(path, root):
    return path == root or path.startswith(root + os.sep)


def trusted_roots(checkout):
    candidates = {
        "candidate": checkout,
        "skip-home": os.environ.get("VOUCHA_SKIP_SWIFT_HOME", ""),
        "mise": os.environ.get("MISE_DATA_DIR", ""),
        "android-sdk": os.environ.get("ANDROID_SDK_ROOT", ""),
        "xcode": os.environ.get("DEVELOPER_DIR", ""),
    }
    roots = {}
    for name, value in candidates.items():
        if not value or not os.path.isabs(value):
            raise ValueError(f"Missing absolute trusted {name} root")
        actual = os.path.realpath(value)
        if not os.path.isdir(actual):
            raise ValueError(f"Trusted {name} root is absent")
        roots[name] = actual
    return roots


def nofollow_stat(path, roots):
    """Resolve known generated links, then walk the resolved path without links."""
    lexical_bucket = next((name for name, root in roots.items() if inside(path, root)), None)
    if lexical_bucket is None:
        return "unknown-root", None, None
    resolved = os.path.realpath(path)
    actual_bucket = next((name for name, root in roots.items() if inside(resolved, root)), None)
    if actual_bucket is None:
        return "outside-root", lexical_bucket, None
    root = roots[actual_bucket]
    relative = os.path.relpath(resolved, root)
    components = [] if relative == "." else relative.split(os.sep)
    if any(part in ("", ".", "..") for part in components):
        return "unsafe-path", lexical_bucket, None
    fd = os.open(root, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW)
    try:
        for part in components[:-1]:
            child = os.open(part, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW, dir_fd=fd)
            os.close(fd)
            fd = child
        try:
            result = os.stat(components[-1], dir_fd=fd, follow_symlinks=False) if components else os.fstat(fd)
        except FileNotFoundError:
            return "missing", lexical_bucket, None
        if stat.S_ISLNK(result.st_mode):
            return "symlink-race", lexical_bucket, None
        return "ok", lexical_bucket, result
    except (FileNotFoundError, NotADirectoryError):
        return "missing", lexical_bucket, None
    finally:
        os.close(fd)


def decode_existing_input(value):
    # Pinned Swift 6.4 llbuild BuildValue: kind=2, one FileInfo, then
    # device/inode/mode/size/mtime.seconds/mtime.nanoseconds/checksum.
    if not isinstance(value, bytes) or len(value) != 85 or value[:5] != b"\x02\x01\x00\x00\x00":
        raise ValueError("Unexpected llbuild ExistingInput encoding")
    size, seconds, nanoseconds = struct.unpack_from("<QQQ", value, 29)
    if nanoseconds >= 1_000_000_000:
        raise ValueError("Invalid llbuild nanosecond timestamp")
    return size, seconds * 1_000_000_000 + nanoseconds


def command_record(key, signature, built_at, roots):
    # Command keys can contain absolute job paths. Replace only known trusted
    # roots for cross-job identity; never emit the key or its arguments.
    normalized = key
    for name, root in sorted(roots.items(), key=lambda item: len(item[1]), reverse=True):
        normalized = normalized.replace(os.fsencode(root), f"<root:{name}>".encode())
    signature_bytes = signature if isinstance(signature, bytes) else str(signature).encode()
    return {
        "logicalKeySha256": hashlib.sha256(normalized).hexdigest(),
        "actualKeySha256": hashlib.sha256(key).hexdigest(),
        "signatureSha256": hashlib.sha256(signature_bytes).hexdigest(),
        "builtAt": built_at,
    }


def read_manifest(path, identity):
    if path.stat().st_size > 8192:
        raise ValueError("Android state manifest exceeds bound")
    manifest = json.loads(path.read_text())
    if manifest.get("identity") != identity or manifest.get("format") != 3:
        raise ValueError("Android state manifest identity mismatch")
    sha = manifest.get("sha256")
    if not isinstance(sha, str) or len(sha) != 64 or any(c not in "0123456789abcdef" for c in sha):
        raise ValueError("Android state manifest digest invalid")
    return sha


def bounded_file_sha(path, byte_cap):
    if not path.is_file() or path.stat().st_size > byte_cap:
        raise ValueError("Pinned toolchain identity file is absent or oversized")
    digest = hashlib.sha256()
    total = 0
    with path.open("rb") as stream:
        while chunk := stream.read(1024 * 1024):
            total += len(chunk)
            if total > byte_cap:
                raise ValueError("Pinned toolchain identity file grew beyond bound")
            digest.update(chunk)
    return digest.hexdigest()


def snapshot(db_path, roots, identity, stage, archive_sha):
    started = time.monotonic()
    if not db_path.is_file() or db_path.stat().st_size > MAX_DB_BYTES:
        raise ValueError("SwiftPM build database is absent or oversized")
    db_hash = hashlib.sha256()
    read_bytes = 0
    with db_path.open("rb") as stream:
        while chunk := stream.read(1024 * 1024):
            read_bytes += len(chunk)
            if read_bytes > MAX_DB_BYTES:
                raise ValueError("SwiftPM build database grew beyond bound")
            db_hash.update(chunk)
            if time.monotonic() - started > MAX_SECONDS:
                raise TimeoutError("SwiftPM input scan timed out")
    uri = f"file:{quote(str(db_path))}?mode=ro&immutable=1"
    connection = sqlite3.connect(uri, uri=True, timeout=1)
    try:
        connection.execute("PRAGMA query_only=ON")
        connection.set_progress_handler(lambda: 1 if time.monotonic() - started > MAX_SECONDS else 0, 1000)
        tables = {row[0] for row in connection.execute("SELECT name FROM sqlite_master WHERE type='table'")}
        if tables != {"info", "key_names", "rule_results"}:
            raise ValueError("Unexpected llbuild database tables")
        columns = {
            name: {row[1] for row in connection.execute(f"PRAGMA table_info({name})")}
            for name in tables
        }
        if not {"version", "client_version"} <= columns["info"] or not {"id", "key"} <= columns["key_names"] or not {"key_id", "value", "signature", "built_at"} <= columns["rule_results"]:
            raise ValueError("Unexpected llbuild database columns")
        if connection.execute("SELECT version, client_version FROM info").fetchone() != (17, 9):
            raise ValueError("Unexpected pinned llbuild database version")
        if connection.execute("SELECT count(*) FROM key_names").fetchone()[0] > MAX_KEYS:
            raise ValueError("SwiftPM key cap exceeded")
        # llbuild keys are byte strings; some non-path keys are not UTF-8.
        connection.text_factory = bytes
        counts = {}
        samples = {}
        commands = hashlib.sha256()
        command_records = []
        command_count = 0
        input_count = 0
        for key, value, signature, built_at in connection.execute(
            "SELECT key, value, signature, built_at FROM key_names "
            "JOIN rule_results ON key_names.id = rule_results.key_id ORDER BY key_names.id"
        ):
            if time.monotonic() - started > MAX_SECONDS:
                raise TimeoutError("SwiftPM input scan timed out")
            if not isinstance(key, bytes) or len(key) > MAX_KEY_BYTES:
                raise ValueError("Invalid SwiftPM key")
            if key.startswith(b"C"):
                command_count += 1
                if command_count > MAX_COMMANDS:
                    raise ValueError("SwiftPM command cap exceeded")
                commands.update(key)
                commands.update(str(signature).encode())
                commands.update(str(built_at).encode())
                command_records.append(command_record(key, signature, built_at, roots))
            if not key.startswith(b"N/"):
                continue
            if len(key) > 8192:
                raise ValueError("SwiftPM input path exceeds bound")
            input_count += 1
            if input_count > MAX_INPUTS:
                raise ValueError("SwiftPM input cap exceeded")
            if not isinstance(value, bytes) or not value or value[0] != 2:
                counts["other-llbuild-input-kind"] = counts.get("other-llbuild-input-kind", 0) + 1
                continue
            try:
                path = key[1:].decode("utf8")
            except UnicodeDecodeError as error:
                raise ValueError("Invalid SwiftPM input path encoding") from error
            if not os.path.isabs(path):
                raise ValueError("Nonabsolute SwiftPM input")
            expected_size, expected_ns = decode_existing_input(value)
            state, bucket, observed = nofollow_stat(path, roots)
            if state == "ok":
                if not (stat.S_ISREG(observed.st_mode) or stat.S_ISDIR(observed.st_mode)):
                    state = "other-type"
                elif observed.st_size != expected_size:
                    state = "size-diff"
                elif observed.st_mtime_ns != expected_ns:
                    state = "mtime-diff"
                else:
                    state = "exact"
            group = f"{bucket or 'unknown'}:{state}"
            counts[group] = counts.get(group, 0) + 1
            if state != "exact" and len(samples) < MAX_SAMPLES:
                samples[hashlib.sha256(path.encode()).hexdigest()[:20]] = {
                    "bucket": bucket or "unknown", "state": state,
                    "expectedSize": expected_size, "expectedMtimeNs": str(expected_ns),
                    "observedSize": observed.st_size if observed else None,
                    "observedMtimeNs": str(observed.st_mtime_ns) if observed else None,
                }
        return {
            "identity": identity, "stage": stage, "archiveSha256": archive_sha,
            "capturedAt": datetime.now(timezone.utc).isoformat(),
            "dbSha256": db_hash.hexdigest(), "llbuildVersion": [17, 9],
            "commandCount": command_count, "commandDigest": commands.hexdigest(),
            "commands": command_records,
            "inputCount": input_count, "counts": counts, "samples": samples,
            "elapsedMs": round((time.monotonic() - started) * 1000),
        }
    finally:
        connection.close()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--stage", choices=sorted(STAGES), required=True)
    parser.add_argument("--package-root", type=Path, required=True)
    parser.add_argument("--manifest", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    revision = os.environ.get("CANDIDATE_REVISION", "")
    run = os.environ.get("GITHUB_RUN_ID", "")
    attempt = os.environ.get("GITHUB_RUN_ATTEMPT", "")
    if len(revision) != 40 or any(c not in "0123456789abcdef" for c in revision) or not run.isdecimal() or not attempt.isdecimal():
        raise ValueError("Exact candidate/run/attempt identity unavailable")
    identity = {"revision": revision, "run": run, "attempt": attempt, "gradle": "9.4.1"}
    archive_sha = read_manifest(args.manifest, identity)
    checkout = os.path.realpath(os.path.join(os.environ["GITHUB_WORKSPACE"], "candidate-clients"))
    package_root = os.path.realpath(args.package_root)
    if package_root != os.path.join(checkout, "swift-clients/apps/android"):
        raise ValueError("Unexpected Android package location")
    roots = trusted_roots(checkout)
    db = Path(package_root) / ".build/plugins/outputs/android/VouchaAndroid/destination/skipstone/VouchaAndroid/build/swift/out/Intermediates.noindex/XCBuildData/build.db"
    report = snapshot(db, roots, identity, args.stage, archive_sha)
    compiler = Path(roots["mise"]) / "installs/swift/6.4.0/usr/bin/swift"
    sdk_info = Path(roots["skip-home"]) / "Library/org.swift.swiftpm/swift-sdks/swift-6.4.0-RELEASE_android.artifactbundle/info.json"
    report["toolchain"] = {
        "swiftVersion": "6.4.0", "swiftBinarySha256": bounded_file_sha(compiler, 128 * 1024 * 1024),
        "androidSdkInfoSha256": bounded_file_sha(sdk_info, 1024 * 1024),
    }
    encoded = (json.dumps(report, sort_keys=True, separators=(",", ":")) + "\n").encode()
    if len(encoded) > MAX_REPORT_BYTES:
        raise ValueError("SwiftPM diagnostic report exceeds bound")
    if args.output.exists():
        raise ValueError("SwiftPM diagnostic output already exists")
    args.output.write_bytes(encoded)
    print(f"SwiftPM {args.stage}: {report['inputCount']} inputs, {report['commandCount']} commands, {report['counts']}")


if __name__ == "__main__":
    try:
        main()
    except (OSError, ValueError, sqlite3.Error, TimeoutError) as error:
        print(f"SwiftPM diagnostic unavailable: {error}", file=sys.stderr)
        sys.exit(1)
