#!/usr/bin/env python3
"""Genera artículo Word/PDF desde tablas en _analysis/."""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

from _bootstrap import setup

setup()

from _paths import OUT_PAPER, ROOT

from generate_entregable2_docx import export_pdf
from informe_data_local import load_analysis
from paper_docx_local import build_paper_docx
from paper_narrative_local import build_paper_narrative, TITLE


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Genera artículo tipo paper Word/PDF desde tablas exportadas (local)"
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
        default=OUT_PAPER,
        help="Destino del .docx y .pdf",
    )
    parser.add_argument(
        "--basename",
        default="Articulo_PF3311",
        help="Nombre base del archivo (sin extensión)",
    )
    parser.add_argument(
        "--docx-only",
        action="store_true",
        help="No intentar exportar PDF (requiere Microsoft Word en Windows)",
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

    paper = build_paper_narrative(bundle)
    output_dir = args.output_dir
    docx_path = output_dir / f"{args.basename}.docx"
    pdf_path = output_dir / f"{args.basename}.pdf"

    build_paper_docx(bundle, paper, docx_path)
    print(f"DOCX: {docx_path.resolve()}")
    print(f"Título: {TITLE}")

    if not args.docx_only:
        export_pdf(docx_path.resolve(), pdf_path.resolve())

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
