# Juan Alberto (FE-0, líder) — pendientes propios y de coordinación

**Rama de referencia:** `integracion_fronEnd`
**Actualizado:** 02/10/2026 · Índice general: [`PENDIENTES_FRONTEND.md`](PENDIENTES_FRONTEND.md)

---

## Ya está hecho

Commit **`f17b02a`**, 02/10:

| Pendiente | Qué se hizo |
|---|---|
| Retirar `MockDataService` (tarea 7 del plan) | Eliminados `MockDataService.cs`, `RespuestaInspeccionModal.razor` y su registro en `Program.cs`. El cliente ya no tiene datos simulados |
| "Siguiente" decía cuántas faltan, no cuáles | Ahora: *"Faltan por responder las preguntas obligatorias 2, 5 y 7"*. Igual para novedades sin observación |
| `EjecutarInspeccion` llamaba al API sin sesión | Comprueba el token antes; ya no se cuela el snackbar rojo mientras la guarda redirige al login |
| Sangría del bloque de Fase 3 en `HttpClientService` | De 8 a 4 espacios. Solo espacios: con `git diff -w` el único cambio es una línea en blanco |
| `ESTADO_PROYECTO.md` §4 (tarea 10 del plan) | Pantallas de Fase 3 con su estado real, separando front terminado de endpoint pendiente; las dos páginas nuevas; `RolRouteGuard` |
| `GUIA_FRONTEND_FASE3.md` | `RolRouteGuard`; por qué `[Authorize]` y `<AuthorizeView>` no funcionan aquí; que la guarda no detiene la página; que `EmptyLayout` no tiene proveedores; `DataLabel` obligatorio |

Y antes, en la integración: `RolRouteGuard` y el `[Authorize]` inerte de `ResultadoInspeccion`
(`75f6c45`), y el eco del servidor en `GuardarAsync` (`96def9f`).

---

## A. Borrar la rama duplicada `Integracion_fronEnd` · **lo primero**

| Rama | Commit | Contenido |
|---|---|---|
| `integracion_fronEnd` (minúscula) | la buena | los tres merges, todos los arreglos y estos documentos |
| `Integracion_fronEnd` (**I mayúscula**) | `96def9f` | solo el arreglo del esqueleto, sin ningún merge |

En Windows —y en macOS por defecto— el disco no distingue mayúsculas, así que las dos refs chocan
al hacer `fetch`: una tapa a la otra. Alguien que haga `checkout` de `origin/integracion_fronEnd`
puede acabar en `96def9f` sin ninguno de los merges.

**Borrarla no pierde nada**: verificado que `96def9f` está contenido en la buena.

```bash
git push origin --delete Integracion_fronEnd
```

Después, en cada equipo, para limpiar la referencia local que quedó tapada:

```bash
git fetch origin --prune
```

---

## B. Pendiente de verificar en ejecución

### B1. El campo de Observación

El campo usa `@bind-Value` con el debounce interno de MudBlazor, así que su valor en memoria puede
ir hasta 500 ms por detrás de lo que hay en pantalla. Si MudBlazor 9.8.0 no protege contra que un
repintado del padre le devuelva ese valor viejo, el texto podría saltar. Lo destapa la prueba manual
de Erica (pasos 4 y 5); si salta, es mío.

### B2. Unificar las guardas (opcional)

`AdminRouteGuard`, `TecnicoRouteGuard` y `RolRouteGuard` hacen casi lo mismo. Ojo: no es un cambio
mecánico, porque `AdminRouteGuard` sin sesión manda al inicio y las otras dos al login. No urge.

---

## C. Coordinación con backend

Cada punto tiene a alguien del front esperando.

### C1. Juan David (BE-1) — su rama no compila desde el 30/09 · bloquea a Erica

En `b87bfd4` añadió un parámetro `IInspeccionService` al constructor de `InspeccionesController` y no
actualizó las pruebas. Cuatro `CS7036` en `ECAR.API.Tests/BackendPhaseThreeTests.cs`, líneas **140,
165, 211 y 239**. Roto en su propia rama, sin mezclarla con nada.

### C2. Alejandro (BE-2) — su rama borra una migración de `develop` · bloquea a Santiago

Elimina `20260921114232_Fase3BaseInspecciones` (en `develop` desde el 22/09) y añade
`20260922222833_AgregarOrdenPregunta`, que vuelve a crear las mismas columnas (`Estado`,
`FechaCierre`, `FirmaHash`, `IdChecklist` en `Inspecciones`, y las nuevas de `Evidencias`).
Cualquier base de datos ya migrada desde `develop` intentaría crear columnas que existen y **el API
no arrancaría**. **No mergear tal cual**: tiene que rebasar sobre `develop` y generar una migración
que parta de la de Carlos.

Lo otro que tenía —que no compilaba contra la integración por `CreateEvidenciaDto`— **ya está
resuelto** en `b1d1c85`. Verificado: su rama mezclada sobre la integración compila con 0/0.

### C3. Carlos (BE-0) — parámetro `estado` en `GET /api/inspecciones` · bloquea a Santiago

`[FromQuery] string? estado = null` y `.Where(i => estado == null || i.Estado == estado)` antes de
paginar.

### C4. Simón (BE-3) — mergear `firma-inmutable` · bloquea a Gary

`POST /firmar`, `GET /resultado` y la inmutabilidad, en su rama desde el 29/09.

---

## D. Cierre de la fase

### D1. Orden de merge hacia `develop`

1. `firma-inmutable` (Simón).
2. `feature-juandalopez` (Juan David), cuando arregle las pruebas (C1).
3. `feature/ECAR-202-...` (Alejandro), cuando arregle la migración (C2).
4. `integracion_fronEnd` → `develop`.

Antes del paso 4, comprobar que cada rama de backend entra sobre la integración y que compila la
solución **entera**, no solo el API.

### D2. Prueba completa · tarea 9 del plan

Con todo mezclado: escanear el QR desde un teléfono real, iniciar, responder, subir dos fotos
seguidas, firmar, ver el resultado, imprimir. A 375 px. Todavía no lo ha hecho nadie: hoy faltan
cuatro endpoints en `develop`.

---

## Resumen

| # | Qué | Cuándo |
|---|---|---|
| A | **Borrar `Integracion_fronEnd`** | **Ya** |
| C1 | Avisar a Juan David: pruebas rotas | Hoy |
| C2 | Avisar a Alejandro: migración | Hoy |
| C3 | Pedir a Carlos el parámetro `estado` | Esta semana |
| C4 | Mergear `firma-inmutable` | Esta semana |
| D1 | Orden de merge a `develop` | Tras C1-C4 |
| D2 | Prueba completa en teléfono | Al final |
| B1 | Verificar el campo de Observación | Con la prueba de Erica |
| B2 | Unificar guardas | Opcional |
