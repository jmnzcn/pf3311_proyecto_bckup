# Herramientas de análisis PF-3311

Scripts **solo para procesar datos** del estudio: exportan tablas CSV, gráficos PNG y pruebas estadísticas en `_analysis/`.

## Qué no hace este pipeline

| Salida | ¿Lo genera `analyze_all_rq.py`? |
|--------|--------------------------------|
| Tablas y gráficos en `_analysis/` | **Sí** |
| `reporte_inferencia.txt` (resumen estadístico) | **Sí** |
| Informe de resultados del estudio (prosa, Word/PDF) | **No** — redacción manual del autor |
| Entregables del curso en `docs/` (E1, E2, protocolo, meCUE…) | **No** — entrega académica aparte |

Los scripts `generate_entregable2_docx.py`, `generate_entregable_figures.py`, etc. son **utilidades de maquetado** para documentos del curso; no leen `CSV data/` ni sustituyen el análisis de datos del estudio.

## Requisitos

```bash
pip install -r _tools/requirements-analysis.txt
```

## Comando principal

```bash
python _tools/analyze_all_rq.py "CSV data" --forms-dir "Forms data" --output-dir _analysis
```

Genera en `_analysis/` (carpeta local, no versionada):

| Tipo | Ejemplos |
|------|----------|
| Medias y descriptivos | `rq1_precision_group_means.csv`, `*_descriptive_stats_complete.csv` |
| Por participante | `rq1_precision_by_participant.csv`, `master_participant_table.csv` |
| Inferencia | `rq_inference_omnibus.csv`, `rq_inference_pairwise.csv`, `reporte_inferencia.txt` |
| Viabilidad | `pilot_integrity_summary.csv`, `pilot_gemini_latency.csv` |
| Gráficos | `_analysis/figures/*.png` |

## Scripts del análisis de datos

| Script | Función |
|--------|---------|
| `analyze_rq1.py` | Precisión por condición |
| `analyze_rq2.py` | Confianza Unity |
| `analyze_rq2_forms.py` | RAW-TLX y meCUE (Forms) |
| `analyze_rq3.py` | Brecha de calibración |
| `analyze_items.py` | Precisión y tiempo por ítem |
| `analyze_chat_quality.py` | HelpScore, leaks, intercambios B vs C |
| `analyze_perfil.py` | Perfil Form 0 |
| `generate_rq_plots.py` | Regenera PNG desde CSV ya exportados |
| `rq_inference.py` | Friedman, Wilcoxon, bootstrap (llamado por el pipeline) |
| `verify_smoke_session.py` | Checklist post-humo (columnas nuevas, protocolo chat, reglas piloto) |
| `verify_sessions.py` | Catálogo de sesiones |
| `summarize_gemini_latency.py` | Latencia API por sesión |
| `summarize_tts_success.py` | Éxito TTS en condición C |

## Flujo típico del autor

1. Exportar sesiones Unity a `CSV data/` y Forms a `Forms data/`.
2. Ejecutar `analyze_all_rq.py` → revisar `_analysis/`.
3. Redactar el informe de resultados a mano (entrega aparte del repositorio de código).
4. Entregar por separado los PDF/Word del curso que estén en `docs/`.

## Otros scripts en `_tools/` (no son análisis de datos)

- `generate_entregable2_docx.py`, `generate_entregable_figures.py` — maquetado Entregable 2.
- `generate_scenarios_yaml.py` — preguntas del escenario desde .docx.
- `generate_mapa_codigo_docx.py`, `generate_mecue_*.py` — material metodológico del curso.
- `clean_for_delivery.py` — limpieza antes de subir el repo a GitHub.
- `build_windows.bat` — build standalone Unity.
