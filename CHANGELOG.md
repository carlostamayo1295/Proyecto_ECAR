# Changelog

Los cambios relevantes de ECAR se documentan aquí desde la Fase 4. Las fechas usan la zona
horaria de Colombia.

## [Sin publicar] — Fase 4

### Añadido

- Matriz de trazabilidad entre el SRS, CFR 21 Parte 11, código y pruebas.
- Revisión de integración backend con estado de ECAR-209 a ECAR-216.

### Corregido (rama `integracion-fase4`, 18/10/2026, pruebas a fondo)

- Hallazgos: la descripción de una novedad no se edita y editar exige motivo.
- Criticidad cerrada (Alta, Media, Baja) en equipos y hallazgos.
- `POST /api/inspecciones` usa la fecha del servidor; la pantalla ya no deja elegirla.
- No se inicia ni se firma una inspección con un checklist sin preguntas, y esos checklists no se ofrecen.
- Consulta pública del QR sin sesión: solo nombre, código y ubicación.
- `PreguntasChecklistController` con DTOs (arregla el 500 de `/openapi/v1.json`), valida el tipo de respuesta
  y no cambia un checklist que ya tiene respuestas.
- Cabeceras de seguridad en el API y HSTS fuera de desarrollo.

### Añadido (rama `integracion-fase4`, 18/10/2026)

- **Tablero** de la portada (SRS §6) con `GET /api/dashboard`.
- **Escáner de QR** en «Iniciar inspección» con html5-qrcode 2.3.8 (Apache-2.0), servido desde el cliente.
- Cliente: menú y Scalar ocultos sin sesión (Scalar solo para el Administrador en desarrollo), Enter inicia
  sesión, Sí/No con teclado, «Con novedad», Auditoría sin desborde a 375 px y las pantallas de administración
  comprueban el rol antes de cargar.

### Integrado (rama `integracion-fase4`, 18/10/2026)

- Entran las ramas de BE-1 (`feature/anulacion-inspecciones-auditoria-Juandavid`) y BE-3
  (`seguridad-de-cuentas-y-firma-verificable`); su código sustituye al de `fase_4_prueba` en hallazgos,
  anulación, auditoría, cuentas y firma.
- Control de concurrencia (`RowVersion`) en inspecciones y hallazgos, hallazgo ligado a su respuesta y
  FK sin borrado en cascada (4 migraciones del 17/10).
- La huella HMAC incluye el SHA-256 de cada foto vigente; el API no arranca sin `Firma:ClaveHmac`.
- Ajustes al contrato: sin `DELETE` (405), cerrar un hallazgo exige pasar por EnProceso, el responsable
  debe ser Técnico o Administrador, integridad solo para Administrador y Auditor, `PUT /api/usuarios`
  sin contraseña, correo fijo tras firmar y la fecha de la Fase 3 leída de SQL Server como UTC.

### Añadido (rama `fase_4_prueba`, 16/10/2026)

- **Hallazgos (ECAR-211/212):** endpoints 4 a 9 con filtros, detalle con origen, alta manual,
  edición limitada, transiciones validadas (409), cierre con acción correctiva, reabrir solo el
  Administrador y anulación con motivo. Un hallazgo por cada "No" al firmar, en la misma transacción.
- **Inspecciones:** anular con motivo (endpoint 10); la inspección y sus fotos se conservan.
- **Auditoría:** filtros, historial de un registro y verificación de la cadena por lotes
  (endpoints 1 a 3); exportación a Excel y PDF (21).
- **Reportes (ECAR-213):** los seis del SRS §7 con vista previa paginada, PDF y Excel, fechas
  obligatorias (máximo un año) y cumplimiento según `FrecuenciaInspeccion`; registro PDF de cada
  inspección firmada (14).
- **Evidencias (ECAR-214):** retirar con motivo solo en inspecciones en curso; las retiradas no
  entran en la ejecución, el resultado ni la huella.
- **Firma (ECAR-215):** contraseña en cada firma (400 si es errónea, 423 si la cuenta está
  bloqueada), manifestación con nombre, significado y zona horaria, HMAC-SHA256 con
  `Firma:ClaveHmac` y verificación de integridad (13).
- **Cuentas (ECAR-216):** bloqueo tras 5 intentos con eventos de acceso, política e historial de
  contraseñas, caducidad a 90 días, cambio obligatorio tras restablecer, desbloquear, y
  `PUT /api/usuarios/{id}` ya no fija contraseñas (22 a 26).
- **§3.3:** motivo obligatorio al modificar equipos y checklists y al desactivar o reactivar.
- Frecuencia de inspección editable en equipos y evento `CierreSesion`.

### Cambiado

- Sin `DELETE` de inspecciones, evidencias ni hallazgos (responden 405).
- PDF con PDFsharp-MigraDoc **6.2.4**, la última versión MIT (la 6.5 es AGPL-3.0), y Excel con ClosedXML (MIT).
- `System.Security.Cryptography.Pkcs` 10.0.13 para cerrar las alertas NU1902/NU1903.

### Corregido

- La huella de la firma se recalculaba mal al leer la inspección de SQL Server: la fecha llega sin
  zona y se tomaba como hora local. Afectaba también a la verificación SHA-256 de la Fase 3.

### Verificado

- 143 pruebas de API y 11 de integración contra SQL Server superadas el 16/10/2026.
- Prueba de punta a punta contra el API: 62 de 62 comprobaciones.

### Pendiente

- Confirmar con ECAR la definición de cumplimiento, el texto del significado de la firma y la
  compresión de fotos (ALCOA+ "Original").
- Configurar `Firma:ClaveHmac` en cada entorno; en la Fase 6, quitar al login de la aplicación los
  permisos `ALTER` y `db_owner` (el trigger no frena a un `db_owner`).

## [2026-10-09] — Base backend de Fase 4 (ECAR-209/210)

### Añadido

- Migración `Fase4HallazgosAuditoria` con los campos de hallazgos, auditoría, evidencias,
  firma, seguridad de cuentas y frecuencia de inspección.
- Conversión de hallazgos heredados a estado `Abierto` y sellado de auditorías existentes.
- Trigger `TR_Auditoria_SoloInsercion` para impedir `UPDATE` y `DELETE` en auditoría.
- Auditoría automática y transaccional desde `ECARDbContext`, con usuario, IP, agente,
  valores anterior/nuevo y motivo.
- Cadena SHA-256 de integridad para las filas de auditoría.
- Revocación inmediata de JWT mediante `SecurityStamp`.
- Proyecto `ECAR.IntegrationTests` contra SQL Server para migraciones, transacciones, trigger
  y concurrencia de la cadena de auditoría.

### Verificado

- Si falla la escritura de auditoría también se revierte el cambio funcional.
- Los guardados concurrentes conservan una única cadena válida.
- El modelo EF no tiene cambios pendientes de migración.
- 95 pruebas de API y 8 pruebas de integración superadas el 11 de octubre de 2026.

## [2026-10-04] — Cierre técnico de Fase 3

### Añadido

- Flujo completo de inspecciones, respuestas, evidencias y firma.
- Inicio manual y mediante QR, resultado histórico e inmutabilidad tras el cierre.
- Autorización por rol y propiedad de la inspección.

### Corregido

- Contrato de respuestas `Si/No`, filtros, paginación, rutas de evidencias y manejo de sesión.
