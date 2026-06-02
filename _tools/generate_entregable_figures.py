#!/usr/bin/env python3
"""Genera diagramas y mockups de pantalla para el Entregable 2 (Figuras 1-8)."""

from __future__ import annotations

from pathlib import Path

import matplotlib.pyplot as plt
import matplotlib.patches as mpatches
from matplotlib.patches import FancyBboxPatch, FancyArrowPatch

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "docs" / "figures"
OUT.mkdir(parents=True, exist_ok=True)

# Paleta alineada con UI oscura del prototipo (URP / consent background ~0.15)
BG = "#1e1e2e"
PANEL = "#2a2a3d"
BORDER = "#4a4a6a"
ACCENT = "#00bcd4"
TEXT = "#eceff4"
MUTED = "#a0a8b8"
BTN = "#3d5a80"
BTN_HI = "#5c7cfa"
STAR = "#ffd166"
AVATAR = "#6c8ebf"


def save(fig, name: str) -> Path:
    path = OUT / name
    fig.savefig(path, dpi=150, bbox_inches="tight", facecolor=fig.get_facecolor())
    plt.close(fig)
    print(f"  {path.relative_to(ROOT)}")
    return path


def box(ax, x, y, w, h, text, fc=PANEL, ec=BORDER, fs=9, bold=False, ha="center", va="center"):
    patch = FancyBboxPatch(
        (x, y),
        w,
        h,
        boxstyle="round,pad=0.02,rounding_size=0.08",
        linewidth=1.2,
        edgecolor=ec,
        facecolor=fc,
    )
    ax.add_patch(patch)
    weight = "bold" if bold else "normal"
    ax.text(x + w / 2, y + h / 2, text, ha=ha, va=va, fontsize=fs, color=TEXT, weight=weight, wrap=True)


def arrow(ax, x1, y1, x2, y2):
    ax.add_patch(
        FancyArrowPatch(
            (x1, y1),
            (x2, y2),
            arrowstyle="-|>",
            mutation_scale=12,
            linewidth=1.4,
            color=ACCENT,
        )
    )


def fig01_architecture() -> None:
    fig, ax = plt.subplots(figsize=(10, 6.5))
    fig.patch.set_facecolor("white")
    ax.set_xlim(0, 10)
    ax.set_ylim(0, 7)
    ax.axis("off")
    ax.set_title("Figura 1. Arquitectura técnica de la PoC", fontsize=12, weight="bold", pad=12)

    ucr = "#003865"
    box(ax, 0.3, 4.6, 9.4, 2.0, "", fc="#eef4fb", ec=ucr)
    ax.text(5, 6.35, "Unity Standalone (Windows 1920×1080)", ha="center", fontsize=10, weight="bold", color=ucr)

    box(ax, 0.6, 4.85, 2.7, 1.35, "ExperimentLogic\n(consentimiento, sesión,\nchat Gemini)", fs=8)
    box(ax, 3.5, 4.85, 2.7, 1.35, "QuestionManager\n(preguntas, confianza,\nnavegación)", fs=8)
    box(ax, 6.4, 4.85, 2.7, 1.35, "AvatarDisplayController\n(RenderTexture,\nsolo condición C)", fs=8)

    box(ax, 0.6, 3.0, 4.2, 1.2, "AgentSpeechController + AzureLipSync\n(TTS, visemas, lip-sync TTBoyB)", fs=8)
    box(ax, 5.0, 3.0, 4.1, 1.2, "DataLogger → CSV data/\n(UTF-8 con BOM)", fs=8)

    box(ax, 1.0, 0.8, 3.2, 1.3, "Google Gemini API\n(gemini-2.5-flash)", fc="#e8f5e9", ec="#2e7d32", fs=9)
    box(ax, 5.8, 0.8, 3.2, 1.3, "Azure Speech API\n(TTS + visemas)", fc="#e8f5e9", ec="#2e7d32", fs=9)

    arrow(ax, 2.0, 4.85, 2.0, 4.2)
    arrow(ax, 4.8, 4.85, 2.5, 4.2)
    arrow(ax, 7.7, 4.85, 7.2, 4.2)
    arrow(ax, 2.5, 3.0, 2.6, 2.1)
    arrow(ax, 7.0, 3.0, 7.4, 2.1)

    save(fig, "figura_01_arquitectura.png")


def fig02_flow() -> None:
    fig, ax = plt.subplots(figsize=(8, 10))
    fig.patch.set_facecolor("white")
    ax.set_xlim(0, 8)
    ax.set_ylim(0, 12)
    ax.axis("off")
    ax.set_title("Figura 2. Flujo del participante", fontsize=12, weight="bold", pad=12)

    steps = [
        (3.0, 10.8, 2.0, 0.7, "Consentimiento\n(casilla + continuar)"),
        (3.0, 9.5, 2.0, 0.7, "Elegir condición\nA / B / C (INICIAR)"),
        (1.2, 7.4, 5.6, 1.8, "Por cada pregunta (1 de 6):\n• Leer REGLAS + SITUACIÓN\n• Elegir A–D → SIGUIENTE\n• Confianza 1–7 estrellas → entregar\n• (B/C) chat opcional entre preguntas"),
        (2.2, 5.5, 3.6, 1.0, "Guardar fila en CSV\nDataLogger"),
        (1.5, 3.6, 5.0, 1.2, "Pantalla final\nOtro Escenario · Realizar Encuesta · Finalizar"),
    ]
    for x, y, w, h, t in steps:
        box(ax, x, y, w, h, t, fs=8.5)

    for y1, y2 in [(10.8, 10.2), (9.5, 9.2), (9.2, 8.2), (7.4, 6.5), (5.5, 4.8)]:
        arrow(ax, 4.0, y1, 4.0, y2)

    ax.text(6.5, 8.0, "OnScenarioSelected(0|1|2)", fontsize=7, color=MUTED, style="italic")
    ax.text(6.5, 5.0, "ExperimentData_{UserID}.csv", fontsize=7, color=MUTED, style="italic")
    save(fig, "figura_02_flujo.png")


def _ui_base(title: str = "ExperimentPrototypeB03230"):
    fig, ax = plt.subplots(figsize=(9.6, 5.4))
    fig.patch.set_facecolor(BG)
    ax.set_xlim(0, 19.2)
    ax.set_ylim(0, 10.8)
    ax.axis("off")
    ax.add_patch(FancyBboxPatch((0, 10.2), 19.2, 0.6, boxstyle="square", facecolor="#11111b", edgecolor=BORDER))
    ax.text(0.4, 10.5, title, va="center", fontsize=9, color=MUTED)
    ax.text(18.8, 10.5, "1920×1080", va="center", ha="right", fontsize=8, color=MUTED)
    return fig, ax


def _question_block(ax, x, y, w, h, show_chat=False, show_avatar=False):
    ax.add_patch(FancyBboxPatch((x, y), w, h, boxstyle="round,pad=0.01", facecolor=PANEL, edgecolor=BORDER))
    ax.text(x + 0.25, y + 0.35, "Pregunta 2 de 6", fontsize=8, color=ACCENT, weight="bold")
    rules = (
        "REGLAS:\n"
        "1. Urgencia vital → atención inmediata.\n"
        "2. Si aplican dos reglas, gana la de menor número.\n\n"
        "SITUACIÓN:\n"
        "Paciente con dolor torácico y saturación 88%.\n\n"
        "¿Cuál es la prioridad de atención?"
    )
    ax.text(x + 0.25, y + 0.75, rules, fontsize=7.2, color=TEXT, va="top", linespacing=1.35)

    opts = [("A", "Urgencia inmediata"), ("B", "Urgencia diferida"), ("C", "Consulta externa"), ("D", "No aplica")]
    oy = y + h - 1.55
    for i, (letter, label) in enumerate(opts):
        ox = x + 0.25 + (i % 2) * 2.8
        row = i // 2
        fc = BTN_HI if letter == "A" else BTN
        box(ax, ox, oy + row * 0.55, 2.5, 0.42, f"{letter}. {label}", fc=fc, fs=7)

    box(ax, x + w - 1.6, y + h - 0.55, 1.3, 0.38, "SIGUIENTE", fc=ACCENT, ec=ACCENT, fs=7, bold=True)

    if show_chat:
        cx = x + w + 0.15
        cw = 4.8 if show_avatar else 5.5
        ax.add_patch(FancyBboxPatch((cx, y), cw, h, boxstyle="round,pad=0.01", facecolor=PANEL, edgecolor=BORDER))
        ax.text(cx + 0.2, y + 0.3, "Chat con el agente", fontsize=8, color=ACCENT, weight="bold")
        ax.text(
            cx + 0.2,
            y + 0.75,
            "Participante: ¿Cuál regla manda si hay conflicto?\n\n"
            "Agente: Revisá la lista; la de número\nmenor tiene precedencia.",
            fontsize=7,
            color=TEXT,
            va="top",
            linespacing=1.3,
        )
        box(ax, cx + 0.2, y + h - 0.85, cw - 2.0, 0.38, "Escribí tu mensaje…", fc="#11111b", fs=7, ha="left")
        box(ax, cx + cw - 1.5, y + h - 0.85, 1.2, 0.38, "Enviar", fc=BTN_HI, fs=7)

    if show_avatar:
        ax.add_patch(FancyBboxPatch((x + w + 5.1, y), 3.6, h, boxstyle="round,pad=0.01", facecolor="#151525", edgecolor=ACCENT))
        ax.add_patch(plt.Circle((x + w + 6.9, y + h / 2 + 0.2), 1.1, color=AVATAR, alpha=0.85))
        ax.text(x + w + 6.9, y + h / 2 - 1.0, "Avatar TTBoyB\n(voz + lip-sync)", ha="center", fontsize=7, color=TEXT)


def _confidence_popup(ax, cx, cy):
    box(ax, cx, cy, 4.2, 2.0, "", fc="#252538", ec=ACCENT)
    ax.text(cx + 2.1, cy + 0.35, "¿Qué tan seguro estás de tu respuesta?", ha="center", fontsize=8, color=TEXT)
    for i in range(7):
        star_x = cx + 0.55 + i * 0.52
        ax.text(star_x, cy + 0.95, "★", fontsize=14, color=STAR if i < 5 else MUTED)
    ax.text(cx + 2.1, cy + 1.45, "5 / 7", ha="center", fontsize=8, color=MUTED)
    box(ax, cx + 1.45, cy + 1.55, 1.3, 0.35, "entregar", fc=ACCENT, ec=ACCENT, fs=7, bold=True)


def fig03_consent() -> None:
    fig, ax = _ui_base()
    ax.add_patch(FancyBboxPatch((3.5, 2.0), 12.2, 6.8, boxstyle="round,pad=0.02", facecolor=PANEL, edgecolor=ACCENT, linewidth=2))
    ax.text(9.6, 2.6, "Consentimiento informado", ha="center", fontsize=13, color=TEXT, weight="bold")
    consent = (
        "Participás en un piloto de usabilidad con escenarios ficticios.\n"
        "Los datos se guardan con un ID anónimo en la máquina virtual.\n"
        "Podés retirarte en cualquier momento desde el menú de salida.\n"
        "Al marcar la casilla confirmás que leíste esta información."
    )
    ax.text(9.6, 4.8, consent, ha="center", va="center", fontsize=9, color=TEXT, linespacing=1.5)
    box(ax, 4.2, 6.8, 0.45, 0.45, "✓", fc=ACCENT, ec=ACCENT, fs=11, bold=True)
    ax.text(4.9, 7.02, "He leído y acepto participar", fontsize=9, color=TEXT, va="center")
    box(ax, 7.8, 7.6, 3.6, 0.55, "Continuar", fc=ACCENT, ec=ACCENT, fs=10, bold=True)
    ax.text(9.6, 1.2, "Figura 3. Pantalla de consentimiento", ha="center", fontsize=9, color=MUTED, style="italic")
    save(fig, "figura_03_consentimiento.png")


def fig04_selection() -> None:
    fig, ax = _ui_base()
    ax.text(9.6, 1.5, "ID de sesión: ID-20260601193008-4821", ha="center", fontsize=9, color=ACCENT)
    ax.text(9.6, 2.3, "Seleccioná la condición indicada por el investigador", ha="center", fontsize=10, color=TEXT)

    buttons = [
        ("INICIAR: SIN ASISTENCIA", "Condición A — 6 preguntas, sin chat"),
        ("INICIAR: AGENTE DE TEXTO", "Condición B — chat Gemini"),
        ("INICIAR: AGENTE VIRTUAL", "Condición C — chat + avatar + voz"),
    ]
    for i, (label, sub) in enumerate(buttons):
        by = 3.5 + i * 1.55
        box(ax, 4.5, by, 10.2, 0.85, label, fc=BTN_HI if i == 0 else BTN, fs=10, bold=True)
        ax.text(9.6, by + 1.05, sub, ha="center", fontsize=8, color=MUTED)

    ax.text(9.6, 0.5, "Figura 4. Selección de escenario (tres botones INICIAR)", ha="center", fontsize=9, color=MUTED, style="italic")
    save(fig, "figura_04_seleccion.png")


def fig05_condition_a() -> None:
    fig, ax = _ui_base()
    _question_block(ax, 0.8, 1.2, 17.6, 8.5)
    _confidence_popup(ax, 7.5, 4.2)
    ax.text(9.6, 0.45, "Figura 5. Condición A — pregunta y panel de confianza (sin chat)", ha="center", fontsize=9, color=MUTED, style="italic")
    save(fig, "figura_05_condicion_a.png")


def fig06_condition_b() -> None:
    fig, ax = _ui_base()
    _question_block(ax, 0.5, 1.2, 11.8, 8.5, show_chat=True)
    ax.text(9.6, 0.45, "Figura 6. Condición B — pregunta con panel de chat (sin avatar)", ha="center", fontsize=9, color=MUTED, style="italic")
    save(fig, "figura_06_condicion_b.png")


def fig07_condition_c() -> None:
    fig, ax = _ui_base()
    _question_block(ax, 0.4, 1.2, 10.5, 8.5, show_chat=True, show_avatar=True)
    ax.text(9.6, 0.45, "Figura 7. Condición C — chat, avatar TTBoyB y síntesis de voz", ha="center", fontsize=9, color=MUTED, style="italic")
    save(fig, "figura_07_condicion_c.png")


def fig08_final() -> None:
    fig, ax = _ui_base()
    ax.add_patch(FancyBboxPatch((4.0, 3.0), 11.2, 4.5, boxstyle="round,pad=0.02", facecolor=PANEL, edgecolor=BORDER))
    ax.text(9.6, 3.6, "Bloque completado", ha="center", fontsize=12, color=TEXT, weight="bold")
    ax.text(9.6, 4.4, "6 respuestas guardadas en CSV", ha="center", fontsize=9, color=MUTED)

    finals = [
        ("Otro Escenario", BTN_HI, "Pasar a la siguiente condición (mismo ID)"),
        ("Realizar Encuesta", "#555555", "meCUE 2.0 (si surveyUrl configurada)"),
        ("Finalizar", BTN, "Cerrar sesión y recargar escena"),
    ]
    for i, (label, color, sub) in enumerate(finals):
        by = 5.0 + i * 0.85
        box(ax, 5.5, by, 8.2, 0.55, label, fc=color, fs=10, bold=True)
        ax.text(9.6, by + 0.65, sub, ha="center", fontsize=7.5, color=MUTED)

    ax.text(9.6, 0.45, "Figura 8. Pantalla final — Otro Escenario / Encuesta / Finalizar", ha="center", fontsize=9, color=MUTED, style="italic")
    save(fig, "figura_08_final.png")


def main() -> int:
    print("Generando figuras en docs/figures/ …")
    fig01_architecture()
    fig02_flow()
    fig03_consent()
    fig04_selection()
    fig05_condition_a()
    fig06_condition_b()
    fig07_condition_c()
    fig08_final()
    print("Listo.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
