# Revisión e integración del backend — Fase 1

Fecha de corte: 31 de agosto de 2026  
Rama de integración: `integration/backend-fase1`  
Base: `origin/develop` (`1f5922c`)

## Conclusión ejecutiva

El backend de la Fase 1 cubre autenticación JWT, autenticación local y LDAP/Active Directory configurable, usuarios, roles, asignaciones de roles, categorías, ubicaciones, inventario y ficha técnica de equipos. La solución compila en .NET 10, no tiene migraciones pendientes ni paquetes reportados como vulnerables y fue probada sobre una base SQL Server LocalDB creada desde cero.

La conexión real con el directorio corporativo requiere que ECAR entregue servidor, puerto, dominio y acceso de prueba. El código ya permite activarla por configuración sin recompilar. La auditoría completa pertenece a la Fase 4 según el cronograma; la generación QR y los checklists pertenecen a la Fase 2.

## Cobertura contra el alcance

| Requisito de Fase 1 | Estado | Evidencia principal |
|---|---|---|
| JWT | Completo | Login, emisión y validación de token |
| Autenticación local | Completo | Contraseña BCrypt y usuario activo |
| Active Directory activable/desactivable | Implementado; pendiente prueba corporativa | Modos `Local`, `ActiveDirectory` y `Hybrid`; adaptador LDAP con TLS |
| Usuarios y múltiples roles | Completo | CRUD, asignaciones y protección del último administrador |
| Categorías | Completo | CRUD paginado y protección de registros en uso |
| Ubicaciones | Completo | CRUD paginado y unicidad planta/área |
| Equipos y ficha técnica | Completo | CRUD, baja lógica, relaciones y consulta detallada |
| Filtros de inventario | Completo | Texto, criticidad, categoría, ubicación, planta, área y estado |
| Migraciones | Completo para instalaciones nuevas | `MigrateAsync`, dos migraciones y prueba desde base vacía |
| Scalar/OpenAPI | Completo en desarrollo | Documento OpenAPI respondió HTTP 200 |
| Pruebas automatizadas | Incorporadas | 7 pruebas de reglas críticas |

## Retroalimentación por rama

### Juan David López — `feature-juandalopez`

**Aciertos:** identificó correctamente que un proyecto mantenible debe usar migraciones (`MigrateAsync`) y no `EnsureCreated`; abordó la preparación del ambiente.

**Por corregir:** la rama contenía credenciales SQL escritas directamente, artefactos generados (`obj_old`) y un script invasivo sobre registro/servicios de Windows. Los secretos nunca deben entrar al repositorio y un script de instalación debe ser mínimo, reversible y documentado.

**Integración:** se tomó el cambio conceptual hacia migraciones; no se hizo merge completo ni se reutilizaron credenciales o artefactos.

### Alejandro Gómez — `feature/ecar-176-ubicaciones`

**Aciertos:** entregó la estructura CRUD de ubicaciones y separó DTO de entidad.

**Por corregir:** faltaban autorización, paginación/búsqueda uniforme, uso consistente de `long`, protección de ubicaciones asociadas y una migración con contenido. El borrado físico podía romper trazabilidad.

**Integración:** el CRUD fue reconstruido sobre la rama canónica con JWT, DTO validados, respuesta estándar, unicidad planta/área y bloqueo HTTP 409 cuando está en uso.

### Alejandro Gómez — `feature/ecar-177-crud-equipos`

**Aciertos:** modeló relaciones y validaciones básicas del inventario.

**Por corregir:** duplicaba otro desarrollo de equipos y no seguía completamente el contrato común de respuestas, paginación y permisos. Dos implementaciones paralelas del mismo ticket aumentan conflictos y revisión.

**Integración:** se comparó con `feature_equipos` y se escogió esta última como base más completa; las validaciones útiles se conservaron en la versión consolidada.

### Juan Alberto Zuluaga — `feature_equipos`

**Aciertos:** fue la implementación más completa: equipos, DTO, paginación, búsqueda, respuestas comunes, baja lógica y administración de asignaciones usuario–rol. Su PR ya estaba integrado en `develop`.

**Por corregir:** el controlador completo estaba restringido al administrador, impidiendo la consulta a técnicos/auditores; faltaban filtros de planta/área/categoría/ubicación, normalización de datos y protección del último administrador.

**Integración:** se mantuvo como base canónica y se reforzaron permisos por operación, filtros, reglas de integridad y pruebas.

### Gary — `feature/pages`

**Aciertos:** adelantó flujos visuales y componentes para ubicaciones y módulos posteriores, lo cual ayuda a descubrir campos e interacciones.

**Por corregir:** las pantallas dependen de datos simulados y no prueban un backend real; parte del servicio simulado no estaba registrado. Un mock debe quedar marcado como temporal y acompañado del contrato API esperado.

**Integración:** su PR ya estaba en `develop`, pero no se tomó como evidencia de backend. Los endpoints reales de ubicaciones se implementaron por separado.

### Erica Avendaño — `feature/paginas-I-A-E`

**Aciertos:** organizó navegación y guardas de ruta para inspecciones, evidencias y hallazgos.

**Por corregir:** corresponde principalmente a fases posteriores y usa información simulada; no incluye persistencia ni API de esos módulos.

**Integración:** no se mezcló en el cierre del backend Fase 1 para evitar declarar terminados módulos de Fase 3.

### Santiago Arango — `fix/correccion-merge-auditoria-categorias-checklists`

**Aciertos:** trabajó categorías, checklists y lectura de auditoría; la separación de pantallas y controladores da una referencia útil.

**Por corregir:** la rama no comparte base de merge con `develop`, carecía de autorización en endpoints revisados y la auditoría era solo lectura, sin generación automática de registros. Checklists son Fase 2 y auditoría completa es Fase 4.

**Integración:** no se hizo merge ni cherry-pick por su historia incompatible. El CRUD de categorías necesario para Fase 1 se reconstruyó y endureció en la rama de integración.

### Carlos / liderazgo e integración

**Aciertos previos:** Fase 0 dejó la arquitectura por capas, entidades, DbContext, autenticación inicial y documentación base.

**Trabajo realizado en integración:** resolución del arranque con migraciones, semillas idempotentes, contratos uniformes, categorías y ubicaciones reales, endurecimiento de usuarios/roles/equipos, autenticación LDAP configurable, actualización segura de OpenAPI, pruebas y validación desde una base vacía.

## Decisiones que el equipo debe conservar

- Una persona responsable por ticket y un PR pequeño por funcionalidad.
- Nunca subir contraseñas, archivos `secrets.json`, cadenas reales ni carpetas `bin/obj`.
- No usar `EnsureCreated` cuando el proyecto ya trabaja con migraciones.
- Las consultas pueden servir a los roles autorizados; las mutaciones administrativas exigen `Administrador`.
- Los datos con historia se desactivan o se bloquea su borrado.
- Una pantalla con mocks no equivale a una funcionalidad terminada.
- Antes de integrar: compilar, ejecutar pruebas, revisar migraciones y probar la API con autorización.

## Pendientes externos y de fases posteriores

- ECAR: entregar servidor, puerto, dominio, esquema de usuario y cuenta de prueba de Active Directory.
- Fase 2: checklists versionados y generación/lectura QR.
- Fase 3: inspecciones, evidencias y correo de novedades a `cati@ecar.com` una vez se definan campos de notificación.
- Fase 4: escritura automática e inmutable de auditoría, hallazgos y reportes.
- Despliegue: validar en el IIS de destino el certificado, identidad del Application Pool, conexión SQL y acceso LDAP.
