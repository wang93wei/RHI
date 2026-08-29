"""Cross-check Loc.ZhHans.cs against the extracted string inventory.

Parses the dictionary's C# source, decodes C# escape sequences in keys and
values, and compares against tools/localize/strings_extracted.json:
  - missing: extracted values with no dictionary entry (will stay English)
  - extra:   dictionary keys not in the extraction (typos / stale entries)
  - bad escape: any escape sequence we do not decode (compile/runtime risk)
  - untranslated: entries whose value equals the key
"""
import json
import re
from pathlib import Path

HERE = Path(__file__).resolve().parent
DICT = HERE.parents[1] / "RenoDXCommander" / "Loc.ZhHans.cs"

ENTRY = re.compile(r'\["((?:[^"\\\r\n]|\\.)*)"\] = "((?:[^"\\\r\n]|\\.)*)"')
ESCAPES = {"n": "\n", "r": "\r", "t": "\t", '"': '"', "\\": "\\", "'": "'", "0": "\0"}

def decode(s: str) -> str:
    out = []
    i = 0
    while i < len(s):
        if s[i] == "\\":
            if i + 1 >= len(s):
                raise ValueError("dangling backslash")
            esc = s[i + 1]
            if esc not in ESCAPES:
                raise ValueError(f"unsupported escape \\{esc}")
            out.append(ESCAPES[esc])
            i += 2
        else:
            out.append(s[i])
            i += 1
    return "".join(out)


def main() -> int:
    text = DICT.read_text(encoding="utf-8")
    entries = {}
    bad_escapes = []
    for match in ENTRY.finditer(text):
        try:
            entries[decode(match.group(1))] = decode(match.group(2))
        except ValueError as ex:
            bad_escapes.append(f"{ex}: {match.group(0)[:80]}")

    extracted = json.loads((HERE / "strings_extracted.json").read_text(encoding="utf-8"))
    # C# findings store the SOURCE literal (backslash-n etc.); decode them to
    # the runtime value the dictionary keys must match. XAML values carry no
    # C# escapes, so decoding is a no-op for them.
    wanted = set()
    for v in extracted["values"]:
        try:
            wanted.add(decode(v["value"]))
        except ValueError as ex:
            print(f"cannot decode extracted value ({ex}): {v['value']!r}")
            wanted.add(v["value"])

    missing = sorted(wanted - entries.keys())
    extra = sorted(entries.keys() - wanted)
    untranslated = sorted(k for k, v in entries.items() if k == v)

    print(f"dictionary entries: {len(entries)}")
    print(f"extracted values:  {len(wanted)}")
    print(f"coverage:          {len(wanted & entries.keys())}/{len(wanted)}")
    print(f"bad escapes:       {len(bad_escapes)}")
    for b in bad_escapes:
        print("  ", b)
    print(f"missing ({len(missing)}):")
    for m in missing:
        print("  ", repr(m))
    print(f"extra ({len(extra)}):")
    for e in extra:
        print("  ", repr(e))
    print(f"untranslated (value == key, {len(untranslated)}):")
    for u in untranslated:
        print("  ", repr(u))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
