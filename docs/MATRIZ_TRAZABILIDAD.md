# Matriz de trazabilidad — ECAR MVP

**Corte:** 16 de octubre de 2026  
**Rama:** `fase_4_prueba` (sobre `develop` `ea2c464`)  
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
| RN-05 | Toda acción queda en auditoría | `ECARDbContext.Auditoria`, `ContextoAuditoriaHttp`, `CadenaAuditoria`, `IMotivoCambio` en cada acción con motivo | `AuditoriaTransaccionalTests`, `AuditoriaSqlServerTests`, `AuditoriaEndpointsTests` | Cumple |
| RN-06 | Los históricos no se modifican | Inmutabilidad de cerradas y anuladas, trigger `TR_Auditoria_SoloInsercion`, sin `DELETE` de inspecciones, evidencias ni hallazgos | `BackendPhaseThreeFirmaTests`, `BorradoInspeccionTests`, `AuditoriaSqlServerTests.El_trigger_impide_modificar_o_borrar_la_auditoria_incluso_por_SQL` | Cumple |
| RN-07 | Un usuario puede tener varios roles | `UsuarioRol`, `UsuariosController`, claims JWT múltiples | `BackendPhaseOneTests`, `AuthServiceTests` | Cumple |
| RN-08 | Solo Administrador modifica equipos | Autorización por rol en `EquiposController` | `BackendPhaseOneTests` | Cumple |
| RN-09 | Fotografías desde dispositivo móvil | `EvidenciasController`, `EvidenciaStorageDisco`, cliente de evidencias | `EvidenciasControllerTests` | Cumple en código; falta prueba final en teléfono real |
| RN-10 | Lectura mediante código QR | Endpoints QR de `EquiposController`, `ConsultaQr.razor` | `BackendPhaseTwoTests` | Cumple |

## Fase 4 y CFR 21 Parte 11

| Referencia | Control técnico | Implementación o dueño | Prueba / evidencia | Estado |
|---|---|---|---|---|
| §11.10(a) | Validación del sistema | Esta matriz, pruebas unitarias y proyecto `ECAR.IntegrationTests` | 143 pruebas API + 11 de integración y 62 comprobaciones de punta a punta el 16/10 | Cumple; IQ/OQ/PQ en la Fase 6 |
| §11.10(b) | Copias legibles y electrónicas | `RegistroInspeccionPdf`, `ReportesController` (PDF/Excel), `AuditoriaController.Exportar` | `ReportesFase4Tests`, `AuditoriaEndpointsTests.Exportar_*` | Cumple |
| §11.10(c) | Conservación sin borrado físico | Anular inspecciones y hallazgos, retirar evidencias; `DELETE` retirados (405) | `BorradoInspeccionTests`, `HallazgosFase4Tests`, `EvidenciasControllerTests.Retirar_*` | Cumple |
| §11.10(d) | Acceso limitado y revocación inmediata | JWT por roles; claim `ecar_sello`; `SelloSeguridad.ValidarTokenAsync` | `SelloSeguridadTests`, `SeguridadCuentasTests.Con_una_contrasena_temporal_solo_se_puede_usar_api_auth` | Cumple |
| §11.10(e) | Auditoría automática, segura e inmutable | `ECARDbContext.Auditoria`, hashes encadenados, trigger; consulta, historial y verificación por API | `AuditoriaTransaccionalTests`, `AuditoriaSqlServerTests`, `MigracionFase4Tests`, `AuditoriaEndpointsTests.Verificar_detecta_una_fila_alterada` | Cumple |
| §11.10(f) | Secuencia operativa controlada | EnCurso → Cerrada/Anulada; Abierto → EnProceso → Cerrado → (Admin) Abierto | `BackendPhaseThreeFirmaTests`, `HallazgosFase4Tests.Abierto_a_Cerrado_sin_pasar_por_EnProceso_es_409` | Cumple |
| §11.10(g) | Comprobación de autoridad | Roles y propiedad; reabrir, anular, restablecer y desbloquear solo el Administrador | `HallazgosFase4Tests.Reabrir_es_solo_del_Administrador`, `SeguridadCuentasTests` | Cumple |
| §11.10(h) | Origen de los datos | `ContextoAuditoriaHttp`: usuario, IP y agente; validación binaria de imágenes | `SelloSeguridadTests`, `AuditoriaSqlServerTests.Cada_cambio_queda_en_la_auditoria_con_su_cadena` | Cumple |
| §11.10(i) | Personal capacitado | Procedimiento y capacitación de ECAR | No corresponde únicamente al software | Fuera de código |
| §11.10(j) | Responsabilidad por firma | Declaración legal antes de firmar y manifestación visible | `PasoFirma.TextoLegal`, revisión en pantalla 16/10 | Cumple en el sistema; falta el SOP de ECAR |
| §11.10(k) | Control de documentación | Git, PR, este archivo y `CHANGELOG.md` | Revisión de repositorio | Cumple desde este corte |
| §11.50 | Nombre, fecha y significado de la firma | `FirmaNombre`, `FirmaSignificado` guardados al firmar; pantalla, impresión y PDF con zona horaria | `SeguridadCuentasTests.Firmar_guarda_la_manifestacion_*` | Cumple |
| §11.70 | Firma vinculada al registro | HMAC-SHA256 con `Firma:ClaveHmac` fuera de la base; `GET …/integridad` | `BackendPhaseThreeFirmaTests.Hash_*`, `FirmaSqlServerTests` | Cumple |
| §11.100(a) | Firma única por persona | Cuentas únicas; correo y usuario AD fijos tras firmar | `SeguridadCuentasTests.Editar_un_usuario_*` | Cumple |
| §11.100(b) | Identidad verificada antes de habilitar firma | Alta de usuario atribuible y auditada | `AuditoriaTransaccionalTests` | Control técnico parcial; requiere procedimiento ECAR |
| §11.200(a) | Reautenticación al firmar | Contraseña en cada firma (400 errónea, 423 bloqueada); restablecer ≠ fijar | `SeguridadCuentasTests.Firmar_*`, `SeguridadCuentasTests.Restablecer_*` | Cumple |
| §11.300(a) | Identificación y contraseña únicas | Índices únicos de correo/usuario AD, BCrypt | Pruebas de Fase 1 | Cumple |
| §11.300(b) | Caducidad e historial de contraseña | 90 días, últimas 5, mínimo 10 con letras y números; cambio obligatorio | `SeguridadCuentasTests.Cambiar_la_contrasena_*`, `…caducada_*` | Cumple |
| §11.300(c) | Revocar credenciales | `SecurityStamp` cambia al desactivar o cambiar contraseña | `SelloSeguridadTests`, `AuditoriaSqlServerTests.Desactivar_un_usuario_renueva_su_sello_en_la_base` | Cumple |
| §11.300(d) | Detectar intentos no autorizados | 5 fallos → bloqueo de 30 min (423), eventos de acceso, aviso en Usuarios | `SeguridadCuentasTests.Cinco_intentos_*` | Cumple |
| §11.300(e) | Dispositivos de autenticación | No se usan tarjetas ni tokens físicos en el MVP | Decisión de alcance | No aplica |

## Evidencia de verificación del corte

```text
dotnet test ECAR.AuditoriaEquipos.slnx
ECAR.API.Tests:         143 superadas, 0 fallidas
ECAR.IntegrationTests:   11 superadas, 0 fallidas

dotnet ef migrations has-pending-model-changes ...
No changes have been made to the model since the last migration.
```

## Regla para actualizar esta matriz

Cada PR de la Fase 4 debe actualizar al menos una fila: pasar su estado, señalar los archivos
finales y enlazar el nombre de la prueba que demuestra el criterio. Una tarea no está terminada
si solo existe el endpoint pero no la prueba o la evidencia de integración.
