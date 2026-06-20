#!/usr/bin/env python3
"""Inferencia estadística (Friedman, Wilcoxon). Requiere scipy."""

from __future__ import annotations

import csv
import random
from dataclasses import dataclass
from pathlib import Path
from typing import Callable

from rq_common import (
    CONDITION_ORDER,
    filter_complete_participants,
    group_metric_values,
    mean,
    write_csv,
)

Emit = Callable[[str], None]

PAIRWISE_COMPARISONS = (("A", "B"), ("A", "C"), ("B", "C"))
N_CONDITIONS = len(CONDITION_ORDER)
BOOTSTRAP_ITERATIONS = 5000
BOOTSTRAP_SEED = 3311

RQ_UNITY_METRICS: tuple[tuple[str, str, str], ...] = (
    ("RQ1", "accuracy", "Precision"),
    ("RQ2", "mean_confidence", "Confianza Unity"),
    ("RQ3", "calibration_gap", "Brecha calibracion"),
)

MECUE_MODULE_LABELS = {
    "MECUE_I": "Utilidad (Mód. I)",
    "MECUE_III": "Emociones (Mód. III)",
    "MECUE_IV": "Consecuencias (Mód. IV)",
    "MECUE_V": "Evaluación global (Mód. V)",
    "MECUE_II": "Intención de reutilización (Mód. II, solo C)",
}


@dataclass
class OmnibusResult:
    test: str
    statistic: float
    p_value: float
    n: int


@dataclass
class PairwiseResult:
    left: str
    right: str
    statistic: float
    p_value: float
    p_adjusted: float
    p_adjusted_holm: float
    significant: bool
    significant_holm: bool


def build_complete_matrix(
    aggregated: dict[str, dict[str, dict[str, float | int]]],
    metric: str,
) -> tuple[list[str], list[tuple[float, float, float]]]:
    keys: list[str] = []
    matrix: list[tuple[float, float, float]] = []
    for p_key in sorted(aggregated):
        by_cond = aggregated[p_key]
        if not all(condition in by_cond for condition in CONDITION_ORDER):
            continue
        try:
            triplet = tuple(float(by_cond[c][metric]) for c in CONDITION_ORDER)  # type: ignore[misc]
        except (KeyError, TypeError, ValueError):
            continue
        keys.append(p_key)
        matrix.append(triplet)  # type: ignore[arg-type]
    return keys, matrix


def kendall_w(friedman_chi2: float, n: int, k: int = N_CONDITIONS) -> float | None:
    """Concordancia W = chi2 / (n * (k - 1)); 0–1, mayor = efecto más fuerte."""
    if n <= 0 or k < 2:
        return None
    return float(friedman_chi2) / (n * (k - 1))


def holm_adjust(p_values: list[float]) -> list[float]:
    """Corrección Holm step-down (menos conservadora que Bonferroni simple)."""
    m = len(p_values)
    if m == 0:
        return []
    order = sorted(range(m), key=lambda i: p_values[i])
    adjusted = [1.0] * m
    prev = 0.0
    for rank, idx in enumerate(order):
        factor = m - rank
        value = min(1.0, p_values[idx] * factor)
        value = max(value, prev)
        adjusted[idx] = value
        prev = value
    return adjusted


def bootstrap_paired_mean_diff_ci(
    left_values: list[float],
    right_values: list[float],
    *,
    n_boot: int = BOOTSTRAP_ITERATIONS,
    seed: int = BOOTSTRAP_SEED,
) -> tuple[float, float, float] | None:
    """IC 95 % bootstrap de la media de (derecha − izquierda) en pares alineados."""
    if len(left_values) < 2 or len(left_values) != len(right_values):
        return None
    diffs = [right - left for left, right in zip(left_values, right_values)]
    mean_diff = sum(diffs) / len(diffs)
    rng = random.Random(seed)
    boot: list[float] = []
    n = len(diffs)
    for _ in range(n_boot):
        sample = [diffs[rng.randrange(n)] for _ in range(n)]
        boot.append(sum(sample) / n)
    boot.sort()
    low_idx = max(0, int(0.025 * n_boot) - 1)
    high_idx = min(n_boot - 1, int(0.975 * n_boot))
    return mean_diff, boot[low_idx], boot[high_idx]


def build_condition_pair_dicts(
    aggregated: dict[str, dict[str, dict[str, float | int]]],
    metric: str,
    left: str,
    right: str,
) -> tuple[dict[str, float], dict[str, float]]:
    left_dict: dict[str, float] = {}
    right_dict: dict[str, float] = {}
    for p_key, by_cond in aggregated.items():
        if left not in by_cond or right not in by_cond:
            continue
        try:
            left_dict[p_key] = float(by_cond[left][metric])
            right_dict[p_key] = float(by_cond[right][metric])
        except (KeyError, TypeError, ValueError):
            continue
    return left_dict, right_dict


def _metric_display_scale(metric: str) -> float:
    return 100.0 if metric == "accuracy" else 1.0


def _aligned_pair_values(
    left_dict: dict[str, float],
    right_dict: dict[str, float],
) -> tuple[list[float], list[float]]:
    keys = sorted(set(left_dict) & set(right_dict))
    return [left_dict[k] for k in keys], [right_dict[k] for k in keys]


def friedman_omnibus(matrix: list[tuple[float, float, float]]) -> OmnibusResult | None:
    if len(matrix) < 3:
        return None
    try:
        from scipy.stats import friedmanchisquare
    except ImportError:
        return None

    a_vals = [row[0] for row in matrix]
    b_vals = [row[1] for row in matrix]
    c_vals = [row[2] for row in matrix]
    stat, p_value = friedmanchisquare(a_vals, b_vals, c_vals)
    return OmnibusResult("Friedman", float(stat), float(p_value), len(matrix))


def wilcoxon_posthoc(
    matrix: list[tuple[float, float, float]],
    alpha: float = 0.05,
) -> list[PairwiseResult] | None:
    if len(matrix) < 3:
        return None
    try:
        from scipy.stats import wilcoxon
    except ImportError:
        return None

    index = {label: idx for idx, label in enumerate(CONDITION_ORDER)}
    n_comparisons = len(PAIRWISE_COMPARISONS)
    results: list[PairwiseResult] = []
    raw_p: list[float] = []

    for left, right in PAIRWISE_COMPARISONS:
        left_vals = [row[index[left]] for row in matrix]
        right_vals = [row[index[right]] for row in matrix]
        try:
            stat, p_value = wilcoxon(left_vals, right_vals, zero_method="wilcox")
        except ValueError:
            stat, p_value = 0.0, 1.0
        raw_p.append(float(p_value))
        results.append(
            PairwiseResult(
                left=left,
                right=right,
                statistic=float(stat),
                p_value=float(p_value),
                p_adjusted=0.0,
                p_adjusted_holm=0.0,
                significant=False,
                significant_holm=False,
            )
        )

    holm = holm_adjust(raw_p)
    for item, p_holm in zip(results, holm):
        p_bonf = min(1.0, item.p_value * n_comparisons)
        item.p_adjusted = p_bonf
        item.p_adjusted_holm = p_holm
        item.significant = p_bonf < alpha
        item.significant_holm = p_holm < alpha
    return results


def wilcoxon_paired(
    left_values: dict[str, float],
    right_values: dict[str, float],
    left_label: str,
    right_label: str,
) -> PairwiseResult | None:
    common = sorted(set(left_values) & set(right_values))
    if len(common) < 3:
        return None
    try:
        from scipy.stats import wilcoxon
    except ImportError:
        return None

    left = [left_values[k] for k in common]
    right = [right_values[k] for k in common]
    try:
        stat, p_value = wilcoxon(left, right, zero_method="wilcox")
    except ValueError:
        stat, p_value = 0.0, 1.0
    p_val = float(p_value)
    return PairwiseResult(
        left=left_label,
        right=right_label,
        statistic=float(stat),
        p_value=p_val,
        p_adjusted=p_val,
        p_adjusted_holm=p_val,
        significant=p_val < 0.05,
        significant_holm=p_val < 0.05,
    )


def print_inference_block(
    emit: Emit,
    metric_label: str,
    matrix: list[tuple[float, float, float]],
) -> None:
    emit("\nInferencia:")
    omnibus = friedman_omnibus(matrix)
    if omnibus is None:
        if len(matrix) < 3:
            emit(
                f"  Friedman no aplicada: se requieren >=3 participantes con A, B y C "
                f"(completos: {len(matrix)})."
            )
        else:
            emit("  Friedman no aplicada: instale scipy (`pip install scipy`).")
        return

    emit(
        f"  {metric_label} | {omnibus.test} chi2={omnibus.statistic:.4f}, "
        f"p={omnibus.p_value:.4f} (n={omnibus.n})"
    )
    w = kendall_w(omnibus.statistic, omnibus.n)
    if w is not None:
        emit(f"  Kendall W = {w:.3f} (0 = sin acuerdo, 1 = ranking idéntico entre condiciones)")

    posthoc = wilcoxon_posthoc(matrix)
    if posthoc is None:
        emit("  Post hoc Wilcoxon pareado: requiere scipy.")
        return

    emit("  Post hoc Wilcoxon pareado (Bonferroni y Holm, 3 comparaciones):")
    for item in posthoc:
        flag = " *" if item.significant_holm else ""
        emit(
            f"    {item.left} vs {item.right}: W={item.statistic:.4f}, "
            f"p={item.p_value:.4f}, p_adj_Bonf={item.p_adjusted:.4f}, "
            f"p_adj_Holm={item.p_adjusted_holm:.4f}{flag}"
        )


def _group_means(
    aggregated: dict[str, dict[str, dict[str, float | int]]],
    metric: str,
) -> dict[str, float | None]:
    return {c: mean(group_metric_values(aggregated, metric, c)) for c in CONDITION_ORDER}


def print_hypothesis_summary(
    emit: Emit,
    aggregated: dict[str, dict[str, dict[str, float | int]]],
) -> None:
    acc = _group_means(aggregated, "accuracy")
    conf = _group_means(aggregated, "mean_confidence")
    gap = _group_means(aggregated, "calibration_gap")

    emit("\n=== Hipotesis exploratorias (medias grupales) ===\n")

    def fmt_pct(value: float | None) -> str:
        return f"{100.0 * value:.1f}%" if value is not None else "n/a"

    def fmt_num(value: float | None, digits: int = 2) -> str:
        return f"{value:.{digits}f}" if value is not None else "n/a"

    emit(
        f"H1 (precision C ~ B > A): "
        f"A={fmt_pct(acc['A'])} | B={fmt_pct(acc['B'])} | C={fmt_pct(acc['C'])}"
    )
    if all(acc[c] is not None for c in CONDITION_ORDER):
        ordered = sorted(CONDITION_ORDER, key=lambda c: acc[c] or 0.0, reverse=True)
        emit(f"  Orden observado (mayor a menor): {' > '.join(ordered)}")

    emit(
        f"\nH2 (confianza C > B > A): "
        f"A={fmt_num(conf['A'])} | B={fmt_num(conf['B'])} | C={fmt_num(conf['C'])}"
    )
    if all(conf[c] is not None for c in CONDITION_ORDER):
        ordered = sorted(CONDITION_ORDER, key=lambda c: conf[c] or 0.0, reverse=True)
        emit(f"  Orden observado (mayor a menor): {' > '.join(ordered)}")

    emit(
        f"\nH3 (mayor brecha en C): "
        f"A={fmt_num(gap['A'], 3)} | B={fmt_num(gap['B'], 3)} | C={fmt_num(gap['C'], 3)}"
    )
    if all(gap[c] is not None for c in CONDITION_ORDER):
        ordered = sorted(CONDITION_ORDER, key=lambda c: gap[c] or 0.0, reverse=True)
        emit(f"  Orden observado (mayor brecha primero): {' > '.join(ordered)}")
        if gap["C"] is not None and gap["A"] is not None:
            direction = "consistente con H3" if gap["C"] >= gap["A"] else "no consistente con H3"
            emit(f"  Brecha C vs A: {direction}")


def print_pairwise_result(emit: Emit, label: str, result: PairwiseResult | None) -> None:
    if result is None:
        emit(f"  {label}: requiere >=3 participantes emparejados y scipy.")
        return
    flag = " *" if result.significant_holm else ""
    emit(
        f"  {label} | Wilcoxon {result.left} vs {result.right}: "
        f"W={result.statistic:.4f}, p={result.p_value:.4f}, "
        f"p_adj_Holm={result.p_adjusted_holm:.4f}{flag}"
    )


def collect_mecue_b_vs_c_pairs(
    rows: list[dict[str, str]],
) -> dict[str, tuple[dict[str, float], dict[str, float]]]:
    by_module: dict[str, tuple[dict[str, float], dict[str, float]]] = {}
    for row in rows:
        module = (row.get("Module") or "").strip()
        code = (row.get("ParticipantCode") or "").strip()
        if not module or not code:
            continue
        b_dict, c_dict = by_module.setdefault(module, ({}, {}))
        for key, target in (("Score_B", b_dict), ("Score_C", c_dict)):
            raw = (row.get(key) or "").strip()
            if not raw:
                continue
            try:
                target[code] = float(raw)
            except ValueError:
                continue
    return by_module


def infer_mecue_b_vs_c(
    rows: list[dict[str, str]],
    *,
    alpha: float = 0.05,
) -> list[tuple[str, PairwiseResult | None]]:
    by_module = collect_mecue_b_vs_c_pairs(rows)
    modules = sorted(by_module)
    if not modules:
        return []

    pending: list[tuple[str, PairwiseResult]] = []
    for module in modules:
        b_dict, c_dict = by_module[module]
        result = wilcoxon_paired(b_dict, c_dict, "B", "C")
        if result is not None:
            pending.append((module, result))

    if not pending:
        return [(module, None) for module in modules]

    holm = holm_adjust([item.p_value for _, item in pending])
    bonf_factor = len(pending)
    output: list[tuple[str, PairwiseResult | None]] = []
    holm_idx = 0
    for module in modules:
        match = next(((m, r) for m, r in pending if m == module), None)
        if match is None:
            output.append((module, None))
            continue
        _, result = match
        p_bonf = min(1.0, result.p_value * bonf_factor)
        p_holm = holm[holm_idx]
        holm_idx += 1
        output.append(
            (
                module,
                PairwiseResult(
                    left=result.left,
                    right=result.right,
                    statistic=result.statistic,
                    p_value=result.p_value,
                    p_adjusted=p_bonf,
                    p_adjusted_holm=p_holm,
                    significant=p_bonf < alpha,
                    significant_holm=p_holm < alpha,
                ),
            )
        )
    return output


def print_mecue_b_vs_c_inference(
    emit: Emit,
    rows: list[dict[str, str]],
    *,
    module_labels: dict[str, str] | None = None,
    alpha: float = 0.05,
) -> None:
    labels = module_labels or MECUE_MODULE_LABELS
    results = infer_mecue_b_vs_c(rows, alpha=alpha)
    if not results:
        emit("  meCUE B vs C: sin datos emparejados.")
        return

    emit(
        f"  Wilcoxon pareado B vs C por módulo meCUE "
        f"(p ajustada Bonferroni, {len(results)} comparaciones):"
    )
    for module, result in results:
        label = labels.get(module, module)
        print_pairwise_result(emit, label, result)


def _read_csv_rows(path: Path) -> list[dict[str, str]]:
    if not path.is_file():
        return []
    with path.open(encoding="utf-8-sig", newline="") as handle:
        return list(csv.DictReader(handle))


def _parse_float(value: str) -> float | None:
    text = (value or "").strip()
    if not text:
        return None
    try:
        return float(text)
    except ValueError:
        return None


def build_raw_tlx_matrix(path: Path) -> list[tuple[float, float, float]]:
    matrix: list[tuple[float, float, float]] = []
    for row in _read_csv_rows(path):
        a = _parse_float(row.get("RAW_TLX_A", ""))
        b = _parse_float(row.get("RAW_TLX_B", ""))
        c = _parse_float(row.get("RAW_TLX_C", ""))
        if a is not None and b is not None and c is not None:
            matrix.append((a, b, c))
    return matrix


def _omnibus_row(
    analysis_id: str,
    metric: str,
    label: str,
    matrix: list[tuple[float, float, float]],
) -> list[object] | None:
    result = friedman_omnibus(matrix)
    if result is None:
        return None
    w = kendall_w(result.statistic, result.n)
    return [
        analysis_id,
        metric,
        label,
        result.test,
        round(result.statistic, 6),
        round(result.p_value, 6),
        round(w, 6) if w is not None else "",
        result.n,
        len(matrix) >= 3,
    ]


def _effect_size_row(
    analysis_id: str,
    metric: str,
    label: str,
    matrix: list[tuple[float, float, float]],
) -> list[object] | None:
    result = friedman_omnibus(matrix)
    if result is None:
        return None
    w = kendall_w(result.statistic, result.n)
    return [
        analysis_id,
        metric,
        label,
        round(w, 6) if w is not None else "",
        round(result.statistic, 6),
        result.n,
    ]


def _bootstrap_rows(
    analysis_id: str,
    metric: str,
    label: str,
    matrix: list[tuple[float, float, float]],
) -> list[list[object]]:
    index = {cond: idx for idx, cond in enumerate(CONDITION_ORDER)}
    scale = _metric_display_scale(metric)
    units = "pp" if metric == "accuracy" else "units"
    rows: list[list[object]] = []
    for left, right in PAIRWISE_COMPARISONS:
        left_vals = [row[index[left]] for row in matrix]
        right_vals = [row[index[right]] for row in matrix]
        ci = bootstrap_paired_mean_diff_ci(left_vals, right_vals)
        if ci is None:
            continue
        mean_d, lo, hi = ci
        rows.append(
            [
                analysis_id,
                metric,
                label,
                left,
                right,
                round(mean_d * scale, 4),
                round(lo * scale, 4),
                round(hi * scale, 4),
                units,
                len(matrix),
            ]
        )
    return rows


def _contrast_row(
    contrast_id: str,
    description: str,
    analysis_id: str,
    metric: str,
    left: str,
    right: str,
    left_dict: dict[str, float],
    right_dict: dict[str, float],
) -> list[object] | None:
    left_vals, right_vals = _aligned_pair_values(left_dict, right_dict)
    if len(left_vals) < 3:
        return None
    result = wilcoxon_paired(left_dict, right_dict, left, right)
    ci = bootstrap_paired_mean_diff_ci(left_vals, right_vals)
    scale = _metric_display_scale(metric)
    units = "pp" if metric == "accuracy" else "units"
    mean_d = ci[0] * scale if ci else ""
    lo = ci[1] * scale if ci else ""
    hi = ci[2] * scale if ci else ""
    return [
        contrast_id,
        description,
        analysis_id,
        metric,
        left,
        right,
        round(mean_d, 4) if mean_d != "" else "",
        round(lo, 4) if lo != "" else "",
        round(hi, 4) if hi != "" else "",
        units,
        round(result.statistic, 6) if result else "",
        round(result.p_value, 6) if result else "",
        int(result.significant) if result else 0,
        len(left_vals),
    ]


def _hypothesis_contrast_rows(
    data: dict[str, dict[str, dict[str, float | int]]],
) -> list[list[object]]:
    rows: list[list[object]] = []
    specs = (
        (
            "H3_C_vs_A",
            "H3: brecha de calibración mayor en C que en A",
            "RQ3",
            "calibration_gap",
            "A",
            "C",
        ),
        (
            "H3_C_vs_B",
            "H3 exploratorio: brecha C vs B",
            "RQ3",
            "calibration_gap",
            "B",
            "C",
        ),
        (
            "VIS_B_vs_C",
            "Contraste clave: precisión B vs C (misma ayuda LLM)",
            "RQ1",
            "accuracy",
            "B",
            "C",
        ),
    )
    for contrast_id, desc, analysis_id, metric, left, right in specs:
        left_d, right_d = build_condition_pair_dicts(data, metric, left, right)
        row = _contrast_row(contrast_id, desc, analysis_id, metric, left, right, left_d, right_d)
        if row:
            rows.append(row)
    return rows


def _help_score_contrast(path: Path) -> list[object] | None:
    b_dict: dict[str, float] = {}
    c_dict: dict[str, float] = {}
    for row in _read_csv_rows(path):
        code = (row.get("ParticipantCode") or "").strip()
        hb = _parse_float(row.get("HelpScore_B", ""))
        hc = _parse_float(row.get("HelpScore_C", ""))
        if code and hb is not None and hc is not None:
            b_dict[code] = hb
            c_dict[code] = hc
    return _contrast_row(
        "AGENT_HELPSCORE",
        "HelpScore pedagógico B vs C",
        "AGENT",
        "HelpScore",
        "B",
        "C",
        b_dict,
        c_dict,
    )


def _pairwise_rows(
    analysis_id: str,
    metric: str,
    label: str,
    matrix: list[tuple[float, float, float]],
) -> list[list[object]]:
    posthoc = wilcoxon_posthoc(matrix)
    if not posthoc:
        return []
    rows: list[list[object]] = []
    for item in posthoc:
        rows.append(
            [
                analysis_id,
                metric,
                label,
                item.left,
                item.right,
                round(item.statistic, 6),
                round(item.p_value, 6),
                round(item.p_adjusted, 6),
                round(item.p_adjusted_holm, 6),
                int(item.significant),
                int(item.significant_holm),
            ]
        )
    return rows


def _mecue_pairwise_rows(
    rows: list[dict[str, str]],
    *,
    alpha: float = 0.05,
) -> list[list[object]]:
    output: list[list[object]] = []
    for module, result in infer_mecue_b_vs_c(rows, alpha=alpha):
        if result is None:
            output.append(
                [
                    "RQ2_FORMS",
                    module,
                    MECUE_MODULE_LABELS.get(module, module),
                    "B",
                    "C",
                    "",
                    "",
                    "",
                    "",
                    "",
                    0,
                    0,
                    0,
                ]
            )
            continue
        output.append(
            [
                "RQ2_FORMS",
                module,
                MECUE_MODULE_LABELS.get(module, module),
                result.left,
                result.right,
                round(result.statistic, 6),
                round(result.p_value, 6),
                round(result.p_adjusted, 6),
                round(result.p_adjusted_holm, 6),
                int(result.significant),
                int(result.significant_holm),
                1,
            ]
        )
    return output


def run(
    aggregated: dict[str, dict[str, dict[str, float | int]]],
    output_dir: Path,
    *,
    complete_only: bool = True,
    emit: Emit | None = None,
) -> int:
    """Exporta inferencia RQ1–RQ3 (y Forms si hay CSV) a tablas y reporte breve."""
    data = filter_complete_participants(aggregated) if complete_only else aggregated

    omnibus_rows: list[list[object]] = []
    pairwise_rows: list[list[object]] = []
    effect_rows: list[list[object]] = []
    bootstrap_rows: list[list[object]] = []
    contrast_rows: list[list[object]] = []
    report_lines: list[str] = []

    def log(text: str = "") -> None:
        if emit is not None:
            emit(text)
        report_lines.append(text)

    log("PF-3311: resumen de inferencia estadistica (insumo para redaccion manual)")
    log(f"Subconjunto: {'participantes completos (6+6+6)' if complete_only else 'todos con A/B/C'}")
    log("")

    for rq_id, metric, label in RQ_UNITY_METRICS:
        _, matrix = build_complete_matrix(data, metric)
        log(f"=== {rq_id}: {label} (n={len(matrix)}) ===")
        row = _omnibus_row(rq_id, metric, label, matrix)
        if row:
            omnibus_rows.append(row)
        es = _effect_size_row(rq_id, metric, label, matrix)
        if es:
            effect_rows.append(es)
        bootstrap_rows.extend(_bootstrap_rows(rq_id, metric, label, matrix))
        pairwise_rows.extend(_pairwise_rows(rq_id, metric, label, matrix))
        print_inference_block(log, label, matrix)
        log("")

    contrast_rows.extend(_hypothesis_contrast_rows(data))
    if contrast_rows:
        log("=== Contrastes dirigidos (H3 y B vs C) ===")
        for row in contrast_rows:
            log(
                f"  {row[0]} ({row[4]} vs {row[5]}): Δ = {row[6]} [{row[7]}, {row[8]}] {row[9]}, "
                f"Wilcoxon p = {row[11]} (n = {row[13]})"
            )
        log("")

    chat_path = output_dir / "chat_quality_by_participant.csv"
    help_row = _help_score_contrast(chat_path) if chat_path.is_file() else None
    if help_row:
        contrast_rows.append(help_row)
        log("=== Agente — HelpScore B vs C ===")
        log(
            f"  Δ = {help_row[6]} [{help_row[7]}, {help_row[8]}] {help_row[9]}, "
            f"Wilcoxon p = {help_row[11]} (n = {help_row[13]})"
        )
        log("")

    tlx_path = output_dir / "rq2_forms_raw_tlx_by_participant.csv"
    if tlx_path.is_file():
        tlx_matrix = build_raw_tlx_matrix(tlx_path)
        log("=== RQ2 Forms — RAW-TLX ===")
        row = _omnibus_row("RQ2_FORMS", "RAW_TLX", "RAW-TLX", tlx_matrix)
        if row:
            omnibus_rows.append(row)
        es = _effect_size_row("RQ2_FORMS", "RAW_TLX", "RAW-TLX", tlx_matrix)
        if es:
            effect_rows.append(es)
        bootstrap_rows.extend(_bootstrap_rows("RQ2_FORMS", "RAW_TLX", "RAW-TLX", tlx_matrix))
        pairwise_rows.extend(_pairwise_rows("RQ2_FORMS", "RAW_TLX", "RAW-TLX", tlx_matrix))
        print_inference_block(log, "RAW-TLX", tlx_matrix)
        log("")

    mecue_path = output_dir / "rq2_forms_mecue_b_vs_c.csv"
    mecue_rows = _read_csv_rows(mecue_path)
    if mecue_rows:
        log("=== RQ2 Forms — meCUE B vs C ===")
        print_mecue_b_vs_c_inference(log, mecue_rows)
        log("")

    print_hypothesis_summary(log, data)

    write_csv(
        output_dir / "rq_inference_omnibus.csv",
        [
            "AnalysisId",
            "Metric",
            "Label",
            "Test",
            "Statistic",
            "PValue",
            "KendallW",
            "N",
            "Applied",
        ],
        omnibus_rows,
    )
    write_csv(
        output_dir / "rq_inference_pairwise.csv",
        [
            "AnalysisId",
            "Metric",
            "Label",
            "Left",
            "Right",
            "Statistic",
            "PValue",
            "PAdjustedBonferroni",
            "PAdjustedHolm",
            "SignificantBonferroni",
            "SignificantHolm",
        ],
        pairwise_rows,
    )
    write_csv(
        output_dir / "rq_inference_effect_sizes.csv",
        ["AnalysisId", "Metric", "Label", "KendallW", "FriedmanChi2", "N"],
        effect_rows,
    )
    write_csv(
        output_dir / "rq_inference_bootstrap_ci.csv",
        [
            "AnalysisId",
            "Metric",
            "Label",
            "Left",
            "Right",
            "MeanDiff",
            "CI_Low",
            "CI_High",
            "Units",
            "N",
        ],
        bootstrap_rows,
    )
    write_csv(
        output_dir / "rq_inference_hypothesis_contrasts.csv",
        [
            "ContrastId",
            "Description",
            "AnalysisId",
            "Metric",
            "Left",
            "Right",
            "MeanDiff",
            "CI_Low",
            "CI_High",
            "Units",
            "WilcoxonW",
            "PValue",
            "Significant",
            "N",
        ],
        contrast_rows,
    )
    if mecue_rows:
        write_csv(
            output_dir / "rq_inference_mecue_b_vs_c.csv",
            [
                "AnalysisId",
                "Module",
                "ModuleLabel",
                "Left",
                "Right",
                "Statistic",
                "PValue",
                "PAdjustedBonferroni",
                "PAdjustedHolm",
                "SignificantBonferroni",
                "SignificantHolm",
                "Applied",
            ],
            _mecue_pairwise_rows(mecue_rows),
        )

    (output_dir / "reporte_inferencia.txt").write_text(
        "\n".join(report_lines) + "\n",
        encoding="utf-8",
    )
    return 0
