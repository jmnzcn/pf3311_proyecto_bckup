#!/usr/bin/env python3
"""Numeración consecutiva de figuras según aparición en informe_docx_local."""

from __future__ import annotations

# Orden de aparición en el informe (sección 2 → 6)
FIG_PERFIL = 1
FIG_RQ1_PRECISION = 2
FIG_RQ2_CONFIDENCE = 3
FIG_RAW_TLX = 4
FIG_MECUE = 5
FIG_TIME = 6
FIG_RQ3_GAP = 7
FIG_RQ3_GAP_BY_ITEM = 8
FIG_RQ3_CURVE_BY_ITEM = 9
FIG_CHAT_HELPSCORE = 10
FIG_CHAT_EXCHANGES = 11
FIG_CHAT_LEAKS = 12


def fig(number: int) -> str:
    return f"Figura {number}"


def figs(from_number: int, to_number: int) -> str:
    if from_number == to_number:
        return fig(from_number)
    return f"Figuras {from_number}–{to_number}"


def caption(number: int, title: str) -> str:
    return f"Figura {number}. {title}"
