# Asignación de tareas — Fase 2: Checklists y Gestión QR

**Duración:** 2 semanas (semanas 4–5 del cronograma)
**Equipo:** 8 personas — 4 backend (1 líder) + 4 frontend (1 líder)
**Rama de la fase:** `feature/fase2-checklists-qr`, salida desde `develop`

---

## 1. Punto de partida

Lo que **ya está hecho** y no hay que rehacer:

- CRUD de checklists en API (`ChecklistsController`) y cliente (`Pages/Checklists.razor`,
  `Components/ChecklistModal.razor`, `ChecklistDetailModal.razor`).
- Preguntas con tipo de respuesta acotado al catálogo `ECAR.Shared/TiposRespuesta.cs`
  (`SiNo` y `Texto`), validado en el API, y el componente `RespuestaPreguntaInput.razor`.
- Tablas `Checklists` y `PreguntasChecklist` con sus índices, incluido el único
  `(Nombre, Version)`.

Lo que **falta** y es el trabajo de esta fase:

| Actividad del cronograma | Estado hoy |
|---|---|
| Administración de checklists | ✅ Hecha |
| Versionamiento | ⚠️ Solo el campo `Version` y su índice; sin lógica |
| Generación QR | ❌ `Equipos.QRCode` es un texto que se escribe a mano |
| Consulta por QR | ❌ No existe |

Deuda que arrastramos y se cierra aquí porque bloquea la fase:

- `ChecklistsController` **no tiene `[Authorize]`**: hoy cualquiera sin token puede crear o
  borrar checklists.
- No existe `PreguntasChecklistController`; la pantalla `/checklists/preguntas` usa
  `MockDataService` (datos en memoria, se pierden al recargar) y **no está en el menú**.

---

## 2. Decisiones técnicas fijadas

Nadie las cambia sin acordarlo con los dos líderes.

**QR**

1. `Equipos.QRCode` guarda un **token opaco** (GUID sin guiones), no la imagen ni una URL.
2. La imagen PNG **no se almacena**: se genera bajo demanda en el endpoint.
3. El QR codifica la URL `{ClienteBaseUrl}/equipos/qr/{token}`. `ClienteBaseUrl` sale de
   configuración, nunca escrita fija en el código.
4. Escanear no exige iniciar sesión: la consulta por token es de solo lectura y anónima.

**Versionamiento**

5. Las versiones de un checklist se agrupan por `Nombre`; el índice único `(Nombre, Version)`
   ya garantiza que no se repitan.
6. Crear una versión nueva **clona** el checklist con todas sus preguntas y **desactiva** la
   versión anterior.
7. Un checklist que ya tiene respuestas registradas **no admite cambios en sus preguntas**:
   hay que crear una versión. Esta es la razón de ser del versionado — que el histórico de
   inspecciones no cambie bajo los pies.
8. Mientras `Inspecciones` no tenga `IdChecklist` (eso es Fase 3), el uso de un checklist se
   detecta por `RespuestasInspeccion → PreguntasChecklist`.

---

## 3. Equipo Backend

### BE-0 · Líder de backend — contratos, seguridad y revisión

1. **Crea** la rama `feature/fase2-checklists-qr` desde `develop` y exige que todo PR de la
   fase apunte ahí.
2. **Agrega** `[Authorize(Roles = "Administrador,Técnico,Auditor")]` a nivel de clase en
   `ECAR.API/Controllers/ChecklistsController.cs`, y `[Authorize(Roles = "Administrador")]`
   en `POST`, `PUT` y `DELETE`. **Es lo primero del día 1**: hoy el controlador es anónimo.
3. **Crea y publica el día 1** los DTOs que consumirá todo el equipo, en `ECAR.Shared/DTOs/`:
   - `CreateChecklistVersionDto` — `{ string Version }`
   - `ChecklistVersionDto` — `{ long IdChecklist, string Nombre, string Version, bool Activo, DateTime FechaCreacion, int TotalPreguntas, bool TieneRespuestas }`
   - `EquipoQrDto` — `{ long IdEquipo, string Token, string UrlConsulta }`
   - `ConsultaQrDto` — `{ EquipoDto Equipo, List<ChecklistDto> ChecklistsActivos }`
   Sin estos DTOs el frontend no puede arrancar: entrégalos antes que nada.
4. **Unifica** el contrato de paginación. Hoy solo cinco controladores validan
   `pageSize is < 1 or > 100`; esa asimetría ya provocó un fallo silencioso en el formulario
   de inspecciones. Aplícalo también en `ChecklistsController` o quítalo de los cinco, y
   anota la decisión en `docs/ESTADO_PROYECTO.md`.
5. **Revisa y aprueba** todos los PR de backend. Ningún merge sin `dotnet build` sin errores
   y `dotnet test` en verde.
6. **Actualiza** `docs/ESTADO_PROYECTO.md` y `docs/CAMBIOS_BASE_DATOS.md` al cerrar la fase
   (si BE-2 termina agregando alguna columna, va documentada ahí).

### BE-1 · Versionamiento de checklists

1. **Crea** el endpoint `POST /api/checklists/{id}/nueva-version` en `ChecklistsController`.
   Recibe `CreateChecklistVersionDto`. Clona el nombre y **todas** las preguntas del checklist
   origen con la versión nueva y `Activo = true`, y deja el origen en `Activo = false`.
   Devuelve el `ChecklistDto` de la versión nueva.
2. **Valida** en ese endpoint: `404` si el checklist origen no existe; `409` si ya existe otro
   con esa pareja `(Nombre, Version)`; `400` si la versión llega vacía.
3. **Crea** el método privado `Task<bool> TieneRespuestasRegistradas(long idChecklist)` que
   consulte `RespuestasInspeccion` uniendo por `PreguntasChecklist.IdChecklist`.
4. **Modifica** `UpdateChecklist`: si viene `updateDto.Preguntas` y el checklist ya tiene
   respuestas registradas, responde `409` con el mensaje *"Este checklist ya se usó en
   inspecciones. Cree una versión nueva para modificar sus preguntas."*. El nombre, la
   versión y el estado activo sí se pueden seguir editando.
5. **Crea** `GET /api/checklists/{id}/versiones`: devuelve `List<ChecklistVersionDto>` con
   todos los checklists que comparten `Nombre`, ordenados por versión descendente.
6. **Crea** las pruebas en `ECAR.API.Tests/BackendPhaseOneTests.cs` (o un archivo nuevo
   `ChecklistVersionTests.cs`): el clonado copia las preguntas, desactiva el origen, rechaza
   una versión duplicada, y la edición de preguntas de un checklist usado devuelve 409.

### BE-2 · Generación de códigos QR

1. **Agrega** el paquete `QRCoder` a `ECAR.API/ECAR.API.csproj`.
2. **Crea** `ECAR.API/Services/IQrCodeService.cs` y `QrCodeService.cs` con dos métodos:
   `string GenerarToken()` (GUID en formato `"N"`) y `byte[] GenerarPng(string contenido)`.
   **Regístralo** en `Program.cs` junto a los demás servicios.
3. **Agrega** la clave `ClienteBaseUrl` a `appsettings.json` y léela con `IConfiguration`.
   No la escribas fija en el código.
4. **Crea** `POST /api/equipos/{id}/qr` (solo `Administrador`) en `EquiposController`: si
   `Equipos.QRCode` está vacío genera un token y lo guarda; si ya tiene uno, lo devuelve **sin
   cambiarlo**. Responde `EquipoQrDto`.
5. **Crea** `GET /api/equipos/{id}/qr.png`: devuelve `File(png, "image/png")` con el QR que
   codifica `{ClienteBaseUrl}/equipos/qr/{token}`. Si el equipo aún no tiene token, responde
   `404` con un mensaje que indique llamar primero al `POST`.
6. **Crea** `PUT /api/equipos/{id}/qr/regenerar` (solo `Administrador`): reemplaza el token
   por uno nuevo. Es para cuando se reimprime una etiqueta; invalida la anterior.
7. **Crea** las pruebas: pedir el QR dos veces devuelve el mismo token, regenerar sí lo
   cambia, y el PNG devuelto no está vacío.

### BE-3 · Consulta por QR y CRUD de preguntas

1. **Crea** `GET /api/equipos/qr/{token}` con `[AllowAnonymous]`: devuelve `ConsultaQrDto`
   con la ficha del equipo (código interno, nombre, marca, modelo, criticidad, categoría y
   ubicación) más los checklists con `Activo = true`. `404` si el token no corresponde a
   ningún equipo. **No expongas** datos de usuarios ni de inspecciones en esta respuesta:
   es un endpoint público.
2. **Crea** `ECAR.API/Controllers/PreguntasChecklistController.cs`, que hoy no existe, con
   `[Authorize(Roles = "Administrador")]`:
   - `GET /api/preguntaschecklist` paginado, con filtros `search` e `idChecklist`.
   - `GET /api/preguntaschecklist/{id}`
   - `POST`, `PUT /{id}`, `DELETE /{id}`
   Reutiliza los DTOs existentes (`PreguntaChecklistDto`, `CreatePreguntaChecklistDto`,
   `UpdatePreguntaChecklistDto`).
3. **Valida** `TipoRespuesta` contra `TiposRespuesta.EsValido` en `POST` y `PUT`, con el mismo
   mensaje de error que ya usa `ChecklistsController`.
4. **Bloquea** con `409` el borrado de una pregunta que ya tenga filas en
   `RespuestasInspeccion`.
5. **Crea** las pruebas del controlador nuevo y de la consulta por token (token válido,
   token inexistente, equipo inactivo).

---

## 4. Equipo Frontend

### FE-0 · Líder de frontend — servicio HTTP, navegación y revisión

1. **Crea el día 1** en `ECAR.Client/Services/HttpClientService.cs` los métodos que usará
   todo el equipo, siguiendo exactamente el patrón de los existentes (encabezado de
   autorización, `ApiResponse<T>`, `try/catch`):
   `CrearVersionChecklistAsync`, `GetVersionesChecklistAsync`, `GenerarQrEquipoAsync`,
   `RegenerarQrEquipoAsync`, `GetEquipoPorQrAsync`, y el CRUD
   `GetPreguntasChecklistAsync` / `CrearPreguntaChecklistAsync` /
   `ActualizarPreguntaChecklistAsync` / `EliminarPreguntaChecklistAsync`.
   Entrégalos aunque el backend todavía devuelva error: desbloquean a FE-1, FE-2 y FE-3.
2. **Agrega** al menú de `Layout/MainLayout.razor` la entrada "Preguntas de Checklist"
   (`/checklists/preguntas`), dentro del bloque `@if (isAdmin)`. Hoy la pantalla existe pero
   solo se llega escribiendo la URL.
3. **Define y comparte** el criterio visual de la fase: los QR se muestran con `MudImage`, el
   estado de cada versión con `MudChip` (verde = activa, gris = histórica), y los errores del
   API siempre con `Snackbar` — nunca dejando un control vacío sin explicación.
4. **Revisa** todos los PR de frontend. **Ningún merge que introduzca warnings `MUD0002`
   nuevos**: ese analizador es exactamente el que escondió durante semanas el fallo de
   asignación de roles (`@bind-Values` en un componente que no tiene ese parámetro).
5. **Elimina** de `Services/MockDataService.cs` la sección de preguntas cuando FE-1 termine.
   Si no queda nada usando el servicio, quita también su registro en `Program.cs`.

### FE-1 · Pantalla de preguntas contra el API real

1. **Migra** `Pages/Admin/PreguntasChecklist.razor` y
   `Components/PreguntaChecklistModal.razor` de `MockDataService` a `HttpClientService`. Hoy
   lo que se crea ahí vive en memoria del navegador y se pierde al recargar la página.
2. **Sustituye** el campo "ID Checklist" del modal —hoy un número que se escribe a mano— por
   un `MudSelect` de checklists cargado del API, mostrando `Nombre (Versión)`.
3. **Agrega** un filtro por checklist encima de la tabla, que llame al API con el parámetro
   `idChecklist`.
4. **Muestra** el mensaje que devuelve el API cuando el borrado se rechaza por tener
   respuestas asociadas, en vez de un error genérico.
5. **Envuelve** la página en `<AdminRouteGuard>`, como ya hacen `Users.razor` y las demás
   pantallas de administración.

### FE-2 · Versionado en la interfaz

1. **Agrega** en `Pages/Checklists.razor` una columna "Versión" a la tabla y un botón
   **"Nueva versión"** por fila, que abra un diálogo pidiendo el número de versión y llame a
   `CrearVersionChecklistAsync`.
2. **Crea** `Components/VersionesChecklistModal.razor`: lista todas las versiones de un
   checklist, marca la activa con un `MudChip` verde, muestra cuántas preguntas tiene cada
   una y permite abrir cualquiera en solo lectura reutilizando `ChecklistDetailModal`.
3. **Modifica** `Components/ChecklistModal.razor`: cuando el checklist ya tenga respuestas
   registradas (campo `TieneRespuestas` del DTO), deshabilita la edición de preguntas y
   muestra un `MudAlert` de severidad `Info` explicando que hay que crear una versión nueva.
   El nombre, la versión y el estado siguen editables.
4. **Ordena** el listado por nombre y luego por versión descendente, para que las versiones
   de un mismo checklist queden juntas.

### FE-3 · QR: generación, impresión y consulta

1. **Crea** `Components/EquipoQrModal.razor` y **agrega** en `Pages/Admin/Equipos.razor` un
   botón "QR" por fila que lo abra. El modal muestra la imagen desde
   `GET /api/equipos/{id}/qr.png` y tiene tres botones:
   - **Descargar PNG**
   - **Imprimir etiqueta** — con estilo `@media print` que deje solo el QR y el código interno
   - **Regenerar** — con `ConfirmDialog` que advierta que **invalida las etiquetas ya pegadas**
2. **Crea** la página `Pages/ConsultaQr.razor` con ruta `/equipos/qr/{token}`: muestra la
   ficha del equipo y sus checklists activos. Es la pantalla que se abre al escanear, así que
   **tiene que verse bien en móvil**: pruébala con el navegador a 375 px de ancho antes de
   pedir revisión.
3. **Muestra** un mensaje claro cuando el token no existe ("Este código QR no corresponde a
   ningún equipo registrado"), nunca una página en blanco.
4. **Verifica con BE-2** el valor de `ClienteBaseUrl`. Si no coincide con la URL real del
   cliente, los QR impresos apuntarán a la nada y habrá que reimprimir todas las etiquetas.

---

## 5. Orden de trabajo y dependencias

**Semana 1**

| Día | Backend | Frontend |
|---|---|---|
| 1 | BE-0 cierra la seguridad de `ChecklistsController` y publica los DTOs | FE-0 publica los métodos de `HttpClientService` y actualiza el menú |
| 2–5 | BE-1, BE-2 y BE-3 trabajan en paralelo | FE-2 y FE-3 maquetan con datos de ejemplo; FE-1 arranca en cuanto BE-3 tenga `PreguntasChecklistController` |

**Semana 2**

| Día | Actividad |
|---|---|
| 6–8 | Integración: FE-1 ↔ BE-3, FE-2 ↔ BE-1, FE-3 ↔ BE-2/BE-3 |
| 9 | Pruebas cruzadas: cada líder recorre el flujo del otro equipo. Imprimir un QR real y escanearlo con un teléfono |
| 10 | BE-0 y FE-0 actualizan documentación y preparan la demo de la Entrega 3 |

**Dependencias duras** (si una se retrasa, arrastra a la otra):

- FE-1 depende de BE-3 punto 2.
- FE-2 depende de BE-1 puntos 1 y 5.
- FE-3 depende de BE-2 puntos 4 y 5, y de BE-3 punto 1.

---

## 6. Definición de terminado

La fase se cierra cuando **todo** esto se cumple:

- `dotnet build ECAR.AuditoriaEquipos.slnx` sin errores y `dotnet test` en verde.
- `ChecklistsController` exige token; ningún endpoint de la fase queda anónimo salvo la
  consulta por QR, que es pública a propósito.
- Un checklist se puede versionar, la versión anterior queda inactiva y sus preguntas ya no
  se pueden modificar.
- Un equipo obtiene su QR, se imprime la etiqueta y al escanearla con un teléfono se abre su
  ficha con los checklists aplicables.
- Ninguna pantalla de la fase usa `MockDataService`.
- `docs/ESTADO_PROYECTO.md` actualizado con el avance real de la Fase 2.

---

## 7. Nota sobre la capacidad del equipo

Ocho personas para dos semanas es holgado para lo que queda de la Fase 2: la administración
de checklists, que es la mitad del alcance original, ya está construida. Si sobra capacidad,
los líderes deberían absorberla en deuda técnica que ya está identificada, **sin abrir Fase 3**:

- Ficha técnica de equipo en solo lectura (lo único que falta para cerrar la Fase 1).
- Los 14 avisos `MUD0002` de `SelectedPageChanged` en `MudPagination`.
- Los `CS8602` de las páginas de Evidencias y Hallazgos.
- Un `.editorconfig` y un flujo de CI que corra `build` y `test` en cada PR — es el entregable
  de "estándares de desarrollo" que quedó pendiente de la Fase 0.
