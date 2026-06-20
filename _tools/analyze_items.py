#!/usr/bin/env python3
"""Metricas por item — exporta tablas CSV."""

from __future__ import annotations

import csv
from collections import defaultdict
from pathlib import Path

from rq_common import CONDITION_BY_SCENARIO, PROVISIONAL_MARKERS, is_real_answer_row, load_item_rows, write_csv


def _load_time_by_item(experiment_files: list[Path]) -> dict[tuple[int, int], list[float]]:
    buckets: dict[tuple[int, int], list[float]] = defaultdict(list)
    for path in experiment_files:
        if not path.is_file():
            continue
        with path.open(encoding="utf-8-sig", newline="") as handle:
            reader = csv.DictReader(handle)
            for row in reader:
                if any(m in (v or "") for v in row.values() for m in PROVISIONAL_MARKERS):
                    continue
                if not is_real_answer_row(row):
                    continue
                scenario = int(row["ScenarioNumber"])
                question = int(row["QuestionNumber"])
                raw_time = (row.get("TimeSpent(Seconds)") or "").strip()
                try:
                    buckets[(scenario, question)].append(float(raw_time))
                except ValueError:
                    continue
    return buckets


def run(
    csv_dir: Path,
    experiment_files: list[Path],
    output_dir: Path | None = None,
) -> int:
    rows = load_item_rows(csv_dir, experiment_files)
    if not rows:
        return 1

    time_by_item = _load_time_by_item(experiment_files)
    by_item: dict[tuple[int, int], list] = defaultdict(list)
    for row in rows:
        by_item[(row.scenario_number, row.question_number)].append(row)

    accuracy_rows: list[list[object]] = []
    time_rows: list[list[object]] = []

    for scenario in sorted({k[0] for k in by_item}):
        condition = CONDITION_BY_SCENARIO[scenario]
        for question in range(1, 7):
            items = by_item.get((scenario, question), [])
            if not items:
                continue
            n = len(items)
            acc = sum(1 for i in items if i.is_correct) / n
            times = time_by_item.get((scenario, question), [])
            mean_time = sum(times) / len(times) if times else None
            accuracy_rows.append([condition, scenario, question, n, round(100.0 * acc, 2)])
            time_rows.append(
                [
                    condition,
                    scenario,
                    question,
                    len(times),
                    round(mean_time, 2) if mean_time is not None else "",
                ]
            )

    time_by_condition: dict[str, list[float]] = defaultdict(list)
    for condition, _scenario, _question, _n, mean_time in time_rows:
        if mean_time != "":
            time_by_condition[str(condition)].append(float(mean_time))

    condition_time_rows: list[list[object]] = []
    for condition in ("A", "B", "C"):
        times = time_by_condition.get(condition, [])
        if times:
            condition_time_rows.append(
                [condition, round(sum(times) / len(times), 2), len(times)]
            )

    if output_dir:
        write_csv(
            output_dir / "items_accuracy.csv",
            ["Condition", "ScenarioNumber", "QuestionNumber", "N", "AccuracyPct"],
            accuracy_rows,
        )
        write_csv(
            output_dir / "items_time_spent.csv",
            ["Condition", "ScenarioNumber", "QuestionNumber", "N", "MeanTimeSeconds"],
            time_rows,
        )
        write_csv(
            output_dir / "items_time_by_condition.csv",
            ["Condition", "MeanTimeSeconds", "NItems"],
            condition_time_rows,
        )

    return 0
