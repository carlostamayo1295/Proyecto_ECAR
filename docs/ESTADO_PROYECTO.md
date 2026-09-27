# Estado del proyecto ECAR — Sistema de Gestión de Equipos e Inspecciones

**Fecha de corte:** 16 de septiembre de 2026 (semana 5 de 12)
**Rama evaluada:** `main` (`0272fa1`) + cambios de cierre de Fase 2 en `feature_FE_0_ServiceHTTP_nave_y_revision` (pendientes de PR)
**Documentos de referencia:** [`ECAR-SRS-MVP-Equipos-Inspecciones-v1.0.md`](../ECAR-SRS-MVP-Equipos-Inspecciones-v1.0.md),
[`ECAR-Cronograma-MVP-v1.0.md`](../ECAR-Cronograma-MVP-v1.0.md),
[`docs/GUIA_FRONTEND_FASE2.md`](GUIA_FRONTEND_FASE2.md),
[`docs/PLAN_FASE3_TAREAS.md`](PLAN_FASE3_TAREAS.md),
[`docs/CAMBIOS_BASE_DATOS.md`](CAMBIOS_BASE_DATOS.md)

> Cortes anteriores: 31/08 y 01/09/2026 (cierre de Fase 1). Su detalle está en el historial
> de git de este archivo; aquí se resume solo lo vigente.

---

## 1. Resumen

Solución .NET 10 de 5 proyectos:

| Proyecto | Rol |
|---|---|
| `ECAR.API` | API REST — 13 controladores, **68 endpoints**, JWT + LDAP/AD configurable, Scalar/OpenAPI, QRCoder |
| `ECAR.Client` | Blazor WebAssembly + MudBlazor 9.8 — 17 páginas, 22 componentes, `HttpClientService` con 65 métodos |
| `ECAR.Infrastructure` | EF Core 10: 13 entidades, `ECARDbContext`, **4 migraciones**, `DataSeeder` |
| `ECAR.Shared` | 39 DTOs, `ApiResponse<T>`, `PagedResultDto<T>`, `TiposRespuesta` |
| `ECAR.API.Tests` | xUnit — **26 pruebas** (7 reglas Fase 1 + 10 núcleo JWT + 9 reglas Fase 2) |

**Fase actual del cronograma: Fase 2 — Checklists y Gestión QR. Estado: cerrada en código
(100 %), pendiente de PR, revisión y demostración a ECAR (Entrega 3, semana 6).**

---

## 2. Estado por fase

| Fase | Alcance | Estado | Avance |
|---|---|---|---|
| Fase 0 | Arquitectura, entidades, DbContext, autenticación inicial | ✅ Completa | 100 % |
| Fase 1 | JWT, usuarios/roles, catálogos, ubicaciones, equipos, ficha técnica | ✅ Completa y verificada | 100 % |
| **Fase 2** | Checklists, preguntas, versionamiento, QR | ✅ **Completa en código** (falta PR + demo) | **100 %** |
| Fase 3 | Ejecución de inspecciones, respuestas, evidencias, firma | 🟡 Cimientos (CRUD básico, sin flujo) | ≈ 25 % |
| Fase 4 | Hallazgos, auditoría automática, reportes | 🟡 Cimientos (CRUD hallazgos, lectura auditoría) | ≈ 15 % |
| Fase 5 | Dashboard | ⚪ No iniciada | 0 % |
| Fase 6 | UAT y producción | ⚪ No iniciada | 0 % |

**Avance global estimado del MVP: ≈ 50 %.**

---

## 3. Validación de la Fase 2

El 16/09 se auditó el código contra el alcance del cronograma. **La fase no estaba
completa** (≈ 60 %): checklists y preguntas sí, pero versionamiento y QR eran maquetas en el
cliente sin backend, y los dos controladores de la fase eran anónimos. Se cerraron esos huecos
en la misma iteración (sección 7). Estado final verificado:

| Requisito | Backend | Frontend | Veredicto |
|---|---|---|---|
| Administración de checklists (CRUD) | `ChecklistsController`: unicidad `Nombre+Version`, preguntas anidadas con `Orden` secuencial, catálogo `TiposRespuesta`; **409 si se intenta reemplazar las preguntas de un checklist ya respondido** (SRS #6) | `Checklists.razor`, `ChecklistModal`, `ChecklistDetailModal` | ✅ |
| Preguntas de checklist | `PreguntasChecklistController`: listado paginado (`page`, `pageSize`, `search`, `idChecklist`), `checklist/{id}`, CRUD; `Orden` asignado por el servidor (`max + 1`); 409 al editar/borrar preguntas ya respondidas | `PreguntasChecklist.razor` + modal contra API real | ✅ |
| Versionamiento | `GET {id}/versiones` (todas las versiones del mismo nombre, activa primero, con `TotalPreguntas` y `TieneRespuestas`); `POST {id}/nueva-version` (copia preguntas con su orden, desactiva las versiones activas del mismo nombre; 400 si es la misma versión, 409 si ya existe) | `VersionesChecklistModal` y `NuevaVersionChecklistDialog` contra API; `Checklists.razor` recarga desde el servidor | ✅ |
| Generación de QR | `POST {id}/qr` (idempotente), `PUT {id}/qr/regenerar`, `GET {id}/qr.png` (PNG generado con QRCoder **en el servidor**); token opaco de 32 hex en `Equipos.QRCode` (`nvarchar(64)`, índice único filtrado) | `EquipoQrModal`: imagen como data URI, descarga PNG, impresión, regeneración con confirmación | ✅ |
| Consulta por QR | `GET qr/{token}` `[AllowAnonymous]`: ficha del equipo activo + checklists activos; nunca expone el token ni datos de usuarios | `ConsultaQr.razor` (`/equipos/qr/{token}`) probada a 375 px | ✅ |
| Control de acceso por rol | `ChecklistsController` y `PreguntasChecklistController`: lectura `Administrador,Técnico,Auditor`; mutación `Administrador`. Verificado: sin token → 401 | `AdminRouteGuard` en pantallas de administración | ✅ |

Verificación en vivo (API `https://localhost:7296`, cliente `https://localhost:7267`):
login → `POST /equipos/1/qr` (nuevo) → segunda llamada devuelve el mismo token →
`qr.png` 200 `image/png` (401 sin token) → consulta pública 200 con checklists activos →
regenerar → el token anterior responde 404. `nueva-version` copia 5 preguntas con orden 1–5 y
deja la 1.0 histórica; `versiones` marca la 1.0 con `TieneRespuestas`.

Decisión de alcance documentada: el MVP **no asocia checklists a equipos ni a categorías**
(no existe en el SRS), así que la consulta por QR devuelve todas las versiones activas. Si
ECAR lo pide, es una tabla puente nueva (fase posterior).

---

## 4. Pantallas del cliente

| Pantalla | Ruta | Origen de datos |
|---|---|---|
| Login | `/login` | API real |
| Inicio | `/` | — |
| Equipos (admin) + modal QR | `/admin/equipos` | API real ✅ |
| Consulta pública por QR | `/equipos/qr/{token}` | API real ✅ (sin sesión) |
| Ubicaciones | `/ubicaciones` | API real |
| Categorías de equipo | `/categorias-equipo` | API real |
| Usuarios (admin) | `/admin/users` | API real |
| Roles (admin) | `/admin/roles` | API real |
| Roles de usuario (admin) | `/admin/usuarios-roles` | API real |
| Checklists + versiones + nueva versión | `/checklists` | API real ✅ |
| Preguntas de checklist (admin) | `/checklists/preguntas` | API real ✅ |
| Inspecciones | `/inspecciones` | API real (CRUD básico, sin flujo de ejecución) |
| **Ejecutar inspección** | `/inspecciones/ejecutar/{id}` | 🔵 **Esqueleto (FE-0)** — stepper y contratos listos; los tres pasos los implementan FE-1/2/3 |
| Respuestas de inspección (admin) | `/inspecciones/respuestas` | **Mock** (`MockDataService`) — Fase 3 |
| Evidencias | `/evidencias` | API real (solo texto; sin archivo) — Fase 3 |
| Hallazgos | `/hallazgos` | API real (CRUD) |
| Auditoría | `/auditoria` | API real (solo lectura) |

`MockDataService` queda reducido a *Respuestas de inspección* y al lookup de preguntas que
usa `RespuestaInspeccionModal`. Desaparece con la Fase 3.

### Guardas de ruta

| Guarda | Deja pasar | Si no hay sesión |
|---|---|---|
| `AdminRouteGuard` | Administrador | Envía al inicio |
| `TecnicoRouteGuard` *(nuevo)* | Administrador y Técnico | Envía a `/login?returnUrl=…` y vuelve al destino |

La segunda es necesaria porque a la ejecución de inspecciones se llega escaneando un QR desde
el teléfono, sin sesión previa.

---

## 5. Backend — API

Todos los controladores devuelven `ApiResponse<T>`; los listados usan `PagedResultDto<T>`.
Inyectan `ECARDbContext` directamente, salvo `AuthController` (`AuthService`).

| Controlador | Ruta base | Operaciones | Autorización |
|---|---|---|---|
| `AuthController` | `api/auth` | `login`, `validate` | Anónimo |
| `UsuariosController` | `api/usuarios` | CRUD + roles, baja lógica, protección último admin | `Administrador` |
| `RolesController` | `api/roles` | CRUD | `Administrador` |
| `UsuariosRolController` | `api/usuariosrol` | CRUD + lookups | `Administrador` |
| `CategoriasEquipoController` | `api/categoriasequipo` | CRUD; 409 si en uso | Lectura: 3 roles; mutación: `Administrador` |
| `UbicacionesController` | `api/ubicaciones` | CRUD; unicidad planta/área; 409 si en uso | Lectura: 3 roles; mutación: `Administrador` |
| `EquiposController` | `api/equipos` | CRUD, baja lógica, ficha, filtros, lookups, **QR** (`{id}/qr`, `{id}/qr/regenerar`, `{id}/qr.png`, `qr/{token}` público) | Lectura: 3 roles; mutación y QR: `Administrador`; `qr/{token}`: anónimo |
| `ChecklistsController` | `api/checklists` | CRUD, **`{id}/versiones`, `{id}/nueva-version`** | Lectura: 3 roles; mutación: `Administrador` ✅ |
| `PreguntasChecklistController` | `api/preguntaschecklist` | Listado paginado, `checklist/{id}`, CRUD | Lectura: 3 roles; mutación: `Administrador` ✅ |
| `InspeccionesController` | `api/inspecciones` | CRUD básico; regla novedad→observación | ⚠️ Anónimo (Fase 3) |
| `EvidenciasController` | `api/evidencias` | Alta/consulta/baja de un texto `Archivo` | ⚠️ Anónimo (Fase 3) |
| `HallazgosController` | `api/hallazgos` | CRUD + filtros | ⚠️ Anónimo (Fase 4) |
| `AuditoriaController` | `api/auditoria` | Solo lectura; sin escritura automática | ⚠️ Anónimo (Fase 4) |

### Autenticación

- JWT con claims `NameIdentifier` (= `IdUsuario`), `Email`, `Name`, `Role` (uno por rol). Fase 3
  debe tomar el usuario del token, no del body (regla SRS #2).
- Modos `ECARAuthentication:Mode`: `Local` (BCrypt), `ActiveDirectory` (LDAP/TLS), `Hybrid`.
- Secretos en User Secrets (`JWT:Secret`, `ConnectionStrings:ECARConnection`, `AdminPassword`).
- Nueva clave de configuración **`Cliente:BaseUrl`** (`appsettings.json`): URL pública del
  cliente que se codifica en las etiquetas QR. **Debe cambiarse en cada ambiente** (en
  producción, la URL del IIS de ECAR).

### Reglas de negocio implementadas

- Último administrador activo protegido; catálogos en uso no se borran (409).
- Unicidad: `CodigoInterno`, `ActivoFijo`, `QRCode`, `Nombre` de rol/categoría, `Planta+Area`,
  `Correo`, `Nombre+Version` de checklist, `IdInspeccion+IdPregunta` en respuestas.
- Solo una versión activa por nombre de checklist; crear versión desactiva las anteriores.
- Un checklist o pregunta ya respondida en una inspección no se modifica (409): SRS #6.
- Orden de preguntas único por checklist y asignado por el servidor.
- Inspecciones: si `Resultado` contiene "novedad", `Observaciones` es obligatoria (SRS #4).

---

## 6. Modelo de datos

13 tablas del SRS en `20260817142004_InitialCreate`; `20260817155507` añade `PasswordHash`;
`20260914011254` añade `PreguntasChecklist.Orden`; **`20260916172707_AgregarTokenQrEquipo`**
acorta `Equipos.QRCode` a `nvarchar(64)`, crea el índice único filtrado `IX_Equipos_QRCode`,
limpia los valores libres previos de `QRCode` y renumera las preguntas con `Orden = 0`.
`has-pending-model-changes` → sin cambios.

Hallazgos del modelo que **deben resolverse al inicio de Fase 3** (detalle y responsable en
[`PLAN_FASE3_TAREAS.md`](PLAN_FASE3_TAREAS.md)):

| Tema | Situación | Consecuencia |
|---|---|---|
| Inspección → checklist | `Inspeccion` no tiene `IdChecklist`; EF creó la FK sombra `ChecklistIdChecklist` desde `Checklist.Inspecciones` | No se sabe con qué versión se ejecutó una inspección. Hacerlo explícito |
| Estado de la inspección | Sin columna de estado ni fecha de cierre | No se distingue "en curso" de "cerrada" para aplicar inmutabilidad (SRS #6) |
| Evidencias | `Archivo` es texto libre; sin nombre original, tipo MIME, tamaño ni ruta física | Requiere decisión de almacenamiento y columnas nuevas |

---

## 7. Cambios de esta iteración (13 – 16 de septiembre de 2026)

### Integrados en `main` (PR #9 – #16)

- QR (Gary): `EquipoQrModal`, `ConsultaQr.razor` — maquetas.
- Versionado en la interfaz (Santiago): `VersionesChecklistModal`, `NuevaVersionChecklistDialog` — maquetas.
- FE-0 (Juan Alberto): 13 métodos en `HttpClientService`, DTOs de versión/QR, menú, `GUIA_FRONTEND_FASE2.md`.
- FE-1 (Erica): `PreguntasChecklist.razor` y modal migrados del mock al API.
- Backend preguntas: `PreguntasChecklistController` y migración `Orden`.

### Cierre de Fase 2 — 16/09, pendiente de PR (`feature_FE_0_ServiceHTTP_nave_y_revision`)

**Backend**
- `ChecklistsController`: `[Authorize]` por rol; `GET {id}/versiones`; `POST {id}/nueva-version`;
  409 al reemplazar preguntas de un checklist con respuestas; `Orden` secuencial al crear/editar;
  `Orden` incluido en las proyecciones.
- `PreguntasChecklistController`: `[Authorize]` activado; `GET` paginado con `search` e
  `idChecklist`; `Orden` automático (`max + 1`), no editable; sin validación de duplicados.
- `EquiposController`: `POST {id}/qr`, `PUT {id}/qr/regenerar`, `GET {id}/qr.png`,
  `GET qr/{token}` público; `QRCode` deja de aceptarse en `Create/UpdateEquipoDto`.
- Paquete `QRCoder 1.8.0`; clave `Cliente:BaseUrl`; `EquipoQrDto.EsNuevo`.
- `Equipo.QRCode` `[MaxLength(64)]` + índice único filtrado; migración `AgregarTokenQrEquipo`.
- `BackendPhaseTwoTests.cs`: 9 pruebas (versionamiento, bloqueo por respuestas, orden
  automático, QR idempotente/regenerar, consulta pública, PNG en servidor).

**Frontend**
- `EquipoQrModal`, `ConsultaQr`, `VersionesChecklistModal`, `NuevaVersionChecklistDialog`,
  `Checklists.razor`: conectados al API; eliminados todos los `TODO`, mocks y el servicio
  externo `api.qrserver.com`.
- `EquipoModal`: se retira el campo de texto "Código QR" (el token lo genera el servidor).
- `Equipos.razor`: corregido un residuo de merge que anidaba el botón QR dentro del botón
  Eliminar (aparecían dos "QR" y una papelera suelta).
- `PreguntasChecklist.razor` / `PreguntaChecklistModal`: endpoint paginado, búsqueda con
  debounce, columna Orden, sin campo Orden en el modal, sin `MockDataService`.
- `MockDataService`: retirado el CRUD de preguntas (tarea 5 de FE-0 en la guía de Fase 2).
- `HttpClientService`: eliminados 3 métodos duplicados que impedían compilar.

### Verificación

```powershell
dotnet build ECAR.AuditoriaEquipos.slnx                                                        # 0 errores
dotnet test ECAR.API.Tests/ECAR.API.Tests.csproj                                               # 26/26 correctas
dotnet ef migrations has-pending-model-changes --project ECAR.Infrastructure --startup-project ECAR.API  # sin cambios
```

Avisos de la solución: **0** (eran 100 hasta el 22/09).

---

## 7.1. Base de frontend para Fase 3 (22/09/2026, FE-0)

Preparación que desbloquea a FE-1, FE-2 y FE-3; no implementa todavía ninguna pantalla de
inspección. Detalle en [`GUIA_FRONTEND_FASE3.md`](GUIA_FRONTEND_FASE3.md).

- **DTOs** (`ECAR.Shared`): `IniciarInspeccionDto`, `PreguntaEjecucionDto`,
  `InspeccionEjecucionDto`, `GuardarRespuestasDto` + `RespuestaEjecucionDto`,
  `FirmarInspeccionDto`, `InspeccionResultadoDto`, y las constantes `InspeccionEstados` /
  `InspeccionResultados`.
- **`HttpClientService`**: 13 métodos para los endpoints de la fase, incluida la subida
  multipart de fotografías y la descarga de la imagen como data URI.
- **`Pages/EjecutarInspeccion.razor`**: esqueleto con `MudStepper` de tres pasos, carga única
  del estado, recálculo local de contadores, bloqueo de avance por las reglas 3 y 4 del SRS y
  redirección automática al resultado si la inspección ya está cerrada.
- **`Components/PasoPreguntas|PasoEvidencias|PasoFirma.razor`**: un componente por persona, con
  el contrato y las llamadas al API ya resueltos; falta solo la interfaz.
- **`Components/TecnicoRouteGuard.razor`** y `returnUrl` en `/login`.
- **Limpieza**: los 100 avisos de compilación a 0 (ver sección 8).

Verificado en el navegador: la guarda redirige al login conservando el destino y vuelve a él;
el stepper se dibuja con cabecera y contadores; el avance se bloquea con el aviso correcto
cuando falta una obligatoria; a 375 px no hay scroll horizontal; la paginación de
`/auditoria` ahora sí recarga datos al cambiar de página.

---

## 8. Pendientes y deuda técnica

### Para cerrar formalmente la Entrega 3 (semana 6)

1. Abrir PR de `feature_FE_0_ServiceHTTP_nave_y_revision` → `develop` → `main`, con revisión
   cruzada BE-0 / FE-0.
2. Cada integrante aplica la migración (`dotnet ef database update` o arrancar el API) — la
   migración limpia `QRCode` y renumera `Orden`.
3. Fijar `Cliente:BaseUrl` por ambiente antes de imprimir etiquetas reales.
4. Sesión de demostración a ECAR: checklists, preguntas, versiones, QR e impresión, consulta
   desde un teléfono.

### Deuda técnica

- **Controladores anónimos** de fases posteriores: Inspecciones, Evidencias, Hallazgos,
  Auditoría. Se protegen al implementar su fase (Fase 3 los dos primeros).
- **Regla SRS #2 sin cumplir:** `CreateInspeccionDto.IdUsuario` viene del body.
- **Auditoría automática (Fase 4)**, **reportes (Fase 4)**, **dashboard (Fase 5)**: no iniciados.
- ~~**Avisos de compilación**~~ ✅ **Resuelto el 22/09 (FE-0): la solución está en 0 errores y
  0 advertencias.** No eran cosméticos: los 26 `MUD0002` venían de `SelectedPageChanged`, un
  parámetro que **no existe** en `MudPagination` (el correcto es `SelectedChanged`), así que
  **la paginación no recargaba datos en las 13 pantallas paginadas**; los 72 `CS8602` eran
  `DialogResult?` desreferenciado sin comprobar null. Segundo defecto real escondido tras
  `MUD0002`, después del de asignación de roles en Fase 1.
- **`pageSize` inconsistente:** 5 controladores validan `<= 100`, 8 no.
- **Solape Users/UsuariosRoles:** dos vías para asignar roles; decidir la oficial.
- **Codificación:** `PreguntasChecklistController.cs` y `Entities/PreguntaChecklist.cs` están
  en UTF-16 (git los trata como binarios, sin diff en los PR). Convertir a UTF-8 en un commit
  aislado.
- **Huecos en `Orden`** al borrar preguntas (1, 2, 4). No afecta al orden ni a `max + 1`;
  renumerar al borrar es opcional.
- **Despliegue IIS:** Hosting Bundle .NET 10, HTTPS, identidad del pool, SQL, LDAP.

### Insumos pendientes de ECAR (sin cambios desde el 08/09)

Active Directory (servidor/puerto/dominio/cuenta), **almacenamiento de evidencias** y
**alcance de la firma digital** (ambos bloquean el diseño de Fase 3), periodicidad de
inspecciones, ambiente de despliegue, validación de .NET 10 frente al .NET 8 del SRS.
