# Pendientes del frontend — Fase 3

**Rama:** `integracion_fronEnd` · **Actualizado:** 02/10/2026
**Estado de la rama:** compila con **0 errores y 0 advertencias** · **32/32 pruebas** correctas

---

## Dónde estamos

Las tres ramas del frontend y el arreglo del esqueleto están **mergeadas** en `integracion_fronEnd`:

| Rama | Persona | Merge | Conflictos |
|---|---|---|---|
| `fix/ECAR-206-guardado-fe0` | Juan Alberto (FE-0) | fast-forward | — |
| `feature/ECAR-205-pantalla-ejecucion-preguntas` | Erica (FE-1) | `bb30c50` | 2 archivos |
| `feature/F3-Santiago` | Santiago (FE-2) | `db8866d` | 1 archivo |
| `feature/qr` | Gary (FE-3) | `2c771cf` | ninguno |

Las cuatro pantallas de la Fase 3 existen de punta a punta: ejecución con sus tres pasos, inicio
desde QR y resultado firmado.

**Lo que no se puede afirmar todavía es que funcionen.** Faltan en `develop` los endpoints de
respuestas, evidencias, firma y resultado, que están en cuatro ramas de backend sin mergear. Nadie ha
ejecutado una inspección completa.

---

## Pendientes por persona

| Persona | Documento | Defecto propio | Pendientes | Bloqueado por |
|---|---|---|---|---|
| **Juan Alberto** (FE-0) | [`PENDIENTES_FE0_JUAN_ALBERTO.md`](PENDIENTES_FE0_JUAN_ALBERTO.md) | — | 5 de código y docs + 4 de coordinación | — |
| **Erica** (FE-1) | [`PENDIENTES_FE1_ERICA.md`](PENDIENTES_FE1_ERICA.md) | **1** | 4 | Juan David, para probar |
| **Santiago** (FE-2) | [`PENDIENTES_FE2_SANTIAGO.md`](PENDIENTES_FE2_SANTIAGO.md) | ninguno | 4 | Carlos y Alejandro |
| **Gary** (FE-3) | [`PENDIENTES_FE3_GARY.md`](PENDIENTES_FE3_GARY.md) | **1** | 4 | Simón, para probar |

### El defecto real de cada uno, en una línea

- **Gary** — un error de negocio o de red en `IniciarInspeccion` manda al técnico al login. Con un
  equipo dado de baja, queda en bucle: login, vuelve, 400, login. **Es el más grave del frontend.**
- **Erica** — la observación solo se ve cuando la pregunta ya es novedad. Si el técnico corrige "No"
  → "Sí", el texto queda guardado e invisible y acaba en el resultado firmado, que es inmutable.
- **Santiago** — ninguno en su código. Sus dos pendientes grandes esperan cambios de backend. Pero
  tiene uno pequeño que **es urgente porque bloquea a otro**: borrar `CreateEvidenciaAsync`, sin lo
  cual la rama de Alejandro no compila contra la integración.

---

## Qué hacer primero, en orden

1. **Borrar la rama `Integracion_fronEnd` (con I mayúscula).** Es un duplicado que en Windows choca
   con la buena y hace que el `checkout` de todos los documentos apunte a la rama equivocada. No
   tiene nada que no esté ya en la buena. → FE-0
2. **Santiago borra `CreateEvidenciaAsync`.** Quince minutos, y desbloquea a Alejandro.
3. **Gary arregla `IniciarInspeccion`.** Es el defecto que deja a un técnico atascado en planta.
4. **Erica saca la observación del `@if`.**
5. **Avisar a Juan David y a Alejandro** de lo que tienen roto antes de mergear. → FE-0

Los pasos 2, 3 y 4 son independientes entre sí y no dependen del backend.

---

## Bloqueos de backend que afectan al frontend

| Quién | Qué | A quién bloquea |
|---|---|---|
| Juan David (BE-1) | Su rama **no compila**: 4 × `CS7036` en `BackendPhaseThreeTests.cs` | Erica (no puede entrar a `develop`) |
| Alejandro (BE-2) | Su rama **borra la migración `Fase3BaseInspecciones`** y la recrea con otro nombre; romperá cualquier base de datos ya migrada | Santiago (subida real de fotos) |
| Carlos (BE-0) | `GET /api/inspecciones` no acepta `estado` | Santiago (filtro honesto) |
| Simón (BE-3) | `firma-inmutable` sin mergear | Gary (firmar y ver resultado) |

El detalle de cada uno, con líneas y evidencia, está en
[`PENDIENTES_FE0_JUAN_ALBERTO.md`](PENDIENTES_FE0_JUAN_ALBERTO.md) §D.
