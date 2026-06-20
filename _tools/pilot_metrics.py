#!/usr/bin/env python3
"""Metricas de viabilidad del piloto — exporta tablas CSV."""

from __future__ import annotations

import csv
from pathlib import Path

from rq_common import write_csv
from session_catalog import SessionCatalog, discover_all_session_files, discover_session_file


def _parse_float(value: str) -> float | None:
    text = (value or "").strip()
    if not text:
        return None
    try:
        return float(text)
    except ValueError:
        return None


def analyze_gemini_latency(
    catalog: SessionCatalog,
    threshold: float = 5.0,
) -> tuple[list[dict], dict]:
    files = discover_all_session_files(catalog.csv_dir, "ChatHelpRating.csv", catalog.canonical)
    rows_out: list[dict] = []
    all_latencies: list[float] = []

    for path in files:
        latencies: list[float] = []
        leaks = 0
        session_label = path.parent.name
        with path.open(encoding="utf-8-sig", newline="") as handle:
            reader = csv.DictReader(handle)
            for row in reader:
                if any("[PENDIENTE]" in (v or "") or "[SIN DATOS]" in (v or "") for v in row.values()):
                    continue
                latency = _parse_float(row.get("GeminiLatencySeconds", ""))
                if latency is not None:
                    latencies.append(latency)
                if (row.get("ModelPossibleLeak") or "").strip() == "1":
                    leaks += 1

        total = len(latencies)
        within = sum(1 for v in latencies if v <= threshold)
        pct = 100.0 * within / total if total else 0.0
        passes = (within / total >= 0.9) if total else False
        mean = sum(latencies) / total if total else None

        if total:
            all_latencies.extend(latencies)

        rows_out.append(
            {
                "SessionFolder": session_label,
                "Turns": total,
                "WithinThreshold": within,
                "PctWithinThreshold": round(pct, 2) if total else "",
                "MeanSeconds": round(mean, 3) if mean is not None else "",
                "ModelLeakCount": leaks,
                "Passes90PctRule": "1" if passes else "0",
            }
        )

    combined: dict = {"total": 0, "within": 0, "pct": 0.0, "passes": False, "leaks": 0}
    if all_latencies:
        combined["total"] = len(all_latencies)
        combined["within"] = sum(1 for v in all_latencies if v <= threshold)
        combined["pct"] = 100.0 * combined["within"] / combined["total"]
        combined["passes"] = combined["within"] / combined["total"] >= 0.9

    combined["leaks"] = sum(int(r["ModelLeakCount"]) for r in rows_out)
    n_participants = len(catalog.canonical) or 1
    leak_rate = combined["leaks"] / n_participants
    combined["leak_per_10"] = leak_rate * 10.0
    combined["leak_passes"] = combined["leak_per_10"] <= 2.0

    return rows_out, combined


def analyze_tts_success(catalog: SessionCatalog) -> tuple[list[dict], dict]:
    files = discover_all_session_files(catalog.csv_dir, "TtsLog.csv", catalog.canonical)
    rows_out: list[dict] = []
    total_attempts = 0
    total_successes = 0

    for path in files:
        attempts = 0
        successes = 0
        session_label = path.parent.name
        with path.open(encoding="utf-8-sig", newline="") as handle:
            reader = csv.DictReader(handle)
            for row in reader:
                if any("[PENDIENTE]" in (v or "") or "[SIN DATOS]" in (v or "") for v in row.values()):
                    continue
                attempts += 1
                if (row.get("TtsSuccess") or "").strip() == "1":
                    successes += 1

        rate = 100.0 * successes / attempts if attempts else 0.0
        passes = rate >= 85.0 if attempts else False
        if attempts:
            total_attempts += attempts
            total_successes += successes

        rows_out.append(
            {
                "SessionFolder": session_label,
                "Attempts": attempts,
                "Successes": successes,
                "SuccessRatePct": round(rate, 2) if attempts else "",
                "Passes85PctRule": "1" if passes else "0",
            }
        )

    combined: dict = {"attempts": total_attempts, "successes": total_successes, "rate": 0.0, "passes": False}
    if total_attempts:
        combined["rate"] = 100.0 * total_successes / total_attempts
        combined["passes"] = combined["rate"] >= 85.0

    return rows_out, combined


def analyze_csv_integrity(catalog: SessionCatalog) -> dict:
    total_rows = 0
    valid_rows = 0
    for session in catalog.canonical:
        if not session.experiment_file:
            continue
        total_rows += session.total_data_rows
        valid_rows += session.total_real_items

    overall_pct = 100.0 * valid_rows / total_rows if total_rows else 0.0
    passes = overall_pct >= 95.0 if total_rows else False
    return {
        "total_rows": total_rows,
        "valid_rows": valid_rows,
        "valid_pct": overall_pct,
        "passes": passes,
    }


def analyze_chat_protocol(catalog: SessionCatalog) -> dict:
    sessions_total = 0
    sessions_with_chat = 0
    sessions_meeting_protocol = 0
    wasted_dominant = 0

    for session in catalog.canonical:
        path = discover_session_file(session.folder, "ChatScenarioSummary.csv")
        if not path or not path.is_file():
            continue
        with path.open(encoding="utf-8-sig", newline="") as handle:
            reader = csv.DictReader(handle)
            for row in reader:
                if any("[PENDIENTE]" in (v or "") or "[SIN DATOS]" in (v or "") for v in row.values()):
                    continue
                cond = (row.get("ConditionCode") or "").strip().upper()
                if cond not in ("B", "C"):
                    continue
                sessions_total += 1
                if (row.get("ChatUsedInBlock") or "").strip() == "1":
                    sessions_with_chat += 1
                if (row.get("ChatMeetsProtocol") or "").strip() == "1":
                    sessions_meeting_protocol += 1
                if (row.get("DominantUtilityLevel") or "").strip().lower() == "wasted":
                    wasted_dominant += 1

    pct_chat = 100.0 * sessions_with_chat / sessions_total if sessions_total else 0.0
    pct_protocol = 100.0 * sessions_meeting_protocol / sessions_total if sessions_total else 0.0
    return {
        "agent_sessions": sessions_total,
        "sessions_with_chat": sessions_with_chat,
        "sessions_meeting_protocol": sessions_meeting_protocol,
        "pct_with_chat": pct_chat,
        "pct_meeting_protocol": pct_protocol,
        "wasted_dominant_sessions": wasted_dominant,
        "passes_chat_protocol": pct_protocol >= 80.0 if sessions_total else False,
    }


def run_pilot_metrics(catalog: SessionCatalog, output_dir: Path | None) -> None:
    latency_rows, latency_combined = analyze_gemini_latency(catalog)
    tts_rows, tts_combined = analyze_tts_success(catalog)
    integrity = analyze_csv_integrity(catalog)
    chat_protocol = analyze_chat_protocol(catalog)

    if output_dir is None:
        return

    write_csv(
        output_dir / "pilot_gemini_latency.csv",
        [
            "SessionFolder",
            "Turns",
            "WithinThreshold",
            "PctWithinThreshold",
            "MeanSeconds",
            "ModelLeakCount",
            "Passes90PctRule",
        ],
        [list(r.values()) for r in latency_rows],
    )
    write_csv(
        output_dir / "pilot_tts_success.csv",
        ["SessionFolder", "Attempts", "Successes", "SuccessRatePct", "Passes85PctRule"],
        [list(r.values()) for r in tts_rows],
    )
    write_csv(
        output_dir / "pilot_integrity_summary.csv",
        ["Metric", "Value"],
        [
            ["ValidRows", integrity["valid_rows"]],
            ["TotalRows", integrity["total_rows"]],
            ["ValidPct", round(integrity["valid_pct"], 2)],
            ["Passes95PctRule", "1" if integrity["passes"] else "0"],
            ["GeminiPasses90Pct", "1" if latency_combined.get("passes") else "0"],
            ["TtsPasses85Pct", "1" if tts_combined.get("passes") else "0"],
            ["TotalModelLeaks", latency_combined.get("leaks", 0)],
            ["LeaksPer10Participants", round(latency_combined.get("leak_per_10", 0.0), 2)],
            ["LeaksPassRule", "1" if latency_combined.get("leak_passes") else "0"],
            ["AgentSessions", chat_protocol.get("agent_sessions", 0)],
            ["SessionsWithChat", chat_protocol.get("sessions_with_chat", 0)],
            ["SessionsMeetingChatProtocol", chat_protocol.get("sessions_meeting_protocol", 0)],
            ["PctSessionsWithChat", round(chat_protocol.get("pct_with_chat", 0.0), 2)],
            ["PctSessionsMeetingChatProtocol", round(chat_protocol.get("pct_meeting_protocol", 0.0), 2)],
            ["WastedDominantSessions", chat_protocol.get("wasted_dominant_sessions", 0)],
            ["ChatProtocolPassRule", "1" if chat_protocol.get("passes_chat_protocol") else "0"],
        ],
    )
