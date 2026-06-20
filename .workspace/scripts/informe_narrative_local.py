#!/usr/bin/env python3
"""Genera narrativa e inferencia para el informe."""

from __future__ import annotations

from dataclasses import dataclass, field
from typing import Callable

from informe_data_local import AnalysisBundle, parse_float
from informe_figures_local import (
    FIG_CHAT_EXCHANGES,
    FIG_CHAT_HELPSCORE,
    FIG_CHAT_LEAKS,
    FIG_MECUE,
    FIG_PERFIL,
    FIG_RAW_TLX,
    FIG_RQ1_PRECISION,
    FIG_RQ2_CONFIDENCE,
    FIG_RQ3_CURVE_BY_ITEM,
    FIG_RQ3_GAP,
    FIG_RQ3_GAP_BY_ITEM,
    FIG_TIME,
    fig,
    figs,
)
from rq_inference import (
    print_hypothesis_summary,
    print_inference_block,
    print_mecue_b_vs_c_inference,
    print_pairwise_result,
    wilcoxon_paired,
)
from rq_common import ITEMS_PER_BLOCK
from informe_synthesis_local import (
    REFERENCES,
    build_conclusiones,
    build_discusion,
    build_flujo_narrative,
    build_hipotesis_section,
    build_metodologia,
    build_mecue_ii_section,
    build_resumen,
    build_respuestas_rq,
)

CONDITION_ORDER = ("A", "B", "C")
Emit = Callable[[str], None]

CONDITION_LABELS = {
    "A": "A (sin agente)",
    "B": "B (chat texto)",
    "C": "C (avatar y voz)",
}


def _means_from_group(rows: list[dict[str, str]], value_key: str) -> dict[str, float]:
    out: dict[str, float] = {}
    for row in rows:
        cond = (row.get("Condition") or "").strip()
        val = parse_float(row.get(value_key, ""))
        if cond and val is not None:
            out[cond] = val
    return out


def _chat_metric(rows: list[dict[str, str]], metric: str) -> tuple[float | None, float | None]:
    for row in rows:
        if (row.get("Metric") or "").strip() == metric:
            return parse_float(row.get("Mean_B", "")), parse_float(row.get("Mean_C", ""))
    return None, None


def _compare_order(values: dict[str, float], order: tuple[str, ...]) -> bool:
    if not all(c in values for c in order):
        return False
    for left, right in zip(order, order[1:]):
        if values[left] <= values[right]:
            return False
    return True


def _approx_equal(a: float, b: float, tol: float) -> bool:
    return abs(a - b) <= tol


def _n_participants(rows: list[dict[str, str]], condition: str) -> str:
    for row in rows:
        if (row.get("Condition") or "").strip() == condition:
            return (row.get("NParticipants") or "").strip() or "?"
    return "?"


def _interpret_rq1(bundle: AnalysisBundle) -> list[str]:
    means = _means_from_group(bundle.rq1_group, "MeanAccuracyPct")
    if not means:
        return ["No hay medias de precisión para interpretar."]

    a, b, c = means.get("A"), means.get("B"), means.get("C")
    lines = [
        f"La {fig(FIG_RQ1_PRECISION)} resume cuánto acertó el grupo en cada condición. "
        "A (gris) es sin agente; B (azul), chat; C (morado), avatar con voz. "
        "La altura de la barra es el porcentaje medio de respuestas correctas en el bloque "
        "de seis ítems. Las barras de error muestran la dispersión entre participantes.",
        "Esta métrica responde RQ1: si el agente ayuda a aplicar las reglas y si cambia "
        "algo pasar del chat al avatar. No mide confianza ni experiencia subjetiva.",
    ]
    if a is not None and b is not None and c is not None:
        lines.append(
            f"En el estudio, A quedó en {a:.1f} % (n={_n_participants(bundle.rq1_group, 'A')}), "
            f"B en {b:.1f} % y C en {c:.1f} %."
        )
        if a < b and a < c:
            lines.append(
                "La lectura más directa es que tener agente (B o C) se asoció con más aciertos "
                "que trabajar solo. Eso encaja con la idea de que el asistente aclara dudas "
                "sobre las reglas del escenario."
            )
        elif a >= min(b, c):
            lines.append(
                "A no fue claramente peor que B o C. Con cinco personas y seis ítems, "
                "un par de respuestas mueve mucho el porcentaje; también pudo influir el orden de los bloques."
            )
        diff_bc = abs(b - c)
        if _approx_equal(b, c, 3.5):
            lines.append(
                f"Entre B y C la diferencia fue pequeña ({diff_bc:.1f} pp). "
                "En este tamaño de muestra, el avatar no pareció separarse del chat en rendimiento."
            )
        elif b > c:
            lines.append(
                f"B superó a C por {diff_bc:.1f} pp. No interpreto eso como «el avatar empeora»; "
                "es una señal a revisar con más datos."
            )
        else:
            lines.append(
                f"C superó a B por {diff_bc:.1f} pp, pero la ventaja fue modesta."
            )
    lines.append(
        "No basta esta figura para decidir un diseño final: es un estudio within-subjects "
        "con pocos ítems. La prueba de Friedman del anexo ayuda a contextualizar si las "
        "diferencias son estadísticamente defendibles."
    )
    return lines


def _interpret_rq2(bundle: AnalysisBundle) -> list[str]:
    means = _means_from_group(bundle.rq2_group, "MeanConfidence")
    lines = [
        f"La {fig(FIG_RQ2_CONFIDENCE)} muestra la confianza media (escala 1–7) que los "
        "participantes marcaron en Unity después de cada respuesta. Mide sensación subjetiva "
        "de seguridad, no si la respuesta fue correcta.",
        "RQ2 pregunta si el agente cambia esa sensación y si el avatar añade algo respecto "
        f"al chat. Conviene leerla junto con la {fig(FIG_RQ1_PRECISION)} (aciertos) y con RQ3 (calibración).",
    ]
    if means:
        parts = []
        for k in CONDITION_ORDER:
            if k in means:
                parts.append(
                    f"{CONDITION_LABELS.get(k, k)}: {means[k]:.2f} "
                    f"(n={_n_participants(bundle.rq2_group, k)})"
                )
        lines.append("En el estudio: " + "; ".join(parts) + ".")
        if _compare_order(means, ("A", "B", "C")):
            lines.append(
                "El orden A < B < C coincide con H2: la confianza subió al agregar agente "
                "y un poco más con avatar y voz."
            )
        elif means.get("C", 0) > means.get("A", 0) and means.get("B", 0) > means.get("A", 0):
            lines.append(
                "B y C superaron a A; la diferencia entre chat y avatar fue más sutil."
            )
        else:
            lines.append(
                "No apareció un orden claro A < B < C. Con tan pocos datos, un bloque difícil "
                "o el cansancio del tercer escenario pueden explicar desviaciones."
            )
    lines.append(
        f"RAW-TLX y meCUE ({figs(FIG_RAW_TLX, FIG_MECUE)}) se responden al terminar cada bloque "
        "y miden carga o experiencia global. Esta figura captura la certeza momento a momento "
        "dentro del simulador."
    )
    return lines


def _interpret_forms_tlx(bundle: AnalysisBundle) -> list[str]:
    means = _means_from_group(bundle.rq2_forms_tlx_group, "MeanRAWTLX")
    lines = [
        f"La {fig(FIG_RAW_TLX)} muestra la carga de trabajo percibida (RAW-TLX, 1–7) "
        "después de cada bloque. Valores altos indican que la tarea se sintió más exigente.",
        "Sirve para contextualizar RQ2: no es lo mismo sentirse seguro "
        f"({fig(FIG_RQ2_CONFIDENCE)}) que sentir que el bloque «pesó» mentalmente.",
    ]
    if means:
        for cond in CONDITION_ORDER:
            if cond in means:
                lines.append(
                    f"{CONDITION_LABELS.get(cond, cond)}: RAW-TLX medio {means[cond]:.2f} "
                    f"(n={_n_participants(bundle.rq2_forms_tlx_group, cond)})."
                )
        a, b, c = means.get("A"), means.get("B"), means.get("C")
        if a is not None and b is not None and c is not None:
            if c > b and c > a:
                lines.append(
                    "La carga fue algo mayor en C: escuchar y seguir al avatar puede sumar "
                    "demanda aunque el agente aclare dudas."
                )
            elif b < a and c < a:
                lines.append(
                    "Con agente (B y C) la carga percibida fue menor que en A."
                )
            elif _approx_equal(a, b, 0.4) and _approx_equal(b, c, 0.4):
                lines.append(
                    "Las tres condiciones quedaron parecidas en carga percibida."
                )
    lines.append(
        "RAW-TLX es autoinforme y se aplica al cerrar el bloque; no reemplaza medir tiempo "
        f"({fig(FIG_TIME)}) ni confianza por ítem."
    )
    return lines


def _interpret_mecue(bundle: AnalysisBundle) -> list[str]:
    lines = [
        f"La {fig(FIG_MECUE)} compara meCUE 2.0 entre B (chat) y C (avatar). "
        "No hay barra para A porque en ese bloque no hubo agente que evaluar.",
        "Cada par de barras corresponde a un módulo: utilidad (I), emociones (III), "
        "consecuencias (IV) y evaluación global (V). Miden experiencia de usuario con el "
        "sistema conversacional, no aciertos en el simulador.",
    ]
    if bundle.rq2_forms_mecue_group:
        lines.append("Medias del estudio (B vs C):")
        for row in bundle.rq2_forms_mecue_group:
            module = row.get("ModuleLabel") or row.get("Module", "")
            mb, mc = parse_float(row.get("Mean_B", "")), parse_float(row.get("Mean_C", ""))
            if mb is not None and mc is not None:
                diff = mc - mb
                if abs(diff) < 0.25:
                    trend = "valores muy parecidos"
                elif diff > 0:
                    trend = f"avatar un poco más alto (+{diff:.2f})"
                else:
                    trend = f"chat un poco más alto ({diff:.2f})"
                lines.append(f"  {module}: B={mb:.2f}, C={mc:.2f} ({trend}).")
    if bundle.rq2_forms_mecue_ii_group:
        row = bundle.rq2_forms_mecue_ii_group[0]
        mean_ii = parse_float(row.get("Mean_C", ""))
        if mean_ii is not None:
            lines.append(
                f"El módulo II (solo en C) tuvo media {mean_ii:.2f}; mide estética y cercanía "
                "del personaje y no se compara con B."
            )
    lines.append(
        f"meCUE complementa RQ1 y las métricas automáticas del chat "
        f"({figs(FIG_CHAT_HELPSCORE, FIG_CHAT_LEAKS)})."
    )
    return lines


def _interpret_time(bundle: AnalysisBundle) -> list[str]:
    means = _means_from_group(bundle.items_time_by_condition, "MeanTimeSeconds")
    lines = [
        f"La {fig(FIG_TIME)} muestra cuántos segundos tardó el grupo, en promedio, "
        "entre ver cada pregunta y pulsar Entregar en Unity (promedio de los seis ítems). "
        "No incluye todo el tiempo escribiendo en el chat fuera de esa pantalla.",
        "Complementa RQ1: dos condiciones pueden acertar parecido pero a ritmos distintos.",
    ]
    if means:
        for cond in CONDITION_ORDER:
            if cond in means:
                lines.append(
                    f"{CONDITION_LABELS.get(cond, cond)}: {means[cond]:.1f} s por ítem."
                )
        b, c = means.get("B"), means.get("C")
        if b is not None and c is not None and abs(b - c) < 5:
            lines.append(
                "B y C tuvieron tiempos parecidos: el avatar no cambió mucho la velocidad "
                "de respuesta en el simulador."
            )
    lines.append(
        f"Tiempo corto no siempre es mejor ni tiempo largo peor; hay que cruzarlo con "
        f"la {fig(FIG_RQ1_PRECISION)}."
    )
    return lines


def _interpret_rq3(bundle: AnalysisBundle) -> list[str]:
    means = _means_from_group(bundle.rq3_group, "MeanCalibrationGap")
    lines = [
        f"La {fig(FIG_RQ3_GAP)} muestra la brecha media de calibración por condición. "
        "Se calcula restando a la confianza normalizada (0–1) la proporción de aciertos. "
        "Cerca de cero = alineación; positivo = más confianza que aciertos; negativo = al revés.",
        "RQ3 pregunta si el avatar hace que la gente se sienta más segura de lo que "
        "realmente acierta (sobreconfianza).",
    ]
    if means:
        for cond in CONDITION_ORDER:
            if cond in means:
                gap = means[cond]
                if gap > 0.05:
                    tone = "algo más confianza que aciertos"
                elif gap < -0.05:
                    tone = "más aciertos que confianza declarada"
                else:
                    tone = "confianza y aciertos bastante alineados"
                lines.append(
                    f"{CONDITION_LABELS.get(cond, cond)}: brecha {gap:.3f} ({tone})."
                )
        if means.get("C", -99) > means.get("B", -99) and means.get("C", -99) > means.get("A", -99):
            lines.append(
                "La brecha más alta en C encaja con H3: el avatar puede inflar la sensación "
                "de dominio sin subir los aciertos al mismo ritmo."
            )
    lines.append(
        "Es un promedio del grupo, no un juicio sobre cada persona. "
        f"El detalle por ítem está en las {figs(FIG_RQ3_GAP_BY_ITEM, FIG_RQ3_CURVE_BY_ITEM)}."
    )
    return lines


def _interpret_rq3_fig3b(bundle: AnalysisBundle) -> list[str]:
    rows = bundle.rq3_calibration_by_item
    if not rows:
        return []

    lines = [
        f"La {fig(FIG_RQ3_GAP_BY_ITEM)} desglosa la brecha por pregunta (Q1–Q6). "
        f"A diferencia de la {fig(FIG_RQ3_GAP)}, aquí no se promedia todo el bloque.",
        "Sirve para ver si el desajuste confianza–aciertos se concentra en ítems concretos "
        "del escenario de tránsito.",
    ]
    for cond in CONDITION_ORDER:
        item_rows = [r for r in rows if (r.get("Condition") or "").strip() == cond]
        gaps = [parse_float(r.get("CalibrationGap", "")) for r in item_rows]
        gaps = [g for g in gaps if g is not None]
        if not gaps:
            continue
        worst = max(item_rows, key=lambda r: parse_float(r.get("CalibrationGap", "")) or 0.0)
        q = worst.get("QuestionNumber", "?")
        gap_w = parse_float(worst.get("CalibrationGap", "")) or 0.0
        lines.append(
            f"{CONDITION_LABELS.get(cond, cond)}: brecha media por ítem "
            f"{sum(gaps) / len(gaps):.3f}; el mayor desajuste fue en Q{q} ({gap_w:.3f})."
        )
    return lines


def _interpret_rq3_fig3c(bundle: AnalysisBundle) -> list[str]:
    rows = bundle.rq3_calibration_by_item
    if not rows:
        return []

    return [
        f"La {fig(FIG_RQ3_CURVE_BY_ITEM)} es la curva de calibración ítem a ítem. "
        "Cada punto es una pregunta en una condición (A gris, B azul, C morado): "
        "eje X = confianza normalizada media; eje Y = proporción de aciertos. "
        "La diagonal punteada marca calibración perfecta.",
        "Los puntos por encima de la diagonal tienen más confianza que aciertos; "
        "los de abajo, más aciertos que confianza. Si los puntos morados (C) quedan "
        "sistemáticamente arriba de la diagonal respecto a A y B, refuerza H3.",
        f"Conviene leerla junto con la {fig(FIG_RQ3_GAP)} y la {fig(FIG_RQ3_GAP_BY_ITEM)}: "
        "una resume en un número, la otra en barras por Q, esta en la relación geométrica.",
    ]


def _interpret_agente_helpscore(bundle: AnalysisBundle) -> list[str]:
    help_b, help_c = _chat_metric(bundle.chat_group, "HelpScore")
    lines = [
        f"La {fig(FIG_CHAT_HELPSCORE)} compara el HelpScore medio en B y C (0–100). "
        "Es una puntuación automática que resume si el agente orientó sin revelar "
        "la respuesta correcta.",
        f"No es lo mismo que la precisión en el simulador ({fig(FIG_RQ1_PRECISION)}): "
        "aquí se evalúa la calidad pedagógica del diálogo.",
    ]
    if help_b is not None and help_c is not None:
        lines.append(f"En la muestra: B={help_b:.1f}, C={help_c:.1f}.")
        if _approx_equal(help_b, help_c, 5):
            lines.append(
                "Los valores son muy parecidos; el mismo LLM alimenta B y C, "
                "así que la diferencia principal es la presentación."
            )
    lines.append(
        "HelpScore es heurístico (no revisé cada turno a mano). Sirve para comparar "
        "condiciones en el estudio, no como certificación del agente."
    )
    return lines


def _interpret_agente_exchanges(bundle: AnalysisBundle) -> list[str]:
    ex_b, ex_c = _chat_metric(bundle.chat_group, "ChatExchanges")
    lines = [
        f"La {fig(FIG_CHAT_EXCHANGES)} muestra cuántos intercambios de chat hubo en promedio "
        "por pregunta del simulador (ida y vuelta usuario–agente).",
        "Indica cómo usaron el agente: más mensajes suelen reflejar más dudas o "
        "conversaciones más largas.",
    ]
    if ex_b is not None and ex_c is not None:
        lines.append(f"Medias: B={ex_b:.1f}, C={ex_c:.1f} intercambios por pregunta.")
    lines.append(
        f"No hay un número «ideal»; depende del diseño. Parte del chat ocurre fuera "
        f"de la pantalla del ítem, así que conviene cruzarlo con la {fig(FIG_TIME)}."
    )
    return lines


def _interpret_agente_leaks(bundle: AnalysisBundle) -> list[str]:
    leak_b, leak_c = _chat_metric(bundle.chat_group, "ModelLeaks")
    eng_b, eng_c = _chat_metric(bundle.chat_group, "Engagement")
    lines = [
        f"La {fig(FIG_CHAT_LEAKS)} cuenta leaks del modelo por participante en B y C. "
        "Un leak es cuando el agente pudo filtrar la letra correcta o una pista demasiado directa.",
        f"Si hay muchos leaks, la {fig(FIG_RQ1_PRECISION)} deja de medir aprendizaje. "
        "El diseño experimental fija metas de pocos leaks por cada diez participantes.",
    ]
    if leak_b is not None and leak_c is not None:
        lines.append(f"Medias: B={leak_b:.2f}, C={leak_c:.2f} leaks por participante.")
    if eng_b is not None and eng_c is not None:
        lines.append(
            f"Engagement (preguntas en tema): B={eng_b:.1f}, C={eng_c:.1f}. "
            "No tiene figura propia, pero indica si el chat se usó para aprender o para desviarse."
        )
    lines.append(
        f"C puede mejorar la experiencia ({fig(FIG_CHAT_HELPSCORE)}, meCUE) y aun así "
        f"mantener la precisión parecida a B; lo crítico es que los leaks sigan bajos."
    )
    return lines


def _interpret_agente(bundle: AnalysisBundle) -> list[str]:
    """Compatibilidad: devuelve la interpretación integrada (última figura de agente)."""
    return _interpret_agente_leaks(bundle)


def _interpret_viabilidad(bundle: AnalysisBundle) -> list[str]:
    integrity = bundle.integrity_map()
    if not integrity:
        return ["No hay métricas de viabilidad registradas."]

    lines = [
        "Esta sección resume si el registro automático y los servicios externos "
        "funcionaron lo bastante bien como para confiar en los datos.",
    ]
    valid = integrity.get("ValidPct", "n/a")
    lines.append(
        f"Integridad CSV ({valid} % de filas válidas): proporción de respuestas guardadas "
        "sin errores de formato. Meta del estudio: ≥ 95 %."
    )
    gemini_ok = integrity.get("GeminiPasses90Pct") == "1"
    lines.append(
        "Latencia Gemini: tiempo de respuesta del chat. Meta: al menos 90 % de turnos ≤ 5 s. "
        + ("Se cumplió en el estudio." if gemini_ok else "No se cumplió; revisar red o API.")
    )
    tts_ok = integrity.get("TtsPasses85Pct") == "1"
    lines.append(
        "TTS (condición C): porcentaje de síntesis de voz exitosa. Meta: ≥ 85 %. "
        + ("Se cumplió." if tts_ok else "No se cumplió; revisar Azure TTS.")
    )
    leaks = integrity.get("TotalModelLeaks", "0")
    lines.append(
        f"Filtraciones del modelo ({leaks} en total): respuestas del agente que pudieron "
        "revelar la opción correcta. Menos es mejor; influye en la validez interna."
    )
    return lines


@dataclass
class InformeNarrative:
    sections: dict[str, list[str]] = field(default_factory=dict)
    inference_log: list[str] = field(default_factory=list)

    def text(self, key: str) -> str:
        return "\n".join(self.sections.get(key, []))


def _friedman_matrix_from_participant(
    rows: list[dict[str, str]],
    cols: tuple[str, str, str],
    item_cols: tuple[str, str, str] | None = None,
    scale: float = 1.0,
    min_items: int = ITEMS_PER_BLOCK,
) -> list[tuple[float, float, float]]:
    matrix: list[tuple[float, float, float]] = []
    for row in rows:
        if item_cols:
            if not all(
                (row.get(c) or "").strip().isdigit() and int(row[c]) >= min_items
                for c in item_cols
            ):
                continue
        vals = [parse_float(row.get(c, "")) for c in cols]
        if any(v is None for v in vals):
            continue
        matrix.append((vals[0] * scale, vals[1] * scale, vals[2] * scale))  # type: ignore[misc]
    return matrix


def _master_to_aggregated(master: list[dict[str, str]]) -> dict:
    aggregated: dict = {}
    for row in master:
        key = f"{row.get('ParticipantCode', '')}_{row.get('SessionID', '')}"
        aggregated[key] = {}
        for cond in CONDITION_ORDER:
            acc = parse_float(row.get(f"AccuracyPct_{cond}", ""))
            conf = parse_float(row.get(f"MeanConfidence_{cond}", ""))
            gap = parse_float(row.get(f"CalibrationGap_{cond}", ""))
            items = (row.get(f"Items_{cond}") or "").strip()
            if not items.isdigit() or int(items) == 0:
                continue
            if acc is not None and conf is not None and gap is not None:
                aggregated[key][cond] = {
                    "accuracy": acc / 100.0,
                    "mean_confidence": conf,
                    "calibration_gap": gap,
                    "n_items": int(items),
                }
    return aggregated


def _count_values(rows: list[dict[str, str]], key: str) -> dict[str, int]:
    counts: dict[str, int] = {}
    for row in rows:
        value = (row.get(key) or "").strip()
        if not value:
            continue
        counts[value] = counts.get(value, 0) + 1
    return counts


def _format_distribution(counts: dict[str, int], total: int) -> str:
    if not counts or total <= 0:
        return "sin datos"
    parts = [
        f"{label} (n={count}, {100.0 * count / total:.0f}%)"
        for label, count in sorted(counts.items(), key=lambda item: (-item[1], item[0]))
    ]
    return "; ".join(parts)


def _interpret_perfil_figure(perfil: list[dict[str, str]]) -> list[str]:
    if not perfil:
        return []

    n = len(perfil)
    age_counts = _count_values(perfil, "AgeRange")
    assistant_counts = _count_values(perfil, "AssistantFrequency")
    avatar_counts = _count_values(perfil, "AvatarExperience")

    top_age = max(age_counts, key=age_counts.get) if age_counts else "n/a"
    top_age_n = age_counts.get(top_age, 0)

    never_avatar = avatar_counts.get("Nunca", 0)
    freq_assistant = sum(
        assistant_counts.get(k, 0)
        for k in ("Casi todos los días", "Varias veces al día")
    )

    lines = [
        f"La {fig(FIG_PERFIL)} resume el Formulario 0: edad, uso de asistentes digitales "
        f"y experiencia con avatares ({n} participantes). No compara condiciones A/B/C; "
        "solo describe quiénes entraron al estudio.",
        f"El rango de edad más frecuente fue «{top_age}» ({top_age_n}/{n}). "
        f"{freq_assistant}/{n} usan asistentes casi a diario; {never_avatar}/{n} "
        "nunca habían usado avatares o agentes visuales.",
        "Con este tamaño de muestra no hice análisis por subgrupos; la figura sirve "
        f"para contextualizar los resultados de RQ1–RQ3, no para explicarlos por sí sola.",
    ]
    return lines


def build_perfil_narrative(perfil: list[dict[str, str]]) -> list[str]:
    if not perfil:
        return [
            "No se encontró exportación del Form 0 (perfil) en Forms data/.",
        ]

    n = len(perfil)
    lines = [
        f"Al inicio de la sesión cada participante completó un formulario de perfil en Google Forms "
        f"({n} registros en el estudio).",
    ]
    for label, key in (
        ("Rango de edad", "AgeRange"),
        ("Nivel educativo", "Education"),
        ("Uso de asistentes digitales", "AssistantFrequency"),
        ("Experiencia con avatares/agentes", "AvatarExperience"),
    ):
        dist = _format_distribution(_count_values(perfil, key), n)
        lines.append(f"{label}: {dist}.")

    with_avatar_exp = sum(
        1
        for row in perfil
        if (row.get("AvatarExperience") or "").strip()
        not in ("", "Nunca")
    )
    frequent_assistant = sum(
        1
        for row in perfil
        if (row.get("AssistantFrequency") or "").strip()
        in ("Casi todos los días", "Varias veces al día")
    )
    lines.append(
        f"Participantes con experiencia previa en avatares/agentes (≠ «Nunca»): "
        f"{with_avatar_exp}/{n} ({100.0 * with_avatar_exp / n:.0f}%)."
    )
    lines.append(
        f"Uso frecuente de asistentes digitales (diario o casi diario): "
        f"{frequent_assistant}/{n} ({100.0 * frequent_assistant / n:.0f}%)."
    )
    lines.append(
        "Estas variables no entran en Friedman ni Wilcoxon; solo caracterizan la muestra."
    )
    return lines


def build_narrative(bundle: AnalysisBundle) -> InformeNarrative:
    narrative = InformeNarrative()
    log: list[str] = []

    def emit(text: str = "") -> None:
        log.append(text)

    n_canon = len(bundle.canonical_sessions)
    n_complete = len(bundle.complete_canonical)
    pct = 100.0 * n_complete / n_canon if n_canon else 0.0

    narrative.sections["resumen"] = build_resumen(
        bundle,
        n_canon=n_canon,
        n_complete=n_complete,
        acc=_means_from_group(bundle.rq1_group, "MeanAccuracyPct"),
        conf=_means_from_group(bundle.rq2_group, "MeanConfidence"),
        gap=_means_from_group(bundle.rq3_group, "MeanCalibrationGap"),
    )

    narrative.sections["muestra"] = [
        f"Se identificaron {len(bundle.sessions)} carpetas de sesión bajo CSV data/.",
        f"Para el análisis se utilizó una sesión canónica por participante (n={n_canon}).",
        f"Completitud A+B+C: {n_complete}/{n_canon} participantes ({pct:.1f}%).",
    ]
    narrative.sections["flujo"] = build_flujo_narrative(bundle)
    narrative.sections["metodologia"] = build_metodologia(
        bundle, n_canon=n_canon, n_complete=n_complete
    )
    narrative.sections["perfil"] = build_perfil_narrative(bundle.perfil)
    narrative.sections["perfil_figura_interpretacion"] = _interpret_perfil_figure(bundle.perfil)

    # RQ1
    rq1_lines = [
        "RQ1 compara el porcentaje de aciertos entre A, B y C.",
        "La Tabla 1 resume la precisión media del grupo en cada bloque (6 ítems).",
    ]
    for row in bundle.rq1_group:
        cond = row.get("Condition", "")
        rq1_lines.append(
            f"{CONDITION_LABELS.get(cond, cond)}: media {row.get('MeanAccuracyPct', '')} % "
            f"(n={row.get('NParticipants', '')} participantes)."
        )
    matrix = _friedman_matrix_from_participant(
        bundle.rq1_participant,
        ("AccuracyPct_A", "AccuracyPct_B", "AccuracyPct_C"),
        ("Items_A", "Items_B", "Items_C"),
        scale=1 / 100.0,
    )
    emit("\n[RQ1]")
    print_inference_block(emit, "Precisión", matrix)
    narrative.sections["rq1"] = rq1_lines
    narrative.sections["rq1_interpretacion"] = _interpret_rq1(bundle)

    # RQ2 Unity
    rq2_lines = [
        "RQ2 mira si cambia la confianza (1–7) que cada persona marcó en Unity tras cada respuesta.",
        "La Tabla 2 trae las medias por condición.",
    ]
    for row in bundle.rq2_group:
        cond = row.get("Condition", "")
        rq2_lines.append(
            f"{CONDITION_LABELS.get(cond, cond)}: media {row.get('MeanConfidence', '')} "
            f"(n={row.get('NParticipants', '')})."
        )
    matrix2 = _friedman_matrix_from_participant(
        bundle.rq2_participant,
        ("MeanConfidence_A", "MeanConfidence_B", "MeanConfidence_C"),
        ("Items_A", "Items_B", "Items_C"),
    )
    emit("\n[RQ2 Unity]")
    print_inference_block(emit, "Confianza Unity", matrix2)
    narrative.sections["rq2_unity"] = rq2_lines
    narrative.sections["rq2_interpretacion"] = _interpret_rq2(bundle)

    # RQ2 Forms
    if bundle.rq2_forms_tlx:
        forms_lines = [
            "Los formularios de Google complementan RQ2: RAW-TLX en los tres bloques "
            "y meCUE en B y C.",
        ]
        for row in bundle.rq2_forms_tlx_group:
            cond = row.get("Condition", "")
            forms_lines.append(
                f"RAW-TLX, {CONDITION_LABELS.get(cond, cond)}: media {row.get('MeanRAWTLX', '')} "
                f"(n={row.get('NParticipants', '')})."
            )
        tlx_matrix: list[tuple[float, float, float]] = []
        for row in bundle.rq2_forms_tlx:
            a, b, c = (
                parse_float(row.get("RAW_TLX_A", "")),
                parse_float(row.get("RAW_TLX_B", "")),
                parse_float(row.get("RAW_TLX_C", "")),
            )
            if a is not None and b is not None and c is not None:
                tlx_matrix.append((a, b, c))
        emit("\n[RQ2 Forms RAW-TLX]")
        print_inference_block(emit, "RAW-TLX", tlx_matrix)
        if bundle.rq2_forms_mecue_group:
            forms_lines.append("meCUE, medias grupales B vs C:")
            for row in bundle.rq2_forms_mecue_group:
                forms_lines.append(
                    f"  {row.get('ModuleLabel', row.get('Module', ''))}: "
                    f"B={row.get('Mean_B', '')}, C={row.get('Mean_C', '')}."
                )
        if bundle.rq2_forms_mecue:
            emit("\n[RQ2 Forms meCUE B vs C]")
            print_mecue_b_vs_c_inference(emit, bundle.rq2_forms_mecue)
        narrative.sections["rq2_forms"] = forms_lines
        narrative.sections["forms_tlx_interpretacion"] = _interpret_forms_tlx(bundle)
        narrative.sections["mecue_interpretacion"] = _interpret_mecue(bundle)
        narrative.sections["mecue_ii"] = build_mecue_ii_section(bundle)
    else:
        narrative.sections["rq2_forms"] = ["No se incluyeron exportaciones de Google Forms en este análisis."]
        narrative.sections["forms_tlx_interpretacion"] = []
        narrative.sections["mecue_interpretacion"] = []

    # Tiempo de respuesta
    if bundle.items_time_by_condition:
        time_lines = [
            "Métrica complementaria: tiempo medio por ítem (`TimeSpent` en Unity), agregado por condición.",
        ]
        for row in bundle.items_time_by_condition:
            cond = row.get("Condition", "")
            time_lines.append(
                f"{CONDITION_LABELS.get(cond, cond)}: {row.get('MeanTimeSeconds', '')} s "
                f"(promedio de {row.get('NItems', '')} ítems)."
            )
        narrative.sections["tiempo"] = time_lines
        narrative.sections["tiempo_interpretacion"] = _interpret_time(bundle)
    else:
        narrative.sections["tiempo"] = []
        narrative.sections["tiempo_interpretacion"] = []

    # RQ3
    rq3_lines = [
        "RQ3 revisa si la confianza declarada coincide con los aciertos reales.",
        "La brecha se calcula como confianza normalizada ((valor−1)/6) menos la proporción de aciertos "
        "del bloque. La Tabla 3 muestra la media por condición.",
    ]
    for row in bundle.rq3_group:
        cond = row.get("Condition", "")
        rq3_lines.append(
            f"{CONDITION_LABELS.get(cond, cond)}: brecha media {row.get('MeanCalibrationGap', '')} "
            f"(n={row.get('NParticipants', '')})."
        )
    matrix3 = _friedman_matrix_from_participant(
        bundle.rq3_participant,
        ("CalibrationGap_A", "CalibrationGap_B", "CalibrationGap_C"),
        ("Items_A", "Items_B", "Items_C"),
    )
    emit("\n[RQ3]")
    print_inference_block(emit, "Brecha calibración", matrix3)
    narrative.sections["rq3"] = rq3_lines
    narrative.sections["rq3_interpretacion"] = _interpret_rq3(bundle)
    if bundle.rq3_calibration_by_item:
        narrative.sections["rq3_items"] = [
            f"Además del promedio del bloque ({fig(FIG_RQ3_GAP)}), el análisis desglosa la calibración "
            f"pregunta por pregunta (Q1–Q6). La Tabla 3b y las {figs(FIG_RQ3_GAP_BY_ITEM, FIG_RQ3_CURVE_BY_ITEM)} permiten ver "
            "si el desajuste confianza–aciertos se concentra en ítems concretos del escenario.",
        ]
        narrative.sections["rq3_fig3b_interpretacion"] = _interpret_rq3_fig3b(bundle)
        narrative.sections["rq3_fig3c_interpretacion"] = _interpret_rq3_fig3c(bundle)
    else:
        narrative.sections["rq3_items"] = []
        narrative.sections["rq3_fig3b_interpretacion"] = []
        narrative.sections["rq3_fig3c_interpretacion"] = []

    # Hipótesis
    aggregated = _master_to_aggregated(bundle.master)
    acc_means = _means_from_group(bundle.rq1_group, "MeanAccuracyPct")
    conf_means = _means_from_group(bundle.rq2_group, "MeanConfidence")
    gap_means = _means_from_group(bundle.rq3_group, "MeanCalibrationGap")
    emit("\n[Hipótesis]")
    if aggregated:
        print_hypothesis_summary(emit, aggregated)
    narrative.sections["hipotesis"] = build_hipotesis_section(acc_means, conf_means, gap_means)

    # Agente
    if bundle.chat_group:
        agent_lines = ["Comparación del agente entre B (texto) y C (avatar + voz):"]
        for row in bundle.chat_group:
            agent_lines.append(
                f"{row.get('Metric', '')}: media B={row.get('Mean_B', '')} (n={row.get('N_B', '')}), "
                f"media C={row.get('Mean_C', '')} (n={row.get('N_C', '')})."
            )
        if bundle.chat_participant:
            b_help: dict[str, float] = {}
            c_help: dict[str, float] = {}
            for row in bundle.chat_participant:
                code = row.get("ParticipantCode", "")
                hb, hc = parse_float(row.get("HelpScore_B", "")), parse_float(row.get("HelpScore_C", ""))
                if code and hb is not None and hc is not None:
                    b_help[code] = hb
                    c_help[code] = hc
            emit("\n[Agente HelpScore B vs C]")
            print_pairwise_result(emit, "Wilcoxon HelpScore", wilcoxon_paired(b_help, c_help, "B", "C"))
        narrative.sections["agente"] = agent_lines
        narrative.sections["agente_fig4_interpretacion"] = _interpret_agente_helpscore(bundle)
        narrative.sections["agente_fig5_interpretacion"] = _interpret_agente_exchanges(bundle)
        narrative.sections["agente_fig6_interpretacion"] = _interpret_agente_leaks(bundle)
    else:
        narrative.sections["agente"] = ["No hay datos de chat agregados para comparar B y C."]
        narrative.sections["agente_fig4_interpretacion"] = []
        narrative.sections["agente_fig5_interpretacion"] = []
        narrative.sections["agente_fig6_interpretacion"] = []

    # Viabilidad
    integrity = bundle.integrity_map()
    viab = [
        "Criterios de viabilidad del estudio (Entregable 2):",
    ]
    if integrity:
        viab.extend(
            [
                f"Integridad CSV válida: {integrity.get('ValidPct', 'n/a')}% "
                f"({'cumple' if integrity.get('Passes95PctRule') == '1' else 'no cumple'} meta 95%).",
                f"Latencia Gemini ≤5 s en ≥90% turnos: "
                f"{'cumple' if integrity.get('GeminiPasses90Pct') == '1' else 'no cumple'}.",
                f"TTS condición C ≥85% éxito: "
                f"{'cumple' if integrity.get('TtsPasses85Pct') == '1' else 'no cumple'}.",
                f"Filtraciones modelo (leaks): {integrity.get('TotalModelLeaks', '0')} "
                f"({integrity.get('LeaksPer10Participants', 'n/a')} por cada 10 participantes).",
            ]
        )
    narrative.sections["viabilidad"] = viab + _interpret_viabilidad(bundle)

    narrative.sections["respuestas_rq"] = build_respuestas_rq(
        bundle, acc_means, conf_means, gap_means
    )
    narrative.sections["discusion"] = build_discusion(
        bundle, acc_means, conf_means, gap_means
    )
    narrative.sections["conclusiones"] = build_conclusiones(
        bundle, acc_means, conf_means, gap_means
    )
    narrative.sections["referencias"] = REFERENCES

    narrative.inference_log = log
    return narrative
