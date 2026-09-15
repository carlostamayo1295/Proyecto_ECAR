# Guía de frontend — Fase 2 (Checklists y QR)

Entrega del líder de frontend (FE-0) para FE-1, FE-2 y FE-3. Lo que está aquí ya se puede
usar: compila y sigue el patrón del resto del cliente. Los endpoints los publica backend en
paralelo; hasta que existan, cada método devuelve `null` o `Success = false`, y la pantalla
debe mostrarlo con `Snackbar` — nunca dejar un control vacío sin explicación.

---

## 1. Métodos nuevos en `HttpClientService`

Los nombres siguen la convención del archivo (verbo en inglés + entidad en español), no los
del plan original. Para el CRUD de preguntas son **los mismos nombres que tenía
`MockDataService`**, así FE-1 migra cambiando solo el servicio inyectado.

| Método | Endpoint | Devuelve | Lo usa |
|---|---|---|---|
| `CreateChecklistVersionAsync(id, dto)` | `POST /api/checklists/{id}/nueva-version` | `ApiResponse<ChecklistDto>` | FE-2 |
| `GetChecklistVersionesAsync(id)` | `GET /api/checklists/{id}/versiones` | `ApiResponse<List<ChecklistVersionDto>>` | FE-2 |
| `GetPreguntasChecklistAsync(page, pageSize, search, idChecklist)` | `GET /api/preguntaschecklist` | `ApiResponse<PagedResultDto<PreguntaChecklistDto>>` | FE-1 |
| `GetPreguntaChecklistAsync(id)` | `GET /api/preguntaschecklist/{id}` | `ApiResponse<PreguntaChecklistDto>` | FE-1 |
| `CreatePreguntaChecklistAsync(dto)` | `POST /api/preguntaschecklist` | `ApiResponse<PreguntaChecklistDto>` | FE-1 |
| `UpdatePreguntaChecklistAsync(id, dto)` | `PUT /api/preguntaschecklist/{id}` | `ApiResponse<PreguntaChecklistDto>` | FE-1 |
| `DeletePreguntaChecklistAsync(id)` | `DELETE /api/preguntaschecklist/{id}` | `ApiResponse<bool>` | FE-1 |
| `GenerateEquipoQrAsync(id)` | `POST /api/equipos/{id}/qr` | `ApiResponse<EquipoQrDto>` | FE-3 |
| `RegenerateEquipoQrAsync(id)` | `PUT /api/equipos/{id}/qr/regenerar` | `ApiResponse<EquipoQrDto>` | FE-3 |
| `GetEquipoQrImageAsync(id)` | `GET /api/equipos/{id}/qr.png` | `byte[]?` | FE-3 |
| `GetEquipoQrImageDataUrlAsync(id)` | (envuelve el anterior) | `string?` listo para `MudImage Src` | FE-3 |
| `GetEquipoByQrAsync(token)` | `GET /api/equipos/qr/{token}` | `ApiResponse<ConsultaQrDto>` | FE-3 |

Dos detalles que no son obvios:

- **La imagen del QR no se puede poner en un `<img src>` directo.** El endpoint exige token
  Bearer y una etiqueta `<img>` no lo envía. Por eso existe `GetEquipoQrImageDataUrlAsync`:
  descarga los bytes y devuelve un `data:image/png;base64,...` para `MudImage`.
- **`GetEquipoByQrAsync` no envía token.** Es la única llamada pública del cliente: la abre
  alguien que escaneó una etiqueta y no tiene sesión.

Los DTOs (`CreateChecklistVersionDto`, `ChecklistVersionDto`, `EquipoQrDto`,
`ConsultaQrDto`) están en `ECAR.Shared/DTOs/`. Si backend necesita cambiarles la forma, se
acuerda entre BE-0 y FE-0 antes de tocarlos: los tres equipos de frontend ya construyen
sobre ellos.

---

## 2. Criterio visual de la fase

Para que las tres pantallas nuevas parezcan de la misma aplicación:

| Elemento | Cómo se muestra |
|---|---|
| Imagen del QR | `MudImage` con `Src` en data URI, `Width="220"`, `ObjectFit="ObjectFit.Contain"` |
| Versión activa de un checklist | `MudChip` `Color="Color.Success"` `Variant="Variant.Outlined"` con el número de versión |
| Versión histórica | `MudChip` `Color="Color.Default"` `Variant="Variant.Outlined"` |
| Checklist bloqueado (ya tiene respuestas) | `MudAlert` `Severity="Severity.Info"` encima de las preguntas, con el texto: *"Este checklist ya se usó en inspecciones. Para modificar sus preguntas cree una versión nueva."* |
| Errores del API | Siempre `Snackbar.Add(result?.Message ?? "...", Severity.Error)`. Si el error se repite en cada pulsación (buscadores), avisar una sola vez por modal |
| Acciones destructivas (regenerar QR) | `ConfirmDialog` antes de llamar al API, con el texto explicando la consecuencia |
| Botones de diálogo | Las clases `dialog-button` / `cancel-btn` que ya usan los modales existentes |

Pantalla de consulta por QR (`/equipos/qr/{token}`): es la única que se abre en un teléfono.
Antes de pedir revisión, probarla a **375 px de ancho** y confirmar que no hay scroll
horizontal.

---

## 3. Reglas de revisión

- **Ningún PR con avisos `MUD0002` nuevos.** Ese analizador marca atributos que el componente
  no reconoce. Un `@bind-Values` sobre un `MudSelect` compila sin error, no enlaza nada, y
  nos escondió durante semanas el fallo de asignación de roles. Ejecutar
  `dotnet build ECAR.Client/ECAR.Client.csproj --no-incremental` y revisar los avisos del
  archivo tocado antes de abrir el PR.
- **`pageSize` nunca mayor que 100.** Los controladores de Equipos, Usuarios, Roles,
  Ubicaciones y UsuariosRol responden `400` por encima de ese valor, y el cliente descarta el
  error en silencio. Para listas largas, usar búsqueda en el API (como hace
  `InspeccionModal`), no traer el catálogo completo.
- Las pantallas de administración van dentro de `<AdminRouteGuard>`.
- Los comentarios del código, en español.

---

## 4. Estado de las tareas de FE-0

| # | Tarea | Estado |
|---|---|---|
| 1 | Métodos en `HttpClientService` | ✅ Hecho (tabla de arriba) |
| 2 | Entrada "Preguntas de Checklist" en el menú de administración | ✅ Hecho (`MainLayout.razor`) |
| 3 | Criterio visual compartido | ✅ Este documento |
| 4 | Revisión de PRs | Continua durante la fase |
| 5 | Retirar la sección de preguntas de `MockDataService` | Pendiente de que FE-1 termine la migración |
