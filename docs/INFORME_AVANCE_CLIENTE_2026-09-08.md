# Informe de Avance para el Cliente
## Sistema de Gestión de Equipos e Inspecciones — Laboratorios ECAR S.A.

**Proyecto:** Sistema de Gestión del Ciclo de Vida de Equipos de Planta (MVP)
**Documentos de referencia:** SRS MVP v1.0 y Cronograma MVP v1.0
**Fecha de corte:** 8 de septiembre de 2026
**Rama evaluada:** `develop`
**Elaborado por:** Equipo de Desarrollo

---

## 1. Resumen ejecutivo

El proyecto se encuentra **al inicio de la Fase 2 del cronograma (semana 4 de 12)**, con la
**Fase 0 y la Fase 1 completadas y verificadas**.

Hoy Laboratorios ECAR ya dispone de una aplicación web funcional que permite **iniciar sesión
de forma segura, administrar usuarios y roles, gestionar los catálogos de categorías y
ubicaciones, y llevar el inventario completo de equipos con su ficha técnica**. Esto
corresponde a las Entregas 1 y 2 comprometidas en el cronograma.

Adicionalmente, y como adelanto sobre lo planificado, ya están construidos los cimientos de
las fases 2, 3 y 4: la **base de datos completa del MVP (las 13 tablas del SRS)** está creada
y en operación, y existen pantallas y servicios iniciales para checklists, inspecciones,
evidencias, hallazgos y consulta de auditoría.

| Indicador | Estado a la fecha |
|---|---|
| Avance global del MVP | **≈ 40 %** |
| Fases completadas | Fase 0 y Fase 1 (2 de 7) |
| Entregas contractuales cumplidas | Entrega 1 (semana 2) y Entrega 2 (semana 4) |
| Modelo de datos del MVP | 13 de 13 tablas implementadas (100 %) |
| Servicios de negocio publicados | 57 operaciones en 12 módulos de API |
| Pantallas construidas | 16 |
| Pruebas automatizadas | 17 (100 % en verde) |
| Estado de compilación | Sin errores |

**Conclusión:** el proyecto avanza **conforme al cronograma**, sin retrasos en los hitos
comprometidos hasta la fecha.

---

## 2. Estado por fase del cronograma

| Fase | Alcance comprometido | Semanas | Estado | Avance |
|---|---|---|---|---|
| **Fase 0.** Inicio y diseño técnico | Arquitectura, modelo de datos, ambientes, estándares | 1 | ✅ Completa | 100 % |
| **Fase 1.** Seguridad y gestión de equipos | Autenticación JWT, usuarios, roles, catálogos, ubicaciones, CRUD de equipos, ficha técnica | 2 – 3 | ✅ Completa | 100 % |
| **Fase 2.** Checklists y gestión QR | Administración y versionamiento de checklists, generación y lectura de QR | 4 – 5 | 🔵 En curso | ≈ 45 % |
| **Fase 3.** Inspecciones y evidencias | Ejecución de inspecciones, respuestas, evidencia fotográfica, firma digital | 6 – 7 | 🟡 Cimientos construidos | ≈ 30 % |
| **Fase 4.** Hallazgos, auditoría y reportes | Hallazgos, auditoría transaccional, reportes PDF y Excel | 8 – 9 | 🟡 Cimientos construidos | ≈ 20 % |
| **Fase 5.** Dashboard y estabilización | Dashboard ejecutivo e indicadores | 10 – 11 | ⚪ No iniciada | 0 % |
| **Fase 6.** UAT y producción | Pruebas de aceptación, capacitación, despliegue | 12 | ⚪ No iniciada | 0 % |

---

## 3. Qué está funcionando hoy (entregable y demostrable)

Las siguientes funcionalidades están **operativas de extremo a extremo** (pantalla → servicio
→ base de datos) y pueden ser revisadas por ECAR en una sesión de demostración.

### 3.1 Seguridad y acceso

- **Inicio de sesión** con usuario y contraseña, protegido con cifrado de contraseñas y token
  de sesión (JWT) con vencimiento controlado.
- **Tres roles operativos** según el SRS: **Administrador**, **Técnico** y **Auditor**, con
  permisos diferenciados. Un usuario puede tener varios roles.
- **Control de acceso aplicado en el servidor**, no solo en la pantalla: aunque un usuario
  intente invocar directamente un servicio, el sistema valida su rol antes de responder.
- **Preparación para Active Directory:** la aplicación admite tres modos de autenticación
  (local, Active Directory corporativo, o híbrido) y se conmuta **por configuración, sin
  recompilar ni redesplegar el sistema**. La conexión real queda pendiente únicamente de que
  ECAR entregue los datos de conexión al directorio corporativo (ver sección 6).

### 3.2 Gestión de equipos

- **Inventario completo de equipos** con alta, consulta, edición y baja.
- **Ficha técnica** por equipo: código interno, activo fijo, serial de fabricante, nombre,
  marca, modelo, fabricante, criticidad, categoría y ubicación.
- **Filtros y búsqueda** por texto libre, criticidad, categoría, ubicación, planta, área y
  estado, con resultados paginados.
- **Baja lógica:** los equipos se desactivan en lugar de eliminarse, preservando la
  trazabilidad histórica exigida por Data Integrity.
- **Reglas de negocio activas:** código interno y activo fijo son obligatorios y únicos; solo
  el Administrador puede modificar el estado de un equipo.

### 3.3 Catálogos maestros

- **Categorías de equipo** y **Ubicaciones (planta / área)** con administración completa.
- **Protección de integridad:** el sistema **impide eliminar** una categoría o una ubicación
  que esté siendo utilizada por algún equipo, informando el motivo al usuario.
- Combinación planta + área única, para evitar duplicados en el maestro de ubicaciones.

### 3.4 Administración de usuarios

- Alta, edición, activación y desactivación de usuarios.
- Asignación de uno o varios roles por usuario.
- **Salvaguarda regulatoria:** el sistema **no permite desactivar ni eliminar al último
  administrador activo**, evitando que la organización quede sin control del sistema.

### 3.5 Experiencia de usuario

- Interfaz web construida con **diseño responsive**, apta para computador, tablet y móvil, con
  identidad visual corporativa aplicada.
- Mensajes de confirmación y de error consistentes en todas las pantallas.

---

## 4. Qué está en construcción (Fase 2 en curso y adelantos)

Estas funcionalidades ya tienen pantalla y servicios, pero **aún no se consideran entregadas**
porque les falta completar su alcance funcional o su capa de seguridad:

| Funcionalidad | Lo que ya existe | Lo que falta |
|---|---|---|
| **Checklists** | Creación, edición, consulta y **versionamiento** (nombre + versión únicos); tipos de respuesta acotados a un catálogo controlado (*Sí/No* y *Texto*) | Publicar la administración de preguntas contra el servicio definitivo y aplicar el control de acceso por rol |
| **Preguntas de checklist** | Pantalla de administración construida | Opera todavía sobre **datos de demostración**; requiere su servicio propio en el API |
| **Inspecciones** | Registro, consulta, edición y detalle de inspecciones asociadas a equipo e inspector; búsqueda asistida de equipo e inspector | Flujo guiado de **ejecución** de la inspección (responder el checklist paso a paso) y control de acceso por rol |
| **Respuestas de inspección** | Pantalla de administración construida y componente de captura según el tipo de pregunta | Opera todavía sobre **datos de demostración**; requiere su servicio propio en el API |
| **Evidencias** | Registro y consulta de evidencias asociadas a una inspección | **Carga real de archivos fotográficos** desde cámara o dispositivo móvil y su almacenamiento |
| **Hallazgos** | Registro, consulta, edición y filtro por inspección y por estado | Ciclo de vida completo (asignación de responsable y cierre) y control de acceso por rol |
| **Auditoría** | Pantalla de consulta con filtros y paginación | **Registro automático** de las acciones (ver sección 5) |

---

## 5. Qué está pendiente

Los siguientes elementos del SRS **no han iniciado** y están planificados en sus fases
correspondientes:

1. **Generación y lectura de código QR (Fase 2).** El campo ya existe en la base de datos y en
   la ficha del equipo, pero la generación automática de la etiqueta QR y su escaneo desde el
   móvil están pendientes.
2. **Firma digital de inspecciones (Fase 3).** El sistema ya almacena y muestra si una
   inspección está firmada; falta el componente de captura de firma en pantalla.
3. **Captura fotográfica desde dispositivo móvil (Fase 3).**
4. **Auditoría automática de acciones (Fase 4).** Hoy la auditoría se puede consultar, pero
   el registro automático e inmutable de altas, cambios y bajas —requisito central de BPM,
   BPL, Data Integrity y CFR 21— está pendiente de implementación. **Este es el elemento
   regulatorio más relevante del backlog restante.**
5. **Reportes descargables (Fase 4).** Los seis reportes del SRS (historial por equipo, por
   área, por técnico, cumplimiento de inspecciones, equipos con novedades e inspecciones por
   fechas) y su exportación a **PDF y Excel**.
6. **Dashboard ejecutivo (Fase 5).** Indicadores de equipos, criticidad, inspecciones y
   hallazgos.
7. **Despliegue en el servidor de ECAR (Fase 6).** Publicación en IIS, certificado HTTPS y
   conexión a la base de datos corporativa.

---

## 6. Puntos que requieren decisión o insumo de ECAR

Para no afectar el cronograma, solicitamos atención a los siguientes puntos:

| # | Tema | Qué necesitamos de ECAR | Impacto si se demora |
|---|---|---|---|
| 1 | **Active Directory** | Servidor, puerto, dominio, tipo de conexión segura y una **cuenta de prueba** | El MVP saldría a UAT con autenticación local; la integración AD se aplazaría |
| 2 | **Almacenamiento de evidencias** | Definir si las fotografías se guardan en el servidor de aplicaciones, en un recurso compartido de red o en la base de datos, y el tamaño máximo por archivo | Bloquea el cierre de la Fase 3 |
| 3 | **Firma digital** | Confirmar si es suficiente una **firma manuscrita en pantalla + usuario autenticado + estampa de tiempo**, o si se exige certificado digital | Cambia el alcance y el esfuerzo de la Fase 3 |
| 4 | **Programación de inspecciones** | Definir la **periodicidad** por equipo o por criticidad; el reporte de *cumplimiento* requiere saber qué equipos estaban **programados** en el periodo | Bloquea el reporte de cumplimiento (Fase 4) |
| 5 | **Ambiente de despliegue** | Servidor IIS, instancia de SQL Server 2022 y certificado HTTPS | Bloquea la Fase 6 (UAT y producción) |
| 6 | **Nota técnica** | El SRS indica .NET 8; la solución se construyó sobre **.NET 10** (versión vigente y con soporte extendido). Requerimos su validación formal | Debe confirmarse antes del despliegue en la infraestructura de ECAR |

---

## 7. Calidad y verificación

Como parte del compromiso de calidad del entregable, a la fecha de corte se ejecutaron las
siguientes verificaciones:

| Verificación | Resultado |
|---|---|
| Compilación de la solución completa | **Sin errores** |
| Batería de pruebas automatizadas | **17 de 17 correctas** |
| Consistencia del modelo de datos con la base de datos | **Sin diferencias pendientes** |
| Cobertura del modelo de datos frente al SRS | **13 de 13 tablas** implementadas |

Las pruebas automatizadas cubren hoy el núcleo de seguridad (inicio de sesión válido e
inválido, usuario inactivo, roles incluidos en el token, tokens vencidos o alterados) y las
reglas de negocio críticas de la Fase 1 (protección del último administrador, unicidad de
códigos, bloqueo de borrado de catálogos en uso).

**Deuda técnica identificada y controlada:** existen 49 advertencias de compilación (ninguna
bloqueante) y dos pantallas que aún operan con datos de demostración. Ambos puntos están
registrados y programados dentro de las Fases 2 y 3.

---

## 8. Plan de trabajo de las próximas cuatro semanas

| Semana | Foco | Entregable hacia ECAR |
|---|---|---|
| **4 – 5** | Cierre de Fase 2: preguntas de checklist sobre servicios reales, generación de QR por equipo, consulta de equipo por escaneo QR, seguridad por rol en los módulos de checklist | **Entrega 3 (semana 6):** checklists, versionamiento y QR |
| **6 – 7** | Fase 3: flujo guiado de ejecución de inspección, captura de respuestas, carga fotográfica desde móvil, firma digital | **Entrega 4 (semana 8):** inspecciones, evidencias y firma |
| **8 – 9** | Fase 4: auditoría automática e inmutable, ciclo de vida de hallazgos, reportes PDF y Excel | **Entrega 5 (semana 10):** hallazgos, auditoría y reportes |

---

## 9. Riesgos del proyecto

| Riesgo | Probabilidad | Impacto | Mitigación |
|---|---|---|---|
| Demora en la entrega de los datos de conexión a Active Directory | Media | Medio | La arquitectura ya soporta ambos modos; se activa por configuración sin reprogramar |
| Auditoría automática subestimada por su exigencia regulatoria | Media | **Alto** | Se abordará al inicio de la Fase 4 y se validará con el área de Calidad de ECAR antes del UAT |
| Definición tardía de la programación de inspecciones | Media | Medio | Solicitud formal incluida en la sección 6 de este informe |
| Indisponibilidad del ambiente de despliegue en la semana 12 | Media | **Alto** | Se solicita habilitar el ambiente a más tardar en la semana 9 |

---

## 10. Conclusión

El proyecto se encuentra **en tiempo** respecto al cronograma de 12 semanas. Las bases
estructurales —arquitectura, modelo de datos completo, seguridad por roles y gestión de
equipos— están **terminadas y verificadas**, lo que reduce significativamente el riesgo de las
fases siguientes, ya que el trabajo restante es principalmente **lógica de aplicación sobre una
estructura de datos que ya está construida y estable**.

El foco inmediato es cerrar la Fase 2 (checklists y QR) para la Entrega 3 de la semana 6, y
priorizar tempranamente la **auditoría automática**, por ser el componente de mayor peso
regulatorio del MVP.

Quedamos atentos a agendar una **sesión de demostración** de lo entregado en las Fases 0 y 1, y
a recibir las definiciones solicitadas en la sección 6.

---

*Documento generado el 8 de septiembre de 2026 a partir del estado verificado del repositorio
en la rama `develop`.*
