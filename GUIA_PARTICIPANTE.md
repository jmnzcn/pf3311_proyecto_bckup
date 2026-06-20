# Guía para participantes - ExperimentPrototypeB03230

Instrucciones para completar el experimento **conectándote a la máquina virtual** que te indique el investigador. No necesitás instalar ni descargar nada en tu computador personal.

Detalle técnico del sistema: [README.md](README.md).

---

## Antes de empezar

1. **Conexión:** Usá el enlace o credenciales (usuario/contraseña) que te envió el investigador para entrar a la **máquina virtual Windows**.
2. **Pantalla:** El experimento se abre en **pantalla completa 1920×1080** (no hace falta maximizar manualmente).
3. **Internet:** La VM debe tener conexión estable (necesaria para condiciones con agente de texto o avatar).
4. **No abras** archivos en la carpeta `CSV data/` ni uses Excel en la VM antes ni durante la sesión.
5. Reservá **60–75 minutos** sin interrupciones.
6. El investigador te asignará un **código de participante** (por ejemplo `P01`, `P12`, `P20`) y los **enlaces de cuestionarios** (perfil al inicio; uno breve tras cada bloque real).

---

## Cómo iniciar

1. En la VM, abrí **`ExperimentPrototypeB03230.exe`** (una sola vez; no hagas doble clic).
2. Ingresá el **código de participante** que te dio el investigador (podés escribir `P1`, `p01`, `P20`, etc.; la app lo normaliza), marcá las **dos casillas** de consentimiento y pulsá **Continuar**.

![Consentimiento informado](docs/figures/figura_03_consentimiento.png)

3. En la pantalla de selección verás **cuatro botones** y, arriba, una **leyenda** que explica qué es cada letra (**A** = sin asistencia, **B** = agente de texto, **C** = agente virtual) y **tu orden de bloques** (ej.: `Tu orden: B → C → A`). Cada botón real lleva la letra **A**, **B** o **C** a la izquierda. Seguí **exactamente esa secuencia** al elegir cada condición.

![Selección de condición](docs/figures/figura_04_seleccion.png)

| Botón | Qué incluye |
|-------|-------------|
| **INICIAR: PRÁCTICA** | 1 pregunta de prueba **sin registro**. Familiarizate con la interfaz y, si aplica, con el chat o avatar. Solo una vez por sesión. |
| **INICIAR: SIN ASISTENCIA** | 6 preguntas reales (condición **A**). Sin chat ni avatar. |
| **INICIAR: AGENTE DE TEXTO** | 6 preguntas reales (condición **B**) + chat con agente. |
| **INICIAR: AGENTE VIRTUAL** | 6 preguntas reales (condición **C**) + chat + avatar que habla. |

### Orden recomendado

1. **Práctica** (opcional pero recomendada por el investigador).
2. Los **tres bloques reales** en el orden que muestra la app (letras **A**, **B**, **C**).

La práctica usa el mismo tipo de ayuda (sin agente, solo chat o chat+avatar) que tu **primer bloque real**, según tu código de participante.

### Orden obligatorio (bloques reales)

Solo podés pulsar **un bloque real a la vez**, en el orden que muestra la app. Los demás botones reales aparecen **deshabilitados** hasta que completes el bloque anterior de tu secuencia. Así evitamos saltar condiciones por error.

**INICIAR: PRÁCTICA** no sigue esa regla: podés usarlo cuando quieras (una vez por sesión), antes o entre bloques reales.

### Bloques ya completados

Cuando termines un bloque real y pulses **Otro Escenario**, el botón correspondiente mostrará **COMPLETADO** debajo del título **INICIAR: …** (no reemplaza el título) y quedará deshabilitado. Es una señal clara de que ese bloque ya terminaste.

Los bloques que **aún no te tocan** en tu orden aparecen deshabilitados **sin** ese subtítulo; el que podés iniciar ahora es el único bloque real habilitado.

En la condición **AGENTE DE TEXTO** verás el chat abajo (sin avatar a la derecha):

![Condición B, chat sin avatar](docs/figures/figura_06_condicion_b.png)

En la condición **AGENTE VIRTUAL** verás el avatar a la derecha y podés escribir en el chat; el personaje también habla en voz alta:

![Condición C, avatar y chat](docs/figures/figura_07_condicion_c.png)

---

## Por cada pregunta

1. Leé el escenario (reglas + situación). Podés desplazarte con la rueda del ratón si el texto es largo.
2. Elegí **A, B, C o D** (en el build aparecen como opciones de prioridad u otras etiquetas según el ítem).
3. Pulsá **SIGUIENTE**.
4. Elegí **1 a 7 estrellas** (qué tan seguro estás de tu respuesta). Las estrellas seleccionadas se ven en cyan brillante; las demás, apagadas.
5. Pulsá **entregar**.

![Pantalla de pregunta](docs/figures/figura_05_condicion_a_pregunta.png)

![Panel de confianza](docs/figures/figura_05_condicion_a_confianza.png)

En condiciones con agente podés usar el **chat** entre preguntas. El asistente **no te dirá la respuesta correcta**; solo ayuda a entender las reglas.

---

## Entre condiciones (A, B y C)

Al terminar las **6 preguntas** de un bloque **real** aparece esta pantalla:

![Pantalla final](docs/figures/figura_08_final.png)

- Pulsá **Otro Escenario** para volver a la selección y pasar al siguiente bloque de tu orden.
- **No** pulses **Finalizar** hasta completar las **tres condiciones reales** (salvo que el investigador te indique lo contrario).
- **Otro Escenario** mantiene el **mismo ID de sesión** y el mismo archivo de datos en la VM.

---

## ID de sesión

- Aparece en pantalla al iniciar cada escenario (ej.: `Participante: P03 · Sesión: ID-20260601193008-4821`).
- El **código de participante** lo asigna el investigador; el **ID de sesión** es técnico y distingue archivos si hubiera repetición.
- Anotalos solo si el investigador te lo pidió (para confirmar que terminaste la sesión).

---

## Tras cada bloque real (A, B o C)

Cuando termines las **6 preguntas** de un bloque **real**, el investigador te enviará un **cuestionario en línea** (Google Forms) sobre tu experiencia en **ese** bloque. Usá el **mismo código de participante** que ingresaste al inicio en la aplicación.

| Bloque que terminaste | Cuestionario |
|----------------------|--------------|
| Sin asistencia (A) | Corto: carga mental de la tarea |
| Chat (B) | Experiencia con el sistema de texto |
| Avatar (C) | Experiencia con el agente virtual |

Completalo sin prisa antes de pasar al siguiente bloque (o antes de cerrar la sesión si era el último).

---

## Al terminar las tres condiciones

1. Confirmá con el investigador que completaste los **tres bloques reales** y los **cuatro cuestionarios** (perfil + tres post-bloque).
2. Pulsá **Finalizar** en la aplicación para cerrarla.
3. Avisá al investigador e indicá tu **código de participante** si te lo pidieron.
4. **Cerrá la conexión a la VM** según las instrucciones del investigador.

Los datos quedan guardados automáticamente en la VM (`CSV data/`). **No** necesitás copiar ni enviar archivos desde tu computador personal. La práctica **no** se guarda en el registro.

Si hubo errores al guardar o el chat no respondió, comunicáselo al investigador con el **ID de sesión** y la hora aproximada.

---

## Si algo falla

| Problema | Qué hacer |
|----------|-----------|
| Aviso al pulsar *entregar* | Cerrá Excel u otro programa en la VM que pueda tener abierto el CSV. Volvé a pulsar *entregar*. |
| Chat sin respuesta | Esperá unos segundos e intentá de nuevo; avisá al investigador si persiste. |
| Avatar no se ve o no hay voz | Confirmá que elegiste **AGENTE VIRTUAL**; reiniciá la app en la VM. |
| Ventana congelada | Cerrá la app y volvé a abrirla; contactá al investigador con el ID de sesión. |
| Botón de encuesta gris en la app | Los cuestionarios se envían por **enlace del investigador** tras cada bloque; no dependés del botón «Realizar Encuesta». |
| No podés conectar a la VM | Contactá al investigador; no intentes instalar el experimento en tu PC. |
| Pulsaste un bloque que aún no te toca | Solo el bloque **siguiente** de tu orden está habilitado; los demás aparecen en gris. Usá **PRÁCTICA** cuando quieras (una vez). |
| Pulsaste un bloque que ya habías completado | Volvé con **Otro Escenario**; los bloques terminados aparecen como **COMPLETADO** y no se pueden repetir. |

---

## Privacidad

- Los escenarios son **ficticios**; no ingreses datos personales reales en el chat.
- Usá solo la VM provista; no grabes pantalla ni compartas credenciales de acceso.

---

*Prototipo de investigación, UCR, PF-3311.*
