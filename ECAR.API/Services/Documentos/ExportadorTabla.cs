using System.Globalization;
using System.Reflection;
using ClosedXML.Excel;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using PdfSharp.Fonts;

namespace ECAR.API.Services.Documentos;

/// <summary>Una columna del documento; el ancho solo se usa en el PDF (horizontal, 25,7 cm útiles).</summary>
public sealed record Columna(string Titulo, double AnchoCm);

/// <summary>
/// Pie de cada documento (PLAN_FASE4_TAREAS §3.9): reporte, filtros, quién lo generó, fecha y hora
/// con zona, página X de Y y versión del sistema.
/// </summary>
public sealed record PieDocumento(string Reporte, string Filtros, string GeneradoPor, DateTime GeneradoUtc)
{
    public static string Version { get; } =
        "ECAR v" + (Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0");

    public string Texto =>
        $"{Reporte} · Filtros: {Filtros} · Generado por {GeneradoPor} el {HoraColombia.Texto(GeneradoUtc)} " +
        $"({HoraColombia.Zona}) · {Version}";
}

/// <summary>
/// Tablas a Excel (ClosedXML, MIT) y PDF (PDFsharp-MigraDoc, MIT), en el servidor. Los valores
/// DateTime llegan ya en hora de Colombia. El mismo contenido sale en la vista previa, en el PDF y
/// en el Excel, así que no pueden discrepar.
/// </summary>
public static class ExportadorTabla
{
    /// <summary>Un archivo lleva todo el rango; con más filas se pide acotar las fechas (§3.9).</summary>
    public const int MaximoFilas = 10_000;

    public const string TipoPdf = "application/pdf";
    public const string TipoExcel = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public static byte[] Excel(string titulo, IReadOnlyList<Columna> columnas,
        IEnumerable<IReadOnlyList<object?>> filas, PieDocumento pie,
        IReadOnlyList<(string Etiqueta, string Valor)>? resumen = null)
    {
        using var libro = new XLWorkbook();
        var hoja = libro.AddWorksheet(NombreHoja(titulo));

        hoja.Cell(1, 1).Value = titulo;
        hoja.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(14);
        hoja.Cell(2, 1).Value = "Filtros: " + pie.Filtros;
        hoja.Cell(3, 1).Value =
            $"Generado por {pie.GeneradoPor} el {HoraColombia.Texto(pie.GeneradoUtc)} ({HoraColombia.Zona}) · {PieDocumento.Version}";

        var fila = 5;
        foreach (var (etiqueta, valor) in resumen ?? [])
        {
            hoja.Cell(fila, 1).Value = etiqueta;
            hoja.Cell(fila, 1).Style.Font.SetBold();
            hoja.Cell(fila, 2).Value = valor;
            fila++;
        }

        if (resumen is { Count: > 0 })
        {
            fila++;
        }

        var filaEncabezado = fila;
        for (var c = 0; c < columnas.Count; c++)
        {
            var celda = hoja.Cell(filaEncabezado, c + 1);
            celda.Value = columnas[c].Titulo;
            celda.Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#DCE6F2"));
        }

        foreach (var valores in filas)
        {
            fila++;
            for (var c = 0; c < valores.Count; c++)
            {
                var celda = hoja.Cell(fila, c + 1);
                switch (valores[c])
                {
                    case null:
                        break;
                    case DateTime fecha:
                        celda.Value = fecha;
                        celda.Style.DateFormat.Format = fecha.TimeOfDay == TimeSpan.Zero ? "dd/mm/yyyy" : "dd/mm/yyyy hh:mm";
                        break;
                    case int or long or decimal or double:
                        celda.Value = Convert.ToDouble(valores[c], CultureInfo.InvariantCulture);
                        break;
                    case bool b:
                        celda.Value = b ? "Sí" : "No";
                        break;
                    default:
                        celda.Value = Convert.ToString(valores[c], CultureInfo.InvariantCulture);
                        break;
                }
            }
        }

        if (fila > filaEncabezado)
        {
            hoja.Range(filaEncabezado, 1, fila, columnas.Count).SetAutoFilter();
        }

        hoja.SheetView.FreezeRows(filaEncabezado);
        hoja.Columns(1, columnas.Count).AdjustToContents(filaEncabezado, Math.Min(fila, filaEncabezado + 500));
        foreach (var columna in hoja.Columns(1, columnas.Count))
        {
            columna.Width = Math.Min(columna.Width, 60);
        }

        hoja.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        hoja.PageSetup.Footer.Left.AddText(pie.Texto, XLHFOccurrence.AllPages);
        hoja.PageSetup.Footer.Right.AddText("Página ", XLHFOccurrence.AllPages);
        hoja.PageSetup.Footer.Right.AddText(XLHFPredefinedText.PageNumber, XLHFOccurrence.AllPages);
        hoja.PageSetup.Footer.Right.AddText(" de ", XLHFOccurrence.AllPages);
        hoja.PageSetup.Footer.Right.AddText(XLHFPredefinedText.NumberOfPages, XLHFOccurrence.AllPages);

        using var salida = new MemoryStream();
        libro.SaveAs(salida);
        return salida.ToArray();
    }

    public static byte[] Pdf(string titulo, IReadOnlyList<Columna> columnas,
        IEnumerable<IReadOnlyList<object?>> filas, PieDocumento pie,
        IReadOnlyList<(string Etiqueta, string Valor)>? resumen = null)
    {
        var documento = Documento(titulo, pie, horizontal: true);
        var seccion = documento.LastSection;

        foreach (var (etiqueta, valor) in resumen ?? [])
        {
            var p = seccion.AddParagraph();
            p.AddFormattedText(etiqueta + ": ", TextFormat.Bold);
            p.AddText(valor);
        }

        if (resumen is { Count: > 0 })
        {
            seccion.AddParagraph().Format.SpaceAfter = Unit.FromPoint(4);
        }

        var tabla = seccion.AddTable();
        tabla.Borders.Width = 0.4;
        tabla.Borders.Color = Colors.Gray;
        tabla.Format.Font.Size = 7.5;
        foreach (var columna in columnas)
        {
            tabla.AddColumn(Unit.FromCentimeter(columna.AnchoCm));
        }

        var encabezado = tabla.AddRow();
        encabezado.HeadingFormat = true;
        encabezado.Shading.Color = Color.FromRgb(0xDC, 0xE6, 0xF2);
        encabezado.Format.Font.Bold = true;
        for (var c = 0; c < columnas.Count; c++)
        {
            encabezado.Cells[c].AddParagraph(columnas[c].Titulo);
        }

        var hayFilas = false;
        foreach (var valores in filas)
        {
            hayFilas = true;
            var fila = tabla.AddRow();
            for (var c = 0; c < valores.Count; c++)
            {
                fila.Cells[c].AddParagraph(Texto(valores[c]));
            }
        }

        if (!hayFilas)
        {
            var vacia = tabla.AddRow();
            vacia.Cells[0].MergeRight = columnas.Count - 1;
            vacia.Cells[0].AddParagraph("No hay registros para los filtros indicados.");
        }

        return Renderizar(documento);
    }

    /// <summary>Documento con el estilo común y el pie de §3.9, con una sección vacía lista.</summary>
    internal static Document Documento(string titulo, PieDocumento pie, bool horizontal)
    {
        ConfigurarFuentes();
        var documento = new Document();
        documento.Info.Title = titulo;
        documento.Info.Author = pie.GeneradoPor;

        var normal = documento.Styles[StyleNames.Normal]!;
        normal.Font.Name = "Arial";
        normal.Font.Size = 9;

        var seccion = documento.AddSection();
        seccion.PageSetup = documento.DefaultPageSetup.Clone();
        seccion.PageSetup.PageFormat = PageFormat.A4;
        seccion.PageSetup.Orientation = horizontal ? Orientation.Landscape : Orientation.Portrait;
        seccion.PageSetup.TopMargin = Unit.FromCentimeter(1.5);
        seccion.PageSetup.BottomMargin = Unit.FromCentimeter(2);
        seccion.PageSetup.LeftMargin = Unit.FromCentimeter(2);
        seccion.PageSetup.RightMargin = Unit.FromCentimeter(2);

        var piePagina = seccion.Footers.Primary.AddParagraph();
        piePagina.Format.Font.Size = 7;
        piePagina.Format.Font.Color = Colors.DimGray;
        piePagina.AddText(pie.Texto + " · Página ");
        piePagina.AddPageField();
        piePagina.AddText(" de ");
        piePagina.AddNumPagesField();

        var encabezado = seccion.AddParagraph(titulo);
        encabezado.Format.Font.Size = 14;
        encabezado.Format.Font.Bold = true;
        encabezado.Format.SpaceAfter = Unit.FromPoint(6);

        return documento;
    }

    internal static byte[] Renderizar(Document documento)
    {
        var renderer = new PdfDocumentRenderer { Document = documento };
        renderer.RenderDocument();
        using var salida = new MemoryStream();
        renderer.PdfDocument.Save(salida, false);
        return salida.ToArray();
    }

    internal static string Texto(object? valor) => valor switch
    {
        null => "—",
        DateTime fecha => fecha.ToString(fecha.TimeOfDay == TimeSpan.Zero ? "dd/MM/yyyy" : "dd/MM/yyyy HH:mm",
            CultureInfo.InvariantCulture),
        decimal d => d.ToString("0.#", CultureInfo.InvariantCulture),
        double d => d.ToString("0.#", CultureInfo.InvariantCulture),
        bool b => b ? "Sí" : "No",
        _ => Convert.ToString(valor, CultureInfo.InvariantCulture) ?? string.Empty
    };

    private static readonly object CandadoFuentes = new();
    private static bool _fuentesConfiguradas;

    /// <summary>PDFsharp 6 necesita saber de dónde sacar las fuentes; el servidor de ECAR es Windows.</summary>
    private static void ConfigurarFuentes()
    {
        lock (CandadoFuentes)
        {
            if (_fuentesConfiguradas)
            {
                return;
            }

            if (OperatingSystem.IsWindows())
            {
                GlobalFontSettings.UseWindowsFontsUnderWindows = true;
            }

            _fuentesConfiguradas = true;
        }
    }

    private static string NombreHoja(string titulo)
    {
        var limpio = new string(titulo.Where(c => !"[]:*?/\\".Contains(c)).ToArray()).Trim();
        return limpio.Length > 31 ? limpio[..31] : limpio.Length == 0 ? "Reporte" : limpio;
    }
}
