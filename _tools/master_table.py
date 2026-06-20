#!/usr/bin/env python3
"""
Tabla maestra por participante (sesion canonica) para analisis y tablas del informe escrito.
"""

from __future__ import annotations

import csv
from pathlib import Path

from rq_common import CONDITION_ORDER, write_csv
from session_catalog import SessionCatalog


def _load_csv_map(path: Path, key_col: str = "ParticipantCode") -> dict[str, dict[str, str]]:
    if not path.is_file():
        return {}
    with path.open(encoding="utf-8-sig", newline="") as handle:
        reader = csv.DictReader(handle)
        return {(row.get(key_col) or "").strip(): row for row in reader if (row.get(key_col) or "").strip()}


def export_master_participant_table(
    catalog: SessionCatalog,
    aggregated: dict[str, dict[str, dict[str, float | int]]],
    output_dir: Path,
) -> Path:
    chat_quality = _load_csv_map(output_dir / "chat_quality_by_participant.csv")
    chat_semantic = _load_csv_map(output_dir / "chat_semantic_by_participant.csv")

    header = ["ParticipantCode", "SessionID", "SessionFolder", "Complete_A_B_C"]
    for condition in CONDITION_ORDER:
        header.extend(
            [
                f"AccuracyPct_{condition}",
                f"MeanConfidence_{condition}",
                f"CalibrationGap_{condition}",
                f"Items_{condition}",
            ]
        )
    header.extend(
        [
            "HelpScore_B",
            "HelpScore_C",
            "OnTopicSeconds_B",
            "OnTopicSeconds_C",
            "OffTopicSeconds_B",
            "OffTopicSeconds_C",
            "SubstantiveQuestions_B",
            "SubstantiveQuestions_C",
            "ChatUsed_B",
            "ChatUsed_C",
            "UtilityLevel_B",
            "UtilityLevel_C",
            "OnTaskRatio_B",
            "OnTaskRatio_C",
            "AgentUtilityScore_B",
            "AgentUtilityScore_C",
            "SemanticTimeWastedSec_B",
            "SemanticTimeWastedSec_C",
        ]
    )

    rows: list[list[object]] = []
    for session in catalog.canonical:
        p_key = session.participant_session_key()
        pcode = session.participant_code
        by_cond = aggregated.get(p_key, {})
        row: list[object] = [
            session.participant_code,
            session.session_id,
            session.folder_name,
            "1" if session.is_complete else "0",
        ]
        for condition in CONDITION_ORDER:
            if condition in by_cond:
                metrics = by_cond[condition]
                row.extend(
                    [
                        round(100.0 * float(metrics["accuracy"]), 2),
                        round(float(metrics["mean_confidence"]), 3),
                        round(float(metrics["calibration_gap"]), 4),
                        int(metrics["n_items"]),
                    ]
                )
            else:
                row.extend(["", "", "", ""])

        cq = chat_quality.get(pcode, {})
        cs = chat_semantic.get(pcode, {})
        row.extend(
            [
                cq.get("HelpScore_B", ""),
                cq.get("HelpScore_C", ""),
                cq.get("OnTopicSeconds_B", ""),
                cq.get("OnTopicSeconds_C", ""),
                cq.get("OffTopicSeconds_B", ""),
                cq.get("OffTopicSeconds_C", ""),
                cq.get("SubstantiveQuestions_B", ""),
                cq.get("SubstantiveQuestions_C", ""),
                cq.get("ChatUsed_B", ""),
                cq.get("ChatUsed_C", ""),
                cq.get("UtilityLevel_B", ""),
                cq.get("UtilityLevel_C", ""),
                cs.get("OnTaskRatio_B", ""),
                cs.get("OnTaskRatio_C", ""),
                cs.get("AgentUtilityScore_B", ""),
                cs.get("AgentUtilityScore_C", ""),
                cs.get("TimeWastedSec_B", ""),
                cs.get("TimeWastedSec_C", ""),
            ]
        )
        rows.append(row)

    path = output_dir / "master_participant_table.csv"
    write_csv(path, header, rows)
    return path
