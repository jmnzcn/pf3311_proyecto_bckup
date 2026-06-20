#!/usr/bin/env python3
"""
Pipeline de analisis PF-3311: exporta tablas CSV y graficos PNG.

Uso:
  pip install -r _tools/requirements-analysis.txt
  python _tools/analyze_all_rq.py "CSV data" --forms-dir "Forms data"
"""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

_TOOLS_DIR = Path(__file__).resolve().parent
if str(_TOOLS_DIR) not in sys.path:
    sys.path.insert(0, str(_TOOLS_DIR))

from pipeline_core import run_pipeline


def main() -> int:
    parser = argparse.ArgumentParser(
        description="PF-3311: exporta tablas CSV y graficos PNG"
    )
    parser.add_argument(
        "csv_dir",
        nargs="?",
        default="CSV data",
        help="Carpeta raiz con subcarpetas P##_ID-.../ (default: CSV data)",
    )
    parser.add_argument(
        "--forms-dir",
        type=Path,
        default=Path("Forms data"),
        help="CSV exportados de Google Forms (default: Forms data)",
    )
    parser.add_argument(
        "--output-dir",
        type=Path,
        default=Path("_analysis"),
        help="Salida de tablas y graficos (default: _analysis)",
    )
    parser.add_argument("--skip-forms", action="store_true", help="Omitir meCUE/RAW-TLX")
    parser.add_argument("--skip-plots", action="store_true", help="Omitir graficos PNG")
    parser.add_argument(
        "--all-sessions",
        action="store_true",
        help="Incluir todas las carpetas (no solo la canonica por P##)",
    )
    parser.add_argument(
        "--include-incomplete",
        action="store_true",
        help="Graficos RQ con medias que incluyen participantes incompletos (default: solo 6+6+6)",
    )
    args = parser.parse_args()

    csv_dir = Path(args.csv_dir)
    if not csv_dir.is_dir():
        print(f"ERROR: no existe {csv_dir}", file=sys.stderr)
        return 1

    output_dir = args.output_dir
    code = run_pipeline(
        csv_dir,
        Path(args.forms_dir),
        output_dir,
        skip_forms=args.skip_forms,
        skip_plots=args.skip_plots,
        all_sessions=args.all_sessions,
        complete_only=not args.include_incomplete,
    )

    figures_dir = output_dir / "figures"
    n_csv = len(list(output_dir.glob("*.csv")))
    n_png = len(list(figures_dir.glob("*.png"))) if figures_dir.is_dir() else 0

    if code == 0:
        print(f"OK: {n_csv} tablas CSV, {n_png} graficos en {output_dir.resolve()}")
        print(
            "Salida lista en _analysis/. Los informes en prosa y los documentos del curso "
            "(docs/) se entregan aparte; este comando solo exporta calculos y figuras."
        )
    else:
        print(
            f"AVISO: analisis incompleto ({n_csv} tablas CSV en {output_dir.resolve()})",
            file=sys.stderr,
        )

    return code


if __name__ == "__main__":
    raise SystemExit(main())
