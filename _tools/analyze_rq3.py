#!/usr/bin/env python3
"""RQ3: brecha de calibracion por condicion — exporta tablas CSV."""

from __future__ import annotations

import argparse
import sys
from collections import defaultdict
from pathlib import Path

_TOOLS_DIR = Path(__file__).resolve().parent
if str(_TOOLS_DIR) not in sys.path:
    sys.path.insert(0, str(_TOOLS_DIR))

from rq_common import (
    CONDITION_ORDER,
    ItemRow,
    SCENARIO_BY_CONDITION,
    aggregate_by_participant_condition,
    export_rq_condition_outputs,
    load_item_rows,
    write_csv,
)


def export_calibration_by_item(rows: list[ItemRow], output_dir: Path) -> None:
    """Brecha de calibración agregada por ítem (Q1–Q6) y condición."""
    buckets: dict[tuple[str, int], list[ItemRow]] = defaultdict(list)
    for row in rows:
        buckets[(row.condition, row.question_number)].append(row)

    item_rows: list[list[object]] = []
    for condition in CONDITION_ORDER:
        scenario = SCENARIO_BY_CONDITION[condition]
        for question in range(1, 7):
            items = buckets.get((condition, question), [])
            if not items:
                continue
            n = len(items)
            accuracy = sum(1 for item in items if item.is_correct) / n
            mean_norm = sum(item.confidence_normalized for item in items) / n
            item_rows.append(
                [
                    condition,
                    question,
                    scenario,
                    n,
                    round(mean_norm, 4),
                    round(100.0 * accuracy, 2),
                    round(mean_norm - accuracy, 4),
                ]
            )

    write_csv(
        output_dir / "rq3_calibration_by_item.csv",
        [
            "Condition",
            "QuestionNumber",
            "ScenarioNumber",
            "N",
            "MeanConfidenceNorm",
            "AccuracyPct",
            "CalibrationGap",
        ],
        item_rows,
    )


def run(
    csv_dir: Path,
    output_dir: Path | None = None,
    experiment_files: list[Path] | None = None,
) -> tuple[int, dict | None]:
    rows = load_item_rows(csv_dir, experiment_files)
    if not rows:
        return 1, None

    aggregated = aggregate_by_participant_condition(rows)
    per_participant_rows: list[list[object]] = []

    for p_key in sorted(aggregated):
        by_cond = aggregated[p_key]
        counts = [
            str(int(by_cond[c]["n_items"])) if c in by_cond else "-"
            for c in CONDITION_ORDER
        ]

        def accuracy_pct(condition: str) -> object:
            if condition not in by_cond:
                return ""
            return round(100.0 * float(by_cond[condition]["accuracy"]), 2)

        per_participant_rows.append(
            [
                p_key,
                "" if "A" not in by_cond else round(float(by_cond["A"]["calibration_gap"]), 4),
                "" if "B" not in by_cond else round(float(by_cond["B"]["calibration_gap"]), 4),
                "" if "C" not in by_cond else round(float(by_cond["C"]["calibration_gap"]), 4),
                accuracy_pct("A"),
                accuracy_pct("B"),
                accuracy_pct("C"),
                counts[0],
                counts[1],
                counts[2],
            ]
        )

    if output_dir:
        write_csv(
            output_dir / "rq3_calibration_gap_by_participant.csv",
            [
                "ParticipantSession",
                "CalibrationGap_A",
                "CalibrationGap_B",
                "CalibrationGap_C",
                "AccuracyPct_A",
                "AccuracyPct_B",
                "AccuracyPct_C",
                "Items_A",
                "Items_B",
                "Items_C",
            ],
            per_participant_rows,
        )
        export_rq_condition_outputs(
            aggregated,
            output_dir,
            prefix="rq3_calibration_gap",
            metric="calibration_gap",
            descriptive_scale="calibration_gap",
            group_mean_column="MeanCalibrationGap",
            round_digits=4,
        )
        export_calibration_by_item(rows, output_dir)

    return 0, aggregated


def main() -> int:
    parser = argparse.ArgumentParser(description="RQ3: brecha de calibracion")
    parser.add_argument("csv_dir", nargs="?", default="CSV data")
    parser.add_argument("--output-dir", type=Path, required=True)
    args = parser.parse_args()
    csv_dir = Path(args.csv_dir)
    if not csv_dir.is_dir():
        print(f"ERROR: no existe la carpeta {csv_dir}", file=sys.stderr)
        return 1
    return run(csv_dir, args.output_dir)[0]


if __name__ == "__main__":
    raise SystemExit(main())
