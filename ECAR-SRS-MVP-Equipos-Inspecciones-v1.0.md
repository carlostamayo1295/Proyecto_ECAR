# Documento Funcional para Inicio de Desarrollo
# Sistema de Gestión de Equipos e Inspecciones
## Laboratorios ECAR S.A.

**Proyecto:** Sistema de Gestión del Ciclo de Vida de Equipos de Planta  
**Tecnología Base:** ASP.NET Core (.NET 8)  
**Base de Datos:** SQL Server 2022  
**Versión:** 1.0  
**Fecha:** Agosto 2026

---

# 1. Resumen Ejecutivo

## Objetivo

Desarrollar una aplicación web responsive para la gestión de equipos de planta, ejecución de inspecciones y control de trazabilidad, permitiendo a la organización cumplir requisitos de BPM, BPL, Data Integrity y CFR 21.

La solución permitirá:

- Administrar el inventario de equipos.
- Controlar ubicación de activos.
- Ejecutar inspecciones periódicas.
- Capturar evidencias fotográficas.
- Incorporar firma digital.
- Gestionar hallazgos y novedades.
- Mantener trazabilidad completa de auditoría.
- Generar reportes regulatorios y operativos.

Fuente: Documento de Levantamiento de Requerimientos ECAR. 【1-d52a27】

---

# 2. Alcance MVP

## Incluido en la Primera Versión

### Gestión de Equipos

- Inventario de equipos.
- Ficha técnica.
- Clasificación por criticidad.
- Ubicación por área.
- Generación de código QR.
- Consulta histórica.

### Gestión de Inspecciones

- Configuración de checklist.
- Ejecución de inspecciones.
- Registro de novedades.
- Evidencia fotográfica.
- Firma digital.
- Historial de inspecciones.

### Administración

- Gestión de usuarios.
- Gestión de roles.
- Auditoría de acciones.
- Seguridad mediante Active Directory.

### Reportes

- Historial por equipo.
- Historial por área.
- Historial por técnico.
- Equipos con novedades.
- Cumplimiento de inspecciones.
- Exportación PDF.
- Exportación Excel.

Fuente: Documento de Levantamiento de Requerimientos ECAR. 【1-d52a27】

---

# 3. Arquitectura Objetivo

## Backend

- ASP.NET Core 8
- Entity Framework Core
- REST API
- SQL Server 2022

## Frontend

- Blazor
- Diseño Responsive
- Compatible con:
  - Computadores
  - Tablets
  - Dispositivos móviles

## Infraestructura

- IIS
- SQL Server 2022
- Active Directory

## Seguridad

- Autenticación basada en JWT (JSON Web Token) para la versión inicial del MVP, con arquitectura preparada para futura integración con Active Directory (AD) corporativo.
- Control de acceso por roles.
- Auditoría completa de cambios.

---

# 4. Roles del Sistema

## Administrador

Permisos:

- Gestionar equipos.
- Gestionar usuarios.
- Administrar roles.
- Gestionar checklist.
- Consultar auditoría.
- Generar reportes.

## Técnico

Permisos:

- Realizar inspecciones.
- Adjuntar evidencias.
- Registrar observaciones.
- Firmar inspecciones.

## Auditor

Permisos:

- Consulta de equipos.
- Consulta de inspecciones.
- Consulta de auditoría.
- Consulta de reportes.

---

# 5. Modelo de Datos Inicial

## Tabla: Equipos

| Campo | Tipo |
|---------|---------|
| IdEquipo | BIGINT |
| CodigoInterno | VARCHAR(50) |
| ActivoFijo | VARCHAR(50) |
| SerialFabricante | VARCHAR(100) |
| NombreEquipo | VARCHAR(200) |
| Marca | VARCHAR(100) |
| Modelo | VARCHAR(100) |
| Fabricante | VARCHAR(200) |
| Criticidad | VARCHAR(20) |
| IdCategoria | BIGINT |
| IdUbicacion | BIGINT |
| QRCode | VARCHAR(MAX) |
| Activo | BIT |
| FechaCreacion | DATETIME |

---

## Tabla: CategoriasEquipo

| Campo | Tipo |
|---------|---------|
| IdCategoria | BIGINT |
| Nombre | VARCHAR(100) |
| Descripcion | VARCHAR(500) |

---

## Tabla: Ubicaciones

| Campo | Tipo |
|---------|---------|
| IdUbicacion | BIGINT |
| Planta | VARCHAR(100) |
| Area | VARCHAR(100) |
| Descripcion | VARCHAR(300) |

---

## Tabla: Usuarios

| Campo | Tipo |
|---------|---------|
| IdUsuario | BIGINT |
| Nombre | VARCHAR(200) |
| Correo | VARCHAR(150) |
| UsuarioAD | VARCHAR(100) |
| Activo | BIT |

---

## Tabla: Roles

| Campo | Tipo |
|---------|---------|
| IdRol | BIGINT |
| Nombre | VARCHAR(50) |

---

## Tabla: UsuarioRol

| Campo | Tipo |
|---------|---------|
| Id | BIGINT |
| IdUsuario | BIGINT |
| IdRol | BIGINT |

---

## Tabla: Checklists

| Campo | Tipo |
|---------|---------|
| IdChecklist | BIGINT |
| Nombre | VARCHAR(200) |
| Version | VARCHAR(20) |
| Activo | BIT |
| FechaCreacion | DATETIME |

---

## Tabla: PreguntasChecklist

| Campo | Tipo |
|---------|---------|
| IdPregunta | BIGINT |
| IdChecklist | BIGINT |
| Pregunta | VARCHAR(MAX) |
| TipoRespuesta | VARCHAR(50) |
| Obligatoria | BIT |

---

## Tabla: Inspecciones

| Campo | Tipo |
|---------|---------|
| IdInspeccion | BIGINT |
| IdEquipo | BIGINT |
| IdUsuario | BIGINT |
| FechaInspeccion | DATETIME |
| Resultado | VARCHAR(50) |
| Observaciones | VARCHAR(MAX) |
| FirmaDigital | VARCHAR(MAX) |

---

## Tabla: RespuestasInspeccion

| Campo | Tipo |
|---------|---------|
| IdRespuesta | BIGINT |
| IdInspeccion | BIGINT |
| IdPregunta | BIGINT |
| Respuesta | VARCHAR(MAX) |
| Observacion | VARCHAR(MAX) |

---

## Tabla: Evidencias

| Campo | Tipo |
|---------|---------|
| IdEvidencia | BIGINT |
| IdInspeccion | BIGINT |
| Archivo | VARCHAR(MAX) |
| FechaCarga | DATETIME |
| UsuarioCarga | BIGINT |

---

## Tabla: Hallazgos

| Campo | Tipo |
|---------|---------|
| IdHallazgo | BIGINT |
| IdInspeccion | BIGINT |
| Descripcion | VARCHAR(MAX) |
| Criticidad | VARCHAR(20) |
| Estado | VARCHAR(20) |
| FechaRegistro | DATETIME |

---

## Tabla: Auditoria

| Campo | Tipo |
|---------|---------|
| IdAuditoria | BIGINT |
| Tabla | VARCHAR(100) |
| RegistroId | BIGINT |
| Accion | VARCHAR(50) |
| ValorAnterior | VARCHAR(MAX) |
| ValorNuevo | VARCHAR(MAX) |
| Usuario | VARCHAR(100) |
| FechaHora | DATETIME |

---

# 6. Pantallas Iniciales

## Seguridad

### Login

- Usuario corporativo.
- Integración AD.

---

## Dashboard

### Indicadores

- Total de equipos.
- Equipos por criticidad.
- Equipos por área.
- Inspecciones realizadas.
- Inspecciones pendientes.
- Hallazgos abiertos.
- Hallazgos cerrados.

---

## Gestión de Equipos

### Pantallas

- Listado de Equipos.
- Crear Equipo.
- Editar Equipo.
- Ver Ficha Técnica.
- Generar QR.
- Consulta Histórica.

Filtros:

- Planta.
- Área.
- Criticidad.

---

## Gestión de Checklists

### Pantallas

- Listado de Checklists.
- Crear Checklist.
- Editar Checklist.
- Versionamiento.

---

## Inspecciones

### Pantallas

- Selección de Equipo.
- Escaneo QR.
- Ejecución de Checklist.
- Registro de Novedades.
- Adjuntar Evidencias.
- Firma Digital.
- Resultado Final.

---

## Hallazgos

### Pantallas

- Registro de Hallazgo.
- Detalle del Hallazgo.
- Consulta de Hallazgos.

---

## Administración

### Pantallas

- Usuarios.
- Roles.
- Auditoría.

---

## Reportes

### Pantallas

- Reporte por Equipo.
- Reporte por Área.
- Reporte por Técnico.
- Cumplimiento de Inspecciones.
- Equipos con Novedades.
- Exportación PDF.
- Exportación Excel.

---

# 7. Información para Reportes Descargables

## Reporte: Historial por Equipo

Campos:

- Código interno
- Activo fijo
- Nombre del equipo
- Marca
- Modelo
- Área
- Fecha inspección
- Resultado
- Observaciones
- Inspector

---

## Reporte: Historial por Área

Campos:

- Planta
- Área
- Equipo
- Fecha
- Resultado
- Responsable

---

## Reporte: Historial por Técnico

Campos:

- Técnico
- Equipo
- Fecha inspección
- Resultado
- Observaciones

---

## Reporte: Cumplimiento de Inspecciones

Campos:

- Periodo
- Planta
- Área
- Equipos programados
- Equipos inspeccionados
- Porcentaje de cumplimiento

---

## Reporte: Equipos con Novedades

Campos:

- Equipo
- Área
- Hallazgo
- Criticidad
- Fecha
- Responsable
- Estado

---

## Reporte: Inspecciones por Fechas

Filtros:

- Fecha inicial
- Fecha final
- Planta
- Área

Resultado:

- Total inspecciones
- Inspecciones satisfactorias
- Inspecciones con novedad

---

# 8. Reglas de Negocio

1. Los equipos deben tener Código Interno y Activo Fijo obligatorios.
2. Toda inspección debe quedar asociada a un usuario autenticado.
3. Las preguntas marcadas como obligatorias deben responderse antes del cierre.
4. Si existe novedad, la observación será obligatoria.
5. Toda acción debe quedar registrada en auditoría.
6. Los registros históricos no pueden modificarse.
7. Un usuario puede tener múltiples roles.
8. Solo los Administradores pueden modificar estados de los equipos.
9. El sistema debe permitir captura de fotografías desde dispositivo móvil.
10. El sistema debe permitir lectura de código QR.

---

# 9. Backlog Inicial de Desarrollo

## Sprint 1

- Seguridad.
- Roles.
- Usuarios.
- Catálogos.
- Equipos.
- Ubicaciones.

## Sprint 2

- Generación QR.
- Checklists.
- Versionamiento.

## Sprint 3

- Ejecución de inspecciones.
- Evidencias fotográficas.
- Firma digital.

## Sprint 4

- Hallazgos.
- Auditoría.
- Reportes PDF.
- Reportes Excel.

## Sprint 5

- Dashboard.
- Ajustes funcionales.
- Salida a pruebas UAT.

---

# 10. Entregable Esperado

El MVP deberá permitir a Laboratorios ECAR:

- Administrar equipos.
- Ejecutar inspecciones periódicas.
- Obtener trazabilidad completa.
- Gestionar hallazgos.
- Generar evidencia auditable.
- Exportar reportes regulatorios.
- Cumplir requisitos BPM, BPL y Data Integrity.

---