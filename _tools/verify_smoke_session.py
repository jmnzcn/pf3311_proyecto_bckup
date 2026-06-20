#!/usr/bin/env python3
"""Checklist post sesion de humo — alinea con Entregable 2 y README."""

from __future__ import annotations

import argparse
import csv
import sys
from pathlib import Path

_TOOLS_DIR = Path(__file__).resolve().parent
if str(_TOOLS_DIR) not in sys.path:
    sys.path.insert(0, str(_TOOLS_DIR))

from pilot_metrics import analyze_chat_protocol, analyze_csv_integrity, analyze_gemini_latency, analyze_tts_success
from rq_common import PROVISIONAL_MARKERS, is_real_answer_row
from session_catalog import SessionCatalog, build_catalog, discover_session_file

EXPECTED_HEADERS = {
    "ChatHelpRating.csv": (
        "ScenarioRelevanceScore",
        "SubstantiveQuestion",
        "QuestionUtilityLevel",
    ),
    "ChatQuestionSummary.csv": (
        "OnTopicSeconds",
        "OffTopicSeconds",
        "SubstantiveQuestionCount",
        "ChatUsedInQuestion",
        "DominantUtilityLevel",
    ),
    "ChatScenarioSummary.csv": (
        "OnTopicSeconds",
        "OffTopicSeconds",
        "ChatUsedInBlock",
        "ChatMeetsProtocol",
        "DominantUtilityLevel",
    ),
}

AGENT_CONDITIONS = {"B", "C"}


def _is_provisional_row(row: dict[str, str]) -> bool:
    return any(
        marker in (value or "")
        for value in row.values()
        for marker in PROVISIONAL_MARKERS
    )


def _load_rows(path: Path | None) -> list[dict[str, str]]:
    if path is None or not path.is_file():
        return []
    with path.open(encoding="utf-8-sig", newline="") as handle:
        return [row for row in csv.DictReader(handle) if not _is_provisional_row(row)]


def _header_has_columns(path: Path | None, required: tuple[str, ...]) -> tuple[bool, list[str]]:
    if path is None or not path.is_file():
        return False, list(required)
    with path.open(encoding="utf-8-sig", newline="") as handle:
        header = next(csv.reader(handle), [])
    missing = [col for col in required if col not in header]
    return not missing, missing


def _latest_session_folder(csv_dir: Path) -> Path | None:
    candidates = [
        path
        for path in csv_dir.iterdir()
        if path.is_dir() and discover_session_file(path, "ExperimentData.csv") is not None
    ]
    if not candidates:
        return None
    return max(candidates, key=lambda path: path.stat().st_mtime)


def _find_session(catalog: SessionCatalog, folder_name: str | None) -> Path | None:
    if folder_name:
        for session in catalog.sessions:
            if session.folder_name == folder_name:
                return session.folder
        direct = catalog.csv_dir / folder_name
        return direct if direct.is_dir() else None
    return _latest_session_folder(catalog.csv_dir)


def verify_session_folder(session_folder: Path) -> tuple[list[str], list[str]]:
    ok: list[str] = []
    fail: list[str] = []

    exp_path = discover_session_file(session_folder, "ExperimentData.csv")
    if not exp_path:
        fail.append("Falta ExperimentData.csv")
        return ok, fail

    rows = _load_rows(exp_path)
    real_rows = [row for row in rows if is_real_answer_row(row)]
    ok.append(f"ExperimentData: {len(real_rows)} filas validas")

    if len(real_rows) < 6:
        fail.append("Se esperaban al menos 6 filas validas para humo minimo (1 bloque completo)")
    if len(real_rows) >= 18:
        ok.append("Sesion completa A+B+C (18 filas)")
    elif len(real_rows) >= 12:
        ok.append("Parcial: al menos 2 bloques con datos")

    scenarios = {int(row["ScenarioNumber"]) for row in real_rows if row.get("ScenarioNumber", "").isdigit()}
    if 2 in scenarios or 3 in scenarios:
        for file_name, cols in EXPECTED_HEADERS.items():
            path = discover_session_file(session_folder, file_name)
            has_cols, missing = _header_has_columns(path, cols)
            if path is None:
                fail.append(f"Falta {file_name} (bloque B/C)")
            elif not has_cols:
                fail.append(f"{file_name} sin columnas nuevas: {', '.join(missing)} (build antiguo?)")
            else:
                ok.append(f"{file_name}: columnas nuevas presentes")

        for cond, scenario in (("B", 2), ("C", 3)):
            if scenario not in scenarios:
                continue
            summary = discover_session_file(session_folder, "ChatScenarioSummary.csv")
            cond_rows = [
                row
                for row in _load_rows(summary)
                if (row.get("ConditionCode") or "").strip().upper() == cond
            ]
            if not cond_rows:
                fail.append(f"Sin ChatScenarioSummary para condicion {cond}")
                continue
            row = cond_rows[-1]
            exchanges = int((row.get("TotalExchanges") or "0").strip() or 0)
            meets = (row.get("ChatMeetsProtocol") or "").strip()
            if exchanges >= 1:
                ok.append(f"Condicion {cond}: {exchanges} intercambio(s) de chat")
            else:
                fail.append(f"Condicion {cond}: protocolo pide >=1 mensaje por bloque (TotalExchanges=0)")
            if meets == "1":
                ok.append(f"Condicion {cond}: ChatMeetsProtocol=1")
            elif exchanges >= 1:
                fail.append(f"Condicion {cond}: ChatMeetsProtocol distinto de 1")

            if cond == "C":
                tts_path = discover_session_file(session_folder, "TtsLog.csv")
                tts_rows = _load_rows(tts_path)
                if not tts_rows:
                    fail.append("Condicion C: falta TtsLog.csv o esta vacio")
                else:
                    ok.append(f"Condicion C: {len(tts_rows)} evento(s) TTS")

    for optional in ("ConsentLog.csv", "ChatLog.csv", "ChatApiEvent.csv"):
        path = discover_session_file(session_folder, optional)
        if path:
            ok.append(f"{optional} presente")

    return ok, fail


def verify_pilot_rules(catalog: SessionCatalog) -> tuple[list[str], list[str]]:
    ok: list[str] = []
    fail: list[str] = []

    integrity = analyze_csv_integrity(catalog)
    if integrity["passes"]:
        ok.append(f"Integridad CSV >=95% ({integrity['valid_pct']:.1f}%)")
    else:
        fail.append(f"Integridad CSV bajo 95% ({integrity['valid_pct']:.1f}%)")

    _, latency = analyze_gemini_latency(catalog)
    if latency.get("passes"):
        ok.append(f"Latencia Gemini <=5s en >=90% ({latency.get('pct', 0):.1f}%)")
    elif latency.get("total", 0):
        fail.append(f"Latencia Gemini bajo umbral 90% ({latency.get('pct', 0):.1f}%)")
    else:
        fail.append("Sin datos de latencia Gemini (ChatHelpRating vacio?)")

    _, tts = analyze_tts_success(catalog)
    if tts.get("attempts", 0):
        if tts.get("passes"):
            ok.append(f"TTS >=85% ({tts.get('rate', 0):.1f}%)")
        else:
            fail.append(f"TTS bajo 85% ({tts.get('rate', 0):.1f}%)")

    chat = analyze_chat_protocol(catalog)
    if chat.get("agent_sessions", 0):
        ok.append(
            f"Chat usado en {chat['sessions_with_chat']}/{chat['agent_sessions']} bloques B/C"
        )
        if chat.get("passes_chat_protocol"):
            ok.append("Protocolo chat >=80% bloques con >=1 intercambio")
        else:
            fail.append(
                f"Protocolo chat bajo 80% ({chat.get('pct_meeting_protocol', 0):.1f}%)"
            )

    leaks = latency.get("leak_per_10", 0)
    if latency.get("leaks", 0) == 0:
        ok.append("Sin leaks detectados en ChatHelpRating")
    elif latency.get("leak_passes"):
        ok.append(f"Leaks por 10 participantes OK ({leaks:.2f})")
    else:
        fail.append(f"Demasiados leaks ({leaks:.2f} por 10 participantes; regla <=2)")

    return ok, fail


def main() -> int:
    parser = argparse.ArgumentParser(description="Verifica sesion de humo / piloto (Entregable 2)")
    parser.add_argument("csv_dir", nargs="?", default="CSV data")
    parser.add_argument("--session", help="Nombre de carpeta P##_ID-...")
    parser.add_argument(
        "--pilot-rules",
        action="store_true",
        help="Evaluar reglas de viabilidad sobre sesiones canonicas",
    )
    args = parser.parse_args()

    csv_dir = Path(args.csv_dir)
    if not csv_dir.is_dir():
        print(f"ERROR: no existe {csv_dir}", file=sys.stderr)
        return 1

    catalog = build_catalog(csv_dir)
    session_folder = _find_session(catalog, args.session)
    if session_folder is None:
        print("ERROR: no hay carpetas de sesion con ExperimentData.csv", file=sys.stderr)
        return 1

    print(f"Sesion: {session_folder.name}\n")

    ok, fail = verify_session_folder(session_folder)
    if args.pilot_rules:
        ok2, fail2 = verify_pilot_rules(catalog)
        ok.extend(ok2)
        fail.extend(fail2)

    if ok:
        print("OK:")
        for line in ok:
            print(f"  [+] {line}")
    if fail:
        print("\nFALLA:")
        for line in fail:
            print(f"  [-] {line}")

    if not fail:
        print("\nResultado: PASS")
        print("\nSiguiente paso:")
        print('  python _tools/analyze_all_rq.py "CSV data" --forms-dir "Forms data"')
        return 0

    print("\nResultado: REVISAR")
    return 1


if __name__ == "__main__":
    raise SystemExit(main())
