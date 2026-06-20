#!/usr/bin/env python3
"""Calidad del agente (B/C) — exporta tablas CSV."""

from __future__ import annotations

import csv
from collections import defaultdict
from pathlib import Path

from rq_common import PROVISIONAL_MARKERS, mean, write_csv
from session_catalog import SessionCatalog, discover_session_file

AGENT_CONDITIONS = ("B", "C")

CHAT_API_FAILURE_EVENT_TYPES = frozenset(
    {
        "empty_response",
        "rate_limit",
        "network_error",
        "service_unavailable",
        "http_error",
        "exhausted_retries",
        "failure",
        "error",
        "timeout",
    }
)


def _is_provisional_row(row: dict[str, str]) -> bool:
    return any(
        marker in (value or "")
        for value in row.values()
        for marker in PROVISIONAL_MARKERS
    )


def _parse_int(value: str) -> int:
    text = (value or "").strip()
    return int(text) if text.isdigit() else 0


def _parse_float(value: str) -> float | None:
    text = (value or "").strip()
    if not text:
        return None
    try:
        return float(text)
    except ValueError:
        return None


def _load_csv_rows(path: Path) -> list[dict[str, str]]:
    if not path.is_file():
        return []
    with path.open(encoding="utf-8-sig", newline="") as handle:
        return [row for row in csv.DictReader(handle) if not _is_provisional_row(row)]


def _is_chat_api_failure_row(row: dict[str, str]) -> bool:
    event = (row.get("EventType") or "").strip().lower()
    if event in CHAT_API_FAILURE_EVENT_TYPES:
        return True
    http = (row.get("HttpStatusCode") or "").strip()
    return http.isdigit() and int(http) >= 400


def run(catalog: SessionCatalog, output_dir: Path | None = None) -> int:
    scenario_detail: list[list[object]] = []
    question_detail: list[list[object]] = []
    api_detail: list[list[object]] = []
    by_participant_condition: dict[str, dict[str, dict[str, float | int]]] = defaultdict(dict)

    for session in catalog.canonical:
        folder = session.folder
        pcode = session.participant_code
        scenario_path = discover_session_file(folder, "ChatScenarioSummary.csv")
        question_path = discover_session_file(folder, "ChatQuestionSummary.csv")
        api_path = discover_session_file(folder, "ChatApiEvent.csv")

        for row in _load_csv_rows(scenario_path) if scenario_path else []:
            cond = (row.get("ConditionCode") or "").strip().upper()
            if cond not in AGENT_CONDITIONS:
                continue
            help_score = _parse_float(row.get("AvgHelpScore", ""))
            engagement = _parse_float(row.get("AvgTaskEngagementScore", ""))
            tts_rate = _parse_float(row.get("TtsSuccessRate", ""))
            scenario_detail.append(
                [
                    pcode,
                    session.folder_name,
                    cond,
                    row.get("ScenarioName", ""),
                    _parse_int(row.get("TotalExchanges", "")),
                    _parse_int(row.get("TotalTurns", "")),
                    round(help_score, 2) if help_score is not None else "",
                    round(engagement, 2) if engagement is not None else "",
                    _parse_int(row.get("AnswerRequestCount", "")),
                    _parse_int(row.get("OffTopicCount", "")),
                    _parse_int(row.get("ModelLeakCount", "")),
                    round(_parse_float(row.get("OnTopicSeconds", "")) or 0.0, 2),
                    round(_parse_float(row.get("OffTopicSeconds", "")) or 0.0, 2),
                    _parse_int(row.get("SubstantiveQuestionCount", "")),
                    row.get("ChatUsedInBlock", ""),
                    row.get("ChatMeetsProtocol", ""),
                    row.get("DominantUtilityLevel", ""),
                    _parse_int(row.get("TtsAttempts", "")),
                    round(tts_rate, 2) if tts_rate is not None else "",
                    row.get("EffectiveHelpLevel", ""),
                ]
            )
            by_participant_condition[pcode][cond] = {
                "AvgHelpScore": help_score if help_score is not None else 0.0,
                "AvgTaskEngagementScore": engagement if engagement is not None else 0.0,
                "TotalExchanges": _parse_int(row.get("TotalExchanges", "")),
                "ModelLeakCount": _parse_int(row.get("ModelLeakCount", "")),
                "TtsSuccessRate": tts_rate if tts_rate is not None else 0.0,
                "OnTopicSeconds": _parse_float(row.get("OnTopicSeconds", "")) or 0.0,
                "OffTopicSeconds": _parse_float(row.get("OffTopicSeconds", "")) or 0.0,
                "SubstantiveQuestionCount": _parse_int(row.get("SubstantiveQuestionCount", "")),
                "ChatUsedInBlock": row.get("ChatUsedInBlock", ""),
                "ChatMeetsProtocol": row.get("ChatMeetsProtocol", ""),
                "DominantUtilityLevel": row.get("DominantUtilityLevel", ""),
            }

        for row in _load_csv_rows(question_path) if question_path else []:
            cond = (row.get("ConditionCode") or "").strip().upper()
            if cond not in AGENT_CONDITIONS:
                continue
            question_detail.append(
                [
                    pcode,
                    cond,
                    _parse_int(row.get("QuestionNumber", "")),
                    _parse_int(row.get("TotalExchanges", "")),
                    _parse_float(row.get("AvgHelpScore", "")) or "",
                    _parse_int(row.get("ModelLeakCount", "")),
                    round(_parse_float(row.get("OnTopicSeconds", "")) or 0.0, 2),
                    round(_parse_float(row.get("OffTopicSeconds", "")) or 0.0, 2),
                    _parse_int(row.get("SubstantiveQuestionCount", "")),
                    row.get("ChatUsedInQuestion", ""),
                    row.get("DominantUtilityLevel", ""),
                    row.get("EffectiveHelpLevel", ""),
                ]
            )

        for row in _load_csv_rows(api_path) if api_path else []:
            api_detail.append(
                [
                    pcode,
                    session.folder_name,
                    (row.get("ConditionCode") or "").strip().upper(),
                    (row.get("EventType") or "").strip(),
                    (row.get("HttpStatusCode") or "").strip(),
                    row.get("FailureReason", ""),
                    _parse_int(row.get("QuestionNumber", "")),
                    "1" if _is_chat_api_failure_row(row) else "0",
                ]
            )

    if not scenario_detail or not output_dir:
        return 0

    participant_rows: list[list[object]] = []
    for pcode in sorted(by_participant_condition):
        b = by_participant_condition[pcode].get("B", {})
        c = by_participant_condition[pcode].get("C", {})
        participant_rows.append(
            [
                pcode,
                b.get("AvgHelpScore", ""),
                c.get("AvgHelpScore", ""),
                b.get("ModelLeakCount", ""),
                c.get("ModelLeakCount", ""),
                b.get("TotalExchanges", ""),
                c.get("TotalExchanges", ""),
                c.get("TtsSuccessRate", ""),
                b.get("OnTopicSeconds", ""),
                c.get("OnTopicSeconds", ""),
                b.get("OffTopicSeconds", ""),
                c.get("OffTopicSeconds", ""),
                b.get("SubstantiveQuestionCount", ""),
                c.get("SubstantiveQuestionCount", ""),
                b.get("ChatUsedInBlock", ""),
                c.get("ChatUsedInBlock", ""),
                b.get("ChatMeetsProtocol", ""),
                c.get("ChatMeetsProtocol", ""),
                b.get("DominantUtilityLevel", ""),
                c.get("DominantUtilityLevel", ""),
            ]
        )

    group_rows: list[list[object]] = []
    for metric, label in (
        ("AvgHelpScore", "HelpScore"),
        ("AvgTaskEngagementScore", "Engagement"),
        ("TotalExchanges", "ChatExchanges"),
        ("ModelLeakCount", "ModelLeaks"),
        ("OnTopicSeconds", "OnTopicSeconds"),
        ("OffTopicSeconds", "OffTopicSeconds"),
        ("SubstantiveQuestionCount", "SubstantiveQuestions"),
    ):
        b_vals = [
            float(d["B"][metric])
            for d in by_participant_condition.values()
            if "B" in d and metric in d["B"]
        ]
        c_vals = [
            float(d["C"][metric])
            for d in by_participant_condition.values()
            if "C" in d and metric in d["C"]
        ]
        mb = mean(b_vals)
        mc = mean(c_vals)
        group_rows.append(
            [
                label,
                round(mb, 3) if mb is not None else "",
                len(b_vals),
                round(mc, 3) if mc is not None else "",
                len(c_vals),
            ]
        )

    write_csv(
        output_dir / "chat_scenario_by_participant.csv",
        [
            "ParticipantCode",
            "SessionFolder",
            "Condition",
            "ScenarioName",
            "TotalExchanges",
            "TotalTurns",
            "AvgHelpScore",
            "AvgTaskEngagement",
            "AnswerRequests",
            "OffTopic",
            "ModelLeaks",
            "OnTopicSeconds",
            "OffTopicSeconds",
            "SubstantiveQuestions",
            "ChatUsedInBlock",
            "ChatMeetsProtocol",
            "DominantUtilityLevel",
            "TtsAttempts",
            "TtsSuccessRatePct",
            "EffectiveHelpLevel",
        ],
        scenario_detail,
    )
    write_csv(
        output_dir / "chat_quality_b_vs_c.csv",
        ["Metric", "Mean_B", "N_B", "Mean_C", "N_C"],
        group_rows,
    )
    write_csv(
        output_dir / "chat_quality_by_participant.csv",
        [
            "ParticipantCode",
            "HelpScore_B",
            "HelpScore_C",
            "Leaks_B",
            "Leaks_C",
            "Exchanges_B",
            "Exchanges_C",
            "TtsSuccessRate_C",
            "OnTopicSeconds_B",
            "OnTopicSeconds_C",
            "OffTopicSeconds_B",
            "OffTopicSeconds_C",
            "SubstantiveQuestions_B",
            "SubstantiveQuestions_C",
            "ChatUsed_B",
            "ChatUsed_C",
            "ChatMeetsProtocol_B",
            "ChatMeetsProtocol_C",
            "UtilityLevel_B",
            "UtilityLevel_C",
        ],
        participant_rows,
    )
    if question_detail:
        write_csv(
            output_dir / "chat_question_detail.csv",
            [
                "ParticipantCode",
                "Condition",
                "QuestionNumber",
                "TotalExchanges",
                "AvgHelpScore",
                "ModelLeakCount",
                "OnTopicSeconds",
                "OffTopicSeconds",
                "SubstantiveQuestionCount",
                "ChatUsedInQuestion",
                "DominantUtilityLevel",
                "EffectiveHelpLevel",
            ],
            question_detail,
        )
    if api_detail:
        write_csv(
            output_dir / "chat_api_events.csv",
            [
                "ParticipantCode",
                "SessionFolder",
                "Condition",
                "EventType",
                "HttpStatusCode",
                "FailureReason",
                "QuestionNumber",
                "IsFailure",
            ],
            api_detail,
        )

    return 0
