Efecto de la visibilidad de un agente virtual en el desempeño y la confianza del usuario en tareas de decisión basadas en reglas
PF-3311 – Agentes Virtuales Inteligentes  
Universidad de Costa Rica — Programa de Posgrado en Computación e Informática  
I Ciclo, 2026
Investigador Principal: Ney Fred Jiménez Campos  
Profesor: Dr. Alexander Barquero Elizondo
---
Descripción
Este repositorio contiene el trabajo desarrollado para el curso PF-3311 Agentes Virtuales Inteligentes. El proyecto investiga si la presencia visual de un agente virtual (avatar 3D con lip-sync y expresiones faciales) produce mejoras reales en el desempeño del usuario en tareas de decisión basadas en reglas, o si sus efectos son principalmente perceptuales.
La pregunta central es: ¿ver al agente cambia cómo el usuario razona y decide, o solo cambia cómo se siente la interacción?
---
Preguntas de Investigación
RQ1 — Precisión: ¿Existen diferencias estadísticamente significativas en la precisión de los usuarios entre las condiciones sin agente (A), asistencia textual (B) y agente virtual visible (C)?
RQ2 — Confianza subjetiva: ¿Existen diferencias en la confianza reportada por los usuarios entre las tres condiciones de asistencia, medida con el cuestionario meCUE 2.0?
RQ3 — Calibración: ¿Cómo varía la relación entre confianza subjetiva y precisión real según la condición del agente? ¿En qué condición se observa mayor sobreconfianza?
---
Stack Tecnológico
Componente	Herramienta
Aplicación	Unity 2022.3 LTS (standalone)
Entorno de ejecución	Máquina virtual controlada por el investigador
Avatar / Embodiment	Modelo 3D Turbosquid + blendshapes lip-sync
LLM	GPT-4o mini (OpenAI) — alternativa: Claude Haiku (Anthropic)
TTS	ElevenLabs o Google Cloud TTS (español latinoamericano)
Backend	Python (FastAPI)
Registro de datos	CSV / JSON
Cuestionario	meCUE 2.0 vía Google Forms o Qualtrics
---
Estructura del Repositorio
```
/
├── README.md
├── docs/
│   └── Entregable1_PF3311_NeyFredJimenez_B03230.pdf
└── src/
    └── (código base — en desarrollo)
```
---
Condiciones Experimentales
El experimento usa un diseño within-subjects: cada participante pasa por las tres condiciones en orden contrabalanceado.
Condición A: Sin agente — el participante resuelve tareas solo
Condición B: Asistencia textual — chatbot sin avatar ni voz
Condición C: Agente visible — mismo contenido que B, más avatar 3D con lip-sync y voz sintetizada
El contenido de la asistencia es idéntico en B y C. La única diferencia es la presencia visual del avatar.
---
Estado del Proyecto
[x] Entregable 1 — Propuesta de Agente e Investigación
[ ] Entregable 2 — En desarrollo
[ ] Entregable 3 — Pendiente
[ ] Entregable 4 — Pendiente
---
Nota de seguridad
Este repositorio no contiene API keys ni credenciales. Todas las claves se manejan mediante variables de entorno en el servidor.
---
Licencia
Proyecto académico — Universidad de Costa Rica, I Ciclo 2026.
