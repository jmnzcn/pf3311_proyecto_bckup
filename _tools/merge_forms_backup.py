#!/usr/bin/env python3
"""
Fusiona exportaciones CSV de Google Forms en un backup de producción sin borrar filas previas.

Solo agrega respuestas que aún no están en el backup (misma Marca temporal + código P##).
Si el backup no existe, lo crea copiando el archivo nuevo.

Ejemplos:
  # Un formulario
  python _tools/merge_forms_backup.py ^
    --backup "Forms data/_produccion/PostBloqueA.csv" ^
    --new "Forms data/PostBloqueA.csv"

  # Carpeta completa (nombres estándar del piloto)
  python _tools/merge_forms_backup.py ^
    --backup-dir "Forms data/_produccion" ^
    --new-dir "Forms data"
"""

from __future__ import annotations

import argparse
import csv
import shutil
from pathlib import Path

from forms_common import find_participant_column, normalize_participant_code

STANDARD_FORM_FILES = (
    "Form0_Perfil.csv",
    "PostBloqueA.csv",
    "PostBloqueB.csv",
    "PostBloqueC.csv",
)


def row_dedupe_key(row: dict[str, str], participant_col: str | None) -> str:
    timestamp = (row.get("Marca temporal") or "").strip()
    code_raw = (row.get(participant_col, "") if participant_col else "").strip()
    code = normalize_participant_code(code_raw) or code_raw.upper()
    if timestamp and code:
        return f"{timestamp}|{code}"
    return "|".join(f"{k}={row.get(k, '').strip()}" for k in sorted(row.keys()))


def read_rows(path: Path) -> tuple[list[str], list[dict[str, str]], str | None]:
    with path.open(encoding="utf-8-sig", newline="") as handle:
        reader = csv.DictReader(handle)
        fieldnames = list(reader.fieldnames or [])
        participant_col = find_participant_column(fieldnames)
        rows = [dict(row) for row in reader]
    return fieldnames, rows, participant_col


def merge_csv_file(new_path: Path, backup_path: Path) -> tuple[int, int]:
    if not new_path.is_file():
        raise FileNotFoundError(f"No existe export nuevo: {new_path}")

    new_fields, new_rows, new_participant_col = read_rows(new_path)

    if not backup_path.is_file():
        backup_path.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(new_path, backup_path)
        return len(new_rows), len(new_rows)

    backup_fields, backup_rows, backup_participant_col = read_rows(backup_path)
    participant_col = new_participant_col or backup_participant_col

    if backup_fields != new_fields:
        raise ValueError(
            f"Encabezados distintos entre backup y export nuevo en {new_path.name}.\n"
            f"  backup ({len(backup_fields)} cols): {backup_fields[:3]}...\n"
            f"  nuevo  ({len(new_fields)} cols): {new_fields[:3]}...\n"
            "Exportá ambos desde el mismo Formulario o revisá que no cambiaste preguntas."
        )

    seen = {row_dedupe_key(row, participant_col) for row in backup_rows}
    added = 0
    merged = list(backup_rows)

    for row in new_rows:
        key = row_dedupe_key(row, participant_col)
        if key in seen:
            continue
        seen.add(key)
        merged.append(row)
        added += 1

    with backup_path.open("w", encoding="utf-8-sig", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=backup_fields)
        writer.writeheader()
        writer.writerows(merged)

    return added, len(merged)


def merge_directories(new_dir: Path, backup_dir: Path, files: tuple[str, ...]) -> None:
    backup_dir.mkdir(parents=True, exist_ok=True)
    total_added = 0

    for name in files:
        new_path = new_dir / name
        if not new_path.is_file():
            print(f"  omitido (no hay export): {name}")
            continue

        backup_path = backup_dir / name
        added, total = merge_csv_file(new_path, backup_path)
        total_added += added
        print(f"  {name}: +{added} filas nuevas (total backup: {total})")

    print(f"Listo. Filas nuevas agregadas en total: {total_added}")


def main() -> None:
    parser = argparse.ArgumentParser(description="Merge incremental de CSV de Google Forms.")
    parser.add_argument("--backup", type=Path, help="CSV de backup de producción (se actualiza).")
    parser.add_argument("--new", type=Path, help="CSV recién exportado desde Google Forms.")
    parser.add_argument(
        "--backup-dir",
        type=Path,
        help="Carpeta backup (p. ej. Forms data/_produccion).",
    )
    parser.add_argument(
        "--new-dir",
        type=Path,
        help="Carpeta con exports frescos (p. ej. Forms data).",
    )
    args = parser.parse_args()

    if args.backup and args.new:
        added, total = merge_csv_file(args.new, args.backup)
        print(f"+{added} filas nuevas → {args.backup} (total: {total})")
        return

    if args.backup_dir and args.new_dir:
        merge_directories(args.new_dir, args.backup_dir, STANDARD_FORM_FILES)
        return

    parser.error("Usá --backup + --new, o --backup-dir + --new-dir.")


if __name__ == "__main__":
    main()
