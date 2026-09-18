# Plan de trabajo — Fase 3: Inspecciones y Evidencias

**Semanas del cronograma:** 6 y 7 (21 de septiembre – 4 de octubre de 2026)
**Entrega hacia ECAR:** Entrega 4, semana 8 (5 – 11 de octubre): inspecciones, evidencias y firma digital
**Referencias:** SRS §2 (Gestión de Inspecciones), §6 (pantallas de Inspecciones), §8 (reglas 2, 3, 4, 6, 9, 10);
[`ESTADO_PROYECTO.md`](ESTADO_PROYECTO.md) §6 (hallazgos del modelo); [`GUIA_FRONTEND_FASE2.md`](GUIA_FRONTEND_FASE2.md) §3 (reglas de revisión, siguen vigentes)

---

## 1. Objetivo de la fase

Que un **Técnico**, desde un teléfono o tablet, pueda: escanear el QR de un equipo → elegir el
checklist → responder las preguntas → adjuntar fotografías → firmar → cerrar la inspección; y
que esa inspección quede **inmutable** y consultable por el Administrador y el Auditor.

Al terminar la fase, `MockDataService` desaparece del cliente y los controladores de
inspecciones, respuestas y evidencias quedan protegidos por rol.

---

## 2. Equipo y roles

| Código | Persona | Grupo | Rol en la fase |
|---|---|---|---|
| **BE-0** | Carlos Tamayo | Backend | **Líder backend.** Modelo de datos, contrato del API, integración, revisión de PR |
| BE-1 | Juan David López | Backend | Ejecución de inspección y respuestas |
| BE-2 | Alejandro Gómez | Backend | Evidencias fotográficas (almacenamiento y API) |
| BE-3 | Simón | Backend | Firma digital, cierre e inmutabilidad, seguridad transversal |
| **FE-0** | Juan Alberto Zuluaga | Frontend | **Líder frontend.** Servicios HTTP, DTOs, guía visual, integración, revisión de PR |
| FE-1 | Erica Avendaño | Frontend | Pantalla de ejecución: paso de preguntas y respuestas |
| FE-2 | Santiago Arango | Frontend | Paso de evidencias (cámara) y listado de inspecciones |
| FE-3 | Gary | Frontend | Firma digital, resultado final e inicio desde QR |

> La asignación nombre ↔ código se tomó de la revisión de Fase 1 y del historial de git.
> Si algún código no coincide con la persona, ajústese la tabla; las tareas están escritas
> por código.

**Regla de pareja:** cada tarea de frontend tiene una contraparte de backend (BE-1 ↔ FE-1,
BE-2 ↔ FE-2, BE-3 ↔ FE-3). Las parejas acuerdan el contrato con sus líderes **antes** de
escribir código y hacen la prueba de integración juntos.

---

## 3. Decisiones de diseño (base común)

Estas decisiones las cierran BE-0 y FE-0 el **lunes 21/09** en una sesión de una hora, y
quedan escritas aquí. Las demás personas construyen sobre ellas.

### 3.1 Modelo de datos (migración `Fase3Inspecciones`, responsable BE-0)

| Tabla | Cambio | Motivo |
|---|---|---|
| `Inspecciones` | `IdChecklist` (bigint, FK explícita a `Checklists`, obligatoria). Reemplaza la FK sombra `ChecklistIdChecklist` | Saber con qué **versión** de checklist se ejecutó cada inspección |
| `Inspecciones` | `Estado` nvarchar(20): `EnCurso` \| `Cerrada` (default `EnCurso`) | Distinguir borrador de registro histórico (SRS #6) |
| `Inspecciones` | `FechaCierre` datetime2 null | Estampa de tiempo del cierre |
| `Inspecciones` | `FirmaHash` nvarchar(64) null | Huella SHA-256 del contenido firmado (integridad) |
| `Inspecciones` | `FirmaDigital` se mantiene (PNG base64 de la firma manuscrita) | Ya existe en el SRS |
| `Evidencias` | `NombreOriginal` nvarchar(200), `TipoContenido` nvarchar(100), `TamanoBytes` bigint | Metadatos del archivo |
| `Evidencias` | `Archivo` pasa a guardar la **ruta relativa** en el almacenamiento (no el binario) | Evitar `VARCHAR(MAX)` con binarios en SQL |
| `Evidencias` | `UsuarioCarga` pasa a `IdUsuarioCarga` bigint FK a `Usuarios` | Trazabilidad real; el SRS lo define como BIGINT |

Índices: `Inspecciones(IdChecklist)`, `Inspecciones(Estado)`, `Inspecciones(IdUsuario, Estado)`.

### 3.2 Contrato del API (nuevos endpoints)

| # | Endpoint | Rol | Respuesta | Dueño |
|---|---|---|---|---|
| 1 | `POST /api/inspecciones/iniciar` `{ idEquipo, idChecklist }` | Técnico, Admin | `InspeccionEjecucionDto` — crea en `EnCurso` con `IdUsuario` **del token**; 409 si ese usuario ya tiene una en curso para el mismo equipo (la devuelve para continuar) | BE-0 |
| 2 | `GET /api/inspecciones/{id}/ejecucion` | Técnico (propia), Admin, Auditor | `InspeccionEjecucionDto`: cabecera + preguntas del checklist (con orden y tipo) + respuestas actuales + evidencias + estado | BE-0 |
| 3 | `PUT /api/inspecciones/{id}/respuestas` `[ { idPregunta, respuesta, observacion } ]` | Técnico (propia), Admin | Upsert en lote; solo `EnCurso`; valida que cada pregunta pertenezca al checklist de la inspección y que `SiNo` reciba `Si`/`No` | BE-1 |
| 4 | `GET /api/inspecciones/{id}/respuestas` | Técnico (propia), Admin, Auditor | `List<RespuestaInspeccionDto>` | BE-1 |
| 5 | `GET /api/respuestasinspeccion?page&pageSize&search&idInspeccion` | Admin, Auditor | `PagedResultDto<RespuestaInspeccionDto>` — para la pantalla de administración | BE-1 |
| 6 | `POST /api/inspecciones/{id}/evidencias` (multipart `archivo`) | Técnico (propia), Admin | `EvidenciaDto`; solo `EnCurso`; JPG/PNG ≤ 5 MB (configurable) | BE-2 |
| 7 | `GET /api/evidencias/{id}/archivo` | Técnico (propia), Admin, Auditor | binario `image/*` | BE-2 |
| 8 | `GET /api/inspecciones/{id}/evidencias` | ídem | `List<EvidenciaDto>` | BE-2 |
| 9 | `DELETE /api/evidencias/{id}` | Técnico (propia), Admin | solo `EnCurso`; borra el archivo físico | BE-2 |
| 10 | `POST /api/inspecciones/{id}/firmar` `{ firmaPngBase64 }` | Técnico (propia), Admin | Valida obligatorias (SRS #3) y novedad→observación (SRS #4); calcula `Resultado` (`Conforme` / `ConNovedad`), `FirmaHash`, `Estado = Cerrada`, `FechaCierre`. Devuelve `InspeccionResultadoDto` | BE-3 |
| 11 | `GET /api/inspecciones/{id}/resultado` | Técnico (propia), Admin, Auditor | `InspeccionResultadoDto`: resumen, respuestas, evidencias, firma, hash | BE-3 |
| 12 | `GET /api/inspecciones/mias?estado=` | Técnico | Inspecciones del usuario del token | BE-1 |

Cambios en endpoints existentes: `PUT /api/inspecciones/{id}` y `DELETE` responden **409** si
`Estado = Cerrada`; `POST /api/inspecciones` (registro manual del admin) sigue existiendo pero
toma `IdUsuario` del token y exige `IdChecklist`.

Los DTOs los crea **FE-0** en `ECAR.Shared/DTOs` el martes 22/09 a partir de esta tabla
(`IniciarInspeccionDto`, `InspeccionEjecucionDto`, `PreguntaEjecucionDto`,
`GuardarRespuestasDto`, `FirmarInspeccionDto`, `InspeccionResultadoDto`, ampliación de
`EvidenciaDto`). Backend consume los mismos tipos; ningún cambio de forma sin acuerdo BE-0/FE-0.

### 3.3 Evidencias — almacenamiento

Propuesta (a confirmar con ECAR el 21/09; si no responden, se implementa así):

- Carpeta en el servidor de aplicaciones, fuera de `wwwroot`: `Evidencias:RutaBase`
  (`appsettings`), estructura `{año}/{mes}/{idInspeccion}/{guid}.{ext}`.
- `Evidencias:TamanoMaximoMB = 5`, `Evidencias:TiposPermitidos = image/jpeg, image/png`.
- Se valida el tipo por **firma binaria** (magic bytes), no por extensión ni `Content-Type`.
- Nunca se sirve el archivo por URL directa: siempre `GET /api/evidencias/{id}/archivo` con
  autorización.

### 3.4 Firma digital

Propuesta (a confirmar con ECAR): firma manuscrita capturada en canvas (PNG) + usuario
autenticado + `FechaCierre` UTC + `FirmaHash = SHA-256` del JSON canónico
`{ idInspeccion, idEquipo, idChecklist, idUsuario, fechaCierre, respuestas[ordenadas por idPregunta], evidencias[ids ordenados] }`.
Cerrar y firmar son **la misma acción**: no existe inspección cerrada sin firma.

### 3.5 Flujo en el cliente

```
/equipos/qr/{token}  (público)
   └─ botón "Iniciar inspección" → si no hay sesión: /login?returnUrl=…
        └─ /inspecciones/iniciar?equipo={id}   (elige checklist activo)
             └─ POST iniciar → /inspecciones/ejecutar/{idInspeccion}
                  ├─ Paso 1  Preguntas (FE-1)   PUT respuestas (guardado incremental)
                  ├─ Paso 2  Evidencias (FE-2)  POST evidencias desde cámara
                  ├─ Paso 3  Firma (FE-3)       POST firmar
                  └─ Paso 4  Resultado (FE-3)   GET resultado
```

`/inspecciones` (listado) muestra `Estado`, botón **Continuar** para `EnCurso` y **Ver
resultado** para `Cerrada`. Todas las páginas de ejecución se prueban a **375 px** sin scroll
horizontal.

---

## 4. Tareas de backend

### BE-0 — Carlos Tamayo (líder)

| # | Tarea | Entregable | Cuándo |
|---|---|---|---|
| 1 | Sesión de diseño con FE-0; cerrar §3 y las dudas de ECAR (evidencias, firma) | §3 firmado | Lun 21/09 |
| 2 | Migración `Fase3Inspecciones` (§3.1): entidades, `ECARDbContext`, índices, script de datos para inspecciones existentes (`Estado = Cerrada` si tienen `FirmaDigital`, `IdChecklist` = versión activa) | PR **primero de la fase**; todos rebasan sobre él | Mar 22/09 |
| 3 | Helper `ICurrentUser` (id, nombre, roles del token) en `ECAR.API/Services`, inyectable en todos los controladores | Base para BE-1/2/3 | Mar 22/09 |
| 4 | `InspeccionesController`: `[Authorize]` por rol; `POST iniciar`; `GET {id}/ejecucion`; `POST` manual con usuario del token e `IdChecklist`; regla "propia": un Técnico solo ve/edita sus inspecciones | Endpoints 1 y 2 | Mié 23 – Jue 24/09 |
| 5 | Pruebas: iniciar (usuario del token, 409 por duplicado en curso), ejecución devuelve preguntas ordenadas y respuestas, Técnico no accede a inspección ajena | `BackendPhaseThreeTests.cs` (sección propia) | Vie 25/09 |
| 6 | Revisión de PR de BE-1/2/3 (máximo 24 h por PR); integración en `develop`; `has-pending-model-changes` siempre en cero | — | Continuo |
| 7 | Semana 7: prueba de integración completa con FE-0 (flujo §3.5 con curl + cliente); `[Authorize]` de lectura en `HallazgosController` y `AuditoriaController` (adelanto de Fase 4) | Flujo verificado | 30/09 – 02/10 |
| 8 | Actualizar `ESTADO_PROYECTO.md` (secciones 5 y 6) y `CAMBIOS_BASE_DATOS.md` | Docs | Vie 02/10 |

### BE-1 — Juan David López · Ejecución y respuestas

| # | Tarea | Detalle | Cuándo |
|---|---|---|---|
| 1 | `PUT /api/inspecciones/{id}/respuestas` (endpoint 3) | Upsert en lote usando el índice único `(IdInspeccion, IdPregunta)`. Validaciones: inspección `EnCurso` (409 si no), pregunta pertenece al `IdChecklist` de la inspección (400), `TipoRespuesta = SiNo` → valor `Si`/`No` (400), `Texto` → no vacío si `Obligatoria`. Guardado parcial permitido (el técnico avanza pregunta a pregunta) | Mié 23 – Jue 24/09 |
| 2 | `GET /api/inspecciones/{id}/respuestas` (4) y `GET /api/inspecciones/mias` (12) | Con `ICurrentUser`; `mias` filtra por `Estado` opcional | Vie 25/09 |
| 3 | `RespuestasInspeccionController` (5): listado paginado para administración | Mismo patrón que `PreguntasChecklistController` (`page`, `pageSize ≤ 100`, `search`, `idInspeccion`). Sin `POST/PUT/DELETE` sueltos: las respuestas solo se escriben por el endpoint 3 | Lun 28/09 |
| 4 | Retirar de `InspeccionesController` la regla de "novedad" por texto libre en `Resultado` | Se reemplaza por el cálculo de BE-3 al firmar; `Resultado` deja de ser editable | Mar 29/09 |
| 5 | Pruebas | Upsert idempotente; 400 por pregunta ajena; 400 por `SiNo` inválido; 409 sobre inspección cerrada; `mias` no devuelve inspecciones de otro usuario | Mar 29 – Mié 30/09 |
| 6 | Prueba de integración con FE-1 | Guardado incremental desde la pantalla | Jue 01/10 |

### BE-2 — Alejandro Gómez · Evidencias fotográficas

| # | Tarea | Detalle | Cuándo |
|---|---|---|---|
| 1 | `IEvidenciaStorage` + `EvidenciaStorageDisco` en `ECAR.API/Services` | `GuardarAsync(stream, extensión, idInspeccion) → rutaRelativa`, `AbrirAsync(ruta)`, `EliminarAsync(ruta)`. Opciones `EvidenciasOptions` desde `appsettings` (§3.3). Crear la carpeta si no existe; nunca aceptar rutas con `..` | Mié 23 – Jue 24/09 |
| 2 | `POST /api/inspecciones/{id}/evidencias` (6) | `IFormFile`, límite con `[RequestSizeLimit]`, validación por magic bytes (JPEG `FF D8 FF`, PNG `89 50 4E 47`), solo `EnCurso`, guarda metadatos y `IdUsuarioCarga` del token | Vie 25 – Lun 28/09 |
| 3 | `GET /api/evidencias/{id}/archivo` (7), `GET /api/inspecciones/{id}/evidencias` (8), `DELETE /api/evidencias/{id}` (9) | `File(stream, tipoContenido)`; regla "propia" para Técnico; `DELETE` borra el físico y la fila en ese orden inverso seguro (fila primero, físico después) | Mar 29/09 |
| 4 | Migrar `EvidenciasController` existente | Eliminar el `POST` con `Archivo` de texto; `[Authorize]`; listado con `NombreEquipo`, `TamanoBytes`, `IdUsuarioCarga` | Mar 29/09 |
| 5 | Pruebas | Con `Evidencias:RutaBase` en carpeta temporal: sube JPG válido; rechaza PDF renombrado a `.jpg`; rechaza > tamaño máximo; rechaza sobre inspección cerrada; `DELETE` elimina el archivo; Técnico ajeno recibe 403 | Mié 30/09 |
| 6 | Prueba de integración con FE-2 | Foto real desde teléfono | Jue 01/10 |

### BE-3 — Simón · Firma, cierre e inmutabilidad

| # | Tarea | Detalle | Cuándo |
|---|---|---|---|
| 1 | `POST /api/inspecciones/{id}/firmar` (10) | Validar: `EnCurso`; todas las `Obligatoria` respondidas (SRS #3, devolver la lista de faltantes en `errors`); si alguna respuesta `SiNo = No` (novedad) → `Observacion` obligatoria en esa respuesta (SRS #4); firma PNG base64 válida (magic bytes, ≤ 200 KB). Calcular `Resultado` (`Conforme` si ninguna novedad, `ConNovedad` si alguna), `FechaCierre = UtcNow`, `FirmaHash` (§3.4), `Estado = Cerrada`. Todo en una transacción | Mié 23 – Vie 25/09 |
| 2 | `GET /api/inspecciones/{id}/resultado` (11) | Cabecera, equipo, checklist+versión, técnico, respuestas con la pregunta, evidencias (ids para `GET archivo`), firma, hash, `FechaCierre` | Lun 28/09 |
| 3 | Inmutabilidad transversal (SRS #6) | `PUT/DELETE /api/inspecciones/{id}` → 409 si `Cerrada`; helper `EnsureEnCursoAsync(id)` compartido con BE-1 y BE-2; `PreguntasChecklistController` y `ChecklistsController` ya bloquean por respuestas — verificar que sigan cubriendo el caso | Mar 29/09 |
| 4 | Seguridad transversal | `[Authorize]` en `EvidenciasController` (con BE-2) y validar que ningún endpoint de Fase 3 quede anónimo; unificar la validación `pageSize ≤ 100` en los 8 controladores que no la tienen | Mar 29 – Mié 30/09 |
| 5 | Pruebas | Firmar con obligatoria sin responder → 400 con lista; novedad sin observación → 400; firma correcta → `Cerrada`, `Resultado` correcto, hash reproducible (dos cálculos iguales); `PUT` sobre cerrada → 409; recomputar el hash con un dato alterado no coincide | Mié 30/09 – Jue 01/10 |
| 6 | Prueba de integración con FE-3 | Firma desde canvas real | Jue 01/10 |

---

## 5. Tareas de frontend

### FE-0 — Juan Alberto Zuluaga (líder)

| # | Tarea | Entregable | Cuándo |
|---|---|---|---|
| 1 | Sesión de diseño con BE-0 (§3); confirmar el criterio visual móvil | §3 firmado | Lun 21/09 |
| 2 | DTOs de Fase 3 en `ECAR.Shared/DTOs` (§3.2) | PR pequeño, primero del frontend | Mar 22/09 |
| 3 | Métodos en `HttpClientService` para los 12 endpoints (mismo patrón: `AddAuthorizationHeaderAsync`, `ApiResponse<T>`, `null` en error). Para el upload: `PostMultipartAsync(IBrowserFile)` con `MultipartFormDataContent`; para la imagen: `GetEvidenciaImageDataUrlAsync(id)` como se hizo con el QR | `HttpClientService` | Mar 22 – Mié 23/09 |
| 4 | `GUIA_FRONTEND_FASE3.md`: tabla de métodos, criterio visual del stepper (`MudStepper`), tamaños táctiles mínimos (44 px), comportamiento offline básico (avisar si no hay red; no se pierde lo ya guardado), reglas de revisión | Guía | Mié 23/09 |
| 5 | Esqueleto de `/inspecciones/ejecutar/{id}`: `EjecutarInspeccion.razor` con `MudStepper` de 4 pasos, carga de `GET ejecucion`, y tres componentes vacíos con sus parámetros (`PasoPreguntas`, `PasoEvidencias`, `PasoFirma`) para que FE-1/2/3 trabajen en paralelo sin conflictos de merge | Página base | Jue 24/09 |
| 6 | Guarda de ruta `TecnicoRouteGuard` (Técnico o Administrador) y `returnUrl` en `/login` | Componentes | Vie 25/09 |
| 7 | Eliminar `MockDataService` y su registro en `Program.cs` cuando FE-1 termine la migración de Respuestas | Limpieza | Semana 7 |
| 8 | PR de limpieza: 26 avisos `MUD0002` (`SelectedPageChanged` → `SelectedChanged` en `MudPagination`) y `CS8602` en Evidencias/Hallazgos | Cliente sin `MUD0002` | Semana 7 |
| 9 | Revisión de PR de FE-1/2/3 (máximo 24 h); prueba de integración completa con BE-0; prueba en un teléfono real a 375 px | Flujo verificado | 30/09 – 02/10 |
| 10 | Actualizar `ESTADO_PROYECTO.md` §4 (pantallas) | Docs | Vie 02/10 |

### FE-1 — Erica Avendaño · Preguntas y respuestas

| # | Tarea | Detalle | Cuándo |
|---|---|---|---|
| 1 | `PasoPreguntas.razor` | Recibe `InspeccionEjecucionDto`; lista las preguntas en orden con `RespuestaPreguntaInput` (ya existe: `SiNo` = dos casillas, `Texto` = campo). Marcar obligatorias; observación visible siempre y **obligatoria cuando `SiNo = No`** (SRS #4) con validación en pantalla | Jue 24 – Lun 28/09 |
| 2 | Guardado incremental | `PUT respuestas` al cambiar cada respuesta (debounce 500 ms) y al pulsar "Siguiente"; indicador "Guardado" / "Error al guardar"; el botón "Siguiente" se bloquea si hay obligatorias sin responder y muestra cuáles | Mar 29/09 |
| 3 | Migrar `Pages/Admin/RespuestasInspeccion.razor` y `RespuestaInspeccionModal` | Del mock al endpoint 5. La pantalla pasa a ser de **solo consulta** (filtro por inspección, paginación); se elimina el modal de crear/editar respuestas sueltas (las respuestas solo nacen en la ejecución) | Mar 29 – Mié 30/09 |
| 4 | Prueba de integración con BE-1 | Guardado parcial, recarga de página conserva respuestas | Jue 01/10 |
| 5 | Prueba a 375 px, sin `MUD0002` | PR | Vie 02/10 |

### FE-2 — Santiago Arango · Evidencias y listado

| # | Tarea | Detalle | Cuándo |
|---|---|---|---|
| 1 | `PasoEvidencias.razor` | `MudFileUpload<IBrowserFile>` con `Accept="image/*"` y atributo `capture="environment"` (abre la cámara trasera en móvil; SRS #9). Compresión en cliente con `IBrowserFile.RequestImageFileAsync("image/jpeg", 1600, 1600)` antes de subir; barra de progreso; máximo 5 MB | Jue 24 – Lun 28/09 |
| 2 | Galería de miniaturas | Cada evidencia como `MudImage` (data URI desde `GetEvidenciaImageDataUrlAsync`), nombre, tamaño, botón eliminar con `ConfirmDialog` (solo en `EnCurso`) | Mar 29/09 |
| 3 | `Pages/Inspecciones.razor` | Columna `Estado` (`MudChip` `EnCurso` = `Warning`, `Cerrada` = `Success`), filtro por estado, botón **Continuar** (`EnCurso` → `/inspecciones/ejecutar/{id}`) y **Ver resultado** (`Cerrada`). `InspeccionModal` (registro manual del admin) añade el selector de checklist activo | Mar 29 – Mié 30/09 |
| 4 | `Pages/Evidencias.razor` | Reemplazar el texto `Archivo` por miniatura + metadatos; retirar `EvidenciaModal` de texto libre | Mié 30/09 |
| 5 | Prueba de integración con BE-2 | Foto desde teléfono real, JPG de 8 MB comprimido en cliente | Jue 01/10 |
| 6 | Prueba a 375 px, sin `MUD0002` | PR | Vie 02/10 |

### FE-3 — Gary · Firma, resultado e inicio desde QR

| # | Tarea | Detalle | Cuándo |
|---|---|---|---|
| 1 | Componente `FirmaCanvas.razor` | Canvas con `pointer` events (ratón y táctil), botones Limpiar / Confirmar, exporta PNG base64 (`toDataURL`). JS mínimo propio en `wwwroot/js/firma.js` (sin librerías externas, misma política que el QR). Fondo blanco, trazo 2 px | Jue 24 – Lun 28/09 |
| 2 | `PasoFirma.razor` | Resumen antes de firmar (equipo, checklist+versión, nº respuestas, nº novedades, nº evidencias); texto legal: *"Al firmar, declaro que realicé esta inspección y que la información es veraz. La inspección quedará cerrada y no podrá modificarse."*; `POST firmar`; mostrar los errores del API (obligatorias faltantes) con enlace de vuelta al paso 1 | Mar 29/09 |
| 3 | `ResultadoInspeccion.razor` (`/inspecciones/{id}/resultado`) | Vista de solo lectura con `GET resultado`: cabecera, resultado (`Conforme` / `ConNovedad`), respuestas, miniaturas de evidencias, imagen de la firma, `FechaCierre` y `FirmaHash` (para Auditor). Botón "Imprimir" (`window.print`, ya usado en el QR) | Mié 30/09 |
| 4 | Inicio desde QR | En `ConsultaQr.razor`: botón **Iniciar inspección** por cada checklist activo → `/inspecciones/iniciar?equipo={id}&checklist={id}`; si no hay sesión, `/login?returnUrl=…`. Página `IniciarInspeccion.razor` que llama `POST iniciar` y redirige a `ejecutar/{id}` (o continúa la existente si el API devuelve 409 con la inspección en curso) | Mié 30/09 – Jue 01/10 |
| 5 | Prueba de integración con BE-3 | Firma desde tablet/teléfono real; verificar que el resultado muestra el hash y que un segundo `firmar` da 409 | Jue 01/10 |
| 6 | Prueba a 375 px, sin `MUD0002` | PR | Vie 02/10 |

---

## 6. Calendario y dependencias

```
Semana 6                           Semana 7
L21   M22   X23   J24   V25        L28   M29   X30   J01   V02
BE-0  diseño migr. ICur  iniciar/ejecución  revisión ── integración ── docs
FE-0  diseño DTOs  Http  esqueleto guard    limpieza ── integración ── docs
BE-1        ─────  respuestas PUT/GET       mias  admin   pruebas    integ FE-1
BE-2        ─────  storage    upload        GET/DEL migrar pruebas   integ FE-2
BE-3        ─────  firmar ─────────         resultado inmut. seguridad pruebas integ FE-3
FE-1              ─────  PasoPreguntas      guardado  migrar Respuestas   integ BE-1  PR
FE-2              ─────  PasoEvidencias     galería   Inspecciones/Evid.  integ BE-2  PR
FE-3              ─────  FirmaCanvas        PasoFirma Resultado  QR→inicio integ BE-3 PR
```

Dependencias duras:

1. **Nadie mergea antes que la migración de BE-0 (22/09)** y los DTOs de FE-0 (22/09).
2. BE-1/2/3 dependen de `ICurrentUser` (BE-0, 22/09).
3. FE-1/2/3 dependen del esqueleto `EjecutarInspeccion.razor` (FE-0, 24/09) y de los métodos
   de `HttpClientService` (FE-0, 23/09). Hasta entonces trabajan con el componente aislado.
4. FE-3 (inicio desde QR) depende del endpoint 1 (BE-0, 24/09).
5. La semana 8 (5 – 11/10) queda para estabilización y la demostración de la Entrega 4; no se
   planifican tareas nuevas.

---

## 7. Definición de terminado (aplica a cada tarea)

- Compila sin errores y **sin avisos `MUD0002` nuevos** (`dotnet build --no-incremental`).
- `dotnet test` en verde; cada endpoint nuevo tiene al menos una prueba del caso feliz y una
  de la regla de negocio que protege.
- `dotnet ef migrations has-pending-model-changes` sin cambios.
- Endpoint con `[Authorize]` y probado con curl sin token (401) y con rol incorrecto (403).
- Pantalla probada a 375 px sin scroll horizontal, y en un dispositivo real si usa cámara o
  firma.
- Sin `TODO` que apunten a otro compañero; sin datos simulados.
- Comentarios en español; un PR por tarea; PR revisado por el líder del grupo y por la
  pareja del otro grupo.

---

## 8. Criterios de aceptación de la fase (Entrega 4)

| Regla / requisito | Cómo se demuestra |
|---|---|
| SRS #2 — inspección asociada a usuario autenticado | El API ignora cualquier `IdUsuario` del cliente; la inspección muestra el técnico del token |
| SRS #3 — obligatorias antes del cierre | Intento de firmar con una obligatoria vacía → el sistema lista las faltantes y no cierra |
| SRS #4 — novedad exige observación | Respuesta `No` sin observación → no permite avanzar ni firmar |
| SRS #6 — históricos inmutables | Editar/borrar una inspección cerrada, sus respuestas o evidencias → 409; el hash de firma se recalcula igual |
| SRS #9 — fotografía desde móvil | Foto tomada con la cámara del teléfono aparece en la galería y en el resultado |
| SRS #10 — lectura de QR | Escanear la etiqueta → ficha → iniciar inspección sin escribir el código del equipo |
| Pantallas §6 del SRS | Selección de equipo (QR), ejecución de checklist, registro de novedades, adjuntar evidencias, firma digital, resultado final |
| Trazabilidad de versión | El resultado indica el checklist **y la versión** con la que se inspeccionó |

---

## 9. Riesgos de la fase

| Riesgo | Mitigación |
|---|---|
| ECAR no confirma almacenamiento/firma a tiempo | Se implementa la propuesta de §3.3/§3.4 el 21/09; cambiar de disco a red es solo configuración; cambiar a certificado digital sería fase posterior |
| Cámara/firma se comportan distinto en el dispositivo de planta | Pedir el modelo a ECAR en la semana 6; FE-2/FE-3 prueban en al menos un Android y un iOS |
| Subidas grandes por red de planta | Compresión en cliente a 1600 px (FE-2) + límite de 5 MB (BE-2) |
| Conflictos de merge en `EjecutarInspeccion.razor` | Esqueleto de FE-0 con un componente por paso; cada persona toca solo el suyo |
| `InMemory` no valida FK ni tipos de columna en pruebas | Las pruebas de integración de la semana 7 se ejecutan contra SQL Server LocalDB |
