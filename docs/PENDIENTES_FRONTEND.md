# Pendientes del frontend — Fase 3

**Rama:** `integracion_fronEnd` · **Actualizado:** 02/10/2026
**Estado de la rama:** compila con **0 errores y 0 advertencias** · **32/32 pruebas** correctas

---

## Resumen

**Todo lo que el frontend podía hacer sin depender del backend está hecho.** Lo que queda son
pruebas en ejecución y cambios que esperan endpoints que todavía no están en `develop`.

Las tres ramas del frontend están mergeadas en esta rama, y el 02/10 se resolvieron sobre ella los
pendientes de código de las cuatro personas:

| Commit | Persona | Qué se resolvió |
|---|---|---|
| `43f6eba` | Erica (FE-1) | Observación siempre visible, obligatoria solo en novedad · `StateHasChanged` pelado retirado |
| `b1d1c85` | Santiago (FE-2) | `CreateEvidenciaAsync` retirado (desbloquea BE-2) · filtro de estado honesto · `DataLabel` en las tablas para móvil |
| `9396278` | Gary (FE-3) | `IniciarInspeccion` muestra los errores en vez de mandar al login · etiquetas y detalles |
| `f17b02a` | Juan Alberto (FE-0) | `MockDataService` retirado · "Siguiente" dice qué preguntas faltan · sin llamadas al API sin sesión · sangría · `ESTADO_PROYECTO` §4 y guía al día |

Los resolvió FE-0 a petición del líder del front. **Nadie tiene que rehacerlos**: si alguien tiene
cambios locales sobre esos archivos, que actualice antes de seguir.

---

## Lo que queda, por persona

| Persona | Documento | Pendiente | Depende de |
|---|---|---|---|
| **Juan Alberto** (FE-0) | [`PENDIENTES_FE0_JUAN_ALBERTO.md`](PENDIENTES_FE0_JUAN_ALBERTO.md) | Borrar la rama duplicada · coordinar backend · orden de merge · prueba completa | — |
| **Erica** (FE-1) | [`PENDIENTES_FE1_ERICA.md`](PENDIENTES_FE1_ERICA.md) | Prueba manual con recarga | Juan David (BE-1) |
| **Santiago** (FE-2) | [`PENDIENTES_FE2_SANTIAGO.md`](PENDIENTES_FE2_SANTIAGO.md) | Filtro de estado en servidor · ocultar "Eliminar" en cerradas · prueba con teléfono | Carlos (BE-0) y Alejandro (BE-2) |
| **Gary** (FE-3) | [`PENDIENTES_FE3_GARY.md`](PENDIENTES_FE3_GARY.md) | Pruebas de firma, resultado y QR desde el teléfono | Simón (BE-3) |

---

## Bloqueos de backend

| Quién | Qué | Bloquea a |
|---|---|---|
| Juan David (BE-1) | Su rama **no compila**: 4 × `CS7036` en `BackendPhaseThreeTests.cs` (140, 165, 211, 239) | Erica |
| Alejandro (BE-2) | Su rama **borra la migración `Fase3BaseInspecciones`** y la recrea con otro nombre; rompería cualquier base de datos ya migrada. Con `b1d1c85`, en cambio, **ya compila** contra esta rama | Santiago |
| Carlos (BE-0) | `GET /api/inspecciones` no acepta `estado` | Santiago |
| Simón (BE-3) | `firma-inmutable` sin mergear | Gary |

Detalle y evidencia en [`PENDIENTES_FE0_JUAN_ALBERTO.md`](PENDIENTES_FE0_JUAN_ALBERTO.md) §C.

---

## Antes de hacer `checkout`

En GitHub existe una segunda rama, **`Integracion_fronEnd` con I mayúscula**, que **no** es esta.
En Windows las dos chocan porque el disco no distingue mayúsculas. Hasta que se borre, comprueba:

```bash
git ls-remote origin integracion_fronEnd
```

Si el commit es `96def9f`, estás viendo la equivocada.
