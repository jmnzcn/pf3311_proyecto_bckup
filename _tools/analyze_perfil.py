#!/usr/bin/env python3
"""Exporta perfil de participantes (Form 0) a CSV — sin texto autogenerado."""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

_TOOLS_DIR = Path(__file__).resolve().parent
if str(_TOOLS_DIR) not in sys.path:
    sys.path.insert(0, str(_TOOLS_DIR))

from forms_common import load_perfil_responses
from rq_common import write_csv


def run(forms_dir: Path, output_dir: Path | None = None) -> int:
    responses = load_perfil_responses(forms_dir)
    if not responses or output_dir is None:
        return 0

    rows = [
        [
            r.participant_code,
            r.age_range,
            r.education,
            r.assistant_frequency,
            r.avatar_experience,
            r.source_file,
        ]
        for r in responses
    ]
    write_csv(
        output_dir / "perfil_participantes.csv",
        [
            "ParticipantCode",
            "AgeRange",
            "Education",
            "AssistantFrequency",
            "AvatarExperience",
            "SourceFile",
        ],
        rows,
    )
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description="Exporta Form 0 (perfil) a CSV")
    parser.add_argument("forms_dir", nargs="?", default="Forms data")
    parser.add_argument("--output-dir", type=Path, required=True)
    args = parser.parse_args()
    forms_dir = Path(args.forms_dir)
    if not forms_dir.is_dir():
        print(f"ERROR: no existe {forms_dir}", file=sys.stderr)
        return 1
    return run(forms_dir, args.output_dir)


if __name__ == "__main__":
    raise SystemExit(main())
