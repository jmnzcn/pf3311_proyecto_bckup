# Despliegue en AWS — acceso por un solo enlace (navegador)

Tu experimento es un **`.exe` de Unity para Windows** (Gemini + Azure TTS + avatar 3D). **No** se puede publicar como página web normal (S3, GitHub Pages, etc.) sin reescribir todo el proyecto a WebGL — y WebGL **no** soporta bien Azure Speech ni el build actual.

Lo que sí funciona: **una VM Windows en AWS** y que el participante abra **un enlace en Chrome/Edge** que muestra el escritorio remoto (NICE DCV en el navegador).

---

## Configuración recomendada — latencia mínima (PF-3311)

Esta es la **mejor configuración práctica** para participantes en **Costa Rica / Centroamérica** con el código actual del proyecto (Azure en `eastus`, Gemini vía HTTPS).

### Resumen ejecutivo

| Componente | Elección óptima | Por qué |
|------------|-----------------|--------|
| **Región AWS** | **`us-east-1`** (N. Virginia) | Coincide con **Azure Speech `eastus`** (ya en `AzureLipSync.cs`); menos RTT servidor↔TTS que São Paulo→Virginia |
| **Tipo de instancia** | **`g5.xlarge`** (mejor) o **`g4dn.xlarge`** (buena relación costo/latencia) | GPU **NVENC** para codificar el stream DCV sin saturar CPU; Unity 3D + avatar fluido |
| **Streaming** | **NICE DCV** + **QUIC (UDP)** | Menor latencia percibida que solo TCP/RDP clásico |
| **Red EC2** | **Enhanced networking (ENA)** activo + **Elastic IP** | Ruta estable; el enlace no cambia al reiniciar |
| **Resolución** | **1920×1080 fija** en VM y en cliente DCV | Evita reescalado (= lag visual) |
| **Participantes** | Chrome/Edge, **Wi‑Fi estable o cable**, sin VPN | La latencia CR→Virginia (~60–90 ms RTT) no la elimina AWS; sí evitás empeorarla |

> **¿Por qué no `sa-east-1` (São Paulo)?** Para el participante puede parecer ~20–30 ms más cerca en el stream, pero cada respuesta de voz en condición **C** iría São Paulo → **Azure East US** (~+80–120 ms por síntesis). Con chat + TTS, **`us-east-1` gana en latencia total**.

### Especificación de lanzamiento EC2 (copiar en consola)

| Campo | Valor **latencia mínima** |
|-------|---------------------------|
| **Region** | `us-east-1` |
| **AMI** | Microsoft Windows Server 2022 Base |
| **Instance type** | **`g5.xlarge`** (4 vCPU, 16 GiB, NVIDIA A10G) |
| **Alternative** | `g4dn.xlarge` (T4; ~USD 0,53/h vs ~USD 1,01/h en g5) |
| **EBS** | **100 GB gp3**, **3000 IOPS** (default gp3 alcanza) |
| **Network** | **Enable** ENA (viene en G-family) |
| **Placement** | Default AZ (una sola AZ; no hace falta multi-AZ) |
| **Elastic IP** | Asignar y usar siempre la misma IP en el enlace |
| **Credit specification** | N/A (instancias G no son burstable) |

**Security group (inbound)** — incluir **UDP** para QUIC:

| Tipo | Protocolo | Puerto | Origen |
|------|-----------|--------|--------|
| Custom TCP | TCP | **8443** | `0.0.0.0/0` |
| Custom UDP | **UDP** | **8443** | `0.0.0.0/0` |
| RDP | TCP | 3389 | Solo tu IP (setup) |

Sin UDP 8443, DCV cae a TCP y la latencia del stream **sube**.

---

### Ajustes NICE DCV (serv en la VM)

Tras instalar DCV, editá (como Administrador):

`C:\Program Files\NICE\dcv\server\conf\dcv.conf`

Añadí o verificá:

```ini
[display]
# No superar la resolución nativa del build Unity
max-head-resolution=(1920, 1080)

[connectivity]
# QUIC = menor latencia en navegador (requiere UDP 8443 en security group)
enable-quic-frontend=true
quic-port=8443

[session-management]
# Una sesión de consola; coherente con Force Single Instance del .exe
create-session=true
```

Reiniciá el servicio **NICE DCV Server** (`services.msc`).

**En el navegador del participante (DCV web client):**

- Preferir **Chrome** o **Edge** recientes.
- En ajustes de calidad del cliente DCV: priorizar ** fluidez / lower latency** sobre máxima calidad visual (si el menú lo ofrece).
- **No** ampliar ventana por encima de 1920×1080.

---

### Alineación de APIs (Gemini + Azure)

| Servicio | Configuración en tu build | Acción |
|----------|---------------------------|--------|
| **Azure TTS** | `region = "eastus"` en `AzureLipSync` | **Dejar `eastus`**; VM en `us-east-1` |
| **Gemini** | `generativelanguage.googleapis.com` | Sin cambio; llamadas salen desde `us-east-1` |
| **Google Forms** | URLs en `QuestionManager` | Se abren en el navegador dentro de DCV; latencia irrelevante |

**No cambies** la región Azure a `brazilsouth` salvo que muevas la VM a `sa-east-1` **y** recompiles el build — hoy el proyecto está optimizado para **Virginia + East US**.

---

### Windows en la VM (menos ruido = menos microcortes)

En PowerShell (Administrador), opcional pero recomendable:

```powershell
# Plan de energía alto rendimiento
powercfg /setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c

# Desactivar animaciones (menos trabajo GPU/CPU fuera del juego)
Set-ItemProperty -Path "HKCU:\Control Panel\Desktop" -Name UserPreferencesMask -Value ([byte[]](0x90,0x12,0x03,0x80,0x10,0x00,0x00,0x00))
```

Desactivá **Windows Update** automático **durante sesiones** (pausar updates en Settings).

---

### Qué mide cada capa de latencia

```
[Laptop CR] --~60-90 ms RTT--> [EC2 us-east-1 + DCV] --~1-5 ms--> [Azure eastus TTS]
                                      |
                                      +--~20-80 ms--> [Gemini API]
```

| Capa | Meta orientativa | Cómo mejorarla |
|------|------------------|----------------|
| Stream DCV (ratón/teclado/imagen) | < 100 ms percibido | g5/g4dn + QUIC + UDP 8443 + cable participante |
| Respuesta Gemini (cond. B/C) | ≤ 5 s en 90 % turnos (criterio del estudio) | VM en `us-east-1`; internet estable en servidor |
| TTS + labios (cond. C) | Audio en < 2 s tras texto | Azure `eastus` + misma región EC2 |

---

## Recomendación general (≈15 participantes)

| Opción | Un solo link | Latencia | Costo orientativo |
|--------|--------------|----------|-------------------|
| **EC2 + NICE DCV** (esta guía) | Sí | **Mejor control** | g5: ~USD 25–55 estudio* · g4dn: ~USD 15–40 |
| **Amazon AppStream 2.0** | Sí | Buena (fleet g4dn/g5) | Mayor setup y costo fijo |
| **EC2 + RDP clásico** | No | Peor en web | Similar |

\* ~25 h encendida + EBS. **Stop** la instancia entre días.

**Importante:** **Force Single Instance** → **un participante activo por VM**. Agendar P01…P15 o varias VMs si hay solapamiento.

---

## Arquitectura (EC2 + DCV)

```
Participante (laptop, Chrome, Costa Rica)
        │
        │  https://ELASTIC-IP:8443  (TCP + QUIC/UDP)
        ▼
┌───────────────────────────────┐
│  EC2 us-east-1 · g5.xlarge    │
│  NICE DCV (GPU encode)        │
│  ExperimentPrototype….exe     │
│  CSV data/                    │
└───────────────┬───────────────┘
                ├──► Gemini (HTTPS)
                └──► Azure Speech eastus
```

---

## Pasos de despliegue

### Paso 1 — Cuenta y región

1. Cuenta AWS con billing activo.
2. Consola en **`us-east-1`** (arriba a la derecha). **Todos** los recursos en esa región.

### Paso 2 — Crear instancia EC2

Usá la tabla **Especificación de lanzamiento** de arriba (`g5.xlarge` preferido).

Tras **Launch** → **Running** → asigná **Elastic IP** (EC2 → Elastic IPs → Allocate → Associate to instance).

### Paso 3 — Security group

Reglas de la sección **latencia mínima** (TCP **y UDP** 8443).

### Paso 4 — Contraseña Windows

EC2 → Connect → RDP → Get password (con `.pem`). Entrá **una vez por RDP** para instalar DCV y el build.

### Paso 5 — Instalar NICE DCV

PowerShell **Administrador**:

```powershell
$base = "https://d1uj6qtbmh3dt5.cloudfront.net/2024.0/Servers"
$dcv = "$env:TEMP\NiceDcvServer.msi"
Invoke-WebRequest -Uri "$base/nice-dcv-server-x64-Release-2024.0-0-0.msi" -OutFile $dcv
Start-Process msiexec.exe -Wait -ArgumentList "/i `"$dcv`" /quiet /norestart"
Invoke-WebRequest -Uri "$base/nice-dcv-web-viewer-x64-Release-2024.0-0-0.msi" -OutFile "$env:TEMP\dcv-viewer.msi"
Start-Process msiexec.exe -Wait -ArgumentList "/i `"$env:TEMP\dcv-viewer.msi`" /quiet /norestart"
```

Aplicá **`dcv.conf`** de la sección anterior y reiniciá la VM.

Documentación: [NICE DCV on Amazon EC2](https://docs.aws.amazon.com/dcv/latest/adminguide/setting-up-installing.html).

### Paso 6 — Build Unity

1. `_tools\build_windows.bat` → `Build/Windows/`.
2. Claves Gemini/Azure en el build (no en GitHub).
3. Subir zip a `C:\Experimento\` y descomprimir.
4. `New-Item -ItemType Directory -Force -Path "C:\Experimento\CSV data"`.

### Paso 7 — Enlace participantes

```text
https://TU-ELASTIC-IP:8443
```

Usuario Windows dedicado (`participante`, sin admin). Certificado autofirmado → **Avanzado → continuar**.

### Paso 8 — Prueba de latencia (obligatoria)

Desde **otra red** (idealmente la misma ciudad que los participantes):

1. Abrí el enlace DCV, mové el ratón y escribí en el chat (cond. B).
2. Cond. C: verificá que voz + labios no van “a tirones”.
3. En la VM, revisá `ChatHelpRating_*.csv` → `GeminiLatencySeconds` (meta ≤ 5 s).
4. Si el stream se siente pesado: confirmá **UDP 8443**, **g5/g4dn**, plan de energía alto rendimiento.

Si g5.xlarge no mejora vs g4dn.xlarge en tu prueba, bajá a **g4dn.xlarge** (ahorro ~50 %).

---

## Operación (15 participantes)

1. **Start** instancia 5 min antes de la sesión.
2. **Un participante por VM** a la vez.
3. Tras cada sesión: respaldar `CSV data/` → borrar pruebas.
4. **Stop** instancia al terminar el día.
5. Rotar claves API al cerrar el estudio.

---

## Checklist pre-estudio

- [ ] Región **`us-east-1`**, tipo **`g5.xlarge`** (o g4dn probado)
- [ ] Elastic IP + security group **TCP y UDP 8443**
- [ ] `dcv.conf` con QUIC + 1920×1080
- [ ] Azure **`eastus`**, build 1920×1080, Force Single Instance
- [ ] Prueba humo 20 min **solo vía enlace DCV** (no RDP)
- [ ] Instrucción a participantes: Chrome, sin VPN, Wi‑Fi estable

---

## Costos

| Recurso | g5.xlarge (~USD 1,01/h) | g4dn.xlarge (~USD 0,53/h) |
|---------|-------------------------|---------------------------|
| 25 h sesiones | ~USD 25 | ~USD 13 |
| EBS 100 GB/mes | ~USD 8 | ~USD 8 |

**Stop** = no pagás GPU/CPU; sí EBS.

---

## Alternativa: AppStream 2.0

Fleet **`g5.xlarge`** o **`g4dn.xlarge`** en **`us-east-1`**, protocolo streaming AppStream. Latencia comparable si el fleet usa GPU; setup más largo. Ver [AppStream 2.0](https://docs.aws.amazon.com/appstream2/latest/developerguide/what-is-appstream.html).

---

## Qué NO hacer (latencia)

| Evitar | Efecto |
|--------|--------|
| `t3` / sin GPU | Codificación por CPU; stream lento |
| Solo TCP 8443 (sin UDP) | Sin QUIC; más lag |
| VM en São Paulo + Azure `eastus` | TTS lento en cond. C |
| Resolución > 1920×1080 en stream | Más píxeles = más delay |
| Participante con VPN | +50–200 ms RTT |

---

## Próximo paso

1. ¿Cuenta AWS lista?
2. ¿Presupuesto permite **g5.xlarge** (~USD 1/h) o preferís **g4dn.xlarge**?
3. ¿Actualizo **`GUIA_PARTICIPANTE.md`** con enlace DCV + tips de red?
