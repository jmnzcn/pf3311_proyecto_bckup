#!/usr/bin/env python3
"""Utilidades compartidas para análisis RQ1–RQ3 desde ExperimentData.csv."""

from __future__ import annotations

import csv
from collections import defaultdict
from dataclasses import dataclass
from pathlib import Path

PROVISIONAL_MARKERS = ("[PENDIENTE]", "[SIN DATOS]")
CONDITION_BY_SCENARIO = {1: "A", 2: "B", 3: "C"}
SCENARIO_BY_CONDITION = {"A": 1, "B": 2, "C": 3}
CONDITION_ORDER = ("A", "B", "C")
ITEMS_PER_BLOCK = 6


@dataclass(frozen=True)
class ItemRow:
    participant_code: str
    session_id: str
    scenario_number: int
    condition: str
    question_number: int
    answer_letter: str
    correct_letter: str
    confidence: int
    is_correct: bool
    confidence_normalized: float
    source_file: str


def discover_experiment_data_files(csv_dir: Path) -> list[Path]:
    matches = set(csv_dir.glob("**/ExperimentData.csv"))
    matches.update(csv_dir.glob("ExperimentData_*.csv"))
    return sorted(matches)


def display_path(csv_dir: Path, path: Path) -> str:
    try:
        return str(path.relative_to(csv_dir))
    except ValueError:
        return path.name


def _contains_provisional_marker(value: str) -> bool:
    text = (value or "").strip()
    return any(marker in text for marker in PROVISIONAL_MARKERS)


def normalize_letter(value: str) -> str:
    return (value or "").strip().upper()


def user_answer_letter_from_row(row: dict) -> str:
    return normalize_letter(row.get("User_answer_Letter") or row.get("AnswerLetter", ""))


def user_answer_text_from_row(row: dict) -> str:
    return (row.get("User_answer") or row.get("Answer") or "").strip()


def parse_int(value: str) -> int | None:
    text = (value or "").strip()
    if not text or not text.isdigit():
        return None
    return int(text)


def parse_confidence(value: str) -> int | None:
    number = parse_int(value)
    if number is None or number < 1 or number > 7:
        return None
    return number


def normalized_confidence(confidence: int) -> float:
    """Escala 1–7 → [0, 1] según Entregable 2: (C − 1) / 6."""
    return (confidence - 1) / 6.0


def is_real_answer_row(row: dict[str, str]) -> bool:
    for value in row.values():
        if _contains_provisional_marker(value):
            return False

    scenario = parse_int(row.get("ScenarioNumber", ""))
    question = parse_int(row.get("QuestionNumber", ""))
    if scenario not in CONDITION_BY_SCENARIO or question is None or question <= 0:
        return False

    answer = user_answer_letter_from_row(row)
    correct = normalize_letter(row.get("CorrectAnswerLetter", ""))
    if not answer or not correct or answer in {"—", "-"} or correct in {"—", "-"}:
        return False

    if parse_confidence(row.get("Confidence", "")) is None:
        return False

    participant = (row.get("ParticipantCode") or "").strip()
    session = (row.get("SessionID") or "").strip()
    if not participant or not session or participant == "Unknown":
        return False

    return True


def load_item_rows_from_files(csv_dir: Path, experiment_files: list[Path]) -> list[ItemRow]:
    rows: list[ItemRow] = []
    for path in experiment_files:
        if not path.is_file():
            continue
        label = display_path(csv_dir, path)
        with path.open(encoding="utf-8-sig", newline="") as handle:
            reader = csv.DictReader(handle)
            for raw in reader:
                if not is_real_answer_row(raw):
                    continue

                scenario = int(raw["ScenarioNumber"])
                confidence = int(raw["Confidence"])
                answer = user_answer_letter_from_row(raw)
                correct = normalize_letter(raw["CorrectAnswerLetter"])

                rows.append(
                    ItemRow(
                        participant_code=raw["ParticipantCode"].strip(),
                        session_id=raw["SessionID"].strip(),
                        scenario_number=scenario,
                        condition=CONDITION_BY_SCENARIO[scenario],
                        question_number=int(raw["QuestionNumber"]),
                        answer_letter=answer,
                        correct_letter=correct,
                        confidence=confidence,
                        is_correct=answer == correct,
                        confidence_normalized=normalized_confidence(confidence),
                        source_file=label,
                    )
                )
    return rows


def load_item_rows(csv_dir: Path, experiment_files: list[Path] | None = None) -> list[ItemRow]:
    if experiment_files is not None:
        return load_item_rows_from_files(csv_dir, experiment_files)
    return load_item_rows_from_files(csv_dir, discover_experiment_data_files(csv_dir))


def participant_key(row: ItemRow) -> str:
    return f"{row.participant_code}_{row.session_id}"


def aggregate_by_participant_condition(
    rows: list[ItemRow],
) -> dict[str, dict[str, dict[str, float | int]]]:
    """
    Retorna participant_key → condition → métricas agregadas del bloque.
    Métricas: n_items, accuracy, mean_confidence, mean_confidence_normalized, calibration_gap.
    """
    buckets: dict[str, dict[str, list[ItemRow]]] = defaultdict(lambda: defaultdict(list))
    for row in rows:
        buckets[participant_key(row)][row.condition].append(row)

    result: dict[str, dict[str, dict[str, float | int]]] = {}
    for p_key, by_condition in buckets.items():
        result[p_key] = {}
        for condition, items in by_condition.items():
            n = len(items)
            accuracy = sum(1 for item in items if item.is_correct) / n
            mean_conf = sum(item.confidence for item in items) / n
            mean_conf_norm = sum(item.confidence_normalized for item in items) / n
            result[p_key][condition] = {
                "n_items": n,
                "accuracy": accuracy,
                "mean_confidence": mean_conf,
                "mean_confidence_normalized": mean_conf_norm,
                "calibration_gap": mean_conf_norm - accuracy,
            }
    return result


def mean(values: list[float]) -> float | None:
    if not values:
        return None
    return sum(values) / len(values)


def stdev(values: list[float]) -> float | None:
    if len(values) < 2:
        return None
    avg = sum(values) / len(values)
    variance = sum((v - avg) ** 2 for v in values) / (len(values) - 1)
    return variance ** 0.5


def median(values: list[float]) -> float | None:
    if not values:
        return None
    ordered = sorted(values)
    mid = len(ordered) // 2
    if len(ordered) % 2:
        return ordered[mid]
    return (ordered[mid - 1] + ordered[mid]) / 2.0


def participant_is_complete(
    by_cond: dict[str, dict[str, float | int]],
    min_items: int = ITEMS_PER_BLOCK,
) -> bool:
    return all(
        condition in by_cond and int(by_cond[condition]["n_items"]) >= min_items
        for condition in CONDITION_ORDER
    )


def filter_complete_participants(
    aggregated: dict[str, dict[str, dict[str, float | int]]],
    min_items: int = ITEMS_PER_BLOCK,
) -> dict[str, dict[str, dict[str, float | int]]]:
    return {
        key: by_cond
        for key, by_cond in aggregated.items()
        if participant_is_complete(by_cond, min_items)
    }


def build_group_mean_rows(
    aggregated: dict[str, dict[str, dict[str, float | int]]],
    metric: str,
    *,
    value_scale: float = 1.0,
    round_digits: int = 4,
) -> list[list[object]]:
    rows: list[list[object]] = []
    for condition in CONDITION_ORDER:
        values = [
            float(by_cond[condition][metric])
            for by_cond in aggregated.values()
            if condition in by_cond
        ]
        if values:
            mean_val = sum(values) / len(values) * value_scale
            rows.append([condition, round(mean_val, round_digits), len(values)])
    return rows


def export_rq_condition_outputs(
    aggregated: dict[str, dict[str, dict[str, float | int]]],
    output_dir: Path,
    *,
    prefix: str,
    metric: str,
    descriptive_scale: str,
    group_mean_column: str,
    value_scale: float = 1.0,
    round_digits: int = 4,
) -> None:
    """Exporta medias grupales y descriptivos (todos los datos y solo participantes completos)."""
    write_csv(
        output_dir / f"{prefix}_group_means.csv",
        ["Condition", group_mean_column, "NParticipants"],
        build_group_mean_rows(
            aggregated, metric, value_scale=value_scale, round_digits=round_digits
        ),
    )
    export_descriptive_stats(
        aggregated, metric, output_dir, f"{prefix}_descriptive_stats.csv", descriptive_scale
    )

    complete = filter_complete_participants(aggregated)
    write_csv(
        output_dir / f"{prefix}_group_means_complete.csv",
        ["Condition", group_mean_column, "NParticipants"],
        build_group_mean_rows(
            complete, metric, value_scale=value_scale, round_digits=round_digits
        ),
    )
    export_descriptive_stats(
        complete,
        metric,
        output_dir,
        f"{prefix}_descriptive_stats_complete.csv",
        descriptive_scale,
    )


def group_metric_values(
    aggregated: dict[str, dict[str, dict[str, float | int]]],
    metric: str,
    condition: str,
) -> list[float]:
    values: list[float] = []
    for by_cond in aggregated.values():
        if condition in by_cond:
            values.append(float(by_cond[condition][metric]))
    return values


def write_csv(path: Path, header: list[str], data_rows: list[list[object]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8-sig", newline="") as handle:
        writer = csv.writer(handle)
        writer.writerow(header)
        writer.writerows(data_rows)


def export_descriptive_stats(
    aggregated: dict,
    metric: str,
    output_dir: Path,
    file_name: str,
    scale: str,
) -> None:
    rows: list[list[object]] = []
    for condition in CONDITION_ORDER:
        values = group_metric_values(aggregated, metric, condition)
        if not values:
            continue
        avg = mean(values)
        rows.append(
            [
                condition,
                scale,
                len(values),
                round(avg, 4) if avg is not None else "",
                round(median(values), 4) if median(values) is not None else "",
                round(stdev(values), 4) if stdev(values) is not None else "",
                round(min(values), 4),
                round(max(values), 4),
            ]
        )
    write_csv(
        output_dir / file_name,
        ["Condition", "Metric", "N", "Mean", "Median", "StDev", "Min", "Max"],
        rows,
    )


def silent_emit(_text: str = "") -> None:
    """No-op para pipeline sin salida textual."""
    return None
