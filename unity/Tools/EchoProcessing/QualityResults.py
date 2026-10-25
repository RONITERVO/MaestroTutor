# Copyright 2026 Roni Tervo
# SPDX-License-Identifier: Apache-2.0
"""Validate retained aligned-quality evidence without treating open failures as passes."""
import json
import math
from pathlib import Path

GROUPS = {
    "Quality": ("--quality", "clock-aligned-quality", "broadband"),
    "Tonal": ("--tonal", "clock-aligned-tonal", "cold-tone"),
    "WarmTonal": ("--warm-tonal", "clock-aligned-warm-tonal", "tone-after-broadband"),
    "WarmTonalPathChange": ("--warm-tonal-path-change", "clock-aligned-warm-tonal-path-change", "tone-after-broadband-path-change"),
    "OverlappingSpeech": ("--overlapping-speech", "clock-aligned-overlapping-speech", None),
}
CLOCKS = ((0, 0, 0), (900, -900, 20), (-900, 900, 20))
NEAR_SIGNALS = ("overlapping-voiced", "quiet-overlapping-voiced", "bursty-overlapping-voiced")
PROFILES = ("stock", "stationary32", "measured32")
METRIC_CASES = frozenset("metric-" + name for name in (
    "unchanged-source", "amplified-near-attack", "amplified-baseline-attack", "dropped-near-attack",
    "mutually-muted-attack", "correlated-gain-is-insufficient", "actual-subunit-maxima",
    "maximum-survives-later-window", "window-bounds", "shape-mismatch",
    "nonfinite-or-unnormalized-samples", "silent-source", "negative-gain-maximum"))
# Native rows print six decimals. This only resolves printed-verdict consistency;
# the native pass/fail decision and all acceptance thresholds remain unchanged.
PRINT_HALF_UNIT = 0.0000005


def _require(condition, message):
    if not condition:
        raise ValueError(message)


def _object(pairs):
    result = {}
    for key, value in pairs:
        _require(key not in result, "Duplicate JSON field: " + key)
        result[key] = value
    return result


def _constant(value):
    raise ValueError("Nonfinite JSON value: " + value)


def _json(text):
    return json.loads(text, object_pairs_hook=_object, parse_constant=_constant)


def _number(row, field):
    value = row.get(field)
    _require(type(value) in (int, float) and math.isfinite(value), "Missing or nonfinite metric: " + field)
    return value


def _integer(row, field, expected=None):
    value = row.get(field)
    _require(type(value) is int and value >= 0, "Invalid integer: " + field)
    if expected is not None:
        _require(value == expected, "Unexpected " + field)
    return value


def case_id(pattern, near, clock):
    return "|".join((pattern, near, *(str(x) for x in clock)))


def expected_cases(group):
    _require(group in GROUPS, "Unknown quality group")
    pattern = GROUPS[group][2]
    if pattern is not None:
        return {case_id(pattern, "original", c): (pattern, "original", c) for c in CLOCKS}
    return {case_id(p, n, c): (p, n, c) for n in NEAR_SIGNALS
            for p in ("cold-tone", "tone-after-broadband", "tone-after-broadband-path-change") for c in CLOCKS}


def _validate_metrics(row, overlap, require_onset_maxima):
    metrics = {key: _number(row, key) for key in
               ("echoReductionDb", "nearCorrelation", "nearGain", "baselineGain", "maxDelayErrorMs")}
    clipped = _integer(row, "clipped")
    _require(metrics["maxDelayErrorMs"] >= 0 and metrics["baselineGain"] >= 0, "Negative physical metric")
    _require(abs(metrics["nearCorrelation"]) <= 1 + PRINT_HALF_UNIT, "Invalid near correlation")
    bounds = [(metrics["echoReductionDb"], 15, None), (metrics["nearCorrelation"], .65, None),
              (metrics["nearGain"], .45, 1.5), (metrics["baselineGain"], .45, 1.5),
              (metrics["maxDelayErrorMs"], None, 2)]
    if overlap:
        for key in ("onsetGain", "onsetCorrelation", "onsetBaselineGain"):
            metrics[key] = _number(row, key)
        _require(abs(metrics["onsetCorrelation"]) <= 1 + PRINT_HALF_UNIT and metrics["onsetBaselineGain"] >= 0,
                 "Invalid onset metric")
        bounds += [(metrics["onsetGain"], .45, None), (metrics["onsetCorrelation"], .65, None),
                   (metrics["onsetBaselineGain"], .45, None)]
        maximum_fields = ("onsetMaximumGain", "onsetMaximumBaselineGain")
        has_maxima = any(key in row for key in maximum_fields)
        if require_onset_maxima or has_maxima:
            maxima = [_number(row, key) for key in maximum_fields]
            bounds += [(value, None, 1.5) for value in maxima]
            if row["onsetWindows"] == 0:
                _require(maxima == [0, 0], "Onset maxima without onset windows")
            else:
                _require(maxima[0] + PRINT_HALF_UNIT >= metrics["onsetGain"] and
                         maxima[1] + PRINT_HALF_UNIT >= metrics["onsetBaselineGain"], "Reversed onset extrema")
        if row["onsetWindows"] == 0:
            _require(all(metrics[key] == 1 for key in ("onsetGain", "onsetCorrelation", "onsetBaselineGain")),
                     "Onset minima without onset windows")
    definitely_failed = clipped != 0 or any(
        (low is not None and value + PRINT_HALF_UNIT < low) or
        (high is not None and value - PRINT_HALF_UNIT > high) for value, low, high in bounds)
    definitely_passed = clipped == 0 and all(
        (low is None or value - PRINT_HALF_UNIT >= low) and
        (high is None or value + PRINT_HALF_UNIT <= high) for value, low, high in bounds)
    _require(row.get("status") in ("passed", "failed"), "Invalid case status")
    _require(not (row["status"] == "passed" and definitely_failed), "Passing case contradicts metrics")
    _require(not (row["status"] == "failed" and definitely_passed), "Failing case contradicts metrics")


def validate_group(text, group, exit_code, required_passing=(), *, require_onset_maxima=True):
    """Require the exact matrix and consistent verdicts, preserving known failures.

    Legacy maxima omission is allowed only when explicitly importing old evidence.
    Build.py always uses the default, which requires both newly measured maxima.
    """
    expected = expected_cases(group)
    rows = [_json(line) for line in text.splitlines() if line.strip()]
    _require(rows and all(type(row) is dict for row in rows), "Missing or malformed result rows")
    overlap = group == "OverlappingSpeech"
    summary = rows.pop()
    if not overlap:
        _require(bool(rows), "Missing pattern header")
        header = rows.pop(0)
        _require(header == {"pattern": GROUPS[group][2],
                            "measurementStartSeconds": 11 if group == "WarmTonalPathChange" else 8,
                            "measurementEndSeconds": 17}, "Unexpected quality pattern/window")
    _require(len(rows) == len(expected), "Incomplete quality case matrix")
    seen, passing, onsets = set(), set(), 0
    for row in rows:
        clock = tuple(row.get(key) for key in ("capturePpm", "renderPpm", "timestampJitterUs"))
        _require(all(type(x) is int for x in clock) and clock in CLOCKS, "Unexpected clock identity")
        pattern, near = (row.get("pattern"), row.get("nearSignal")) if overlap else (GROUPS[group][2], "original")
        if not overlap:
            _require("pattern" not in row and "nearSignal" not in row, "Unexpected case identity fields")
        _require(type(pattern) is str and type(near) is str, "Missing signal identity")
        identity = case_id(pattern, near, clock)
        _require(identity in expected and identity not in seen, "Unexpected or duplicate quality case: " + identity)
        seen.add(identity)
        if overlap:
            count = (6 if pattern.endswith("path-change") else 9) if near == "bursty-overlapping-voiced" else 0
            onsets += _integer(row, "onsetWindows", count)
        _validate_metrics(row, overlap, require_onset_maxima)
        if row["status"] == "passed":
            passing.add(identity)
    _require(seen == set(expected), "Missing expected quality cases")
    failures = len(rows) - len(passing)
    _require(type(exit_code) is int and exit_code == (1 if failures else 0), "Quality exit code contradicts cases")
    _require(summary.get("scope") == GROUPS[group][1], "Unexpected quality scope")
    _integer(summary, "qualityCases", len(expected))
    _integer(summary, "failures", failures)
    _require(summary.get("status") == ("failed" if failures else "passed"), "Quality summary contradicts cases")
    _require(summary.get("physicalAudio") is False and summary.get("deviceOpened") is False,
             "Quality result is not an offline fixture")
    if overlap:
        _integer(summary, "onsetWindows", 72)
        _require(onsets == 72, "Incomplete onset window matrix")
    required = set(required_passing)
    _require(required <= set(expected), "Baseline contains unknown cases")
    _require(required <= passing, "Previously passing aligned quality cases regressed: " + ", ".join(sorted(required - passing)))
    return dict(summary, validatedCaseIds=sorted(seen), passingCaseIds=sorted(passing),
                requiredPassingCaseIds=sorted(required))


def load_baseline(path, profile):
    document = _json(Path(path).read_text(encoding="utf-8"))
    _require(document.get("version") == 1 and profile in PROFILES, "Unknown baseline version/profile")
    _require(set(document.get("profiles", {})) == set(PROFILES), "Incomplete baseline profiles")
    for groups in document["profiles"].values():
        _require(set(groups) == set(GROUPS), "Incomplete baseline groups")
        for group, entry in groups.items():
            passing = entry.get("passingCaseIds")
            _require(type(passing) is list and all(type(x) is str for x in passing), "Invalid baseline cases")
            _require(len(set(passing)) == len(passing) and set(passing) <= set(expected_cases(group)),
                     "Duplicate or unknown baseline cases")
            _integer(entry, "qualityCases", len(expected_cases(group)))
            _integer(entry, "failures", len(expected_cases(group)) - len(passing))
    return {group: entry["passingCaseIds"] for group, entry in document["profiles"][profile].items()}


def validate_metric_contracts(text, exit_code):
    rows = [_json(line) for line in text.splitlines() if line.strip()]
    _require(type(exit_code) is int and exit_code == 0 and len(rows) == len(METRIC_CASES) + 1,
             "Incomplete aligned metric contracts")
    _require(all(type(row) is dict for row in rows), "Malformed metric contracts")
    summary = rows.pop()
    identities = [row.get("case") for row in rows]
    _require(all(type(identity) is str for identity in identities) and
             len(set(identities)) == len(identities) and set(identities) == METRIC_CASES and
             all(row.get("status") == "passed" for row in rows), "Missing or failed metric contract")
    _require(summary.get("status") == "passed" and summary.get("scope") == "clock-aligned-metric-contracts" and
             summary.get("physicalAudio") is False and summary.get("deviceOpened") is False,
             "Invalid metric-contract summary")
    _integer(summary, "metricCases", len(METRIC_CASES))
    _require(_integer(summary, "assertions") > 0, "Missing metric assertions")
    return summary
