Google Forms — exportaciones para analisis RQ2 (meCUE + RAW-TLX)
================================================================

1. En Google Forms, abra cada formulario post-bloque.
2. Respuestas > Crear hoja de calculo / Descargar respuestas (.csv).
3. Guarde los CSV en esta carpeta con nombres que contengan el bloque, por ejemplo:

   PostBloqueA.csv   (o Form1_PostA.csv)
   PostBloqueB.csv   (o Form2_PostB.csv)
   PostBloqueC.csv   (o Form3_PostC.csv)

4. Ejecute desde la raiz del proyecto:

   pip install -r _tools/requirements-analysis.txt
   python _tools/analyze_all_rq.py "CSV data" --forms-dir "Forms data"

   Genera en _analysis/: tablas CSV y graficos figures/*.png

   Solo verificar sesiones:

   python _tools/verify_sessions.py "CSV data"

   o solo Forms:

   python _tools/analyze_rq2_forms.py "Forms data" --output-dir _analysis

Plantillas de preguntas: docs/google_forms/ y docs/GoogleForms_Scripts_PF3311.md

URLs ya configuradas en Unity (SampleScene) y listadas en:
  _tools/data/pf3311_forms_config.json

El codigo de participante en Forms (P01-P15) debe coincidir con el usado en Unity.
