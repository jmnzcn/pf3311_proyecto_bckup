"""Inicializa sys.path para scripts del área de trabajo."""

from __future__ import annotations

import sys
from pathlib import Path

_SCRIPTS = Path(__file__).resolve().parent
if str(_SCRIPTS) not in sys.path:
    sys.path.insert(0, str(_SCRIPTS))

from _paths import SCRIPTS, TOOLS  # noqa: E402


def setup() -> None:
    for directory in (SCRIPTS, TOOLS):
        entry = str(directory)
        if entry not in sys.path:
            sys.path.insert(0, entry)
