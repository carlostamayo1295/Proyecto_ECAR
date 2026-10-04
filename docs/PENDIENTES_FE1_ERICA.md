# Erica (FE-1) — pendientes sobre la rama de integración

**De:** Juan Alberto (FE-0) · **Para:** Erica Avendaño (FE-1)
**Rama de trabajo:** `integracion_fronEnd`
**Actualizado:** 02/10/2026 · Índice general: [`PENDIENTES_FRONTEND.md`](PENDIENTES_FRONTEND.md)

---

## Ya está hecho — no lo repitas

Tu rama está mergeada en `integracion_fronEnd` y tus pendientes de código se resolvieron sobre
ella el 02/10, en el commit **`43f6eba`**:

| Pendiente | Qué se hizo |
|---|---|
| Observación visible siempre | Salió del `@if (pregunta.EsNovedad)`. Está siempre, con `Required="@pregunta.EsNovedad"` y la etiqueta "(obligatoria)" u "(opcional)". Si el técnico corrige "No" → "Sí", ve su observación vieja y la puede borrar |
| `StateHasChanged()` pelado | Retirado de `EsperarYGuardarAsync`: era redundante, `GuardarAsync` ya repinta con `InvokeAsync` |
| `IDisposable` para los debounces | **No se hizo, a propósito.** Te lo había propuesto como higiene y era un error: si el técnico pulsa "Siguiente" dentro de los 500 ms de su última respuesta, `MudStepper` desmonta el paso, y es esa tarea pendiente la que termina de guardar. Cancelarla perdería la respuesta. Quedó un comentario en el código explicándolo |

Además, al integrar tu rama:

- Se quitó el **eco del servidor** de `GuardarAsync` (`pregunta.Respuesta = confirmada.Respuesta` y
  la de `Observacion`). Venía de mi versión, no de la tuya: en preguntas de texto hacía que el campo
  revirtiera mientras el técnico seguía escribiendo.
- El indicador de estado había quedado **duplicado** por el automerge; queda uno.
- `GuardarRespuestasAsync` estaba **duplicado** en `HttpClientService`; queda el tuyo.
- El bloque de Fase 3 de `HttpClientService` volvió a **4 espacios** de sangría.

Si tienes cambios locales sobre `PasoPreguntas.razor` o `HttpClientService.cs`, actualiza antes de
seguir:

```bash
git fetch origin && git checkout -b prueba/fe1 origin/integracion_fronEnd
```

> En GitHub hay una segunda rama, `Integracion_fronEnd` con **I mayúscula**, que no es la buena. En
> Windows chocan. Comprueba con `git ls-remote origin integracion_fronEnd` que el commit no es
> `96def9f`.

---

## Lo que te queda: la prueba manual (tarea 4 del plan)

Es tuya y nadie la puede hacer por ti: demuestra que el debounce por pregunta y el autoguardado de
la observación funcionan de verdad.

### Necesitas los endpoints de Juan David

`PUT respuestas` y `GET respuestasinspeccion` siguen sin estar en `develop`. Mezcla su rama en tu
rama de prueba y **no la subas**:

```bash
git merge origin/feature-juandalopez
```

El merge entra limpio, pero **la solución completa no compila, y no es por ti**: Juan David añadió un
parámetro al constructor de `InspeccionesController` y no actualizó las pruebas. Cuatro errores
`CS7036` en `BackendPhaseThreeTests.cs`. Está roto en su propia rama; ya se le avisa.

**No te bloquea:** levanta solo el API, que sí compila:

```bash
dotnet run --project ECAR.API
```

y no corras `dotnet build` de la solución entera mientras tengas su rama mezclada.

### Recorrido

1. Abre una inspección con **dos preguntas Sí/No y una de texto**. El modal de alta ya pide el
   checklist (el 400 que reportaste está resuelto).
2. Responde las dos Sí/No **seguidas y rápido**.
3. Marca "No" en una y escribe la observación.
4. En la de texto, escribe una frase larga, **para medio segundo a la mitad y sigue escribiendo**. El
   texto no debe saltar hacia atrás.
5. **Pulsa "Siguiente" justo después de escribir**, sin esperar. Vuelve atrás: la respuesta tiene
   que estar.
6. **Recarga la página (F5).** Todo tiene que seguir ahí.
7. Cambia la respuesta con observación de "No" a "Sí": la observación sigue visible, como opcional, y
   la puedes borrar.
8. Deja una obligatoria sin responder y pulsa "Siguiente": el aviso tiene que decir **cuál** falta
   por su número.

Si algo falla en los pasos 4 o 5, **avísame a mí**: el campo de texto (`RespuestaPreguntaInput`) y el
eco son míos.

---

## No toques

`EjecutarInspeccion.razor` (FE-0), `PasoEvidencias.razor` (Santiago), `PasoFirma.razor` (Gary).

— Juan Alberto
