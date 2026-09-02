# Cambios sobre el modelo de datos del SRS

Documento de trazabilidad entre el **modelo de datos inicial** definido en
`ECAR-SRS-MVP-Equipos-Inspecciones-v1.0.md` (sección 5) y el **esquema realmente
implementado** en `ECAR.Infrastructure`.

Fuentes del esquema implementado:

- Entidades: `ECAR.Infrastructure/Entities/*.cs`
- Configuración e índices: `ECAR.Infrastructure/Data/ECARDbContext.cs`
- Migraciones: `20260817142004_InitialCreate`, `20260817155507_AddPasswordToUsuarios`

Las 13 tablas del SRS existen con los mismos nombres y las mismas llaves primarias.
Lo que se documenta aquí son las diferencias.

---

## 1. Resumen

| # | Cambio | Tabla | Tipo de cambio | Intencional |
|---|--------|-------|----------------|-------------|
| 1 | Columna nueva `PasswordHash` | Usuarios | Estructural | Sí |
| 2 | Columna extra `ChecklistIdChecklist` | Inspecciones | Estructural | **No** |
| 3 | `UsuarioCarga` pasa de BIGINT a texto | Evidencias | Tipo de dato | Sí (revisable) |
| 4 | `VARCHAR` → `NVARCHAR`, `DATETIME` → `datetime2` | Todas | Tipo de dato | Sí (por defecto de EF) |
| 5 | Definición de nulabilidad por columna | Varias | Restricción | Sí |
| 6 | 10 restricciones UNIQUE y 25+ índices | Varias | Restricción / rendimiento | Sí |
| 7 | Reglas de borrado en cascada | Varias | Integridad | Sí (mitigado) |
| 8 | Dominio cerrado para `TipoRespuesta` | PreguntasChecklist | Aplicación | Sí |

---

## 2. Columnas nuevas

### 2.1 `Usuarios.PasswordHash` — NVARCHAR(255) NOT NULL

Migración `20260817155507_AddPasswordToUsuarios`.

**Por qué.** El SRS modela `Usuarios` con `UsuarioAD` únicamente, dando por sentado que
toda la autenticación pasa por Active Directory. El MVP arranca configurado en modo
`Local` (`ECARAuthentication:Mode` en `appsettings.json`), porque en desarrollo no hay
un directorio corporativo disponible. Para poder autenticar sin AD hay que guardar la
contraseña, y se guarda como hash BCrypt, nunca en claro.

**Cómo convive con AD.** La columna es `NOT NULL` pero admite cadena vacía: los usuarios
que solo entran por AD se crean con `PasswordHash = ""`. `UsuariosController.CreateUsuario`
exige que el alta traiga contraseña local **o** `UsuarioAD`, para que no quede un usuario
sin ninguna forma de iniciar sesión. `AuthService` rechaza cualquier hash vacío o dañado
en vez de dejar pasar la autenticación.

### 2.2 `Inspecciones.ChecklistIdChecklist` — BIGINT NULL — **no intencional**

**Qué pasó.** La entidad `Checklist` declara `ICollection<Inspeccion> Inspecciones`, pero
`Inspeccion` no tiene ninguna propiedad `IdChecklist`. Al no encontrar la llave foránea del
otro lado, EF Core la inventa: crea la columna sombra `ChecklistIdChecklist` y la
restricción `FK_Inspecciones_Checklists_ChecklistIdChecklist`.

**Consecuencia.** Es una columna huérfana: ningún controlador la escribe ni la lee, y
siempre queda en `NULL`. El efecto de fondo es que **no se registra con qué checklist se
ejecutó cada inspección**; hoy solo puede deducirse indirectamente recorriendo
`RespuestasInspeccion → PreguntasChecklist → IdChecklist`, lo que falla en cuanto una
inspección se guarda sin respuestas.

**Pendiente de decidir.** Dos salidas, y conviene tomarla antes de que haya datos reales:

- Agregar `IdChecklist` explícito a `Inspecciones` (lo coherente con el SRS: la inspección
  se ejecuta *sobre* un checklist versionado, y el reporte de cumplimiento de la sección 8
  lo necesita).
- O quitar la navegación `Checklist.Inspecciones` y dejar que la columna desaparezca.

---

## 3. Cambios de tipo de dato

### 3.1 `Evidencias.UsuarioCarga` — de `BIGINT` a `NVARCHAR(100)`

El SRS lo define como `BIGINT`, es decir una llave foránea a `Usuarios`. En la
implementación es texto: guarda el nombre o identificador de quien cargó el archivo.

**Por qué.** Evita una dependencia dura con `Usuarios` para un dato que en la práctica se
usa solo para mostrar y filtrar, y permite conservar la trazabilidad aunque el usuario se
elimine. Se le puso índice para que el filtrado por cargador siga siendo barato.

**Costo.** Se pierde integridad referencial y la posibilidad de hacer JOIN con `Usuarios`.
Si el reporte "historial por técnico" tiene que cubrir también las evidencias, habrá que
volver al `BIGINT` con FK.

### 3.2 `VARCHAR` → `NVARCHAR` y `DATETIME` → `datetime2`

Todas las columnas de texto del SRS (`VARCHAR(n)` y `VARCHAR(MAX)`) quedaron como
`NVARCHAR(n)` y `NVARCHAR(MAX)`; las de fecha, como `datetime2`.

**Por qué.** Es el mapeo por defecto de EF Core sobre SQL Server para `string` y
`DateTime`. No fue una decisión explícita, pero es la correcta para este dominio:
`NVARCHAR` guarda Unicode, así que nombres de equipo, observaciones y hallazgos con
tildes o «ñ» no se corrompen; `datetime2` tiene más precisión y mayor rango que `datetime`.

**Costo.** `NVARCHAR` ocupa el doble de bytes por carácter. Si el volumen lo justifica, se
puede forzar `varchar` con `IsUnicode(false)` en `ECARDbContext`, columna por columna.

### 3.3 Llaves primarias como IDENTITY

Todas las PK `BIGINT` se crearon como `IDENTITY(1,1)`. El SRS no especifica la estrategia
de generación; se usó la autoincremental por ser la predeterminada y suficiente para el MVP.

---

## 4. Nulabilidad

El SRS lista campos y tipos, pero no dice cuáles son obligatorios. Estas son las decisiones
que se tomaron y su motivo.

| Tabla | Obligatorias (NOT NULL) | Opcionales (NULL) | Motivo |
|-------|------------------------|-------------------|--------|
| Equipos | `CodigoInterno`, `ActivoFijo`, `NombreEquipo`, `Activo`, `FechaCreacion` | `SerialFabricante`, `Marca`, `Modelo`, `Fabricante`, `Criticidad`, `IdCategoria`, `IdUbicacion`, `QRCode` | Un equipo se puede dar de alta con lo mínimo para identificarlo y clasificarlo después; el QR se genera más adelante |
| Usuarios | `Nombre`, `Correo`, `PasswordHash`, `Activo` | `UsuarioAD` | Hay usuarios solo locales, sin cuenta de dominio |
| Inspecciones | `IdEquipo`, `IdUsuario`, `FechaInspeccion` | `Resultado`, `Observaciones`, `FirmaDigital` | Una inspección puede quedar en curso: se abre sin resultado y se firma al cerrarla |
| RespuestasInspeccion | `IdInspeccion`, `IdPregunta` | `Respuesta`, `Observacion` | Permite dejar constancia de una pregunta no respondida |
| Hallazgos | `Descripcion`, `FechaRegistro` | `Criticidad`, `Estado` | El hallazgo se registra primero y se clasifica después |
| Evidencias | `Archivo`, `FechaCarga`, `UsuarioCarga` | — | Una evidencia sin archivo no tiene sentido |
| Auditoria | `Tabla`, `RegistroId`, `Accion`, `Usuario`, `FechaHora` | `ValorAnterior`, `ValorNuevo` | Un alta no tiene valor anterior; una baja no tiene valor nuevo |

**Valores por defecto.** `Activo = true` y las fechas (`FechaCreacion`, `FechaCarga`,
`FechaRegistro`, `FechaHora` = `DateTime.UtcNow`) se asignan **en la aplicación**, no con
un `DEFAULT` de SQL Server. Un `INSERT` hecho por fuera de la API tiene que informarlos
explícitamente. Las fechas se guardan en UTC.

---

## 5. Restricciones únicas e índices

El SRS no define ninguno. Los siguientes se agregaron en `ECARDbContext.OnModelCreating`.

### 5.1 Restricciones UNIQUE

| Tabla | Columnas | Regla de negocio que protege |
|-------|----------|------------------------------|
| Equipos | `CodigoInterno` | El código interno identifica al equipo (sección 3 del SRS) |
| Equipos | `ActivoFijo` | Un activo fijo no puede estar en dos equipos |
| CategoriasEquipo | `Nombre` | Evita categorías duplicadas que fragmentan los reportes |
| Ubicaciones | `Planta` + `Area` | La ubicación es la pareja planta/área; repetirla duplica el destino |
| Usuarios | `Correo` | El correo es la credencial de login local |
| Usuarios | `UsuarioAD` (filtrado, `WHERE UsuarioAD IS NOT NULL`) | Una cuenta de dominio pertenece a un solo usuario; el filtro permite varios usuarios sin AD |
| Roles | `Nombre` | Evita dos roles «Administrador» con permisos distintos |
| UsuarioRol | `IdUsuario` + `IdRol` | Un usuario no puede tener el mismo rol dos veces |
| Checklists | `Nombre` + `Version` | El versionado del SRS exige que la pareja sea única |
| RespuestasInspeccion | `IdInspeccion` + `IdPregunta` | Cada pregunta se responde una sola vez por inspección |

### 5.2 Índices de consulta

Se indexaron todas las llaves foráneas más las columnas por las que filtran los reportes de
la sección 8 del SRS:

- **Equipos**: `IdCategoria`, `IdUbicacion`, `Criticidad` — reportes por área y por criticidad.
- **Checklists**: `Activo`. **PreguntasChecklist**: `IdChecklist`, `TipoRespuesta`.
- **Inspecciones**: `IdEquipo`, `IdUsuario`, `FechaInspeccion`, `Resultado` — historial por
  equipo, por técnico y por rango de fechas.
- **Hallazgos**: `IdInspeccion`, `Criticidad`, `Estado`, `FechaRegistro` — tablero de
  hallazgos abiertos.
- **Evidencias**: `IdInspeccion`, `UsuarioCarga`, `FechaCarga`.
- **Auditoria**: `Tabla`, `RegistroId`, `Accion`, `Usuario`, `FechaHora`, más el índice
  compuesto `Tabla + RegistroId + FechaHora`, que es la consulta natural de auditoría
  («todo lo que le pasó a este registro, en orden»).

---

## 6. Reglas de borrado

El SRS no define comportamiento ante borrados. Quedó así:

| Relación | Regla | Efecto |
|----------|-------|--------|
| Inspecciones → Equipos, Usuarios | CASCADE | Borrar un equipo o un usuario arrastra sus inspecciones |
| RespuestasInspeccion → Inspecciones, PreguntasChecklist | CASCADE | — |
| Evidencias → Inspecciones | CASCADE | — |
| Hallazgos → Inspecciones | CASCADE | — |
| PreguntasChecklist → Checklists | CASCADE | Borrar un checklist arrastra sus preguntas |
| UsuarioRol → Usuarios, Roles | CASCADE | Quitar un usuario o un rol limpia sus asignaciones |
| Equipos → CategoriasEquipo, Ubicaciones | NO ACTION | Una categoría o ubicación en uso no se puede borrar |

**Riesgo y mitigación.** El cascada sobre inspecciones es agresivo: un `DELETE` sobre un
usuario borraría su historial completo de inspecciones, evidencias y hallazgos, que es
justo lo que la sección de auditoría del SRS pide conservar. Por eso **la API no borra
físicamente**: `Usuarios`, `Equipos`, `Checklists`, `CategoriasEquipo` y `Ubicaciones` usan
borrado lógico (`Activo = false`), y las categorías y ubicaciones en uso responden HTTP 409
en vez de intentar el borrado.

La mitigación vive en la aplicación, no en la base. Un `DELETE` ejecutado directamente por
SQL sí destruye el historial.

---

## 7. Dominio de valores de `TipoRespuesta`

No es un cambio de esquema: la columna sigue siendo `NVARCHAR(50)`.

El SRS deja el tipo de respuesta como texto libre. La aplicación lo cerró a dos valores,
definidos en `ECAR.Shared/TiposRespuesta.cs`:

| Valor guardado | Significado | Control en pantalla |
|----------------|-------------|---------------------|
| `SiNo` | Sí / No | Dos casillas; se marca la correcta |
| `Texto` | Rellenar información | Campo de texto libre |

`ChecklistsController` valida contra ese catálogo al crear y al actualizar un checklist, y
devuelve un mensaje explícito si llega un valor fuera de lista.

**Por qué en la aplicación y no con un `CHECK`.** El catálogo va a crecer (numérico,
selección múltiple, fotografía estaban previstos y se retiraron del MVP); dejarlo en código
evita una migración por cada tipo nuevo. Si se decide congelarlo, el `CHECK` en base es el
siguiente paso.

**Efecto sobre datos previos.** Antes de esta validación el tipo se escribía a mano desde
el formulario de checklist, así que pueden existir filas con valores arbitrarios
(`"Numérico"`, `"Fotografía"`, texto suelto). Esas filas se leen sin problema, pero al
editar el checklist que las contiene hay que reelegir el tipo: el desplegable aparece vacío
y la API rechaza el guardado hasta que el valor esté en el catálogo.

---

## 8. Tablas sin cambios respecto al SRS

`Roles`, `UsuarioRol`, `CategoriasEquipo`, `Ubicaciones`, `Checklists`,
`PreguntasChecklist` y `Auditoria` conservan exactamente los campos del SRS. Las únicas
diferencias son las transversales ya descritas: tipos `NVARCHAR`/`datetime2`, nulabilidad,
índices y reglas de borrado.

---

## 9. Datos iniciales

El SRS no define datos semilla. `DataSeeder` inserta, de forma idempotente, al arrancar la API:

- **Roles**: `Administrador`, `Técnico`, `Auditor`.
- **Usuario administrador**: `admin@ecar.com` (`UsuarioAD = admin`), con el rol
  `Administrador` asignado. La contraseña se toma del secreto `AdminPassword`; si no está
  configurado, la API no arranca, para que nunca quede un administrador con clave fija en
  el repositorio.
- **Categorías**: Instrumentación, Equipos de Laboratorio, Equipos de Producción,
  Servicios Industriales.
- **Ubicaciones**: Planta Principal / Producción, Control de Calidad y Almacén.

Cada grupo se siembra por separado comprobando qué falta, de modo que una base parcialmente
poblada se pueda completar sin duplicar nada.

**Nota**: no existe un rol llamado «Inspector». El inspector del SRS es el **Técnico**.
El buscador «Inspector» del formulario de inspecciones consulta `/api/usuarios` con el término
escrito y **no filtra por rol ni por estado**, así que también ofrece administradores,
auditores y usuarios desactivados. Si se decide que solo los técnicos activos pueden firmar
una inspección, hay que filtrar en la consulta —y ese endpoint hoy exige rol `Administrador`,
de modo que un técnico no puede abrir el formulario de alta.
