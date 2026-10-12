"""Regression cases for the catalog CI gate. Run on the Windows CI runner."""
import importlib.util
import json
import pathlib
import subprocess
import sys
import tempfile
import unittest


SCRIPT = pathlib.Path(__file__).resolve().parents[1] / "check-i18n-coverage.py"
SPEC = importlib.util.spec_from_file_location("i18n_coverage", SCRIPT)
coverage = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(coverage)


class FormatIndicesTests(unittest.TestCase):
    def test_alignment_format_and_reordering(self):
        self.assertEqual(
            coverage.format_indices("{0,-12:N2} / {1:yyyy-MM-dd} / {0, 8}"),
            coverage.format_indices("{1} / {0} / {0}"),
        )

    def test_escaped_braces_and_normal_punctuation(self):
        self.assertEqual(coverage.format_indices("{{0}} {{name}} {说明}，。"), {})
        self.assertEqual(coverage.format_indices("{{{0:D}}} {{text}} {1}"), {0: 1, 1: 1})


class StrictCatalogTests(unittest.TestCase):
    def run_catalogs(self, english, translated, strict=True, raw_translation=None):
        with tempfile.TemporaryDirectory() as temp:
            base = pathlib.Path(temp) / "RenoDXCommander/Assets/Languages"
            base.mkdir(parents=True)
            (base / "en-US.json").write_text(json.dumps(english), encoding="utf-8")
            for lang in ("zh-CN", "zh-TW", "ja-JP", "ko-KR"):
                raw = raw_translation if raw_translation is not None else json.dumps(translated)
                (base / f"{lang}.json").write_text(raw, encoding="utf-8")
            return subprocess.run(
                [sys.executable, str(SCRIPT)] + (["--strict"] if strict else []),
                cwd=temp, capture_output=True, text=True, encoding="utf-8",
            )

    def test_matching_catalogs_pass(self):
        result = self.run_catalogs({"A": "{{{0:N2}}} / {1}"}, {"A": "{1}：{{{0,8}}}"})
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)

    def test_missing_key_fails_even_above_old_threshold(self):
        result = self.run_catalogs({"A": "one", "B": "two", "C": "three"}, {"A": "甲", "B": "乙"})
        self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
        self.assertIn("Missing (1): C", result.stdout)

    def test_extra_key_fails(self):
        result = self.run_catalogs({"A": "one"}, {"A": "甲", "B": "乙"})
        self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
        self.assertIn("EXTRA KEYS (1): B", result.stdout)

    def test_changed_missing_or_repeated_index_fails(self):
        for value in ("{1}", "{{0}}", "{0} {0}"):
            with self.subTest(value=value):
                result = self.run_catalogs({"A": "{0}"}, {"A": value})
                self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
                self.assertIn("FORMAT INDEX MISMATCH A", result.stdout)

    def test_empty_or_nonstring_value_fails(self):
        for value in (" ", None, 12, {"nested": "text"}):
            with self.subTest(value=value):
                result = self.run_catalogs({"A": "one"}, {"A": value})
                self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
                self.assertIn("INVALID OR EMPTY VALUES", result.stdout)

    def test_duplicates_fail_in_both_modes(self):
        for strict in (False, True):
            with self.subTest(strict=strict):
                result = self.run_catalogs({"A": "one"}, {}, strict, '{"A":"甲","a":"乙"}')
                self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
                self.assertIn("CASE-INSENSITIVE DUPLICATE KEYS", result.stdout)

    def test_invalid_catalog_fails(self):
        for raw in ("[]", "{"):
            with self.subTest(raw=raw):
                result = self.run_catalogs({"A": "one"}, {}, raw_translation=raw)
                self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
                self.assertIn("JSON error", result.stdout)

    def test_nonstrict_remains_report_only(self):
        result = self.run_catalogs({"A": "{0}", "B": "two"}, {"A": "{1}", "Extra": "text"}, False)
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertIn("Missing (1): B", result.stdout)


if __name__ == "__main__":
    unittest.main()
