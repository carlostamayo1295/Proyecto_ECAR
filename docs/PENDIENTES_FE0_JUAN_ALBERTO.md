# Juan Alberto (FE-0, líder) — pendientes propios y de coordinación

**Rama de referencia:** `integracion_fronEnd`
**Actualizado:** 02/10/2026 · Índice general: [`PENDIENTES_FRONTEND.md`](PENDIENTES_FRONTEND.md)

Este documento separa lo que es **código o documentación mía** de lo que es **coordinación**:
cosas que no programo yo pero que, como líder del front, me toca mover para que los demás no se
queden bloqueados.

---

## A. Antes de mandar ningún documento al equipo

### A1. Borrar la rama duplicada `Integracion_fronEnd` · **urgente**

En GitHub hay dos ramas que solo se diferencian por la mayúscula:

| Rama | Commit | Contenido |
|---|---|---|
| `integracion_fronEnd` | la buena | los tres merges, la guarda nueva y estos documentos |
| `Integracion_fronEnd` (**I mayúscula**) | `96def9f` | solo mi arreglo del esqueleto, sin ningún merge |

En **Windows** —y en macOS con la configuración por defecto— el disco no distingue mayúsculas, así
que las dos refs chocan al hacer `fetch`: una tapa a la otra. Me pasó a mí: después del fetch, mi propia
rama desapareció de `git branch -r` y la local empezó a decir *"ahead 17"* respecto a la de
mayúscula. Si alguien corre `git checkout -b x origin/integracion_fronEnd`, puede acabar trabajando
sobre `96def9f` sin ninguno de los merges.

**Borrarla no pierde nada**: verificado que `96def9f` está contenido en la rama buena. Desde GitHub
(*Branches → Integracion_fronEnd → papelera*) o:

```bash
git push origin --delete Integracion_fronEnd
```

Hazlo **antes** de mandarle los pendientes a nadie, porque todos empiezan con ese `checkout`.

---

## B. Código mío

### B1. Retirar `MockDataService` · tarea 7 del plan, **desbloqueada**

Lo verifiqué en la integración: el único consumidor que queda de `MockDataService` es
`RespuestaInspeccionModal.razor`, y ese modal **no lo abre nadie** (la única mención es un comentario
dentro del propio `MockDataService`). La migración de Erica ya entró, que era la condición.

Borrar:

- `ECAR.Client/Services/MockDataService.cs`
- `ECAR.Client/Components/RespuestaInspeccionModal.razor`
- `ECAR.Client/Program.cs`, línea 20: `builder.Services.AddScoped<MockDataService>();`

Y `dotnet build --no-incremental` en 0/0.

### B2. El aviso de "Siguiente" dice cuántas faltan, no cuáles

**Archivo:** `ECAR.Client/Pages/EjecutarInspeccion.razor`, línea **196**

```csharp
Snackbar.Add($"Faltan {faltantes} pregunta(s) obligatoria(s) por responder", Severity.Warning);
```

El plan (FE-1, tarea 2) pide que el bloqueo **"muestre cuáles"**. La lógica vive en mi archivo, así
que es mío. Con el `Orden` de cada `PreguntaEjecucionDto` basta: *"Faltan por responder las
preguntas 2, 5 y 7"*. Lo mismo para la línea 204, la de novedades sin observación.

### B3. Sangría del bloque de Fase 3 en `HttpClientService.cs`

El bloque quedó con 8 espacios en vez de 4, heredado del merge de Erica del 26/09. Lo dejé así a
propósito para no mezclar ruido con los merges. **Va en un commit aislado**, y solo cuando Erica y
Santiago hayan subido lo suyo, porque toca muchas líneas y chocaría con cualquier cosa que estén
escribiendo en ese archivo.

### B4. Riesgo pendiente de verificar en el campo de Observación

Quité el eco del servidor de `GuardarAsync` (`96def9f`). Queda un caso que no he podido comprobar sin
levantar la aplicación: el campo de Observación usa `@bind-Value` con el debounce interno de
MudBlazor, así que su valor en memoria puede ir hasta 500 ms por detrás de lo que hay en pantalla. Si
MudBlazor 9.8.0 no protege contra que un repintado del padre le devuelva ese valor viejo, el texto
podría saltar. Lo va a destapar la prueba manual de Erica (su pendiente 4); si salta, es mío.

### B5. Unificar las guardas (opcional)

`AdminRouteGuard`, `TecnicoRouteGuard` y la nueva `RolRouteGuard` hacen lo mismo con listas de roles
distintas. Las dos primeras se pueden reescribir sobre la tercera. No es urgente y no lo mezclé con
la integración.

---

## C. Documentación mía

### C1. `ESTADO_PROYECTO.md` §4 · tarea 10 del plan, **vence hoy**

La tabla de pantallas está desactualizada desde el 22/09:

| Línea | Dice | Debería decir |
|---|---|---|
| 93 | Ejecutar inspección — *"Esqueleto (FE-0)"* | Los tres pasos implementados (FE-1/2/3) |
| 94 | Respuestas de inspección — *"Mock (`MockDataService`)"* | API real, solo consulta, filtro por inspección |
| 95 | Evidencias — *"API real (solo texto; sin archivo)"* | Galería con miniaturas; subida real pendiente de BE-2 |
| — | *(no aparece)* | `/inspecciones/iniciar` (inicio desde QR) |
| — | *(no aparece)* | `/inspecciones/{id}/resultado` (resultado firmado, imprimible) |

Y en la tabla de guardas, añadir `RolRouteGuard`.

### C2. `GUIA_FRONTEND_FASE3.md`: avisar de que `[Authorize]` no funciona

La guía dice qué guarda usar en cada tipo de pantalla (líneas 197-204 y 252-253) pero no dice que el
atributo `[Authorize]` de Blazor **no hace nada en este proyecto**. Yo mismo se lo indiqué a Gary en
su documento. Añadir una línea con el motivo (`RouteView` en vez de `AuthorizeRouteView`, sin
`AddAuthorizationCore`) y presentar `RolRouteGuard` para los casos con roles mixtos.

---

## D. Coordinación con backend

Nada de esto lo programo yo, pero cada punto tiene a alguien del front esperando.

### D1. Juan David (BE-1) — su rama no compila desde el 30/09

En `b87bfd4` añadió un parámetro `IInspeccionService` al constructor de `InspeccionesController` y no
actualizó las pruebas que lo instancian. Cuatro errores `CS7036` en
`ECAR.API.Tests/BackendPhaseThreeTests.cs`, líneas **140, 165, 211 y 239**. **Está roto en su propia
rama**, sin mezclarla con nada: lo compilé por separado.

El API sí compila (con 2 advertencias `CS8601` en `InspeccionService.cs:118` y `:206`), así que Erica
puede probar levantando solo el API. Pero así no puede entrar a `develop`. **Bloquea a Erica.**

### D2. Alejandro (BE-2) — dos problemas antes de mergear

**a) Su rama borra la migración de la base de Fase 3.** Elimina
`20260921114232_Fase3BaseInspecciones` (la que está en `develop` desde el 22/09) y añade
`20260922222833_AgregarOrdenPregunta`, que vuelve a crear las mismas columnas: `Estado`,
`FechaCierre`, `FirmaHash`, `IdChecklist` en `Inspecciones`, y las de `Evidencias`. Cualquier base de
datos que ya aplicó la de Carlos —o sea, la de todo el que haya levantado `develop`— va a intentar
añadir columnas que ya existen, y **el API no arrancará**. **No mergear tal cual.** Tiene que rebasar
sobre `develop` y generar una migración que parta de la de Carlos.

**b) No compila contra la integración hasta que Santiago borre `CreateEvidenciaAsync`.** Alejandro
eliminó `CreateEvidenciaDto` y ese método del cliente todavía lo usa. Lo comprobé mezclando su rama:
`CS0246` en `HttpClientService.cs:1179`. Es el pendiente 1 de Santiago.

Lo bueno: ya añadió `EstadoInspeccion` a `EvidenciaDto` y el `DELETE` devuelve 409 en inspecciones
cerradas. Eso desbloquea el pendiente 3 de Santiago en cuanto entre. **Bloquea a Santiago** (subida
real de fotos y miniaturas).

### D3. Carlos (BE-0) — el parámetro `estado` en `GET /api/inspecciones`

`[FromQuery] string? estado = null` y un `.Where(i => estado == null || i.Estado == estado)` antes de
paginar. Sin eso el filtro de Santiago se aplica solo sobre la página visible. **Bloquea a Santiago.**

### D4. Simón (BE-3) — `firma-inmutable` sin mergear

`POST /firmar`, `GET /resultado` y la inmutabilidad están en su rama desde el 29/09. Sin ella no se
puede cerrar ni consultar ninguna inspección. **Bloquea a Gary.**

---

## E. Cierre de la fase

### E1. Orden de merge hacia `develop`

Propuesta, de menos a más riesgo:

1. `firma-inmutable` (Simón) — no toca el front.
2. `feature-juandalopez` (Juan David) — **cuando arregle las pruebas** (D1).
3. Santiago borra `CreateEvidenciaAsync` en la integración.
4. `feature/ECAR-202-...` (Alejandro) — **cuando arregle la migración** (D2a).
5. `integracion_fronEnd` → `develop`.

Antes del paso 5, comprobar que cada rama de backend entra sin conflictos sobre la integración y que
la solución compila **entera** (no solo el API).

### E2. Prueba de integración completa · tarea 9 del plan

Con todo lo anterior mezclado: escanear el QR desde un teléfono real, iniciar, responder, subir dos
fotos seguidas, firmar, ver el resultado, imprimir. **A 375 px.** Es el recorrido que va a hacer el
cliente y **todavía no lo ha hecho nadie**: hoy no se puede, porque faltan cuatro endpoints en
`develop`.

---

## Resumen

| # | Qué | Tipo | Bloquea a | Cuándo |
|---|---|---|---|---|
| A1 | **Borrar `Integracion_fronEnd`** | Repo | Todo el equipo | **Antes de enviar nada** |
| B1 | Retirar `MockDataService` y el modal | Código | — | Hoy |
| B2 | "Siguiente" dice cuáles faltan, no cuántas | Código | — | Esta semana |
| B3 | Sangría de Fase 3 en `HttpClientService` | Código | — | Después de Erica y Santiago |
| B4 | Verificar el campo de Observación | Prueba | — | Con la prueba de Erica |
| B5 | Unificar guardas | Código | — | Opcional |
| C1 | `ESTADO_PROYECTO.md` §4 | Docs | — | **Hoy** |
| C2 | `[Authorize]` en la guía | Docs | — | Esta semana |
| D1 | Avisar a Juan David: pruebas rotas | Coordinación | Erica | **Hoy** |
| D2 | Avisar a Alejandro: migración y `CreateEvidenciaDto` | Coordinación | Santiago | **Hoy** |
| D3 | Pedir a Carlos el parámetro `estado` | Coordinación | Santiago | Esta semana |
| D4 | Mergear `firma-inmutable` | Coordinación | Gary | Esta semana |
| E1 | Orden de merge a `develop` | Cierre | — | Tras D1-D4 |
| E2 | Prueba completa en teléfono a 375 px | Cierre | — | Al final |
