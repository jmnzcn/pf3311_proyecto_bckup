#!/usr/bin/env python3
"""Resume latencia Gemini desde ChatHelpRating.csv (criterio piloto: <=5 s en >=90% turnos)."""

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


def parse_float(value: str) -> float | None:
    value = (value or "").strip()
    if not value:
        return None
    try:
        return float(value)
    except ValueError:
        return None


def analyze_file(path: Path, csv_dir: Path, threshold: float) -> dict:
    latencies: list[float] = []
    with path.open(encoding="utf-8-sig", newline="") as handle:
        reader = csv.DictReader(handle)
        for row in reader:
            latency = parse_float(row.get("GeminiLatencySeconds", ""))
            if latency is not None:
                latencies.append(latency)

    total = len(latencies)
    label = display_path(csv_dir, path)
    if total == 0:
        return {
            "label": label,
            "total": 0,
            "within_threshold": 0,
            "pct_within": 0.0,
            "min": None,
            "max": None,
            "mean": None,
            "p90": None,
            "passes_90pct_rule": False,
        }

    within = sum(1 for value in latencies if value <= threshold)
    sorted_values = sorted(latencies)
    p90_index = max(0, int(round(0.9 * (total - 1))))
    mean = sum(latencies) / total

    return {
        "label": label,
        "total": total,
        "within_threshold": within,
        "pct_within": 100.0 * within / total,
        "min": sorted_values[0],
        "max": sorted_values[-1],
        "mean": mean,
        "p90": sorted_values[p90_index],
        "passes_90pct_rule": (within / total) >= 0.9,
    }


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Analiza GeminiLatencySeconds en ChatHelpRating.csv"
    )
    parser.add_argument(
        "csv_dir",
        nargs="?",
        default="CSV data",
        help="Carpeta CSV data (default: CSV data)",
    )
    parser.add_argument(
        "--threshold",
        type=float,
        default=5.0,
        help="Umbral en segundos (default: 5.0)",
    )
    args = parser.parse_args()

    csv_dir = Path(args.csv_dir)
    if not csv_dir.is_dir():
        print(f"ERROR: no existe la carpeta {csv_dir}", file=sys.stderr)
        return 1

    files = discover_csv_files(csv_dir, "ChatHelpRating.csv", "ChatHelpRating_*.csv")
    if not files:
        print(f"No se encontraron ChatHelpRating.csv ni ChatHelpRating_*.csv en {csv_dir}")
        return 1

    print(f"Umbral: {args.threshold:.1f} s | Regla piloto: >=90% de turnos dentro del umbral\n")

    all_latencies: list[float] = []
    for path in files:
        result = analyze_file(path, csv_dir, args.threshold)
        if result["total"] == 0:
            print(f"{result['label']}: sin filas con GeminiLatencySeconds")
            continue

        status = "OK" if result["passes_90pct_rule"] else "NO CUMPLE"
        print(
            f"{result['label']}: n={result['total']} | "
            f"<= {args.threshold:.1f}s: {result['within_threshold']} ({result['pct_within']:.1f}%) | "
            f"mean={result['mean']:.2f}s p90={result['p90']:.2f}s max={result['max']:.2f}s | {status}"
        )

        with path.open(encoding="utf-8-sig", newline="") as handle:
            reader = csv.DictReader(handle)
            for row in reader:
                latency = parse_float(row.get("GeminiLatencySeconds", ""))
                if latency is not None:
                    all_latencies.append(latency)

    if len(all_latencies) > 1:
        combined_within = sum(1 for value in all_latencies if value <= args.threshold)
        combined_pct = 100.0 * combined_within / len(all_latencies)
        combined_pass = combined_within / len(all_latencies) >= 0.9
        status = "OK" if combined_pass else "NO CUMPLE"
        print(
            f"\nTOTAL combinado: n={len(all_latencies)} | "
            f"<= {args.threshold:.1f}s: {combined_within} ({combined_pct:.1f}%) | {status}"
        )

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
