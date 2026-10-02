# Erica (FE-1) — pendientes sobre la rama de integración

**De:** Juan Alberto (FE-0) · **Para:** Erica Avendaño (FE-1)
**Rama de trabajo:** `integracion_fronEnd` (commit `75f6c45`)
**Sustituye a:** la lista anterior del 30/09, que apuntaba a `feature/ECAR-205-pantalla-ejecucion-preguntas`
**Actualizado:** 02/10/2026 · Índice general: [`PENDIENTES_FRONTEND.md`](PENDIENTES_FRONTEND.md)

---

## Lo primero: tu trabajo ya está integrado

Tu rama `feature/ECAR-205-pantalla-ejecucion-preguntas` **ya está mergeada** en
`integracion_fronEnd`, junto con la de Santiago, la de Gary y mi arreglo del esqueleto. La rama
compila con **0 errores y 0 advertencias** y pasa **32/32 pruebas**.

Eso cambia dos cosas para ti:

1. **A partir de ahora trabajas sobre `integracion_fronEnd`**, no sobre tu rama. Si sigues en la
   tuya, lo que escribas va a chocar con el merge que ya hice.
   ```bash
   git fetch origin && git checkout -b fix/fe1-observacion origin/integracion_fronEnd
   ```

   > **Ojo antes de correr eso.** En GitHub hay una segunda rama, `Integracion_fronEnd` con **I
   > mayúscula**, que no es la buena. En Windows las dos chocan porque el disco no distingue
   > mayúsculas. Comprueba con `git ls-remote origin integracion_fronEnd` que el commit no es
   > `96def9f`. Si lo es, avísame antes de tocar nada.
2. **Ya no tienes que abrir un PR de tu rama.** Lo que queda son cuatro cosas pequeñas; cuando las
   tengas, un PR corto contra `integracion_fronEnd` y listo.

De las cinco cosas de la lista anterior, **la 5 (abrir el PR) se cae** y las otras cuatro siguen.

---

## Lo que toqué de tus archivos al integrar

Te lo digo para que no lo deshagas sin darte cuenta, porque son cambios dentro de código tuyo.

### `PasoPreguntas.razor`

- **Quité el eco del servidor de `GuardarAsync`.** Tenías esto:
  ```csharp
  pregunta.IdRespuesta = confirmada.IdRespuesta;
  pregunta.Respuesta   = confirmada.Respuesta;    // ← eliminado
  pregunta.Observacion = confirmada.Observacion;  // ← eliminado
  ```
  No es culpa tuya: mi documento anterior te dijo que tomaras `GuardarAsync` de mi rama, y mi
  versión tenía esas dos líneas. Las corregí después, en `96def9f`.

  **Por qué estaban mal:** entre que sale la petición y vuelve la respuesta pasan cientos de
  milisegundos, y en una pregunta de **texto** el técnico sigue escribiendo. Al llegar el eco, el
  campo revertía a la frase a medias que se había enviado. Se reproduce escribiendo, parando medio
  segundo (salta tu debounce y se envía) y siguiendo sin levantar las manos.

  Del eco se toma solo `IdRespuesta`, que es lo único que el cliente no tenía. `Respuesta` y
  `Observacion` ya las tienes y más frescas que el servidor, porque el API guarda literalmente lo
  que se le manda sin recortar ni normalizar (`InspeccionService.GuardarRespuestasAsync`).
  La regla está en `GUIA_FRONTEND_FASE3.md`, sección *"Tampoco copies de vuelta lo que el usuario
  acaba de escribir"*.

- **El automerge dejó el indicador de estado dos veces** — tu bloque y el mío, uno detrás del
  otro. Dejé uno solo, con mi comentario encima. No lo vuelvas a añadir.

- Tu cabecera del componente (líneas 3-11) se quedó tal como la escribiste. La mía era el texto de
  instrucciones y la descarté.

### `HttpClientService.cs`

- **`GuardarRespuestasAsync` quedaba duplicado**: tu copia y la mía, con el mismo nombre y la misma
  firma. Conservé **la tuya** y le devolví las dos líneas de comentario del contrato que se habían
  perdido en tu merge del 26/09 (las que explican que devuelve el estado completo recalculado, así
  que no hace falta volver a pedir la ejecución tras guardar).

- Tu bloque de Fase 3 quedó con **sangría de 8 espacios** en vez de los 4 del resto del archivo.
  No lo reformateé para no mezclar ruido con el merge. **No lo arregles tú tampoco**: lo hago yo en
  un commit aparte, porque toca muchas líneas y chocaría con cualquier cosa que estés escribiendo.

---

## Pendiente 1 — La observación tiene que estar visible siempre

**Archivo:** `ECAR.Client/Components/PasoPreguntas.razor`, línea **36**

Sigue igual: el campo solo existe cuando la pregunta ya es novedad.

```razor
@if (pregunta.EsNovedad)
{
    <MudTextField T="string" Label="Observación" @bind-Value="pregunta.Observacion" ... Required="true" ... />
}
```

El encargo (`docs/PLAN_FASE3_TAREAS.md` §5, FE-1, tarea 1) pide dos cosas y solo está la segunda:

> *"observación **visible siempre** y **obligatoria cuando `SiNo = No`** (SRS #4) con validación en
> pantalla"*

**El escenario que lo rompe:** el técnico marca "No", escribe la observación —que se guarda sola,
gracias a tu `@bind-Value:after`—, y luego se da cuenta de que se equivocó de casilla y cambia a
"Sí". El campo desaparece de la pantalla, pero `pregunta.Observacion` conserva el texto y tu
`EsperarYGuardarAsync` lo vuelve a mandar en el siguiente guardado (línea 113,
`Observacion = pregunta.Observacion`). Queda **una observación guardada en la base de datos, en una
respuesta conforme, que el técnico ya no puede ver ni borrar.** Después aparece en el resultado
firmado, que es inmutable por la regla 6 del SRS, y nadie sabe de dónde salió.

Y en una pregunta de tipo `Texto` no hay forma de añadir contexto, porque `EsNovedad` solo es
`true` en preguntas Sí/No respondidas con "No".

**Qué hacer.** Saca el campo del `@if` y cambia solo el aspecto según sea novedad o no:

```razor
<RespuestaPreguntaInput Label="@(pregunta.Obligatoria ? pregunta.Pregunta + " *" : pregunta.Pregunta)"
                         TipoRespuesta="@pregunta.TipoRespuesta"
                         Valor="@pregunta.Respuesta"
                         ValorChanged="(nuevoValor) => OnRespuestaCambiada(pregunta, nuevoValor)" />

<MudTextField T="string" @bind-Value="pregunta.Observacion"
              @bind-Value:after="() => ProgramarGuardado(pregunta)"
              Label="@(pregunta.EsNovedad ? "Observación (obligatoria)" : "Observación (opcional)")"
              Variant="Variant.Outlined" Lines="2"
              Required="@pregunta.EsNovedad"
              Immediate="true" DebounceInterval="500"
              Error="@(pregunta.EsNovedad && string.IsNullOrWhiteSpace(pregunta.Observacion))"
              ErrorText="La observación es obligatoria cuando hay novedad" />
```

Fíjate en que `Required` pasa de `true` fijo a `@pregunta.EsNovedad`: el campo está siempre, pero
solo es obligatorio cuando corresponde. Con esto, el técnico que se equivocó de casilla ve su
observación vieja y la borra él mismo, que es lo que cierra el agujero.

> **Contexto justo:** esto ya estaba así en tu versión original y **yo no te lo señalé** en el
> primer documento — cuando te escribí el paso de la observación cité tus líneas sin mirar el `@if`
> que las envolvía. Lo pongo ahora porque es el encargo y porque el efecto es real, no para
> cobrártelo dos veces.

---

## Pendiente 2 — El `StateHasChanged` pelado

**Archivo:** `ECAR.Client/Components/PasoPreguntas.razor`, línea **117**

```csharp
private async Task EsperarYGuardarAsync(PreguntaEjecucionDto pregunta, CancellationToken token)
{
    try
    {
        await Task.Delay(500, token);
        await GuardarAsync(new List<RespuestaEjecucionDto> { ... });

        StateHasChanged();   // ← línea 117
    }
    ...
}
```

`EsperarYGuardarAsync` se lanza con `_ =` desde `ProgramarGuardado`, o sea **fuera del ciclo de
render**. Contradice tu propio comentario de la línea 128 ("Se invoca desde tareas lanzadas fuera
del ciclo de render (debounce), por eso el repintado va siempre por `InvokeAsync`") y la regla de
`GUIA_FRONTEND_FASE3.md`.

Y además **es redundante**: `GuardarAsync` ya termina llamando a `CambiarEstadoAsync`, que hace
`await InvokeAsync(StateHasChanged)`. Para cuando llegas a la línea 117 la pantalla ya se repintó.

En WASM, que es un solo hilo, hoy no rompe nada. Pero es la clase de línea que estalla el día que
alguien pruebe en Blazor Server. **Bórrala.** Si prefieres dejar un repintado explícito ahí, que sea
`await InvokeAsync(StateHasChanged);`.

---

## Pendiente 3 — Cancela los debounces al salir (opcional)

**Archivo:** `ECAR.Client/Components/PasoPreguntas.razor`, línea **80**

Tu diccionario `_debounces` guarda un `CancellationTokenSource` por pregunta y ninguno se cancela
cuando el componente se destruye. Si el técnico sale de la pantalla dentro de los 500 ms del último
cambio, queda un `Task.Delay` suelto que va a intentar guardar y repintar un componente que ya no
está en el árbol.

No lo he visto fallar y probablemente Blazor lo ignore en silencio, así que no te lo pongo como
defecto. Pero es la higiene estándar y son ocho líneas:

```csharp
@implements IDisposable
```

```csharp
public void Dispose()
{
    foreach (var cts in _debounces.Values)
    {
        cts.Cancel();
        cts.Dispose();
    }

    _debounces.Clear();
}
```

Hazlo si te sobra un minuto; no bloquea nada.

---

## Pendiente 4 — La prueba manual (tu tarea 4 del plan)

Esta es tuya y nadie la puede hacer por ti. Es la que demuestra que el debounce por pregunta y el
autoguardado de la observación funcionan de verdad.

**Antes necesitas los endpoints**, que siguen sin estar en `develop` ni en la rama de integración.
Ármate una rama local de prueba y **no la subas**:

```bash
git checkout -b prueba/fe1 origin/integracion_fronEnd && git merge origin/feature-juandalopez
```

Lo probé y el merge **entra limpio en git**, pero aviso de algo para que no pierdas tiempo
buscándole la causa: **la solución completa no compila**, y no es por el merge.

Juan David, en su commit `b87bfd4`, le añadió un parámetro `IInspeccionService` al constructor de
`InspeccionesController` y no actualizó las pruebas que lo instancian. Resultado: cuatro errores
`CS7036` en `ECAR.API.Tests/BackendPhaseThreeTests.cs` (líneas 140, 165, 211 y 239).
**Está roto en su propia rama**, sin mezclar con nada — lo comprobé por separado. Ya le aviso yo.

**No te bloquea:** el fallo es solo del proyecto de pruebas, y para levantar el API no se compila.
Verificado: `ECAR.API` solo compila bien (con 2 advertencias `CS8601` en
`InspeccionService.cs:118` y `:206`, suyas, no las persigas). Así que arranca con:

```bash
dotnet run --project ECAR.API
```

y no toques `dotnet build` de la solución entera mientras esa rama esté mezclada, porque te va a
dar esos cuatro errores que no son tuyos.

Después:

1. Abre una inspección con **dos preguntas Sí/No y una de texto**.
2. Responde las dos Sí/No **seguidas y rápido**, sin esperar entre una y otra.
3. Marca "No" en una y escribe la observación.
4. Escribe algo largo en la de texto, **para a media frase medio segundo, y sigue escribiendo**.
   El texto no debe saltar hacia atrás. Esto valida mi arreglo del eco, no el tuyo, pero es el
   mismo recorrido.
5. **Recarga la página (F5).**
6. **Las cuatro cosas tienen que seguir ahí.** Si se pierde alguna, el debounce no está aislando
   bien por pregunta.
7. Cambia esa respuesta de "No" a "Sí" y comprueba que puedes ver y borrar la observación vieja
   (esto valida el pendiente 1).

Si la 10002 sigue siendo tu única inspección y tiene un checklist de una sola pregunta: agrégale
dos preguntas al checklist desde `/preguntas-checklist` y entra a `/inspecciones/ejecutar/10002`.
Las preguntas se leen en vivo del checklist (`CargarEjecucionAsync` hace
`.Include(i => i.Checklist).ThenInclude(c => c.Preguntas)`), así que aparecen de inmediato. Solo
sirve si la inspección sigue `EnCurso`.

Ahora el modal de registrar inspección **ya manda `IdChecklist`**: el selector de Santiago entró en
la integración, así que el 400 que reportaste está resuelto y puedes crear inspecciones nuevas.

---

## Lo que **no** es tuyo

- **El selector de checklist del modal.** Era de Santiago y ya está en la rama.
- **Los tres endpoints que te faltan** (`PUT`/`GET respuestas` y `GET respuestasinspeccion`). De
  Juan David, sin mergear.
- **El eco del servidor** y **la sangría del bloque de Fase 3** de `HttpClientService`. Míos.
- **El texto que revierte al escribir seguido en una pregunta de tipo Texto.** Mío en dos sitios: el
  eco (ya corregido) y el `RespuestaPreguntaInput` con `Immediate="true"` sin `DebounceInterval`,
  que escribí yo en `40c56a7`. Si en la prueba del punto 4 ves que todavía salta, dímelo y lo
  arreglo yo.

---

## Resumen

| # | Qué | Dónde | Tamaño |
|---|---|---|---|
| 1 | Observación visible siempre, obligatoria solo en novedad | `PasoPreguntas.razor:36` | Baja |
| 2 | Quitar el `StateHasChanged()` pelado | `PasoPreguntas.razor:117` | Trivial |
| 3 | `IDisposable` para los debounces (opcional) | `PasoPreguntas.razor:80` | Trivial |
| 4 | Prueba manual con recarga | — | Media |

Los pendientes 1, 2 y 3 son media hora y no dependen de nadie. El 4 necesita la rama de Juan David,
pero la rama de prueba te la deja lista en un comando.

— Juan Alberto
