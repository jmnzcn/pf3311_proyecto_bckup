#!/usr/bin/env python3
"""RQ2 Forms: meCUE + RAW-TLX — exporta tablas CSV."""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

_TOOLS_DIR = Path(__file__).resolve().parent
if str(_TOOLS_DIR) not in sys.path:
    sys.path.insert(0, str(_TOOLS_DIR))

from forms_common import load_all_forms
from rq_common import CONDITION_ORDER, mean, write_csv

MECUE_MODULES = ("MECUE_I", "MECUE_III", "MECUE_IV", "MECUE_V")
MECUE_MODULE_LABELS = {
    "MECUE_I": "Utilidad (Mód. I)",
    "MECUE_III": "Emociones (Mód. III)",
    "MECUE_IV": "Consecuencias (Mód. IV)",
    "MECUE_V": "Evaluación global (Mód. V)",
}


def run(forms_dir: Path, output_dir: Path | None = None) -> int:
    forms = load_all_forms(forms_dir)
    if not any(forms.values()):
        return 0

    by_condition: dict[str, dict[str, dict[str, object]]] = {"A": {}, "B": {}, "C": {}}
    for condition, responses in forms.items():
        for response in responses:
            entry: dict[str, object] = {}
            if response.raw_tlx_mean is not None:
                entry["RAW_TLX"] = response.raw_tlx_mean
            for module, score in response.module_scores.items():
                if score is not None:
                    entry[module] = score
            by_condition[condition][response.participant_code] = entry

    tlx_rows: list[list[object]] = []
    participants = sorted(
        set(by_condition["A"]) | set(by_condition["B"]) | set(by_condition["C"])
    )
    for code in participants:
        vals = []
        for condition in CONDITION_ORDER:
            value = by_condition[condition].get(code, {}).get("RAW_TLX")
            vals.append("" if value is None else round(float(value), 3))
        tlx_rows.append([code, vals[0], vals[1], vals[2]])

    module_rows: list[list[object]] = []
    for module in MECUE_MODULES:
        b_codes = {
            code: float(metrics[module])
            for code, metrics in by_condition["B"].items()
            if metrics.get(module) is not None
        }
        c_codes = {
            code: float(metrics[module])
            for code, metrics in by_condition["C"].items()
            if metrics.get(module) is not None
        }
        for code in sorted(set(b_codes) | set(c_codes)):
            module_rows.append(
                [
                    code,
                    module,
                    round(b_codes[code], 3) if code in b_codes else "",
                    round(c_codes[code], 3) if code in c_codes else "",
                ]
            )

    tlx_group_rows: list[list[object]] = []
    for condition in CONDITION_ORDER:
        values = [
            float(metrics["RAW_TLX"])
            for metrics in by_condition[condition].values()
            if metrics.get("RAW_TLX") is not None
        ]
        if values:
            tlx_group_rows.append(
                [condition, round(mean(values), 3), len(values)]  # type: ignore[arg-type]
            )

    mecue_group_rows: list[list[object]] = []
    for module in MECUE_MODULES:
        b_vals = [
            float(metrics[module])
            for metrics in by_condition["B"].values()
            if metrics.get(module) is not None
        ]
        c_vals = [
            float(metrics[module])
            for metrics in by_condition["C"].values()
            if metrics.get(module) is not None
        ]
        if not b_vals and not c_vals:
            continue
        mecue_group_rows.append(
            [
                module,
                MECUE_MODULE_LABELS.get(module, module),
                round(mean(b_vals), 3) if b_vals else "",
                len(b_vals),
                round(mean(c_vals), 3) if c_vals else "",
                len(c_vals),
            ]
        )

    mecue_ii_rows: list[list[object]] = []
    for code, metrics in sorted(by_condition["C"].items()):
        value = metrics.get("MECUE_II")
        if value is not None:
            mecue_ii_rows.append([code, round(float(value), 3)])

    if output_dir:
        write_csv(
            output_dir / "rq2_forms_raw_tlx_by_participant.csv",
            ["ParticipantCode", "RAW_TLX_A", "RAW_TLX_B", "RAW_TLX_C"],
            tlx_rows,
        )
        write_csv(
            output_dir / "rq2_forms_mecue_b_vs_c.csv",
            ["ParticipantCode", "Module", "Score_B", "Score_C"],
            module_rows,
        )
        write_csv(
            output_dir / "rq2_forms_raw_tlx_group_means.csv",
            ["Condition", "MeanRAWTLX", "NParticipants"],
            tlx_group_rows,
        )
        write_csv(
            output_dir / "rq2_forms_mecue_group_means.csv",
            ["Module", "ModuleLabel", "Mean_B", "N_B", "Mean_C", "N_C"],
            mecue_group_rows,
        )
        if mecue_ii_rows:
            write_csv(
                output_dir / "rq2_forms_mecue_ii_by_participant.csv",
                ["ParticipantCode", "MECUE_II_C"],
                mecue_ii_rows,
            )
            ii_vals = [float(row[1]) for row in mecue_ii_rows]
            write_csv(
                output_dir / "rq2_forms_mecue_ii_group_mean.csv",
                ["Module", "Mean_C", "NParticipants"],
                [["MECUE_II", round(mean(ii_vals), 3), len(ii_vals)]],  # type: ignore[arg-type]
            )

    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description="RQ2 Forms: meCUE y RAW-TLX")
    parser.add_argument("forms_dir", nargs="?", default="Forms data")
    parser.add_argument("--output-dir", type=Path, required=True)
    args = parser.parse_args()
    forms_dir = Path(args.forms_dir)
    if not forms_dir.is_dir():
        print(f"ERROR: no existe la carpeta {forms_dir}", file=sys.stderr)
        return 1
    return run(forms_dir, args.output_dir)


if __name__ == "__main__":
    raise SystemExit(main())
