# Guía de frontend — Fase 3 (Inspecciones y Evidencias)

Entrega del líder de frontend (FE-0) para FE-1, FE-2 y FE-3.
Todo lo que está aquí ya compila y está en `develop`: los DTOs, los métodos de
`HttpClientService`, el esqueleto de la pantalla de ejecución y la guarda de ruta.
Los endpoints los publica backend en paralelo; hasta que existan, cada método devuelve
`null` o `Success = false` y la pantalla debe avisarlo con `Snackbar` — **nunca** dejar un
control vacío sin explicación.

Referencias: [`PLAN_FASE3_TAREAS.md`](PLAN_FASE3_TAREAS.md) (tareas y fechas),
[`GUIA_FRONTEND_FASE2.md`](GUIA_FRONTEND_FASE2.md) (§3, reglas de revisión, siguen vigentes).

---

## 1. Métodos nuevos en `HttpClientService`

| Método | Endpoint | Devuelve | Lo usa |
|---|---|---|---|
| `IniciarInspeccionAsync(dto)` | `POST /api/inspecciones/iniciar` | `ApiResponse<InspeccionEjecucionDto>` | FE-3 |
| `GetInspeccionEjecucionAsync(id)` | `GET /api/inspecciones/{id}/ejecucion` | `ApiResponse<InspeccionEjecucionDto>` | FE-0 (esqueleto) |
| `GuardarRespuestasAsync(id, dto)` | `PUT /api/inspecciones/{id}/respuestas` | `ApiResponse<List<RespuestaInspeccionDto>>` | FE-1 |
| `GetRespuestasInspeccionAsync(id)` | `GET /api/inspecciones/{id}/respuestas` | `ApiResponse<List<RespuestaInspeccionDto>>` | FE-1 |
| `GetRespuestasInspeccionAsync(page, pageSize, search, idInspeccion)` | `GET /api/respuestasinspeccion` | `ApiResponse<PagedResultDto<RespuestaInspeccionDto>>` | FE-1 (pantalla admin) |
| `GetMisInspeccionesAsync(estado)` | `GET /api/inspecciones/mias` | `ApiResponse<List<InspeccionDto>>` | FE-2 |
| `UploadEvidenciaAsync(id, IBrowserFile)` | `POST /api/inspecciones/{id}/evidencias` | `ApiResponse<EvidenciaDto>` | FE-2 |
| `GetEvidenciasInspeccionAsync(id)` | `GET /api/inspecciones/{id}/evidencias` | `ApiResponse<List<EvidenciaDto>>` | FE-2 |
| `GetEvidenciaImageAsync(id)` | `GET /api/evidencias/{id}/archivo` | `byte[]?` | FE-2 |
| `GetEvidenciaImageDataUrlAsync(id, tipoContenido)` | (envuelve el anterior) | `string?` listo para `MudImage Src` | FE-2, FE-3 |
| `DeleteEvidenciaAsync(id)` | `DELETE /api/evidencias/{id}` | `ApiResponse<bool>` | FE-2 |
| `FirmarInspeccionAsync(id, dto)` | `POST /api/inspecciones/{id}/firmar` | `ApiResponse<InspeccionResultadoDto>` | FE-3 |
| `GetInspeccionResultadoAsync(id)` | `GET /api/inspecciones/{id}/resultado` | `ApiResponse<InspeccionResultadoDto>` | FE-3 |

Cuatro detalles que no son obvios:

- **`IniciarInspeccionAsync` puede devolver `Success = false` con datos.** Si el técnico ya
  tiene una inspección en curso para ese equipo, el API responde **409 con la inspección
  existente en `Data`**. FE-3 debe continuarla, no mostrar un error.
- **La imagen de la evidencia no se puede poner en un `<img src>` directo.** El endpoint exige
  token Bearer y una etiqueta `<img>` no lo envía. Por eso existe `GetEvidenciaImageDataUrlAsync`.
  Mismo patrón que el QR de Fase 2.
- **`UploadEvidenciaAsync` es multipart**, no JSON. Recibe el `IBrowserFile` tal cual sale de
  `MudFileUpload`; el límite de lectura es `HttpClientService.MaxEvidenciaBytes` (5 MB).
- **`FirmarInspeccionAsync` detalla los faltantes en `Errors`.** Cuando el API rechaza la firma
  por preguntas obligatorias sin responder, la lista viene en `result.Errors`, no en `Message`.

---

## 2. DTOs

Todos en `ECAR.Shared/DTOs`. **Ninguno se modifica sin acuerdo entre BE-0 y FE-0**: los tres
equipos de frontend construyen sobre ellos.

| DTO | Para qué |
|---|---|
| `IniciarInspeccionDto` | `{ IdEquipo, IdChecklist }`. No lleva inspector: lo toma el API del token |
| `InspeccionEjecucionDto` | Estado completo: cabecera + `Preguntas` + `Evidencias` + contadores |
| `PreguntaEjecucionDto` | Pregunta **con** su respuesta actual. `Respuesta == null` = sin responder |
| `GuardarRespuestasDto` / `RespuestaEjecucionDto` | Lote de respuestas para el upsert |
| `FirmarInspeccionDto` | `{ FirmaPngBase64, Observaciones }` |
| `InspeccionResultadoDto` | Inspección cerrada en modo lectura, con firma y hash |
| `InspeccionEstados` / `InspeccionResultados` | Constantes en `ECAR.Shared`. **Nunca escribir `"Cerrada"` a mano** |

`PreguntaEjecucionDto` trae dos propiedades calculadas que evitan repetir las reglas del SRS
en cada pantalla:

```csharp
pregunta.FaltaResponder   // Obligatoria y sin respuesta  → regla 3 del SRS
pregunta.EsNovedad        // Sí/No respondida con "No"    → regla 4 del SRS
```

`InspeccionEjecucionDto` trae `EstaCerrada` y `PuedeFirmar`.

---

## 3. El esqueleto de ejecución

`Pages/EjecutarInspeccion.razor` (`/inspecciones/ejecutar/{IdInspeccion:long}`) ya está hecho
y **tiene un único dueño: FE-0**. Carga la inspección una sola vez, dibuja la cabecera y un
`MudStepper` de tres pasos, y reparte el DTO a tres componentes:

| Paso | Componente | Dueño |
|---|---|---|
| 1. Preguntas | `Components/PasoPreguntas.razor` | FE-1 |
| 2. Evidencias | `Components/PasoEvidencias.razor` | FE-2 |
| 3. Firma | `Components/PasoFirma.razor` | FE-3 |

**Cada persona toca solo su componente.** Si los tres editaran la página contenedora, los tres
PR del jueves chocarían sobre el mismo archivo.

Cada componente ya trae resueltos el contrato con la página y las llamadas al API; lo que
falta es la interfaz. Los métodos listos para usar:

```csharp
// PasoPreguntas  (FE-1)
await GuardarAsync(respuestas);          // lote o una sola respuesta; refresca contadores

// PasoEvidencias (FE-2)
await SubirAsync(archivo);               // valida tamaño, sube y añade a la lista
await EliminarAsync(evidencia);
await CargarMiniaturaAsync(evidencia);   // devuelve el data URI para MudImage

// PasoFirma      (FE-3)
await FirmarAsync(firmaPng, observaciones);   // acepta el PNG con o sin prefijo data:
TextoLegal                                     // texto ya redactado, mostrarlo antes de firmar
```

Los parámetros de entrada y salida de cada componente ya están declarados; no hace falta
añadir ninguno para las tareas de la fase.

### Decisión de diseño: el resultado es una página, no un paso

El plan hablaba de cuatro pasos. El cuarto, **Resultado, es una ruta propia**
(`/inspecciones/{id}/resultado`, FE-3) y no un paso del stepper, porque debe poder abrirse
más tarde por URL, imprimirse y consultarla el Auditor. Al firmar, el esqueleto redirige ahí
con `replace: true` para que el botón "atrás" del teléfono no devuelva al formulario.

Además, si `GET ejecucion` devuelve una inspección ya `Cerrada`, el esqueleto redirige solo al
resultado: ninguna pantalla de ejecución puede abrirse sobre una inspección cerrada.

### Validación al avanzar de paso

El esqueleto ya bloquea el avance con `OnPreviewInteraction`:

- quedan preguntas obligatorias sin responder → cancela y avisa (regla 3 del SRS);
- hay novedades sin observación → cancela y avisa (regla 4 del SRS).

FE-1 **no** tiene que repetir esa validación para el botón "Siguiente"; sí debe marcarlo en
la propia pregunta para que el técnico sepa cuál le falta.

---

## 4. Guarda de ruta y vuelta desde el login

`Components/TecnicoRouteGuard.razor` deja pasar a **Técnico y Administrador**. A diferencia de
`AdminRouteGuard`, si no hay sesión **no manda al inicio**: manda al login conservando el
destino, porque a estas pantallas se llega escaneando un QR desde el teléfono.

```razor
<TecnicoRouteGuard>
    ... contenido de la pantalla ...
</TecnicoRouteGuard>
```

`Pages/Login.razor` acepta `?returnUrl=…` y vuelve ahí tras iniciar sesión. FE-3 lo usa en el
botón "Iniciar inspección" de la página pública del QR:

```csharp
NavigationManager.NavigateTo($"/login?returnUrl={Uri.EscapeDataString(destino)}");
```

---

## 5. Criterio visual de la fase

Las tres pantallas nuevas se usan **en un teléfono, de pie, con guantes**. El criterio manda
sobre la estética:

| Elemento | Cómo se muestra |
|---|---|
| Objetivo táctil | Mínimo **44 × 44 px**. Nada de `Size.Small` en botones que el técnico pulsa en campo |
| Estado de la inspección | `MudChip` — `EnCurso` = `Color.Warning`, `Cerrada` = `Color.Success` |
| Pregunta obligatoria sin responder | Borde o icono `Color.Error` y texto "Obligatoria" |
| Novedad (Sí/No = "No") | La observación pasa a `Required="true"` y se muestra en `Color.Warning` |
| Indicador de guardado | Texto discreto junto al paso: "Guardando…" / "Guardado" / "Error al guardar" |
| Miniatura de evidencia | `MudImage` con data URI, `Height="120"`, `ObjectFit="ObjectFit.Cover"` |
| Resultado final | `Conforme` = `Severity.Success`; `ConNovedad` = `Severity.Warning` |
| Firma | Canvas de ancho completo, alto 180 px, fondo blanco, trazo 2 px |
| Acciones destructivas (eliminar foto) | `ConfirmDialog` antes de llamar al API |
| Errores del API | Siempre `Snackbar.Add(result?.Message ?? "…", Severity.Error)` |
| Botones de diálogo | Las clases `dialog-button` / `cancel-btn` que ya usan los modales |

**Antes de pedir revisión, probar a 375 px de ancho** y confirmar que no hay scroll horizontal.
Las pantallas con cámara o firma se prueban además en un dispositivo real.

---

## 6. Reglas de revisión

Siguen las de Fase 2, con dos añadidos:

- **Ningún PR con avisos nuevos.** La solución está hoy en **0 errores y 0 advertencias**;
  se queda así. Ejecutar `dotnet build ECAR.Client/ECAR.Client.csproj --no-incremental` y
  revisar los avisos del archivo tocado antes de abrir el PR.
- **`pageSize` nunca mayor que 100.** Para listas largas, búsqueda en el API.
- **Nada de datos simulados.** Un PR que "funciona con mock mientras backend entrega" no se
  aprueba: es lo que dejó la Fase 2 declarada como completa estando al 60 %.
- **Sin `TODO` dirigidos a otra persona.** Si falta algo de backend, el método devuelve `null`
  y la pantalla lo avisa; eso es suficiente.
- Las pantallas de administración van dentro de `<AdminRouteGuard>`; las de ejecución, dentro
  de `<TecnicoRouteGuard>`.
- Los comentarios del código, en español.

---

## 7. Estado de las tareas de FE-0

| # | Tarea | Estado |
|---|---|---|
| 1 | Sesión de diseño con BE-0 y contrato de los 12 endpoints | ✅ Hecho (`PLAN_FASE3_TAREAS.md` §3.2) |
| 2 | DTOs de Fase 3 | ✅ Hecho |
| 3 | Métodos en `HttpClientService` | ✅ Hecho (tabla de §1) |
| 4 | Esta guía | ✅ Hecho |
| 5 | Esqueleto `EjecutarInspeccion.razor` + los tres componentes de paso | ✅ Hecho |
| 6 | `TecnicoRouteGuard` y `returnUrl` en el login | ✅ Hecho |
| 7 | Retirar `MockDataService` | ⏳ Pendiente de que FE-1 migre Respuestas de inspección |
| 8 | Limpieza de avisos de compilación | ✅ Hecho — de 100 avisos a **0** |
| 9 | Revisión de PR e integración con BE-0 | Continua durante la fase |
| 10 | Actualizar `ESTADO_PROYECTO.md` | ✅ Hecho |

### Lo que se corrigió en la limpieza (tarea 8)

No eran avisos cosméticos:

- **26 × `MUD0002`** — las 13 pantallas paginadas usaban `SelectedPageChanged` en
  `MudPagination`, **parámetro que no existe** (el correcto es `SelectedChanged`). MudBlazor lo
  absorbía en `UserAttributes` sin enlazar nada: **la paginación no recargaba los datos en
  ninguna de las 13 pantallas**. Cambiar de página movía el número y dejaba la tabla igual.
- **72 × `CS8602`** — `var result = await dialog.Result;` devuelve `DialogResult?` y se
  desreferenciaba sin comprobar null; un diálogo cerrado de forma anómala lanzaba
  `NullReferenceException`. Ahora `if (result is not null && !result.Canceled)`.
- **2 × `CS8601`** — asignación de `Descripcion` nullable en `UbicacionModal`.

Es el segundo defecto real que esconde `MUD0002`, después del de asignación de roles en
Fase 1. De ahí la regla de no dejar avisos nuevos.

---

## 8. Dependencias de backend

Estos métodos ya existen en el cliente pero **fallarán hasta que backend publique su
endpoint**. No es un error del front:

| Endpoint | Responsable | Comprometido para |
|---|---|---|
| `POST /api/inspecciones/iniciar`, `GET /api/inspecciones/{id}/ejecucion` | BE-0 | Mié 24/09 |
| `PUT/GET /api/inspecciones/{id}/respuestas`, `GET /api/respuestasinspeccion`, `GET /api/inspecciones/mias` | BE-1 | Jue 25/09 – Lun 28/09 |
| `POST /api/inspecciones/{id}/evidencias`, `GET /api/evidencias/{id}/archivo`, `GET /api/inspecciones/{id}/evidencias` | BE-2 | Lun 28/09 – Mar 29/09 |
| `POST /api/inspecciones/{id}/firmar`, `GET /api/inspecciones/{id}/resultado` | BE-3 | Vie 25/09 – Lun 28/09 |

Mientras tanto se puede maquetar el componente con el DTO en memoria, pero **no se mergea**
una pantalla que dependa de un endpoint inexistente sin avisarlo en el PR.
