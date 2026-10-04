# Gary (FE-3) — pendientes sobre la rama de integración

**De:** Juan Alberto (FE-0) · **Para:** Gary (FE-3)
**Rama de trabajo:** `integracion_fronEnd`
**Actualizado:** 02/10/2026 · Índice general: [`PENDIENTES_FRONTEND.md`](PENDIENTES_FRONTEND.md)

---

## Ya está hecho — no lo repitas

Tu rama está mergeada en `integracion_fronEnd` (fue la única que entró sin conflictos) y tus
pendientes de código se resolvieron el 02/10 en el commit **`9396278`**:

| Pendiente | Qué se hizo |
|---|---|
| **`IniciarInspeccion` mandaba al login ante cualquier fallo** | La página va dentro de `RolRouteGuard` (Administrador, Técnico), que resuelve el caso sin sesión. Dentro, cualquier respuesta sin `Data` es un error real y se muestra: `result.Message` o *"No se pudo conectar con el servidor…"*. Sin token no se llama al API. Se retiraron el `try/catch` y el `returnUrl` absoluto |
| "Volver al escáner QR" iba a `/` | Ahora dice **"Volver al inicio"**, que es adonde va |
| Botón "Confirmar" del canvas | Ahora dice **"Usar esta firma"** |
| Comentario borrado en `ConsultaQr` | Restaurado |
| `StateHasChanged()` en las miniaturas | `await InvokeAsync(StateHasChanged)` |
| Borde `#ccc` en la firma | `var(--mud-palette-lines-default)` |

Antes, en el merge, `ResultadoInspeccion` pasó de `@attribute [Authorize]` a `RolRouteGuard`. Aquel
atributo **no hacía nada en este proyecto** y fue por una instrucción mía, no por ti. Tu filtro del
hash con `AuthorizationService.GetUserRolesAsync()` sí funcionaba y no se tocó.

**Dos correcciones a lo que te dije antes:**

- Te recomendé poner `@layout EmptyLayout` en `IniciarInspeccion`. **No lo hagas.** `EmptyLayout` es
  solo `@Body`: no monta `MudSnackbarProvider`, `MudDialogProvider` ni `MudPopoverProvider`, así que
  el aviso de "no autorizado" de la guarda no se vería. Está explicado en `GUIA_FRONTEND_FASE3.md` §4.
- El error de `IniciarInspeccion` no era solo con los 400: también con la falta de red, porque
  `IniciarInspeccionAsync` atrapa la excepción y devuelve `null`. Con el cambio, los dos casos
  muestran el error.

> En GitHub hay una segunda rama, `Integracion_fronEnd` con **I mayúscula**, que no es la buena. En
> Windows chocan. Comprueba con `git ls-remote origin integracion_fronEnd` que el commit no es
> `96def9f`.

---

## Lo que te queda: las pruebas (tareas 5 y 6 del plan)

`POST /firmar` y `GET /resultado` siguen fuera de `develop`; están en la rama de Simón. Ármate una
rama de prueba y **no la subas**:

```bash
git fetch origin && git checkout -b prueba/fe3 origin/integracion_fronEnd && git merge origin/firma-inmutable
```

| Qué | Qué tiene que pasar |
|---|---|
| Firmar sin dibujar | Aviso; "Firmar y cerrar" sigue deshabilitado |
| Doble clic en "Firmar y cerrar" | Deshabilitado al primer clic; un segundo `firmar` al API da **409** |
| Obligatoria sin responder | El API lista cuáles y la pantalla vuelve al paso 1 |
| Resultado como Auditor | Respuestas, miniaturas, firma **y** el hash |
| Resultado como Técnico | Todo **menos** el hash |
| Resultado sin sesión | Login → vuelve al resultado |
| Imprimir | Solo el informe, sin menú ni botones |
| QR con equipo **inactivo**, con sesión | Se queda en la página y muestra el error del API, **sin ir al login** |
| QR con el API apagado, con sesión | *"No se pudo conectar con el servidor…"* |
| **QR completo desde el teléfono, sin sesión** | Consulta → Iniciar → login → vuelve → ejecución |
| 375 px | El canvas cabe y el trazo sigue al dedo |

La del QR desde el teléfono es la importante: es el recorrido completo que va a hacer el cliente, y
todavía no lo ha hecho nadie.

---

## No toques

`EjecutarInspeccion.razor` (FE-0), `PasoPreguntas.razor` (Erica), `PasoEvidencias.razor`
(Santiago), `RolRouteGuard.razor` (si necesitas algo distinto, me lo pides).

— Juan Alberto
