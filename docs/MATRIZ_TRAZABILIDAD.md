# Matriz de trazabilidad — ECAR MVP

**Corte:** 11 de octubre de 2026  
**Rama base:** `develop` (`0be5871`)  
**Alcance revisado:** reglas de negocio del SRS MVP y controles técnicos de la Fase 4.

Esta matriz relaciona cada requisito con su implementación y su evidencia automatizada. Un estado
`Parcial` significa que existe una base técnica, pero todavía depende de una tarea backend o frontend
que no está integrada en `develop`.

## Reglas de negocio del SRS

| ID | Requisito | Implementación principal | Evidencia | Estado |
|---|---|---|---|---|
| RN-01 | Código interno y activo fijo obligatorios | `Equipo`, `CreateEquipoDto`, `EquiposController` | `BackendPhaseOneTests` | Cumple |
| RN-02 | La inspección pertenece al usuario autenticado | `CurrentUser`, `InspeccionesController`, `InspeccionService` | `BackendPhaseThreeTests` | Cumple |
| RN-03 | Obligatorias respondidas antes del cierre | `InspeccionService`, `InspeccionesController.FirmarInspeccion` | `BackendPhaseThreeFirmaTests` | Cumple |
| RN-04 | Una novedad exige observación | `InspeccionService`, validación de firma | `BackendPhaseThreeFirmaTests.Firmar_RechazaNovedadSinObservacion` | Cumple |
| RN-05 | Toda acción queda en auditoría | `ECARDbContext.Auditoria`, `ContextoAuditoriaHttp`, `CadenaAuditoria` | `AuditoriaTransaccionalTests`, `AuditoriaSqlServerTests` | Cumple en el núcleo; faltan motivos en endpoints de BE-1/BE-2/BE-3 |
| RN-06 | Los históricos no se modifican | Inmutabilidad de inspecciones cerradas y trigger `TR_Auditoria_SoloInsercion` | `BackendPhaseThreeFirmaTests`, `AuditoriaSqlServerTests.El_trigger_impide_modificar_o_borrar_la_auditoria_incluso_por_SQL` | Parcial: falta retirar los `DELETE` de Fase 4 |
| RN-07 | Un usuario puede tener varios roles | `UsuarioRol`, `UsuariosController`, claims JWT múltiples | `BackendPhaseOneTests`, `AuthServiceTests` | Cumple |
| RN-08 | Solo Administrador modifica equipos | Autorización por rol en `EquiposController` | `BackendPhaseOneTests` | Cumple |
| RN-09 | Fotografías desde dispositivo móvil | `EvidenciasController`, `EvidenciaStorageDisco`, cliente de evidencias | `EvidenciasControllerTests` | Cumple en código; falta prueba final en teléfono real |
| RN-10 | Lectura mediante código QR | Endpoints QR de `EquiposController`, `ConsultaQr.razor` | `BackendPhaseTwoTests` | Cumple |

## Fase 4 y CFR 21 Parte 11

| Referencia | Control técnico | Implementación o dueño | Prueba / evidencia | Estado |
|---|---|---|---|---|
| §11.10(a) | Validación del sistema | Esta matriz, pruebas unitarias y proyecto `ECAR.IntegrationTests` | 95 pruebas API + 8 de integración verificadas el 11/10 | Cumple para el alcance implementado |
| §11.10(b) | Copias legibles y electrónicas | BE-2: registro PDF, reportes PDF/Excel y exportación de auditoría | Pendiente de rama/PR de ECAR-213 | Pendiente |
| §11.10(c) | Conservación sin borrado físico | BE-1/BE-2: anular hallazgos/inspecciones y retirar evidencias | Ramas ECAR-211/212 y ECAR-214 aún sin integrar | Parcial |
| §11.10(d) | Acceso limitado y revocación inmediata | JWT por roles; claim `ecar_sello`; `SelloSeguridad.ValidarTokenAsync` | `SelloSeguridadTests` | Cumple para revocación; BE-3 completa política de cuentas |
| §11.10(e) | Auditoría automática, segura e inmutable | `ECARDbContext.Auditoria`, hashes encadenados, trigger de solo inserción | `AuditoriaTransaccionalTests`, `AuditoriaSqlServerTests`, `MigracionFase4Tests` | Cumple en el núcleo; BE-1 completa consultas y verificación API |
| §11.10(f) | Secuencia operativa controlada | Flujo EnCurso → Firmada/Cerrada; BE-1 agrega estados de hallazgos | `BackendPhaseThreeFirmaTests`; pruebas BE-1 pendientes | Parcial |
| §11.10(g) | Comprobación de autoridad | Roles y propiedad de la inspección | `BackendPhaseThreeTests`, `BackendPhaseThreeFirmaTests` | Parcial: faltan acciones administrativas BE-1/BE-3 |
| §11.10(h) | Origen de los datos | `ContextoAuditoriaHttp`: usuario, IP y agente; validación binaria de imágenes | `SelloSeguridadTests`, `AuditoriaSqlServerTests.Cada_cambio_queda_en_la_auditoria_con_su_cadena` | Cumple |
| §11.10(i) | Personal capacitado | Procedimiento y capacitación de ECAR | No corresponde únicamente al software | Fuera de código |
| §11.10(j) | Responsabilidad por firma | Declaración legal y manifestación visual | FE-3 / BE-3 | Parcial |
| §11.10(k) | Control de documentación | Git, PR, este archivo y `CHANGELOG.md` | Revisión de repositorio | Cumple desde este corte |
| §11.50 | Nombre, fecha y significado de la firma | Campos de migración listos; implementación BE-3/FE-3 | Pruebas BE-3 pendientes | Parcial |
| §11.70 | Firma vinculada al registro | Campos de algoritmo listos; HMAC e integridad son de BE-3 | Pruebas BE-3 pendientes | Pendiente |
| §11.100(a) | Firma única por persona | Identidad de usuario y restricciones de cuenta | BE-3 | Parcial |
| §11.100(b) | Identidad verificada antes de habilitar firma | Alta de usuario atribuible y auditada | `AuditoriaTransaccionalTests` | Control técnico parcial; requiere procedimiento ECAR |
| §11.200(a) | Reautenticación al firmar | DTO preparado; implementación BE-3 | Pruebas BE-3 pendientes | Pendiente |
| §11.300(a) | Identificación y contraseña únicas | Índices únicos de correo/usuario AD, BCrypt | Pruebas de Fase 1 | Cumple |
| §11.300(b) | Caducidad e historial de contraseña | Entidad y migración `HistorialPasswords`; lógica BE-3 | Pruebas BE-3 pendientes | Parcial |
| §11.300(c) | Revocar credenciales | `SecurityStamp` cambia al desactivar o cambiar contraseña | `SelloSeguridadTests`, `AuditoriaSqlServerTests.Desactivar_un_usuario_renueva_su_sello_en_la_base` | Cumple |
| §11.300(d) | Detectar intentos no autorizados | Bloqueo y auditoría de cuenta son de BE-3 | Pruebas BE-3 pendientes | Pendiente |
| §11.300(e) | Dispositivos de autenticación | No se usan tarjetas ni tokens físicos en el MVP | Decisión de alcance | No aplica |

## Evidencia de verificación del corte

```text
dotnet test ECAR.AuditoriaEquipos.slnx
ECAR.API.Tests:         95 superadas, 0 fallidas
ECAR.IntegrationTests:   8 superadas, 0 fallidas

dotnet ef migrations has-pending-model-changes ...
No changes have been made to the model since the last migration.
```

## Regla para actualizar esta matriz

Cada PR de la Fase 4 debe actualizar al menos una fila: pasar su estado, señalar los archivos
finales y enlazar el nombre de la prueba que demuestra el criterio. Una tarea no está terminada
si solo existe el endpoint pero no la prueba o la evidencia de integración.
