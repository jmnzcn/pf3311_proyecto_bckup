# Guía para participantes - ExperimentPrototypeB03230

Instrucciones para completar el experimento **conectándote a la máquina virtual** que te indique el investigador. No necesitás instalar ni descargar nada en tu computador personal.

Detalle técnico del sistema: [README.md](README.md).

---

## Antes de empezar

1. **Conexión:** Usá el enlace o credenciales (usuario/contraseña) que te envió el investigador para entrar a la **máquina virtual Windows**.
2. **Pantalla:** En la VM, la ventana del experimento debe verse a **1920×1080** (pantalla completa o ventana maximizada).
3. **Internet:** La VM debe tener conexión estable (necesaria para condiciones con agente de texto o avatar).
4. **No abras** archivos en la carpeta `CSV data/` ni uses Excel en la VM antes ni durante la sesión.
5. Reservá **60–75 minutos** sin interrupciones.
6. El investigador te indicará el **orden de las condiciones** (A, B, C) y, al final, el enlace de la **encuesta** si aplica.

---

## Cómo iniciar

1. En la VM, abrí **`ExperimentPrototypeB03230.exe`** (una sola vez; no hagas doble clic).
2. Marcá la casilla de **consentimiento** y pulsá continuar.

![Consentimiento informado](docs/figures/figura_03_consentimiento.png)

3. Seguí el orden de condiciones que te indicaron y pulsá el botón INICIAR correspondiente:

![Selección de condición](docs/figures/figura_04_seleccion.png)

| Botón | Qué incluye |
|-------|-------------|
| **INICIAR: SIN ASISTENCIA** | 6 preguntas. Sin chat ni avatar. |
| **INICIAR: AGENTE DE TEXTO** | 6 preguntas + chat con agente. |
| **INICIAR: AGENTE VIRTUAL** | 6 preguntas + chat + avatar que habla. |

En la condición **AGENTE DE TEXTO** verás el chat abajo (sin avatar a la derecha):

![Condición B, chat sin avatar](docs/figures/figura_06_condicion_b.png)

En la condición **AGENTE VIRTUAL** verás el avatar a la derecha y podés escribir en el chat; el personaje también habla en voz alta:

![Condición C, avatar y chat](docs/figures/figura_07_condicion_c.png)

---

## Por cada pregunta

1. Leé el escenario (reglas + situación).
2. Elegí **A, B, C o D** (en el build aparecen como opciones de prioridad u otras etiquetas según el ítem).
3. Pulsá **SIGUIENTE**.
4. Elegí **1 a 7 estrellas** (qué tan seguro estás de tu respuesta).
5. Pulsá **entregar**.

![Pantalla de pregunta](docs/figures/figura_05_condicion_a_pregunta.png)

![Panel de confianza](docs/figures/figura_05_condicion_a_confianza.png)

En condiciones con agente podés usar el **chat** entre preguntas. El asistente **no te dirá la respuesta correcta**; solo ayuda a entender las reglas.

---

## Entre condiciones (A, B y C)

Al terminar las **6 preguntas** de un bloque aparece esta pantalla:

![Pantalla final](docs/figures/figura_08_final.png)

- Pulsá **Otro Escenario** para pasar a la siguiente condición.
- **No** pulses **Finalizar** hasta completar las tres condiciones (salvo que el investigador te indique lo contrario).
- **Otro Escenario** mantiene el **mismo ID de sesión** y el mismo archivo de datos en la VM.

---

## ID de sesión

- Aparece en pantalla al iniciar cada escenario (ej.: `ID-20260601193008-4821`).
- Anotalo solo si el investigador te lo pidió (para confirmar que terminaste la sesión).

---

## Al terminar las tres condiciones

1. Pulsá **Realizar Encuesta** si el botón está activo (enlace configurado por el investigador).
2. Pulsá **Finalizar** para cerrar la aplicación.
3. Avisá al investigador que completaste la sesión e indicá tu **ID de sesión** si te lo pidieron.
4. **Cerrá la conexión a la VM** según las instrucciones del investigador.

Los datos quedan guardados automáticamente en la VM (`CSV data/`). **No** necesitás copiar ni enviar archivos desde tu computador personal.

Si hubo errores al guardar o el chat no respondió, comunicáselo al investigador con el **ID de sesión** y la hora aproximada.

---

## Si algo falla

| Problema | Qué hacer |
|----------|-----------|
| Aviso al pulsar *entregar* | Cerrá Excel u otro programa en la VM que pueda tener abierto el CSV. Volvé a pulsar *entregar*. |
| Chat sin respuesta | Esperá unos segundos e intentá de nuevo; avisá al investigador si persiste. |
| Avatar no se ve o no hay voz | Confirmá que elegiste **AGENTE VIRTUAL**; reiniciá la app en la VM. |
| Ventana congelada | Cerrá la app y volvé a abrirla; contactá al investigador con el ID de sesión. |
| Botón de encuesta gris | Completá las tres condiciones; si sigue gris, el investigador enviará el enlace por otro medio. |
| No podés conectar a la VM | Contactá al investigador; no intentes instalar el experimento en tu PC. |

---

## Privacidad

- Los escenarios son **ficticios**; no ingreses datos personales reales en el chat.
- Usá solo la VM provista; no grabes pantalla ni compartas credenciales de acceso.

---

*Prototipo de investigación, UCR, PF-3311.*
