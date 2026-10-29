using System.Security.Claims;
using ECAR.API.Controllers;
using ECAR.API.Services;
using ECAR.Infrastructure.Data;
using ECAR.Infrastructure.Entities;
using ECAR.Shared;
using ECAR.Shared.DTOs;
using ECAR.Shared.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ECAR.API.Tests;

/// <summary>
/// Correcciones de Informes/PRUEBAS_INTEGRACION_2026-10-18.md: criticidad cerrada, fecha de
/// inspección del servidor, checklists sin preguntas, consulta pública del QR, preguntas por DTO y
/// tablero de indicadores. Las novedades no se editan (no hay PUT): ver NovedadesModeloTests.
/// </summary>
public class CorreccionesPruebasTests
{
    private const string Motivo = "Motivo de la prueba de correcciones";

    private sealed class Usuario1(long id, params string[] roles) : ICurrentUser
    {
        public long IdUsuario => id;
        public string Nombre => "Usuario de pruebas";
        public IReadOnlyCollection<string> Roles => roles;
        public bool IsInRole(string role) => roles.Contains(role);
    }

    private sealed class Contexto : IContextoAuditoria
    {
        public long? IdUsuario => 1;
        public string Usuario => "Pruebas";
        public string? DireccionIp => null;
        public string? AgenteUsuario => null;
        public string? Motivo => null;
    }

    private sealed record Escenario(ECARDbContext Db, Usuario Tecnico, Equipo Equipo, Checklist Checklist, Checklist Vacio);

    private static async Task<Escenario> SeedAsync()
    {
        var db = new ECARDbContext(new DbContextOptionsBuilder<ECARDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, new Contexto());
        var tecnico = new Usuario { Nombre = "Ana", Correo = "ana@ecar.test", PasswordHash = "x", Activo = true };
        tecnico.UsuarioRoles.Add(new UsuarioRol { Rol = new Rol { Nombre = "Operador" } });
        var ubicacion = new Ubicacion { Planta = "Planta 1", Area = "Pesaje" };
        var equipo = new Equipo
        {
            CodigoInterno = "EQ-1", ActivoFijo = "AF-1", NombreEquipo = "Balanza", Marca = "Mettler", Modelo = "XS205",
            SerialFabricante = "SN-77", Criticidad = Criticidades.Alta, Activo = true, Ubicacion = ubicacion,
            QRCode = new string('a', 32)
        };
        var checklist = new Checklist
        {
            Nombre = "Diario", Version = "1", Activo = true,
            Preguntas = [new PreguntaChecklist { Pregunta = "¿Nivelada?", TipoRespuesta = TiposRespuesta.SiNo, Obligatoria = true, Orden = 1 }]
        };
        var vacio = new Checklist { Nombre = "Sin preguntas", Version = "1", Activo = true };
        db.AddRange(tecnico, equipo, checklist, vacio);
        await db.SaveChangesAsync();
        return new Escenario(db, tecnico, equipo, checklist, vacio);
    }

    /// <summary>Una inspección firmada con una novedad sin resolver.</summary>
    private static async Task<Novedad> NovedadPendienteAsync(Escenario e)
    {
        var inspeccion = new Inspeccion
        {
            IdEquipo = e.Equipo.IdEquipo, IdChecklist = e.Checklist.IdChecklist, IdUsuario = e.Tecnico.IdUsuario,
            FechaInspeccion = DateTime.UtcNow, Estado = InspeccionEstados.NovedadesPendientes,
            Resultado = InspeccionResultados.Pendiente
        };
        e.Db.Inspecciones.Add(inspeccion);
        await e.Db.SaveChangesAsync();
        var novedad = new Novedad
        {
            IdInspeccion = inspeccion.IdInspeccion, IdPregunta = e.Checklist.Preguntas.Single().IdPregunta,
            Descripcion = "¿Nivelada?: Burbuja corrida", FechaRegistro = DateTime.UtcNow, IdUsuarioRegistro = e.Tecnico.IdUsuario
        };
        e.Db.Novedades.Add(novedad);
        await e.Db.SaveChangesAsync();
        e.Db.ChangeTracker.Clear();
        return novedad;
    }

    private static EquiposController Equipos(Escenario e, bool conSesion)
    {
        var controlador = new EquiposController(e.Db, new ConfigurationBuilder().Build(), new MotivoCambio());
        var identidad = conSesion ? new ClaimsIdentity([new Claim(ClaimTypes.Role, "Operador")], "Pruebas") : new ClaimsIdentity();
        controlador.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identidad) } };
        return controlador;
    }

    // ------------------------------------------------------------------ criticidad (punto 2)

    [Fact]
    public async Task La_criticidad_solo_admite_Alta_Media_o_Baja()
    {
        var e = await SeedAsync();
        var controlador = Equipos(e, conSesion: true);

        var crear = await controlador.CreateEquipo(new CreateEquipoDto { CodigoInterno = "EQ-2", ActivoFijo = "AF-2", NombreEquipo = "Horno", Criticidad = "Altísima" });
        var editar = await controlador.UpdateEquipo(e.Equipo.IdEquipo, new UpdateEquipoDto { Criticidad = "Crítica", Motivo = Motivo });
        var valida = await controlador.CreateEquipo(new CreateEquipoDto { CodigoInterno = "EQ-3", ActivoFijo = "AF-3", NombreEquipo = "Horno", Criticidad = Criticidades.Baja });

        Assert.IsType<BadRequestObjectResult>(crear.Result);
        Assert.IsType<BadRequestObjectResult>(editar.Result);
        Assert.IsType<CreatedAtActionResult>(valida.Result);
    }

    // ------------------------------------------------------------------ inspecciones (puntos 3 y 8)

    [Fact]
    public async Task La_fecha_de_una_inspeccion_creada_por_el_administrador_es_la_del_servidor()
    {
        var e = await SeedAsync();
        var admin = Fabrica.Inspecciones(e.Db, new Usuario1(e.Tecnico.IdUsuario, "Administrador"), new AlmacenVacio());

        var resultado = await admin.CreateInspeccion(new CreateInspeccionDto
        {
            IdEquipo = e.Equipo.IdEquipo, IdChecklist = e.Checklist.IdChecklist, FechaInspeccion = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc)
        });

        Assert.IsType<CreatedAtActionResult>(resultado.Result);
        var guardada = await e.Db.Inspecciones.AsNoTracking().SingleAsync();
        Assert.True((DateTime.UtcNow - guardada.FechaInspeccion).Duration() < TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task No_se_inspecciona_con_un_checklist_sin_preguntas()
    {
        var e = await SeedAsync();
        var tecnico = Fabrica.Inspecciones(e.Db, new Usuario1(e.Tecnico.IdUsuario, "Operador"), new AlmacenVacio());
        var admin = Fabrica.Inspecciones(e.Db, new Usuario1(e.Tecnico.IdUsuario, "Administrador"), new AlmacenVacio());

        var iniciar = await tecnico.IniciarInspeccion(new IniciarInspeccionDto { IdEquipo = e.Equipo.IdEquipo, IdChecklist = e.Vacio.IdChecklist });
        var crear = await admin.CreateInspeccion(new CreateInspeccionDto { IdEquipo = e.Equipo.IdEquipo, IdChecklist = e.Vacio.IdChecklist });

        Assert.Equal(InspeccionesController.MensajeChecklistSinPreguntas,
            Assert.IsType<ApiResponse<InspeccionEjecucionDto>>(Assert.IsType<BadRequestObjectResult>(iniciar.Result).Value).Message);
        Assert.IsType<BadRequestObjectResult>(crear.Result);
        Assert.False(await e.Db.Inspecciones.AnyAsync());
    }

    // ------------------------------------------------------------------ QR público (punto 7)

    [Fact]
    public async Task Sin_sesion_el_QR_muestra_solo_nombre_codigo_y_ubicacion()
    {
        var e = await SeedAsync();

        var anonimo = (await Equipos(e, conSesion: false).GetByQr(e.Equipo.QRCode!)).Result;
        var conSesion = (await Equipos(e, conSesion: true).GetByQr(e.Equipo.QRCode!)).Result;

        var publico = Assert.IsType<ApiResponse<ConsultaQrDto>>(Assert.IsType<OkObjectResult>(anonimo).Value).Data!.Equipo;
        Assert.Equal("EQ-1", publico.CodigoInterno);
        Assert.Equal("Balanza", publico.NombreEquipo);
        Assert.Equal("Planta 1 - Pesaje", publico.UbicacionNombre);
        Assert.True(publico.IdEquipo > 0);
        Assert.Null(publico.Marca);
        Assert.Null(publico.Modelo);
        Assert.Null(publico.SerialFabricante);
        Assert.Null(publico.Criticidad);
        Assert.Equal(string.Empty, publico.ActivoFijo);

        var completo = Assert.IsType<ApiResponse<ConsultaQrDto>>(Assert.IsType<OkObjectResult>(conSesion).Value).Data!.Equipo;
        Assert.Equal("Mettler", completo.Marca);
        Assert.Equal("AF-1", completo.ActivoFijo);
        Assert.Null(completo.QRCode);
    }

    // ------------------------------------------------------------------ preguntas por DTO (punto 9)

    [Fact]
    public async Task Las_preguntas_validan_el_tipo_y_no_cambian_un_checklist_ya_usado()
    {
        var e = await SeedAsync();
        var controlador = new PreguntasChecklistController(e.Db);
        var inspeccion = new Inspeccion
        {
            IdEquipo = e.Equipo.IdEquipo, IdChecklist = e.Checklist.IdChecklist, IdUsuario = e.Tecnico.IdUsuario,
            FechaInspeccion = DateTime.UtcNow, Estado = InspeccionEstados.EnCurso,
            Respuestas = [new RespuestaInspeccion { IdPregunta = e.Checklist.Preguntas.First().IdPregunta, Respuesta = "Si" }]
        };
        e.Db.Inspecciones.Add(inspeccion);
        await e.Db.SaveChangesAsync();

        var tipoMalo = await controlador.CrearPregunta(new CreatePreguntaChecklistDto { IdChecklist = e.Vacio.IdChecklist, Pregunta = "¿Temperatura?", TipoRespuesta = "Numero" });
        var usado = await controlador.CrearPregunta(new CreatePreguntaChecklistDto { IdChecklist = e.Checklist.IdChecklist, Pregunta = "¿Otra?", TipoRespuesta = TiposRespuesta.SiNo });
        var bien = await controlador.CrearPregunta(new CreatePreguntaChecklistDto { IdChecklist = e.Vacio.IdChecklist, Pregunta = "¿Limpio?", TipoRespuesta = TiposRespuesta.SiNo });

        Assert.IsType<BadRequestObjectResult>(tipoMalo.Result);
        Assert.IsType<ConflictObjectResult>(usado.Result);
        var creada = Assert.IsType<ApiResponse<PreguntaChecklistDto>>(Assert.IsType<OkObjectResult>(bien.Result).Value).Data!;
        Assert.Equal(1, creada.Orden);
    }

    // 29/10/2026: toda pregunta nueva o editada es Sí/No; «Texto» queda solo para leer las que ya existían.
    [Fact]
    public async Task Las_preguntas_nuevas_solo_pueden_ser_Si_No()
    {
        var e = await SeedAsync();

        var porPregunta = await new PreguntasChecklistController(e.Db).CrearPregunta(new CreatePreguntaChecklistDto
        {
            IdChecklist = e.Vacio.IdChecklist, Pregunta = "Lectura del termómetro", TipoRespuesta = TiposRespuesta.Texto
        });
        var porChecklist = await new ChecklistsController(e.Db, new MotivoCambio()).CreateChecklist(new CreateChecklistDto
        {
            Nombre = "Con texto", Version = "1",
            Preguntas = [new CreatePreguntaChecklistDto { Pregunta = "Lectura", TipoRespuesta = TiposRespuesta.Texto }]
        });

        Assert.Equal(TiposRespuesta.MensajeSoloSiNo,
            Assert.IsType<ApiResponse<PreguntaChecklistDto>>(Assert.IsType<BadRequestObjectResult>(porPregunta.Result).Value).Message);
        Assert.Equal(TiposRespuesta.MensajeSoloSiNo,
            Assert.IsType<ApiResponse<ChecklistDto>>(Assert.IsType<BadRequestObjectResult>(porChecklist.Result).Value).Message);
    }

    // ------------------------------------------------------------------ tablero (punto 6)

    [Fact]
    public async Task El_tablero_cuenta_equipos_inspecciones_y_novedades()
    {
        var e = await SeedAsync();
        await NovedadPendienteAsync(e);
        e.Db.Inspecciones.Add(new Inspeccion
        {
            IdEquipo = e.Equipo.IdEquipo, IdChecklist = e.Checklist.IdChecklist, IdUsuario = e.Tecnico.IdUsuario,
            FechaInspeccion = DateTime.UtcNow, Estado = InspeccionEstados.EnCurso
        });
        e.Db.Equipos.Add(new Equipo { CodigoInterno = "EQ-9", ActivoFijo = "AF-9", NombreEquipo = "Sin criticidad", Activo = true });
        await e.Db.SaveChangesAsync();

        var resultado = await new DashboardController(e.Db).GetDashboard();

        var t = Assert.IsType<ApiResponse<DashboardDto>>(Assert.IsType<OkObjectResult>(resultado.Result).Value).Data!;
        Assert.Equal(2, t.TotalEquipos);
        Assert.Equal(1, t.EquiposPorCriticidad.Single(c => c.Etiqueta == Criticidades.Alta).Cantidad);
        Assert.Equal(1, t.EquiposPorCriticidad.Single(c => c.Etiqueta == "Sin criticidad").Cantidad);
        Assert.Contains(t.EquiposPorArea, a => a.Etiqueta == "Planta 1 - Pesaje" && a.Cantidad == 1);
        // Firmada con una novedad pendiente: cuenta como realizada.
        Assert.Equal(1, t.InspeccionesRealizadas);
        Assert.Equal(1, t.InspeccionesRealizadasMes);
        Assert.Equal(1, t.InspeccionesConNovedadesPendientes);
        Assert.Equal(1, t.InspeccionesEnCurso);
        Assert.Equal(1, t.NovedadesPendientes);
        Assert.Equal(0, t.NovedadesResueltas);
        var hoyColombia = HoraColombia.DesdeUtc(DateTime.UtcNow).Date;
        Assert.Equal(new DateTime(hoyColombia.Year, hoyColombia.Month, 1), t.Filtros.Desde);
        Assert.Empty(t.Filtros.Plantas);
    }

    [Fact]
    public async Task El_tablero_aplica_periodo_y_planta_en_hora_de_Colombia()
    {
        var e = await SeedAsync();
        var desde = new DateTime(2026, 10, 1);
        var hasta = new DateTime(2026, 10, 10);
        var (inicio, fin) = HoraColombia.RangoUtc(desde, hasta);
        var otraUbicacion = new Ubicacion { Planta = "Planta 2", Area = "Laboratorio" };
        var otroEquipo = new Equipo
        {
            CodigoInterno = "EQ-2", ActivoFijo = "AF-2", NombreEquipo = "Horno", Activo = true,
            Criticidad = Criticidades.Media, Ubicacion = otraUbicacion
        };
        e.Db.Add(otroEquipo);
        await e.Db.SaveChangesAsync();
        e.Db.Inspecciones.AddRange(
            new Inspeccion
            {
                IdEquipo = e.Equipo.IdEquipo, IdChecklist = e.Checklist.IdChecklist,
                IdUsuario = e.Tecnico.IdUsuario, FechaInspeccion = inicio!.Value,
                Estado = InspeccionEstados.Cerrada
            },
            new Inspeccion
            {
                IdEquipo = otroEquipo.IdEquipo, IdChecklist = e.Checklist.IdChecklist,
                IdUsuario = e.Tecnico.IdUsuario, FechaInspeccion = inicio.Value,
                Estado = InspeccionEstados.Cerrada
            },
            new Inspeccion
            {
                IdEquipo = e.Equipo.IdEquipo, IdChecklist = e.Checklist.IdChecklist,
                IdUsuario = e.Tecnico.IdUsuario, FechaInspeccion = fin!.Value,
                Estado = InspeccionEstados.Cerrada
            });
        await e.Db.SaveChangesAsync();

        var resultado = await new DashboardController(e.Db).GetDashboard(desde, hasta, ["  Planta 1 "]);

        var dto = Assert.IsType<ApiResponse<DashboardDto>>(
            Assert.IsType<OkObjectResult>(resultado.Result).Value).Data!;
        Assert.Equal(desde, dto.Filtros.Desde);
        Assert.Equal(hasta, dto.Filtros.Hasta);
        Assert.Equal(["Planta 1"], dto.Filtros.Plantas);
        Assert.Equal(1, dto.TotalEquipos);
        Assert.Equal(1, dto.InspeccionesRealizadasMes);
    }

    [Fact]
    public async Task El_tablero_rechaza_un_rango_mayor_de_un_anio()
    {
        var e = await SeedAsync();

        var resultado = await new DashboardController(e.Db).GetDashboard(
            new DateTime(2025, 1, 1), new DateTime(2026, 1, 2));

        var error = Assert.IsType<ApiResponse<DashboardDto>>(
            Assert.IsType<BadRequestObjectResult>(resultado.Result).Value);
        Assert.False(error.Success);
        Assert.Contains("un año", error.Message);
    }

    [Fact]
    public async Task La_tendencia_incluye_semanas_vacias_y_datos_a_ambos_lados_del_cambio_de_mes()
    {
        var e = await SeedAsync();
        var hoyColombia = HoraColombia.DesdeUtc(DateTime.UtcNow).Date;
        var primerDiaMes = hoyColombia.AddDays(1 - hoyColombia.Day);
        var antes = primerDiaMes.AddDays(-1).AddHours(10) - HoraColombia.Desfase;
        var despues = primerDiaMes.AddHours(10) - HoraColombia.Desfase;
        var inspeccionAntes = new Inspeccion
        {
            IdEquipo = e.Equipo.IdEquipo, IdChecklist = e.Checklist.IdChecklist,
            IdUsuario = e.Tecnico.IdUsuario, FechaInspeccion = antes, FechaCierre = antes,
            Estado = InspeccionEstados.Cerrada
        };
        var inspeccionDespues = new Inspeccion
        {
            IdEquipo = e.Equipo.IdEquipo, IdChecklist = e.Checklist.IdChecklist,
            IdUsuario = e.Tecnico.IdUsuario, FechaInspeccion = despues, FechaCierre = despues,
            Estado = InspeccionEstados.Cerrada
        };
        e.Db.Inspecciones.AddRange(inspeccionAntes, inspeccionDespues);
        await e.Db.SaveChangesAsync();
        var respuesta = new RespuestaInspeccion
        {
            IdInspeccion = inspeccionAntes.IdInspeccion,
            IdPregunta = e.Checklist.Preguntas.Single().IdPregunta,
            Respuesta = "No"
        };
        e.Db.RespuestasInspeccion.Add(respuesta);
        await e.Db.SaveChangesAsync();
        e.Db.Novedades.Add(new Novedad
        {
            IdInspeccion = inspeccionAntes.IdInspeccion,
            IdPregunta = respuesta.IdPregunta,
            IdRespuesta = respuesta.IdRespuesta,
            IdUsuarioRegistro = e.Tecnico.IdUsuario,
            Descripcion = "Novedad en cambio de mes",
            FechaRegistro = antes,
            FechaResolucion = despues,
            Estado = NovedadEstados.Resuelta
        });
        await e.Db.SaveChangesAsync();

        var resultado = await new DashboardController(e.Db).GetTendencia(8, ["Planta 1"]);

        var dto = Assert.IsType<ApiResponse<DashboardTendenciaDto>>(
            Assert.IsType<OkObjectResult>(resultado.Result).Value).Data!;
        Assert.Equal(8, dto.Datos.Count);
        Assert.Equal(2, dto.Datos.Sum(s => s.InspeccionesFirmadas));
        Assert.Equal(1, dto.Datos.Sum(s => s.NovedadesRegistradas));
        Assert.Equal(1, dto.Datos.Sum(s => s.NovedadesResueltas));
        Assert.Contains(dto.Datos, s => s.InspeccionesFirmadas == 0 &&
                                        s.NovedadesRegistradas == 0 && s.NovedadesResueltas == 0);
        Assert.All(dto.Datos, s => Assert.Equal(DayOfWeek.Monday, s.Desde.DayOfWeek));
    }

    // ------------------------------------------------------------------ tablero: periodo, total general (30/10/2026)

    /// <summary>Una inspección firmada a esa hora de Colombia (inicio y cierre).</summary>
    private static Inspeccion Firmada(Escenario e, DateTime horaColombia, long? idEquipo = null)
    {
        var utc = horaColombia - HoraColombia.Desfase;
        return new Inspeccion
        {
            IdEquipo = idEquipo ?? e.Equipo.IdEquipo, IdChecklist = e.Checklist.IdChecklist, IdUsuario = e.Tecnico.IdUsuario,
            FechaInspeccion = utc, FechaCierre = utc, Estado = InspeccionEstados.Cerrada,
            Resultado = InspeccionResultados.Satisfactorio
        };
    }

    private static T Ok<T>(ActionResult<ApiResponse<T>> resultado) =>
        Assert.IsType<ApiResponse<T>>(Assert.IsType<OkObjectResult>(resultado.Result).Value).Data!;

    [Fact]
    public async Task El_total_general_cuenta_todo_el_historial_aunque_pase_de_un_anio_y_la_tendencia_va_por_mes()
    {
        var e = await SeedAsync();
        var hoy = HoraColombia.DesdeUtc(DateTime.UtcNow).Date;
        e.Db.Inspecciones.AddRange(Firmada(e, new DateTime(2024, 3, 10, 9, 0, 0)), Firmada(e, hoy.AddHours(8)));
        await e.Db.SaveChangesAsync();
        var controlador = new DashboardController(e.Db);

        // Las fechas no cuentan en el total general: ni el límite de un año.
        var tablero = Ok(await controlador.GetDashboard(new DateTime(2026, 1, 1), new DateTime(2026, 1, 2), total: true));
        var tendencia = Ok(await controlador.GetTendencia(total: true));

        Assert.True(tablero.Filtros.Total);
        Assert.Equal(new DateTime(2024, 3, 1), tablero.Filtros.Desde);
        Assert.Equal(hoy, tablero.Filtros.Hasta);
        Assert.Equal(2, tablero.InspeccionesRealizadasMes);
        Assert.StartsWith("Total general (del 01/03/2024 al ", tablero.Periodo);
        Assert.StartsWith("total general, desde 01/03/2024, ", DashboardController.TextoFiltros(tablero.Filtros));

        Assert.Equal(TendenciaAgrupaciones.Mes, tendencia.Agrupacion);
        Assert.True(tendencia.Total);
        Assert.Equal((hoy.Year - 2024) * 12 + hoy.Month - 3 + 1, tendencia.Datos.Count);
        Assert.Equal(tendencia.Datos.Count, tendencia.Semanas);
        Assert.Equal((new DateTime(2024, 3, 1), new DateTime(2024, 3, 31)), (tendencia.Datos[0].Desde, tendencia.Datos[0].Hasta));
        Assert.Equal(1, tendencia.Datos[0].InspeccionesFirmadas);
        Assert.Equal(hoy, tendencia.Datos[^1].Hasta);
        Assert.Equal(1, tendencia.Datos[^1].InspeccionesFirmadas);
    }

    [Fact]
    public async Task El_total_general_sin_inspecciones_es_el_mes_enCurso()
    {
        var e = await SeedAsync();
        var hoy = HoraColombia.DesdeUtc(DateTime.UtcNow).Date;

        var tablero = Ok(await new DashboardController(e.Db).GetDashboard(total: true));

        Assert.Equal(new DateTime(hoy.Year, hoy.Month, 1), tablero.Filtros.Desde);
        Assert.Equal(0, tablero.InspeccionesRealizadasMes);
    }

    [Fact]
    public async Task La_tendencia_de_un_mes_va_por_semanas_recortadas_al_mes()
    {
        var e = await SeedAsync();
        e.Db.Inspecciones.AddRange(
            Firmada(e, new DateTime(2026, 9, 15, 10, 0, 0)),
            // 30/09 a las 23:00 en Colombia ya es 1/10 en UTC: cuenta en septiembre.
            Firmada(e, new DateTime(2026, 9, 30, 23, 0, 0)),
            Firmada(e, new DateTime(2026, 10, 1, 8, 0, 0)));
        await e.Db.SaveChangesAsync();

        var tendencia = Ok(await new DashboardController(e.Db).GetTendencia(
            desde: new DateTime(2026, 9, 1), hasta: new DateTime(2026, 9, 30)));

        Assert.Equal(TendenciaAgrupaciones.Semana, tendencia.Agrupacion);
        Assert.Equal(new[] { 1, 7, 14, 21, 28 }, tendencia.Datos.Select(s => s.Desde.Day));
        Assert.Equal(new[] { 6, 13, 20, 27, 30 }, tendencia.Datos.Select(s => s.Hasta.Day));
        Assert.Equal(new[] { 0, 0, 1, 0, 1 }, tendencia.Datos.Select(s => s.InspeccionesFirmadas));
    }

    [Fact]
    public void Hasta_92_dias_la_tendencia_va_por_semana_y_despues_por_mes()
    {
        var meses = DashboardController.Tramos(new DateTime(2026, 1, 15), new DateTime(2026, 6, 10), porMes: true);

        Assert.Equal(6, meses.Count);
        Assert.Equal((new DateTime(2026, 1, 15), new DateTime(2026, 1, 31)), meses[0]);
        Assert.Equal((new DateTime(2026, 2, 1), new DateTime(2026, 2, 28)), meses[1]);
        Assert.Equal((new DateTime(2026, 6, 1), new DateTime(2026, 6, 10)), meses[^1]);
        // Un trimestre (del 1/07 al 30/09 son 92 días) todavía va por semana.
        Assert.Equal(92, DashboardController.DiasMaximosPorSemana);
    }

    [Fact]
    public async Task La_tendencia_elige_semana_o_mes_segun_el_largo_del_rango()
    {
        var e = await SeedAsync();
        var controlador = new DashboardController(e.Db);

        var trimestre = Ok(await controlador.GetTendencia(desde: new DateTime(2026, 7, 1), hasta: new DateTime(2026, 9, 30)));
        var cuatroMeses = Ok(await controlador.GetTendencia(desde: new DateTime(2026, 6, 1), hasta: new DateTime(2026, 9, 30)));

        Assert.Equal(TendenciaAgrupaciones.Semana, trimestre.Agrupacion);
        Assert.Equal(TendenciaAgrupaciones.Mes, cuatroMeses.Agrupacion);
        Assert.Equal(new[] { 6, 7, 8, 9 }, cuatroMeses.Datos.Select(s => s.Desde.Month));
    }

    [Fact]
    public async Task La_tendencia_de_un_rango_rechaza_mas_de_un_anio_como_el_tablero()
    {
        var e = await SeedAsync();

        var resultado = await new DashboardController(e.Db).GetTendencia(
            desde: new DateTime(2025, 1, 1), hasta: new DateTime(2026, 1, 2));

        var error = Assert.IsType<ApiResponse<DashboardTendenciaDto>>(
            Assert.IsType<BadRequestObjectResult>(resultado.Result).Value);
        Assert.Contains("un año", error.Message);
    }

    [Fact]
    public async Task El_tablero_cuenta_los_equipos_inspeccionados_en_el_periodo()
    {
        var e = await SeedAsync();
        var otro = new Equipo
        {
            CodigoInterno = "EQ-2", ActivoFijo = "AF-2", NombreEquipo = "Horno", Activo = true,
            Criticidad = Criticidades.Media, IdUbicacion = e.Equipo.IdUbicacion
        };
        e.Db.Equipos.Add(otro);
        await e.Db.SaveChangesAsync();
        var enCurso = Firmada(e, new DateTime(2026, 9, 10, 9, 0, 0), otro.IdEquipo);
        enCurso.Estado = InspeccionEstados.EnCurso;
        enCurso.FechaCierre = null;
        e.Db.Inspecciones.AddRange(
            Firmada(e, new DateTime(2026, 9, 5, 9, 0, 0)),
            Firmada(e, new DateTime(2026, 9, 20, 9, 0, 0)),
            Firmada(e, new DateTime(2026, 8, 20, 9, 0, 0), otro.IdEquipo),
            enCurso);
        await e.Db.SaveChangesAsync();

        var septiembre = Ok(await new DashboardController(e.Db).GetDashboard(new DateTime(2026, 9, 1), new DateTime(2026, 9, 30)));

        Assert.Equal(2, septiembre.TotalEquipos);
        // El EQ-1 dos veces cuenta una; el horno solo tiene una en curso en septiembre y la firmada es de agosto.
        Assert.Equal(1, septiembre.EquiposInspeccionados);
        Assert.False(septiembre.Filtros.Total);
    }

    private sealed class AlmacenVacio : IEvidenciaStorage
    {
        public Task<string> GuardarAsync(Stream stream, string extension, long idInspeccion) => Task.FromResult("f.png");
        public Task<Stream> AbrirAsync(string rutaRelativa) => Task.FromResult<Stream>(new MemoryStream());
        public Task EliminarAsync(string rutaRelativa) => Task.CompletedTask;
    }
}
