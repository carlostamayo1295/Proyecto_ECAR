# Santiago (FE-2) — pendientes sobre la rama de integración

**De:** Juan Alberto (FE-0) · **Para:** Santiago Arango (FE-2)
**Rama de trabajo:** `integracion_fronEnd`
**Actualizado:** 02/10/2026 · Índice general: [`PENDIENTES_FRONTEND.md`](PENDIENTES_FRONTEND.md)

---

## Ya está hecho — no lo repitas

Tu rama está mergeada en `integracion_fronEnd`. **Tu código no tenía defectos propios**, y lo que
se podía hacer sin backend se resolvió el 02/10 en el commit **`b1d1c85`**:

| Pendiente | Qué se hizo |
|---|---|
| Borrar `CreateEvidenciaAsync` | Retirado de `HttpClientService`. Además de huérfano, **bloqueaba la rama de Alejandro**, que elimina `CreateEvidenciaDto`: mezclada sobre la integración daba `CS0246`. Comprobado después del cambio: BE-2 ya compila contra la integración |
| Filtro de estado honesto | `HelperText="Por ahora filtra solo las filas de esta página"` en el `MudSelect` |
| Móvil a 375 px | `DataLabel` en las 9 celdas de `Inspecciones.razor` y las 8 de la tabla de `Evidencias.razor` |
| `Width="200"` en la miniatura | Retirado: `mud-width-full` ya fija el ancho |

**Una corrección a lo que te dije antes:** te escribí que la tabla de Inspecciones, con nueve
columnas, haría scroll horizontal a 375 px. **No es así.** `MudTable` tiene `Breakpoint.Xs` por
defecto y por debajo de 600 px ya se apila en tarjetas. El problema real era otro: sin `DataLabel`,
en ese modo apilado los valores salían sin etiqueta ("42", "Balanza", "01/10/2026"…). Eso es lo que
se arregló.

Y del merge de tu rama, como ya sabías: `_subidasEnCurso` y `Subiendo` sustituyen al `bool
subiendo`, sin el guard que botaba la segunda foto, y con "Subiendo N fotografía(s)…".

> En GitHub hay una segunda rama, `Integracion_fronEnd` con **I mayúscula**, que no es la buena. En
> Windows chocan. Comprueba con `git ls-remote origin integracion_fronEnd` que el commit no es
> `96def9f`.

---

## Lo que te queda

Los tres dependen del backend.

### 1. Filtro de estado contra el servidor · depende de Carlos (BE-0)

Cuando `GET /api/inspecciones` acepte `estado`:

- En `HttpClientService.GetInspeccionesAsync`, añade `string? estado = null` y pásalo al query string
  solo si trae valor, como `search`.
- En `Inspecciones.razor`, borra `inspeccionesFiltradas`, usa `inspecciones` en el `MudTable`, quita
  el `HelperText` y recarga desde el servidor:

```csharp
private async Task OnEstadoChanged(string? value)
{
    estadoFiltro = value;
    currentPage = 1;   // sin esto te quedas en la página 7 de un listado que ahora tiene 2
    await LoadInspeccionesAsync();
}
```

### 2. Ocultar "Eliminar" en evidencias de inspecciones cerradas · depende de Alejandro (BE-2)

Alejandro ya añadió `EstadoInspeccion` a `EvidenciaDto` y el servidor ya responde 409 al borrar en
una inspección cerrada. Falta que su rama entre a la integración; **antes de eso, el campo no existe
y no compila**. Cuando entre, en `Evidencias.razor`, en la galería y en la tabla:

```razor
@if (evidencia.EstadoInspeccion != InspeccionEstados.Cerrada)
{
    @* botón Eliminar actual *@
}
```

Y muestra el estado como `MudChip` en la tarjeta, para que el administrador sepa por qué no hay
botón.

### 3. Prueba con un teléfono real · depende de Alejandro (BE-2)

En la integración **no existen** la subida multipart (`POST /api/inspecciones/{id}/evidencias`) ni la
descarga de la imagen (`GET /api/evidencias/{id}/archivo`): están en la rama de Alejandro. Con ella
mezclada en una rama de prueba (**no la subas**), y el API levantado con tu IP de red:

- `capture="environment"` abre la **cámara trasera**, no el carrete.
- Una foto real de 8-12 MB queda por debajo de 5 MB tras comprimir.
- **Dos fotos seguidas sin esperar**: aparecen las dos y el contador dice "Subiendo 2
  fotografía(s)…".
- A 375 px, las tarjetas apiladas de Inspecciones y Evidencias muestran sus etiquetas.

---

## No toques

`EjecutarInspeccion.razor` (FE-0), `PasoPreguntas.razor` (Erica), `PasoFirma.razor` (Gary).

— Juan Alberto
