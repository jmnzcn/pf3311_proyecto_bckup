"""Rutas del área de trabajo local (no versionada)."""

from __future__ import annotations

from pathlib import Path

WORKSPACE = Path(__file__).resolve().parent.parent
ROOT = WORKSPACE.parent
TOOLS = ROOT / "_tools"
SCRIPTS = WORKSPACE / "scripts"
OUT = WORKSPACE / "output"
OUT_INFORME = OUT / "informe"
OUT_PAPER = OUT / "paper"
OUT_PPT = OUT / "ppt"
OUT_LOGS = OUT / "logs"
