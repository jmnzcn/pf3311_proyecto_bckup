#!/usr/bin/env python3
"""Carga y puntuacion de exportaciones CSV de Google Forms (meCUE + RAW-TLX)."""

from __future__ import annotations

import csv
import re
from dataclasses import dataclass, field
from pathlib import Path

# Patrones específicos primero: *Post*B* matchea «PostBloqueA» por la B de «Bloque».
FORM_FILE_PATTERNS = {
    "A": ("*PostBloqueA*.csv", "Form1*.csv", "*postbloquea*.csv"),
    "B": ("*PostBloqueB*.csv", "Form2*.csv", "*postbloqueb*.csv"),
    "C": ("*PostBloqueC*.csv", "Form3*.csv", "*postbloquec*.csv"),
}

PERFIL_FILE_PATTERNS = ("Form0*.csv", "*Perfil*.csv", "*perfil*.csv")

PARTICIPANT_COLUMN_KEYWORDS = ("codigo de participante", "código de participante", "participant")

RAW_TLX_KEYWORDS: list[list[str]] = [
    ["actividad mental"],
    ["presión de tiempo", "presion de tiempo"],
    ["trabajar muy duro", "esfuerzo"],
    ["frustrado", "tenso", "irritado"],
    ["seguro", "desempeñ", "desempen"],
    ["exigente en general", "exigente"],
]

MECUE_MODULE_I_KEYWORDS: list[list[str]] = [
    ["fácil de usar", "facil de usar"],
    ["funciones del asistente apoyan", "apoyan lo que necesitaba"],
    ["evidente rápidamente", "evidente rapidamente"],
    ["extremadamente útil", "extremadamente util"],
    ["procedimientos de uso", "sencillos de entender"],
    ["pude avanzar en los casos"],
]

MECUE_MODULE_II_KEYWORDS: list[list[str]] = [
    ["diseñado de forma creativa", "disenado de forma creativa"],
    ["diseño se ve atractivo", "diseno se ve atractivo"],
    ["elegante", "estilo"],
    ["personaje del agente", "cercano"],
    ["sin un agente como este"],
]

MECUE_MODULE_III_KEYWORDS: list[list[str]] = [
    ["entusiasma"],
    ["cansa"],
    ["molesta"],
    ["relaja"],
    ["agotado"],
    ["feliz"],
    ["frustra"],
    ["eufórico", "euforico"],
    ["pasivo"],
    ["calma"],
    ["alegre"],
    ["enoja"],
]

MECUE_MODULE_IV_KEYWORDS: list[list[str]] = [
    ["volvería a usar", "volveria a usar"],
    ["pierdo la noción del tiempo", "pierdo la nocion del tiempo"],
]

MODULE_V_KEYWORDS = ["evaluás el producto", "evaluas el producto", "evaluación global", "evaluacion global"]


@dataclass
class FormResponse:
    participant_code: str
    condition: str
    source_file: str
    module_scores: dict[str, float | None] = field(default_factory=dict)
    raw_tlx_mean: float | None = None


@dataclass
class PerfilResponse:
    participant_code: str
    age_range: str
    education: str
    assistant_frequency: str
    avatar_experience: str
    source_file: str


def normalize_participant_code(raw: str) -> str | None:
    text = (raw or "").strip().upper().replace(" ", "")
    if not text:
        return None
    if text.startswith("P"):
        text = text[1:]
    if not text.isdigit():
        return None
    number = int(text)
    if number < 1 or number > 99:
        return None
    return f"P{number:02d}"


def discover_form_file(forms_dir: Path, condition: str) -> Path | None:
    patterns = FORM_FILE_PATTERNS.get(condition, ())
    for pattern in patterns:
        matches = sorted(forms_dir.glob(pattern))
        if matches:
            return matches[0]
    return None


def discover_perfil_file(forms_dir: Path) -> Path | None:
    for pattern in PERFIL_FILE_PATTERNS:
        matches = sorted(forms_dir.glob(pattern))
        if matches:
            return matches[0]
    return None


def _normalize_header(value: str) -> str:
    return re.sub(r"\s+", " ", (value or "").strip().lower())


def find_participant_column(columns: list[str]) -> str | None:
    for col in columns:
        header = _normalize_header(col)
        if any(keyword in header for keyword in PARTICIPANT_COLUMN_KEYWORDS):
            return col
    return None


def find_column_any(columns: list[str], alternatives: list[str]) -> str | None:
    for col in columns:
        header = _normalize_header(col)
        if any(alt in header for alt in alternatives):
            return col
    return None


def find_module_v_column(columns: list[str]) -> str | None:
    for col in columns:
        header = _normalize_header(col)
        if any(keyword in header for keyword in MODULE_V_KEYWORDS):
            return col
    return None


def parse_scale_1_7(value: str) -> float | None:
    text = (value or "").strip()
    if not text:
        return None
    if text.isdigit():
        number = int(text)
        if 1 <= number <= 7:
            return float(number)
    match = re.search(r"\b([1-7])\b", text)
    if match:
        return float(match.group(1))
    return None


def parse_module_v(value: str) -> float | None:
    text = (value or "").strip()
    if not text:
        return None
    if "muy malo" in text.lower() and "-5" in text:
        return -5.0
    if "muy bueno" in text.lower() and "+5" in text:
        return 5.0
    match = re.match(r"^\s*([+-]?\d+)", text.replace("—", "-"))
    if match:
        return float(match.group(1))
    if text in {"0", "0 — Neutral", "0 - Neutral"}:
        return 0.0
    return None


def mean_from_row(row: dict[str, str], columns: list[str | None]) -> float | None:
    values: list[float] = []
    for col in columns:
        if col is None:
            continue
        parsed = parse_scale_1_7(row.get(col, ""))
        if parsed is not None:
            values.append(parsed)
    if not values:
        return None
    return sum(values) / len(values)


def score_item_columns(row: dict[str, str], columns: list[str]) -> list[float | None]:
    scored: list[float | None] = []
    for col in columns:
        scored.append(parse_scale_1_7(row.get(col, "")))
    return scored


def resolve_item_columns(columns: list[str], keyword_sets: list[list[str]]) -> list[str | None]:
    resolved: list[str | None] = []
    for keywords in keyword_sets:
        resolved.append(find_column_any(columns, keywords))
    return resolved


def load_perfil_responses(forms_dir: Path) -> list[PerfilResponse]:
    path = discover_perfil_file(forms_dir)
    if path is None:
        return []

    responses: list[PerfilResponse] = []
    with path.open(encoding="utf-8-sig", newline="") as handle:
        reader = csv.DictReader(handle)
        columns = reader.fieldnames or []
        participant_col = find_participant_column(columns)
        if participant_col is None:
            return []

        age_col = find_column_any(columns, ["rango de edad", "edad te encontr"])
        edu_col = find_column_any(columns, ["nivel educativo"])
        assistant_col = find_column_any(
            columns, ["asistentes digitales", "siri", "chatgpt", "copilot"]
        )
        avatar_col = find_column_any(
            columns, ["agentes virtuales", "avatares conversacionales", "avatares"]
        )

        for row in reader:
            code = normalize_participant_code(row.get(participant_col, ""))
            if code is None:
                continue
            responses.append(
                PerfilResponse(
                    participant_code=code,
                    age_range=(row.get(age_col, "") if age_col else "").strip(),
                    education=(row.get(edu_col, "") if edu_col else "").strip(),
                    assistant_frequency=(row.get(assistant_col, "") if assistant_col else "").strip(),
                    avatar_experience=(row.get(avatar_col, "") if avatar_col else "").strip(),
                    source_file=path.name,
                )
            )
    return responses


def load_form_responses(forms_dir: Path, condition: str) -> list[FormResponse]:
    path = discover_form_file(forms_dir, condition)
    if path is None:
        return []

    responses: list[FormResponse] = []
    with path.open(encoding="utf-8-sig", newline="") as handle:
        reader = csv.DictReader(handle)
        columns = reader.fieldnames or []
        participant_col = find_participant_column(columns)
        if participant_col is None:
            return []

        tlx_cols = resolve_item_columns(columns, RAW_TLX_KEYWORDS)
        mod_i_cols = resolve_item_columns(columns, MECUE_MODULE_I_KEYWORDS)
        mod_ii_cols = resolve_item_columns(columns, MECUE_MODULE_II_KEYWORDS)
        mod_iii_cols = resolve_item_columns(columns, MECUE_MODULE_III_KEYWORDS)
        mod_iv_cols = resolve_item_columns(columns, MECUE_MODULE_IV_KEYWORDS)
        mod_v_col = find_module_v_column(columns)

        for row in reader:
            code = normalize_participant_code(row.get(participant_col, ""))
            if code is None:
                continue

            module_scores: dict[str, float | None] = {}
            for label, cols in (
                ("MECUE_I", mod_i_cols),
                ("MECUE_II", mod_ii_cols),
                ("MECUE_III", mod_iii_cols),
                ("MECUE_IV", mod_iv_cols),
            ):
                if any(col is not None for col in cols):
                    module_scores[label] = mean_from_row(row, cols)

            if mod_v_col is not None:
                module_scores["MECUE_V"] = parse_module_v(row.get(mod_v_col, ""))

            tlx_mean = mean_from_row(row, tlx_cols)

            responses.append(
                FormResponse(
                    participant_code=code,
                    condition=condition,
                    source_file=path.name,
                    module_scores=module_scores,
                    raw_tlx_mean=tlx_mean,
                )
            )
    return responses


def load_all_forms(forms_dir: Path) -> dict[str, list[FormResponse]]:
    if not forms_dir.is_dir():
        return {}
    return {
        condition: load_form_responses(forms_dir, condition)
        for condition in ("A", "B", "C")
    }


def index_by_participant(responses: list[FormResponse]) -> dict[str, FormResponse]:
    indexed: dict[str, FormResponse] = {}
    for response in responses:
        indexed[response.participant_code] = response
    return indexed
