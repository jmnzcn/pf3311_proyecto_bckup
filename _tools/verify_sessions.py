#!/usr/bin/env python3
"""Verifica carpetas de sesion en CSV data/ — exporta sessions_summary.csv."""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

_TOOLS_DIR = Path(__file__).resolve().parent
if str(_TOOLS_DIR) not in sys.path:
    sys.path.insert(0, str(_TOOLS_DIR))

from session_catalog import build_catalog, export_session_summary


def main() -> int:
    parser = argparse.ArgumentParser(description="Exporta resumen de sesiones Unity a CSV")
    parser.add_argument("csv_dir", nargs="?", default="CSV data")
    parser.add_argument(
        "--output-dir",
        type=Path,
        default=Path("_analysis"),
        help="Exporta sessions_summary.csv",
    )
    parser.add_argument(
        "--all-sessions",
        action="store_true",
        help="Incluir todas las carpetas (no solo la canonica por P##)",
    )
    args = parser.parse_args()

    csv_dir = Path(args.csv_dir)
    if not csv_dir.is_dir():
        print(f"ERROR: no existe {csv_dir}", file=sys.stderr)
        return 1

    catalog = build_catalog(csv_dir, canonical_only=not args.all_sessions)

    if not catalog.sessions:
        print("AVISO: no se encontraron carpetas con ExperimentData.csv", file=sys.stderr)
        return 1

    args.output_dir.mkdir(parents=True, exist_ok=True)
    path = export_session_summary(catalog, args.output_dir)
    print(
        f"OK: {len(catalog.sessions)} sesiones, "
        f"{len(catalog.canonical)} canonicas, "
        f"{len(catalog.complete_participants)} completas -> {path.resolve()}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
