#!/usr/bin/env python3
"""RQ1: precision (% aciertos) por condicion — exporta tablas CSV."""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

_TOOLS_DIR = Path(__file__).resolve().parent
if str(_TOOLS_DIR) not in sys.path:
    sys.path.insert(0, str(_TOOLS_DIR))

from rq_common import (
    CONDITION_ORDER,
    aggregate_by_participant_condition,
    export_rq_condition_outputs,
    load_item_rows,
    write_csv,
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
        counts: list[str] = []
        for condition in CONDITION_ORDER:
            counts.append(str(int(by_cond[condition]["n_items"])) if condition in by_cond else "-")

        per_participant_rows.append(
            [
                p_key,
                "" if "A" not in by_cond else round(100.0 * float(by_cond["A"]["accuracy"]), 2),
                "" if "B" not in by_cond else round(100.0 * float(by_cond["B"]["accuracy"]), 2),
                "" if "C" not in by_cond else round(100.0 * float(by_cond["C"]["accuracy"]), 2),
                counts[0],
                counts[1],
                counts[2],
            ]
        )

    if output_dir:
        write_csv(
            output_dir / "rq1_precision_by_participant.csv",
            [
                "ParticipantSession",
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
            prefix="rq1_precision",
            metric="accuracy",
            descriptive_scale="accuracy_0_1",
            group_mean_column="MeanAccuracyPct",
            value_scale=100.0,
            round_digits=2,
        )

    return 0, aggregated


def main() -> int:
    parser = argparse.ArgumentParser(description="RQ1: precision por condicion")
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
