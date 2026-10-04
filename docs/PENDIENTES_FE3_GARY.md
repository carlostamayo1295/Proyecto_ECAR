# Gary (FE-3) — pendientes

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
| `IniciarInspeccion` muestra los errores del API en vez de mandar al login · etiquetas y detalles | `9396278` |
| La guarda de `ResultadoInspeccion` (el `[Authorize]` no funcionaba; fue por una instrucción mía) | `75f6c45` |
| Al vencer la sesión, vuelta al login y después a la misma página | `f2ec51b` |

Verificado el 04/10 por HTTP: firmar con obligatorias sin responder da la lista, una novedad sin
observación se rechaza, una firma que no es PNG o pasa de 200 KB se rechaza, la firma correcta
cierra con resultado y hash SHA-256, el segundo firmar da 409, el resultado completo lo ve el
Auditor y otro técnico no. En el navegador: la URL del QR inicia la inspección y lleva a la
ejecución.

## Lo que te queda

**La prueba en una tablet o teléfono real:** firmar con el dedo (que el trazo siga al dedo a
375 px), "Usar esta firma" y "Firmar y cerrar", el resultado impreso sin menú ni botones, y el
**QR completo desde el teléfono sin sesión**: consulta → Iniciar → login → vuelve → ejecución.
