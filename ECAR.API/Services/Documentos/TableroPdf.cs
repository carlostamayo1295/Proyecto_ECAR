using ECAR.Shared.DTOs;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;

namespace ECAR.API.Services.Documentos;

/// <summary>
/// El tablero de la portada en A4 vertical (B-03): los indicadores, los equipos por criticidad y
/// por área, con los filtros de B-01 (periodo y planta) y el pie común de §3.9. Sale del mismo
/// DashboardDto que la pantalla, así que los números no pueden discrepar.
/// </summary>
public static class TableroPdf
{
    public const string Titulo = "Tablero de indicadores";

    /// <summary>Tamaño de letra normal del documento (ExportadorTabla.Documento).</summary>
    private const double TamanoPt = 9;

    public static byte[] Generar(DashboardDto tablero, string filtros, string generadoPor, DateTime generadoUtc) =>
        ExportadorTabla.Renderizar(Crear(tablero, filtros, generadoPor, generadoUtc));

    /// <summary>El documento sin renderizar, para revisar su contenido en las pruebas.</summary>
    internal static Document Crear(DashboardDto tablero, string filtros, string generadoPor, DateTime generadoUtc)
    {
        var pie = new PieDocumento(Titulo, filtros, generadoPor, generadoUtc);
        var documento = ExportadorTabla.Documento(Titulo, pie, horizontal: false);
        var seccion = documento.LastSection;

        var encabezado = seccion.AddParagraph();
        encabezado.AddFormattedText("Filtros: ", TextFormat.Bold);
        encabezado.AddText(filtros);
        var periodo = seccion.AddParagraph();
        periodo.AddFormattedText("Periodo: ", TextFormat.Bold);
        periodo.AddText(tablero.Periodo);

        // Las etiquetas dicen qué cuenta cada número con los filtros de B-01: el inventario y el total
        // de inspecciones no dependen del periodo; el resto, sí.
        Subtitulo(seccion, "Indicadores");
        Tabla(seccion, "Indicador", "Valor",
        [
            new() { Etiqueta = "Equipos activos", Cantidad = tablero.TotalEquipos },
            new() { Etiqueta = "Inspecciones realizadas desde el inicio", Cantidad = tablero.InspeccionesRealizadas },
            new() { Etiqueta = "Inspecciones realizadas en el periodo", Cantidad = tablero.InspeccionesRealizadasMes },
            new() { Etiqueta = "Inspecciones en curso (iniciadas en el periodo)", Cantidad = tablero.InspeccionesEnCurso },
            new() { Etiqueta = "Inspecciones del periodo con novedades pendientes", Cantidad = tablero.InspeccionesConNovedadesPendientes },
            new() { Etiqueta = "Novedades pendientes (registradas en el periodo)", Cantidad = tablero.NovedadesPendientes },
            new() { Etiqueta = "Novedades resueltas en el periodo", Cantidad = tablero.NovedadesResueltas }
        ], "Sin indicadores.");

        Subtitulo(seccion, "Equipos por criticidad");
        Tabla(seccion, "Criticidad", "Equipos", tablero.EquiposPorCriticidad, "No hay equipos para los filtros indicados.");

        Subtitulo(seccion, "Equipos por área");
        Tabla(seccion, "Planta - Área", "Equipos", tablero.EquiposPorArea, "No hay equipos para los filtros indicados.");

        return documento;
    }

    private static void Subtitulo(Section seccion, string texto)
    {
        var titulo = seccion.AddParagraph(texto);
        titulo.Format.Font.Size = 11;
        titulo.Format.Font.Bold = true;
        titulo.Format.SpaceBefore = Unit.FromPoint(10);
        titulo.Format.SpaceAfter = Unit.FromPoint(4);
    }

    private static void Tabla(Section seccion, string etiqueta, string valor, IReadOnlyList<ConteoDto> filas, string vacia)
    {
        // Anchos según el contenido y palabras largas partidas (AjusteTexto): un nombre de área muy largo
        // ya no se sale de la celda.
        var textos = filas.Select(f => (IReadOnlyList<string>)[f.Etiqueta, ExportadorTabla.Texto(f.Cantidad)]).ToList();
        using var ajuste = new AjusteTexto(TamanoPt);
        var anchos = ajuste.Anchos([etiqueta, valor], textos, ExportadorTabla.AnchoUtilCm(seccion.PageSetup));
        var tabla = seccion.AddTable();
        AjusteTexto.Preparar(tabla);
        tabla.Borders.Width = 0.4;
        tabla.Borders.Color = Colors.Gray;
        tabla.AddColumn(Unit.FromCentimeter(anchos[0]));
        tabla.AddColumn(Unit.FromCentimeter(anchos[1])).Format.Alignment = ParagraphAlignment.Right;

        var encabezado = tabla.AddRow();
        encabezado.HeadingFormat = true;
        encabezado.Shading.Color = Color.FromRgb(0xDC, 0xE6, 0xF2);
        encabezado.Format.Font.Bold = true;
        ajuste.Escribir(encabezado.Cells[0].AddParagraph(), etiqueta, anchos[0], negrita: true);
        ajuste.Escribir(encabezado.Cells[1].AddParagraph(), valor, anchos[1], negrita: true);

        foreach (var texto in textos)
        {
            var fila = tabla.AddRow();
            ajuste.Escribir(fila.Cells[0].AddParagraph(), texto[0], anchos[0]);
            ajuste.Escribir(fila.Cells[1].AddParagraph(), texto[1], anchos[1]);
        }

        if (filas.Count == 0)
        {
            var fila = tabla.AddRow();
            fila.Cells[0].MergeRight = 1;
            fila.Cells[0].AddParagraph(vacia);
        }
    }
}
