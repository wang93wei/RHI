#!/usr/bin/env python3
"""
Check i18n coverage: compare en-US.json against other languages.
Usage: python3 tools/check-i18n-coverage.py [--strict]
Exit code 0 always, but prints coverage and missing keys.
--strict will exit 1 if any language < 50% coverage.
"""
import json, pathlib, sys

base = pathlib.Path("RenoDXCommander/Assets/Languages")
en_path = base / "en-US.json"
if not en_path.exists():
    print(f"[check-i18n] Missing {en_path}")
    sys.exit(1)

en_data = json.loads(en_path.read_text(encoding="utf-8"))
total = len(en_data)
print(f"[check-i18n] en-US baseline: {total} keys")

langs = ["zh-CN", "zh-TW", "ja-JP", "ko-KR"]
strict = "--strict" in sys.argv
has_low = False

for lang in langs:
    p = base / f"{lang}.json"
    if not p.exists():
        print(f"[check-i18n] {lang}: MISSING FILE")
        has_low = True
        continue
    try:
        data = json.loads(p.read_text(encoding="utf-8"))
    except Exception as e:
        print(f"[check-i18n] {lang}: JSON error {e}")
        has_low = True
        continue
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

if strict and has_low:
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
        if "{Binding" in val or "{x:Bind" in val or val.strip() in ("RHI", "+", "✕", "⚙", "◀", ""):
            continue
        if val.strip().startswith(" by ") or "Licence" in val or "·" in val:
            continue
        hard.append(val)
    if hard:
        print(f"[check-i18n] Hard-coded XAML strings remaining: {len(hard)}")
        for h in hard[:10]:
            print(f"  {h!r}")
    else:
        print(f"[check-i18n] No hard-coded XAML strings (good)")

print("[check-i18n] Done")
