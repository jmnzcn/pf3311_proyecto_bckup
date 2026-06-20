#!/usr/bin/env python3
"""Evaluacion semantica offline del chat (heuristica + Gemini opcional)."""

from __future__ import annotations

import csv
import json
import os
import urllib.error
import urllib.request
from collections import defaultdict
from pathlib import Path

from rq_common import PROVISIONAL_MARKERS, mean, write_csv
from session_catalog import SessionCatalog, discover_session_file

AGENT_CONDITIONS = ("B", "C")
UTILITY_RANK = {"Wasted": 0, "Minimal": 1, "Productive": 2, "HighValue": 3}
DEFAULT_BANK = Path(__file__).resolve().parent / "data" / "question_bank.json"


def _is_provisional_row(row: dict[str, str]) -> bool:
    return any(
        marker in (value or "")
        for value in row.values()
        for marker in PROVISIONAL_MARKERS
    )


def _parse_float(value: str) -> float | None:
    text = (value or "").strip()
    if not text:
        return None
    try:
        return float(text)
    except ValueError:
        return None


def _parse_int(value: str) -> int:
    text = (value or "").strip()
    return int(text) if text.isdigit() else 0


def _load_csv_rows(path: Path | None) -> list[dict[str, str]]:
    if path is None or not path.is_file():
        return []
    with path.open(encoding="utf-8-sig", newline="") as handle:
        return [row for row in csv.DictReader(handle) if not _is_provisional_row(row)]


def load_question_bank(bank_path: Path = DEFAULT_BANK) -> dict[tuple[int, int], str]:
    if not bank_path.is_file():
        return {}
    data = json.loads(bank_path.read_text(encoding="utf-8"))
    out: dict[tuple[int, int], str] = {}
    for scenario in data:
        sn = int(scenario.get("scenarioNumber", 0))
        for question in scenario.get("questions", []):
            qn = int(question.get("questionNumber", 0))
            situation = (question.get("situation") or "").strip()
            if sn and qn and situation:
                out[(sn, qn)] = situation
    return out


def _exchange_duration_seconds(rows: list[dict[str, str]], index: int) -> float:
    start = _parse_float(rows[index].get("SecondsSinceQuestionStart", "")) or 0.0
    if index + 1 < len(rows):
        end = _parse_float(rows[index + 1].get("SecondsSinceQuestionStart", "")) or start
        return max(0.0, end - start)
    return max(0.0, 30.0 - start) if start < 30.0 else 30.0


def _heuristic_semantic(
    student_message: str,
    model_message: str,
    row: dict[str, str],
    exchange_duration: float,
) -> dict[str, object]:
    relevance = _parse_float(row.get("ScenarioRelevanceScore", "")) or 0.0
    utility = (row.get("QuestionUtilityLevel") or "Minimal").strip()
    off_topic = (row.get("StudentOffTopic") or "").strip() == "1"
    requested = (row.get("StudentRequestedAnswer") or "").strip() == "1"
    gaming = (row.get("StudentGamingAttempt") or "").strip() == "1"
    substantive = (row.get("SubstantiveQuestion") or "").strip() == "1"
    leak = (row.get("ModelPossibleLeak") or "").strip() == "1"
    help_score = _parse_float(row.get("HelpScore", "")) or 0.0

    on_task_ratio = 0.0 if (off_topic or gaming) else min(1.0, relevance / 100.0)
    if substantive and not gaming:
        on_task_ratio = max(on_task_ratio, 0.55)
    if requested and not gaming:
        on_task_ratio = max(on_task_ratio, 0.25)

    agent_utility = help_score
    if leak:
        agent_utility *= 0.35
    if off_topic and not substantive:
        agent_utility *= 0.5

    wasted = exchange_duration if (off_topic or requested or gaming) else 0.0
    if wasted <= 0 and utility == "Wasted":
        wasted = max(exchange_duration, 8.0)

    return {
        "OnTaskRatio": round(on_task_ratio, 3),
        "AgentUtilityScore": round(agent_utility, 2),
        "SubstantiveQuestions": 1 if substantive else 0,
        "TimeWastedSec": round(wasted, 1),
        "UtilityLabel": utility or "Minimal",
        "EvaluationMode": "heuristic",
        "Notes": "derivado de metricas Unity",
    }


def _call_gemini(prompt: str, api_key: str, model: str = "gemini-2.5-flash") -> dict | None:
    url = (
        f"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent"
        f"?key={api_key}"
    )
    body = json.dumps(
        {
            "contents": [{"parts": [{"text": prompt}]}],
            "generationConfig": {
                "temperature": 0.1,
                "responseMimeType": "application/json",
            },
        }
    ).encode("utf-8")
    request = urllib.request.Request(
        url,
        data=body,
        headers={"Content-Type": "application/json"},
        method="POST",
    )
    try:
        with urllib.request.urlopen(request, timeout=45) as response:
            payload = json.loads(response.read().decode("utf-8"))
    except (urllib.error.URLError, TimeoutError, json.JSONDecodeError):
        return None

    try:
        text = payload["candidates"][0]["content"]["parts"][0]["text"]
        return json.loads(text)
    except (KeyError, IndexError, json.JSONDecodeError):
        return None


def _gemini_semantic(
    situation: str,
    student_message: str,
    model_message: str,
    api_key: str,
) -> dict[str, object] | None:
    prompt = f"""Evalua este intercambio de chat en un experimento de razonamiento con reglas.
Responde SOLO JSON con claves:
OnTaskRatio (0-1), AgentUtilityScore (0-100), SubstantiveQuestions (0 o 1),
TimeWastedSec (numero), UtilityLabel (Wasted|Minimal|Productive|HighValue), Notes (string corto).

Criterios:
- OnTaskRatio: que tan alineada esta la pregunta del estudiante con el enunciado.
- AgentUtilityScore: utilidad pedagogica de la respuesta del agente sin spoilear A/B/C/D.
- TimeWastedSec: tiempo estimado perdido si la pregunta fue off-topic o gaming.
- UtilityLabel: calidad global de la pregunta del estudiante.

ENUNCIADO:
{situation[:3500]}

ESTUDIANTE:
{student_message[:1200]}

AGENTE:
{model_message[:1200]}
"""
    result = _call_gemini(prompt, api_key)
    if not result:
        return None

    utility = str(result.get("UtilityLabel", "Minimal")).strip()
    if utility not in UTILITY_RANK:
        utility = "Minimal"

    return {
        "OnTaskRatio": round(float(result.get("OnTaskRatio", 0)), 3),
        "AgentUtilityScore": round(float(result.get("AgentUtilityScore", 0)), 2),
        "SubstantiveQuestions": 1 if int(result.get("SubstantiveQuestions", 0)) else 0,
        "TimeWastedSec": round(float(result.get("TimeWastedSec", 0)), 1),
        "UtilityLabel": utility,
        "EvaluationMode": "gemini",
        "Notes": str(result.get("Notes", ""))[:240],
    }


def _dominant_utility(labels: list[str]) -> str:
    best = "None"
    best_rank = -1
    for label in labels:
        rank = UTILITY_RANK.get(label.strip(), -1)
        if rank > best_rank:
            best_rank = rank
            best = label.strip()
    return best if best_rank >= 0 else "None"


def run(
    catalog: SessionCatalog,
    output_dir: Path | None = None,
    *,
    bank_path: Path = DEFAULT_BANK,
    use_gemini: bool | None = None,
) -> int:
    bank = load_question_bank(bank_path)
    api_key = os.environ.get("GEMINI_API_KEY", "").strip()
    gemini_enabled = use_gemini if use_gemini is not None else bool(api_key)

    question_rows: list[list[object]] = []
    participant_agg: dict[str, dict[str, dict[str, float | int | str]]] = defaultdict(dict)

    for session in catalog.canonical:
        folder = session.folder
        pcode = session.participant_code
        rating_path = discover_session_file(folder, "ChatHelpRating.csv")
        rating_rows = _load_csv_rows(rating_path)
        grouped_ratings: dict[tuple[str, int, int], list[dict[str, str]]] = defaultdict(list)
        for row in rating_rows:
            cond = (row.get("ConditionCode") or "").strip().upper()
            if cond not in AGENT_CONDITIONS:
                continue
            key = (
                cond,
                _parse_int(row.get("ScenarioNumber", "")),
                _parse_int(row.get("QuestionNumber", "")),
            )
            grouped_ratings[key].append(row)

        for key, rows in grouped_ratings.items():
            cond, scenario_number, question_number = key
            rows.sort(key=lambda item: _parse_int(item.get("ExchangeIndex", "")))

            for index, row in enumerate(rows):
                student = (row.get("StudentMessage") or "").strip()
                model = (row.get("ModelMessage") or "").strip()
                if not student:
                    continue

                exchange_duration = _exchange_duration_seconds(rows, index)
                situation = bank.get((scenario_number, question_number), "")
                evaluation = None
                if gemini_enabled and api_key and situation:
                    evaluation = _gemini_semantic(situation, student, model, api_key)
                if evaluation is None:
                    evaluation = _heuristic_semantic(student, model, row, exchange_duration)

                question_rows.append(
                    [
                        pcode,
                        session.folder_name,
                        cond,
                        scenario_number,
                        question_number,
                        _parse_int(row.get("ExchangeIndex", "")),
                        round(float(evaluation["OnTaskRatio"]), 3),
                        evaluation["AgentUtilityScore"],
                        evaluation["SubstantiveQuestions"],
                        evaluation["TimeWastedSec"],
                        evaluation["UtilityLabel"],
                        evaluation["EvaluationMode"],
                        evaluation["Notes"],
                    ]
                )

                bucket = participant_agg[pcode].setdefault(
                    cond,
                    {
                        "OnTaskRatio": [],
                        "AgentUtilityScore": [],
                        "TimeWastedSec": 0.0,
                        "SubstantiveQuestions": 0,
                        "UtilityLabels": [],
                    },
                )
                bucket["OnTaskRatio"].append(float(evaluation["OnTaskRatio"]))
                bucket["AgentUtilityScore"].append(float(evaluation["AgentUtilityScore"]))
                bucket["TimeWastedSec"] = float(bucket["TimeWastedSec"]) + float(evaluation["TimeWastedSec"])
                bucket["SubstantiveQuestions"] = int(bucket["SubstantiveQuestions"]) + int(
                    evaluation["SubstantiveQuestions"]
                )
                bucket["UtilityLabels"].append(str(evaluation["UtilityLabel"]))

    if not question_rows or output_dir is None:
        return 0

    participant_rows: list[list[object]] = []
    for pcode in sorted(participant_agg):
        row: list[object] = [pcode]
        for cond in AGENT_CONDITIONS:
            metrics = participant_agg[pcode].get(cond)
            if not metrics:
                row.extend(["", "", "", "", ""])
                continue
            row.extend(
                [
                    round(mean(metrics["OnTaskRatio"]) or 0.0, 3),
                    round(mean(metrics["AgentUtilityScore"]) or 0.0, 2),
                    round(float(metrics["TimeWastedSec"]), 1),
                    int(metrics["SubstantiveQuestions"]),
                    _dominant_utility(list(metrics["UtilityLabels"])),
                ]
            )
        participant_rows.append(row)

    group_rows: list[list[object]] = []
    for metric_key, label in (
        ("OnTaskRatio", "OnTaskRatio"),
        ("AgentUtilityScore", "AgentUtilityScore"),
        ("TimeWastedSec", "TimeWastedSec"),
        ("SubstantiveQuestions", "SubstantiveQuestions"),
    ):
        b_vals: list[float] = []
        c_vals: list[float] = []
        for pdata in participant_agg.values():
            if "B" in pdata:
                if metric_key in ("TimeWastedSec", "SubstantiveQuestions"):
                    b_vals.append(float(pdata["B"][metric_key]))
                else:
                    b_vals.append(mean(pdata["B"][metric_key]) or 0.0)
            if "C" in pdata:
                if metric_key in ("TimeWastedSec", "SubstantiveQuestions"):
                    c_vals.append(float(pdata["C"][metric_key]))
                else:
                    c_vals.append(mean(pdata["C"][metric_key]) or 0.0)
        group_rows.append(
            [
                label,
                round(mean(b_vals) or 0.0, 3) if b_vals else "",
                len(b_vals),
                round(mean(c_vals) or 0.0, 3) if c_vals else "",
                len(c_vals),
            ]
        )

    write_csv(
        output_dir / "chat_semantic_by_question.csv",
        [
            "ParticipantCode",
            "SessionFolder",
            "Condition",
            "ScenarioNumber",
            "QuestionNumber",
            "ExchangeIndex",
            "OnTaskRatio",
            "AgentUtilityScore",
            "SubstantiveQuestions",
            "TimeWastedSec",
            "UtilityLabel",
            "EvaluationMode",
            "Notes",
        ],
        question_rows,
    )
    write_csv(
        output_dir / "chat_semantic_by_participant.csv",
        [
            "ParticipantCode",
            "OnTaskRatio_B",
            "AgentUtilityScore_B",
            "TimeWastedSec_B",
            "SubstantiveQuestions_B",
            "UtilityLabel_B",
            "OnTaskRatio_C",
            "AgentUtilityScore_C",
            "TimeWastedSec_C",
            "SubstantiveQuestions_C",
            "UtilityLabel_C",
        ],
        participant_rows,
    )
    write_csv(
        output_dir / "chat_semantic_b_vs_c.csv",
        ["Metric", "Mean_B", "N_B", "Mean_C", "N_C"],
        group_rows,
    )
    return 0


def main() -> int:
    import argparse
    from session_catalog import build_catalog

    parser = argparse.ArgumentParser(description="Evaluacion semantica del chat")
    parser.add_argument("--csv-dir", type=Path, default=Path("CSV data"))
    parser.add_argument("--output-dir", type=Path, default=Path("_analysis"))
    parser.add_argument("--bank", type=Path, default=DEFAULT_BANK)
    parser.add_argument("--no-gemini", action="store_true")
    args = parser.parse_args()
    catalog = build_catalog(args.csv_dir)
    return run(
        catalog,
        args.output_dir,
        bank_path=args.bank,
        use_gemini=False if args.no_gemini else None,
    )


if __name__ == "__main__":
    raise SystemExit(main())
