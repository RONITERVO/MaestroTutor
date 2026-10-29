# Copyright 2026 Roni Tervo
# SPDX-License-Identifier: Apache-2.0
"""Validate control completeness and measurement consistency, not speech acceptance."""
import math
from QualityResults import _json, _require, _integer, _number

CASES = (
    ("broadband", "original", 0, 0, 0),
    ("cold-tone", "quiet-overlapping-voiced", 0, 0, 0),
    ("tone-after-broadband", "quiet-overlapping-voiced", 0, 0, 0),
    ("tone-after-broadband-path-change", "quiet-overlapping-voiced", -900, 900, 20),
    ("tone-after-broadband", "bursty-overlapping-voiced", 900, -900, 20),
    ("tone-after-broadband-path-change", "bursty-overlapping-voiced", 0, 0, 0),
    ("cold-tone", "quiet-overlapping-voiced", -900, 900, 20),
    ("tone-after-broadband-path-change", "quiet-overlapping-voiced", 0, 0, 0),
)
QUIET_PREFIX_CASES = (2, 3)
WINDOWS = ((6.02, 8), (8, 11), (11, 17))
SUMMARY = dict(status="recorded", scope="clock-aligned-history-controls", caseCount=len(CASES)+len(QUIET_PREFIX_CASES),
               windowCount=(len(CASES)+len(QUIET_PREFIX_CASES))*len(WINDOWS), framesPerCase=1800, independentStreams=4,
               speechQualityAccepted=False, physicalAudio=False, deviceOpened=False)
METRICS = dict(status="passed", scope="history-metric-contracts", metricCases=8,
               physicalAudio=False, deviceOpened=False)


def _exact(actual, expected):
    # JSON bool must not be accepted as integer zero/one.
    if type(actual) is not type(expected):
        return False
    if isinstance(expected, dict):
        return actual.keys() == expected.keys() and all(
            _exact(actual[key], value) for key, value in expected.items())
    return actual == expected


def validate_metric_contracts(text, exit_code):
    rows = [_json(line) for line in text.splitlines() if line.strip()]
    _require(exit_code == 0 and len(rows) == 1 and _exact(rows[0], METRICS),
             "Incomplete history metric contracts")
    return rows[0]


def _score(row):
    _require(type(row) is dict and set(row) == {
        "projectedGain", "correlation", "rmsRatio", "differenceRmsRatio"}, "Invalid control score")
    g, c, r, e = (_number(row, key) for key in
                  ("projectedGain", "correlation", "rmsRatio", "differenceRmsRatio"))
    _require(abs(c) <= 1 + 1e-8 and r >= 0 and e >= 0, "Invalid control metric range")
    _require(math.isfinite(r*r) and math.isfinite(e*e), "Overflowed control energy")
    # Nine-decimal print tolerance only. These identities do not define good audio.
    _require(math.isclose(g, c*r, abs_tol=1e-7, rel_tol=1e-7), "Inconsistent projected gain")
    _require(math.isclose(e*e, r*r+1-2*g, abs_tol=1e-7, rel_tol=1e-7), "Inconsistent residual energy")


def validate_history(text, exit_code):
    rows = [_json(line) for line in text.splitlines() if line.strip()]
    _require(exit_code == 0 and len(rows) == SUMMARY["windowCount"]+1 and _exact(rows[-1], SUMMARY),
             "Incomplete history diagnostic")
    expected = {(i, "original", w) for i in range(len(CASES)) for w in WINDOWS}
    expected |= {(i, "quiet-before-six", w) for i in QUIET_PREFIX_CASES for w in WINDOWS}
    seen, case_metadata = set(), {}
    for row in rows[:-1]:
        _require(type(row) is dict, "Invalid history row")
        i = _integer(row, "caseIndex")
        _require(i < len(CASES), "Unknown history case")
        pattern, near, cap, render, jitter = CASES[i]
        _require(row.get("pattern") == pattern and row.get("nearSignal") == near,
                 "Changed history source identity")
        for key, value in (("capturePpm", cap), ("renderPpm", render), ("timestampJitterUs", jitter)):
            _require(_number(row, key) == value, "Changed history clock identity")
        window = (_number(row, "sourceStartSeconds"), _number(row, "sourceEndSeconds"))
        identity = (i, row.get("schedule"), window)
        _require(identity in expected and identity not in seen, "Duplicate or unexpected control/window")
        seen.add(identity)
        count = _integer(row, "samples")
        # Sampling follows measured input coordinates; fixed 16 kHz output indices
        # would incorrectly lose the modeled clock drift in these windows.
        _require(abs(count-(window[1]-window[0])*16000*(1+render/1e6)) <= 2, "Incomplete source window")
        _require(0 < _number(row, "baselineRms") <= 1 and 0 < _number(row, "rawNearRms") <= 1,
                 "Inaudible or unnormalized comparison baseline")
        _number(row, "echoReductionDb")
        _require(0 <= _number(row, "maxDelayErrorMs") <= 2, "Invalid modeled delay")
        _integer(row, "metadataParityFrames", 1800)
        _integer(row, "independentStreams", 4)
        clipped = row.get("clipped")
        _require(type(clipped) is list and len(clipped) == 4 and all(
            type(n) is int and 0 <= n <= 1800*480 for n in clipped), "Invalid clipping evidence")
        _require(row.get("status") == "recorded" and row.get("speechQualityAccepted") is False,
                 "Diagnostic cannot certify quality")
        _score(row.get("control"))
        _score(row.get("mixed"))
        meta = (tuple(clipped), row["maxDelayErrorMs"])
        owner = (i, row["schedule"])
        _require(owner not in case_metadata or case_metadata[owner] == meta, "Inconsistent case lifetime evidence")
        case_metadata[owner] = meta
    _require(seen == expected, "Missing history control")
    return dict(rows[-1], validatedWindows=len(seen))
