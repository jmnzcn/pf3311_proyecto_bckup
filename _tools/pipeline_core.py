#!/usr/bin/env python3
"""Orquestacion del analisis PF-3311: tablas CSV y graficos PNG."""

from __future__ import annotations

import sys
from pathlib import Path

_TOOLS_DIR = Path(__file__).resolve().parent
if str(_TOOLS_DIR) not in sys.path:
    sys.path.insert(0, str(_TOOLS_DIR))

import analyze_perfil
import analyze_chat_quality
import analyze_chat_semantic
import analyze_items
import export_question_bank
import analyze_rq1
import analyze_rq2
import analyze_rq2_forms
import analyze_rq3
import generate_rq_plots
import rq_inference
from master_table import export_master_participant_table
from pilot_metrics import run_pilot_metrics
from rq_common import silent_emit
from session_catalog import build_catalog, export_session_summary


def run_pipeline(
    csv_dir: Path,
    forms_dir: Path,
    output_dir: Path,
    *,
    skip_forms: bool = False,
    skip_plots: bool = False,
    all_sessions: bool = False,
    complete_only: bool = True,
) -> int:
    output_dir.mkdir(parents=True, exist_ok=True)

    catalog = build_catalog(csv_dir, canonical_only=not all_sessions)
    experiment_files = catalog.canonical_experiment_files
    export_session_summary(catalog, output_dir)

    if not experiment_files:
        return 1

    unity_failed = False
    aggregated = None

    code, aggregated = analyze_rq1.run(csv_dir, output_dir, experiment_files)
    if code != 0:
        unity_failed = True

    code, agg2 = analyze_rq2.run(csv_dir, output_dir, experiment_files)
    if code != 0:
        unity_failed = True
    if aggregated is None:
        aggregated = agg2

    if not skip_forms and forms_dir.is_dir():
        analyze_rq2_forms.run(forms_dir, output_dir)
        analyze_perfil.run(forms_dir, output_dir)

    code, agg3 = analyze_rq3.run(csv_dir, output_dir, experiment_files)
    if code != 0:
        unity_failed = True
    if aggregated is None:
        aggregated = agg3

    analyze_items.run(csv_dir, experiment_files, output_dir)

    bank_path = _TOOLS_DIR / "data" / "question_bank.json"
    if not bank_path.is_file():
        export_question_bank.export_bank(
            export_question_bank.DEFAULT_SCENE,
            bank_path,
        )

    analyze_chat_quality.run(catalog, output_dir)
    analyze_chat_semantic.run(catalog, output_dir, bank_path=bank_path)

    if aggregated:
        export_master_participant_table(catalog, aggregated, output_dir)

    run_pilot_metrics(catalog, output_dir)

    if aggregated:
        rq_inference.run(
            aggregated,
            output_dir,
            complete_only=complete_only,
            emit=silent_emit,
        )

    if not skip_plots:
        generate_rq_plots.run(output_dir, emit=silent_emit, complete_only=complete_only)

    return 1 if unity_failed else 0
