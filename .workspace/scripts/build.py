#!/usr/bin/env python3
"""
Orquestador: tablas _analysis/ → Word → PDF del informe.

  pip install -r _tools/requirements-analysis.txt
  pip install -r .workspace/requirements.txt
  python .workspace/scripts/build.py
"""

from __future__ import annotations

import argparse
import subprocess
import sys
from pathlib import Path

from _bootstrap import setup

setup()

from _paths import OUT_INFORME, OUT_LOGS, OUT_PAPER, OUT_PPT, ROOT, SCRIPTS, TOOLS


def _run(cmd: list[str], label: str) -> int:
    print(f"\n=== {label} ===")
    print(" ".join(cmd))
    result = subprocess.run(cmd, cwd=ROOT)
    if result.returncode != 0:
        print(f"ERROR: falló {label} (código {result.returncode})", file=sys.stderr)
    return result.returncode


def main() -> int:
    parser = argparse.ArgumentParser(description="Análisis + informe PDF (área de trabajo local)")
    parser.add_argument("csv_dir", nargs="?", default="CSV data")
    parser.add_argument("--forms-dir", type=Path, default=Path("Forms data"))
    parser.add_argument("--data-dir", type=Path, default=Path("_analysis"))
    parser.add_argument("--logs-dir", type=Path, default=OUT_LOGS)
    parser.add_argument("--output-dir", type=Path, default=OUT_INFORME)
    parser.add_argument("--skip-analysis", action="store_true")
    parser.add_argument("--skip-logs", action="store_true")
    parser.add_argument("--docx-only", action="store_true")
    parser.add_argument("--paper", action="store_true")
    parser.add_argument("--paper-only", action="store_true")
    parser.add_argument("--ppt", action="store_true")
    args = parser.parse_args()

    py = sys.executable
    analyze_cmd = [
        py,
        str(TOOLS / "analyze_all_rq.py"),
        args.csv_dir,
        "--forms-dir",
        str(args.forms_dir),
        "--output-dir",
        str(args.data_dir),
    ]

    if args.paper_only:
        if not args.skip_analysis:
            code = _run(analyze_cmd, "Pipeline CSV/PNG")
            if code != 0:
                return code
        paper_cmd = [py, str(SCRIPTS / "generate_paper_local.py"), "--data-dir", str(args.data_dir)]
        if args.docx_only:
            paper_cmd.append("--docx-only")
        code = _run(paper_cmd, "Artículo Word/PDF")
        if code == 0:
            print(f"\nSalida paper: {OUT_PAPER.resolve()}")
        return code

    if not args.skip_analysis:
        code = _run(analyze_cmd, "Pipeline CSV/PNG")
        if code != 0:
            return code

    if not args.skip_logs:
        code = _run(
            [
                py,
                str(SCRIPTS / "generate_informe_logs.py"),
                "--data-dir",
                str(args.data_dir),
                "--logs-dir",
                str(args.logs_dir),
            ],
            "Logs de apoyo",
        )
        if code != 0:
            return code

    informe_cmd = [
        py,
        str(SCRIPTS / "generate_informe_local.py"),
        "--data-dir",
        str(args.data_dir),
        "--output-dir",
        str(args.output_dir),
    ]
    if args.docx_only:
        informe_cmd.append("--docx-only")

    code = _run(informe_cmd, "Informe Word/PDF")
    if code != 0:
        return code

    if args.paper:
        paper_cmd = [py, str(SCRIPTS / "generate_paper_local.py"), "--data-dir", str(args.data_dir)]
        if args.docx_only:
            paper_cmd.append("--docx-only")
        code = _run(paper_cmd, "Artículo Word/PDF")
        if code != 0:
            return code

    if args.ppt:
        code = _run(
            [py, str(SCRIPTS / "generate_ppt_local.py"), "--data-dir", str(args.data_dir)],
            "Presentación PPTX",
        )
        if code != 0:
            return code

    print("\nListo.")
    print(f"  Tablas/gráficos: {args.data_dir.resolve()}")
    if not args.skip_logs:
        print(f"  Logs: {args.logs_dir.resolve()}")
    print(f"  Informe: {args.output_dir.resolve()}")
    if args.paper:
        print(f"  Paper: {OUT_PAPER.resolve()}")
    if args.ppt:
        print(f"  PPT: {OUT_PPT.resolve()}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
