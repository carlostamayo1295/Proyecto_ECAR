# Erica (FE-1) — pendientes

**De:** Juan Alberto (FE-0) · **Actualizado:** 04/10/2026 · Índice: [`PENDIENTES_FRONTEND.md`](PENDIENTES_FRONTEND.md)

> **Rama de trabajo: `integracion/fase3-completa`.** Contiene las integraciones de backend y de
> frontend más todas las correcciones del 04/10. Sustituye a `integracion_fronEnd` y a
> `integration/backend-fase3` para seguir trabajando. Si tienes cambios locales sobre una de
> esas, actualiza antes de seguir:
>
> ```bash
> git fetch origin && git checkout -b mi-rama origin/integracion/fase3-completa
> ```

---

## Lo que ya está hecho — no lo repitas

| Qué | Commit |
|---|---|
| Observación siempre visible, obligatoria solo en novedad · `StateHasChanged` pelado retirado | `43f6eba` |
| Las respuestas Sí/No se envían como `"Si"` (el contrato); antes se enviaba `"Sí"` y toda respuesta afirmativa daba 400. El componente es mío | `07db102` |
| Una respuesta vacía se guarda como pendiente: escribir la observación antes de responder, desmarcar una casilla o borrar un texto ya no dan 400 | `064c2ff`, `6e9dfae` |

Verificado en el navegador el 04/10: marcar "Sí" guarda `"Si"` y el indicador dice "Guardado";
una observación escrita antes de responder se guarda; al recargar, el "Sí" sigue marcado.

## Lo que te queda

**La prueba en un teléfono real a 375 px**: dos Sí/No seguidas y rápido, una observación, texto en
la pregunta de texto parando a la mitad, "Siguiente" justo después de escribir, recargar. Todo
tiene que seguir ahí. Si algo falla, avísame: el componente de respuesta y el eco son míos.
