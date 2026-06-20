#!/usr/bin/env python3
"""Síntesis integrada: problema, RQs, hipótesis, inferencia y conclusiones."""

from __future__ import annotations

from informe_data_local import AnalysisBundle, parse_float

CONDITION_ORDER = ("A", "B", "C")
CONDITION_LABELS = {
    "A": "A (sin agente)",
    "B": "B (chat texto)",
    "C": "C (avatar y voz)",
}

PROBLEM_TITLE = (
    "Efecto de la visibilidad de un agente virtual en el desempeño y la confianza "
    "del usuario en tareas de decisión basadas en reglas"
)


def _means_from_group(rows: list[dict[str, str]], value_key: str) -> dict[str, float]:
    out: dict[str, float] = {}
    for row in rows:
        cond = (row.get("Condition") or "").strip()
        val = parse_float(row.get(value_key, ""))
        if cond and val is not None:
            out[cond] = val
    return out


def _n_from_group(rows: list[dict[str, str]], condition: str) -> int | None:
    for row in rows:
        if (row.get("Condition") or "").strip() == condition:
            text = (row.get("NParticipants") or "").strip()
            if text.isdigit():
                return int(text)
    return None


def _approx_equal(a: float, b: float, tol: float) -> bool:
    return abs(a - b) <= tol


def _compare_order(values: dict[str, float], order: tuple[str, ...]) -> bool:
    if not all(c in values for c in order):
        return False
    for left, right in zip(order, order[1:]):
        if values[left] <= values[right]:
            return False
    return True


def _format_p(p: float | None) -> str:
    if p is None:
        return "n/d"
    if p < 0.001:
        return "< 0,001"
    return f"{p:.3f}".replace(".", ",")


def _find_omnibus(
    rows: list[dict[str, str]],
    analysis_id: str,
    metric: str | None = None,
) -> dict[str, str] | None:
    for row in rows:
        if (row.get("AnalysisId") or "").strip() != analysis_id:
            continue
        if metric is not None and (row.get("Metric") or "").strip() != metric:
            continue
        return row
    return None


def _pairwise_rows(
    rows: list[dict[str, str]],
    analysis_id: str,
    metric: str | None = None,
) -> list[dict[str, str]]:
    out: list[dict[str, str]] = []
    for row in rows:
        if (row.get("AnalysisId") or "").strip() != analysis_id:
            continue
        if metric is not None and (row.get("Metric") or "").strip() != metric:
            continue
        out.append(row)
    return out


def _friedman_sentence(
    row: dict[str, str] | None,
    *,
    label: str,
    effect_rows: list[dict[str, str]] | None = None,
    analysis_id: str | None = None,
    metric: str | None = None,
) -> str:
    if not row or (row.get("Applied") or "").strip() != "1":
        return (
            f"Para {label}, la prueba de Friedman no pudo aplicarse "
            f"(muestra insuficiente o falta scipy; ver anexo)."
        )
    p = parse_float(row.get("PValue", ""))
    stat = parse_float(row.get("Statistic", ""))
    n = (row.get("N") or "?").strip()
    w = parse_float(row.get("KendallW", ""))
    if w is None and effect_rows and analysis_id and metric:
        for er in effect_rows:
            if er.get("AnalysisId") == analysis_id and er.get("Metric") == metric:
                w = parse_float(er.get("KendallW", ""))
                break
    if p is None or stat is None:
        return f"Para {label}, no hay resultado inferencial válido."
    w_text = f", Kendall W = {w:.3f}" if w is not None else ""
    if p < 0.05:
        return (
            f"Para {label}, Friedman detectó diferencias entre A, B y C "
            f"(χ² = {stat:.3f}, p = {_format_p(p)}, n = {n}{w_text})."
        )
    return (
        f"Para {label}, Friedman no mostró diferencias globales significativas "
        f"(χ² = {stat:.3f}, p = {_format_p(p)}, n = {n}{w_text})."
    )


def _pairwise_sentence(rows: list[dict[str, str]]) -> str:
    if not rows:
        return "No hay contrastes pareados disponibles."
    significant = [
        r
        for r in rows
        if (r.get("SignificantHolm") or r.get("Significant") or "").strip() == "1"
    ]
    if not significant:
        return (
            "Ningún contraste pareado (A–B, A–C o B–C) alcanzó p < 0,05 tras corrección de Holm, "
            "lo cual es habitual con muestras tan pequeñas."
        )
    parts = []
    for r in significant:
        left = r.get("Left", "")
        right = r.get("Right", "")
        p_adj = parse_float(
            r.get("PAdjustedHolm") or r.get("PAdjustedBonferroni") or r.get("PAdjusted") or ""
        )
        parts.append(f"{left} vs {right} (p_adj Holm = {_format_p(p_adj)})")
    return "Contrastes pareados significativos (Holm): " + "; ".join(parts) + "."


def _find_contrast(
    rows: list[dict[str, str]],
    contrast_id: str,
) -> dict[str, str] | None:
    for row in rows:
        if (row.get("ContrastId") or "").strip() == contrast_id:
            return row
    return None


def _contrast_sentence(row: dict[str, str] | None, *, fallback: str) -> str:
    if not row:
        return fallback
    left = row.get("Left", "")
    right = row.get("Right", "")
    diff = row.get("MeanDiff", "")
    lo = row.get("CI_Low", "")
    hi = row.get("CI_High", "")
    units = row.get("Units", "")
    p = parse_float(row.get("PValue", ""))
    n = row.get("N", "?")
    unit_label = " puntos porcentuales" if units == "pp" else ""
    sig = (row.get("Significant") or "").strip() == "1"
    sig_text = "significativo" if sig else "no significativo"
    return (
        f"Contraste dirigido {left} vs {right}: Δ = {diff}{unit_label} "
        f"(IC 95 % bootstrap [{lo}, {hi}]), Wilcoxon p = {_format_p(p)} ({sig_text}, n = {n})."
    )


def _bootstrap_bc_sentence(
    rows: list[dict[str, str]],
    analysis_id: str,
    metric: str,
) -> str:
    for row in rows:
        if row.get("AnalysisId") != analysis_id or row.get("Metric") != metric:
            continue
        if row.get("Left") == "B" and row.get("Right") == "C":
            units = row.get("Units", "")
            unit_label = " pp" if units == "pp" else ""
            return (
                f"Diferencia B–C: {row.get('MeanDiff', '')}{unit_label} "
                f"(IC 95 % [{row.get('CI_Low', '')}, {row.get('CI_High', '')}])."
            )
    return ""


def _verdict_h1(acc: dict[str, float]) -> str:
    if not all(k in acc for k in CONDITION_ORDER):
        return "H1 (precisión C ≈ B > A): no hay datos suficientes."
    if acc["A"] < acc["B"] and acc["A"] < acc["C"] and _approx_equal(acc["B"], acc["C"], 4.0):
        return (
            "H1: en líneas generales se cumple. A quedó por debajo de B y C "
            f"({acc['B']:.1f} % y {acc['C']:.1f} %, respectivamente), y la diferencia entre "
            "chat y avatar fue pequeña."
        )
    if acc["A"] < min(acc["B"], acc["C"]):
        return (
            f"H1: se cumple en parte. A fue la condición con menos aciertos, pero B ({acc['B']:.1f} %) "
            f"y C ({acc['C']:.1f} %) no quedaron tan parecidas como se esperaba."
        )
    return "H1: con estos datos el orden esperado no se observa de forma clara."


def _verdict_h2(conf: dict[str, float]) -> str:
    if not all(k in conf for k in CONDITION_ORDER):
        return "H2 (confianza C > B > A): no hay datos suficientes."
    if _compare_order(conf, ("A", "B", "C")):
        return (
            f"H2: se cumple. La confianza subió de A ({conf['A']:.2f}) a B ({conf['B']:.2f}) "
            f"y a C ({conf['C']:.2f})."
        )
    if conf.get("C", 0) > conf.get("A", 0) and conf.get("B", 0) > conf.get("A", 0):
        return (
            "H2: se cumple en parte. B y C superaron a A, pero el orden entre B y C no fue tan marcado."
        )
    return "H2: el patrón C > B > A no aparece con claridad en los resultados del estudio."


def _verdict_h3(gap: dict[str, float]) -> str:
    if not all(k in gap for k in CONDITION_ORDER):
        return "H3 (mayor brecha en C): no hay datos suficientes."
    if gap["C"] >= gap["B"] and gap["C"] >= gap["A"]:
        return (
            f"H3: se cumple. La brecha más alta fue en C ({gap['C']:.3f}), frente a "
            f"A ({gap['A']:.3f}) y B ({gap['B']:.3f})."
        )
    return (
        "H3: no se cumple de forma nítida; conviene revisar el desglose por ítem y la variación entre participantes."
    )


def _chat_metric(bundle: AnalysisBundle, metric: str) -> tuple[float | None, float | None]:
    for row in bundle.chat_group:
        if (row.get("Metric") or "").strip() == metric:
            return parse_float(row.get("Mean_B", "")), parse_float(row.get("Mean_C", ""))
    return None, None


def _integrity(bundle: AnalysisBundle) -> dict[str, str]:
    return bundle.integrity_map()


def _mecue_summary(bundle: AnalysisBundle) -> str:
    if not bundle.rq2_forms_mecue_group:
        return "No hay medias meCUE agregadas."
    parts: list[str] = []
    labels = {
        "MECUE_I": "utilidad (Mód. I)",
        "MECUE_III": "emociones (Mód. III)",
        "MECUE_IV": "consecuencias (Mód. IV)",
        "MECUE_V": "evaluación global (Mód. V)",
    }
    for row in bundle.rq2_forms_mecue_group:
        module = (row.get("Module") or "").strip()
        mean_b = parse_float(row.get("Mean_B", ""))
        mean_c = parse_float(row.get("Mean_C", ""))
        if mean_b is None and mean_c is None:
            continue
        label = labels.get(module, module)
        parts.append(
            f"{label}: B = {mean_b if mean_b is not None else 'n/d'}, "
            f"C = {mean_c if mean_c is not None else 'n/d'}"
        )
    if not parts:
        return "meCUE sin puntuaciones válidas."
    return "Medias meCUE (B vs C): " + "; ".join(parts) + "."


def _tlx_summary(bundle: AnalysisBundle) -> str:
    means = _means_from_group(bundle.rq2_forms_tlx_group, "MeanRAWTLX")
    if not means:
        return "RAW-TLX no disponible."
    parts = [
        f"{CONDITION_LABELS.get(c, c)} = {means[c]:.2f}"
        for c in CONDITION_ORDER
        if c in means
    ]
    return "Carga percibida (RAW-TLX, 1–7): " + "; ".join(parts) + "."


def build_resumen(
    bundle: AnalysisBundle,
    *,
    n_canon: int,
    n_complete: int,
    acc: dict[str, float],
    conf: dict[str, float],
    gap: dict[str, float],
) -> list[str]:
    pct = 100.0 * n_complete / n_canon if n_canon else 0.0
    lines = [
        "Este informe presenta los resultados del estudio within-subjects realizado con el prototipo PF-3311. "
        "Cada participante pasó por tres condiciones: sin agente (A), chat de texto con Gemini (B) "
        "y avatar con voz (C), resolviendo en cada una seis ítems sobre reglas de tránsito.",
        f"El problema que guía el estudio es: {PROBLEM_TITLE}.",
        f"Se analizaron {n_canon} sesiones canónicas; {n_complete} completaron los tres bloques "
        f"con los 18 ítems esperados ({pct:.1f} % del total canónico).",
    ]
    if acc or conf or gap:
        lines.append("A grandes rasgos, los promedios del subconjunto completo fueron estos:")
    if acc:
        lines.append(
            f"En precisión (RQ1), A alcanzó {acc.get('A', 0):.1f} %, B {acc.get('B', 0):.1f} % "
            f"y C {acc.get('C', 0):.1f} %. Tener agente pareció ayudar respecto a trabajar solo; "
            "entre B y C la diferencia fue menor."
        )
    if conf:
        lines.append(
            f"En confianza (RQ2), las medias fueron A = {conf.get('A', 0):.2f}, "
            f"B = {conf.get('B', 0):.2f} y C = {conf.get('C', 0):.2f}. "
            "La seguridad subjetiva aumentó al incorporar el agente y un poco más con el avatar."
        )
    if gap:
        lines.append(
            f"En calibración (RQ3), las brechas medias fueron A = {gap.get('A', 0):.3f}, "
            f"B = {gap.get('B', 0):.3f} y C = {gap.get('C', 0):.3f}. "
            "El desajuste entre confianza y aciertos fue algo mayor en C."
        )
    integrity = _integrity(bundle)
    if integrity:
        valid = integrity.get("ValidPct", "n/a")
        gemini = "cumple" if integrity.get("GeminiPasses90Pct") == "1" else "no cumple"
        tts = "cumple" if integrity.get("TtsPasses85Pct") == "1" else "no cumple"
        leaks = integrity.get("TotalModelLeaks", "0")
        lines.append(
            f"En viabilidad técnica, la integridad de los CSV fue del {valid} %, "
            f"la latencia de Gemini {gemini} la meta, el TTS {tts} y se registraron {leaks} leaks del modelo."
        )
    lines.append(
        "En las secciones siguientes se detallan tablas, figuras y la lectura de cada gráfico. "
        "Las pruebas estadísticas (Friedman y Wilcoxon) están en el anexo y se usan en las "
        "respuestas a las preguntas de investigación y en las conclusiones."
    )
    return lines


def build_respuestas_rq(
    bundle: AnalysisBundle,
    acc: dict[str, float],
    conf: dict[str, float],
    gap: dict[str, float],
) -> list[str]:
    omni = bundle.inference_omnibus
    pair = bundle.inference_pairwise

    rq1_omni = _find_omnibus(omni, "RQ1", "accuracy")
    rq2_omni = _find_omnibus(omni, "RQ2", "mean_confidence")
    rq3_omni = _find_omnibus(omni, "RQ3", "calibration_gap")
    tlx_omni = _find_omnibus(omni, "RQ2_FORMS", "RAW_TLX")

    lines = [
        "A continuación se responden las preguntas de investigación del Entregable 2. "
        "Se apoya en las medias observadas en la muestra y en pruebas no paramétricas within-subjects. "
        "Con una n reducida, un resultado no significativo no implica que las condiciones sean equivalentes.",
    ]

    if acc:
        diff_bc = abs(acc.get("B", 0) - acc.get("C", 0))
        visibility = (
            "La visibilidad del avatar (C) no separó claramente el desempeño del chat (B)."
            if _approx_equal(acc.get("B", 0), acc.get("C", 0), 4.0)
            else (
                f"C superó a B por {diff_bc:.1f} puntos porcentuales."
                if acc.get("C", 0) > acc.get("B", 0)
                else f"B superó a C por {diff_bc:.1f} puntos porcentuales."
            )
        )
        agent_effect = (
            "Tener agente (B o C) se asoció con más aciertos que A."
            if acc.get("A", 100) < min(acc.get("B", 0), acc.get("C", 0))
            else "A no fue claramente inferior a B/C en los datos del estudio."
        )
        bc_boot = _bootstrap_bc_sentence(bundle.inference_bootstrap, "RQ1", "accuracy")
        vis_contrast = _contrast_sentence(
            _find_contrast(bundle.inference_contrasts, "VIS_B_vs_C"),
            fallback="",
        )
        lines.append(
            f"RQ1 (precisión). Medias: A = {acc['A']:.1f} %, B = {acc['B']:.1f} %, C = {acc['C']:.1f} %. "
            f"{agent_effect} {visibility} "
            f"{_friedman_sentence(rq1_omni, label='precisión', effect_rows=bundle.inference_effect_sizes, analysis_id='RQ1', metric='accuracy')} "
            f"{_pairwise_sentence(_pairwise_rows(pair, 'RQ1', 'accuracy'))} "
            f"{bc_boot} {vis_contrast}".strip()
        )

    if conf:
        lines.append(
            f"RQ2 (confianza en Unity, escala 1–7). Medias: A = {conf['A']:.2f}, B = {conf['B']:.2f}, "
            f"C = {conf['C']:.2f}. La confianza subió al pasar de A a B y de B a C, "
            "lo que encaja con la idea de que la presencia del agente (y más aún el avatar) "
            "influye en cómo de seguro se siente la persona al responder. "
            f"{_friedman_sentence(rq2_omni, label='confianza Unity', effect_rows=bundle.inference_effect_sizes, analysis_id='RQ2', metric='mean_confidence')}. "
            f"{_pairwise_sentence(_pairwise_rows(pair, 'RQ2', 'mean_confidence'))} "
            f"En Forms: {_tlx_summary(bundle)} {_mecue_summary(bundle)}."
        )

    if gap:
        h3_row = _find_contrast(bundle.inference_contrasts, "H3_C_vs_A")
        h3_note = (
            "C tuvo la brecha más alta, lo que apunta a algo de sobreconfianza con el avatar."
            if gap["C"] >= max(gap["A"], gap["B"])
            else "El patrón entre condiciones no fue uniforme."
        )
        lines.append(
            f"RQ3 (calibración). Brechas medias: A = {gap['A']:.3f}, B = {gap['B']:.3f}, "
            f"C = {gap['C']:.3f} (confianza normalizada menos proporción de aciertos; "
            "positivo = más confianza que aciertos). "
            f"{h3_note} "
            f"{_friedman_sentence(rq3_omni, label='brecha de calibración', effect_rows=bundle.inference_effect_sizes, analysis_id='RQ3', metric='calibration_gap')}. "
            f"{_pairwise_sentence(_pairwise_rows(pair, 'RQ3', 'calibration_gap'))} "
            f"{_contrast_sentence(h3_row, fallback='')}"
            "La Tabla 3b y las figuras 8–9 permiten ver si el efecto se concentra en ítems puntuales."
        )

    if tlx_omni:
        lines.append(
            f"Carga de trabajo (RAW-TLX, apoyo a RQ2). {_friedman_sentence(tlx_omni, label='RAW-TLX')}. "
            f"{_pairwise_sentence(_pairwise_rows(pair, 'RQ2_FORMS', 'RAW_TLX'))}"
        )

    mecue_inf = bundle.inference_mecue
    if mecue_inf:
        sig = [r for r in mecue_inf if (r.get("SignificantHolm") or r.get("Significant") or "").strip() == "1"]
        if sig:
            parts = [
                f"{r.get('ModuleLabel', r.get('Module', ''))} (p_adj Holm = {_format_p(parse_float(r.get('PAdjustedHolm') or r.get('PAdjusted') or ''))})"
                for r in sig
            ]
            mecue_line = "meCUE B vs C: diferencias significativas (Holm) en " + "; ".join(parts) + "."
        else:
            mecue_line = (
                "En meCUE (B vs C) ningún módulo alcanzó significación tras Holm; "
                "las medias muestran tendencias, pero el tamaño de muestra es muy reducido."
            )
        lines.append(mecue_line)

    help_row = _find_contrast(bundle.inference_contrasts, "AGENT_HELPSCORE")
    if help_row:
        lines.append(_contrast_sentence(help_row, fallback=""))

    return lines


def build_discusion(
    bundle: AnalysisBundle,
    acc: dict[str, float],
    conf: dict[str, float],
    gap: dict[str, float],
) -> list[str]:
    help_b, help_c = _chat_metric(bundle, "HelpScore")
    leaks_b, leaks_c = _chat_metric(bundle, "ModelLeaks")
    integrity = _integrity(bundle)

    lines = [
        "El estudio pregunta si hacer visible el agente (C) cambia el desempeño y la confianza "
        "frente a no tener agente (A) o usar solo chat (B). En B y C el modelo de lenguaje fue "
        "el mismo; lo que varió fue la forma de presentarlo.",
    ]

    if acc and conf:
        lines.append(
            f"En el estudio, la precisión mejoró al pasar de A ({acc['A']:.1f} %) a B/C "
            f"({acc['B']:.1f} % y {acc['C']:.1f} %), pero B y C quedaron muy cerca "
            f"({abs(acc['B'] - acc['C']):.1f} pp de diferencia). La confianza, en cambio, "
            f"sí subió de forma más ordenada: A ({conf['A']:.2f}), B ({conf['B']:.2f}), "
            f"C ({conf['C']:.2f}). Eso sugiere que el avatar influyó más en la experiencia "
            "subjetiva y en la calibración que en los aciertos como tal, algo que otros trabajos "
            "también reportan al separar confianza de rendimiento (Lee y See, 2004; Zhang et al., 2020)."
        )

    if gap:
        lines.append(
            f"La brecha en C ({gap['C']:.3f}) indica que, en promedio, los participantes "
            "declararon más seguridad de la que sus aciertos respaldaban en ese bloque. "
            "Para un tutor virtual esto importa: alguien puede sentir que ya dominó la regla "
            "cuando todavía se equivoca. No invalida el uso del agente, pero sí obliga a "
            "pensar en retroalimentación más explícita cuando hay avatar."
        )

    if help_b is not None and help_c is not None:
        lines.append(
            f"En calidad del chat, el HelpScore promedio fue B = {help_b:.1f} y C = {help_c:.1f}. "
            "Esa métrica resume si el agente orientó sin filtrar la respuesta correcta. "
            "Si C no queda por debajo de B, el avatar no pareció empeorar la utilidad pedagógica del contenido."
        )
    if leaks_b is not None and leaks_c is not None:
        lines.append(
            f"Se detectaron en promedio {leaks_b:.2f} leaks en B y {leaks_c:.2f} en C por participante. "
            "Cualquier filtración de la respuesta correcta afecta la validez de RQ1 y RQ3; "
            "conviene seguir afinando el prompt."
        )

    lines.extend(
        [
            "RAW-TLX y meCUE aportan contexto sobre carga y experiencia global tras cada bloque; "
            "complementan, pero no sustituyen, la confianza ítem a ítem que registra Unity.",
            "El tiempo de respuesta ayuda a ver si el agente aceleró o alargó la tarea sin que "
            "eso se note solo en la precisión.",
            "En la inferencia, Friedman puede salir significativa con pocos datos mientras los "
            "post hoc corregidos no lo hacen. Por eso prioricé el tamaño del efecto, la dirección "
            "de H1–H3 y la lectura conjunta de tablas y figuras antes de generalizar.",
            "Las limitaciones incluyen una muestra pequeña, seis ítems por bloque, "
            "diseño within-subjects, escenarios ficticios y dependencia de APIs externas. "
            "Los hallazgos responden a las preguntas planteadas en el marco del diseño, "
            "pero no agotan la generalización a otros contextos.",
        ]
    )

    if integrity:
        if integrity.get("TtsPasses85Pct") != "1":
            lines.append(
                "El TTS no alcanzó el 85 % de síntesis exitosas en C. Eso pudo atenuar "
                "la comparación con B y conviene revisar Azure antes de una nueva recolección de datos."
            )
        if integrity.get("Passes95PctRule") == "1":
            lines.append(
                f"El {integrity.get('ValidPct', 'n/a')} % de filas válidas en los CSV da confianza "
                "en que el registro automático funcionó bien."
            )

    return lines


def build_conclusiones(
    bundle: AnalysisBundle,
    acc: dict[str, float],
    conf: dict[str, float],
    gap: dict[str, float],
) -> list[str]:
    n_complete = len(bundle.complete_canonical)
    integrity = _integrity(bundle)

    lines = [
        f"El flujo Unity + Forms + chat/TTS permitió registrar sesiones completas y analizar "
        f"RQ1 a RQ3 con {n_complete} participantes que cerraron los tres bloques (6 ítems cada uno).",
    ]

    if acc:
        lines.append(
            f"En desempeño (RQ1 / H1), el agente se asoció con más aciertos que A "
            f"({acc['A']:.1f} % frente a B = {acc['B']:.1f} % y C = {acc['C']:.1f} %). "
            f"{_verdict_h1(acc)} Pasar de chat a avatar no movió mucho la precisión "
            f"(Δ B–C = {abs(acc['B'] - acc['C']):.1f} pp): la ayuda textual ya explicaba "
            "gran parte del beneficio."
        )

    if conf:
        lines.append(
            f"En confianza (RQ2 / H2), las medias fueron A = {conf['A']:.2f}, B = {conf['B']:.2f} "
            f"y C = {conf['C']:.2f}. {_verdict_h2(conf)} El avatar parece influir en la sensación "
            "de seguridad aunque los aciertos no suban en la misma proporción."
        )

    if gap:
        h3_text = _contrast_sentence(
            _find_contrast(bundle.inference_contrasts, "H3_C_vs_A"),
            fallback="",
        )
        lines.append(
            f"En calibración (RQ3 / H3), la brecha fue mayor en C ({gap['C']:.3f}) que en A "
            f"({gap['A']:.3f}). {_verdict_h3(gap)} {h3_text} "
            "En la práctica, un personaje convincente puede dar sensación de dominio que "
            "no siempre coincide con el rendimiento."
        )

    help_row = _find_contrast(bundle.inference_contrasts, "AGENT_HELPSCORE")
    if help_row:
        lines.append(
            f"Entre B y C, {_contrast_sentence(help_row, fallback='')} "
            "El LLM fue el mismo; lo que cambió fue la presentación."
        )
    else:
        help_b, help_c = _chat_metric(bundle, "HelpScore")
        if help_b is not None and help_c is not None:
            lines.append(
                f"El HelpScore (evaluación automática del chat) fue B = {help_b:.1f} y C = {help_c:.1f}. "
                "La modalidad visual/sonora pareció pesar más en la experiencia que en la "
                "puntuación heurística del texto."
            )

    if integrity:
        tts_ok = integrity.get("TtsPasses85Pct") == "1"
        gemini_ok = integrity.get("GeminiPasses90Pct") == "1"
        lines.append(
            f"En lo técnico, la integridad de los CSV fue del {integrity.get('ValidPct', 'n/a')} %, "
            f"Gemini {'cumplió' if gemini_ok else 'no cumplió'} la meta de latencia, "
            f"el TTS {'cumplió' if tts_ok else 'quedó por debajo de la meta'} y hubo "
            f"{integrity.get('TotalModelLeaks', '0')} leaks en total."
        )

    lines.extend(
        [
            "En conjunto, hacer visible el agente no cambió mucho los aciertos respecto al chat, "
            "pero sí elevó la confianza y la brecha de calibración. El diseño within-subjects "
            "resultó viable y los hallazgos van en la dirección de RQ1–RQ3 dentro del alcance "
            "de la muestra analizada.",
            "Como trabajo futuro conviene ampliar N, estabilizar TTS, reducir leaks e "
            "incorporar la entrevista semiestructurada. También habría que decidir si el producto "
            "prioriza precisión (quizá basta B) o experiencia motivacional (C), cuidando la sobreconfianza.",
        ]
    )
    return lines


def build_hipotesis_section(
    acc: dict[str, float],
    conf: dict[str, float],
    gap: dict[str, float],
) -> list[str]:
    return [
        "Las hipótesis del Entregable 2 se revisan con las medias del subconjunto completo "
        "y con la inferencia del anexo:",
        "H1 (precisión C ≈ B > A): el agente debería mejorar aciertos frente a A; B y C "
        "deberían parecerse porque comparten el mismo LLM.",
        "H2 (confianza C > B > A): más seguridad con agente y un poco más con avatar visible.",
        "H3 (mayor brecha en C): más desajuste entre confianza y aciertos cuando hay embodiment.",
        _verdict_h1(acc) if acc else "H1: sin datos.",
        _verdict_h2(conf) if conf else "H2: sin datos.",
        _verdict_h3(gap) if gap else "H3: sin datos.",
        "Con tan pocos participantes estas lecturas son tentativas; hay que cruzarlas con "
        "Friedman/Wilcoxon y con las tablas individuales antes de plantear un estudio mayor.",
    ]


REFERENCES: list[str] = [
    "Glikson, E., & Woolley, A. W. (2020). Human trust in artificial intelligence: Review of empirical research. "
    "Academy of Management Annals, 14(2), 627–660.",
    "Lee, J. D., & See, K. A. (2004). Trust in automation: Designing for appropriate reliance. "
    "Human Factors, 46(1), 50–80.",
    "Minge, M., & Thüring, M. (2018). The meCUE 2.0 user experience questionnaire. "
    "Proceedings of Mensch und Computer 2018.",
    "Hart, S. G., & Staveland, L. E. (1988). Development of NASA-TLX. Advances in Psychology, 52, 139–183.",
    "Zhang, Y., Liao, Q. V., & Bellamy, R. K. E. (2020). Effect of confidence and explanation on accuracy and "
    "trust calibration in AI-assisted decision making. Proceedings of FAccT 2020.",
    "Hornbæk, K. (2010). Dogmas in the assessment of usability evaluation methods. Behaviour & Information Technology, "
    "29(1), 97–111.",
    "Nielsen, J. (1994). Usability Engineering. Morgan Kaufmann.",
]


def build_metodologia(bundle: AnalysisBundle, *, n_canon: int, n_complete: int) -> list[str]:
    return [
        "Diseño. Estudio within-subjects con tres condiciones: A (sin agente), "
        "B (chat con Gemini) y C (mismo agente con avatar 3D y voz Azure). Cada participante "
        "respondió 6 ítems por condición, con escenarios ficticios de reglas de tránsito. "
        "El orden A/B/C se contrabalanceó según el código de participante en la app.",
        "Instrumentos. Precisión y confianza (1–7) por ítem en Unity (`ExperimentData.csv`); "
        "RAW-TLX y meCUE 2.0 tras cada bloque (Google Forms); métricas de chat "
        "(HelpScore, intercambios, leaks) en B y C.",
        f"Muestra. Se tomó una sesión canónica por código P##. El análisis principal usa "
        f"quienes completaron los tres bloques: n = {n_complete} de {n_canon} sesiones canónicas.",
        "Análisis. Descriptivos por condición; Friedman con Kendall W; Wilcoxon pareado "
        "(Bonferroni y Holm); intervalos bootstrap al 95 % y contrastes dirigidos "
        "(H3 C vs A, precisión B vs C, HelpScore). Todo el pipeline corre con "
        "`_tools/analyze_all_rq.py` y deja las tablas en `_analysis/`.",
        "Alcance. La muestra es reducida pero suficiente para contrastar las RQ del diseño. "
        "La revisión del protocolo con expertos y la entrevista post-sesión se documentan aparte; "
        "aquí me concentro en los resultados cuantitativos registrados en Unity y Forms.",
    ]


def build_flujo_narrative(bundle: AnalysisBundle) -> list[str]:
    total = len(bundle.sessions)
    canon = bundle.canonical_sessions
    complete = bundle.complete_canonical
    n_canon = len(canon)
    n_complete = len(complete)
    excluded = total - n_canon
    pct_complete = 100.0 * n_complete / n_canon if n_canon else 0.0

    lines = [
        "Criterios de inclusión y flujo de datos para el análisis cuantitativo.",
        f"En `CSV data/` aparecieron {total} carpeta(s) de sesión. De esas, {n_canon} "
        "se tomaron como canónicas (una por participante P##, sin duplicados).",
    ]
    if excluded:
        lines.append(
            f"{excluded} carpeta(s) quedaron fuera por repetición, sesiones incompletas "
            "o lo anotado en `sessions_summary.csv`."
        )
    lines.append(
        f"{n_complete} de {n_canon} participantes canónicos completaron A, B y C "
        f"con 6 ítems cada uno ({pct_complete:.1f} %). La meta de completitud del diseño era ≥ 80 %."
    )
    lines.append(
        "La Tabla F resume el embudo; la Tabla S detalla ítems y consentimiento por persona. "
        "Las medias, gráficos e inferencia usan por defecto solo quienes cerraron los tres bloques."
    )
    return lines


def build_flujo_summary_rows(bundle: AnalysisBundle) -> list[dict[str, str]]:
    total = len(bundle.sessions)
    n_canon = len(bundle.canonical_sessions)
    n_complete = len(bundle.complete_canonical)
    with_consent = sum(1 for r in bundle.canonical_sessions if (r.get("HasConsent") or "").strip() == "1")
    return [
        {"Etapa": "Carpetas de sesión encontradas", "N": str(total), "Nota": "Todas bajo CSV data/"},
        {"Etapa": "Sesiones canónicas (análisis)", "N": str(n_canon), "Nota": "Una por P##"},
        {"Etapa": "Con consentimiento registrado", "N": str(with_consent), "Nota": "HasConsent = 1"},
        {
            "Etapa": "Completas A+B+C (6+6+6 ítems)",
            "N": str(n_complete),
            "Nota": "Subconjunto principal de inferencia",
        },
    ]


def build_sessions_detail_rows(bundle: AnalysisBundle) -> list[dict[str, str]]:
    rows: list[dict[str, str]] = []
    for row in sorted(bundle.canonical_sessions, key=lambda r: r.get("ParticipantCode", "")):
        rows.append(
            {
                "P##": row.get("ParticipantCode", ""),
                "Ítems A": row.get("Items_A", ""),
                "Ítems B": row.get("Items_B", ""),
                "Ítems C": row.get("Items_C", ""),
                "Completa": "Sí" if (row.get("Complete_A_B_C") or "").strip() == "1" else "No",
                "Consent.": "Sí" if (row.get("HasConsent") or "").strip() == "1" else "No",
            }
        )
    return rows


def build_viabilidad_semaforo_rows(bundle: AnalysisBundle) -> list[dict[str, str]]:
    integrity = bundle.integrity_map()
    n_canon = len(bundle.canonical_sessions)
    n_complete = len(bundle.complete_canonical)
    pct_complete = 100.0 * n_complete / n_canon if n_canon else 0.0

    def status(ok: bool) -> str:
        return "Cumple" if ok else "No cumple"

    rows: list[dict[str, str]] = [
        {
            "Criterio": "Completitud A+B+C",
            "Meta del diseño": "≥ 80 % de participantes canónicos",
            "Valor observado": f"{pct_complete:.1f} % ({n_complete}/{n_canon})",
            "Estado": status(pct_complete >= 80.0),
        },
    ]
    if integrity:
        valid_pct = integrity.get("ValidPct", "n/d")
        rows.extend(
            [
                {
                    "Criterio": "Integridad CSV",
                    "Meta del diseño": "≥ 95 % filas válidas",
                    "Valor observado": f"{valid_pct} %",
                    "Estado": status(integrity.get("Passes95PctRule") == "1"),
                },
                {
                    "Criterio": "Latencia Gemini",
                    "Meta del diseño": "≤ 5 s en ≥ 90 % turnos",
                    "Valor observado": "Ver `pilot_gemini_latency.csv`",
                    "Estado": status(integrity.get("GeminiPasses90Pct") == "1"),
                },
                {
                    "Criterio": "TTS condición C",
                    "Meta del diseño": "≥ 85 % síntesis exitosas",
                    "Valor observado": "Ver `pilot_tts_success.csv`",
                    "Estado": status(integrity.get("TtsPasses85Pct") == "1"),
                },
                {
                    "Criterio": "Leaks del modelo",
                    "Meta del diseño": "≤ 2 por cada 10 participantes",
                    "Valor observado": (
                        f"{integrity.get('TotalModelLeaks', '0')} total "
                        f"({integrity.get('LeaksPer10Participants', 'n/d')} / 10 p.)"
                    ),
                    "Estado": status(integrity.get("LeaksPassRule") == "1"),
                },
            ]
        )
    return rows


def build_mecue_ii_section(bundle: AnalysisBundle) -> list[str]:
    if not bundle.rq2_forms_mecue_ii_group and not bundle.rq2_forms_mecue_ii_participant:
        return [
            "El módulo meCUE II (estética y cercanía del avatar) se aplicó solo después de C. "
            "En este análisis no había exportación de Forms para esa parte."
        ]
    lines = [
        "meCUE II corresponde únicamente a la condición C, cuando el participante ya vio el avatar.",
        "Mide percepción estética, diseño y cercanía del personaje. No se compara con B "
        "porque el avatar animado solo existe en C.",
    ]
    if bundle.rq2_forms_mecue_ii_group:
        row = bundle.rq2_forms_mecue_ii_group[0]
        mean_c = parse_float(row.get("Mean_C", ""))
        n = row.get("NParticipants", "?")
        if mean_c is not None:
            lines.append(
                f"La media grupal en C fue {mean_c:.2f} (n = {n}). En términos generales, "
                "los puntajes sugieren una experiencia positiva con la presencia del personaje."
            )
    if bundle.rq2_forms_mecue_ii_participant:
        vals = [
            parse_float(r.get("MECUE_II_C", ""))
            for r in bundle.rq2_forms_mecue_ii_participant
        ]
        vals = [v for v in vals if v is not None]
        if vals:
            spread = max(vals) - min(vals)
            lines.append(
                f"Entre participantes el puntaje en C osciló entre {min(vals):.2f} y {max(vals):.2f} "
                f"(rango {spread:.2f})."
            )
    lines.append(
        "Esta subescala complementa RQ2 en la dimensión de embodiment; no reemplaza "
        "la confianza ítem a ítem de Unity ni los módulos I, III y V de meCUE en B y C."
    )
    return lines
