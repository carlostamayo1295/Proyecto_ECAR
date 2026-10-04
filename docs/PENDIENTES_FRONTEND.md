# Pendientes del frontend — Fase 3

**Rama:** `integracion/fase3-completa` · **Actualizado:** 04/10/2026
**Estado:** compila con **0 errores y 0 advertencias** · **68 pruebas** automáticas ·
**60/60** comprobaciones del contrato en ejecución

> **Rama de trabajo: `integracion/fase3-completa`.** Contiene las integraciones de backend y de
> frontend más todas las correcciones del 04/10. Sustituye a `integracion_fronEnd` y a
> `integration/backend-fase3` para seguir trabajando. Si tienes cambios locales sobre una de
> esas, actualiza antes de seguir:
>
> ```bash
> git fetch origin && git checkout -b mi-rama origin/integracion/fase3-completa
> ```

---

## Resumen

**La Fase 3 está completa en código y verificada en ejecución.** Se juntaron las integraciones
de backend y frontend, se corrigió todo lo que se encontró al verificar y se probó el flujo
real: por HTTP (60 comprobaciones contra el contrato) y en el navegador. Detalle y lista de
defectos corregidos en [`ESTADO_PROYECTO.md`](ESTADO_PROYECTO.md) §3.1.

**Lo que no se ha probado todavía es un teléfono real**, y eso es lo único que queda por persona.

---

## Lo que queda

| Persona | Documento | Pendiente |
|---|---|---|
| **Juan Alberto** (FE-0) | [`PENDIENTES_FE0_JUAN_ALBERTO.md`](PENDIENTES_FE0_JUAN_ALBERTO.md) | PR a `develop`, borrar las ramas viejas, prueba completa en teléfono |
| **Erica** (FE-1) | [`PENDIENTES_FE1_ERICA.md`](PENDIENTES_FE1_ERICA.md) | Prueba del paso de preguntas en un teléfono a 375 px |
| **Santiago** (FE-2) | [`PENDIENTES_FE2_SANTIAGO.md`](PENDIENTES_FE2_SANTIAGO.md) | Prueba de la cámara con una foto real |
| **Gary** (FE-3) | [`PENDIENTES_FE3_GARY.md`](PENDIENTES_FE3_GARY.md) | Prueba de la firma táctil, la impresión y el QR desde el teléfono |

Ningún pendiente depende ya del backend: los cuatro endpoints que faltaban están integrados.
