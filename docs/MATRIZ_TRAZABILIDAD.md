# Matriz de trazabilidad — ECAR MVP

**Corte:** 18 de octubre de 2026  
**Rama:** `integracion-fase4` (`fase_4_prueba` + ramas de BE-1 y BE-3 + correcciones de las pruebas a fondo)  
**Alcance revisado:** reglas de negocio del SRS MVP y controles técnicos de la Fase 4.

Esta matriz relaciona cada requisito con su implementación y su evidencia automatizada. Un estado
`Parcial` significa que existe una base técnica, pero todavía depende de una tarea backend o frontend
que no está integrada en `develop`.

## Reglas de negocio del SRS

| ID | Requisito | Implementación principal | Evidencia | Estado |
|---|---|---|---|---|
| RN-01 | Código interno y activo fijo obligatorios | `Equipo`, `CreateEquipoDto`, `EquiposController` | `BackendPhaseOneTests` | Cumple |
| SRS §2 | Clasificación por criticidad | `Criticidades` (Alta, Media, Baja) en `EquiposController` y `HallazgosController` | `CorreccionesPruebasTests.La_criticidad_solo_admite_Alta_Media_o_Baja` | Cumple |
| RN-02 | La inspección pertenece al usuario autenticado | `CurrentUser`, `InspeccionesController`, `InspeccionService`; la fecha la pone el servidor (ALCOA+ contemporáneo) | `BackendPhaseThreeTests`, `CorreccionesPruebasTests.La_fecha_de_una_inspeccion_creada_por_el_administrador_es_la_del_servidor` | Cumple |
| RN-03 | Obligatorias respondidas antes del cierre | `InspeccionService`, `InspeccionesController.FirmarInspeccion`; no se inicia ni se firma con un checklist sin preguntas | `BackendPhaseThreeFirmaTests`, `CorreccionesPruebasTests.No_se_inspecciona_con_un_checklist_sin_preguntas` | Cumple |
| RN-04 | Una novedad exige observación | `InspeccionService`, validación de firma | `BackendPhaseThreeFirmaTests.Firmar_RechazaNovedadSinObservacion` | Cumple |
| RN-05 | Toda acción queda en auditoría | `ECARDbContext.Auditoria`, `ContextoAuditoriaHttp`, `CadenaAuditoria`, `IMotivoCambio` en cada acción con motivo | `AuditoriaTransaccionalTests`, `AuditoriaSqlServerTests`, `AuditoriaConsultaTests`, `AuditoriaHistorialTests`, `CorreccionesPruebasTests.Editar_un_hallazgo_exige_motivo` | Cumple |
| RN-06 | Los históricos no se modifican | Inmutabilidad de cerradas y anuladas, trigger `TR_Auditoria_SoloInsercion`, sin `DELETE` de inspecciones, evidencias ni hallazgos | `BackendPhaseThreeFirmaTests`, `BorradoInspeccionTests`, `InspeccionesAnulacionTests`, `AuditoriaSqlServerTests.El_trigger_impide_modificar_o_borrar_la_auditoria_incluso_por_SQL`, `CorreccionesPruebasTests.La_descripcion_de_un_hallazgo_de_novedad_no_se_cambia`, `CorreccionesPruebasTests.Las_preguntas_validan_el_tipo_y_no_cambian_un_checklist_ya_usado` | Cumple |
| RN-07 | Un usuario puede tener varios roles | `UsuarioRol`, `UsuariosController`, claims JWT múltiples | `BackendPhaseOneTests`, `AuthServiceTests` | Cumple |
| RN-08 | Solo Administrador modifica equipos | Autorización por rol en `EquiposController` | `BackendPhaseOneTests` | Cumple |
| RN-09 | Fotografías desde dispositivo móvil | `EvidenciasController`, `EvidenciaStorageDisco`, cliente de evidencias | `EvidenciasControllerTests` | Cumple en código; falta prueba final en teléfono real |
| RN-10 | Lectura mediante código QR | Endpoints QR de `EquiposController`, `ConsultaQr.razor` (sin sesión solo nombre, código y ubicación); escáner en la app (`EscanerQrDialog`, html5-qrcode 2.3.8) | `BackendPhaseTwoTests`, `CorreccionesPruebasTests.Sin_sesion_el_QR_muestra_solo_nombre_codigo_y_ubicacion`, revisión en pantalla 18/10 con lector simulado | Cumple en código; falta la prueba con la cámara de un teléfono |
| SRS §6 | Dashboard: equipos, inspecciones y hallazgos | `DashboardController` (`GET /api/dashboard`), `Pages/Home.razor` | `CorreccionesPruebasTests.El_tablero_cuenta_equipos_inspecciones_y_hallazgos` | Cumple; la definición de "pendientes" es la del cumplimiento, por confirmar con ECAR |

## Fase 4 y CFR 21 Parte 11

| Referencia | Control técnico | Implementación o dueño | Prueba / evidencia | Estado |
|---|---|---|---|---|
| §11.10(a) | Validación del sistema | Esta matriz, pruebas unitarias y proyecto `ECAR.IntegrationTests` | 354 pruebas API + 28 de integración, batería del API 86/86 (permisos de 99 rutas) y 67 comprobaciones de punta a punta el 18/10 | Cumple; IQ/OQ/PQ en la Fase 6 |
| §11.10(b) | Copias legibles y electrónicas | `RegistroInspeccionPdf`, `ReportesController` (PDF/Excel), `AuditoriaController.Exportar` | `ReportesFase4Tests`, `AuditoriaEndpointsTests.Exportar_*` | Cumple |
| §11.10(c) | Conservación sin borrado físico | Anular inspecciones y hallazgos, retirar evidencias; `DELETE` retirados (405) | `BorradoInspeccionTests`, `HallazgosModeloTests.Ninguna_ruta_borra_inspecciones_hallazgos_ni_evidencias`, `InspeccionesSqlServerTests`, `EvidenciasControllerTests.Retirar_*` | Cumple |
| §11.10(d) | Acceso limitado y revocación inmediata | JWT por roles; claim `ecar_sello`; `SelloSeguridad.ValidarTokenAsync`; cabeceras `nosniff`, `X-Frame-Options`, CSP y HSTS fuera de desarrollo | `SelloSeguridadTests`, `SeguridadCuentasTests.ConPasswordPendiente_SoloSePermiteCambiarla`, `SeguridadCuentasTests.Desactivar_InvalidaLasSesionesAnteriores` | Cumple |
| §11.10(e) | Auditoría automática, segura e inmutable | `ECARDbContext.Auditoria`, hashes encadenados, trigger; consulta, historial y verificación por API | `AuditoriaTransaccionalTests`, `AuditoriaSqlServerTests`, `MigracionFase4Tests`, `AuditoriaVerificacionTests.UnaFilaAlterada_SeDetectaYSeIndicaCual`, `AuditoriaVerificacionSqlServerTests` | Cumple |
| §11.10(f) | Secuencia operativa controlada | EnCurso → Cerrada/Anulada; Abierto → EnProceso → Cerrado → (Admin) Abierto | `BackendPhaseThreeFirmaTests`, `HallazgosEstadosTests.Matriz_SoloPermiteLasCincoTransicionesAcordadas` | Cumple |
| §11.10(g) | Comprobación de autoridad | Roles y propiedad; reabrir, anular, restablecer y desbloquear solo el Administrador | `HallazgosEstadosTests.Reabrir_ElAdministradorConMotivo_LimpiaElCierreYConservaLaAccion`, `InspeccionesAnulacionTests.Anular_UnTecnicoQueNoEsElInspector_Devuelve403`, `IntegracionFase4Tests.Verificar_la_integridad_es_solo_de_Administrador_y_Auditor`, `SeguridadCuentasTests` | Cumple |
| §11.10(h) | Origen de los datos | `ContextoAuditoriaHttp`: usuario, IP y agente; validación binaria de imágenes | `SelloSeguridadTests`, `AuditoriaSqlServerTests.Cada_cambio_queda_en_la_auditoria_con_su_cadena` | Cumple |
| §11.10(i) | Personal capacitado | Procedimiento y capacitación de ECAR | No corresponde únicamente al software | Fuera de código |
| §11.10(j) | Responsabilidad por firma | Declaración legal antes de firmar y manifestación visible | `PasoFirma.TextoLegal`, revisión en pantalla 16/10 | Cumple en el sistema; falta el SOP de ECAR |
| §11.10(k) | Control de documentación | Git, PR, este archivo y `CHANGELOG.md` | Revisión de repositorio | Cumple desde este corte |
| §11.50 | Nombre, fecha y significado de la firma | `FirmaNombre`, `FirmaSignificado` guardados al firmar; pantalla, impresión y PDF con zona horaria | `FirmaIntegridadTests.Firmar_GuardaNombreFechaSignificadoYAlgoritmo` | Cumple |
| §11.70 | Firma vinculada al registro | HMAC-SHA256 con `Firma:ClaveHmac` fuera de la base, sobre el contenido, la manifestación y la huella de cada foto vigente; `GET …/integridad` | `FirmaIntegridadTests.Integridad_*`, `IntegracionFase4Tests.La_huella_no_incluye_las_evidencias_retiradas`, `FirmaSqlServerTests` | Cumple |
| §11.100(a) | Firma única por persona | Cuentas únicas; correo y usuario AD fijos tras firmar | `IntegracionFase4Tests.Editar_un_usuario_no_fija_contrasenas_ni_cambia_el_correo_de_quien_firmo` | Cumple |
| §11.100(b) | Identidad verificada antes de habilitar firma | Alta de usuario atribuible y auditada | `AuditoriaTransaccionalTests` | Control técnico parcial; requiere procedimiento ECAR |
| §11.200(a) | Reautenticación al firmar | Contraseña en cada firma (400 errónea, 423 bloqueada); restablecer ≠ fijar | `FirmaIntegridadTests.Firmar_*`, `SeguridadCuentasTests.Restablecer_*`, `IntegracionFase4Tests.Restablecer_no_da_contrasena_local_a_una_cuenta_solo_de_Active_Directory` | Cumple |
| §11.300(a) | Identificación y contraseña únicas | Índices únicos de correo/usuario AD, BCrypt | Pruebas de Fase 1 | Cumple |
| §11.300(b) | Caducidad e historial de contraseña | 90 días, últimas 5, mínimo 10 con letras y números; cambio obligatorio | `SeguridadCuentasTests.CambiarPassword_*`, `SeguridadCuentasTests.PasswordCaducada_ObligaACambiarla` | Cumple |
| §11.300(c) | Revocar credenciales | `SecurityStamp` cambia al desactivar o cambiar contraseña | `SelloSeguridadTests`, `AuditoriaSqlServerTests.Desactivar_un_usuario_renueva_su_sello_en_la_base` | Cumple |
| §11.300(d) | Detectar intentos no autorizados | 5 fallos → bloqueo de 30 min (423), eventos de acceso, aviso en Usuarios | `SeguridadCuentasTests.CincoIntentosFallidos_BloqueanLaCuentaYDevuelven423` | Cumple |
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
