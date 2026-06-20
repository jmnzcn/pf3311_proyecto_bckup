#!/usr/bin/env python3
"""
Títulos de columnas como aparecen al exportar Google Forms a CSV.

Fuente: docs/GoogleForms_Scripts_PF3311.md y docs/google_forms/*.txt
"""

from __future__ import annotations

TIMESTAMP_COL = "Marca temporal"
CODE_COL = "Código de participante"

RAW_TLX_ITEMS: tuple[str, ...] = (
    "La tarea me exigió mucha actividad mental y concentración.",
    "Sentí presión de tiempo mientras resolvía los casos.",
    "Tuve que trabajar muy duro (esfuerzo) para completar la tarea.",
    "Me sentí frustrado/a, tenso/a o irritado/a durante el bloque.",
    "Me sentí seguro/a de cómo desempeñé la tarea en este bloque.",
    "La tarea me resultó exigente en general.",
)

MECUE_I_ITEMS: tuple[str, ...] = (
    "El producto es fácil de usar.",
    "Las funciones del asistente apoyan lo que necesitaba hacer en los casos de este bloque.",
    "Es evidente rápidamente cómo usar el producto.",
    "Considero que el producto es extremadamente útil.",
    "Los procedimientos de uso del producto son sencillos de entender.",
    "Con la ayuda de este asistente pude avanzar en los casos de este bloque.",
)

MECUE_II_ITEMS: tuple[str, ...] = (
    "El producto está diseñado de forma creativa.",
    "El diseño se ve atractivo.",
    "El producto es elegante / tiene estilo.",
    "El personaje del agente me resultó cercano.",
    "Me costaría completar tareas similares sin un agente como este.",
)

MECUE_III_ITEMS: tuple[str, ...] = (
    "El producto me entusiasma.",
    "El producto me cansa.",
    "El producto me molesta.",
    "El producto me relaja.",
    "Al usar este producto me siento agotado/a.",
    "El producto me hace sentir feliz.",
    "El producto me frustra.",
    "El producto me hace sentir eufórico/a.",
    "El producto me hace sentir pasivo/a.",
    "El producto me calma.",
    "Al usar este producto me siento alegre.",
    "El producto me enoja.",
)

MECUE_IV_ITEMS: tuple[str, ...] = (
    "Volvería a usar un asistente como este para tareas similares.",
    "Al usar el producto, pierdo la noción del tiempo.",
)

MECUE_V_B = "¿Cómo evaluás el producto (el sistema de asistencia por chat) en general?"
MECUE_V_C = "¿Cómo evaluás el producto (el agente virtual) en general?"

PERFIL_AGE_COL = "¿En qué rango de edad te encontrás?"
PERFIL_EDU_COL = "Nivel educativo más alto alcanzado"
PERFIL_ASSISTANT_COL = (
    "¿Con qué frecuencia usás asistentes digitales (Siri, Alexa, ChatGPT, Copilot, etc.)?"
)
PERFIL_AVATAR_COL = (
    "¿Has interactuado antes con agentes virtuales o avatares conversacionales "
    "(videojuegos, apps, kioscos, etc.)?"
)

AGE_OPTIONS: tuple[str, ...] = (
    "18–24 años",
    "25–34 años",
    "35–44 años",
    "45–54 años",
    "55–64 años",
    "65 años o más",
)

EDUCATION_OPTIONS: tuple[str, ...] = (
    "Primaria completa",
    "Secundaria completa",
    "Técnico / diplomado",
    "Universidad incompleta",
    "Universidad completa (grado / licenciatura)",
    "Posgrado (maestría / doctorado)",
    "Otro",
)

ASSISTANT_FREQ_OPTIONS: tuple[str, ...] = (
    "Nunca o casi nunca",
    "Algunas veces al mes",
    "Algunas veces por semana",
    "Casi todos los días",
    "Varias veces al día",
)

AVATAR_EXP_OPTIONS: tuple[str, ...] = (
    "Nunca",
    "Una o pocas veces",
    "Algunas veces",
    "Con frecuencia",
)

MECUE_V_LABELS: dict[int, str] = {
    -5: "-5 — Muy malo",
    -4: "-4",
    -3: "-3",
    -2: "-2",
    -1: "-1",
    0: "0 — Neutral",
    1: "+1",
    2: "+2",
    3: "+3",
    4: "+4",
    5: "+5 — Muy bueno",
}

FORM0_PERFIL_HEADERS: tuple[str, ...] = (
    TIMESTAMP_COL,
    CODE_COL,
    PERFIL_AGE_COL,
    PERFIL_EDU_COL,
    PERFIL_ASSISTANT_COL,
    PERFIL_AVATAR_COL,
)

FORM_POST_A_HEADERS: tuple[str, ...] = (TIMESTAMP_COL, CODE_COL) + RAW_TLX_ITEMS

FORM_POST_B_HEADERS: tuple[str, ...] = (
    (TIMESTAMP_COL, CODE_COL)
    + MECUE_I_ITEMS
    + MECUE_III_ITEMS
    + MECUE_IV_ITEMS
    + (MECUE_V_B,)
    + RAW_TLX_ITEMS
)

FORM_POST_C_HEADERS: tuple[str, ...] = (
    (TIMESTAMP_COL, CODE_COL)
    + MECUE_I_ITEMS
    + MECUE_II_ITEMS
    + MECUE_III_ITEMS
    + MECUE_IV_ITEMS
    + (MECUE_V_C,)
    + RAW_TLX_ITEMS
)
