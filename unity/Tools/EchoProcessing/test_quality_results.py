# Copyright 2026 Roni Tervo
# SPDX-License-Identifier: Apache-2.0
"""Offline checks for evidence corruption and per-case quality regressions."""
import copy
import json
from pathlib import Path
import tempfile
import unittest

from QualityResults import (GROUPS, METRIC_CASES, PROFILES, expected_cases,
                            load_baseline, validate_group, validate_metric_contracts)

BASELINE = Path(__file__).with_name("aligned-quality-baseline.json")


def fixture(group="OverlappingSpeech", profile="measured32"):
    """A deliberately simple synthetic report, never acoustic evidence."""
    passing = load_baseline(BASELINE, profile)[group]
    rows = []
    if group != "OverlappingSpeech":
        rows.append(dict(pattern=GROUPS[group][2], measurementStartSeconds=11 if group == "WarmTonalPathChange" else 8,
                         measurementEndSeconds=17))
    for identity, (pattern, near, clock) in expected_cases(group).items():
        row = dict(zip(("capturePpm", "renderPpm", "timestampJitterUs"), clock))
        row.update(echoReductionDb=20 if identity in passing else 0, nearCorrelation=.8,
                   nearGain=1, baselineGain=1, maxDelayErrorMs=.6, clipped=0,
                   status="passed" if identity in passing else "failed")
        if group == "OverlappingSpeech":
            windows = (6 if pattern.endswith("path-change") else 9) if near == "bursty-overlapping-voiced" else 0
            row.update(pattern=pattern, nearSignal=near, onsetGain=1, onsetCorrelation=1,
                       onsetBaselineGain=1, onsetWindows=windows, onsetMaximumGain=1 if windows else 0,
                       onsetMaximumBaselineGain=1 if windows else 0)
        rows.append(row)
    failures = len(expected_cases(group)) - len(passing)
    rows.append(dict(status="failed" if failures else "passed", scope=GROUPS[group][1],
                     qualityCases=len(expected_cases(group)), failures=failures, physicalAudio=False, deviceOpened=False))
    if group == "OverlappingSpeech":
        rows[-1]["onsetWindows"] = 72
    return rows


def encode(rows):
    return "\n".join(json.dumps(row) for row in rows) + "\n"


def validate(rows, group="OverlappingSpeech", profile="measured32", **kwargs):
    return validate_group(encode(rows), group, int(rows[-1]["failures"] != 0),
                          load_baseline(BASELINE, profile)[group], **kwargs)


class QualityResultsTests(unittest.TestCase):
    def test_all_three_profile_matrices_preserve_open_failures(self):
        for profile, pass_count in zip(PROFILES, (13, 3, 8)):
            details = [validate(fixture(group, profile), group, profile) for group in GROUPS]
            self.assertEqual(sum(len(row["validatedCaseIds"]) for row in details), 39)
            self.assertEqual(sum(len(row["passingCaseIds"]) for row in details), pass_count)
            self.assertEqual(sum(row["failures"] for row in details), 39 - pass_count)

    def test_missing_duplicate_and_unknown_case_rejected(self):
        for mutation in (lambda rows: rows.pop(0), lambda rows: rows.__setitem__(1, copy.deepcopy(rows[0])),
                         lambda rows: rows[0].update(nearSignal="different-voice"),
                         lambda rows: rows[0].update(pattern="different-pattern")):
            with self.subTest(mutation=mutation):
                rows = fixture(); mutation(rows)
                with self.assertRaises(ValueError):
                    validate(rows)

    def test_clock_identity_requires_exact_integer_tuple(self):
        for value in (True, 0.0, None, "0", float("nan"), float("inf"), 901):
            with self.subTest(value=value):
                rows = fixture(); rows[0]["capturePpm"] = value
                with self.assertRaises(ValueError):
                    validate(rows)

    def test_all_metrics_are_required_finite_numbers(self):
        fields = ("echoReductionDb", "nearCorrelation", "nearGain", "baselineGain", "maxDelayErrorMs",
                  "onsetGain", "onsetCorrelation", "onsetBaselineGain", "onsetMaximumGain", "onsetMaximumBaselineGain")
        for field in fields:
            for value in (None, True, "1", float("nan"), float("inf"), -float("inf")):
                with self.subTest(field=field, value=value):
                    rows = fixture(); rows[0][field] = value
                    with self.assertRaises(ValueError):
                        validate(rows)

    def test_new_runner_requires_both_onset_maxima_but_legacy_import_does_not(self):
        rows = fixture()
        for row in rows[:-1]:
            del row["onsetMaximumGain"]; del row["onsetMaximumBaselineGain"]
        with self.assertRaises(ValueError):
            validate(rows)
        self.assertEqual(validate(rows, require_onset_maxima=False)["onsetWindows"], 72)
        rows[0]["onsetMaximumGain"] = 0
        with self.assertRaises(ValueError):
            validate(rows, require_onset_maxima=False)

    def test_amplified_onset_cannot_report_passing(self):
        for field in ("onsetMaximumGain", "onsetMaximumBaselineGain"):
            rows = fixture()
            row = next(row for row in rows[:-1] if row["status"] == "passed" and row["onsetWindows"] > 0)
            row[field] = 1.6
            with self.assertRaisesRegex(ValueError, "contradicts metrics"):
                validate(rows)

    def test_amplified_onset_failure_is_kept_open(self):
        rows = fixture()
        row = next(row for row in rows[:-1] if row["status"] == "failed" and row["onsetWindows"] > 0)
        row["echoReductionDb"] = 20
        row["onsetMaximumGain"] = 1.6
        self.assertEqual(validate(rows)["failures"], 24)

    def test_onsets_checked_per_case_not_only_total(self):
        rows = fixture()
        burst = [row for row in rows[:-1] if row["onsetWindows"] > 0]
        burst[0]["onsetWindows"] -= 1; burst[1]["onsetWindows"] += 1
        self.assertEqual(sum(row["onsetWindows"] for row in rows[:-1]), 72)
        with self.assertRaises(ValueError):
            validate(rows)

    def test_summary_failure_status_scope_counts_and_offline_flags_must_agree(self):
        for field, value in (("failures", 23), ("failures", True), ("qualityCases", 26), ("qualityCases", 27.0),
                             ("status", "passed"), ("scope", "other"), ("physicalAudio", 0),
                             ("deviceOpened", True), ("onsetWindows", 71)):
            with self.subTest(field=field):
                rows = fixture(); rows[-1][field] = value
                with self.assertRaises(ValueError):
                    validate(rows)

    def test_exit_code_must_match_all_rows(self):
        for code in (0, 2, True):
            with self.subTest(code=code), self.assertRaises(ValueError):
                validate_group(encode(fixture()), "OverlappingSpeech", code)

    def test_case_verdict_cannot_contradict_metrics(self):
        for reported in ("passed", "failed"):
            rows = fixture()
            row = next(row for row in rows[:-1] if row["status"] != reported)
            row["status"] = reported
            with self.assertRaisesRegex(ValueError, "contradicts metrics"):
                validate(rows)

    def test_new_failure_cannot_be_hidden_by_an_improvement_elsewhere(self):
        rows = fixture()
        passed = next(row for row in rows[:-1] if row["status"] == "passed")
        failed = next(row for row in rows[:-1] if row["status"] == "failed")
        passed.update(echoReductionDb=0, status="failed")
        failed.update(echoReductionDb=20, status="passed")
        self.assertEqual(sum(row["status"] == "failed" for row in rows[:-1]), rows[-1]["failures"])
        with self.assertRaisesRegex(ValueError, "Previously passing"):
            validate(rows)

    def test_improvements_do_not_require_preserving_old_failures(self):
        rows = fixture()
        failed = next(row for row in rows[:-1] if row["status"] == "failed")
        failed.update(echoReductionDb=20, status="passed")
        rows[-1]["failures"] -= 1
        self.assertEqual(validate(rows)["failures"], 23)

    def test_each_profiles_previous_passes_are_protected(self):
        for profile in PROFILES:
            rows = fixture("Quality", profile)
            rows[1].update(echoReductionDb=0, status="failed")
            rows[-1].update(status="failed", failures=1)
            with self.subTest(profile=profile), self.assertRaisesRegex(ValueError, "Previously passing"):
                validate(rows, "Quality", profile)

    def test_wrong_pattern_window_and_malformed_json_rejected(self):
        rows = fixture("Quality"); rows[0]["measurementStartSeconds"] = 9
        with self.assertRaises(ValueError):
            validate(rows, "Quality")
        for text in ("[]", "null", "{invalid}", encode(fixture()) + '{}\n',
                     encode(fixture()).replace('"capturePpm": 0', '"capturePpm": 0, "capturePpm": 0', 1)):
            with self.subTest(text=text[:30]), self.assertRaises(ValueError):
                validate_group(text, "OverlappingSpeech", 1)

    def test_rounded_threshold_never_overrides_native_verdict(self):
        rows = fixture("Quality")
        rows[1]["nearGain"] = .45
        self.assertEqual(validate(rows, "Quality")["failures"], 0)
        rows[1]["status"] = "failed"; rows[-1].update(status="failed", failures=1)
        # No baseline here: the exact native value can lie just below .45 but
        # print .450000. A native failure is never upgraded by rounded metrics.
        self.assertEqual(validate_group(encode(rows), "Quality", 1)["failures"], 1)

    def test_baseline_requires_complete_profiles_groups_and_valid_unique_ids(self):
        original = json.loads(BASELINE.read_text())
        mutations = (lambda doc: doc["profiles"].pop("stock"),
                     lambda doc: doc["profiles"]["stock"].pop("Tonal"),
                     lambda doc: doc["profiles"]["stock"]["Quality"]["passingCaseIds"].append("unknown"),
                     lambda doc: doc["profiles"]["stock"]["Quality"].update(failures=True))
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "baseline.json"
            for mutation in mutations:
                doc = copy.deepcopy(original); mutation(doc); path.write_text(json.dumps(doc))
                with self.assertRaises(ValueError):
                    load_baseline(path, "stock")

    def test_metric_contracts_require_every_unique_passing_case(self):
        rows = [dict(case=name, status="passed") for name in sorted(METRIC_CASES)]
        rows.append(dict(status="passed", scope="clock-aligned-metric-contracts", metricCases=13, assertions=35,
                         physicalAudio=False, deviceOpened=False))
        self.assertEqual(validate_metric_contracts(encode(rows), 0)["metricCases"], 13)
        for mutation in (lambda data: data.pop(0), lambda data: data.__setitem__(1, data[0]),
                         lambda data: data[0].update(status="failed"), lambda data: data[-1].update(assertions=True),
                         lambda data: data[-1].update(metricCases=12), lambda data: data[-1].update(deviceOpened=True)):
            data = copy.deepcopy(rows); mutation(data)
            with self.assertRaises(ValueError):
                validate_metric_contracts(encode(data), 0)


if __name__ == "__main__":
    unittest.main()
