#!/usr/bin/env python3
"""Narrativa IMRaD para artículo tipo paper."""

from __future__ import annotations

from dataclasses import dataclass, field

from informe_data_local import AnalysisBundle
from informe_narrative_local import build_narrative, build_perfil_narrative

TITLE = (
    "Efecto de la visibilidad de un agente virtual en el desempeño "
    "y la confianza del usuario en tareas de decisión basadas en reglas"
)
AUTHORS = "Ney Fred Jiménez Campos"
AFFILIATION = (
    "Universidad de Costa Rica, Posgrado en Computación e Informática, "
    "PF-3311 — Agentes Virtuales Inteligentes"
)
KEYWORDS = (
    "agente virtual; confianza calibrada; diseño within-subjects; "
    "LLM; precisión; usabilidad"
)

REFERENCES = [
    "Lee, J. D., & See, K. A. (2004). Trust in automation: Designing for appropriate reliance. "
    "Human Factors, 46(1), 50–80.",
    "Minge, M., & Thüring, M. (2018). meCUE 2.0. Proceedings of Mensch und Computer 2018.",
    "Hart, S. G., & Staveland, L. E. (1988). Development of NASA-TLX. Advances in Psychology, 52, 139–183.",
    "Nielsen, J. (1994). Usability Engineering. Morgan Kaufmann.",
    "Brooke, J. (1996). SUS: A quick and dirty usability scale. Usability Evaluation in Industry.",
]


@dataclass
class PaperNarrative:
    title: str = TITLE
    authors: str = AUTHORS
    affiliation: str = AFFILIATION
    keywords: str = KEYWORDS
    abstract: list[str] = field(default_factory=list)
    sections: dict[str, list[str]] = field(default_factory=dict)
    references: list[str] = field(default_factory=list)


def _mean_line(rows: list[dict[str, str]], cond_key: str, val_key: str, suffix: str = "") -> str:
    parts: list[str] = []
    for row in rows:
        cond = row.get(cond_key, "")
        val = row.get(val_key, "")
        if cond and val:
            parts.append(f"{cond}={val}{suffix}")
    return ", ".join(parts) if parts else "sin datos"


def _build_abstract(bundle: AnalysisBundle) -> list[str]:
    n = len(bundle.complete_canonical) or len(bundle.canonical_sessions)
    n_canon = len(bundle.canonical_sessions)
    rq1 = _mean_line(bundle.rq1_group, "Condition", "MeanAccuracyPct", "%")
    rq2 = _mean_line(bundle.rq2_group, "Condition", "MeanConfidence")
    rq3 = _mean_line(bundle.rq3_group, "Condition", "MeanCalibrationGap")

    return [
        "Se evaluó un prototipo Unity con tres condiciones within-subjects: "
        "A (sin agente), B (agente conversacional en texto) y C (mismo agente con avatar 3D y voz). "
        "Cada participante resolvió seis ítems de decisión basados en reglas por condición, "
        "reportando confianza inmediata (escala 1–7) tras cada respuesta.",
        f"En el estudio participaron {n_canon} sesiones canónicas "
        f"({n} con bloques A, B y C completos). "
        f"La precisión media fue {rq1}. "
        f"La confianza media (Unity) fue {rq2}. "
        f"La brecha de calibración (confianza normalizada menos precisión del bloque) "
        f"fue {rq3}. "
        "Se discuten implicaciones para el diseño de agentes virtuales pedagógicos "
        "y las limitaciones de una muestra exploratoria.",
    ]


def build_paper_narrative(bundle: AnalysisBundle) -> PaperNarrative:
    informe = build_narrative(bundle)
    paper = PaperNarrative()
    paper.abstract = _build_abstract(bundle)
    paper.references = REFERENCES.copy()

    n_complete = len(bundle.complete_canonical)
    n_canon = len(bundle.canonical_sessions)

    paper.sections["introduccion"] = [
        "Los agentes conversacionales basados en modelos de lenguaje amplio (LLM) "
        "se integran cada vez más en interfaces de apoyo a la decisión. "
        "Una pregunta abierta es si la presencia visible del agente (avatar y voz) "
        "modifica el desempeño objetivo y la confianza subjetiva del usuario "
        "frente a la misma asistencia solo textual o sin agente.",
        "Este trabajo reporta un estudio within-subjects en el que cada participante "
        "experimenta tres condiciones: sin agente (A), chat con LLM (B) y chat con "
        "avatar animado y síntesis de voz (C). Las preguntas de investigación son: "
        "(RQ1) ¿difieren los aciertos entre condiciones?; "
        "(RQ2) ¿difieren los niveles de confianza?; y "
        "(RQ3) ¿cómo varía la calibración entre confianza y precisión?",
        "Las hipótesis exploratorias plantean precisión C ≈ B > A (H1), "
        "confianza C > B > A (H2) y mayor sobreconfianza en C (H3).",
    ]

    metodo = [
        "**Participantes.** Adultos mayores de 18 años reclutados de forma abierta. "
        f"Se analizaron {n_canon} sesiones canónicas; "
        f"{n_complete} completaron los tres bloques (6+6+6 ítems).",
    ]
    metodo.extend(build_perfil_narrative(bundle.perfil))
    metodo.extend(
        [
        "**Diseño.** Factor within-subjects con tres niveles (A, B, C). "
        "Orden de condiciones contrabalanceado según código de participante. "
        "Materiales: 18 ítems con reglas y opciones múltiples; "
        "confianza 1–7 tras cada respuesta; cuestionarios RAW-TLX y meCUE 2.0 tras cada bloque.",
        "**Procedimiento.** Sesión en máquina virtual Windows con build Unity standalone. "
        "En B y C el participante podía consultar al agente (Gemini 2.5 Flash) "
        "con un system prompt restrictivo que prohíbe revelar la respuesta correcta. "
        "En C las respuestas del agente se sintetizan con Azure Speech y lip-sync sobre avatar TTBoyB.",
        "**Métricas.** Precisión (% aciertos por bloque), confianza media por ítem, "
        "brecha de calibración ((confianza−1)/6 − precisión), carga RAW-TLX, "
        "métricas de chat (HelpScore, intercambios, filtraciones del modelo) y "
        "indicadores de viabilidad (latencia Gemini, éxito TTS, integridad CSV).",
        "**Análisis.** Estadística descriptiva por condición. "
        "Con n≥3 participantes completos se aplican prueba de Friedman y "
        "Wilcoxon pareado post hoc con corrección Bonferroni (α=0,05). "
        "Los resultados se interpretan como exploratorios.",
        ]
    )
    paper.sections["metodo"] = metodo

    results: list[str] = [
        f"La muestra analizada comprendió {n_canon} participantes canónicos "
        f"({n_complete} con datos completos A–C).",
    ]
    results.extend(informe.sections.get("rq1", []))
    results.extend(informe.sections.get("rq2_unity", []))
    if bundle.rq2_forms_tlx:
        results.append("Los cuestionarios RAW-TLX por bloque se resumen en la Tabla 4.")
    results.extend(informe.sections.get("rq3", []))
    results.extend(informe.sections.get("respuestas_rq", []))
    results.extend(informe.sections.get("hipotesis", []))
    if bundle.chat_group:
        results.append("En condiciones B y C se compararon métricas del agente (Tabla 5).")
        results.extend(informe.sections.get("agente", []))
    if bundle.pilot_integrity:
        results.append("Los criterios de viabilidad técnica del estudio se reportan en la Tabla 6.")

    stats_lines = [ln for ln in informe.inference_log if ln.strip() and not ln.startswith("===")]
    if stats_lines:
        results.append("Inferencia no paramétrica:")
        results.extend(stats_lines[:20])

    paper.sections["resultados"] = results

    paper.sections["discusion"] = informe.sections.get("discusion", []) + [
        "La comparación B versus C aísla el efecto de la modalidad embodied "
        "(avatar + voz) manteniendo constante el contenido del LLM. "
        "Los cuestionarios meCUE y RAW-TLX aportan contexto sobre carga y percepción, "
        "aunque la medida primaria de confianza proviene de la escala inmediata en Unity.",
        "Entre las limitaciones figuran el tamaño muestral del estudio, "
        "la dependencia de APIs en la nube y la generalización limitada "
        "fuera del contexto académico ficticio empleado.",
    ]

    paper.sections["conclusiones"] = informe.sections.get("conclusiones", [])

    return paper
