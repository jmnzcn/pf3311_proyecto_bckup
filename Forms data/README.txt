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

BACKUP DE PRODUCCION — SOLO GOOGLE DRIVE (oficial)
--------------------------------------------------

Las respuestas viven en Google Forms (nube). El backup va a:
  https://drive.google.com/drive/folders/1RLLESYhJbJkBOnNdV6uZTaehl-t-W-Cp

Tras participantes reales (aunque sigas probando):

  powershell -ExecutionPolicy Bypass -File _tools\backup_forms_produccion_drive.ps1

Lee los 4 Forms en Google y fusiona CSV en Drive (solo agrega P03, P04… sin borrar P01, P02).

Instrucciones: _tools/google_drive/COMO_BACKUP_FORMS_DRIVE.txt

Archivos en la carpeta de backup:
  Form0_Perfil.csv, PostBloqueA.csv, PostBloqueB.csv, PostBloqueC.csv

--- Datos Unity (CSV data) ---

Copiá sesiones nuevas al backup (carpetas P##_ID-... que aun no existan):

     powershell -ExecutionPolicy Bypass -File _tools/merge_csv_data_backup.ps1

Por defecto: de CSV data/ → CSV data/_produccion/

Nota: Google Forms ya guarda todas las respuestas en la nube. El backup local
es por si reexportás, probás en la misma PC o querés un snapshot fijo para analisis.
