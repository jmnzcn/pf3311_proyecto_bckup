#!/usr/bin/env python3
"""Carga tablas de _analysis/ para documentos derivados."""

from __future__ import annotations

import csv
from dataclasses import dataclass, field
from pathlib import Path


def read_csv(path: Path) -> list[dict[str, str]]:
    if not path.is_file():
        return []
    with path.open(encoding="utf-8-sig", newline="") as handle:
        return list(csv.DictReader(handle))


def prefer_complete_group_means(data_dir: Path, prefix: str) -> list[dict[str, str]]:
    complete = read_csv(data_dir / f"{prefix}_group_means_complete.csv")
    if complete:
        return complete
    return read_csv(data_dir / f"{prefix}_group_means.csv")


def parse_float(value: str) -> float | None:
    text = (value or "").strip()
    if not text:
        return None
    try:
        return float(text)
    except ValueError:
        return None


@dataclass
class AnalysisBundle:
    data_dir: Path
    sessions: list[dict[str, str]] = field(default_factory=list)
    master: list[dict[str, str]] = field(default_factory=list)
    rq1_participant: list[dict[str, str]] = field(default_factory=list)
    rq1_group: list[dict[str, str]] = field(default_factory=list)
    rq1_desc: list[dict[str, str]] = field(default_factory=list)
    rq2_participant: list[dict[str, str]] = field(default_factory=list)
    rq2_group: list[dict[str, str]] = field(default_factory=list)
    rq2_desc: list[dict[str, str]] = field(default_factory=list)
    rq2_forms_tlx: list[dict[str, str]] = field(default_factory=list)
    rq2_forms_tlx_group: list[dict[str, str]] = field(default_factory=list)
    rq2_forms_mecue: list[dict[str, str]] = field(default_factory=list)
    rq2_forms_mecue_group: list[dict[str, str]] = field(default_factory=list)
    rq2_forms_mecue_ii_group: list[dict[str, str]] = field(default_factory=list)
    rq2_forms_mecue_ii_participant: list[dict[str, str]] = field(default_factory=list)
    rq3_participant: list[dict[str, str]] = field(default_factory=list)
    rq3_group: list[dict[str, str]] = field(default_factory=list)
    rq3_desc: list[dict[str, str]] = field(default_factory=list)
    rq3_calibration_by_item: list[dict[str, str]] = field(default_factory=list)
    items_accuracy: list[dict[str, str]] = field(default_factory=list)
    items_time_by_condition: list[dict[str, str]] = field(default_factory=list)
    items_time_spent: list[dict[str, str]] = field(default_factory=list)
    chat_participant: list[dict[str, str]] = field(default_factory=list)
    chat_group: list[dict[str, str]] = field(default_factory=list)
    pilot_integrity: list[dict[str, str]] = field(default_factory=list)
    pilot_latency: list[dict[str, str]] = field(default_factory=list)
    pilot_tts: list[dict[str, str]] = field(default_factory=list)
    perfil: list[dict[str, str]] = field(default_factory=list)
    inference_omnibus: list[dict[str, str]] = field(default_factory=list)
    inference_pairwise: list[dict[str, str]] = field(default_factory=list)
    inference_mecue: list[dict[str, str]] = field(default_factory=list)
    inference_effect_sizes: list[dict[str, str]] = field(default_factory=list)
    inference_bootstrap: list[dict[str, str]] = field(default_factory=list)
    inference_contrasts: list[dict[str, str]] = field(default_factory=list)

    @property
    def figures_dir(self) -> Path:
        return self.data_dir / "figures"

    @property
    def canonical_sessions(self) -> list[dict[str, str]]:
        return [r for r in self.sessions if (r.get("IncludedInAnalysis") or "").strip() == "1"]

    @property
    def complete_canonical(self) -> list[dict[str, str]]:
        return [r for r in self.canonical_sessions if (r.get("Complete_A_B_C") or "").strip() == "1"]

    def integrity_map(self) -> dict[str, str]:
        return {r.get("Metric", ""): r.get("Value", "") for r in self.pilot_integrity}


def load_analysis(data_dir: Path) -> AnalysisBundle:
    data_dir = data_dir.resolve()
    return AnalysisBundle(
        data_dir=data_dir,
        sessions=read_csv(data_dir / "sessions_summary.csv"),
        master=read_csv(data_dir / "master_participant_table.csv"),
        rq1_participant=read_csv(data_dir / "rq1_precision_by_participant.csv"),
        rq1_group=prefer_complete_group_means(data_dir, "rq1_precision"),
        rq1_desc=read_csv(data_dir / "rq1_precision_descriptive_stats_complete.csv")
        or read_csv(data_dir / "rq1_precision_descriptive_stats.csv"),
        rq2_participant=read_csv(data_dir / "rq2_confidence_by_participant.csv"),
        rq2_group=prefer_complete_group_means(data_dir, "rq2_confidence"),
        rq2_desc=read_csv(data_dir / "rq2_confidence_descriptive_stats_complete.csv")
        or read_csv(data_dir / "rq2_confidence_descriptive_stats.csv"),
        rq2_forms_tlx=read_csv(data_dir / "rq2_forms_raw_tlx_by_participant.csv"),
        rq2_forms_tlx_group=read_csv(data_dir / "rq2_forms_raw_tlx_group_means.csv"),
        rq2_forms_mecue=read_csv(data_dir / "rq2_forms_mecue_b_vs_c.csv"),
        rq2_forms_mecue_group=read_csv(data_dir / "rq2_forms_mecue_group_means.csv"),
        rq2_forms_mecue_ii_group=read_csv(data_dir / "rq2_forms_mecue_ii_group_mean.csv"),
        rq2_forms_mecue_ii_participant=read_csv(data_dir / "rq2_forms_mecue_ii_by_participant.csv"),
        rq3_participant=read_csv(data_dir / "rq3_calibration_gap_by_participant.csv"),
        rq3_group=prefer_complete_group_means(data_dir, "rq3_calibration_gap"),
        rq3_desc=read_csv(data_dir / "rq3_calibration_gap_descriptive_stats_complete.csv")
        or read_csv(data_dir / "rq3_calibration_gap_descriptive_stats.csv"),
        rq3_calibration_by_item=read_csv(data_dir / "rq3_calibration_by_item.csv"),
        items_accuracy=read_csv(data_dir / "items_accuracy.csv"),
        items_time_by_condition=read_csv(data_dir / "items_time_by_condition.csv"),
        items_time_spent=read_csv(data_dir / "items_time_spent.csv"),
        chat_participant=read_csv(data_dir / "chat_quality_by_participant.csv"),
        chat_group=read_csv(data_dir / "chat_quality_b_vs_c.csv"),
        pilot_integrity=read_csv(data_dir / "pilot_integrity_summary.csv"),
        pilot_latency=read_csv(data_dir / "pilot_gemini_latency.csv"),
        pilot_tts=read_csv(data_dir / "pilot_tts_success.csv"),
        perfil=read_csv(data_dir / "perfil_participantes.csv"),
        inference_omnibus=read_csv(data_dir / "rq_inference_omnibus.csv"),
        inference_pairwise=read_csv(data_dir / "rq_inference_pairwise.csv"),
        inference_mecue=read_csv(data_dir / "rq_inference_mecue_b_vs_c.csv"),
        inference_effect_sizes=read_csv(data_dir / "rq_inference_effect_sizes.csv"),
        inference_bootstrap=read_csv(data_dir / "rq_inference_bootstrap_ci.csv"),
        inference_contrasts=read_csv(data_dir / "rq_inference_hypothesis_contrasts.csv"),
    )
