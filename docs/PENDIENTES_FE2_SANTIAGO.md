# Santiago (FE-2) — pendientes

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
| `CreateEvidenciaAsync` retirado · `DataLabel` en las tablas para móvil · `Width` sobrante | `b1d1c85` |
| Filtro por estado resuelto en el servidor, con vuelta a la página 1 | `7fdf44c` |
| `/evidencias` no ofrece "Eliminar" en inspecciones cerradas y muestra el estado | `bbb1e74` |
| El inspector de una inspección nueva es el usuario de la sesión; fuera "Resultado" y "Firma" del modal | `d995097` |

Verificado el 04/10: el filtro devuelve solo las de ese estado y la paginación las cuenta bien;
las evidencias de inspecciones cerradas no tienen botón; la subida rechaza un PDF renombrado y
archivos de más de 5 MB, y al borrar se elimina el archivo del disco.

## Lo que te queda

**La prueba con un teléfono real:** que `capture="environment"` abra la cámara trasera y no el
carrete, que una foto de 8-12 MB baje de 5 MB tras comprimir, y que **dos fotos seguidas sin
esperar** aparezcan las dos. A 375 px, las tablas apiladas deben mostrar sus etiquetas.
