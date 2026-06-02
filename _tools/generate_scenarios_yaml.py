"""Regenerate QuestionManager scenarios in SampleScene.unity from the official docx."""
import re
import zipfile
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DOCX = Path(
    r"c:\UCR Maestria\2026\Semestre 1\PF-3311 Temas Especiales de Ingeniería de Sistemas de Información Agentes Virtuales Inteligentes"
    r"\Proyecto Investigacion\Preguntas Proyecto Investigacion\Preguntas_Por_Escenario_Demo.docx"
)
SCENE = ROOT / "Assets" / "Scenes" / "SampleScene.unity"
W = "{http://schemas.openxmlformats.org/wordprocessingml/2006/main}"
SCENARIO_NAMES = {
    "1": "Condición A — Sin Asistencia",
    "2": "Condición B — Agente de Texto",
    "3": "Condición C — Agente Virtual",
}


def read_docx_lines(path: Path) -> list[str]:
    with zipfile.ZipFile(path) as z:
        root = ET.fromstring(z.read("word/document.xml"))
    lines: list[str] = []
    for p in root.iter(W + "p"):
        parts = [t.text for t in p.iter(W + "t") if t.text]
        if parts:
            lines.append("".join(parts))
    return lines


def parse_scenarios(lines: list[str]) -> list[dict]:
    scenarios: list[dict] = []
    current: dict | None = None
    i = 0
    while i < len(lines):
        line = lines[i].strip()
        if line.startswith("Escenario "):
            if current:
                scenarios.append(current)
            m = re.match(r"Escenario (\d+)", line)
            num = m.group(1) if m else "?"
            current = {"name": SCENARIO_NAMES.get(num, line), "questions": []}
            i += 1
            continue

        if re.match(r"^Q\d+$", line) and current is not None:
            i += 1
            if i < len(lines):
                i += 1  # title (Triaje Médico, etc.)

            rules: list[str] = []
            while i < len(lines) and not lines[i].strip().startswith("SITUACIÓN"):
                t = lines[i].strip()
                if t == "REGLAS" or t.startswith("•"):
                    rules.append(t)
                i += 1

            situation_body: list[str] = []
            while i < len(lines):
                t = lines[i].strip()
                if (
                    t.startswith("¿")
                    or re.match(r"^[A-D]\)", t)
                    or t.startswith("✓")
                    or t.startswith("⚠")
                    or re.match(r"^Q\d+$", t)
                    or t.startswith("Escenario ")
                ):
                    break
                if t.startswith("SITUACIÓN"):
                    situation_body.append(t)
                elif situation_body and t:
                    situation_body[-1] += " " + t
                i += 1

            question_line = ""
            if i < len(lines) and lines[i].strip().startswith("¿"):
                question_line = lines[i].strip()
                i += 1

            opts: dict[str, str] = {}
            for letter in "ABCD":
                if i < len(lines) and re.match(rf"^{letter}\)\s*", lines[i].strip()):
                    opts[letter] = re.sub(rf"^{letter}\)\s*", "", lines[i].strip())
                    i += 1

            correct = ""
            if i < len(lines) and lines[i].strip().startswith("✓"):
                correct = lines[i].strip().replace("✓", "").strip()
                i += 1

            while i < len(lines):
                t = lines[i].strip()
                if re.match(r"^Q\d+$", t) or t.startswith("Escenario ") or t.startswith("Banco"):
                    break
                if t.startswith("⚠") or (len(t) > 2 and t[1] == ":" and t[0] in "ABCD"):
                    i += 1
                    continue
                if re.match(r"^[A-D]\)", t) or t.startswith("¿"):
                    break
                i += 1

            parts: list[str] = []
            if rules:
                parts.append("REGLAS")
                parts.extend(r for r in rules if r != "REGLAS")
                parts.append("")
            parts.extend(situation_body)
            if question_line:
                parts.append("")
                parts.append(question_line)

            current["questions"].append(
                {
                    "situation": "\r\n".join(parts),
                    "optA": opts.get("A", ""),
                    "optB": opts.get("B", ""),
                    "optC": opts.get("C", ""),
                    "optD": opts.get("D", ""),
                    "correctOption": correct,
                }
            )
            continue

        i += 1

    if current:
        scenarios.append(current)
    return scenarios


def unity_escape(s: str) -> str:
    out: list[str] = []
    text = s.replace("\r\n", "\n").replace("\r", "\n")
    parts = text.split("\n")
    for pi, part in enumerate(parts):
        if pi:
            out.append("\\r\\n")
        for ch in part:
            o = ord(ch)
            if ch == '"':
                out.append('\\"')
            elif ch == "\\":
                out.append("\\\\")
            elif o < 32:
                continue
            elif o <= 127:
                out.append(ch)
            else:
                out.append(f"\\u{o:04x}")
    return "".join(out)


def build_yaml_block(scenarios: list[dict]) -> str:
    lines = ["  scenarios:"]
    for sc in scenarios:
        lines.append(f'  - scenarioName: {sc["name"]}')
        lines.append("    questions:")
        for q in sc["questions"]:
            # Single-line quoted strings avoid breaking \\uXXXX across YAML wraps.
            lines.append(f'    - situation: "{unity_escape(q["situation"])}"')
            for key in ("optA", "optB", "optC", "optD"):
                lines.append(f'      {key}: "{unity_escape(q[key])}"')
            lines.append(f'      correctOption: {q["correctOption"]}')
    return "\n".join(lines)


def patch_scene(scenarios: list[dict]) -> None:
    block = build_yaml_block(scenarios)
    scene = SCENE.read_text(encoding="utf-8")
    start = scene.index("  scenarios:")
    end = scene.index("  currentQuestionIndex:", start)
    SCENE.write_text(scene[:start] + block + scene[end:], encoding="utf-8")


def main() -> None:
    if not DOCX.is_file():
        raise SystemExit(f"Docx not found: {DOCX}")
    scenarios = parse_scenarios(read_docx_lines(DOCX))
    counts = [len(s["questions"]) for s in scenarios]
    if counts != [6, 6, 6]:
        raise SystemExit(f"Expected 6 questions per scenario, got {counts}")
    patch_scene(scenarios)
    total = sum(counts)
    print(f"Patched {SCENE} with {total} questions from {DOCX.name}")


if __name__ == "__main__":
    main()
