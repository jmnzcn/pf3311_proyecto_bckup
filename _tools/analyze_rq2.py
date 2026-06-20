#!/usr/bin/env python3
"""RQ2: confianza media (1-7) por condicion — exporta tablas CSV."""

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
        counts = [
            str(int(by_cond[c]["n_items"])) if c in by_cond else "-"
            for c in CONDITION_ORDER
        ]
        per_participant_rows.append(
            [
                p_key,
                "" if "A" not in by_cond else round(float(by_cond["A"]["mean_confidence"]), 3),
                "" if "B" not in by_cond else round(float(by_cond["B"]["mean_confidence"]), 3),
                "" if "C" not in by_cond else round(float(by_cond["C"]["mean_confidence"]), 3),
                counts[0],
                counts[1],
                counts[2],
            ]
        )

    if output_dir:
        write_csv(
            output_dir / "rq2_confidence_by_participant.csv",
            [
                "ParticipantSession",
                "MeanConfidence_A",
                "MeanConfidence_B",
                "MeanConfidence_C",
                "Items_A",
                "Items_B",
                "Items_C",
            ],
            per_participant_rows,
        )
        export_rq_condition_outputs(
            aggregated,
            output_dir,
            prefix="rq2_confidence",
            metric="mean_confidence",
            descriptive_scale="confidence_1_7",
            group_mean_column="MeanConfidence",
            round_digits=3,
        )

    return 0, aggregated


def main() -> int:
    parser = argparse.ArgumentParser(description="RQ2: confianza por condicion")
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
