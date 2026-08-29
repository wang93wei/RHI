"""Verify wrap_literals.py apply results without a C# compiler.

Per modified file, compare the multiset of diff-removed lines against the
multiset of wrapper-stripped diff-added lines. They must be equal, proving
every added line is exactly an old line with Loc.Tr(...) around a string
literal and nothing else changed. Diff-pairing order does not matter.

Files with hand-written edits (App/MainWindow/SetupWindow/UIFactory wiring)
are skipped here and reviewed by eye instead.
"""
import re
import subprocess
import sys

TR_CALL = re.compile(r'Loc\.Tr\("(?:[^"\\\r\n]|\\.)*"\)')
MANUAL_FILES = {
    "RenoDXCommander/App.xaml.cs",
    "RenoDXCommander/MainWindow.xaml.cs",
    "RenoDXCommander/SetupWindow.xaml.cs",
    "RenoDXCommander/UIFactory.cs",
}


def strip_wrappers(line: str) -> str:
    return TR_CALL.sub(lambda m: m.group(0)[len("Loc.Tr("):-1], line)


def main() -> int:
    diff = subprocess.run(
        ["git", "diff", "-U0", "--", "RenoDXCommander"],
        capture_output=True, text=True, encoding="utf-8", check=True,
    ).stdout

    from collections import Counter

    file_name = None
    removed: Counter = Counter()
    added: Counter = Counter()
    failures = 0
    checked_files = 0

    def flush() -> None:
        nonlocal failures, checked_files, removed, added
        if file_name is None or file_name in MANUAL_FILES:
            removed, added = Counter(), Counter()
            return
        if not removed and not added:
            return
        checked_files += 1
        stripped_added = Counter({strip_wrappers(l): n for l, n in added.items()})
        # Lines that survived stripping unchanged (no wrapper) must be pure
        # additions (manual edits) — not expected in script-touched files.
        if stripped_added != removed:
            failures += 1
            print(f"MISMATCH {file_name}")
            only_old = removed - stripped_added
            only_new = stripped_added - removed
            for line, n in only_old.items():
                print(f"  old-only x{n}: {line!r}")
            for line, n in only_new.items():
                print(f"  new-only x{n}: {line!r}")
        removed, added = Counter(), Counter()

    for line in diff.splitlines():
        if line.startswith("+++ b/"):
            flush()
            file_name = line[6:]
            continue
        if line.startswith("---"):
            continue
        if line.startswith("-"):
            removed[line[1:]] += 1
        elif line.startswith("+"):
            added[line[1:]] += 1
    flush()

    print(f"checked {checked_files} script-modified files, {failures} mismatches")
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
