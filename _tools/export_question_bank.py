#!/usr/bin/env python3
"""Exporta enunciados de preguntas desde SampleScene.unity a JSON para analisis offline."""

from __future__ import annotations

import argparse
import codecs
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
DEFAULT_SCENE = ROOT / "Assets" / "Scenes" / "SampleScene.unity"
DEFAULT_OUT = Path(__file__).resolve().parent / "data" / "question_bank.json"

SCENARIO_SPLIT = re.compile(r"^\s*-\s*scenarioName:\s*(.+)$", re.MULTILINE)
OPT_A_MARKER = re.compile(r"^\s*optA:\s*", re.MULTILINE)


def _decode_unity_string(raw: str) -> str:
    text = raw.strip()
    if text.startswith('"') and text.endswith('"'):
        text = text[1:-1]
    try:
        return codecs.decode(text, "unicode_escape")
    except UnicodeDecodeError:
        return text


def _extract_situations(block: str) -> list[str]:
    situations: list[str] = []
    chunks = re.split(r"\n\s*-\s*situation:\s*", block)
    for chunk in chunks[1:]:
        match = OPT_A_MARKER.search(chunk)
        raw = chunk[: match.start()] if match else chunk
        lines = [line.rstrip() for line in raw.splitlines()]
        while lines and not lines[-1].strip():
            lines.pop()
        if not lines:
            continue
        merged = " ".join(line.strip() for line in lines)
        situations.append(_decode_unity_string(merged).replace("\r\n", "\n").strip())
    return situations


def parse_question_bank(scene_path: Path) -> list[dict]:
    content = scene_path.read_text(encoding="utf-8")
    scenarios: list[dict] = []
    scenario_starts = list(SCENARIO_SPLIT.finditer(content))
    if not scenario_starts:
        return scenarios

    for idx, match in enumerate(scenario_starts):
        start = match.start()
        end = scenario_starts[idx + 1].start() if idx + 1 < len(scenario_starts) else len(content)
        block = content[start:end]
        scenario_number = idx + 1
        scenario_name = _decode_unity_string(match.group(1))
        questions = [
            {"questionNumber": q_idx, "situation": situation}
            for q_idx, situation in enumerate(_extract_situations(block), start=1)
        ]
        scenarios.append(
            {
                "scenarioNumber": scenario_number,
                "scenarioName": scenario_name,
                "questions": questions,
            }
        )
    return scenarios


def export_bank(scene_path: Path, output_path: Path) -> Path:
    bank = parse_question_bank(scene_path)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(json.dumps(bank, ensure_ascii=False, indent=2), encoding="utf-8")
    return output_path


def main() -> int:
    parser = argparse.ArgumentParser(description="Exporta banco de preguntas desde Unity scene")
    parser.add_argument("--scene", type=Path, default=DEFAULT_SCENE)
    parser.add_argument("--output", type=Path, default=DEFAULT_OUT)
    args = parser.parse_args()
    if not args.scene.is_file():
        print(f"ERROR: no existe {args.scene}")
        return 1
    bank = parse_question_bank(args.scene)
    path = export_bank(args.scene, args.output)
    count = sum(len(s["questions"]) for s in bank)
    print(f"Exportadas {count} preguntas en {len(bank)} escenarios -> {path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
