# Copyright 2026 Roni Tervo
# SPDX-License-Identifier: Apache-2.0
"""Corrupt/incomplete control evidence must never resemble a completed diagnostic."""
import copy
import json
import math
import unittest
from HistoryResults import CASES, QUIET_PREFIX_CASES, WINDOWS, SUMMARY, METRICS, validate_history, validate_metric_contracts


def fixture():
    rows = []
    for schedule, indexes in (("original", range(len(CASES))), ("quiet-before-six", QUIET_PREFIX_CASES)):
        for i in indexes:
            pattern, near, capture, render, jitter = CASES[i]
            for start, end in WINDOWS:
                score = dict(projectedGain=1, correlation=1, rmsRatio=1, differenceRmsRatio=0)
                rows.append(dict(caseIndex=i, schedule=schedule, pattern=pattern, nearSignal=near,
                    capturePpm=capture, renderPpm=render, timestampJitterUs=jitter,
                    sourceStartSeconds=start, sourceEndSeconds=end,
                    samples=round((end-start)*16000*(1+render/1e6)), baselineRms=.1, rawNearRms=.1,
                    echoReductionDb=10, control=score.copy(), mixed=score.copy(),
                    metadataParityFrames=1800, independentStreams=4, maxDelayErrorMs=.64,
                    clipped=[0, 0, 0, 0], status="recorded", speechQualityAccepted=False))
    return rows+[SUMMARY.copy()]


def encode(rows):
    return "\n".join(json.dumps(row) for row in rows)


class HistoryResultsTests(unittest.TestCase):
    def reject(self, mutate):
        rows = fixture()
        mutate(rows)
        with self.assertRaises(ValueError):
            validate_history(encode(rows), 0)

    def test_complete_diagnostic_is_never_quality_acceptance(self):
        result = validate_history(encode(fixture()), 0)
        self.assertEqual(result["validatedWindows"], 30)
        self.assertIs(result["speechQualityAccepted"], False)
        self.assertNotIn("passingCaseIds", result)

    def test_remaining_quiet_failures_have_exact_source_and_clock_controls(self):
        self.assertEqual(CASES[6], ("cold-tone", "quiet-overlapping-voiced", -900, 900, 20))
        self.assertEqual(CASES[7], ("tone-after-broadband-path-change", "quiet-overlapping-voiced", 0, 0, 0))
        self.assertEqual(SUMMARY["caseCount"], 10)
        for index in (3, 6, 7):
            with self.subTest(index=index):
                self.reject(lambda rows: rows.pop(next(
                    i for i, row in enumerate(rows) if row.get("caseIndex") == index)))

    def test_adverse_metrics_are_retained_as_observations(self):
        rows = fixture()
        for row in rows[:-1]:
            row["echoReductionDb"] = -4
            row["control"] = dict(projectedGain=1, correlation=1/math.sqrt(2),
                                  rmsRatio=math.sqrt(2), differenceRmsRatio=1)
            row["clipped"] = [4, 0, 2, 0]
        self.assertEqual(validate_history(encode(rows), 0)["status"], "recorded")

    def test_missing_row(self):
        self.reject(lambda rows: rows.pop(2))

    def test_duplicate_window(self):
        self.reject(lambda rows: rows.__setitem__(2, copy.deepcopy(rows[1])))

    def test_missing_intervention_disguised_as_original(self):
        self.reject(lambda rows: next(row for row in rows if row.get("schedule") == "quiet-before-six").update(schedule="original"))

    def test_changed_source(self):
        self.reject(lambda rows: rows[2].update(nearSignal="quiet-overlapping-voiced"))

    def test_changed_clock(self):
        self.reject(lambda rows: rows[2].update(capturePpm=900))

    def test_shifted_window(self):
        self.reject(lambda rows: rows[2].update(sourceStartSeconds=11.01))

    def test_fixed_output_indices_lose_source_clock(self):
        self.reject(lambda rows: rows[11].update(samples=96000))

    def test_truncated_window(self):
        self.reject(lambda rows: rows[2].update(samples=20))

    def test_missing_independent_stream(self):
        self.reject(lambda rows: rows[2].update(independentStreams=3))

    def test_unchecked_metadata(self):
        self.reject(lambda rows: rows[2].update(metadataParityFrames=1799))

    def test_nonfinite_metric(self):
        self.reject(lambda rows: rows[2].update(baselineRms=float("nan")))

    def test_finite_fields_cannot_overflow_energy_identity(self):
        self.reject(lambda rows: rows[2]["control"].update(
            projectedGain=0, correlation=0, rmsRatio=1e308, differenceRmsRatio=1e308))

    def test_silent_baseline(self):
        self.reject(lambda rows: rows[2].update(baselineRms=0))

    def test_gain_cannot_hide_distortion(self):
        self.reject(lambda rows: rows[2]["control"].update(rmsRatio=2))

    def test_residual_energy_must_match(self):
        self.reject(lambda rows: rows[2]["control"].update(differenceRmsRatio=1))

    def test_invalid_correlation(self):
        self.reject(lambda rows: rows[2]["mixed"].update(correlation=1.01))

    def test_lifetime_clipping_must_agree_between_windows(self):
        self.reject(lambda rows: rows[1].update(clipped=[1, 0, 0, 0]))

    def test_no_speech_acceptance_claim(self):
        self.reject(lambda rows: rows[1].update(speechQualityAccepted=True))

    def test_boolean_cannot_be_counter(self):
        self.reject(lambda rows: rows[2].update(caseIndex=False))

    def test_summary_requires_exact_types(self):
        self.reject(lambda rows: rows[-1].update(physicalAudio=0))

    def test_summary_cannot_claim_pass(self):
        self.reject(lambda rows: rows[-1].update(status="passed"))

    def test_duplicate_json_fields_rejected(self):
        raw = encode(fixture()).replace('"caseIndex": 0', '"caseIndex": 0, "caseIndex": 0', 1)
        with self.assertRaises(ValueError):
            validate_history(raw, 0)

    def test_exit_code_failure_not_a_report(self):
        with self.assertRaises(ValueError):
            validate_history(encode(fixture()), 1)

    def test_metric_contracts_complete(self):
        self.assertEqual(validate_metric_contracts(encode([METRICS]), 0), METRICS)

    def test_metric_contracts_incomplete_or_wrong_exit(self):
        for rows, code in (([], 0), ([METRICS, METRICS], 0), ([METRICS], 1),
                           ([dict(METRICS, metricCases=7)], 0)):
            with self.subTest(rows=rows, code=code), self.assertRaises(ValueError):
                validate_metric_contracts(encode(rows), code)


if __name__ == "__main__":
    unittest.main()
