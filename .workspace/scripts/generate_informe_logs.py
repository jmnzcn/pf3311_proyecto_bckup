#!/usr/bin/env python3
"""Lee tablas de _analysis/ y escribe logs de apoyo."""

from __future__ import annotations

import argparse
import csv
import sys
from datetime import datetime
from pathlib import Path

from _bootstrap import setup

setup()

from _paths import OUT_LOGS, ROOT

from rq_common import ITEMS_PER_BLOCK
from rq_inference import (
    print_hypothesis_summary,
    print_inference_block,
    print_mecue_b_vs_c_inference,
    print_pairwise_result,
    wilcoxon_paired,
)

CONDITION_ORDER = ("A", "B", "C")

PIECES = [
    ("Sesiones analizadas", "sessions_summary.csv"),
    ("Tabla maestra participantes", "master_participant_table.csv"),
    ("RQ1 por participante", "rq1_precision_by_participant.csv"),
    ("RQ1 medias grupales", "rq1_precision_group_means.csv"),
    ("RQ1 medias grupales (completos)", "rq1_precision_group_means_complete.csv"),
    ("RQ1 descriptivos", "rq1_precision_descriptive_stats_complete.csv"),
    ("RQ2 confianza Unity", "rq2_confidence_by_participant.csv"),
    ("RQ2 medias grupales", "rq2_confidence_group_means.csv"),
    ("RQ2 medias grupales (completos)", "rq2_confidence_group_means_complete.csv"),
    ("RQ2 descriptivos", "rq2_confidence_descriptive_stats_complete.csv"),
    ("RQ2 meCUE/RAW-TLX", "rq2_forms_raw_tlx_by_participant.csv"),
    ("RQ2 meCUE B vs C", "rq2_forms_mecue_b_vs_c.csv"),
    ("RQ2 meCUE II (C)", "rq2_forms_mecue_ii_group_mean.csv"),
    ("Perfil participantes", "perfil_participantes.csv"),
    ("Perfil grafico muestra", "figures/perfil_muestra.png"),
    ("RQ3 calibracion", "rq3_calibration_gap_by_participant.csv"),
    ("RQ3 medias grupales", "rq3_calibration_gap_group_means.csv"),
    ("RQ3 medias grupales (completos)", "rq3_calibration_gap_group_means_complete.csv"),
    ("RQ3 descriptivos", "rq3_calibration_gap_descriptive_stats_complete.csv"),
    ("RQ3 calibracion por item", "rq3_calibration_by_item.csv"),
    ("Inferencia omnibus", "rq_inference_omnibus.csv"),
    ("Inferencia pareada", "rq_inference_pairwise.csv"),
    ("Tamaños del efecto", "rq_inference_effect_sizes.csv"),
    ("IC bootstrap", "rq_inference_bootstrap_ci.csv"),
    ("Contrastes H3 / agente", "rq_inference_hypothesis_contrasts.csv"),
    ("Inferencia meCUE B vs C", "rq_inference_mecue_b_vs_c.csv"),
    ("Reporte inferencia", "reporte_inferencia.txt"),
    ("Precision por item", "items_accuracy.csv"),
    ("Tiempo por item", "items_time_spent.csv"),
    ("Agente por participante", "chat_quality_by_participant.csv"),
    ("Agente por escenario", "chat_scenario_by_participant.csv"),
    ("Agente medias B vs C", "chat_quality_b_vs_c.csv"),
    ("Detalle chat por pregunta", "chat_question_detail.csv"),
    ("Eventos API", "chat_api_events.csv"),
    ("Latencia Gemini", "pilot_gemini_latency.csv"),
    ("TTS condicion C", "pilot_tts_success.csv"),
    ("Viabilidad resumen", "pilot_integrity_summary.csv"),
]


def _read_csv(path: Path) -> list[dict[str, str]]:
    if not path.is_file():
        return []
    with path.open(encoding="utf-8-sig", newline="") as handle:
        return list(csv.DictReader(handle))


def _float(value: str) -> float | None:
    text = (value or "").strip()
    if not text:
        return None
    try:
        return float(text)
    except ValueError:
        return None


def _int(value: str) -> int:
    text = (value or "").strip()
    return int(text) if text.isdigit() else 0


def _group_means_path(data_dir: Path, prefix: str) -> Path:
    complete = data_dir / f"{prefix}_group_means_complete.csv"
    if complete.is_file():
        return complete
    return data_dir / f"{prefix}_group_means.csv"


def _emit_table(emit, title: str, path: Path, max_rows: int = 50) -> None:
    rows = _read_csv(path)
    if not rows:
        emit(f"\n[{title}] sin archivo o vacio: {path.name}")
        return
    emit(f"\n=== {title} ({path.name}) ===\n")
    headers = list(rows[0].keys())
    emit(" | ".join(headers))
    emit("-" * min(120, 8 * len(headers)))
    for row in rows[:max_rows]:
        emit(" | ".join((row.get(h) or "") for h in headers))
    if len(rows) > max_rows:
        emit(f"... ({len(rows) - max_rows} filas mas)")


def _friedman_from_participant_csv(
    path: Path,
    cols: tuple[str, str, str],
    item_cols: tuple[str, str, str] | None = None,
    scale: float = 1.0,
    min_items: int = ITEMS_PER_BLOCK,
) -> list[tuple[float, float, float]]:
    matrix: list[tuple[float, float, float]] = []
    for row in _read_csv(path):
        if item_cols and not all(_int(row.get(c, "")) >= min_items for c in item_cols):
            continue
        vals = [_float(row.get(c, "")) for c in cols]
        if any(v is None for v in vals):
            continue
        matrix.append((vals[0] * scale, vals[1] * scale, vals[2] * scale))
    return matrix


def _section_perfil(data_dir: Path, emit) -> None:
    path = data_dir / "perfil_participantes.csv"
    _emit_table(emit, "Perfil (Form 0)", path)


def _section_rq1(data_dir: Path, emit) -> None:
    path = data_dir / "rq1_precision_by_participant.csv"
    rows = _read_csv(path)
    if not rows:
        emit("\n[RQ1] Falta rq1_precision_by_participant.csv")
        return

    emit("\n=== RQ1 - Precision (% aciertos por condicion) ===\n")
    emit(f"{'Participante':<28} {'A':>8} {'B':>8} {'C':>8} {'n(A/B/C)':>12}")
    emit("-" * 68)
    for row in rows:
        emit(
            f"{row.get('ParticipantSession', ''):<28} "
            f"{row.get('AccuracyPct_A', ''):>8} "
            f"{row.get('AccuracyPct_B', ''):>8} "
            f"{row.get('AccuracyPct_C', ''):>8} "
            f"{row.get('Items_A', '')}/{row.get('Items_B', '')}/{row.get('Items_C', '')}"
        )

    for row in _read_csv(_group_means_path(data_dir, "rq1_precision")):
        emit(
            f"\nMedia {row.get('Condition', '')}: "
            f"{row.get('MeanAccuracyPct', '')}% (n={row.get('NParticipants', '')})"
        )

    matrix = _friedman_from_participant_csv(
        path,
        ("AccuracyPct_A", "AccuracyPct_B", "AccuracyPct_C"),
        ("Items_A", "Items_B", "Items_C"),
        scale=1 / 100.0,
    )
    print_inference_block(emit, "Precision", matrix)


def _section_rq2_unity(data_dir: Path, emit) -> None:
    path = data_dir / "rq2_confidence_by_participant.csv"
    rows = _read_csv(path)
    if not rows:
        emit("\n[RQ2 Unity] Falta rq2_confidence_by_participant.csv")
        return

    emit("\n=== RQ2 (Unity) - Confianza media (escala 1-7) ===\n")
    for row in rows:
        emit(
            f"{row.get('ParticipantSession', ''):<28} "
            f"A={row.get('MeanConfidence_A', '')} "
            f"B={row.get('MeanConfidence_B', '')} "
            f"C={row.get('MeanConfidence_C', '')}"
        )

    for row in _read_csv(_group_means_path(data_dir, "rq2_confidence")):
        emit(
            f"Media {row.get('Condition', '')}: "
            f"{row.get('MeanConfidence', '')} (n={row.get('NParticipants', '')})"
        )

    matrix = _friedman_from_participant_csv(
        path,
        ("MeanConfidence_A", "MeanConfidence_B", "MeanConfidence_C"),
        ("Items_A", "Items_B", "Items_C"),
    )
    print_inference_block(emit, "Confianza Unity", matrix)


def _section_rq3(data_dir: Path, emit) -> None:
    path = data_dir / "rq3_calibration_gap_by_participant.csv"
    rows = _read_csv(path)
    if not rows:
        emit("\n[RQ3] Falta rq3_calibration_gap_by_participant.csv")
        return

    emit("\n=== RQ3 - Calibracion (brecha confianza - precision) ===\n")
    for row in rows:
        emit(
            f"{row.get('ParticipantSession', ''):<28} "
            f"A={row.get('CalibrationGap_A', '')} "
            f"B={row.get('CalibrationGap_B', '')} "
            f"C={row.get('CalibrationGap_C', '')}"
        )

    for row in _read_csv(_group_means_path(data_dir, "rq3_calibration_gap")):
        emit(
            f"Media {row.get('Condition', '')}: "
            f"brecha={row.get('MeanCalibrationGap', '')} (n={row.get('NParticipants', '')})"
        )

    matrix = _friedman_from_participant_csv(
        path,
        ("CalibrationGap_A", "CalibrationGap_B", "CalibrationGap_C"),
        ("Items_A", "Items_B", "Items_C"),
    )
    print_inference_block(emit, "Brecha calibracion", matrix)


def _section_hypotheses(data_dir: Path, emit) -> None:
    master = _read_csv(data_dir / "master_participant_table.csv")
    if not master:
        return

    aggregated: dict[str, dict[str, dict[str, float | int]]] = {}
    for row in master:
        key = f"{row.get('ParticipantCode', '')}_{row.get('SessionID', '')}"
        aggregated[key] = {}
        for cond in CONDITION_ORDER:
            acc = _float(row.get(f"AccuracyPct_{cond}", ""))
            conf = _float(row.get(f"MeanConfidence_{cond}", ""))
            gap = _float(row.get(f"CalibrationGap_{cond}", ""))
            n = _int(row.get(f"Items_{cond}", ""))
            if n > 0 and acc is not None and conf is not None and gap is not None:
                aggregated[key][cond] = {
                    "accuracy": acc / 100.0,
                    "mean_confidence": conf,
                    "calibration_gap": gap,
                    "n_items": n,
                }

    if aggregated:
        print_hypothesis_summary(emit, aggregated)


def _section_forms(data_dir: Path, emit) -> None:
    tlx_path = data_dir / "rq2_forms_raw_tlx_by_participant.csv"
    if not tlx_path.is_file():
        return

    emit("\n=== RQ2 (Forms) - RAW-TLX ===\n")
    matrix: list[tuple[float, float, float]] = []
    for row in _read_csv(tlx_path):
        a, b, c = _float(row.get("RAW_TLX_A", "")), _float(row.get("RAW_TLX_B", "")), _float(row.get("RAW_TLX_C", ""))
        if a is not None and b is not None and c is not None:
            matrix.append((a, b, c))
            emit(f"  {row.get('ParticipantCode', '')}: A={a} B={b} C={c}")
    print_inference_block(emit, "RAW-TLX", matrix)

    mecue_path = data_dir / "rq2_forms_mecue_b_vs_c.csv"
    if not mecue_path.is_file():
        return

    emit("\n=== RQ2 (Forms) - meCUE B vs C ===\n")
    print_mecue_b_vs_c_inference(emit, _read_csv(mecue_path))


def _section_chat_infer(data_dir: Path, emit) -> None:
    rows = _read_csv(data_dir / "chat_quality_by_participant.csv")
    if not rows:
        emit("\n[Agente] Sin chat_quality_by_participant.csv")
        return
    emit("\n=== Calidad del agente (B vs C) ===\n")
    for row in rows:
        emit(
            f"{row.get('ParticipantCode', ''):<6} "
            f"Help B={row.get('HelpScore_B', '')} C={row.get('HelpScore_C', '')} | "
            f"Leaks B={row.get('Leaks_B', '')} C={row.get('Leaks_C', '')}"
        )
    for row in _read_csv(data_dir / "chat_quality_b_vs_c.csv"):
        metric = row.get("Metric", "")
        if metric:
            emit(
                f"  Media {metric}: B={row.get('Mean_B', '')} "
                f"(n={row.get('N_B', '')}) | C={row.get('Mean_C', '')} (n={row.get('N_C', '')})"
            )

    rows = _read_csv(data_dir / "chat_quality_by_participant.csv")
    metric_cols = {
        "HelpScore": ("HelpScore_B", "HelpScore_C"),
        "Engagement": None,
        "ChatExchanges": ("Exchanges_B", "Exchanges_C"),
        "ModelLeaks": ("Leaks_B", "Leaks_C"),
    }
    for label, cols in metric_cols.items():
        if not cols:
            continue
        b_dict: dict[str, float] = {}
        c_dict: dict[str, float] = {}
        for row in rows:
            code = row.get("ParticipantCode", "")
            bv, cv = _float(row.get(cols[0], "")), _float(row.get(cols[1], ""))
            if code and bv is not None and cv is not None:
                b_dict[code] = bv
                c_dict[code] = cv
        if b_dict:
            result = wilcoxon_paired(b_dict, c_dict, "B", "C")
            print_pairwise_result(emit, f"  Wilcoxon {label}", result)


def _section_sessions(data_dir: Path, emit) -> None:
    rows = _read_csv(data_dir / "sessions_summary.csv")
    if not rows:
        emit("\n[Sesiones] sin sessions_summary.csv")
        return

    emit("=== Catalogo de sesiones ===\n")
    included = [r for r in rows if (r.get("IncludedInAnalysis") or "").strip() == "1"]
    complete = [r for r in included if (r.get("Complete_A_B_C") or "").strip() == "1"]
    emit(
        f"Sesiones encontradas: {len(rows)} | "
        f"Canonicas: {len(included)} | Completas (6+6+6): {len(complete)}\n"
    )
    emit(
        f"{'Carpeta':<42} {'P##':<6} {'A':>3} {'B':>3} {'C':>3} "
        f"{'OK':>4} {'Cons':>5} {'Analisis':>10}"
    )
    emit("-" * 82)

    for row in rows:
        analisis = "SI" if (row.get("IncludedInAnalysis") or "").strip() == "1" else "no"
        emit(
            f"{row.get('FolderName', ''):<42} {row.get('ParticipantCode', ''):<6} "
            f"{row.get('Items_A', ''):>3} {row.get('Items_B', ''):>3} {row.get('Items_C', ''):>3} "
            f"{'SI' if (row.get('Complete_A_B_C') or '').strip() == '1' else 'no':>4} "
            f"{'SI' if (row.get('HasConsent') or '').strip() == '1' else 'no':>5} "
            f"{analisis:>10}"
        )
        note = (row.get("SelectionNote") or "").strip()
        if note:
            emit(f"    -> {note}")

    if included:
        pct = 100.0 * len(complete) / len(included)
        emit(
            f"\nCompletitud: {len(complete)}/{len(included)} "
            f"participantes canonicos con A+B+C ({pct:.1f}%)."
        )


def _section_pilot(data_dir: Path, emit) -> None:
    summary = _read_csv(data_dir / "pilot_integrity_summary.csv")
    if summary:
        emit("\n=== Viabilidad técnica del estudio (resumen) ===\n")
        for row in summary:
            emit(f"  {row.get('Metric', '')}: {row.get('Value', '')}")
    _emit_table(emit, "Latencia Gemini", data_dir / "pilot_gemini_latency.csv", max_rows=20)
    _emit_table(emit, "TTS condicion C", data_dir / "pilot_tts_success.csv", max_rows=20)


def _write_index(logs_dir: Path, data_dir: Path) -> Path:
    figures = sorted((data_dir / "figures").glob("*.png")) if (data_dir / "figures").is_dir() else []
    lines = [
        "# Piezas de analisis PF-3311",
        "",
        f"Generado: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}",
        f"Datos: `{data_dir.resolve()}`",
        f"Logs: `{logs_dir.resolve()}`",
        "",
        "| Archivo | Estado |",
        "|---------|--------|",
    ]
    for _title, filename in PIECES:
        status = "OK" if (data_dir / filename).is_file() else "—"
        lines.append(f"| `{filename}` | {status} |")
    lines.extend(["", "## Graficos", ""])
    if figures:
        for fig in figures:
            lines.append(f"- `{fig.relative_to(data_dir).as_posix()}`")
    else:
        lines.append("- (ninguno)")
    out = logs_dir / "INFORME_PIEZAS.md"
    out.write_text("\n".join(lines) + "\n", encoding="utf-8")
    return out


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Lee CSV exportados y escribe logs de apoyo",
    )
    parser.add_argument(
        "--data-dir",
        type=Path,
        default=ROOT / "_analysis",
        help="Carpeta con tablas y figures/ del pipeline principal",
    )
    parser.add_argument(
        "--logs-dir",
        type=Path,
        default=OUT_LOGS,
        help="Donde escribir reportes de texto",
    )
    args = parser.parse_args()

    data_dir = args.data_dir.resolve()
    if not data_dir.is_dir():
        print(f"ERROR: no existe {data_dir}", file=sys.stderr)
        return 1

    logs_dir = args.logs_dir
    logs_dir.mkdir(parents=True, exist_ok=True)

    lines: list[str] = [
        "PF-3311 - Notas de analisis (desde tablas exportadas)",
        f"Generado: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}",
        f"Fuente: {data_dir}",
        "=" * 72,
        "",
    ]

    def emit(text: str = "") -> None:
        print(text)
        lines.append(text)

    _section_sessions(data_dir, emit)
    _section_perfil(data_dir, emit)
    _section_rq1(data_dir, emit)
    _section_rq2_unity(data_dir, emit)

    forms_tlx = data_dir / "rq2_forms_raw_tlx_by_participant.csv"
    if forms_tlx.is_file():
        _section_forms(data_dir, emit)

    _section_rq3(data_dir, emit)
    _section_hypotheses(data_dir, emit)
    _emit_table(emit, "Metricas por item (precision)", data_dir / "items_accuracy.csv", max_rows=25)
    _emit_table(emit, "RQ3 calibracion por item", data_dir / "rq3_calibration_by_item.csv", max_rows=25)
    _section_chat_infer(data_dir, emit)
    _section_pilot(data_dir, emit)

    figures = list((data_dir / "figures").glob("*.png"))
    if figures:
        emit("\n=== Graficos disponibles ===\n")
        for fig in sorted(figures):
            emit(f"  {fig.relative_to(data_dir).as_posix()}")

    body = "\n".join(lines) + "\n"
    report_txt = logs_dir / "reporte_rq.txt"
    report_md = logs_dir / "reporte_rq.md"
    report_txt.write_text(body, encoding="utf-8")
    report_md.write_text("# Reporte PF-3311\n\n```text\n" + body + "```\n", encoding="utf-8")
    index_path = _write_index(logs_dir, data_dir)

    print(f"\nLogs en {logs_dir.resolve()}")
    print(f"  {report_txt.name}, {report_md.name}, {index_path.name}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
