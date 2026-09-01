#!/usr/bin/env python3
"""
Check i18n coverage: compare en-US.json against other languages.
Usage: python3 tools/check-i18n-coverage.py [--strict]
Exits 1 for case-insensitive duplicate keys, which break the runtime catalog loader.
--strict will exit 1 if any language < 50% coverage.
"""
import json, pathlib, sys


def load_catalog(path):
    pairs = json.loads(
        path.read_text(encoding="utf-8"),
        object_pairs_hook=lambda items: items,
    )
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

base = pathlib.Path("RenoDXCommander/Assets/Languages")
en_path = base / "en-US.json"
if not en_path.exists():
    print(f"[check-i18n] Missing {en_path}")
    sys.exit(1)

en_data, en_duplicates = load_catalog(en_path)
report_duplicates("en-US", en_duplicates)
total = len(en_data)
print(f"[check-i18n] en-US baseline: {total} keys")

langs = ["zh-CN", "zh-TW", "ja-JP", "ko-KR"]
strict = "--strict" in sys.argv
has_low = False
has_duplicates = bool(en_duplicates)

for lang in langs:
    p = base / f"{lang}.json"
    if not p.exists():
        print(f"[check-i18n] {lang}: MISSING FILE")
        has_low = True
        continue
    try:
        data, duplicates = load_catalog(p)
    except Exception as e:
        print(f"[check-i18n] {lang}: JSON error {e}")
        has_low = True
        continue
    report_duplicates(lang, duplicates)
    has_duplicates = has_duplicates or bool(duplicates)
    present = sum(1 for k in en_data if k in data and str(data[k]).strip() != "")
    coverage = present / total if total else 0
    missing = [k for k in en_data if k not in data or str(data[k]).strip() == ""]
    # Check for untranslated (value == en-US)
    untranslated = [k for k in en_data if k in data and data[k] == en_data[k]]
    print(f"[check-i18n] {lang}: {present}/{total} ({coverage:.1%})  untranslated: {len(untranslated)}")
    if missing:
        print(f"  Missing ({len(missing)}): {', '.join(missing[:10])}{' ...' if len(missing)>10 else ''}")
    if coverage < 0.5:
        has_low = True
        print(f"  WARN: {lang} coverage < 50%")

if has_duplicates or (strict and has_low):
    sys.exit(1)

# Also check hard-coded strings in MainWindow.xaml
xaml = pathlib.Path("RenoDXCommander/MainWindow.xaml")
if xaml.exists():
    import re
    text = xaml.read_text(encoding="utf-8")
    # Find Text="..." not containing {Binding or {x:Bind
    pattern = re.compile(r'(Text|Content|PlaceholderText)\s*=\s*"([^"]+)"')
    hard = []
    for m in pattern.finditer(text):
        val = m.group(2)
        stripped = val.strip()
        if "{Binding" in val or "{x:Bind" in val or stripped in ("RHI", "+", "✕", "⚙", "◀", "▶", "↺", ""):
            continue
        # Attribution / licence / brand fragments are intentionally not translated per R3.4
        if stripped.startswith("by ") or stripped.startswith(" by ") or "Licence" in val or "·" in val or "github.com" in val or "Copyright" in val:
            continue
        # Pure symbols / single-char decorative
        if len(stripped) <= 2 and not stripped[0].isalpha():
            continue
        hard.append(val)
    if hard:
        print(f"[check-i18n] Hard-coded XAML strings remaining: {len(hard)}")
        for h in hard[:10]:
            print(f"  {h!r}")
    else:
        print(f"[check-i18n] No hard-coded XAML strings (good)")

print("[check-i18n] Done")
