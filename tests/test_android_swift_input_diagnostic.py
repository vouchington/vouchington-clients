"""Focused safety and classification tests for the read-only SwiftPM diagnostic."""

import importlib.util
import json
import os
from pathlib import Path
import sqlite3
import struct
import tempfile
import unittest


HELPER = Path(__file__).resolve().parents[1] / "swift-clients/apps/android/tooling/report-swiftpm-inputs.py"
SPEC = importlib.util.spec_from_file_location("android_swift_input_diagnostic", HELPER)
diagnostic = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(diagnostic)


def existing_input(file_stat):
    return b"\x02" + struct.pack(
        "<IQQQQQQ", 1, 0, 0, file_stat.st_mode, file_stat.st_size,
        file_stat.st_mtime_ns // 1_000_000_000, file_stat.st_mtime_ns % 1_000_000_000,
    ) + bytes(32)


class DiagnosticTest(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name).resolve()
        self.candidate = self.root / "candidate"
        self.candidate.mkdir()
        self.source = self.candidate / "Input.swift"
        self.source.write_text("public let answer = 42\n")
        self.db = self.root / "build.db"
        self.connection = sqlite3.connect(self.db)
        self.connection.executescript("""
            CREATE TABLE info (id INTEGER PRIMARY KEY, version INTEGER, client_version INTEGER, iteration INTEGER);
            CREATE TABLE key_names (id INTEGER PRIMARY KEY, key STRING UNIQUE);
            CREATE TABLE rule_results (key_id INTEGER PRIMARY KEY, value BLOB, signature INTEGER,
                built_at INTEGER, computed_at INTEGER, start REAL, end REAL, dependencies BLOB);
            INSERT INTO info VALUES (0,17,9,2);
        """)
        self.connection.execute("INSERT INTO key_names VALUES (1, ?)", ("N" + str(self.source),))
        self.connection.execute(
            "INSERT INTO rule_results VALUES (1, ?, 1, 1, 1, 0, 0, X'')",
            (existing_input(self.source.stat()),),
        )
        self.connection.execute("INSERT INTO key_names VALUES (2, 'Ccompile')")
        self.connection.execute("INSERT INTO rule_results VALUES (2, X'01', 42, 1, 1, 0, 0, X'')")
        self.connection.commit()
        self.connection.close()
        self.identity = {"revision": "a" * 40, "run": "123", "attempt": "1", "gradle": "9.4.1"}
        self.roots = {"candidate": str(self.candidate.resolve())}

    def take(self):
        return diagnostic.snapshot(self.db, self.roots, self.identity, "consumer-after-restore", "b" * 64)

    def test_exact_then_changed_mtime_without_source_mutation(self):
        original = self.source.read_bytes()
        self.assertEqual(self.take()["counts"], {"candidate:exact": 1})
        before = self.source.stat()
        os.utime(self.source, ns=(before.st_atime_ns, before.st_mtime_ns + 10_000_000))
        report = self.take()
        self.assertEqual(report["counts"], {"candidate:mtime-diff": 1})
        self.assertEqual(len(report["samples"]), 1)
        self.assertNotIn(str(self.root), json.dumps(report))
        self.assertEqual(self.source.read_bytes(), original)

    def test_symlink_escape_is_not_stat_ed(self):
        outside = self.root / "outside"
        outside.write_bytes(self.source.read_bytes())
        self.source.unlink()
        self.source.symlink_to(outside)
        self.assertEqual(self.take()["counts"], {"candidate:outside-root": 1})

    def test_malformed_build_value_fails_closed(self):
        db = sqlite3.connect(self.db)
        db.execute("UPDATE rule_results SET value = X'0201' WHERE key_id = 1")
        db.commit()
        db.close()
        with self.assertRaisesRegex(ValueError, "Unexpected llbuild"):
            self.take()

    def test_key_cap_fails_closed(self):
        old = diagnostic.MAX_KEYS
        diagnostic.MAX_KEYS = 1
        self.addCleanup(setattr, diagnostic, "MAX_KEYS", old)
        with self.assertRaisesRegex(ValueError, "key cap"):
            self.take()

    def test_manifest_identity_and_sha_are_required(self):
        path = self.root / "manifest.json"
        path.write_text(json.dumps({"format": 3, "identity": self.identity, "sha256": "b" * 64}))
        self.assertEqual(diagnostic.read_manifest(path, self.identity), "b" * 64)
        with self.assertRaisesRegex(ValueError, "identity mismatch"):
            diagnostic.read_manifest(path, {**self.identity, "run": "124"})

    def test_binary_nonpath_keys_do_not_break_input_snapshot(self):
        db = sqlite3.connect(self.db)
        db.execute("INSERT INTO key_names VALUES (3, ?)", (b"XR\xff\x00",))
        db.execute("INSERT INTO rule_results VALUES (3, X'01', 0, 1, 1, 0, 0, X'')")
        db.commit()
        db.close()
        self.assertEqual(self.take()["counts"], {"candidate:exact": 1})

    def test_command_fingerprint_redacts_root_and_preserves_signature(self):
        first = diagnostic.command_record(b"C" + os.fsencode(self.candidate) + b"/compile", 42, 7, self.roots)
        second = diagnostic.command_record(b"C/other/candidate/compile", 42, 7, {"candidate": "/other/candidate"})
        self.assertEqual(first["logicalKeySha256"], second["logicalKeySha256"])
        self.assertNotEqual(first["actualKeySha256"], second["actualKeySha256"])
        self.assertEqual(first["signatureSha256"], second["signatureSha256"])
        self.assertEqual(first["builtAt"], 7)
        self.assertNotIn(str(self.candidate), json.dumps(first))

    def test_command_cap_fails_closed(self):
        old = diagnostic.MAX_COMMANDS
        diagnostic.MAX_COMMANDS = 0
        self.addCleanup(setattr, diagnostic, "MAX_COMMANDS", old)
        with self.assertRaisesRegex(ValueError, "command cap"):
            self.take()


if __name__ == "__main__":
    unittest.main()
