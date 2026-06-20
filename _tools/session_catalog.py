#!/usr/bin/env python3
"""
Descubre sesiones Unity bajo CSV data/ — una carpeta por ejecución, p. ej.:

  CSV data/P01_ID-20260615001513-7895/ExperimentData.csv
  CSV data/P01_ID-20260615001513-7895/ChatHelpRating.csv
  ...

Selecciona una sesión canónica por participante (la más completa).
"""

from __future__ import annotations

import csv
import re
from dataclasses import dataclass, field
from pathlib import Path

from rq_common import (
    CONDITION_BY_SCENARIO,
    CONDITION_ORDER,
    PROVISIONAL_MARKERS,
    display_path,
    is_real_answer_row,
    parse_int,
    write_csv,
)

SESSION_FOLDER_PATTERN = re.compile(r"^(P\d{2,})_(ID-.+)$", re.IGNORECASE)
ITEMS_PER_BLOCK = 6


@dataclass
class SessionRecord:
    folder: Path
    folder_name: str
    participant_code: str
    session_id: str
    experiment_file: Path | None
    items_by_condition: dict[str, int] = field(default_factory=dict)
    total_real_items: int = 0
    provisional_rows: int = 0
    total_data_rows: int = 0
    has_consent: bool = False
    included_in_analysis: bool = False
    selection_note: str = ""

    @property
    def is_complete(self) -> bool:
        return all(self.items_by_condition.get(c, 0) >= ITEMS_PER_BLOCK for c in CONDITION_ORDER)

    @property
    def conditions_present(self) -> list[str]:
        return [c for c in CONDITION_ORDER if self.items_by_condition.get(c, 0) > 0]

    @property
    def completeness_score(self) -> int:
        return sum(min(self.items_by_condition.get(c, 0), ITEMS_PER_BLOCK) for c in CONDITION_ORDER)

    def participant_session_key(self) -> str:
        return f"{self.participant_code}_{self.session_id}"


@dataclass
class SessionCatalog:
    csv_dir: Path
    sessions: list[SessionRecord]
    canonical: list[SessionRecord]

    @property
    def canonical_experiment_files(self) -> list[Path]:
        files: list[Path] = []
        for session in self.canonical:
            if session.experiment_file and session.experiment_file.is_file():
                files.append(session.experiment_file)
        return files

    @property
    def complete_participants(self) -> list[SessionRecord]:
        return [s for s in self.canonical if s.is_complete]


def normalize_participant_code(raw: str) -> str:
    text = (raw or "").strip().upper()
    if not text.startswith("P"):
        return text
    digits = text[1:]
    if digits.isdigit():
        number = int(digits)
        return f"P{number:02d}" if number < 10 else f"P{number}"
    return text


def parse_folder_name(folder_name: str) -> tuple[str, str] | None:
    match = SESSION_FOLDER_PATTERN.match(folder_name.strip())
    if not match:
        return None
    return normalize_participant_code(match.group(1)), match.group(2)


def _contains_provisional(value: str) -> bool:
    text = (value or "").strip()
    return any(marker in text for marker in PROVISIONAL_MARKERS)


def _scan_experiment_file(path: Path) -> tuple[dict[str, int], int, int, int, str, str]:
    items_by_condition: dict[str, int] = {c: 0 for c in CONDITION_ORDER}
    provisional = 0
    total_rows = 0
    participant_code = ""
    session_id = ""

    with path.open(encoding="utf-8-sig", newline="") as handle:
        reader = csv.DictReader(handle)
        for row in reader:
            total_rows += 1
            if any(_contains_provisional(v) for v in row.values()):
                provisional += 1
                continue

            if is_real_answer_row(row):
                scenario = int(row["ScenarioNumber"])
                condition = CONDITION_BY_SCENARIO[scenario]
                items_by_condition[condition] += 1
                if not participant_code:
                    participant_code = normalize_participant_code(row.get("ParticipantCode", ""))
                if not session_id:
                    session_id = (row.get("SessionID") or "").strip()

    return items_by_condition, sum(items_by_condition.values()), provisional, total_rows, participant_code, session_id


def _scan_consent(folder: Path) -> bool:
    consent_path = folder / "ConsentLog.csv"
    if not consent_path.is_file():
        return False
    with consent_path.open(encoding="utf-8-sig", newline="") as handle:
        reader = csv.DictReader(handle)
        for row in reader:
            age = (row.get("AgeConsent") or "").strip()
            study = (row.get("StudyConsent") or "").strip()
            if age == "1" and study == "1":
                return True
    return False


def _build_session_record(folder: Path, csv_dir: Path, experiment_file: Path) -> SessionRecord | None:
    folder_name = folder.name if folder.is_dir() else experiment_file.parent.name
    parsed = parse_folder_name(folder_name)

    items_by_condition, total_real, provisional, total_rows, code_from_csv, session_from_csv = (
        _scan_experiment_file(experiment_file)
    )

    if parsed:
        participant_code, session_id = parsed
    else:
        participant_code = code_from_csv
        session_id = session_from_csv

    if not participant_code or not session_id:
        return None

    session_folder = folder if folder.is_dir() else experiment_file.parent
    return SessionRecord(
        folder=session_folder,
        folder_name=folder_name if folder.is_dir() else session_folder.name,
        participant_code=participant_code,
        session_id=session_id,
        experiment_file=experiment_file,
        items_by_condition=items_by_condition,
        total_real_items=total_real,
        provisional_rows=provisional,
        total_data_rows=total_rows,
        has_consent=_scan_consent(session_folder),
    )


def discover_sessions(csv_dir: Path) -> list[SessionRecord]:
    """
    Descubre sesiones en subcarpetas directas de csv_dir/, p. ej.:
      CSV data/P01_ID-20260615001513-7895/ExperimentData.csv
    """
    csv_dir = csv_dir.resolve()
    if not csv_dir.is_dir():
        return []

    seen_files: set[Path] = set()
    records: list[SessionRecord] = []

    for session_folder in sorted(p for p in csv_dir.iterdir() if p.is_dir()):
        experiment_file = session_folder / "ExperimentData.csv"
        if not experiment_file.is_file():
            continue
        resolved = experiment_file.resolve()
        if resolved in seen_files:
            continue
        seen_files.add(resolved)

        record = _build_session_record(session_folder, csv_dir, experiment_file)
        if record is not None:
            records.append(record)

    records.sort(key=lambda s: (s.participant_code, s.session_id))
    return records


def select_canonical_sessions(sessions: list[SessionRecord]) -> list[SessionRecord]:
    by_participant: dict[str, list[SessionRecord]] = {}
    for session in sessions:
        by_participant.setdefault(session.participant_code, []).append(session)

    canonical: list[SessionRecord] = []
    for participant_code in sorted(by_participant):
        candidates = by_participant[participant_code]
        best = max(
            candidates,
            key=lambda s: (
                s.completeness_score,
                int(s.is_complete),
                s.total_real_items,
                int(s.has_consent),
                s.session_id,
            ),
        )
        best.included_in_analysis = True
        if len(candidates) > 1:
            others = [c.folder_name for c in candidates if c is not best]
            best.selection_note = (
                f"Canónica entre {len(candidates)} carpetas; omitidas: {', '.join(others)}"
            )
        else:
            best.selection_note = "Unica carpeta para este participante"
        canonical.append(best)

    for session in sessions:
        if not session.included_in_analysis:
            session.selection_note = "Omitida (existe sesion mas completa del mismo participante)"

    return canonical


def build_catalog(csv_dir: Path, canonical_only: bool = True) -> SessionCatalog:
    sessions = discover_sessions(csv_dir)
    canonical = select_canonical_sessions(sessions)
    if not canonical_only:
        for session in sessions:
            session.included_in_analysis = True
            if not session.selection_note:
                session.selection_note = "Incluida (--all-sessions)"
        return SessionCatalog(csv_dir=csv_dir.resolve(), sessions=sessions, canonical=sessions)
    return SessionCatalog(csv_dir=csv_dir.resolve(), sessions=sessions, canonical=canonical)


def discover_session_file(session_folder: Path, file_name: str) -> Path | None:
    direct = session_folder / file_name
    if direct.is_file():
        return direct
    matches = sorted(session_folder.glob(file_name.replace(".csv", "_*.csv")))
    return matches[0] if matches else None


def discover_all_session_files(csv_dir: Path, file_name: str, sessions: list[SessionRecord]) -> list[Path]:
    files: list[Path] = []
    for session in sessions:
        path = discover_session_file(session.folder, file_name)
        if path is not None:
            files.append(path)
    return files


def export_session_summary(catalog: SessionCatalog, output_dir: Path) -> Path:
    rows: list[list[object]] = []
    for session in catalog.sessions:
        rows.append(
            [
                session.folder_name,
                session.participant_code,
                session.session_id,
                session.items_by_condition.get("A", 0),
                session.items_by_condition.get("B", 0),
                session.items_by_condition.get("C", 0),
                session.total_real_items,
                "1" if session.is_complete else "0",
                "1" if session.has_consent else "0",
                "1" if session.included_in_analysis else "0",
                session.provisional_rows,
                session.total_data_rows,
                session.selection_note,
                display_path(catalog.csv_dir, session.folder),
            ]
        )
    path = output_dir / "sessions_summary.csv"
    write_csv(
        path,
        [
            "FolderName",
            "ParticipantCode",
            "SessionID",
            "Items_A",
            "Items_B",
            "Items_C",
            "TotalRealItems",
            "Complete_A_B_C",
            "HasConsent",
            "IncludedInAnalysis",
            "ProvisionalRows",
            "TotalDataRows",
            "SelectionNote",
            "RelativePath",
        ],
        rows,
    )
    return path
