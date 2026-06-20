#!/usr/bin/env python3
"""Genera graficos PNG desde tablas CSV del pipeline de analisis (_analysis/)."""

from __future__ import annotations

import csv
import statistics
from pathlib import Path

DPI = 300
FIGURE_FACE = "#FFFFFF"
AXES_FACE = "#F8FAFC"
GRID_COLOR = "#E2E8F0"
TEXT_COLOR = "#0F172A"
MUTED_COLOR = "#64748B"
ERROR_COLOR = "#334155"

CONDITION_COLORS = {
    "A": "#64748B",
    "B": "#0284C7",
    "C": "#7C3AED",
}
BC_COLORS = [CONDITION_COLORS["B"], CONDITION_COLORS["C"]]

CONDITION_LABELS = {
    "A": "Sin agente",
    "B": "Chat texto",
    "C": "Avatar y voz",
}


def _configure_matplotlib() -> None:
    import matplotlib as mpl

    mpl.rcParams.update(
        {
            "figure.dpi": DPI,
            "savefig.dpi": DPI,
            "font.family": "sans-serif",
            "font.sans-serif": [
                "Segoe UI",
                "Helvetica Neue",
                "Arial",
                "DejaVu Sans",
            ],
            "font.size": 10,
            "axes.titlesize": 14,
            "axes.titleweight": "600",
            "axes.titlecolor": TEXT_COLOR,
            "axes.labelsize": 11,
            "axes.labelcolor": TEXT_COLOR,
            "axes.edgecolor": GRID_COLOR,
            "axes.facecolor": AXES_FACE,
            "axes.linewidth": 0.8,
            "figure.facecolor": FIGURE_FACE,
            "xtick.color": MUTED_COLOR,
            "ytick.color": MUTED_COLOR,
            "grid.color": GRID_COLOR,
            "grid.linewidth": 0.7,
            "grid.alpha": 0.9,
        }
    )


def _sem(values: list[float]) -> float:
    if len(values) < 2:
        return 0.0
    return statistics.stdev(values) / (len(values) ** 0.5)


def _read_group_means(path: Path, value_col: str) -> tuple[list[str], list[float], list[str]]:
    if not path.is_file():
        return [], [], []
    keys: list[str] = []
    labels: list[str] = []
    values: list[float] = []
    with path.open(encoding="utf-8-sig", newline="") as handle:
        reader = csv.DictReader(handle)
        for row in reader:
            cond = (row.get("Condition") or "").strip()
            raw = (row.get(value_col) or "").strip()
            if cond and raw:
                try:
                    keys.append(cond)
                    labels.append(CONDITION_LABELS.get(cond, cond))
                    values.append(float(raw))
                except ValueError:
                    continue
    return keys, labels, values


def _group_means_path(output_dir: Path, prefix: str, complete_only: bool) -> Path:
    complete_path = output_dir / f"{prefix}_group_means_complete.csv"
    all_path = output_dir / f"{prefix}_group_means.csv"
    if complete_only and complete_path.is_file():
        return complete_path
    return all_path


def _filter_complete_participant_rows(
    rows: list[dict[str, str]],
    item_cols: tuple[str, str, str] = ("Items_A", "Items_B", "Items_C"),
    min_items: int = 6,
) -> list[dict[str, str]]:
    filtered: list[dict[str, str]] = []
    for row in rows:
        if all(
            (row.get(col) or "").strip().isdigit() and int(row[col]) >= min_items
            for col in item_cols
        ):
            filtered.append(row)
    return filtered


def _read_participant_rows(path: Path, complete_only: bool) -> list[dict[str, str]]:
    if not path.is_file():
        return []
    with path.open(encoding="utf-8-sig", newline="") as handle:
        rows = list(csv.DictReader(handle))
    if complete_only:
        return _filter_complete_participant_rows(rows)
    return rows


def _participant_series_from_rows(
    rows: list[dict[str, str]], columns: dict[str, str]
) -> dict[str, list[float]]:
    out: dict[str, list[float]] = {key: [] for key in columns}
    for row in rows:
        for key, col in columns.items():
            raw = (row.get(col) or "").strip()
            if not raw:
                continue
            try:
                out[key].append(float(raw))
            except ValueError:
                continue
    return out


def _participant_series(path: Path, columns: dict[str, str]) -> dict[str, list[float]]:
    out: dict[str, list[float]] = {key: [] for key in columns}
    if not path.is_file():
        return out
    with path.open(encoding="utf-8-sig", newline="") as handle:
        reader = csv.DictReader(handle)
        for row in reader:
            for key, col in columns.items():
                raw = (row.get(col) or "").strip()
                if not raw:
                    continue
                try:
                    out[key].append(float(raw))
                except ValueError:
                    continue
    return out


def _bar_chart(
    labels: list[str],
    values: list[float],
    title: str,
    ylabel: str,
    out_path: Path,
    *,
    colors: list[str] | None = None,
    yerr: list[float] | None = None,
    ylim: tuple[float, float] | None = None,
) -> bool:
    try:
        import matplotlib.pyplot as plt
        import numpy as np
    except ImportError:
        return False

    if not labels or not values:
        return False

    _configure_matplotlib()

    palette = colors or [CONDITION_COLORS.get(label[:1], "#475569") for label in labels]
    if len(palette) < len(values):
        palette = (palette * len(values))[: len(values)]

    x = np.arange(len(values))
    width = 0.58

    fig, ax = plt.subplots(figsize=(7.2, 4.6))
    fig.patch.set_facecolor(FIGURE_FACE)
    ax.set_facecolor(AXES_FACE)

    bars = ax.bar(
        x,
        values,
        width=width,
        color=palette[: len(values)],
        edgecolor="white",
        linewidth=1.4,
        zorder=3,
        yerr=yerr,
        error_kw={
            "ecolor": ERROR_COLOR,
            "elinewidth": 1.1,
            "capsize": 4.5,
            "capthick": 1.1,
            "zorder": 4,
        }
        if yerr
        else None,
    )

    ax.set_title(title, pad=14)
    ax.set_ylabel(ylabel, labelpad=8)
    ax.set_xticks(x)
    ax.set_xticklabels(labels, fontsize=10.5)
    ax.set_axisbelow(True)
    ax.yaxis.grid(True, linestyle="-", alpha=0.55)
    ax.xaxis.grid(False)
    ax.spines["top"].set_visible(False)
    ax.spines["right"].set_visible(False)
    ax.spines["left"].set_color(GRID_COLOR)
    ax.spines["bottom"].set_color(GRID_COLOR)

    if ylim:
        ax.set_ylim(*ylim)
        y_range = ylim[1] - ylim[0]
        offset = y_range * 0.025
    else:
        ymax = max(values)
        if yerr:
            ymax = max(v + e for v, e in zip(values, yerr))
        ax.set_ylim(0, ymax * 1.18 if ymax > 0 else 1)
        offset = ymax * 0.03 if ymax > 0 else 0.05

    for idx, (bar, val) in enumerate(zip(bars, values)):
        err = yerr[idx] if yerr else 0.0
        ax.text(
            bar.get_x() + bar.get_width() / 2,
            bar.get_height() + err + offset,
            f"{val:.1f}",
            ha="center",
            va="bottom",
            fontsize=9.5,
            fontweight="600",
            color=TEXT_COLOR,
            zorder=5,
        )

    fig.tight_layout()
    out_path.parent.mkdir(parents=True, exist_ok=True)
    fig.savefig(
        out_path,
        dpi=DPI,
        bbox_inches="tight",
        facecolor=FIGURE_FACE,
        edgecolor="none",
        pad_inches=0.08,
    )
    plt.close(fig)
    return True


def _grouped_bc_chart(
    categories: list[str],
    values_b: list[float],
    values_c: list[float],
    title: str,
    ylabel: str,
    out_path: Path,
    *,
    ylim: tuple[float, float] | None = None,
) -> bool:
    try:
        import matplotlib.pyplot as plt
        import numpy as np
    except ImportError:
        return False

    if not categories or not values_b or not values_c:
        return False

    _configure_matplotlib()
    x = np.arange(len(categories))
    width = 0.34

    fig, ax = plt.subplots(figsize=(8.0, 4.8))
    fig.patch.set_facecolor(FIGURE_FACE)
    ax.set_facecolor(AXES_FACE)

    bars_b = ax.bar(x - width / 2, values_b, width, label=CONDITION_LABELS["B"], color=BC_COLORS[0], edgecolor="white", linewidth=1.2)
    bars_c = ax.bar(x + width / 2, values_c, width, label=CONDITION_LABELS["C"], color=BC_COLORS[1], edgecolor="white", linewidth=1.2)

    ax.set_title(title, pad=14)
    ax.set_ylabel(ylabel, labelpad=8)
    ax.set_xticks(x)
    ax.set_xticklabels(categories, fontsize=9.5, rotation=15, ha="right")
    ax.legend(frameon=False, loc="upper right")
    ax.set_axisbelow(True)
    ax.yaxis.grid(True, linestyle="-", alpha=0.55)
    ax.spines["top"].set_visible(False)
    ax.spines["right"].set_visible(False)
    if ylim:
        ax.set_ylim(*ylim)

    for bars in (bars_b, bars_c):
        for bar in bars:
            h = bar.get_height()
            ax.text(
                bar.get_x() + bar.get_width() / 2,
                h + (0.08 if ylim else h * 0.02 + 0.05),
                f"{h:.1f}",
                ha="center",
                va="bottom",
                fontsize=8.5,
                fontweight="600",
                color=TEXT_COLOR,
            )

    fig.tight_layout()
    out_path.parent.mkdir(parents=True, exist_ok=True)
    fig.savefig(out_path, dpi=DPI, bbox_inches="tight", facecolor=FIGURE_FACE, edgecolor="none", pad_inches=0.08)
    plt.close(fig)
    return True


def _forms_tlx_chart(path: Path, figures_dir: Path) -> bool:
    keys, labels, values = _read_group_means(path, "MeanRAWTLX")
    if not values:
        return False
    colors = [CONDITION_COLORS.get(k, "#475569") for k in keys]
    return _bar_chart(
        labels,
        values,
        "Carga de trabajo (RAW-TLX) por condición",
        "Media RAW-TLX (1–7)",
        figures_dir / "forms_raw_tlx_by_condition.png",
        colors=colors,
        ylim=(1, 7),
    )


def _mecue_b_vs_c_chart(path: Path, figures_dir: Path) -> bool:
    if not path.is_file():
        return False
    labels: list[str] = []
    vals_b: list[float] = []
    vals_c: list[float] = []
    with path.open(encoding="utf-8-sig", newline="") as handle:
        reader = csv.DictReader(handle)
        for row in reader:
            label = (row.get("ModuleLabel") or row.get("Module") or "").strip()
            mb = (row.get("Mean_B") or "").strip()
            mc = (row.get("Mean_C") or "").strip()
            if not label or not mb or not mc:
                continue
            try:
                labels.append(label.replace(" (Mód. ", "\n(Mód. "))
                vals_b.append(float(mb))
                vals_c.append(float(mc))
            except ValueError:
                continue
    return _grouped_bc_chart(
        labels,
        vals_b,
        vals_c,
        "meCUE — Comparación B vs C",
        "Puntuación media (1–7)",
        figures_dir / "forms_mecue_b_vs_c.png",
        ylim=(1, 7),
    )


def _time_by_condition_chart(path: Path, figures_dir: Path) -> bool:
    keys, labels, values = _read_group_means(path, "MeanTimeSeconds")
    if not values:
        return False
    colors = [CONDITION_COLORS.get(k, "#475569") for k in keys]
    return _bar_chart(
        labels,
        values,
        "Tiempo medio de respuesta por condición",
        "Segundos (promedio por ítem)",
        figures_dir / "items_time_by_condition.png",
        colors=colors,
    )


def _calibration_gap_by_item_chart(path: Path, figures_dir: Path) -> bool:
    try:
        import matplotlib.pyplot as plt
        import numpy as np
    except ImportError:
        return False

    if not path.is_file():
        return False

    by_question: dict[int, dict[str, float]] = {q: {} for q in range(1, 7)}
    with path.open(encoding="utf-8-sig", newline="") as handle:
        reader = csv.DictReader(handle)
        for row in reader:
            cond = (row.get("Condition") or "").strip()
            raw_q = (row.get("QuestionNumber") or "").strip()
            raw_gap = (row.get("CalibrationGap") or "").strip()
            if not cond or not raw_q.isdigit() or not raw_gap:
                continue
            try:
                by_question[int(raw_q)][cond] = float(raw_gap)
            except ValueError:
                continue

    questions = [q for q in range(1, 7) if by_question[q]]
    if not questions:
        return False

    _configure_matplotlib()
    x = np.arange(len(questions))
    width = 0.24
    offsets = {"A": -width, "B": 0.0, "C": width}

    fig, ax = plt.subplots(figsize=(8.4, 4.8))
    fig.patch.set_facecolor(FIGURE_FACE)
    ax.set_facecolor(AXES_FACE)

    for cond in ("A", "B", "C"):
        values = [by_question[q].get(cond, 0.0) for q in questions]
        if not any(cond in by_question[q] for q in questions):
            continue
        ax.bar(
            x + offsets[cond],
            values,
            width,
            label=CONDITION_LABELS[cond],
            color=CONDITION_COLORS[cond],
            edgecolor="white",
            linewidth=1.1,
        )

    ax.axhline(0, color=MUTED_COLOR, linewidth=0.9, linestyle="--", alpha=0.8)
    ax.set_title("RQ3 — Brecha de calibración por ítem (Q1–Q6)", pad=14)
    ax.set_ylabel("Brecha (conf. norm. − precisión)", labelpad=8)
    ax.set_xticks(x)
    ax.set_xticklabels([f"Q{q}" for q in questions])
    ax.legend(frameon=False, loc="upper right")
    ax.set_axisbelow(True)
    ax.yaxis.grid(True, linestyle="-", alpha=0.55)
    ax.spines["top"].set_visible(False)
    ax.spines["right"].set_visible(False)

    fig.tight_layout()
    out_path = figures_dir / "rq3_calibration_gap_by_item.png"
    out_path.parent.mkdir(parents=True, exist_ok=True)
    fig.savefig(out_path, dpi=DPI, bbox_inches="tight", facecolor=FIGURE_FACE, edgecolor="none", pad_inches=0.08)
    plt.close(fig)
    return True


def _calibration_curve_by_item_chart(path: Path, figures_dir: Path) -> bool:
    try:
        import matplotlib.pyplot as plt
    except ImportError:
        return False

    if not path.is_file():
        return False

    points: list[tuple[str, int, float, float]] = []
    with path.open(encoding="utf-8-sig", newline="") as handle:
        reader = csv.DictReader(handle)
        for row in reader:
            cond = (row.get("Condition") or "").strip()
            raw_q = (row.get("QuestionNumber") or "").strip()
            raw_x = (row.get("MeanConfidenceNorm") or "").strip()
            raw_y = (row.get("AccuracyPct") or "").strip()
            if not cond or not raw_q.isdigit() or not raw_x or not raw_y:
                continue
            try:
                points.append((cond, int(raw_q), float(raw_x), float(raw_y) / 100.0))
            except ValueError:
                continue

    if not points:
        return False

    _configure_matplotlib()
    fig, ax = plt.subplots(figsize=(6.8, 6.0))
    fig.patch.set_facecolor(FIGURE_FACE)
    ax.set_facecolor(AXES_FACE)

    for cond in ("A", "B", "C"):
        subset = [(q, x, y) for c, q, x, y in points if c == cond]
        if not subset:
            continue
        ax.scatter(
            [p[1] for p in subset],
            [p[2] for p in subset],
            s=72,
            color=CONDITION_COLORS[cond],
            edgecolor="white",
            linewidth=1.2,
            label=CONDITION_LABELS[cond],
            zorder=3,
        )
        for q, x_val, y_val in subset:
            ax.annotate(
                f"Q{q}",
                (x_val, y_val),
                textcoords="offset points",
                xytext=(4, 4),
                fontsize=8,
                color=TEXT_COLOR,
            )

    ax.plot([0, 1], [0, 1], linestyle="--", color=MUTED_COLOR, linewidth=1.0, label="Calibración perfecta")
    ax.set_xlim(0, 1)
    ax.set_ylim(0, 1)
    ax.set_title("RQ3 — Curva de calibración por ítem", pad=14)
    ax.set_xlabel("Confianza normalizada ((C−1)/6)", labelpad=8)
    ax.set_ylabel("Proporción de aciertos", labelpad=8)
    ax.legend(frameon=False, loc="lower right")
    ax.set_axisbelow(True)
    ax.grid(True, linestyle="-", alpha=0.35)
    ax.spines["top"].set_visible(False)
    ax.spines["right"].set_visible(False)

    fig.tight_layout()
    out_path = figures_dir / "rq3_calibration_curve_by_item.png"
    out_path.parent.mkdir(parents=True, exist_ok=True)
    fig.savefig(out_path, dpi=DPI, bbox_inches="tight", facecolor=FIGURE_FACE, edgecolor="none", pad_inches=0.08)
    plt.close(fig)
    return True


AGE_RANGE_ORDER = (
    "18–24 años",
    "25–34 años",
    "35–44 años",
    "45–54 años",
    "55–64 años",
    "65 años o más",
)

ASSISTANT_FREQ_ORDER = (
    "Nunca o casi nunca",
    "Algunas veces al mes",
    "Algunas veces por semana",
    "Casi todos los días",
    "Varias veces al día",
)

AVATAR_EXP_ORDER = (
    "Nunca",
    "Una o pocas veces",
    "Algunas veces",
    "Con frecuencia",
)

PERFIL_PANELS = (
    ("AgeRange", "Edad", AGE_RANGE_ORDER),
    ("AssistantFrequency", "Uso de asistentes digitales", ASSISTANT_FREQ_ORDER),
    ("AvatarExperience", "Experiencia con avatares/agentes", AVATAR_EXP_ORDER),
)

PERFIL_BAR_COLOR = "#0284C7"


def _ordered_value_counts(
    rows: list[dict[str, str]],
    key: str,
    order: tuple[str, ...],
) -> tuple[list[str], list[int]]:
    counts: dict[str, int] = {}
    for row in rows:
        label = (row.get(key) or "").strip()
        if label:
            counts[label] = counts.get(label, 0) + 1

    labels: list[str] = [label for label in order if counts.get(label, 0) > 0]
    for label in sorted(counts):
        if label not in labels:
            labels.append(label)
    values = [counts[label] for label in labels]
    return labels, values


def _perfil_muestra_chart(path: Path, figures_dir: Path) -> bool:
    try:
        import matplotlib.pyplot as plt
        import numpy as np
    except ImportError:
        return False

    if not path.is_file():
        return False

    with path.open(encoding="utf-8-sig", newline="") as handle:
        rows = list(csv.DictReader(handle))
    if not rows:
        return False

    _configure_matplotlib()
    n_total = len(rows)

    fig, axes = plt.subplots(1, 3, figsize=(12.5, 4.6))
    fig.patch.set_facecolor(FIGURE_FACE)

    for ax, (key, title, order) in zip(axes, PERFIL_PANELS):
        labels, values = _ordered_value_counts(rows, key, order)
        if not labels:
            ax.set_visible(False)
            continue

        ax.set_facecolor(AXES_FACE)
        y = np.arange(len(labels))
        bars = ax.barh(
            y,
            values,
            color=PERFIL_BAR_COLOR,
            edgecolor="white",
            linewidth=1.1,
            height=0.62,
        )
        ax.set_yticks(y)
        ax.set_yticklabels(labels, fontsize=8.5)
        ax.invert_yaxis()
        ax.set_title(title, fontsize=11, fontweight="600", pad=8)
        ax.set_xlabel("Participantes (n)", fontsize=9)
        ax.set_xticks(range(0, max(values) + 1))
        ax.spines["top"].set_visible(False)
        ax.spines["right"].set_visible(False)
        ax.spines["left"].set_color(GRID_COLOR)
        ax.spines["bottom"].set_color(GRID_COLOR)
        ax.xaxis.grid(True, linestyle="-", alpha=0.35)
        ax.set_axisbelow(True)

        for bar, val in zip(bars, values):
            pct = 100.0 * val / n_total
            ax.text(
                bar.get_width() + 0.06,
                bar.get_y() + bar.get_height() / 2,
                f"{val} ({pct:.0f}%)",
                va="center",
                ha="left",
                fontsize=8.5,
                fontweight="600",
                color=TEXT_COLOR,
            )
        xmax = max(values) + 2.5
        ax.set_xlim(0, xmax)

    fig.suptitle(
        f"Perfil de la muestra — Formulario 0 (n={n_total})",
        fontsize=13,
        fontweight="600",
        color=TEXT_COLOR,
        y=1.02,
    )
    fig.tight_layout()
    out_path = figures_dir / "perfil_muestra.png"
    out_path.parent.mkdir(parents=True, exist_ok=True)
    fig.savefig(
        out_path,
        dpi=DPI,
        bbox_inches="tight",
        facecolor=FIGURE_FACE,
        edgecolor="none",
        pad_inches=0.1,
    )
    plt.close(fig)
    return True


CHAT_B_VS_C_CHARTS = [
    (
        "HelpScore",
        "HelpScore_B",
        "HelpScore_C",
        "Agente — HelpScore (B vs C)",
        "HelpScore medio",
        "chat_helpscore_b_vs_c.png",
        (0, 100),
    ),
    (
        "Engagement",
        "Engagement_B",
        "Engagement_C",
        "Agente — Task engagement (B vs C)",
        "Engagement medio",
        "chat_engagement_b_vs_c.png",
        None,
    ),
    (
        "ChatExchanges",
        "Exchanges_B",
        "Exchanges_C",
        "Agente — Intercambios de chat (B vs C)",
        "Intercambios medios",
        "chat_exchanges_b_vs_c.png",
        None,
    ),
    (
        "ModelLeaks",
        "Leaks_B",
        "Leaks_C",
        "Agente — Posibles leaks del modelo (B vs C)",
        "Leaks medios",
        "chat_leaks_b_vs_c.png",
        None,
    ),
    (
        "OnTopicSeconds",
        "OnTopicSeconds_B",
        "OnTopicSeconds_C",
        "Agente — Segundos on-topic (B vs C)",
        "Segundos on-topic",
        "chat_on_topic_b_vs_c.png",
        None,
    ),
    (
        "OffTopicSeconds",
        "OffTopicSeconds_B",
        "OffTopicSeconds_C",
        "Agente — Segundos off-topic (B vs C)",
        "Segundos off-topic",
        "chat_off_topic_b_vs_c.png",
        None,
    ),
    (
        "SubstantiveQuestions",
        "SubstantiveQuestions_B",
        "SubstantiveQuestions_C",
        "Agente — Preguntas sustantivas (B vs C)",
        "Preguntas sustantivas",
        "chat_substantive_b_vs_c.png",
        None,
    ),
]


def _chat_b_vs_c_charts(
    path: Path,
    participant_path: Path,
    figures_dir: Path,
    *,
    metric_map: dict[str, tuple[str, str, str, str, str, tuple[float, float] | None]] | None = None,
) -> list[str]:
    if not path.is_file():
        return []

    rows_by_metric: dict[str, tuple[float, float]] = {}
    with path.open(encoding="utf-8-sig", newline="") as handle:
        reader = csv.DictReader(handle)
        for row in reader:
            metric = (row.get("Metric") or "").strip()
            mb = (row.get("Mean_B") or "").strip()
            mc = (row.get("Mean_C") or "").strip()
            if not metric or not mb or not mc:
                continue
            try:
                rows_by_metric[metric] = (float(mb), float(mc))
            except ValueError:
                continue

    created: list[str] = []
    chart_specs = CHAT_B_VS_C_CHARTS
    if metric_map:
        chart_specs = [
            (
                metric_key,
                cols[0],
                cols[1],
                cols[2],
                cols[3],
                cols[4],
                cols[5],
            )
            for metric_key, cols in metric_map.items()
        ]

    for metric_key, col_b, col_c, title, ylabel, filename, ylim in chart_specs:
        values = rows_by_metric.get(metric_key)
        if values is None:
            continue
        series = _participant_series(participant_path, {"B": col_b, "C": col_c})
        yerr = [_sem(series["B"]), _sem(series["C"])]
        if _bar_chart(
            [CONDITION_LABELS["B"], CONDITION_LABELS["C"]],
            list(values),
            title,
            ylabel,
            figures_dir / filename,
            colors=BC_COLORS,
            yerr=yerr,
            ylim=ylim,
        ):
            created.append(filename)
    return created


def run(output_dir: Path, emit=print, *, complete_only: bool = True) -> int:
    figures_dir = output_dir / "figures"
    figures_dir.mkdir(parents=True, exist_ok=True)
    created: list[str] = []

    specs = [
        (
            "rq1_precision",
            "rq1_precision_by_participant.csv",
            "MeanAccuracyPct",
            {
                "A": "AccuracyPct_A",
                "B": "AccuracyPct_B",
                "C": "AccuracyPct_C",
            },
            "RQ1 — Precisión por condición",
            "% aciertos",
            figures_dir / "rq1_precision.png",
            (0, 100),
        ),
        (
            "rq2_confidence",
            "rq2_confidence_by_participant.csv",
            "MeanConfidence",
            {
                "A": "MeanConfidence_A",
                "B": "MeanConfidence_B",
                "C": "MeanConfidence_C",
            },
            "RQ2 — Confianza media (Unity)",
            "Confianza (1–7)",
            figures_dir / "rq2_confidence.png",
            (1, 7),
        ),
        (
            "rq3_calibration_gap",
            "rq3_calibration_gap_by_participant.csv",
            "MeanCalibrationGap",
            {
                "A": "CalibrationGap_A",
                "B": "CalibrationGap_B",
                "C": "CalibrationGap_C",
            },
            "RQ3 — Brecha de calibración",
            "Brecha (conf. norm. − precisión)",
            figures_dir / "rq3_calibration_gap.png",
            None,
        ),
    ]

    try:
        import matplotlib  # noqa: F401
    except ImportError:
        emit("\nAVISO: matplotlib no instalado — sin graficos. pip install matplotlib")
        return 0

    for prefix, participant_file, col, participant_cols, title, ylabel, out_path, ylim in specs:
        csv_path = _group_means_path(output_dir, prefix, complete_only)
        participant_path = output_dir / participant_file
        keys, labels, values = _read_group_means(csv_path, col)
        if not values:
            continue
        p_rows = _read_participant_rows(participant_path, complete_only)
        series = _participant_series_from_rows(p_rows, participant_cols)
        colors = [CONDITION_COLORS.get(key, "#475569") for key in keys]
        yerr = [_sem(series.get(key, [])) for key in keys]
        if _bar_chart(labels, values, title, ylabel, out_path, colors=colors, yerr=yerr, ylim=ylim):
            created.append(out_path.name)

    item_path = output_dir / "rq3_calibration_by_item.csv"
    if _calibration_gap_by_item_chart(item_path, figures_dir):
        created.append("rq3_calibration_gap_by_item.png")
    if _calibration_curve_by_item_chart(item_path, figures_dir):
        created.append("rq3_calibration_curve_by_item.png")

    chat_path = output_dir / "chat_quality_b_vs_c.csv"
    participant_chat = output_dir / "chat_quality_by_participant.csv"
    created.extend(_chat_b_vs_c_charts(chat_path, participant_chat, figures_dir))

    semantic_path = output_dir / "chat_semantic_b_vs_c.csv"
    participant_semantic = output_dir / "chat_semantic_by_participant.csv"
    created.extend(
        _chat_b_vs_c_charts(
            semantic_path,
            participant_semantic,
            figures_dir,
            metric_map={
                "OnTaskRatio": ("OnTaskRatio_B", "OnTaskRatio_C", "Agente — On-task ratio (B vs C)", "On-task ratio", "chat_semantic_on_task_b_vs_c.png", (0, 1)),
                "AgentUtilityScore": ("AgentUtilityScore_B", "AgentUtilityScore_C", "Agente — Utilidad semantica (B vs C)", "Utilidad del agente", "chat_semantic_utility_b_vs_c.png", (0, 100)),
                "TimeWastedSec": ("TimeWastedSec_B", "TimeWastedSec_C", "Agente — Tiempo perdido estimado (B vs C)", "Segundos perdidos", "chat_semantic_wasted_b_vs_c.png", None),
            },
        )
    )

    if _forms_tlx_chart(output_dir / "rq2_forms_raw_tlx_group_means.csv", figures_dir):
        created.append("forms_raw_tlx_by_condition.png")
    if _mecue_b_vs_c_chart(output_dir / "rq2_forms_mecue_group_means.csv", figures_dir):
        created.append("forms_mecue_b_vs_c.png")
    if _time_by_condition_chart(output_dir / "items_time_by_condition.csv", figures_dir):
        created.append("items_time_by_condition.png")
    if _perfil_muestra_chart(output_dir / "perfil_participantes.csv", figures_dir):
        created.append("perfil_muestra.png")

    if created:
        emit(f"\nGraficos generados en {figures_dir.resolve()}:")
        for name in created:
            emit(f"  - figures/{name}")
    else:
        emit("\nNo se generaron graficos (faltan CSV de salida o matplotlib).")

    return 0


def main() -> int:
    import argparse

    parser = argparse.ArgumentParser(description="Genera graficos PNG desde _analysis/")
    parser.add_argument("--output-dir", type=Path, default=Path("_analysis"))
    parser.add_argument(
        "--include-incomplete",
        action="store_true",
        help="Usar medias que incluyen participantes incompletos",
    )
    args = parser.parse_args()
    return run(args.output_dir, complete_only=not args.include_incomplete)


if __name__ == "__main__":
    raise SystemExit(main())
