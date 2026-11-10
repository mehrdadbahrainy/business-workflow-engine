"""Regression tests for the UTF-8/mojibake quality gate."""
import importlib.util
from pathlib import Path
import subprocess
import tempfile
import unittest

SCRIPT = Path(__file__).resolve().parents[2] / "scripts" / "check_text_encoding.py"
spec = importlib.util.spec_from_file_location("check_text_encoding", SCRIPT)
guard = importlib.util.module_from_spec(spec)
spec.loader.exec_module(guard)


class TextEncodingTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        subprocess.run(["git", "init", "-q", str(self.root)], check=True)

    def tracked(self, name, content):
        target = self.root / name
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(content)
        subprocess.run(["git", "-C", str(self.root), "add", name], check=True)

    def test_correct_utf8_and_unicode_arrows_are_allowed(self):
        self.tracked("page.html", "<p>↓ – Café</p>\n".encode("utf-8"))
        n, issues = guard.check(self.root)
        self.assertEqual(n, 1)
        self.assertEqual(issues, [])

    def test_utf8_bytes_interpreted_as_windows1252_are_rejected(self):
        corrupted = "".join(chr(cp) for cp in (0x00e2, 0x2020, 0x201c))
        self.tracked("page.html", f"<span>{corrupted}</span>\n".encode("utf-8"))
        _, issues = guard.check(self.root)
        self.assertEqual(len(issues), 1)
        self.assertIn("page.html:1", issues[0])

    def test_replacement_character_is_rejected(self):
        self.tracked("README.md", ("Header\n" + chr(0xfffd)).encode("utf-8"))
        _, issues = guard.check(self.root)
        self.assertEqual(len(issues), 1)

    def test_non_utf8_bytes_fail(self):
        self.tracked("layout.scss", b".icon " + bytes([0xff]))
        _, issues = guard.check(self.root)
        self.assertEqual(len(issues), 1)
        self.assertIn("invalid UTF-8", issues[0])

    def test_untracked_artifacts_are_ignored(self):
        self.tracked("valid.cs", b"class Valid {}")
        (self.root / "scratch.html").write_bytes(bytes([0xff]))
        _, issues = guard.check(self.root)
        self.assertEqual(issues, [])


if __name__ == "__main__":
    unittest.main()
