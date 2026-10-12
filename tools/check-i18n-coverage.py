#!/usr/bin/env python3
"""
Check i18n coverage: compare en-US.json against other languages.
Usage: python3 tools/check-i18n-coverage.py [--strict]
Exits 1 for case-insensitive duplicate keys, which break the runtime catalog loader.
--strict also requires identical keys, nonempty string values and format indices.
"""
import json
import pathlib
import re
import sys
from collections import Counter


def load_catalog(path):
    raw = path.read_text(encoding="utf-8")
    if not raw.lstrip().startswith("{"):
        raise ValueError("catalog must be a JSON object")
    pairs = json.loads(raw, object_pairs_hook=lambda items: items)
    seen = {}
    duplicates = []
    for key, _ in pairs:
        normalized = key.casefold()
        if normalized in seen:
            duplicates.append((seen[normalized], key))
        else:
            seen[normalized] = key
    return dict(pairs), duplicates


def report_duplicates(label, duplicates):
    if not duplicates:
        return
    print(f"[check-i18n] {label}: CASE-INSENSITIVE DUPLICATE KEYS")
    for first, second in duplicates:
        print(f"  {first} <=> {second}")


FORMAT_ITEM = re.compile(r"\{([0-9]+) *(?:, *-?[0-9]+ *)?(?::[^{}]*)?\}")


def format_indices(value):
    """Count .NET composite format indices; escaped braces are literal text."""
    indices = Counter()
    pos = 0
    while pos < len(value):
        if value[pos:pos + 2] in ("{{", "}}"):
            pos += 2
            continue
        match = FORMAT_ITEM.match(value, pos)
        if match:
            indices[int(match.group(1))] += 1
            pos = match.end()
        else:
            pos += 1
    return indices


def main():
    base = pathlib.Path("RenoDXCommander/Assets/Languages")
    en_path = base / "en-US.json"
    if not en_path.exists():
        print(f"[check-i18n] Missing {en_path}")
        return 1
    try:
        en_data, en_duplicates = load_catalog(en_path)
    except Exception as e:
        print(f"[check-i18n] en-US: JSON error {e}")
        return 1
    report_duplicates("en-US", en_duplicates)
    total = len(en_data)
    print(f"[check-i18n] en-US baseline: {total} keys")

    langs = ["zh-CN", "zh-TW", "ja-JP", "ko-KR"]
    strict = "--strict" in sys.argv
    has_errors = False
    has_duplicates = bool(en_duplicates)
    invalid_en = [k for k, v in en_data.items() if not isinstance(v, str) or not v.strip()]
    if strict and invalid_en:
        print(f"[check-i18n] en-US: INVALID OR EMPTY VALUES: {', '.join(invalid_en)}")
        has_errors = True

    for lang in langs:
        p = base / f"{lang}.json"
        if not p.exists():
            print(f"[check-i18n] {lang}: MISSING FILE")
            has_errors = True
            continue
        try:
            data, duplicates = load_catalog(p)
        except Exception as e:
            print(f"[check-i18n] {lang}: JSON error {e}")
            has_errors = True
            continue
        report_duplicates(lang, duplicates)
        has_duplicates = has_duplicates or bool(duplicates)
        present = sum(1 for k in en_data if k in data and str(data[k]).strip() != "")
        coverage = present / total if total else 0
        missing = [k for k in en_data if k not in data or str(data[k]).strip() == ""]
        # Untranslated brands, filenames and protocol values remain report-only.
        untranslated = [k for k in en_data if k in data and data[k] == en_data[k]]
        print(f"[check-i18n] {lang}: {present}/{total} ({coverage:.1%})  untranslated: {len(untranslated)}")
        if missing:
            print(f"  Missing ({len(missing)}): {', '.join(missing[:10])}{' ...' if len(missing)>10 else ''}")
        if coverage < 0.5:
            print(f"  WARN: {lang} coverage < 50%")
        if strict:
            extra = sorted(data.keys() - en_data.keys())
            invalid = [k for k, v in data.items() if not isinstance(v, str) or not v.strip()]
            mismatches = [
                k for k in en_data if k in data
                and isinstance(en_data[k], str) and isinstance(data[k], str)
                and format_indices(en_data[k]) != format_indices(data[k])
            ]
            if extra:
                print(f"  EXTRA KEYS ({len(extra)}): {', '.join(extra)}")
            if invalid:
                print(f"  INVALID OR EMPTY VALUES ({len(invalid)}): {', '.join(invalid)}")
            for key in mismatches:
                print(f"  FORMAT INDEX MISMATCH {key}: en-US={dict(format_indices(en_data[key]))}, {lang}={dict(format_indices(data[key]))}")
            has_errors = has_errors or bool(missing or extra or invalid or mismatches)

    if has_duplicates or (strict and has_errors):
        return 1

    # Also report hard-coded strings in MainWindow.xaml.
    xaml = pathlib.Path("RenoDXCommander/MainWindow.xaml")
    if xaml.exists():
        text = xaml.read_text(encoding="utf-8")
        pattern = re.compile(r'(Text|Content|PlaceholderText)\s*=\s*"([^"]+)"')
        hard = []
        for m in pattern.finditer(text):
            val = m.group(2)
            stripped = val.strip()
            if "{Binding" in val or "{x:Bind" in val or stripped in ("RHI", "+", "✕", "⚙", "◀", "▶", "↺", ""):
                continue
            # Attribution / licence / brand fragments are intentionally not translated per R3.4.
            if stripped.startswith("by ") or stripped.startswith(" by ") or "Licence" in val or "·" in val or "github.com" in val or "Copyright" in val:
                continue
            if len(stripped) <= 2 and not stripped[0].isalpha():
                continue
            hard.append(val)
        if hard:
            print(f"[check-i18n] Hard-coded XAML strings remaining: {len(hard)}")
            for h in hard[:10]:
                print(f"  {h!r}")
        else:
            print("[check-i18n] No hard-coded XAML strings (good)")

    print("[check-i18n] Done")
    return 0


if __name__ == "__main__":
    sys.exit(main())
