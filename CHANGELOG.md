# Changelog

Los cambios relevantes de ECAR se documentan aquí desde la Fase 4. Las fechas usan la zona
horaria de Colombia.

## [Sin publicar] — Fase 4

### Añadido

- Matriz de trazabilidad entre el SRS, CFR 21 Parte 11, código y pruebas.
- Revisión de integración backend con estado de ECAR-209 a ECAR-216.

### Pendiente

- Integrar el ciclo de vida de hallazgos y la anulación de inspecciones (ECAR-211/212).
- Implementar los seis reportes, exportaciones y el registro PDF (ECAR-213).
- Integrar el retiro lógico de evidencias, sin borrado físico (ECAR-214).
- Implementar firma electrónica HMAC, verificación de integridad y reautenticación (ECAR-215).
- Implementar bloqueo, caducidad, historial y administración de contraseñas (ECAR-216).

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
