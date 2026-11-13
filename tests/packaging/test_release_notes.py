"""Unit checks for release formatting and integrity validation; no network services."""

import hashlib
import importlib.util
import json
import sys
import tempfile
import unittest
from pathlib import Path

PACKAGING = Path(__file__).resolve().parents[2] / "packaging"
sys.path.insert(0, str(PACKAGING))
from release_assets import PAYLOADS, download_table, validate_assets

spec = importlib.util.spec_from_file_location("release_notes", PACKAGING / "release-notes.py")
notes = importlib.util.module_from_spec(spec)
spec.loader.exec_module(notes)


class ReleaseNotesTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.checksums = Path(self.directory.name)
        self.metadata = {"assets": []}
        # Small unit inputs exercise the validator, not actual build/install verification.
        for name in PAYLOADS:
            digest = hashlib.sha256(name.encode()).hexdigest()
            content = f"{digest}  {name}\n".encode()
            (self.checksums / (name + ".sha256")).write_bytes(content)
            self.metadata["assets"].extend((
                {"name": name, "size": 1, "digest": "sha256:" + digest},
                {"name": name + ".sha256", "size": len(content),
                 "digest": "sha256:" + hashlib.sha256(content).hexdigest()},
            ))

    def tearDown(self):
        self.directory.cleanup()

    def test_complete_matrix_and_consistent_digests_pass(self):
        validate_assets(self.metadata, self.checksums)

    def test_missing_payload_or_checksum_blocks_publication(self):
        for name in (PAYLOADS[0], PAYLOADS[0] + ".sha256"):
            with self.subTest(name=name):
                metadata = json.loads(json.dumps(self.metadata))
                metadata["assets"] = [asset for asset in metadata["assets"] if asset["name"] != name]
                with self.assertRaisesRegex(ValueError, "Incomplete release"):
                    validate_assets(metadata, self.checksums)

    def test_mismatched_uploaded_payload_blocks_publication(self):
        self.metadata["assets"][0]["digest"] = "sha256:" + "0" * 64
        with self.assertRaisesRegex(ValueError, "Payload SHA256 mismatch"):
            validate_assets(self.metadata, self.checksums)

    def test_corrupt_downloaded_checksum_blocks_publication(self):
        (self.checksums / (PAYLOADS[0] + ".sha256")).write_text("corrupt", encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "Downloaded checksum"):
            validate_assets(self.metadata, self.checksums)

    def test_table_only_links_supported_payloads(self):
        table = download_table("v0.7.0")
        self.assertEqual(5, table.count("[Download]"))
        self.assertIn("| Linux |", table)
        self.assertTrue(table.endswith("| — |"))
        self.assertIn("/v0.7.0/MarkMello-setup-win-arm64.exe", table)

    def test_notes_include_summary_version_and_source(self):
        rendered = notes.render_notes("v0.7.0", "a" * 40, "Approved release summary.")
        self.assertTrue(rendered.startswith("MarkMello 0.7.0.\n\nApproved release summary."))
        self.assertIn("Source commit: " + "a" * 40, rendered)
        for tag, commit in (("../unsafe", "a" * 40), ("v0.7.0", "main")):
            with self.subTest(tag=tag, commit=commit), self.assertRaises(ValueError):
                notes.render_notes(tag, commit, "Summary")

    def test_unsigned_release_notes_do_not_claim_windows_signing(self):
        rendered = notes.render_notes("v0.7.0", "a" * 40, "Summary", unsigned_windows=True)
        self.assertIn("Windows installers are unsigned", rendered)
        self.assertNotIn("signed through SignPath", rendered)
        self.assertIn("macOS builds use an ad-hoc signature", rendered)


if __name__ == "__main__":
    unittest.main()
