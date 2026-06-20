#!/usr/bin/env python3
"""Genera presentación PPTX desde tablas en _analysis/."""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

from _bootstrap import setup

setup()

from _paths import OUT_PPT, ROOT

from informe_data_local import load_analysis
from informe_narrative_local import build_narrative
from ppt_local import TITLE, build_pptx


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Genera presentación PPTX desde tablas exportadas (local)"
    )
    parser.add_argument(
        "--data-dir",
        type=Path,
        default=ROOT / "_analysis",
        help="Carpeta con CSV y figures/ del pipeline principal",
    )
    parser.add_argument(
        "--output-dir",
        type=Path,
        default=OUT_PPT,
        help="Destino del archivo .pptx",
    )
    parser.add_argument(
        "--basename",
        default="Presentacion_PF3311",
        help="Nombre base del archivo (sin extensión)",
    )
    args = parser.parse_args()

    data_dir = args.data_dir.resolve()
    if not data_dir.is_dir():
        print(f"ERROR: no existe {data_dir}", file=sys.stderr)
        return 1

    bundle = load_analysis(data_dir)
    if not bundle.sessions and not bundle.rq1_group:
        print(
            "ERROR: _analysis/ vacío. Ejecutá primero analyze_all_rq.py",
            file=sys.stderr,
        )
        return 1

    narrative = build_narrative(bundle)
    output_path = args.output_dir / f"{args.basename}.pptx"
    build_pptx(bundle, narrative, output_path)
    print(f"PPTX: {output_path.resolve()}")
    print(f"Título: {TITLE}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
