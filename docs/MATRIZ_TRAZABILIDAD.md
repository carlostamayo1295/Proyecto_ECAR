# Matriz de trazabilidad — ECAR MVP

**Corte:** 22 de octubre de 2026  
**Rama:** `Prueba_nuevo_novedades` (`develop` + novedades: reemplazan a los hallazgos, se resuelven con firma y avisan por correo)  
**Alcance revisado:** reglas de negocio del SRS MVP y controles técnicos de la Fase 4.

Esta matriz relaciona cada requisito con su implementación y su evidencia automatizada. Un estado
`Parcial` significa que existe una base técnica, pero todavía depende de una tarea backend o frontend
que no está integrada en `develop`.

## Reglas de negocio del SRS

| ID | Requisito | Implementación principal | Evidencia | Estado |
|---|---|---|---|---|
| RN-01 | Código interno y activo fijo obligatorios | `Equipo`, `CreateEquipoDto`, `EquiposController` | `BackendPhaseOneTests` | Cumple |
| SRS §2 | Clasificación por criticidad | `Criticidades` (Alta, Media, Baja) en `EquiposController`; la novedad hereda la del equipo | `CorreccionesPruebasTests.La_criticidad_solo_admite_Alta_Media_o_Baja` | Cumple |
| RN-02 | La inspección pertenece al usuario autenticado | `CurrentUser`, `InspeccionesController`, `InspeccionService`; la fecha la pone el servidor (ALCOA+ contemporáneo) | `BackendPhaseThreeTests`, `CorreccionesPruebasTests.La_fecha_de_una_inspeccion_creada_por_el_administrador_es_la_del_servidor` | Cumple |
| RN-03 | Obligatorias respondidas antes del cierre | `InspeccionService`, `InspeccionesController.FirmarInspeccion`; no se inicia ni se firma con un checklist sin preguntas | `BackendPhaseThreeFirmaTests`, `CorreccionesPruebasTests.No_se_inspecciona_con_un_checklist_sin_preguntas` | Cumple |
| RN-04 | Una novedad exige observación | `InspeccionService`, validación de firma | `BackendPhaseThreeFirmaTests.Firmar_RechazaNovedadSinObservacion` | Cumple |
| RN-04 | Notificación a la mesa de ayuda | Un solo correo por inspección al firmar, con todas sus novedades (22/10/2026; antes uno por novedad): `ColaNotificaciones`, `EnvioNotificacionesService`, `CorreoNovedad`; SMTP con `Correo:*` (sin credenciales, `.eml` en `Correo:CarpetaSalida`); reenvío por el Administrador (manda el de la inspección) | `NovedadesTests.Firmar_con_dos_No_crea_dos_novedades_pendientes_y_la_inspeccion_queda_pendiente`, `NovedadesTests.Reenviar_el_correo_pone_en_cola_el_de_su_inspeccion`, `NotificacionNovedadesTests`; prueba contra SQL Server el 22/10: tres «No», un solo `.eml` | Cumple en código; falta la cuenta de correo de ECAR y confirmar los datos del mensaje |
| SRS §2 | Gestión de novedades | Una por cada «No» al firmar (`Novedades`); la inspección queda `NovedadesPendientes` / `Pendiente` y pasa a `Cerrada` / `Conforme` al resolverse la última; resolución con firma (texto, firma dibujada y contraseña) por un Estándar o el Administrador, que consulta en solo lectura la inspección completa (`AccesoInspeccion`); el Operador solo ve las de sus inspecciones | `NovedadesTests`, `NovedadesTests.El_Operador_solo_ve_las_novedades_de_sus_inspecciones_y_no_abre_las_ajenas`, `NovedadesTests.Quien_resuelve_consulta_en_solo_lectura_la_inspeccion_completa_de_otro`, `NovedadesSqlServerTests.Dos_personas_resuelven_a_la_vez_las_dos_ultimas_y_la_inspeccion_queda_cerrada` | Cumple |
| RN-05 | Toda acción queda en auditoría | `ECARDbContext.Auditoria`, `ContextoAuditoriaHttp`, `CadenaAuditoria`, `IMotivoCambio` en cada acción con motivo | `AuditoriaTransaccionalTests`, `AuditoriaSqlServerTests`, `AuditoriaConsultaTests`, `AuditoriaHistorialTests`, `NovedadesTests.El_administrador_anula_con_motivo_y_si_era_la_ultima_cierra_la_inspeccion` | Cumple |
| RN-06 | Los históricos no se modifican | Inmutabilidad de cerradas y anuladas, trigger `TR_Auditoria_SoloInsercion`, sin `DELETE` de inspecciones, evidencias ni novedades; una novedad no se edita | `BackendPhaseThreeFirmaTests`, `BorradoInspeccionTests`, `InspeccionesAnulacionTests`, `AuditoriaSqlServerTests.El_trigger_impide_modificar_o_borrar_la_auditoria_incluso_por_SQL`, `NovedadesTests.Ninguna_ruta_borra_ni_edita_inspecciones_novedades_ni_evidencias`, `CorreccionesPruebasTests.Las_preguntas_validan_el_tipo_y_no_cambian_un_checklist_ya_usado` | Cumple |
| RN-07 | Un usuario puede tener varios roles | `UsuarioRol`, `UsuariosController`, claims JWT múltiples | `BackendPhaseOneTests`, `AuthServiceTests` | Cumple |
| RN-08 | Solo Administrador modifica equipos | Autorización por rol en `EquiposController` | `BackendPhaseOneTests` | Cumple |
| SRS §4 | Roles del sistema | `RolesSistema`: Administrador, Operador (antes Técnico), Estándar y Supervisor (antes Auditor), en cada `[Authorize]` y en el menú; `DataSeeder` renombra los de una base anterior | `BackendPhaseOneTests.El_seeder_renombra_Tecnico_y_Auditor_y_sus_usuarios_conservan_el_rol`, `NovedadesTests.Cada_accion_exige_su_rol`, prueba por HTTP de cada rol 53/53 el 22/10 | Cumple; los roles cambiaron a pedido de ECAR el 21/10 respecto al SRS |
| RN-09 | Fotografías desde dispositivo móvil | `EvidenciasController`, `EvidenciaStorageDisco`, cliente de evidencias | `EvidenciasControllerTests` | Cumple en código; falta prueba final en teléfono real |
| RN-10 | Lectura mediante código QR | Endpoints QR de `EquiposController` (el QR se crea al guardar el equipo), `ConsultaQr.razor` (sin sesión solo nombre, código y ubicación); escáner en la app (`EscanerQrDialog`, html5-qrcode 2.3.8) | `BackendPhaseTwoTests` (incluye `CrearUnEquipoLeGeneraElQrAlGuardarlo`), `CorreccionesPruebasTests.Sin_sesion_el_QR_muestra_solo_nombre_codigo_y_ubicacion`, revisión en pantalla 18/10 con lector simulado; etiqueta impresa en A4 y en 50×70 y 62×40 mm el 22/10 | Cumple en código; falta la prueba con la cámara de un teléfono |
| SRS §6 | Dashboard: equipos, inspecciones y novedades; filtros y PDF (B-01/B-03) | `DashboardController` (`GET /api/dashboard`, `GET /api/dashboard/pdf`), `TableroPdf`, `Pages/Home.razor` | `CorreccionesPruebasTests.El_tablero_cuenta_equipos_inspecciones_y_novedades`, `DashboardPdfTests` | Cumple; el indicador y el reporte de cumplimiento se retiraron a pedido de ECAR (21/10), con la frecuencia de inspección (`ReportesFase4Tests.El_reporte_de_cumplimiento_ya_no_existe`) |

## Fase 4 y CFR 21 Parte 11

| Referencia | Control técnico | Implementación o dueño | Prueba / evidencia | Estado |
|---|---|---|---|---|
| §11.10(a) | Validación del sistema | Esta matriz, pruebas unitarias y proyecto `ECAR.IntegrationTests` | 287 pruebas API + 31 de integración y permisos de cada rol por HTTP 53/53 el 22/10; batería del API 89/89 (permisos de 99 rutas) y 37 comprobaciones de punta a punta de novedades el 19/10 | Cumple; IQ/OQ/PQ en la Fase 6 |
| §11.10(b) | Copias legibles y electrónicas | `RegistroInspeccionPdf`, `ReportesController` (PDF/Excel), `AuditoriaController.Exportar` | `ReportesFase4Tests` (incluye `El_PDF_de_un_reporte_sale_en_A4_horizontal_y_la_tabla_cabe_en_la_hoja`), `AuditoriaEndpointsTests.Exportar_*` | Cumple |
| §11.10(c) | Conservación sin borrado físico | Anular inspecciones y novedades, retirar evidencias; `DELETE` retirados (405) | `BorradoInspeccionTests`, `NovedadesTests.Ninguna_ruta_borra_ni_edita_inspecciones_novedades_ni_evidencias`, `NovedadesSqlServerTests.Una_inspeccion_con_novedades_no_se_puede_borrar`, `InspeccionesSqlServerTests`, `EvidenciasControllerTests.Retirar_*` | Cumple |
| §11.10(d) | Acceso limitado y revocación inmediata | JWT por roles; claim `ecar_sello`; `SelloSeguridad.ValidarTokenAsync`; cabeceras `nosniff`, `X-Frame-Options`, CSP y HSTS fuera de desarrollo | `SelloSeguridadTests`, `SeguridadCuentasTests.ConPasswordPendiente_SoloSePermiteCambiarla`, `SeguridadCuentasTests.Desactivar_InvalidaLasSesionesAnteriores` | Cumple |
| §11.10(e) | Auditoría automática, segura e inmutable | `ECARDbContext.Auditoria`, hashes encadenados, trigger; consulta, historial y verificación por API | `AuditoriaTransaccionalTests`, `AuditoriaSqlServerTests`, `MigracionFase4Tests`, `AuditoriaVerificacionTests.UnaFilaAlterada_SeDetectaYSeIndicaCual`, `AuditoriaVerificacionSqlServerTests` | Cumple |
| §11.10(f) | Secuencia operativa controlada | Inspección: EnCurso → Cerrada / NovedadesPendientes → Cerrada, o EnCurso → Anulada. Novedad: Pendiente → Resuelta (firma) o → Anulada (Administrador); no se reabre | `BackendPhaseThreeFirmaTests`, `NovedadesTests.Al_resolver_la_ultima_la_inspeccion_queda_cerrada_y_conforme_y_sus_sellos_siguen_integros`, `NovedadesTests.Una_resuelta_no_se_vuelve_a_resolver_ni_se_anula` | Cumple |
| §11.10(g) | Comprobación de autoridad | Roles y propiedad (`RolesSistema`); resolver una novedad, Estándar o Administrador; anularla, anular una inspección en curso, restablecer y desbloquear, solo el Administrador; un Estándar lee una inspección ajena solo si tiene novedades, y no la modifica; el Operador, solo las suyas; reportes y auditoría, Administrador y Supervisor | `NovedadesTests.Cada_accion_exige_su_rol`, `NovedadesTests.Sin_novedades_la_inspeccion_sigue_siendo_solo_de_quien_la_ejecuta`, `InspeccionesAnulacionTests.Anular_QuienNoEsAdministrador_Devuelve403AunqueSeaElInspector`, `IntegracionFase4Tests.Verificar_la_integridad_es_solo_de_Administrador_y_Auditor`, `SeguridadCuentasTests` | Cumple |
| §11.10(h) | Origen de los datos | `ContextoAuditoriaHttp`: usuario, IP y agente; validación binaria de imágenes | `SelloSeguridadTests`, `AuditoriaSqlServerTests.Cada_cambio_queda_en_la_auditoria_con_su_cadena` | Cumple |
| §11.10(i) | Personal capacitado | Procedimiento y capacitación de ECAR | No corresponde únicamente al software | Fuera de código |
| §11.10(j) | Responsabilidad por firma | Declaración legal antes de firmar y manifestación visible | `PasoFirma.TextoLegal`, revisión en pantalla 16/10 | Cumple en el sistema; falta el SOP de ECAR |
| §11.10(k) | Control de documentación | Git, PR, este archivo y `CHANGELOG.md` | Revisión de repositorio | Cumple desde este corte |
| §11.50 | Nombre, fecha y significado de la firma | `FirmaNombre`, `FirmaSignificado` guardados al firmar la inspección y al resolver cada novedad («Responsable de la resolución de la novedad»); pantalla, impresión y PDF con zona horaria | `FirmaIntegridadTests.Firmar_GuardaNombreFechaSignificadoYAlgoritmo`, `NovedadesTests.Otro_tecnico_la_resuelve_con_su_firma_y_la_inspeccion_espera_a_la_otra` | Cumple |
| §11.70 | Firma vinculada al registro | HMAC-SHA256 con `Firma:ClaveHmac` fuera de la base, sobre el contenido, la manifestación y la huella de cada foto vigente. Desde las novedades, `HMACSHA256-v2` sin el resultado (cambia al resolverse); las firmadas con v1 se siguen verificando. La resolución de una novedad tiene su propio sello, atado al de la inspección; `GET …/integridad` en los dos | `FirmaIntegridadTests.Integridad_*`, `IntegracionFase4Tests.La_huella_no_incluye_las_evidencias_retiradas`, `FirmaSqlServerTests`, `NovedadesTests.Cambiar_lo_que_se_hizo_despues_de_firmar_rompe_el_sello`, `NovedadesTests.Una_inspeccion_sellada_con_v1_se_sigue_verificando_y_en_v2_el_resultado_no_cuenta` | Cumple |
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
ECAR.API.Tests:         276 superadas, 0 fallidas
ECAR.IntegrationTests:   31 superadas, 0 fallidas

dotnet ef migrations has-pending-model-changes ...
No changes have been made to the model since the last migration.
```

## Regla para actualizar esta matriz

Cada PR de la Fase 4 debe actualizar al menos una fila: pasar su estado, señalar los archivos
finales y enlazar el nombre de la prueba que demuestra el criterio. Una tarea no está terminada
si solo existe el endpoint pero no la prueba o la evidencia de integración.
