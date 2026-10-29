# Changelog

Los cambios relevantes de ECAR se documentan aquí desde la Fase 4. Las fechas usan la zona
horaria de Colombia.

## [Sin publicar] — Fase 5

### Cambiado (pedidos del 30/10/2026, rama `integracion-back-front-fase-5`)

- **Sin pantalla de bienvenida**: al abrir el sistema sin sesión se va directo al inicio de sesión. La portada con
  «Bienvenido…» y «Iniciar Sesión» se quitó de `Pages/Home.razor`.
- **El tablero sigue al filtro** (antes la gráfica era siempre la de las últimas 8 semanas):
  - La tendencia (gráfica y tabla) es la del periodo elegido. Va por semana, de lunes a domingo, hasta 92 días, y por
    mes en un periodo más largo. La primera y la última semana o mes se recortan al periodo.
  - `GET /api/dashboard/tendencia` acepta `desde`, `hasta` y `total`, con las mismas reglas que el tablero. Trae
    `Agrupacion` (`Semana` o `Mes`). Sin ellos sigue dando las últimas `semanas` semanas.
  - La tarjeta «Equipos activos» dice cuántos se inspeccionaron en el periodo (`EquiposInspeccionados`, también en el
    PDF). «Equipos por criticidad» y «Equipos por área» aclaran que son el inventario actual: cambian con la planta,
    no con el periodo.
- **«Total general»** en el filtro de periodo: todos los meses, desde el de la primera inspección (de las plantas
  elegidas) hasta hoy, sin el límite de un año. La tendencia va por mes.
  - Contrato: `?total=true` en `GET /api/dashboard`, `/tendencia` y `/pdf`. `DashboardFiltroAplicadoDto.Total` lo
    indica; el periodo dice «Total general (del … al …)», y el pie del PDF y la auditoría empiezan con «total general».
  - Un rango de fechas sigue teniendo el máximo de un año.

### Cambiado (pedidos del 29/10/2026, rama `integracion-back-front-fase-5`)

- **Sin la pantalla «Preguntas de Checklist»**: se quitaron la página `/checklists/preguntas`, su formulario y su opción
  del menú. Las preguntas se editan dentro de cada checklist; el API `api/preguntaschecklist` no cambia.
- **Visor de evidencias**: al tocar una foto en Evidencias, en la ejecución de la inspección (paso «Evidencias») o en su
  resultado, se abre en grande (`VisorImagenDialog` y `wwwroot/js/visor-imagen.js`). Se acerca con la rueda del mouse,
  los botones o pellizcando; con zoom se mueve arrastrando y el doble clic la vuelve a ajustar. Es de solo lectura.
- **Resultado «Satisfactorio»** (`InspeccionResultados.Satisfactorio`): sale al firmar una inspección sin ninguna
  novedad. Con novedades sigue igual: «Pendiente» hasta resolver la última y después «Conforme». Las firmadas sin
  novedades antes de este cambio siguen como «Conforme» y el reporte «Inspecciones por fechas» las cuenta como
  satisfactorias.
- **Filtros de varios valores** en Reportes y en el tablero. Se pueden elegir varios equipos, usuarios, plantas, áreas,
  estados y criticidades a la vez.
  - Contrato: `ReporteFiltroDto` pasa a listas (`Plantas`, `Areas`, `IdEquipos`, `IdUsuarios`, `Estados`, `Criticidades`)
    y la URL repite la clave (`?plantas=Norte&plantas=Sur`).
  - El tablero acepta `?planta=` repetido, y `DashboardFiltroAplicadoDto` y `DashboardTendenciaDto` traen `Plantas`
    (antes `Planta`).
  - Equipos y usuarios se eligen con el buscador nuevo `BuscadorMultiple`, que los agrega como etiquetas. Las áreas
    que se ofrecen son las de las plantas elegidas.
  - El pie del PDF y la auditoría nombran los equipos por su código y los usuarios por su nombre, en lugar del id.
  - Se probaron en pantalla y por HTTP todos los filtros: cada reporte trae solo lo que coincide.
- **Roles oculta**: la pantalla «Roles» sale del menú y su dirección ya no abre nada. El componente se conserva, y los
  roles se siguen asignando en «Roles de Usuario».
- **Contraseñas** (decisión del 29/10/2026):
  - No caducan: `Seguridad:DiasExpiracionPassword` pasa a 0. Con un valor mayor que 0 vuelve la caducidad.
  - La que pone el Administrador al crear un usuario ya no es temporal: el usuario entra sin tener que cambiarla.
  - Restablecer la contraseña sigue dando una temporal.
- **PDF sin desbordes** (`AjusteTexto`):
  - El ancho de cada columna sale de su contenido: las cortas (fechas, códigos, estados) quedan en una línea y el resto
    se reparte entre las que tienen más información.
  - Una palabra que no cabe en su celda (correos, el JSON de la auditoría, nombres de archivo, la huella) se parte en
    renglones; antes se salía de la celda.
  - Aplica a los reportes, la auditoría, el tablero y el registro de la inspección.
  - Se revisaron los PDF convertidos a imagen: nada se sale de su celda.
- Evidencias: un nombre de archivo largo sin espacios ya no ensancha la tarjeta ni la página a 375 px.
- **«¿Presenta novedad?» en cada pregunta** (formulario de la inspección):
  - Cada pregunta trae, debajo de su Sí/No, un segundo Sí/No «¿Presenta novedad?». Empieza en «No».
  - La novedad la decide esa casilla y no la respuesta: responder «No» ya no crea una novedad, y un «Sí» con la casilla
    marcada sí la crea. Marcarla exige la observación, aunque la pregunta sea opcional.
  - Columna nueva `RespuestasInspeccion.PresentaNovedad` (`bit` nulo; migración `PresentaNovedadYSede`). Se muestra en el
    resultado («Con novedad» / «Sin novedad») y en el registro PDF (columna «¿Presenta novedad?»).
  - Las firmadas antes del cambio siguen mostrando como novedad su respuesta «No» (`ReglaNovedad`).
- **Sede de la inspección**: Rionegro o Medellín (`ECAR.Shared/Sedes`).
  - Se elige una vez, arriba del formulario, y se guarda al momento con `PUT /api/inspecciones/{id}/sede`. Solo la
    cambia quien ejecuta, mientras la inspección está en curso; otro valor da 400.
  - Sin sede no se firma, y la pantalla no deja pasar del paso de preguntas.
  - Columna nueva `Inspecciones.Sede` (`nvarchar(30)` nula). Sale en el resultado, en el registro PDF, en la columna
    «Sede» de los cuatro reportes tabulares y como filtro de varios valores (`?sedes=`) en todos los reportes.
- **Todas las preguntas son Sí/No**: el formulario del checklist ya no pide el tipo. El API rechaza con 400 una pregunta
  nueva o editada de tipo «Texto»; las que ya existen no cambian.
- **Sello `HMACSHA256-v3`**: cubre también la sede y la marca «¿Presenta novedad?» de cada respuesta. Los sellos v0, v1
  y v2 se siguen verificando.

### Completado en la integración (rama `integracion-back-front-fase-5`, 29/10/2026)

Lo que faltaba del cronograma al mezclar todas las ramas. No tenía rama: el tablero y la búsqueda de usuarios de FE-2,
y F-35 a F-37 de FE-3.

- **Tablero** (`Pages/Home.razor`):
  - F-20: textos que dicen qué depende del periodo, hora de Colombia en «actualizado a las» y vista a 375 px.
    «Actualizar» y «Exportar PDF» comparten la fila.
  - F-21: filtros por periodo (el mes en curso, uno de los 11 anteriores o un rango de fechas) y por planta. El título
    dice el periodo y la planta que aplicó el servidor. Si el rango no es válido (más de un año, «Desde» después de
    «Hasta»), se muestra el motivo del API junto a los filtros y se conservan los indicadores anteriores.
  - F-22: tendencia de las últimas 8 semanas (`MudChart` de líneas): firmadas, novedades registradas y resueltas. Solo
    depende de la planta. Debajo va una tabla con las mismas cifras, para el teléfono y para quien no distingue los
    colores.
  - F-23: «Exportar PDF» descarga el tablero con los filtros aplicados (B-03).
- **F-24 «Historial por usuario»:** el filtro «Usuario» busca en `GET /api/reportes/usuarios` (B-04). El Supervisor ya
  no recibe el error de `api/usuarios`.
- **Reportes:** el equipo y el usuario elegidos en los buscadores no llegaban al API. Se copiaban al filtro en
  `OnParametersSet`, que no corre al elegir. Ahora se aplican antes de cada consulta o descarga.
- **F-35 Cambiar contraseña:** se muestra cada motivo que devuelve el API (`Errors`, B-06), por ejemplo «La contraseña
  actual es incorrecta».
- **F-36 Firma:** `ConfirmarIdentidadDialog` reconoce la cuenta bloqueada por `CodigoHttp == 423` (B-07), no por la
  palabra «bloquead». Se probó en pantalla: el quinto intento fallido bloquea y el diálogo lo muestra.
- **F-37 Nueva inspección:** la fecha es un texto de solo lectura (hoy, hora de Colombia), sin el botón del calendario.
- **F-11:** «Continuar» y «Novedades» de Inspecciones, en azul.
- **F-13:** faltaba un espacio entre `Options` y `Class` en el `<MudDialog>` de `InspeccionModal`.
- **Tablero, periodo:** un rango dentro de un mes (del 05/10 al 10/10) se rotulaba «octubre de 2026», como si fuera el
  mes entero, en la pantalla y en el PDF. Ahora el nombre del mes sale solo para el mes entero o para el mes en curso
  hasta hoy (`DashboardController.TextoPeriodo`, prueba
  `DashboardPdfTests.El_periodo_lleva_el_nombre_del_mes_solo_si_es_el_mes_entero_o_el_en_curso`).
- `appsettings.json`: se deshizo una sangría accidental en `Evidencias:RutaBase` (commit `f20bb73` de `develop`).

### Integrado en `integracion-back-front-fase-5` (29/10/2026)

- Sobre `develop` (`d79814b`): `fase5/integracion-parcial` (`0c11a27`, que trae las ramas del 27/10),
  `feature/fase5-cierre-backend`, `fase5/colores-erica` (`1694d79`), `fase5/checklist-repetido-erica` y
  `fase5/enter-erica`, `fase5/resultado-gary` y `fase5/textos-gary`.
- Un solo conflicto, en la fila «§11.10(g)» de la matriz: se unió la lectura del Estándar (28/10) con la búsqueda de
  usuarios de reportes (B-04).
- Compila con 0 avisos; pasan 320 pruebas del API y 44 contra SQL Server, y no hay cambios de modelo pendientes.
- Se revisaron en pantalla las tareas del frontend y 18 pantallas a 1366, 1024, 768 y 375 px, sin desbordes ni botones
  ocultos.

### Corregido (ramas `fase5/colores-erica`, `fase5/checklist-repetido-erica` y `fase5/enter-erica`, 27 y 28/10/2026; FE-1)

- **F-11:** «Desactivar» lleva el ícono de bloqueo en lugar de la papelera, en Equipos, Usuarios y Checklists.
- **F-12 Checklist con nombre repetido:** con el 409 de B-05, el formulario muestra el mensaje del API y ofrece crear una
  «Nueva versión» del checklist activo que tiene ese nombre, con sus preguntas actuales.
- **F-13 Enter pasa al siguiente campo** (`wwwroot/js/formularios.js`): en los formularios de categoría, rol,
  ubicación, rol de usuario, checklist, usuario, inspección y equipo (clase `form-enter`). En el último campo lleva el
  foco a «Guardar» (`enter-guardar`) y nunca guarda solo. Las áreas de texto conservan el salto de línea, y el login,
  «Enter inicia sesión».

### Corregido (ramas `fase5/resultado-gary` y `fase5/textos-gary`, 27 y 28/10/2026; FE-3)

- **F-30 Resultado:** «Fecha de firma» en hora de Colombia (`dd/MM/yyyy HH:mm`) y «Sin ubicación» cuando el equipo no la
  tiene.
- **F-31:** un solo botón de PDF en el resultado, «Descargar registro PDF». Se quitó «Imprimir / PDF».
- **F-32:** «Atrás» y «Volver al listado» con `Variant.Filled`.
- **F-33 Roles de usuario:** los combos «Usuario» y «Rol» dicen «Seleccione un usuario» y «Seleccione un rol» en vez
  de «0».
- **F-34:** «Contraseña» y «La contraseña es requerida» en el login y en el formulario de usuario.

### Corregido (rama `feature/fase5-cierre-backend`, 28/10/2026; B-06, B-07 y B-09)

- **B-06 · Validaciones de contraseña (ECAR-225):** se retiró de los DTO de usuarios la regla antigua de
  6 caracteres. La creación, el cambio y el restablecimiento usan una sola política de 10 caracteres con letras
  y números. Los rechazos distinguen contraseña requerida, actual incorrecta, longitud, combinación y
  reutilización, siempre con mensajes en español.
- **B-07 · Cuenta bloqueada al firmar (ECAR-226):** `POST /api/inspecciones/{id}/firmar` y
  `POST /api/novedades/{id}/resolver` devuelven 423 si la cuenta ya estaba bloqueada o si el bloqueo se activa
  en ese mismo intento. La operación no modifica ni cierra el registro. El cliente reconoce el contrato por
  `ApiResponse.CodigoHttp` y no por el texto del mensaje.
- **B-09 · Regresión (ECAR-227):** la suite cubre bloqueo, caducidad, contraseña temporal, cambio obligatorio y
  reautenticación al firmar inspecciones y resolver novedades.

### Corregido (rama `fase5/base-front-juanalberto`, 26/10/2026; tareas de FE-0 del cronograma)

- **F-01 Tema.** `theme.css` pintaba de azul todo `.mud-button-root`. Por eso los botones de ícono, los interruptores y
  los adornos de los campos salían como círculos azules, los `Outlined` y `Text` quedaban ilegibles y un botón
  deshabilitado parecía activo.
  - Ahora solo los botones con relleno son azules, con texto blanco.
  - `Color.Error` se ve rojo y `Color.Warning` naranja.
  - Los deshabilitados se ven grises.
  - El primario de MudBlazor es el azul de ECAR (antes el morado por defecto).
- **F-02 Carga.** Las 13 listas mostraban «No hay … para mostrar» antes de que llegaran los datos. Ahora empiezan
  cargando.
- **F-03 Auditoría.** Cerrar la alerta de «Verificar integridad» lanzaba `NullReferenceException` y salía la barra
  roja de error.
- **F-04 Menú lateral.** Se resalta la página actual, «Administración» es un grupo desplegable y el cajón ya no tiene
  barra de desplazamiento horizontal (`MudNavMenu` en lugar de `MudList`).
- **F-05 Login.** Se muestra sin la barra azul ni el menú, y cabe en la pantalla sin desplazarse.

### Agregado (rama `fase5/servicios-front-juanalberto`, 27/10/2026; F-06 de FE-0)

- **`HttpClientService` para lo nuevo del backend**. Ningún método devuelve `null`.
  - `GetDashboardAsync(desde, hasta, planta)` (B-01): la llamada sin argumentos sigue igual.
  - `GetTendenciaTableroAsync(semanas, planta)` (B-02).
  - `DescargarTableroPdfAsync(desde, hasta, planta)` (B-03).
  - `BuscarUsuariosReportesAsync(texto)` (B-04).
  - `CreateChecklistAsync` y `UpdateChecklistAsync` pasan al estilo de la Fase 4, con el 409 de B-05 en `Message`.
- **`ApiResponse.CodigoHttp`**: solo en el cliente (`[JsonIgnore]`, no viaja en el JSON). Lo llena `LectorRespuestaApi` con
  el código HTTP de la respuesta, para reconocer la cuenta bloqueada (423) sin leer el texto del mensaje (F-36).
- Revisión de la integración (F-08): las 14 pantallas del menú a 1366, 1024, 768 y 375 px sin desbordes ni botones
  ocultos, y los colores de F-11 y la fila del checklist de F-10 comprobados en pantalla.

### Corregido (ramas `fase5/checklist-erica` y `fase5/colores-erica`, 25 y 26/10/2026; tareas de FE-1)

- **F-10 Checklist:** «Agregar pregunta» se lee y la fila de la pregunta ya no se superpone a 375 px.
- **F-11 Colores por tipo de acción** (sobre F-01):
  - «Desbloquear», en la lista y en su diálogo, y «Regenerar» del QR, en naranja.
  - «Activar» en azul y sin papelera en Checklists, Equipos y Usuarios.
  - `ConfirmDialog` con clase propia: «Confirmar» rojo y «Cancelar» azul en cualquier pantalla.

### Agregado (rama `fase5/tablero-pdf-alejandro`, 24/10/2026)

- **B-03 · Tablero en PDF** (ECAR-224): `GET /api/dashboard/pdf` genera el tablero en A4 vertical con
  MigraDoc (`Services/Documentos/TableroPdf.cs`): periodo, indicadores, equipos por criticidad y por área, y
  el pie común de `ExportadorTabla.Documento` con los filtros. Usa los filtros de B-01 (`desde`, `hasta`,
  `planta`; por defecto el mes en curso, un año como máximo) y el mismo cálculo que `GET /api/dashboard`,
  así que la pantalla y el PDF no pueden discrepar. Cada PDF queda en la auditoría como `Exportacion`
  (`Reporte = "tablero"`). Pruebas en `DashboardPdfTests`. Incluye la rama `feature/carlos-tamayo` (B-01).

### Agregado (rama `feature/carlos-tamayo`, 23/10/2026; entró con las de Alejandro y Juan David)

- **B-01 · Tablero con filtros**: `GET /api/dashboard?desde=&hasta=&planta=`. Sin filtros, el mes en curso; el rango
  no puede superar un año. La respuesta trae en `Filtros` los que se aplicaron.
- **B-02 · Tendencia**: `GET /api/dashboard/tendencia?semanas=8&planta=`, de 1 a 52 semanas que empiezan el lunes
  (hora de Colombia); por semana, inspecciones firmadas y novedades registradas y resueltas.

### Agregado (rama `feature/juan-david-lopez-t-ECAR-221-222`, 24/10/2026)

- **Búsqueda de usuarios para reportes** (B-04, ECAR-221, 24/10/2026): `GET /api/reportes/usuarios?texto=`, para el
  Administrador y el Supervisor (Operador y Estándar reciben 403). Busca en el nombre y el correo, incluye a los
  inactivos y devuelve como máximo 20, solo con id, nombre y correo (sin contraseña ni roles). El filtro «Usuario» de
  la pantalla de reportes sigue llamando a `api/usuarios`, que es solo del Administrador: **el cliente debe pasar a
  `api/reportes/usuarios`** para que el Supervisor pueda usarlo.
- **Sin nombres repetidos en checklists activos** (B-05, ECAR-222, 24/10/2026): `POST /api/checklists` y el cambio de
  nombre en `PUT /api/checklists/{id}` responden 409 si ya hay otro checklist activo con ese nombre, sin distinguir
  mayúsculas, minúsculas ni espacios externos, y piden usar «Nueva versión». El nombre se guarda sin espacios externos.
  Reenviar el mismo nombre al editar no se valida, y «Nueva versión» no cambia. Activar una versión
  (`PUT /api/checklists/{id}/activar` o `Activo = true` en el PUT) sigue comparando el nombre exacto.
- **Pruebas de integración de la Fase 5 con SQL Server** (B-08, ECAR-223, 24/10/2026), en `ECAR.IntegrationTests`:
  filtros del tablero (`DashboardSqlServerTests`: planta, periodo con los bordes del día en hora de Colombia,
  validaciones y valores por defecto), tendencia semanal (`DashboardTendenciaSqlServerTests`: semanas de lunes a
  domingo en Colombia, semanas vacías, planta y límites de 1 a 52) y nombres repetidos de checklists
  (`ChecklistNombresSqlServerTests`). Los datos del tablero se siembran con `DatosTablero`, una planta por prueba.
  La suite de integración corre ahora **en serie** y con 120 s por comando: en paralelo, cada clase creaba y migraba su
  base a la vez, y migrar una base entera en un solo comando pasaba de los 30 s por defecto; fallaba al azar.

### Integrado en `fase5/integracion-parcial` (27/10/2026; pendiente de `develop`)

- `fase5/base-front-juanalberto`, `fase5/colores-erica`, `fase5/checklist-erica`, `fase5/tablero-pdf-alejandro`
  (incluye `feature/carlos-tamayo`) y `feature/juan-david-lopez-t-ECAR-221-222`. Compila con 0 avisos; 311 pruebas del
  API y 44 contra SQL Server superadas.

## [Sin publicar] — Fase 4

### Cambiado (rama `Prueba_nuevo_novedades`, 19/10/2026)

- **Roles nuevos** (decisión de ECAR, 21/10/2026; constantes en `ECAR.Shared/RolesSistema.cs`):
  - **Operador** (antes Técnico): ejecuta sus inspecciones y consulta tablero, equipos, evidencias y checklists.
    No resuelve novedades: solo ve las de sus inspecciones, en el resultado de cada una. Sin reportes ni auditoría.
  - **Estándar** (nuevo): inspecciones y resolución de novedades, con la lectura de la inspección completa de otro
    que tenga novedades. Sin reportes ni auditoría.
  - **Supervisor** (antes Auditor): solo consulta, incluidos reportes, auditoría e integridad.
  - **Administrador**: sin cambios.
  - Al arrancar, el API renombra en la base «Técnico» → «Operador» y «Auditor» → «Supervisor» (la fila del rol;
    los usuarios lo conservan y queda en la auditoría) y crea «Estándar». Quien tenía sesión abierta debe volver a
    entrar. Los antiguos técnicos que resuelven novedades necesitan el rol Estándar.
  - El reporte «Historial por técnico» pasa a **«Historial por usuario»** (22/10/2026): ruta
    `GET /api/reportes/historial-usuario` (antes `historial-tecnico`), columna y filtro «Usuario».
- **Sin frecuencia de inspección ni cumplimiento** (decisión de ECAR, 21/10/2026): la migración
  `QuitarFrecuenciaInspeccion` borra la columna de `Equipos`; el formulario del equipo ya no la pide, el tablero
  cambia «Inspecciones pendientes del periodo» por «Inspecciones en curso» y se retira el reporte de cumplimiento.

- **Los hallazgos pasan a ser novedades.** La tabla `Novedades` reemplaza a `Hallazgos` (la migración
  `NovedadesReemplazanHallazgos` borra `Hallazgos` sin pasar sus datos: la base es de prueba). El API pasa de
  `api/hallazgos` a `api/novedades` y la pantalla, de `/hallazgos` a `/novedades` (la ruta anterior sigue abriendo la nueva).
- Al firmar una inspección se crea una novedad **Pendiente** por cada «No». Con novedades, la inspección queda
  firmada e inmutable, pero en estado **Novedades pendientes** y con resultado **Pendiente**; al resolverse (o
  anularse) la última, pasa sola a **Cerrada** con resultado **Conforme**. El reporte «Inspecciones por fechas»
  la sigue contando entre las «con novedad», porque la tuvo.
- Una novedad se **resuelve con firma**, como una inspección: lo que se hizo (10 a 2000 caracteres), la firma
  dibujada y la contraseña. La resuelve un Estándar o el Administrador, aunque no haya hecho la inspección.
  Solo el Administrador la anula, con motivo. No hay alta manual, edición, responsable ni reapertura.
- **Quien resuelve ve la inspección completa** (decisión de ECAR, 20/10/2026): un Estándar consulta, en solo
  lectura, las inspecciones de otros que tienen novedades (respuestas, fotos, firma y registro PDF), desde el
  detalle de la novedad («Ver la inspección completa»). Las que están en curso o no tuvieron novedades siguen
  siendo solo de quien las ejecuta, y su listado de inspecciones sigue mostrando solo las suyas.
- **Correo a la mesa de ayuda** de las novedades (`Correo:Destinatarios`; uno por inspección desde el 22/10), en segundo plano: si el correo falla,
  la firma no se revierte; queda en la auditoría (`NotificacionEnviada` / `NotificacionFallida`) y el Administrador
  puede reenviarlo. Sin `Correo:Usuario` y `Correo:Password` (user-secrets) el mensaje se guarda como `.eml` en
  `Correo:CarpetaSalida` y no sale.
- Sello de la inspección `HMACSHA256-v2` (sin el resultado); las firmadas con `HMACSHA256-v1` se siguen verificando.
- Tablero: novedades pendientes y resueltas; los reportes cuentan como firmadas las que tienen novedades pendientes.
- Inspecciones, su detalle y la ficha del equipo: columnas separadas de evidencias, novedades totales y novedades pendientes.
- **Pantallas responsive** (20/10/2026): entre 600 y 1550 px las tablas no cabían junto al menú y los botones
  quedaban fuera de la vista a la derecha.
  - Las tablas anchas (inspecciones, novedades, equipos, usuarios, auditoría y la ficha del equipo) se apilan hasta
    1280 px y las demás hasta 960 px (`Breakpoint`).
  - Por encima, la columna de los botones (`Class="col-acciones"`, reglas en `wwwroot/styles/theme.css`) queda fija
    a la derecha y las demás se desplazan por debajo; apilada, los botones bajan de línea.
  - Las celdas que no lo tenían llevan `DataLabel`.
  - Los diálogos anchos (ficha del equipo, detalle y edición de inspecciones, checklists, versiones y detalle de
    auditoría) salían todos con 600 px: MudBlazor 9 solo aplica las `Options` de `<MudDialog>` a los diálogos en
    línea. Ahora las aplican al abrirse (`SetOptionsAsync`). En el teléfono, todos los diálogos usan casi todo el ancho.
- **El QR se crea al guardar el equipo** (22/10/2026): `POST /api/equipos` le asigna el token y, al guardar, la
  pantalla abre su etiqueta para imprimirla. Los equipos creados antes lo reciben en su próxima edición (o al abrir
  «Código QR», como hasta ahora). Regenerar sigue siendo una acción aparte.
- **La etiqueta del QR ya no sale cortada:** se imprimía el diálogo ocultando el resto de la página con CSS
  (`css/print-qr.css`, retirado). Ahora se imprime solo la etiqueta, en un iframe aparte (`wwwroot/js/etiqueta-qr.js`),
  ajustada a la hoja: el QR mide hasta 60 mm y se encoge en papel de etiqueta pequeño.
- **Los reportes en PDF ya no salen cortados por la derecha:** con PDFsharp-MigraDoc 6, `Orientation = Landscape`
  sobre `PageFormat.A4` dejaba la hoja vertical mientras la tabla se medía para la horizontal. La hoja lleva ahora sus
  medidas a mano (29,7 × 21 cm) y los anchos de las columnas se escalan al ancho útil (25,7 cm).
- **Sin círculo negro al iniciar sesión** (22/10/2026): al entrar, la página se recarga y mientras arranca mostraba el
  indicador de carga de la plantilla de Blazor, cuyos estilos están en `css/app.css`, que no se carga; se veía como un
  círculo negro. Se quitó de `index.html`: mientras arranca solo se ve el fondo.
- **Sin el botón «API Scalar»** en la barra superior (22/10/2026). La documentación del API sigue en el API
  (`Scalar.AspNetCore`, `/scalar/v1`, solo en desarrollo).
- **Un solo correo por inspección** (22/10/2026): al firmar una inspección con novedades sale un correo a la mesa de
  ayuda con todas ellas (pregunta, observación del inspector y estado de cada una), no uno por novedad. La cola lleva
  inspecciones; «Reenviar» en el detalle de una novedad manda el correo de su inspección. En la auditoría,
  `NotificacionEnviada` / `NotificacionFallida` tienen como `RegistroId` la inspección y listan sus novedades.
- **Inspecciones sin «Editar»** (22/10/2026): se completan y se firman en la ejecución («Continuar»). El formulario
  de inspección queda solo para el registro manual del Administrador. `PUT /api/inspecciones/{id}` sigue en el API.
- **Anular una inspección es solo del Administrador** (22/10/2026), en la pantalla y en el API
  (`POST /api/inspecciones/{id}/anular`); antes también la anulaba su inspector.
- **El Estándar consulta todas las inspecciones, evidencias y novedades** (28/10/2026), como el Administrador y el
  Supervisor, las haya hecho quien las haya hecho: el listado de inspecciones (también el historial de un equipo), su
  detalle, resultado, respuestas (`api/inspecciones/{id}/respuestas` y `api/respuestasinspeccion`), registro PDF y
  ejecución, y las fotos (`api/evidencias` y `api/inspecciones/{id}/evidencias`). Las novedades ya las veía todas.
  Deja de valer lo de «solo las ajenas que tienen novedades» del 20/10/2026. El Operador sigue viendo solo lo de sus
  inspecciones, y las fotos retiradas siguen siendo solo del Administrador y el Supervisor.
- Consultar no es modificar: una inspección en curso la continúan quien la ejecuta o el Administrador, y la firma solo
  quien la ejecuta. En el listado, «Continuar» sale solo a ellos, y abrir la ejecución de una ajena por su dirección
  devuelve al listado. En la pantalla Evidencias, «Retirar» sale solo a quien el API se lo permite (el Administrador,
  quien subió la foto o quien ejecuta la inspección, y solo mientras está en curso); antes salía a todos, también al
  Supervisor y en las inspecciones anuladas, y el API lo rechazaba. Para eso `EvidenciaDto` trae `IdUsuarioInspeccion`.

### Añadido

- Matriz de trazabilidad entre el SRS, CFR 21 Parte 11, código y pruebas.
- Revisión de integración backend con estado de ECAR-209 a ECAR-216.

### Corregido (rama `integracion-fase4`, 18/10/2026, pruebas a fondo)

- Hallazgos: la descripción de una novedad no se edita y editar exige motivo.
- Criticidad cerrada (Alta, Media, Baja) en equipos y hallazgos.
- `POST /api/inspecciones` usa la fecha del servidor; la pantalla ya no deja elegirla.
- No se inicia ni se firma una inspección con un checklist sin preguntas, y esos checklists no se ofrecen.
- Consulta pública del QR sin sesión: solo nombre, código y ubicación.
- `PreguntasChecklistController` con DTOs (arregla el 500 de `/openapi/v1.json`), valida el tipo de respuesta
  y no cambia un checklist que ya tiene respuestas.
- Cabeceras de seguridad en el API y HSTS fuera de desarrollo.

### Añadido (rama `integracion-fase4`, 18/10/2026)

- **Tablero** de la portada (SRS §6) con `GET /api/dashboard`.
- **Escáner de QR** en «Iniciar inspección» con html5-qrcode 2.3.8 (Apache-2.0), servido desde el cliente.
- Cliente: menú y Scalar ocultos sin sesión (Scalar solo para el Administrador en desarrollo), Enter inicia
  sesión, Sí/No con teclado, «Con novedad», Auditoría sin desborde a 375 px y las pantallas de administración
  comprueban el rol antes de cargar.

### Integrado (rama `integracion-fase4`, 18/10/2026)

- Entran las ramas de BE-1 (`feature/anulacion-inspecciones-auditoria-Juandavid`) y BE-3
  (`seguridad-de-cuentas-y-firma-verificable`); su código sustituye al de `fase_4_prueba` en hallazgos,
  anulación, auditoría, cuentas y firma.
- Control de concurrencia (`RowVersion`) en inspecciones y hallazgos, hallazgo ligado a su respuesta y
  FK sin borrado en cascada (4 migraciones del 17/10).
- La huella HMAC incluye el SHA-256 de cada foto vigente; el API no arranca sin `Firma:ClaveHmac`.
- Ajustes al contrato: sin `DELETE` (405), cerrar un hallazgo exige pasar por EnProceso, el responsable
  debe ser Técnico o Administrador, integridad solo para Administrador y Auditor, `PUT /api/usuarios`
  sin contraseña, correo fijo tras firmar y la fecha de la Fase 3 leída de SQL Server como UTC.

### Añadido (rama `fase_4_prueba`, 16/10/2026)

- **Hallazgos (ECAR-211/212):** endpoints 4 a 9 con filtros, detalle con origen, alta manual,
  edición limitada, transiciones validadas (409), cierre con acción correctiva, reabrir solo el
  Administrador y anulación con motivo. Un hallazgo por cada "No" al firmar, en la misma transacción.
- **Inspecciones:** anular con motivo (endpoint 10); la inspección y sus fotos se conservan.
- **Auditoría:** filtros, historial de un registro y verificación de la cadena por lotes
  (endpoints 1 a 3); exportación a Excel y PDF (21).
- **Reportes (ECAR-213):** los seis del SRS §7 con vista previa paginada, PDF y Excel, fechas
  obligatorias (máximo un año) y cumplimiento según `FrecuenciaInspeccion`; registro PDF de cada
  inspección firmada (14).
- **Evidencias (ECAR-214):** retirar con motivo solo en inspecciones en curso; las retiradas no
  entran en la ejecución, el resultado ni la huella.
- **Firma (ECAR-215):** contraseña en cada firma (400 si es errónea, 423 si la cuenta está
  bloqueada), manifestación con nombre, significado y zona horaria, HMAC-SHA256 con
  `Firma:ClaveHmac` y verificación de integridad (13).
- **Cuentas (ECAR-216):** bloqueo tras 5 intentos con eventos de acceso, política e historial de
  contraseñas, caducidad a 90 días, cambio obligatorio tras restablecer, desbloquear, y
  `PUT /api/usuarios/{id}` ya no fija contraseñas (22 a 26).
- **§3.3:** motivo obligatorio al modificar equipos y checklists y al desactivar o reactivar.
- Frecuencia de inspección editable en equipos y evento `CierreSesion`.

### Cambiado

- Sin `DELETE` de inspecciones, evidencias ni hallazgos (responden 405).
- PDF con PDFsharp-MigraDoc **6.2.4**, la última versión MIT (la 6.5 es AGPL-3.0), y Excel con ClosedXML (MIT).
- `System.Security.Cryptography.Pkcs` 10.0.13 para cerrar las alertas NU1902/NU1903.

### Corregido

- La huella de la firma se recalculaba mal al leer la inspección de SQL Server: la fecha llega sin
  zona y se tomaba como hora local. Afectaba también a la verificación SHA-256 de la Fase 3.

### Verificado

- 143 pruebas de API y 11 de integración contra SQL Server superadas el 16/10/2026.
- Prueba de punta a punta contra el API: 62 de 62 comprobaciones.

### Pendiente

- Confirmar con ECAR la definición de cumplimiento, el texto del significado de la firma y la
  compresión de fotos (ALCOA+ "Original").
- Configurar `Firma:ClaveHmac` en cada entorno; en la Fase 6, quitar al login de la aplicación los
  permisos `ALTER` y `db_owner` (el trigger no frena a un `db_owner`).

## [2026-10-09] — Base backend de Fase 4 (ECAR-209/210)

### Añadido

- Migración `Fase4HallazgosAuditoria` con los campos de hallazgos, auditoría, evidencias,
  firma, seguridad de cuentas y frecuencia de inspección.
- Conversión de hallazgos heredados a estado `Abierto` y sellado de auditorías existentes.
- Trigger `TR_Auditoria_SoloInsercion` para impedir `UPDATE` y `DELETE` en auditoría.
- Auditoría automática y transaccional desde `ECARDbContext`, con usuario, IP, agente,
  valores anterior/nuevo y motivo.
- Cadena SHA-256 de integridad para las filas de auditoría.
- Revocación inmediata de JWT mediante `SecurityStamp`.
- Proyecto `ECAR.IntegrationTests` contra SQL Server para migraciones, transacciones, trigger
  y concurrencia de la cadena de auditoría.

### Verificado

- Si falla la escritura de auditoría también se revierte el cambio funcional.
- Los guardados concurrentes conservan una única cadena válida.
- El modelo EF no tiene cambios pendientes de migración.
- 95 pruebas de API y 8 pruebas de integración superadas el 11 de octubre de 2026.

## [2026-10-04] — Cierre técnico de Fase 3

### Añadido

- Flujo completo de inspecciones, respuestas, evidencias y firma.
- Inicio manual y mediante QR, resultado histórico e inmutabilidad tras el cierre.
- Autorización por rol y propiedad de la inspección.

### Corregido

- Contrato de respuestas `Si/No`, filtros, paginación, rutas de evidencias y manejo de sesión.
