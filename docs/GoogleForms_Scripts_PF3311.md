# Scripts para Google Forms — Piloto PF-3311

> **Qué es este archivo:** guía para **crear los formularios a mano** en [Google Forms](https://forms.google.com). **No** es un formato de importación: Google Forms no importa Markdown ni CSV de preguntas. Copiá solo el texto dentro de cada bloque de código al campo correspondiente de la interfaz.

> **Curación meCUE:** ver `Justificacion_meCUE_Curado_PF3311_NeyFredJimenez_B03230.md` (y `.docx`).

## Cómo mapear cada bloque a la UI de Google Forms

| En este .md | En Google Forms (español) |
|-------------|---------------------------|
| Título del formulario | Configuración (⚙) → *Título* del formulario |
| Descripción | Primer bloque *Descripción* (ícono ⊞ → Descripción) |
| **Sección:** … | ⊞ → *Título de la sección* (solo título; no es una pregunta) |
| *Respuesta corta* | Tipo de pregunta → **Respuesta corta** |
| *Opción múltiple* | Tipo de pregunta → **Opción múltiple** (una sola respuesta) |
| *Escala lineal* 1–7 | Tipo de pregunta → **Escala lineal** → mín. **1**, máx. **7** |
| Módulo V (−5 a +5) | Ver sección **Módulo V** más abajo (la escala lineal **no** admite negativos) |

**No copies** líneas como `**Pregunta 2** — *Escala lineal* 1–7` al título de la pregunta; solo el texto del bloque **Título:**.

---

Copiá cada bloque en un **formulario separado**. Creá **4 formularios** en total.

**Configuración común (escala Likert meCUE):** pregunta tipo *Escala lineal* → mínimo **1**, máximo **7**, etiquetas: `1 = Totalmente en desacuerdo` · `7 = Totalmente de acuerdo`. Activá **Obligatoria** en cada ítem.

**Código participante:** pregunta tipo *Respuesta corta* → **Obligatoria**, sin validación de formato (acepta P01, P16, etc.). Solo pedir que coincida con el código usado en Unity.

**Edad:** *Opción múltiple* (obligatoria) — rangos, no edad exacta:

```
¿En qué rango de edad te encontrás?
18–24 años · 25–34 · 35–44 · 45–54 · 55–64 · 65 años o más
```

### Módulo V (−5 a +5) — limitación de Google Forms

La **escala lineal** de Google Forms solo permite mínimo **0 o 1** y máximo **2–10**; **no admite −5 ni negativos**.

**Opción recomendada (equivalente al docx meCUE):** tipo **Opción múltiple** (una respuesta), opciones en este orden:

```
-5 — Muy malo
-4
-3
-2
-1
0 — Neutral
+1
+2
+3
+4
+5 — Muy bueno
```

**Alternativa (si preferís escala lineal):** mín. **1**, máx. **11**, etiquetas `1 = Muy malo (−5)` · `11 = Muy bueno (+5)`. Al analizar datos: **puntuación meCUE = valor − 6** (1→−5, 6→0, 11→+5).

---

## FORMULARIO 0 — Perfil (inicio de sesión, antes del bloque A)

### Título del formulario
```
PF-3311 — Perfil del participante
```

### Descripción
```
Estudio: Efecto de la visibilidad de un agente virtual en el desempeño y la confianza del usuario.
Investigador: Ney Fred Jiménez Campos (B03230) — UCR.

Respondé con sinceridad. Tus respuestas son anónimas: usamos solo un código (P01, P02, etc.), no tu nombre.
Usá el mismo código que te indicó el investigador y que vas a ingresar en la aplicación Unity.
```

---

**Pregunta 1** — *Respuesta corta* (obligatoria)  
**Título:**
```
Código de participante
```

**Pregunta 2** — *Opción múltiple* (obligatoria)  
**Título:**
```
¿En qué rango de edad te encontrás?
```
**Opciones:**
```
18–24 años
25–34 años
35–44 años
45–54 años
55–64 años
65 años o más
```

**Pregunta 3** — *Opción múltiple* (obligatoria)  
**Título:**
```
Nivel educativo más alto alcanzado
```
**Opciones:**
```
Primaria completa
Secundaria completa
Técnico / diplomado
Universidad incompleta
Universidad completa (grado / licenciatura)
Posgrado (maestría / doctorado)
Otro
```

**Pregunta 4** — *Opción múltiple* (obligatoria)  
**Título:**
```
¿Con qué frecuencia usás asistentes digitales (Siri, Alexa, ChatGPT, Copilot, etc.)?
```
**Opciones:**
```
Nunca o casi nunca
Algunas veces al mes
Algunas veces por semana
Casi todos los días
Varias veces al día
```

**Pregunta 5** — *Opción múltiple* (obligatoria)  
**Título:**
```
¿Has interactuado antes con agentes virtuales o avatares conversacionales (videojuegos, apps, kioscos, etc.)?
```
**Opciones:**
```
Nunca
Una o pocas veces
Algunas veces
Con frecuencia
```

---

## FORMULARIO 1 — Tras condición A (sin asistencia)

### Título del formulario
```
PF-3311 — Post bloque A (sin asistencia)
```

### Descripción
```
Acabás de completar el bloque SIN asistencia del agente (solo casos de decisión).

En este cuestionario evaluamos la CARGA de la tarea, no un sistema de asistencia (porque en este bloque no hubo agente).

Escala para todas las preguntas siguientes: 1 = Totalmente en desacuerdo · 7 = Totalmente de acuerdo.

Nota: no había límite de tiempo impuesto en la aplicación; respondé según cómo te sentiste durante el bloque.
```

---

**Pregunta 1** — *Respuesta corta* (obligatoria)  
**Título:**
```
Código de participante
```

**Sección:** Carga cognitiva (RAW-TLX adaptado)

**Pregunta 2** — *Escala linear* 1–7  
**Título:**
```
La tarea me exigió mucha actividad mental y concentración.
```

**Pregunta 3** — *Escala linear* 1–7  
**Título:**
```
Sentí presión de tiempo mientras resolvía los casos.
```

**Pregunta 4** — *Escala linear* 1–7  
**Título:**
```
Tuve que trabajar muy duro (esfuerzo) para completar la tarea.
```

**Pregunta 5** — *Escala linear* 1–7  
**Título:**
```
Me sentí frustrado/a, tenso/a o irritado/a durante el bloque.
```

**Pregunta 6** — *Escala linear* 1–7  
**Título:**
```
Me sentí seguro/a de cómo desempeñé la tarea en este bloque.
```

**Pregunta 7** — *Escala linear* 1–7  
**Título:**
```
La tarea me resultó exigente en general.
```

---

## FORMULARIO 2 — Tras condición B (chat de texto)

### Título del formulario
```
PF-3311 — Post bloque B (asistencia por chat)
```

### Descripción
```
Acabás de completar el bloque con ASISTENCIA POR CHAT DE TEXTO.

Respondé según tu experiencia con ese sistema en este bloque. No hay respuestas correctas. Decidí de forma espontánea.

«El producto» = el sistema de asistencia por chat que acabás de usar.

Escala meCUE (preguntas de acuerdo/desacuerdo): 1 = Totalmente en desacuerdo · 7 = Totalmente de acuerdo.

Nota RAW-TLX: no había límite de tiempo impuesto en la aplicación; en carga cognitiva, respondé según cómo te sentiste.
```

---

**Pregunta 1** — *Respuesta corta* (obligatoria)  
**Título:**
```
Código de participante
```

**Sección:** meCUE — Módulo I (cualidades instrumentales)

**Pregunta 2** — *Escala linear* 1–7  
**Título:**
```
El producto es fácil de usar.
```

**Pregunta 3** — *Escala linear* 1–7  
**Título:**
```
Las funciones del asistente apoyan lo que necesitaba hacer en los casos de este bloque.
```

**Pregunta 4** — *Escala linear* 1–7  
**Título:**
```
Es evidente rápidamente cómo usar el producto.
```

**Pregunta 5** — *Escala linear* 1–7  
**Título:**
```
Considero que el producto es extremadamente útil.
```

**Pregunta 6** — *Escala linear* 1–7  
**Título:**
```
Los procedimientos de uso del producto son sencillos de entender.
```

**Pregunta 7** — *Escala linear* 1–7  
**Título:**
```
Con la ayuda de este asistente pude avanzar en los casos de este bloque.
```

**Sección:** meCUE — Módulo III (emociones)

**Pregunta 8** — *Escala linear* 1–7  
**Título:**
```
El producto me entusiasma.
```

**Pregunta 9** — *Escala linear* 1–7  
**Título:**
```
El producto me cansa.
```

**Pregunta 10** — *Escala linear* 1–7  
**Título:**
```
El producto me molesta.
```

**Pregunta 11** — *Escala linear* 1–7  
**Título:**
```
El producto me relaja.
```

**Pregunta 12** — *Escala linear* 1–7  
**Título:**
```
Al usar este producto me siento agotado/a.
```

**Pregunta 13** — *Escala linear* 1–7  
**Título:**
```
El producto me hace sentir feliz.
```

**Pregunta 14** — *Escala linear* 1–7  
**Título:**
```
El producto me frustra.
```

**Pregunta 15** — *Escala linear* 1–7  
**Título:**
```
El producto me hace sentir eufórico/a.
```

**Pregunta 16** — *Escala linear* 1–7  
**Título:**
```
El producto me hace sentir pasivo/a.
```

**Pregunta 17** — *Escala linear* 1–7  
**Título:**
```
El producto me calma.
```

**Pregunta 18** — *Escala linear* 1–7  
**Título:**
```
Al usar este producto me siento alegre.
```

**Pregunta 19** — *Escala linear* 1–7  
**Título:**
```
El producto me enoja.
```

**Sección:** meCUE — Módulo IV (consecuencias de uso — 2 ítems curados)

**Pregunta 20** — *Escala lineal* 1–7  
**Título:**
```
Volvería a usar un asistente como este para tareas similares.
```

**Pregunta 21** — *Escala lineal* 1–7  
**Título:**
```
Al usar el producto, pierdo la noción del tiempo.
```

**Sección:** meCUE — Módulo V (evaluación global)

**Pregunta 22** — *Opción múltiple* (obligatoria) — ver **Módulo V** arriba  
**Título:**
```
¿Cómo evaluás el producto (el sistema de asistencia por chat) en general?
```
**Opciones:** −5 … 0 … +5 (lista completa en sección *Módulo V*).

**Sección:** Carga cognitiva (RAW-TLX adaptado)

**Pregunta 23** — *Escala lineal* 1–7  
**Título:**
```
La tarea me exigió mucha actividad mental y concentración.
```

**Pregunta 24** — *Escala lineal* 1–7  
**Título:**
```
Sentí presión de tiempo mientras resolvía los casos.
```

**Pregunta 25** — *Escala lineal* 1–7  
**Título:**
```
Tuve que trabajar muy duro (esfuerzo) para completar la tarea.
```

**Pregunta 26** — *Escala lineal* 1–7  
**Título:**
```
Me sentí frustrado/a, tenso/a o irritado/a durante el bloque.
```

**Pregunta 27** — *Escala lineal* 1–7  
**Título:**
```
Me sentí seguro/a de cómo desempeñé la tarea en este bloque.
```

**Pregunta 28** — *Escala lineal* 1–7  
**Título:**
```
La tarea me resultó exigente en general.
```

---

## FORMULARIO 3 — Tras condición C (agente virtual)

### Título del formulario
```
PF-3311 — Post bloque C (agente virtual)
```

### Descripción
```
Acabás de completar el bloque con AGENTE VIRTUAL (chat + personaje con voz).

Respondé según tu experiencia con ese sistema en este bloque. No hay respuestas correctas. Decidí de forma espontánea.

«El producto» = el agente virtual (asistencia + personaje) que acabás de usar.

Escala meCUE: 1 = Totalmente en desacuerdo · 7 = Totalmente de acuerdo.

Nota RAW-TLX: no había límite de tiempo impuesto en la aplicación; en carga cognitiva, respondé según cómo te sentiste.
```

---

**Pregunta 1** — *Respuesta corta* (obligatoria)  
**Título:**
```
Código de participante
```

**Sección:** meCUE — Módulo I (cualidades instrumentales)

**Pregunta 2** — *Escala linear* 1–7  
**Título:**
```
El producto es fácil de usar.
```

**Pregunta 3** — *Escala linear* 1–7  
**Título:**
```
Las funciones del asistente apoyan lo que necesitaba hacer en los casos de este bloque.
```

**Pregunta 4** — *Escala linear* 1–7  
**Título:**
```
Es evidente rápidamente cómo usar el producto.
```

**Pregunta 5** — *Escala linear* 1–7  
**Título:**
```
Considero que el producto es extremadamente útil.
```

**Pregunta 6** — *Escala linear* 1–7  
**Título:**
```
Los procedimientos de uso del producto son sencillos de entender.
```

**Pregunta 7** — *Escala linear* 1–7  
**Título:**
```
Con la ayuda de este asistente pude avanzar en los casos de este bloque.
```

**Sección:** meCUE — Módulo II (cualidades no instrumentales — avatar)

**Pregunta 8** — *Escala linear* 1–7  
**Título:**
```
El producto está diseñado de forma creativa.
```

**Pregunta 9** — *Escala linear* 1–7  
**Título:**
```
El diseño se ve atractivo.
```

**Pregunta 10** — *Escala linear* 1–7  
**Título:**
```
El producto es elegante / tiene estilo.
```

**Pregunta 11** — *Escala linear* 1–7  
**Título:**
```
El personaje del agente me resultó cercano.
```

**Pregunta 12** — *Escala linear* 1–7  
**Título:**
```
Me costaría completar tareas similares sin un agente como este.
```

**Sección:** meCUE — Módulo III (emociones)

**Pregunta 13** — *Escala linear* 1–7  
**Título:**
```
El producto me entusiasma.
```

**Pregunta 14** — *Escala linear* 1–7  
**Título:**
```
El producto me cansa.
```

**Pregunta 15** — *Escala linear* 1–7  
**Título:**
```
El producto me molesta.
```

**Pregunta 16** — *Escala linear* 1–7  
**Título:**
```
El producto me relaja.
```

**Pregunta 17** — *Escala linear* 1–7  
**Título:**
```
Al usar este producto me siento agotado/a.
```

**Pregunta 18** — *Escala linear* 1–7  
**Título:**
```
El producto me hace sentir feliz.
```

**Pregunta 19** — *Escala linear* 1–7  
**Título:**
```
El producto me frustra.
```

**Pregunta 20** — *Escala linear* 1–7  
**Título:**
```
El producto me hace sentir eufórico/a.
```

**Pregunta 21** — *Escala linear* 1–7  
**Título:**
```
El producto me hace sentir pasivo/a.
```

**Pregunta 22** — *Escala linear* 1–7  
**Título:**
```
El producto me calma.
```

**Pregunta 23** — *Escala linear* 1–7  
**Título:**
```
Al usar este producto me siento alegre.
```

**Pregunta 24** — *Escala linear* 1–7  
**Título:**
```
El producto me enoja.
```

**Sección:** meCUE — Módulo IV (consecuencias de uso — 2 ítems curados)

**Pregunta 25** — *Escala lineal* 1–7  
**Título:**
```
Volvería a usar un asistente como este para tareas similares.
```

**Pregunta 26** — *Escala lineal* 1–7  
**Título:**
```
Al usar el producto, pierdo la noción del tiempo.
```

**Sección:** meCUE — Módulo V (evaluación global)

**Pregunta 27** — *Opción múltiple* (obligatoria) — ver **Módulo V** arriba  
**Título:**
```
¿Cómo evaluás el producto (el agente virtual) en general?
```
**Opciones:** −5 … 0 … +5 (lista completa en sección *Módulo V*).

**Sección:** Carga cognitiva (RAW-TLX adaptado)

**Pregunta 28** — *Escala lineal* 1–7  
**Título:**
```
La tarea me exigió mucha actividad mental y concentración.
```

**Pregunta 29** — *Escala lineal* 1–7  
**Título:**
```
Sentí presión de tiempo mientras resolvía los casos.
```

**Pregunta 30** — *Escala lineal* 1–7  
**Título:**
```
Tuve que trabajar muy duro (esfuerzo) para completar la tarea.
```

**Pregunta 31** — *Escala lineal* 1–7  
**Título:**
```
Me sentí frustrado/a, tenso/a o irritado/a durante el bloque.
```

**Pregunta 32** — *Escala lineal* 1–7  
**Título:**
```
Me sentí seguro/a de cómo desempeñé la tarea en este bloque.
```

**Pregunta 33** — *Escala lineal* 1–7  
**Título:**
```
La tarea me resultó exigente en general.
```

---

## Checklist al publicar en Google Forms

- [ ] 4 formularios creados (Perfil, Post A, Post B, Post C)
- [ ] Títulos de **sección** insertados donde dice `**Sección:**` (no son preguntas)
- [ ] Escalas meCUE/TLX: tipo **Escala lineal** 1–7 con etiquetas en los extremos
- [ ] Módulo V: **Opción múltiple** −5…+5 (o escala 1–11 con recodificación al exportar)
- [ ] Todas las preguntas marcadas **Obligatoria** (salvo las que decidas opcionales)
- [ ] Campo de código: respuesta corta obligatoria, sin regex (libre en encuestas)
- [ ] Rango de edad (opción múltiple) en Formulario 0
- [ ] **Configuración** → *Recopilar direcciones de correo electrónico* → **Desactivado** (anonimato)
- [ ] Limitar a **1 respuesta** por persona (opcional; en VM compartida suele ser mejor **no** limitar)
- [ ] Guardar URLs: `Form0_Perfil`, `Form1_PostA`, `Form2_PostB`, `Form3_PostC`

---

*PF-3311 · Ney Fred Jiménez (B03230) · Curación documentada en `Justificacion_meCUE_Curado_PF3311_NeyFredJimenez_B03230.docx`*
