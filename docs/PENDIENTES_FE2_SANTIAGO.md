# Santiago (FE-2) — pendientes sobre la rama de integración

**De:** Juan Alberto (FE-0) · **Para:** Santiago Arango (FE-2)
**Rama de trabajo:** `integracion_fronEnd`
**Sustituye a:** `INSTRUCCIONES_FE2_SANTIAGO.md` del 29/09, que apuntaba a `feature/F3-Santiago`
**Actualizado:** 02/10/2026 · Índice general: [`PENDIENTES_FRONTEND.md`](PENDIENTES_FRONTEND.md)

---

## Lo primero: tu trabajo ya está integrado

Tu rama `feature/F3-Santiago` **ya está mergeada** en `integracion_fronEnd` (commit `db8866d`),
junto con las de Erica y Gary. La rama compila con **0 errores y 0 advertencias** y pasa
**32/32 pruebas**.

Desde ahora trabajas sobre la integración, no sobre tu rama:

```bash
git fetch origin && git checkout -b fix/fe2-pendientes origin/integracion_fronEnd
```

> **Ojo antes de correr eso.** En GitHub hay una segunda rama, `Integracion_fronEnd` con **I
> mayúscula**, que no es la buena. En Windows las dos chocan porque el disco no distingue
> mayúsculas. Comprueba con `git ls-remote origin integracion_fronEnd` que el commit es el de los
> pendientes y no `96def9f`. Si ves `96def9f`, avísame antes de tocar nada.

**Tu código no tiene ningún defecto propio.** De los cuatro pendientes, dos dependen del backend y
los otros dos son limpieza y pruebas. Pero uno de la limpieza se volvió obligatorio, y es el
primero de la lista.

---

## Lo que hice en tus archivos al integrar

Para que no lo deshagas sin darte cuenta:

**`PasoEvidencias.razor`** — es el "paso 1" del documento anterior, que hice yo durante el merge:

- `private bool subiendo` sustituido por `private int _subidasEnCurso` y la propiedad
  `protected bool Subiendo => _subidasEnCurso > 0`.
- **Quité el `|| subiendo`** de `OnArchivoSeleccionadoAsync`. Descartaba en silencio la segunda
  foto cuando el técnico tomaba dos seguidas sin esperar a que terminara la primera.
- El markup ahora usa `Disabled="Subiendo"` y `@if (Subiendo)`. Con el campo viejo, **ni el botón
  se deshabilitaba ni la barra de progreso se pintaba nunca**: tu primer `await` era
  `RequestImageFileAsync`, anterior al `subiendo = true`, así que el único repintado intermedio de
  Blazor lo veía en `false`.
- Añadí *"Subiendo N fotografía(s)…"* debajo de la barra, porque ahora dos subidas pueden
  solaparse.

Todo lo demás —el `MudFileUpload` con `capture="environment"`, la compresión, la galería, la caché
de miniaturas, `ConfirmarEliminarAsync`, el `miniaturas.Remove`— **se quedó exactamente como lo
escribiste.**

---

## Pendiente 1 — Borrar `CreateEvidenciaAsync` · **ahora es obligatorio**

**Archivo:** `ECAR.Client/Services/HttpClientService.cs`, línea **1179**

En el documento anterior esto era "código muerto, bórralo cuando puedas". **Ya no.** Lo comprobé
mezclando la rama de Alejandro (`feature/ECAR-202-almacenamiento-y-API-de-evidencias`) sobre la
integración:

```
HttpClientService.cs(1179,72): error CS0246: El nombre del tipo o del espacio de nombres
'CreateEvidenciaDto' no se encontró
```

Alejandro **eliminó `CreateEvidenciaDto`** de `ECAR.Shared` (era lo que tocaba: el alta de
evidencias por texto libre ya no existe), y tu método todavía lo usa. Mientras ese método siga ahí,
**la rama de BE-2 no puede entrar a la integración**, y con ella no entra el almacenamiento real de
fotografías.

En la integración el método no lo llama nadie: el único consumidor era `EvidenciaModal.razor`, que
borraste tú. Así que el cambio es solo esto:

1. Borra el método completo `CreateEvidenciaAsync` (desde su `/// <summary>` hasta la llave de
   cierre).
2. `dotnet build --no-incremental` → **0 errores, 0 advertencias**.

Es lo primero porque **desbloquea a Alejandro**, y porque se puede hacer hoy sin esperar a nadie.

---

## Pendiente 2 — El filtro por estado sigue mintiendo

**Archivo:** `ECAR.Client/Pages/Inspecciones.razor`, líneas **29-30**, **130** y **185**

Sigue aplicándose sobre la página ya cargada:

```csharp
// línea 130
private IEnumerable<InspeccionDto> inspeccionesFiltradas =>
    (inspecciones ?? new List<InspeccionDto>())
        .Where(i => estadoFiltro == null || i.Estado == estadoFiltro);
```

Con `pageSize = 10`, si en la página actual las diez inspecciones están cerradas y eliges "En
curso", la pantalla dice *"No hay inspecciones para mostrar"* aunque haya catorce abiertas en las
otras páginas. La paginación de abajo sigue contando las páginas sin filtrar.

**El arreglo de fondo no es tuyo:** `GET /api/inspecciones` no acepta el parámetro `estado`, y eso es
de Carlos (BE-0). Se lo pido yo.

**Lo que sí es tuyo, y puedes hacer ya:**

**a) Que el filtro sea honesto mientras tanto.** Una línea en el `MudSelect` de la línea 29:

```razor
<MudSelect T="string" Label="Estado" Value="estadoFiltro" Variant="Variant.Outlined" Clearable="true"
    ValueChanged="OnEstadoChanged"
    HelperText="Por ahora filtra solo las filas de esta página">
```

Es feo, pero es verdad, y se borra el día que llegue el parámetro.

**b) Dejar preparado el cambio para cuando Carlos lo suba.** En ese momento:

- En `GetInspeccionesAsync` (línea 1029) añade `string? estado = null` y pásalo al query string solo
  si trae valor, igual que ya se hace con `search`.
- En `Inspecciones.razor`, borra `inspeccionesFiltradas`, vuelve a usar `inspecciones` en el
  `MudTable` (líneas 41 y 43) y haz que el cambio recargue desde el servidor:

```csharp
private async Task OnEstadoChanged(string? value)
{
    estadoFiltro = value;
    currentPage = 1;   // sin esto te quedas en la página 7 de un listado que ahora tiene 2
    await LoadInspeccionesAsync();
}
```

Fíjate en el `currentPage = 1`: tu `OnEstadoChanged` actual (línea 185) tampoco lo hace, y con el
filtro en el servidor sería un segundo bug.

---

## Pendiente 3 — `/evidencias` deja borrar fotos de inspecciones cerradas

**Archivo:** `ECAR.Client/Pages/Evidencias.razor`, líneas **62-65** (galería) y **110-113** (tabla)

El botón "Eliminar" sigue saliendo siempre. Hoy un administrador puede borrar la fotografía de
respaldo de una inspección firmada, que es lo que prohíbe la **regla 6 del SRS**.

**Esto cambió desde el documento anterior, y para bien:** te dije que estabas bloqueado porque
`EvidenciaDto` no traía el estado de la inspección. **Alejandro ya lo añadió** en su rama:

```csharp
/// Estado de la inspección a la que pertenece esta evidencia ("EnCurso" o "Cerrada").
public string EstadoInspeccion { get; set; } = string.Empty;
```

Y además el servidor ya rechaza el borrado: tanto la rama de Alejandro como la de Simón
(`firma-inmutable`) devuelven **409** al hacer `DELETE` sobre una evidencia de una inspección
cerrada. Así que el riesgo real ya está cubierto en el backend; lo tuyo es que la pantalla **no
ofrezca lo que el servidor va a rechazar**.

**El orden importa.** `EstadoInspeccion` **todavía no existe en la integración**, así que si lo usas
hoy, no compila. Esto entra **junto con** la rama de Alejandro, después de tu pendiente 1. Cuando
esté:

```razor
@if (evidencia.EstadoInspeccion != InspeccionEstados.Cerrada)
{
    <MudButton Size="Size.Small" Color="Color.Error" Variant="Variant.Text"
        StartIcon="@Icons.Material.Filled.Delete" OnClick="() => DeleteEvidencia(evidencia)">
        Eliminar
    </MudButton>
}
```

y el equivalente en la vista de tabla con `context`. Aprovecha y muestra el estado como `MudChip` en
la tarjeta de la galería, igual que hiciste en `Inspecciones.razor`, para que el administrador
entienda **por qué** no hay botón.

Si quieres ir adelantando, ármate una rama local con la de Alejandro encima —**no la subas**— y
recuerda que primero tienes que haber hecho el pendiente 1, porque si no esa mezcla no compila.

---

## Pendiente 4 — Las pruebas (tareas 5 y 6 del plan)

Siguen sin hacerse.

**Teléfono real.** No sirve el emulador: hay que comprobar que `capture="environment"` abre la
**cámara trasera** y no el carrete, y que una foto de verdad (8-12 MB) baja de 5 MB después de
`RequestImageFileAsync`. Levanta el API con tu IP de red en vez de `localhost`.

- Abre la cámara trasera, no la galería.
- La foto queda por debajo de 5 MB (mira el tamaño que pinta tu propia tarjeta).
- **Toma dos fotos seguidas sin esperar a que termine la primera.** Las dos tienen que aparecer y
  el contador tiene que decir "Subiendo 2 fotografía(s)…". Es la prueba de que el arreglo del merge
  funciona.
- Borra una y recarga: no vuelve.

Para esto necesitas la rama de Alejandro. Lo comprobé: en la integración, el controlador de
evidencias solo tiene el `POST /api/evidencias` de texto libre. **Ni la subida multipart
(`POST /api/inspecciones/{id}/evidencias`) ni la descarga de la imagen
(`GET /api/evidencias/{id}/archivo`) existen todavía**, así que sin su rama la foto da 404 y las
miniaturas se quedan en el esqueleto de carga. No es un fallo tuyo.

**375 px** (herramientas de desarrollador, iPhone SE):

- `Inspecciones.razor` tiene **nueve columnas** (líneas 45-53) y hasta **tres botones** por fila. A
  375 px eso es scroll horizontal seguro. Decide: o `Breakpoint="Breakpoint.Sm"` para pasar a
  tarjetas en móvil, o deja solo el botón principal (Continuar / Ver resultado) y mete "Ver",
  "Editar" y "Eliminar" en un `MudMenu`.
- En `PasoEvidencias.razor:44`, quita el `Width="200"` de la `MudImage`: el `mud-width-full` ya gana
  y ese atributo no hace nada más que confundir.

---

## Lo que ya **no** te tienes que preocupar

- **`/inspecciones/{id}/resultado` ya no da 404.** La página de Gary entró en la integración. Tu
  botón "Ver resultado" funciona.
- **El paso 1 del documento anterior** (integrar mi rama, quitar el guard de `subiendo`) lo hice yo
  en el merge.
- **El bloqueo del `DELETE` en el servidor**: ya lo tienen Alejandro y Simón.
- **No toques** `EjecutarInspeccion.razor` (es mío), ni `PasoPreguntas.razor` (Erica) ni
  `PasoFirma.razor` (Gary).

---

## Resumen

| # | Qué | Dónde | Depende de | Cuándo |
|---|---|---|---|---|
| 1 | **Borrar `CreateEvidenciaAsync`** — bloquea la entrada de BE-2 | `HttpClientService.cs:1179` | Nadie | **Hoy** |
| 2a | `HelperText` honesto en el filtro de estado | `Inspecciones.razor:29` | Nadie | Hoy |
| 2b | Filtro de estado contra el servidor + `currentPage = 1` | `Inspecciones.razor`, `HttpClientService.cs:1029` | Carlos (BE-0) | Cuando suba `estado` |
| 3 | No ofrecer "Eliminar" en inspecciones cerradas | `Evidencias.razor:62, 110` | Alejandro (BE-2) mergeado | Junto con su rama |
| 4 | Teléfono real + 375 px | — | Alejandro (BE-2) para la foto | Esta semana |

Los puntos 1 y 2a son de quince minutos y no dependen de nadie. Empieza por el 1: es lo que deja
pasar a Alejandro.

— Juan Alberto
