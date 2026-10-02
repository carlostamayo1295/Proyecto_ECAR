# Gary (FE-3) — pendientes sobre la rama de integración

**De:** Juan Alberto (FE-0) · **Para:** Gary (FE-3)
**Rama de trabajo:** `integracion_fronEnd`
**Sustituye a:** `INSTRUCCIONES_FE3_GARY.md` del 29/09, que apuntaba a `feature/qr`
**Actualizado:** 02/10/2026 · Índice general: [`PENDIENTES_FRONTEND.md`](PENDIENTES_FRONTEND.md)

---

## Lo primero: cumpliste casi todo

Tu rama `feature/qr` **ya está mergeada** en `integracion_fronEnd` (commit `2c771cf`) y fue **la
única de las tres que entró sin ningún conflicto**, porque ya habías integrado mi rama por tu cuenta.
La integración compila con **0 errores y 0 advertencias** y pasa **32/32 pruebas**.

De los 22 puntos del documento anterior resolviste 18, y varios mejor de lo que te pedí:

- La firma en blanco ya no pasa: `tieneTrazo` en el `dataset` del canvas, marcado solo cuando el
  trazo se pinta de verdad.
- **Capturar y cerrar están separados.** El canvas solo entrega la firma; el cierre es un botón
  aparte, en rojo, con `Disabled="@(Firmando || ...)"` y la etiqueta que cambia a "Cerrando la
  inspección…". Era el punto grave y quedó exactamente como tocaba.
- En vez de un `return` silencioso con el recuadro en blanco, avisas al técnico. Mejor que lo que
  te pedí.
- `ResultadoInspeccion` tiene respuestas, miniaturas, estados de carga y error, ruta `long`, hash
  por rol, colores del tema y CSS de impresión propio.
- El inicio desde QR existe de punta a punta, y en `ConsultaQr` capturaste los ids en variables
  locales antes del lambda, que es justo lo que evita el bug de captura de closure.

Desde ahora trabajas sobre la integración:

```bash
git fetch origin && git checkout -b fix/fe3-pendientes origin/integracion_fronEnd
```

> **Ojo antes de correr eso.** En GitHub hay una segunda rama, `Integracion_fronEnd` con **I
> mayúscula**, que no es la buena. En Windows las dos chocan porque el disco no distingue
> mayúsculas. Comprueba con `git ls-remote origin integracion_fronEnd` que el commit no es
> `96def9f`. Si lo es, avísame antes de tocar nada.

---

## Lo que toqué en tu código al integrar

**`ResultadoInspeccion.razor`** — quité el `@attribute [Authorize(Roles = "...")]` y su `@using`, y
envolví la página en un componente nuevo, `RolRouteGuard`.

**Esto fue culpa mía, no tuya.** En el documento anterior te dije literalmente que pusieras ese
atributo, y en este proyecto **no hace nada**: `App.razor` monta un `<RouteView>` normal, no un
`<AuthorizeRouteView>`, que es el único componente que lo lee, y `Program.cs` no registra
`AddAuthorizationCore()` ni ningún `AuthenticationStateProvider`. Por eso el proyecto tiene
`AdminRouteGuard` y `TecnicoRouteGuard`. Un anónimo que abría tu página llegaba a ella, el API le
respondía 401 y veía el error; no lo mandaba al login.

`RolRouteGuard` es la misma guarda que ya existía, con los roles como parámetro (aquí entra también
el Auditor). **No vuelvas a poner el `[Authorize]`.** Y lo vas a necesitar en el pendiente 1.

Tu filtro del hash **no lo toqué**: ahí usaste `AuthorizationService.GetUserRolesAsync()`, que lee el
token sin depender del pipeline, y por eso sí funcionaba. Buen instinto.

---

## Pendiente 1 — Un error de negocio manda al técnico al login

**Archivo:** `ECAR.Client/Pages/IniciarInspeccion.razor`, líneas **57-72**

Es el único defecto real que te queda, y es el más importante de todo el front.

```csharp
if (result?.Data != null)
{
    NavigationManager.NavigateTo($"/inspecciones/ejecutar/{result.Data.IdInspeccion}", replace: true);
    return;
}

// Solo si de verdad no vino ningún dato (probable 401/sin sesión), mandamos a login.
NavigationManager.NavigateTo(loginUrl, forceLoad: true);
```

El 409 lo resolviste bien. Pero **todo** lo que no trae `Data` cae en "no hay sesión", y hay al
menos tres casos que no lo son:

1. **Un 400 legítimo del API.** `POST /api/inspecciones/iniciar` responde 400 con `Success = false`
   y `Data = null` cuando el equipo no existe o no está activo, cuando el checklist no existe o no
   está activo, y cuando el usuario está deshabilitado.
2. **Sin red.** `IniciarInspeccionAsync` atrapa la excepción por dentro y devuelve `null`. En la red
   de planta eso va a pasar.
3. **Cualquier excepción** en el `catch` de la línea 68, que también manda al login.

**Escenario concreto:** un técnico **con sesión válida** escanea el QR de una balanza que
administración dio de baja. En vez de *"El equipo indicado no existe o no está activo"*, la página lo
expulsa al login. Inicia sesión, vuelve solo a la misma URL —porque el `returnUrl` funciona—, el API
vuelve a decir 400, y lo vuelve a expulsar. **Queda en un bucle** sin saber por qué.

Y como `_error` solo se asigna cuando faltan parámetros (línea 42), el bloque de error de las líneas
15-23 es casi código muerto.

**Cómo arreglarlo.** El fallo de fondo es que la página intenta adivinar si hay sesión a partir de
la respuesta. No hay que adivinarlo: hay que comprobarlo **antes** de llamar al API. Y para eso ya
tienes la guarda:

```razor
@page "/inspecciones/iniciar"
@layout EmptyLayout
@inject HttpClientService HttpClientService
@inject NavigationManager NavigationManager

<RolRouteGuard Roles="@(new[] { "Administrador", "Técnico" })"
               MensajeNoAutorizado="Solo técnicos y administradores pueden iniciar inspecciones.">
    @* ... tu MudCard actual ... *@
</RolRouteGuard>
```

La guarda ya resuelve el caso sin sesión: manda al login con el `returnUrl` y vuelve. Así que
**dentro** de la página, cualquier respuesta sin `Data` es un error real y se muestra:

```csharp
var result = await HttpClientService.IniciarInspeccionAsync(new IniciarInspeccionDto { ... });

if (result?.Data != null)
{
    // Creada (201) o ya existente (409, que trae la que estaba en curso): se continúa igual.
    NavigationManager.NavigateTo($"/inspecciones/ejecutar/{result.Data.IdInspeccion}", replace: true);
    return;
}

_error = result?.Message
    ?? "No se pudo conectar con el servidor. Compruebe la conexión e intente de nuevo.";
_cargando = false;
```

Y **borra el `try/catch` entero**, las líneas de `loginUrl` y el `currentUri`: el servicio ya atrapa
las excepciones de red, y la redirección al login la hace la guarda.

Con esto quedan resueltos a la vez otros dos detalles del documento anterior:

- El `returnUrl` deja de ser absoluto (`NavigationManager.Uri`) y pasa a ser relativo, como en el
  resto del proyecto, porque lo construye la guarda.
- Fíjate en el **`@layout EmptyLayout`** del ejemplo. `ConsultaQr` lo usa y tu página no: a esta
  pantalla se llega escaneando desde un teléfono, posiblemente sin sesión, y hoy se le pinta el
  `MainLayout` con el menú completo.

**Cómo probarlo:**

| Caso | Qué tiene que pasar |
|---|---|
| Sin sesión | Login → vuelve solo → ejecución |
| Con sesión, equipo inactivo | Se queda en la página y muestra *"El equipo indicado no existe o no está activo"* |
| Con sesión, ya tiene una abierta para ese equipo | Entra a la existente |
| Con sesión, API apagado | *"No se pudo conectar con el servidor..."*, sin ir al login |

---

## Pendiente 2 — El botón "Volver al escáner QR" va al inicio

**Archivo:** `ECAR.Client/Pages/IniciarInspeccion.razor`, líneas **20** y **76-79**

```csharp
private void VolverAlQr()
{
    NavigationManager.NavigateTo("/", replace: true);
}
```

El botón dice "Volver al escáner QR" y te lleva a `/`. La página no tiene el token del QR, así que
no puede volver a él. La salida honesta es cambiar el texto a **"Volver al inicio"**. Si prefieres
que vuelva de verdad, `ConsultaQr` tiene que pasar el token en la URL de `iniciar` y aquí lo lees
igual que `equipo` y `checklist`; las dos opciones valen, pero que el botón haga lo que dice.

---

## Pendiente 3 — Detalles

Ninguno bloquea. Son diez minutos en total.

| Qué | Dónde | Por qué |
|---|---|---|
| El botón del canvas sigue diciendo **"Confirmar"** | `FirmaCanvas.razor:12` | Pedí "Usar esta firma". Con la separación hecha el riesgo desapareció, pero al lado de "Limpiar" y encima del texto legal, "Confirmar" sigue sonando a que cierra algo |
| Devolver el comentario que borraste | `ConsultaQr.razor`, encima de la línea 99 | `// Consulta pública: no requiere sesión. Un token desconocido o un equipo inactivo devuelven 404`. Explica por qué esa página es la única sin guarda; sin él, el siguiente que pase le pone una |
| `StateHasChanged()` pelado | `ResultadoInspeccion.razor:206` | Funciona porque viene de `OnInitializedAsync`, pero la regla del proyecto es `await InvokeAsync(StateHasChanged)` |
| Borde con color a mano | `ResultadoInspeccion.razor:131` | `border: 1px solid #ccc` en la imagen de la firma; el resto de la página ya usa el tema |

---

## Pendiente 4 — Las pruebas (tareas 5 y 6 del plan)

**Tus endpoints siguen sin estar en `develop` ni en la integración.** `POST /firmar` y
`GET /resultado` están en la rama de Simón, `firma-inmutable`. Para probar, ármate una rama local y
**no la subas**:

```bash
git checkout -b prueba/fe3 origin/integracion_fronEnd && git merge origin/firma-inmutable
```

| Qué | Qué tiene que pasar |
|---|---|
| Firmar sin dibujar | Aviso, y el botón "Firmar y cerrar" sigue deshabilitado |
| Doble clic en "Firmar y cerrar" | Botón deshabilitado al primer clic; un segundo `firmar` al API da **409** |
| Obligatoria sin responder | El API lista cuáles y la pantalla vuelve al paso 1 |
| Resultado como Auditor | Respuestas, miniaturas, firma **y** el hash |
| Resultado como Técnico | Todo **menos** el hash |
| Resultado sin sesión | Login → vuelve al resultado (lo valida la guarda nueva) |
| Imprimir | Solo el informe; sin menú, sin botones |
| **QR completo desde el teléfono, sin sesión** | Consulta → Iniciar → login → vuelve → ejecución |
| 375 px | El canvas cabe y el trazo sigue al dedo |

La del QR desde el teléfono es la importante: es el recorrido completo que va a hacer el cliente y
todavía no lo ha hecho nadie.

---

## Lo que **no** es tuyo

- **No toques `EjecutarInspeccion.razor`.** Es mío. Ya te manda al resultado cuando la inspección está
  cerrada y después de firmar.
- **No toques `PasoPreguntas.razor` ni `PasoEvidencias.razor`.** Son de Erica y de Santiago.
- **No toques `RolRouteGuard.razor`.** Si necesitas algo distinto, me lo pides.
- **El backend de firma e inmutabilidad** es de Simón.

---

## Resumen

| # | Qué | Dónde | Tamaño |
|---|---|---|---|
| 1 | **Errores de negocio y de red mandan al login** → guarda + mostrar el error + `EmptyLayout` | `IniciarInspeccion.razor:57-72` | **Media — es lo grave** |
| 2 | "Volver al escáner QR" va a `/` | `IniciarInspeccion.razor:76` | Trivial |
| 3 | Etiqueta del canvas, comentario borrado, `StateHasChanged`, `#ccc` | varios | Trivial |
| 4 | Pruebas contra `firma-inmutable` + QR desde el teléfono | — | Media |

Empieza por el 1: es el único que deja a un técnico atascado en planta.

— Juan Alberto
