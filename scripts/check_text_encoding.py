#!/usr/bin/env python3
"""Reject malformed UTF-8 and common mojibake in Git-tracked text files.

A defensive guard for accidental Latin-1 / Windows-1252 misdecoding of UTF-8.
Tracks only known text formats; excludes generated build output and binary files.
"""
import argparse
import re
import subprocess
import sys
from pathlib import Path

EXTENSIONS = {
    ".md", ".txt", ".html", ".htm", ".css", ".scss", ".ts", ".tsx", ".js",
    ".jsx", ".mjs", ".cjs", ".json", ".jsonc", ".yml", ".yaml", ".xml",
    ".cs", ".csproj", ".fs", ".fsproj", ".vb", ".vbproj", ".py", ".sln",
    ".slnx", ".sql", ".sh", ".ps1", ".http", ".svg", ".properties",
    ".conf", ".editorconfig", ".gitattributes", ".gitignore",
    ".env", ".example",
}
TEXT_FILENAMES = {"Dockerfile", "Makefile", ".env.example", ".dockerignore"}
# Escaped code points keep the guard's own source free of corrupted characters.
# Matches typical markers from UTF-8 decoded incorrectly as Windows-1252.
CORRUPTED = re.compile(
    r"\ufffd"
    r"|\u00c3[\u0080-\u00ff\u2010-\u203a]"
    r"|\u00c2[\u0080-\u00bf\u00a0]"
    r"|\u00e2[\u0080-\u00bf\u2010-\u203a\u20ac-\u2122]"
    r"|\u00ef[\u0080-\u00ff][\u0080-\u00ff\u2010-\u203a]"
    r"|\u00f0[\u0080-\u00ff\u2010-\u203a]"
)


def tracked_paths(root: Path):
    cmd = subprocess.run(
        ["git", "-C", str(root), "ls-files", "-z", "--cached"],
        capture_output=True, check=True,
    )
    for raw in cmd.stdout.split(b"\0"):
        if raw:
            path = Path(raw.decode("utf-8", errors="strict"))
            if path.suffix.lower() in EXTENSIONS or path.name in TEXT_FILENAMES:
                yield path


def check(root: Path):
    issues = []
    checked = 0
    for relative in tracked_paths(root):
        file = root / relative
        if not file.is_file():
            continue
        raw = file.read_bytes()
        checked += 1
        try:
            text = raw.decode("utf-8", errors="strict")
        except UnicodeDecodeError as exc:
            issues.append(f"{relative}: invalid UTF-8 at byte {exc.start}")
            continue
        for line_number, line in enumerate(text.splitlines(), 1):
            if CORRUPTED.search(line):
                issues.append(
                    f"{relative}:{line_number}: probable mojibake or replacement character"
                )
    return checked, issues


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[1])
    args = parser.parse_args()
    root = args.root.resolve()
    try:
        scanned, issues = check(root)
    except (OSError, subprocess.CalledProcessError, UnicodeError) as exc:
        print(f"Text encoding scan failed: {exc}", file=sys.stderr)
        return 2
    for issue in issues:
        print(f"ERROR: {issue}", file=sys.stderr)
    if issues:
        print(f"Text encoding check failed: {len(issues)} issue(s) in {scanned} files.")
        return 1
    print(f"Text encoding OK: scanned {scanned} tracked text files.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
