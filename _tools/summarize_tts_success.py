#!/usr/bin/env python3
"""Resume TTS en condición C desde TtsLog.csv y ChatScenarioSummary.csv (meta >=85%)."""

from __future__ import annotations

import argparse
import csv
import sys
from pathlib import Path


def discover_csv_files(csv_dir: Path, file_name: str, legacy_glob: str) -> list[Path]:
    matches = set(csv_dir.glob(f"**/{file_name}"))
    matches.update(csv_dir.glob(legacy_glob))
    return sorted(matches)


def display_path(csv_dir: Path, path: Path) -> str:
    try:
        return str(path.relative_to(csv_dir))
    except ValueError:
        return path.name


def analyze_tts_log(path: Path, csv_dir: Path) -> dict:
    attempts = 0
    successes = 0
    with path.open(encoding="utf-8-sig", newline="") as handle:
        reader = csv.DictReader(handle)
        for row in reader:
            attempts += 1
            if (row.get("TtsSuccess") or "").strip() == "1":
                successes += 1

    rate = (100.0 * successes / attempts) if attempts else 0.0
    return {
        "label": display_path(csv_dir, path),
        "attempts": attempts,
        "successes": successes,
        "rate": rate,
        "passes_85pct": rate >= 85.0 if attempts else False,
    }


def analyze_scenario_summary(path: Path, csv_dir: Path) -> list[str]:
    lines: list[str] = []
    label = display_path(csv_dir, path)
    with path.open(encoding="utf-8-sig", newline="") as handle:
        reader = csv.DictReader(handle)
        for row in reader:
            if (row.get("ConditionCode") or "").strip() != "C":
                continue
            attempts = int((row.get("TtsAttempts") or "0").strip() or 0)
            if attempts <= 0:
                continue
            rate_raw = (row.get("TtsSuccessRate") or "").strip()
            rate = float(rate_raw) if rate_raw else 0.0
            scenario = row.get("ScenarioName") or row.get("ScenarioNumber") or "?"
            status = "OK" if rate >= 85.0 else "NO CUMPLE"
            lines.append(
                f"{label} escenario {scenario}: "
                f"{row.get('TtsSuccessCount', '?')}/{attempts} ({rate:.1f}%) | {status}"
            )
    return lines


def main() -> int:
    parser = argparse.ArgumentParser(description="Analiza TtsLog.csv y ChatScenarioSummary.csv")
    parser.add_argument(
        "csv_dir",
        nargs="?",
        default="CSV data",
        help="Carpeta CSV data (default: CSV data)",
    )
    args = parser.parse_args()

    csv_dir = Path(args.csv_dir)
    if not csv_dir.is_dir():
        print(f"ERROR: no existe la carpeta {csv_dir}", file=sys.stderr)
        return 1

    tts_files = discover_csv_files(csv_dir, "TtsLog.csv", "TtsLog_*.csv")
    summary_files = discover_csv_files(csv_dir, "ChatScenarioSummary.csv", "ChatScenarioSummary_*.csv")

    if not tts_files and not summary_files:
        print(f"No se encontraron TtsLog.csv ni ChatScenarioSummary.csv en {csv_dir}")
        return 1

    print("Meta piloto: TtsSuccessRate >= 85% en condición C\n")

    total_attempts = 0
    total_successes = 0
    for path in tts_files:
        result = analyze_tts_log(path, csv_dir)
        if result["attempts"] == 0:
            print(f"{result['label']}: sin intentos TTS")
            continue
        status = "OK" if result["passes_85pct"] else "NO CUMPLE"
        print(
            f"{result['label']}: {result['successes']}/{result['attempts']} "
            f"({result['rate']:.1f}%) | {status}"
        )
        total_attempts += result["attempts"]
        total_successes += result["successes"]

    if total_attempts > 0:
        combined_rate = 100.0 * total_successes / total_attempts
        status = "OK" if combined_rate >= 85.0 else "NO CUMPLE"
        print(
            f"\nTOTAL TtsLog combinado: {total_successes}/{total_attempts} "
            f"({combined_rate:.1f}%) | {status}"
        )

    summary_lines: list[str] = []
    for path in summary_files:
        summary_lines.extend(analyze_scenario_summary(path, csv_dir))

    if summary_lines:
        print("\nResúmenes por escenario (ChatScenarioSummary):")
        for line in summary_lines:
            print(f"  {line}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
