# Estado del proyecto ECAR — Sistema de Gestión de Equipos e Inspecciones

**Fecha de corte:** 31 de agosto de 2026
**Rama:** `feature_equipos`
**Documentos de referencia:** [`ECAR-SRS-MVP-Equipos-Inspecciones-v1.0.md`](../ECAR-SRS-MVP-Equipos-Inspecciones-v1.0.md),
[`ECAR-Cronograma-MVP-v1.0.md`](../ECAR-Cronograma-MVP-v1.0.md),
[`docs/REVISION_BACKEND_FASE1.md`](REVISION_BACKEND_FASE1.md),
[`docs/OPERACION_BACKEND_FASE1.md`](OPERACION_BACKEND_FASE1.md)

---

## 1. Resumen

Solución .NET 10 de 4 proyectos:

| Proyecto | Rol |
|---|---|
| `ECAR.API` | API REST (controladores, autenticación JWT + LDAP/AD configurable, Scalar/OpenAPI) |
| `ECAR.Client` | Blazor WebAssembly + MudBlazor 9 (tema corporativo) |
| `ECAR.Infrastructure` | EF Core: entidades, `ECARDbContext`, migraciones, `DataSeeder` |
| `ECAR.Shared` | DTOs, `ApiResponse<T>`, `PagedResultDto<T>` |
| `ECAR.API.Tests` | xUnit — 7 pruebas de reglas críticas |

Base de datos: **SQL Server**. Migraciones aplicadas automáticamente al arranque con
`Database.MigrateAsync()`. Semillas idempotentes (roles, administrador, categorías,
ubicaciones) mediante `DataSeeder.SeedDataAsync`.

**Fase actual del cronograma: Fase 1 — Seguridad y Gestión de Equipos (completa).**

---

## 2. Estado por fase

| Fase | Alcance | Estado |
|---|---|---|
| Fase 0 | Arquitectura por capas, entidades, DbContext, autenticación inicial | ✅ Completa |
| **Fase 1** | JWT, usuarios y roles, asignación usuario–rol, catálogos, ubicaciones, CRUD de equipos y ficha técnica | ✅ **Completa (backend + cliente)** |
| Fase 2 | Checklists versionados, generación/lectura QR | ⏳ Backend parcial (CRUD de checklists), cliente con datos simulados |
| Fase 3 | Inspecciones, respuestas, evidencias, firma digital | ⏳ Backend parcial (controladores adelantados), cliente con datos simulados |
| Fase 4 | Hallazgos, auditoría automática, reportes PDF/Excel | ⏳ Backend parcial (CRUD hallazgos, lectura de auditoría), sin escritura automática de auditoría ni reportes |
| Fase 5 | Dashboard e indicadores | ❌ No iniciada |

### Pantallas del cliente

| Pantalla | Ruta | Origen de datos |
|---|---|---|
| Login | `/login` | API real (`/api/auth/login`) |
| Equipos (admin) | `/admin/equipos` | API real |
| Ubicaciones (admin) | `/ubicaciones` | API real *(migrada del mock en esta iteración)* |
| Usuarios (admin) | `/admin/users` | API real |
| Roles (admin) | `/admin/roles` | API real |
| Roles de Usuario (admin) | `/admin/usuarios-roles` | API real |
| Categoría de Equipos | `/categorias-equipo` | API real |
| Auditoría | `/auditoria` | API real (solo lectura) |
| Checklists | `/checklists` | API real (CRUD) |
| Inspecciones | `/inspecciones` | API real (CRUD) |
| Evidencias | `/evidencias` | API real (CRUD) |
| Hallazgos | `/hallazgos` | API real (CRUD) |
| Preguntas de Checklist (admin) | `/admin/preguntas-checklist` | **Mock** (`MockDataService`) — sin endpoint |
| Respuestas de Inspección (admin) | `/admin/respuestas-inspeccion` | **Mock** (`MockDataService`) — sin endpoint |

---

## 3. Backend — API

Todos los controladores devuelven `ApiResponse<T>`; los listados usan
`PagedResultDto<T>` con parámetros `page` / `pageSize` / `search`. Inyectan `ECARDbContext`
directamente (sin capa de servicios), salvo `AuthController`.

| Controlador | Ruta base | Operaciones | Autorización |
|---|---|---|---|
| `AuthController` | `api/auth` | `login`, `validate-token` | Anónimo |
| `UsuariosController` | `api/usuarios` | CRUD + asignación de roles, baja lógica, protección del último administrador | `[Authorize(Roles = "Administrador")]` |
| `RolesController` | `api/roles` | CRUD | `[Authorize(Roles = "Administrador")]` |
| `UsuariosRolController` | `api/usuariosrol` | CRUD de asignaciones + lookups `usuarios` / `roles` | `[Authorize(Roles = "Administrador")]` |
| `CategoriasEquipoController` | `api/categoriasequipo` | CRUD; **409** si la categoría está en uso | Lectura: `Administrador,Técnico,Auditor`; mutación: `Administrador` *(añadido en esta iteración)* |
| `UbicacionesController` | `api/ubicaciones` | CRUD; unicidad planta/área; **409** si está en uso | Lectura: `Administrador,Técnico,Auditor`; mutación: `Administrador` |
| `EquiposController` | `api/equipos` | CRUD, baja lógica, ficha técnica (`GET {id}`), filtros (texto, criticidad, categoría, ubicación, planta, área, estado), lookups `categorias` / `ubicaciones` | Lectura: `Administrador,Técnico,Auditor`; mutación: `Administrador` |
| `ChecklistsController` | `api/checklists` | CRUD (adelanto Fase 2) | ⚠️ Anónimo |
| `InspeccionesController` | `api/inspecciones` | CRUD (adelanto Fase 3) | ⚠️ Anónimo |
| `EvidenciasController` | `api/evidencias` | Alta/consulta/baja (adelanto Fase 3) | ⚠️ Anónimo |
| `HallazgosController` | `api/hallazgos` | CRUD + filtro por inspección/estado (adelanto Fase 4) | ⚠️ Anónimo |
| `AuditoriaController` | `api/auditoria` | Solo lectura (adelanto Fase 4; sin escritura automática) | ⚠️ Anónimo |

### Autenticación

- JWT (emisión y validación); secreto y cadena de conexión en User Secrets.
- Modos `ECARAuthentication:Mode`: `Local` (BCrypt), `ActiveDirectory` (LDAP/TLS), `Hybrid`.
- La conexión real con AD requiere que ECAR entregue servidor, puerto, dominio y cuenta de
  prueba; el código ya lo activa por configuración sin recompilar.

### Reglas de negocio implementadas

- No se puede desactivar/eliminar al último administrador activo.
- No se puede borrar una categoría o ubicación en uso (HTTP 409).
- Unicidad: `CodigoInterno` y `ActivoFijo` de equipo, `Nombre` de rol/categoría,
  `Planta+Area` de ubicación, `Correo` de usuario, `Nombre+Version` de checklist.
- Equipos y ubicaciones se desactivan/bloquean en vez de borrarse para preservar trazabilidad.

---

## 4. Modelo de datos y jerarquía

Las **13 tablas del SRS ya existen** en la migración `20260817142004_InitialCreate`
(la segunda migración, `20260817155507_AddPasswordToUsuarios`, añade `PasswordHash`).
`dotnet ef migrations has-pending-model-changes` → *sin cambios pendientes*.

> **El esquema cubre el SRS completo, no solo la Fase 1. No falta ninguna tabla ni
> columna para Fase 1 ni para fases posteriores.** Lo que falta en fases 2–4 es lógica de
> aplicación (endpoints, escritura de auditoría, reportes), no estructura de base de datos.

### Jerarquía de claves foráneas

```
Rol ──< UsuarioRol >── Usuario ──< Inspeccion >── Equipo >── CategoriaEquipo
                            │            │            └────── Ubicacion
                            │            ├──< RespuestaInspeccion >── PreguntaChecklist >── Checklist
                            │            ├──< Evidencia
                            │            └──< Hallazgo
                            └──< (Inspeccion.IdUsuario)

Auditoria ── tabla transversal, sin FK (Tabla + RegistroId + FechaHora)
```

| Tabla | PK | FKs | Notas |
|---|---|---|---|
| `Roles` | `IdRol` | — | catálogo de seguridad |
| `Usuarios` | `IdUsuario` | — | `PasswordHash`, `UsuarioAD` (único, filtrado) |
| `UsuarioRol` | `Id` | `IdUsuario` → Usuarios, `IdRol` → Roles | único (`IdUsuario`,`IdRol`); N:M |
| `CategoriasEquipo` | `IdCategoria` | — | catálogo |
| `Ubicaciones` | `IdUbicacion` | — | único (`Planta`,`Area`) |
| `Equipos` | `IdEquipo` | `IdCategoria` → CategoriasEquipo, `IdUbicacion` → Ubicaciones | baja lógica (`Activo`), `QRCode` |
| `Checklists` | `IdChecklist` | — | versionado (`Nombre`,`Version`) |
| `PreguntasChecklist` | `IdPregunta` | `IdChecklist` → Checklists (cascade) | |
| `Inspecciones` | `IdInspeccion` | `IdEquipo` → Equipos, `IdUsuario` → Usuarios, `ChecklistIdChecklist` → Checklists | `FirmaDigital`, `Resultado` |
| `RespuestasInspeccion` | `IdRespuesta` | `IdInspeccion` → Inspecciones (cascade), `IdPregunta` → PreguntasChecklist (cascade) | único (`IdInspeccion`,`IdPregunta`) |
| `Evidencias` | `IdEvidencia` | `IdInspeccion` → Inspecciones (cascade) | `Archivo`, `UsuarioCarga` |
| `Hallazgos` | `IdHallazgo` | `IdInspeccion` → Inspecciones (cascade) | `Criticidad`, `Estado` |
| `Auditoria` | `IdAuditoria` | — | índices por `Tabla`, `RegistroId`, `Accion`, `Usuario`, `FechaHora` |

---

## 5. Cambios de esta iteración (31/08/2026)

### Build estabilizado (la solución no compilaba — 16 errores tras merges)

- `ECAR.Shared/DTOs/CreatePreguntaChecklistDto.cs`: se añade `IdChecklist` (ya existía en
  `UpdatePreguntaChecklistDto` y lo consumían el modal y el mock).
- `ECAR.Client/Services/HttpClientService.cs`: se añaden los métodos que las páginas ya
  invocaban pero nunca se implementaron:
  - Equipos: `GetEquipoAsync`, `CreateEquipoAsync`, `UpdateEquipoAsync`, `DeleteEquipoAsync`,
    parámetro `criticidad` en `GetEquiposAsync`, lookups `GetEquipoCategoriasLookupAsync` /
    `GetEquipoUbicacionesLookupAsync`.
  - Asignaciones usuario–rol: `GetUsuariosRolAsync`, `CreateUsuarioRolAsync`,
    `UpdateUsuarioRolAsync`, `DeleteUsuarioRolAsync`, `GetUsuariosLookupAsync`,
    `GetRolesLookupAsync`.
  - Ubicaciones: `GetUbicacionesAsync`, `CreateUbicacionAsync`, `UpdateUbicacionAsync`,
    `DeleteUbicacionAsync`.
- `ECAR.Client/Pages/Admin/Equipos.razor`: 2 llamadas de lookup apuntadas a los métodos nuevos.
- `ECAR.API.Tests/BackendPhaseOneTests.cs`: `DeleteCategoria` → `DeleteCategoriaEquipo`.

### Duplicados / código muerto eliminado

- `ECAR.Infrastructure/Data/ECARDbContext.cs`: se elimina el segundo índice sobre
  `Usuario.Correo` (redundante con el índice único; EF los fusionaba).
- `ECAR.Infrastructure/Data/DataSeeder.cs`: se elimina el método privado
  `SeedCatalogoEquiposAsync` — nunca se invocaba y definía un catálogo de categorías
  **contradictorio** con el seed real.
- `ECAR.Client/Services/MockDataService.cs`: se elimina la sección de Ubicaciones (duplicaba
  el CRUD real). El mock queda acotado a Preguntas/Respuestas (Fase 2/3).

### Consistencia y funcionamiento

- `CategoriasEquipoController`: se añade `[Authorize]` (antes era **anónimo**) — lectura para
  `Administrador,Técnico,Auditor` y mutaciones solo `Administrador`, igual que `EquiposController`.
- `CategoriasEquipoController.DeleteCategoriaEquipo`: devuelve **409 Conflict** (antes 400)
  cuando la categoría está en uso, igual criterio que `UbicacionesController`.
- `ECAR.Client/Pages/Ubicacion.razor` y `Components/UbicacionModal.razor`: migradas de
  `MockDataService` al API real (`HttpClientService`).
- `ECAR.Client/Program.cs`: se registra `MockDataService` en DI (antes lo inyectaban páginas
  sin estar registrado → fallo en runtime).
- `ECAR.Client/Layout/MainLayout.razor`: enlace a `/ubicaciones` en el menú de administración.

### Verificación

```powershell
dotnet build ECAR.AuditoriaEquipos.slnx --no-restore          # 0 errores
dotnet test ECAR.API.Tests/ECAR.API.Tests.csproj -c Release    # 7/7 correctas
dotnet ef migrations has-pending-model-changes --project ECAR.Infrastructure --startup-project ECAR.API  # sin cambios
```

---

## 6. Pendientes y deuda técnica

- **Auditoría automática (Fase 4):** `AuditoriaController` es solo lectura; falta el
  interceptor/`SaveChanges` que registre altas, bajas y cambios de forma inmutable.
- **QR (Fase 2):** el campo `Equipos.QRCode` existe pero no hay generación ni lectura.
- **Reportes (Fase 4):** exportación PDF/Excel no implementada.
- **Autorización de servidor:** los controladores adelantados de fases posteriores
  (`ChecklistsController`, `InspeccionesController`, `EvidenciasController`,
  `HallazgosController`, `AuditoriaController`) siguen siendo **anónimos**; deben recibir
  `[Authorize]` al implementarse su fase. Los de Fase 1 ya están protegidos.
- **Solape funcional:** `Pages/Admin/Users.razor` (+`UserModal`, con selección múltiple de
  roles) y `Pages/Admin/UsuariosRoles.razor` (+`UsuarioRolModal`, CRUD de la tabla puente)
  cubren en parte la misma necesidad. Conviene decidir cuál es la vía oficial.
- **Pantallas Fase 2/3 con mock:** `preguntas-checklist` y `respuestas-inspeccion` usan
  `MockDataService`; requieren sus controladores (`PreguntasChecklistController`,
  `RespuestasInspeccionController`) y métodos en `HttpClientService`.
- **Warnings de compilación:** analizadores MudBlazor (`SelectedPageChanged` en
  `MudPagination`, `Values`/`ValuesChanged` en `MudSelect`) y `CS8602` en las páginas de
  Evidencias/Hallazgos. No bloquean el build.
- **Despliegue IIS:** validar en el servidor destino el Hosting Bundle .NET 10, certificado
  HTTPS, identidad del Application Pool, cadena SQL y acceso LDAP.
