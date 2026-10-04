# Juan Alberto (FE-0, líder) — pendientes

**Actualizado:** 04/10/2026 · Índice: [`PENDIENTES_FRONTEND.md`](PENDIENTES_FRONTEND.md)

> **Rama de trabajo: `integracion/fase3-completa`.** Contiene las integraciones de backend y de
> frontend más todas las correcciones del 04/10. Sustituye a `integracion_fronEnd` y a
> `integration/backend-fase3` para seguir trabajando. Si tienes cambios locales sobre una de
> esas, actualiza antes de seguir:
>
> ```bash
> git fetch origin && git checkout -b mi-rama origin/integracion/fase3-completa
> ```

---

## Lo que se resolvió el 04/10

Todo lo de este documento de la versión anterior está hecho o ya no aplica:

- Código propio: `MockDataService` retirado, aviso de "Siguiente" con las preguntas que faltan,
  sangría, guías y `ESTADO_PROYECTO` al día.
- **Defectos propios encontrados al verificar**, todos corregidos: el "Sí" con tilde que impedía
  cerrar una inspección sin novedades (`07db102`) y el `[Required]` en `RespuestaEjecucionDto`
  (`6e9dfae`).
- Coordinación con backend: Carlos integró las tres ramas y arregló la migración de BE-2 y las
  pruebas de BE-1. El parámetro `estado`, el límite de `pageSize` y el endpoint de evidencias por
  inspección se resolvieron en esta rama.
- Además, a petición: inspector automático en el alta, vuelta al login al vencer la sesión,
  botones de activar y la firma digital en el modal de Inspecciones (`68f3ebf`).

---

## Lo que queda

### 1. PR de `integracion/fase3-completa` → `develop`

Con revisión de Carlos (BE-0), porque la rama incluye cambios de backend suyos y de su equipo.

### 2. Borrar las ramas que quedan obsoletas

`integracion_fronEnd` e `integration/backend-fase3` quedan contenidas en
`integracion/fase3-completa`. Borrarlas después del merge evita que alguien siga trabajando sobre
una de ellas.

### 3. Prueba completa en un teléfono real (tarea 9 del plan)

Escanear el QR, iniciar, responder, subir dos fotos seguidas, firmar con el dedo, ver el
resultado e imprimir, a 375 px. En el escritorio está verificado; en un teléfono, no.

### 4. Ancho de la barra superior en el teléfono

A 375 px, la barra superior ("API SCALAR" + "LOGOUT") y el botón "Nueva …" de la cabecera
de las tarjetas miden más que la pantalla y la página queda en 397 px: el navegador la reduce un
poco y los modales pierden unos 20 px por la derecha. Es del layout común (`MainLayout`), así que
afecta a todas las pantallas. La tabla de Inspecciones ya no ensancha la página (`68f3ebf`).

### 5. Opcional

- Unificar `AdminRouteGuard`, `TecnicoRouteGuard` y `RolRouteGuard`.
- `PreguntasChecklistController.cs` y `Entities/PreguntaChecklist.cs` siguen en UTF-16 (git los
  trata como binarios).
- El formulario de login no se envía con Intro; hay que pulsar el botón.
