#!/usr/bin/env python3
"""
Inicializa y actualiza el backup de producción de encuestas Google Forms (PF-3311).

Flujo:
  1. Exportá CSV desde cada Form → Forms data/ (archivos separados).
  2. Ejecutá: python _tools/backup_forms_produccion.py
  3. El script fusiona lo nuevo en Forms data/_produccion/ sin borrar P01, P02, etc.
  4. Opcional: copia los CSV fusionados a una carpeta local sincronizada con Google Drive.

Comandos:
  python _tools/backup_forms_produccion.py init
  python _tools/backup_forms_produccion.py merge
  python _tools/backup_forms_produccion.py merge --sync-drive
"""

from __future__ import annotations

import argparse
import json
import shutil
import sys
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CONFIG_PATH = Path(__file__).resolve().parent / "data" / "forms_produccion_backup.json"

# Permite importar merge_forms_backup desde _tools/
sys.path.insert(0, str(Path(__file__).resolve().parent))
from merge_forms_backup import merge_csv_file  # noqa: E402


def load_config() -> dict:
    with CONFIG_PATH.open(encoding="utf-8") as handle:
        return json.load(handle)


def resolve_path(relative: str) -> Path:
    return (ROOT / relative).resolve()


def backup_readme(config: dict) -> str:
    lines = [
        "BACKUP DE PRODUCCION — Encuestas Google Forms (PF-3311)",
        "=" * 60,
        "",
        "Esta carpeta es el respaldo acumulado. NO edites a mano salvo emergencia.",
        "Actualizala con:  python _tools/backup_forms_produccion.py merge",
        "",
        f"Drive (copia en la nube): {config['google_drive_folder_url']}",
        "",
        "Formularios (un CSV por formulario):",
        "",
    ]
    for form in config["forms"]:
        lines.append(f"  • {form['export_filename']}")
        lines.append(f"    {form['title']}")
        lines.append(f"    {form['responses_url']}")
        lines.append("")

    lines.extend(
        [
            "Como exportar desde Google Forms:",
            "  1. Abrí el enlace de respuestas del formulario.",
            "  2. Respuestas → Descargar respuestas (.csv)",
            "  3. Guardá en Forms data/ con el nombre indicado arriba.",
            "  4. Ejecutá merge (comando de arriba).",
            "",
            "El merge SOLO AGREGA filas nuevas (Marca temporal + codigo P##).",
            "Ejemplo: backup tiene P01,P02 → export tiene P03,P04 → backup queda P01–P04.",
        ]
    )
    return "\n".join(lines) + "\n"


def cmd_init(config: dict) -> None:
    backup_dir = resolve_path(config["local_backup_dir"])
    exports_dir = resolve_path(config["local_exports_dir"])
    backup_dir.mkdir(parents=True, exist_ok=True)
    exports_dir.mkdir(parents=True, exist_ok=True)

    readme_path = backup_dir / "README.txt"
    readme_path.write_text(backup_readme(config), encoding="utf-8")

    manifest = {
        "created_utc": datetime.now(timezone.utc).isoformat(),
        "google_drive_folder_url": config["google_drive_folder_url"],
        "files": [form["export_filename"] for form in config["forms"]],
    }
    (backup_dir / "manifest.json").write_text(
        json.dumps(manifest, indent=2, ensure_ascii=False) + "\n",
        encoding="utf-8",
    )

    print(f"Carpeta backup creada: {backup_dir}")
    print(f"Exports entrantes:     {exports_dir}")
    print(f"Drive:                 {config['google_drive_folder_url']}")
    print("")
    print("Archivos de backup (uno por formulario):")
    for form in config["forms"]:
        target = backup_dir / form["export_filename"]
        status = "ya existe" if target.is_file() else "pendiente (se crea al primer merge)"
        print(f"  {form['export_filename']} — {status}")


def cmd_merge(config: dict, sync_drive: bool) -> None:
    backup_dir = resolve_path(config["local_backup_dir"])
    exports_dir = resolve_path(config["local_exports_dir"])

    if not backup_dir.is_dir():
        print("Backup no inicializado. Ejecutando init...")
        cmd_init(config)

    total_added = 0
    print(f"Fusionando: {exports_dir} -> {backup_dir}")
    print("")

    for form in config["forms"]:
        name = form["export_filename"]
        incoming = form.get("incoming_filename", name)
        new_path = exports_dir / incoming
        backup_path = backup_dir / name

        if not new_path.is_file():
            print(f"  omitido (sin export en Forms data): {name}")
            continue

        added, total = merge_csv_file(new_path, backup_path)
        total_added += added
        print(f"  {name}: +{added} filas nuevas (total backup: {total})")

    print("")
    print(f"Listo. Filas nuevas en total: {total_added}")

    stamp = datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
    (backup_dir / "last_merge_utc.txt").write_text(stamp + "\n", encoding="utf-8")

    if sync_drive:
        sync_to_drive_mirror(config, backup_dir)


def sync_to_drive_mirror(config: dict, backup_dir: Path) -> None:
    mirror = (config.get("drive_local_mirror") or "").strip()
    if not mirror:
        print("")
        print("Sync Drive omitido: configurá drive_local_mirror en")
        print(f"  {CONFIG_PATH}")
        print("  con la ruta local de la carpeta sincronizada de Drive, por ejemplo:")
        print('  "drive_local_mirror": "G:/Mi unidad/PF3311/Forms_backup_produccion"')
        print("")
        print(f"Luego subí manualmente los CSV a: {config['google_drive_folder_url']}")
        return

    drive_dir = Path(mirror)
    if not drive_dir.is_dir():
        print(f"ADVERTENCIA: no existe drive_local_mirror: {drive_dir}")
        print(f"Subí manualmente desde {backup_dir} a {config['google_drive_folder_url']}")
        return

    drive_dir.mkdir(parents=True, exist_ok=True)
    copied = 0
    for form in config["forms"]:
        name = form["export_filename"]
        src = backup_dir / name
        if not src.is_file():
            continue
        shutil.copy2(src, drive_dir / name)
        copied += 1

    readme = backup_dir / "README.txt"
    if readme.is_file():
        shutil.copy2(readme, drive_dir / "README.txt")

    print("")
    print(f"Copiados {copied} CSV a carpeta Drive local: {drive_dir}")
    print(f"Carpeta en la nube: {config['google_drive_folder_url']}")


def main() -> None:
    parser = argparse.ArgumentParser(description="Backup producción encuestas PF-3311")
    sub = parser.add_subparsers(dest="command", required=True)

    sub.add_parser("init", help="Crea Forms data/_produccion/ y README.")

    merge_parser = sub.add_parser("merge", help="Fusiona exports nuevos al backup.")
    merge_parser.add_argument(
        "--sync-drive",
        action="store_true",
        help="Copia CSV fusionados a drive_local_mirror (Google Drive desktop).",
    )

    args = parser.parse_args()
    config = load_config()

    if args.command == "init":
        cmd_init(config)
    elif args.command == "merge":
        cmd_merge(config, sync_drive=args.sync_drive)


if __name__ == "__main__":
    main()
