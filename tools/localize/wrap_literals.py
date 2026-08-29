#!/usr/bin/env python3
"""Localisation prep for RHI (RenoDXCommander).

Mode "extract" (default): collect every user-facing English string literal
that will flow through Loc.Tr() — display-property literals in C#, XAML
attribute literals, and UIFactory helper first-arg literals — and dump them
as JSON for dictionary authoring. Files are not modified.

Mode "apply": rewrite C# files, wrapping display-property literals with
Loc.Tr(...). Interpolated ($"..."), verbatim (@"..."), concatenated and
multi-part expressions are deliberately skipped — Loc.Tr falls back to the
original text at runtime, so untranslated paths simply stay English.

Selection-vocabulary literals (combo values mapped back to INI keys by
string comparison) are never wrapped; see SKIP_VALUES.
"""
from __future__ import annotations

import json
import re
import sys
from html import unescape
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
APP_DIR = REPO / "RenoDXCommander"

# Display properties whose string-literal assignments are pure UI text.
PROPS = [
    "Text", "Content", "Header", "PlaceholderText", "Title", "Subtitle",
    "Description", "Message", "Label", "PrimaryButtonText",
    "SecondaryButtonText", "CloseButtonText", "ButtonText",
    "OnContent", "OffContent",
]

# Combo-box vocabulary that selection handlers map back to INI keys or
# compare against. Wrapping any of these would break write-back logic.
SKIP_VALUES = {
    "Press a key...",  # compared against TextBox.Text in hotkey capture
    "None (Real DLSSG)", "Nukem's", "Enabler", "FSR 3/4 FG",
    "OptiFG (Upscaler)", "DLSSG via Streamline", "DLSSG via Nvngx",
    "FSR 3.1 FG", "FSR 3.0 FG", "FSR FG", "DLSSG", "XeFG",
    "Auto (Default)", "Stable", "Nightly",
    "DX11", "DX12", "Vulkan",
}

EXCLUDE_FILES = {"Loc.cs", "Loc.ZhHans.cs"}

PROP_PATTERN = re.compile(
    r'(?<![A-Za-z0-9_])((?:' + "|".join(PROPS) + r')\s*=\s*)"((?:[^"\\\r\n]|\\.)*)"'
)
TOOLTIP_PATTERN = re.compile(
    r'(ToolTipService\.SetToolTip\(\s*(?:[^(),"]|\([^()]*\))+,\s*)"((?:[^"\\\r\n]|\\.)*)"'
)
# UIFactory funnels translate at runtime; their literal args only need
# dictionary entries, not call-site wrapping.
FUNNEL_PATTERN = re.compile(
    r'\b(MakeLabel|MakeActionButton|MakeStatusDot)\(\s*"((?:[^"\\\r\n]|\\.)*)"'
)
# A display-property assignment whose first segment ends with "+" opens a
# multi-line concatenation; plain-literal continuation fragments can be
# wrapped individually (interpolated $"..." fragments cannot).
CONCAT_HEAD_PATTERN = re.compile(
    r'(?<![A-Za-z0-9_])(?:' + "|".join(PROPS) + r')\s*=\s*(?:Loc\.Tr\("(?:[^"\\\r\n]|\\.)*"\)|\$?"(?:[^"\\\r\n]|\\.)*")\s*\+\s*$'
)
CONCAT_FRAGMENT_PATTERN = re.compile(r'^(\s*)"((?:[^"\\\r\n]|\\.)*)"(.*)$')
XAML_ATTR_PATTERN = re.compile(
    r'\b(Text|Content|Header|PlaceholderText|Title|ToolTipService\.ToolTip)="([^"{}]*)"'
)


def has_ascii_letter(value: str) -> bool:
    return any(c.isascii() and c.isalpha() for c in value)


def line_prefix(text: str, start: int) -> str:
    line_start = text.rfind("\n", 0, start) + 1
    return text[line_start:start]


def is_wrappable(value: str) -> bool:
    return has_ascii_letter(value) and value not in SKIP_VALUES


def collect_cs(path: Path, findings: list[dict]) -> str | None:
    """Extract findings and (in apply mode) return rewritten text.

    Each pattern runs as its own full pass over the current text: a shared
    output cursor across interleaved finditer loops would duplicate code when
    a later pattern matches before an earlier pattern's cursor position.
    """
    text = path.read_text(encoding="utf-8", newline="")
    rel = str(path.relative_to(REPO)).replace("\\", "/")
    changed = False

    def run_pass(pattern: re.Pattern, kind: str) -> None:
        nonlocal text, changed
        rewritten: list[str] = []
        last = 0
        for match in pattern.finditer(text):
            prefix = line_prefix(text, match.start())
            if "//" in prefix or "/*" in prefix:
                continue
            value = match.group(2)
            if not is_wrappable(value):
                continue
            lineno = text.count("\n", 0, match.start()) + 1
            findings.append({
                "kind": kind,
                "prop": match.group(1).split()[0].rstrip(" =(,"),
                "value": value,
                "where": f"{rel}:{lineno}",
            })
            if kind == "cs":
                rewritten.append(text[last:match.start()])
                rewritten.append(f'{match.group(1)}Loc.Tr("{value}")')
                last = match.end()
                changed = True
        if last:
            rewritten.append(text[last:])
            text = "".join(rewritten)

    def run_concat_pass() -> None:
        """Wrap plain-literal fragments of display-property concatenations."""
        nonlocal text, changed
        lines = text.splitlines(keepends=True)
        in_concat = False
        for index, line in enumerate(lines):
            line_body = line.rstrip("\r\n")
            if in_concat:
                match = CONCAT_FRAGMENT_PATTERN.match(line_body)
                if match and is_wrappable(match.group(2)):
                    value = match.group(2)
                    findings.append({
                        "kind": "cs",
                        "prop": "concat-fragment",
                        "value": value,
                        "where": f"{rel}:{index + 1}",
                    })
                    tail = match.group(3)
                    lines[index] = f'{match.group(1)}Loc.Tr("{value}"){tail}' + line[len(line_body):]
                    changed = True
                    in_concat = tail.rstrip().endswith("+")
                    continue
                # Non-literal fragment (interpolated, method call, variable):
                # keep the state while the expression continues with "+".
                stripped = line_body.rstrip()
                if stripped.endswith("+"):
                    continue
                in_concat = False
                continue
            if CONCAT_HEAD_PATTERN.search(line_body):
                in_concat = True
        text = "".join(lines)

    run_pass(PROP_PATTERN, "cs")
    run_pass(TOOLTIP_PATTERN, "cs")
    run_concat_pass()
    run_pass(FUNNEL_PATTERN, "funnel")

    return text if changed else None


def collect_xaml(path: Path, findings: list[dict]) -> None:
    text = path.read_text(encoding="utf-8", newline="")
    rel = str(path.relative_to(REPO)).replace("\\", "/")
    # Skip XML comments.
    text_scrub = re.sub(r"<!--.*?-->", lambda m: " " * len(m.group(0)), text, flags=re.S)
    for match in XAML_ATTR_PATTERN.finditer(text_scrub):
        # XAML entities (&amp; etc.) are decoded by the parser before the
        # value reaches the runtime property — the dictionary key must match
        # the decoded form, which is what the tree walker sees.
        value = unescape(match.group(2))
        if not is_wrappable(value):
            continue
        lineno = text_scrub.count("\n", 0, match.start()) + 1
        findings.append({"kind": "xaml", "prop": match.group(1), "value": value,
                         "where": f"{rel}:{lineno}"})


def main() -> int:
    mode = sys.argv[1] if len(sys.argv) > 1 else "extract"
    findings: list[dict] = []

    cs_files = sorted(
        p for p in APP_DIR.rglob("*.cs")
        if "obj" not in p.parts and "bin" not in p.parts
        and ".zcode" not in p.parts and p.name not in EXCLUDE_FILES
    )
    for path in cs_files:
        new_text = collect_cs(path, findings)
        if mode == "apply" and new_text is not None:
            path.write_text(new_text, encoding="utf-8", newline="")

    for path in sorted(APP_DIR.rglob("*.xaml")):
        if "obj" in path.parts or "bin" in path.parts:
            continue
        collect_xaml(path, findings)

    # Deduplicate by value; keep first location for reference.
    unique: dict[str, dict] = {}
    for finding in findings:
        entry = unique.setdefault(finding["value"], {
            "value": finding["value"], "count": 0,
            "kinds": set(), "where": finding["where"],
        })
        entry["count"] += 1
        entry["kinds"].add(finding["kind"])

    out = {
        "total_occurrences": len(findings),
        "unique_values": len(unique),
        "values": sorted(
            ({**e, "kinds": sorted(e["kinds"])} for e in unique.values()),
            key=lambda e: e["value"].lower(),
        ),
    }
    out_path = REPO / "tools" / "localize" / "strings_extracted.json"
    out_path.write_text(json.dumps(out, ensure_ascii=False, indent=1), encoding="utf-8")
    print(f"mode={mode} occurrences={out['total_occurrences']} unique={out['unique_values']}")
    print(f"findings -> {out_path}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
