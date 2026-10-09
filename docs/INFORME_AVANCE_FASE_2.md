# Informe de Avance para el Cliente
## Sistema de Gestión de Equipos e Inspecciones — Laboratorios ECAR S.A.

**Proyecto:** Sistema de Gestión del Ciclo de Vida de Equipos de Planta (MVP)
**Documentos de referencia:** SRS MVP v1.0 y Cronograma MVP v1.0
**Fecha de corte:** 16 de septiembre de 2026
**Semana del cronograma:** 5 de 12
**Informe anterior:** 8 de septiembre de 2026
**Elaborado por:** Equipo de Desarrollo

---

## 1. Resumen ejecutivo

El proyecto se encuentra al **cierre de la Fase 2 del cronograma (Checklists y Gestión QR),
semana 5 de 12**, con las **Fases 0, 1 y 2 completadas**. La Fase 2 queda lista

Desde la reunion anterior se ha logrado la **administración completa de checklists y
preguntas**, el **versionamiento de checklists** con protección de la evidencia histórica, y
el **código QR por equipo**: generación e impresión de la etiqueta desde la ficha del equipo y
**consulta del equipo desde el teléfono al escanear la etiqueta**, sin necesidad de iniciar
sesión. Todo opera de extremo a extremo sobre servicios reales y ha sido verificado.

| Indicador | 09/09/2026 | 30/09/2026 |
|---|---|---|
| Avance global del MVP | ≈ 40 % | **≈ 50 %** |
| Fases completadas | 2 de 7 | **3 de 7** |
| Entregas contractuales | 1 y 2 cumplidas | 1 y 2 cumplidas; **3 lista para presentar** |
| Servicios de negocio publicados | 57 operaciones / 12 módulos | **68 operaciones / 13 módulos** |
| Pantallas construidas | 16 | **17 + 3 diálogos nuevos** |
| Pantallas sobre datos reales | 14 | **19 de 20** |
| Pruebas automatizadas | 17 (100 % en verde) | **26 (100 % en verde)** |
| Estado de compilación | Sin errores | Sin errores |

**Conclusión:** el proyecto avanza **conforme al cronograma**. La Entrega 3 se presenta en la
semana 6 como estaba previsto; la Fase 3 (inspecciones en campo) arranca la misma semana.

---

## 2. Estado por fase del cronograma

| Fase | Alcance comprometido | Semanas | Estado | Avance |
|---|---|---|---|---|
| **Fase 0.** Inicio y diseño técnico | Arquitectura, modelo de datos, ambientes | 1 | ✅ Completa | 100 % |
| **Fase 1.** Seguridad y gestión de equipos | JWT, usuarios, roles, catálogos, ubicaciones, equipos, ficha técnica | 2 – 3 | ✅ Completa | 100 % |
| **Fase 2.** Checklists y gestión QR | Checklists, preguntas, versionamiento, generación y lectura de QR | 4 – 5 | ✅ **Completa** | **100 %** |
| **Fase 3.** Inspecciones y evidencias | Ejecución de inspecciones, respuestas, evidencia fotográfica, firma digital | 6 – 7 | 🟡 Cimientos construidos | ≈ 25 % |
| **Fase 4.** Hallazgos, auditoría y reportes | Hallazgos, auditoría automática, reportes PDF/Excel | 8 – 9 | 🟡 Cimientos construidos | ≈ 15 % |
| **Fase 5.** Dashboard y estabilización | Indicadores | 10 – 11 | ⚪ No iniciada | 0 % |
| **Fase 6.** UAT y producción | Pruebas de aceptación, capacitación, despliegue | 12 | ⚪ No iniciada | 0 % |

---

## 3. Qué se muestra en la Fase 2 (demostrable) 

### 3.1 Checklists de inspección

- **Creación, edición, consulta y desactivación de checklists**, con nombre y versión únicos.
- **Tipos de respuesta controlados** (*Sí / No* y *Texto*) para que las inspecciones capturen
  información homogénea y reportable.
- **Preguntas** administradas desde su propia pantalla: búsqueda, filtro por checklist,
  paginación y **orden asignado automáticamente** por el sistema.
- **Protección de la evidencia histórica** (regla 6 del SRS): un checklist o una pregunta que
  ya fueron respondidos en una inspección **no pueden modificarse ni eliminarse**; el sistema
  lo informa y orienta a crear una versión nueva.

### 3.2 Versionamiento de checklists

- **Historial de versiones** de cada checklist, con indicación de cuál está activa, cuántas
  preguntas tiene cada una y cuáles ya se usaron en inspecciones.
- **Creación de una versión nueva** a partir de la actual: copia sus preguntas, queda como
  única versión activa y la anterior se conserva como histórica para las inspecciones ya
  realizadas. Se rechazan versiones repetidas.

### 3.3 Código QR por equipo

- Desde el inventario, el Administrador **genera la etiqueta QR** del equipo, la **imprime**
  o la **descarga como imagen**. La imagen se produce **en el servidor de ECAR**, sin depender
  de servicios externos, como exige la operación en red interna.
- El QR es **estable**: abrirlo de nuevo devuelve la misma etiqueta. Solo se **regenera**
  bajo confirmación explícita, porque invalida las etiquetas ya pegadas.
- **Consulta desde el teléfono:** al escanear la etiqueta se abre una página pública con la
  ficha del equipo (código, nombre, marca, modelo, criticidad, categoría, ubicación) y los
  checklists activos. **No requiere iniciar sesión** y no expone información de usuarios ni
  de inspecciones. Optimizada para pantalla de 375 px.
- Esta página es la **puerta de entrada de la inspección en campo** de la Fase 3.

### 3.4 Seguridad

- Los servicios de checklists, preguntas y QR aplican **control de acceso por rol en el
  servidor**: consulta para Administrador, Técnico y Auditor; modificaciones solo para el
  Administrador. La única excepción, deliberada, es la consulta pública por QR.

### 3.5 Calidad

- **9 pruebas automatizadas nuevas** cubren el versionamiento, el bloqueo de checklists con
  respuestas, el orden automático de preguntas y todo el ciclo del QR (generar, regenerar,
  consultar, imagen).
- Se corrigieron tres defectos detectados en pruebas internas: la pantalla de preguntas no
  mostraba resultados al entrar, la segunda pregunta de un checklist fallaba por conflicto de
  orden, y un botón duplicado en el inventario de equipos.

---

## 4. Qué está en construcción (Fase 3 — desde la semana 6)

| Funcionalidad | Lo que ya existe | Lo que se construye en Fase 3 |
|---|---|---|
| **Inspecciones** | Registro, consulta y edición básica; regla "novedad → observación obligatoria" | Flujo guiado en el móvil: escanear QR → elegir checklist → responder → evidencias → firma → cierre. Estados *en curso* / *cerrada*; una inspección cerrada es inmutable |
| **Respuestas** | Componente de captura por tipo de pregunta | Servicio propio; validación de preguntas obligatorias antes del cierre (regla 3 del SRS) |
| **Evidencias** | Registro de referencia textual | **Carga real de fotografías** desde la cámara del móvil, almacenamiento y visualización |
| **Firma digital** | Campo de almacenamiento | Captura de firma manuscrita en pantalla, sellada con usuario autenticado, fecha/hora y huella de integridad |
| **Hallazgos** | Registro, consulta, edición, filtros | Ciclo de vida (responsable, cierre) y control de acceso — Fase 4 |
| **Auditoría** | Consulta con filtros | Registro automático e inmutable de todas las acciones — Fase 4 |

---

## 5. Qué está pendiente (no iniciado)

1. Reportes descargables PDF y Excel (Fase 4).
2. Dashboard ejecutivo (Fase 5).
3. Despliegue en el servidor de ECAR (Fase 6).


---

## 6. Calidad y verificación

| Verificación | Resultado |
|---|---|
| Compilación de la solución completa | **Sin errores** |
| Batería de pruebas automatizadas | **26 de 26 correctas** |
| Consistencia del modelo de datos con la base de datos | **Sin diferencias pendientes** (4 migraciones) |
| Cobertura del modelo de datos frente al SRS | **13 de 13 tablas** |
| Verificación funcional de Fase 2 | Ciclo completo QR y versionamiento probados contra el API y en el navegador (escritorio y móvil) |

---

## 7. Conclusión

La **Fase 2 completa y verificada** y alcanza el **50 % del
MVP** en la fecha prevista. Con checklists versionados y equipos etiquetados con QR, la
plataforma tiene todo lo necesario para la **inspección en field**, que es el corazón
funcional del sistema y el foco de las dos próximas semanas.


---

*Documento generado el 16 de septiembre de 2026 a partir del estado verificado del
repositorio.*
