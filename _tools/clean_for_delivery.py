"""Remove test artifacts and sanitize API keys before GitHub / entrega.

Usage (from repo root):
    python _tools/clean_for_delivery.py
    python _tools/clean_for_delivery.py --dry-run
"""
from __future__ import annotations

import argparse
import re
import shutil
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]

CSV_GLOBS = (
    ROOT / "Logs",
    ROOT / "CSV data",
)
REMOVE_DIRS = (
    ROOT / "obj",
    ROOT / "Temp",
)
OPTIONAL_REMOVE = (
    ROOT / "UserSettings",
    ROOT / "Library",
)

GEMINI_PLACEHOLDER = "YOUR_GEMINI_API_KEY_HERE"
AZURE_PLACEHOLDER = "YOUR_API_KEY_HERE"

EXPERIMENT_LOGIC = ROOT / "Assets" / "_Project" / "Scripts" / "Core" / "ExperimentLogic.cs"
SAMPLE_SCENE = ROOT / "Assets" / "Scenes" / "SampleScene.unity"
USER_LAYOUTS = ROOT / "UserSettings" / "Layouts"


def delete_csv_files(dry_run: bool) -> list[Path]:
    removed: list[Path] = []
    for folder in CSV_GLOBS:
        if not folder.is_dir():
            continue
        for path in folder.glob("*.csv"):
            removed.append(path)
            if not dry_run:
                path.unlink()
    return removed


def delete_dirs(dry_run: bool, paths: tuple[Path, ...]) -> list[Path]:
    removed: list[Path] = []
    for path in paths:
        if not path.exists():
            continue
        removed.append(path)
        if not dry_run:
            shutil.rmtree(path, ignore_errors=True)
    return removed


def sanitize_keys(dry_run: bool) -> list[str]:
    changes: list[str] = []

    if EXPERIMENT_LOGIC.is_file():
        text = EXPERIMENT_LOGIC.read_text(encoding="utf-8")
        new_text, n = re.subn(
            r'public string apiKey = "[^"]*";',
            f'public string apiKey = "{GEMINI_PLACEHOLDER}";',
            text,
            count=1,
        )
        if n:
            changes.append(f"ExperimentLogic.cs -> {GEMINI_PLACEHOLDER}")
            if not dry_run:
                EXPERIMENT_LOGIC.write_text(new_text, encoding="utf-8", newline="\n")

    if SAMPLE_SCENE.is_file():
        text = SAMPLE_SCENE.read_text(encoding="utf-8")
        original = text
        text = re.sub(
            r"apiKey: AIzaSy[^\n\r]+",
            f"apiKey: {GEMINI_PLACEHOLDER}",
            text,
            count=1,
        )
        text = re.sub(
            r"subscriptionKey: [^\n\r]+",
            f"subscriptionKey: {AZURE_PLACEHOLDER}",
            text,
            count=1,
        )
        if text != original:
            changes.append(f"SampleScene.unity -> Gemini + Azure placeholders")
            if not dry_run:
                SAMPLE_SCENE.write_text(text, encoding="utf-8", newline="\n")

    return changes


def scrub_user_settings(dry_run: bool) -> list[str]:
    """Remove stale paths to old Unity project folders from editor layouts."""
    changes: list[str] = []
    if not USER_LAYOUTS.is_dir():
        return changes

    stale_layout_block = re.compile(
        r"  m_SerializedViewNames:\n"
        r"  - UnityEditor\.DeviceSimulation\.SimulatorWindow\n"
        r"  m_SerializedViewValues:\n"
        r"  - [^\n]+\n",
        re.MULTILINE,
    )
    stale_project_path = re.compile(r".*ExperimentPrototype V\d+.*\n")

    for path in USER_LAYOUTS.glob("*.dwlt"):
        text = path.read_text(encoding="utf-8")
        original = text
        text = stale_layout_block.sub(
            "  m_SerializedViewNames: []\n  m_SerializedViewValues: []\n",
            text,
        )
        text = stale_project_path.sub("", text)
        if text != original:
            changes.append(str(path.relative_to(ROOT)))
            if not dry_run:
                path.write_text(text, encoding="utf-8", newline="\n")
    return changes


def main() -> int:
    parser = argparse.ArgumentParser(description="Clean test data and secrets for delivery.")
    parser.add_argument("--dry-run", action="store_true", help="Show what would change.")
    parser.add_argument(
        "--include-unity-cache",
        action="store_true",
        help="Also delete Library/ and UserSettings/ (long reimport on next open).",
    )
    parser.add_argument(
        "--remove-user-settings",
        action="store_true",
        help="Delete UserSettings/ entirely (Unity recreates default layout on next open).",
    )
    args = parser.parse_args()

    print("=== Limpieza para entrega ===")
    if args.dry_run:
        print("(modo simulacion)\n")

    csvs = delete_csv_files(args.dry_run)
    print(f"CSVs de prueba: {len(csvs)}")
    for p in csvs[:5]:
        print(f"  - {p.relative_to(ROOT)}")
    if len(csvs) > 5:
        print(f"  ... y {len(csvs) - 5} mas")

    dirs = delete_dirs(args.dry_run, REMOVE_DIRS)
    print(f"\nCarpetas de cache local: {len(dirs)}")
    for p in dirs:
        print(f"  - {p.relative_to(ROOT)}/")

    if args.remove_user_settings:
        extra = delete_dirs(args.dry_run, (ROOT / "UserSettings",))
        print(f"\nUserSettings eliminado: {len(extra)}")
        for p in extra:
            print(f"  - {p.relative_to(ROOT)}/")
    elif args.include_unity_cache:
        extra = delete_dirs(args.dry_run, OPTIONAL_REMOVE)
        print(f"\nCache Unity (Library + UserSettings): {len(extra)}")
        for p in extra:
            print(f"  - {p.relative_to(ROOT)}/")
    else:
        layout_changes = scrub_user_settings(args.dry_run)
        if layout_changes:
            print("\nUserSettings (rutas viejas eliminadas):")
            for line in layout_changes:
                print(f"  - {line}")
        else:
            print("\nUserSettings: sin rutas a proyectos viejos")

    key_changes = sanitize_keys(args.dry_run)
    print("\nClaves API:")
    if key_changes:
        for line in key_changes:
            print(f"  - {line}")
    else:
        print("  (sin cambios; ya estaban en placeholder)")

    print("\nRecordatorio:")
    print("  1. Rotar claves Gemini/Azure en la consola (ya quedaron expuestas en pruebas).")
    print("  2. Configurar claves de nuevo en la VM del piloto (Inspector), no en el repo.")
    print("  3. Para GitHub: no subir Library/, Logs/, UserSettings/, CSV data/, obj/.")
    print("  4. Zip de entrega: Assets + Packages + ProjectSettings + docs + README (sin Library).")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
