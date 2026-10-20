using ECAR.Infrastructure.Entities;
using ECAR.Shared;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;

namespace ECAR.API.Services.Documentos;

/// <summary>
/// Copia legible y completa de una inspección cerrada (endpoint 14, Parte 11 §11.10(b) y §11.50):
/// cabecera, respuestas con las novedades resaltadas, miniaturas de las evidencias vigentes, la
/// firma con su manifestación (nombre, fecha y hora con zona, significado), el algoritmo y la huella.
/// </summary>
public static class RegistroInspeccionPdf
{
    /// <param name="inspeccion">Con Equipo.Ubicacion, Usuario, Checklist.Preguntas, Respuestas y Evidencias.</param>
    /// <param name="imagenes">Bytes de cada evidencia vigente por IdEvidencia; las que no se pudieron leer, ausentes.</param>
    public static byte[] Generar(Inspeccion inspeccion, IReadOnlyDictionary<long, byte[]> imagenes,
        string generadoPor, DateTime generadoUtc)
    {
        var codigo = $"ECAR-INSP-{inspeccion.IdInspeccion}";
        var pie = new PieDocumento($"Copia electrónica del registro {codigo}", $"Inspección {inspeccion.IdInspeccion}",
            generadoPor, generadoUtc);
        var documento = ExportadorTabla.Documento($"Registro de inspección {codigo}", pie, horizontal: false);
        var seccion = documento.LastSection;

        Cabecera(seccion, inspeccion);
        Respuestas(seccion, inspeccion);

        if (!string.IsNullOrWhiteSpace(inspeccion.Observaciones))
        {
            Titulo(seccion, "Observaciones generales");
            seccion.AddParagraph(inspeccion.Observaciones);
        }

        Evidencias(seccion, inspeccion, imagenes);
        Firma(seccion, inspeccion);

        return ExportadorTabla.Renderizar(documento);
    }

    private static void Cabecera(Section seccion, Inspeccion inspeccion)
    {
        var equipo = inspeccion.Equipo;
        var filas = new (string, string)[]
        {
            ("Equipo", $"{equipo.CodigoInterno} — {equipo.NombreEquipo}"),
            ("Activo fijo", equipo.ActivoFijo),
            ("Ubicación", equipo.Ubicacion == null ? "—" : $"{equipo.Ubicacion.Planta} - {equipo.Ubicacion.Area}"),
            ("Checklist", $"{inspeccion.Checklist.Nombre} (versión {inspeccion.Checklist.Version})"),
            ("Inspector", inspeccion.Usuario.Nombre),
            ("Inicio", $"{HoraColombia.Texto(inspeccion.FechaInspeccion)} ({HoraColombia.Zona})"),
            ("Cierre", inspeccion.FechaCierre is { } cierre ? $"{HoraColombia.Texto(cierre)} ({HoraColombia.Zona})" : "—"),
            ("Estado", inspeccion.Estado),
            ("Resultado", inspeccion.Resultado == InspeccionResultados.ConNovedad ? "Con novedad" : inspeccion.Resultado ?? "—")
        };

        var tabla = seccion.AddTable();
        tabla.Borders.Width = 0.4;
        tabla.Borders.Color = Colors.Gray;
        tabla.AddColumn(Unit.FromCentimeter(4));
        tabla.AddColumn(Unit.FromCentimeter(13));
        foreach (var (etiqueta, valor) in filas)
        {
            var fila = tabla.AddRow();
            fila.Cells[0].Shading.Color = Color.FromRgb(0xF2, 0xF2, 0xF2);
            fila.Cells[0].AddParagraph().AddFormattedText(etiqueta, TextFormat.Bold);
            fila.Cells[1].AddParagraph(valor);
        }
    }

    private static void Respuestas(Section seccion, Inspeccion inspeccion)
    {
        Titulo(seccion, "Respuestas");
        var respuestas = inspeccion.Respuestas.ToDictionary(r => r.IdPregunta);
        var tabla = seccion.AddTable();
        tabla.Borders.Width = 0.4;
        tabla.Borders.Color = Colors.Gray;
        tabla.Format.Font.Size = 8.5;
        tabla.AddColumn(Unit.FromCentimeter(0.8));
        tabla.AddColumn(Unit.FromCentimeter(7.7));
        tabla.AddColumn(Unit.FromCentimeter(2.5));
        tabla.AddColumn(Unit.FromCentimeter(6));

        var encabezado = tabla.AddRow();
        encabezado.HeadingFormat = true;
        encabezado.Format.Font.Bold = true;
        encabezado.Shading.Color = Color.FromRgb(0xDC, 0xE6, 0xF2);
        encabezado.Cells[0].AddParagraph("#");
        encabezado.Cells[1].AddParagraph("Pregunta");
        encabezado.Cells[2].AddParagraph("Respuesta");
        encabezado.Cells[3].AddParagraph("Observación");

        var numero = 0;
        foreach (var pregunta in inspeccion.Checklist.Preguntas.OrderBy(p => p.Orden).ThenBy(p => p.IdPregunta))
        {
            respuestas.TryGetValue(pregunta.IdPregunta, out var respuesta);
            var esNovedad = pregunta.TipoRespuesta == TiposRespuesta.SiNo &&
                            string.Equals(respuesta?.Respuesta, "No", StringComparison.OrdinalIgnoreCase);
            var fila = tabla.AddRow();
            if (esNovedad)
            {
                fila.Shading.Color = Color.FromRgb(0xFD, 0xE2, 0xE1);
            }

            fila.Cells[0].AddParagraph((++numero).ToString());
            fila.Cells[1].AddParagraph(pregunta.Pregunta + (pregunta.Obligatoria ? " *" : string.Empty));
            var valor = fila.Cells[2].AddParagraph();
            valor.AddFormattedText(Valor(respuesta?.Respuesta, pregunta.TipoRespuesta) + (esNovedad ? " (novedad)" : string.Empty),
                esNovedad ? TextFormat.Bold : TextFormat.NotBold);
            fila.Cells[3].AddParagraph(string.IsNullOrWhiteSpace(respuesta?.Observacion) ? "—" : respuesta!.Observacion);
        }

        var nota = seccion.AddParagraph("* Pregunta obligatoria.");
        nota.Format.Font.Size = 7;
        nota.Format.Font.Color = Colors.DimGray;
    }

    private static void Evidencias(Section seccion, Inspeccion inspeccion, IReadOnlyDictionary<long, byte[]> imagenes)
    {
        var vigentes = inspeccion.Evidencias.Where(e => !e.Retirada).OrderBy(e => e.FechaCarga).ToList();
        Titulo(seccion, $"Evidencias ({vigentes.Count})");
        if (vigentes.Count == 0)
        {
            seccion.AddParagraph("La inspección no tiene evidencias fotográficas.");
            return;
        }

        const int porFila = 3;
        var tabla = seccion.AddTable();
        for (var c = 0; c < porFila; c++)
        {
            tabla.AddColumn(Unit.FromCentimeter(5.6));
        }

        Row? fila = null;
        for (var i = 0; i < vigentes.Count; i++)
        {
            if (i % porFila == 0)
            {
                fila = tabla.AddRow();
            }

            var evidencia = vigentes[i];
            var celda = fila!.Cells[i % porFila];
            if (imagenes.TryGetValue(evidencia.IdEvidencia, out var bytes))
            {
                var imagen = celda.AddParagraph().AddImage("base64:" + Convert.ToBase64String(bytes));
                imagen.Width = Unit.FromCentimeter(5);
                imagen.LockAspectRatio = true;
            }
            else
            {
                celda.AddParagraph("(imagen no disponible)");
            }

            var pie = celda.AddParagraph($"{evidencia.NombreOriginal} · {HoraColombia.Texto(evidencia.FechaCarga)}");
            pie.Format.Font.Size = 7;
            pie.Format.SpaceAfter = Unit.FromPoint(6);
        }
    }

    private static void Firma(Section seccion, Inspeccion inspeccion)
    {
        Titulo(seccion, "Firma electrónica");
        if (string.IsNullOrEmpty(inspeccion.FirmaDigital) || inspeccion.FechaCierre is not { } cierre)
        {
            seccion.AddParagraph("La inspección no está firmada.");
            return;
        }

        var imagen = seccion.AddParagraph().AddImage("base64:" + inspeccion.FirmaDigital);
        imagen.Width = Unit.FromCentimeter(6);
        imagen.LockAspectRatio = true;

        var manifestacion = seccion.AddParagraph();
        manifestacion.Format.SpaceBefore = Unit.FromPoint(4);
        if (!string.IsNullOrWhiteSpace(inspeccion.FirmaNombre))
        {
            manifestacion.AddText($"Firmado electrónicamente por {inspeccion.FirmaNombre} el {HoraColombia.Texto(cierre)} " +
                                  $"({HoraColombia.Zona}). Significado: {inspeccion.FirmaSignificado}.");
        }
        else
        {
            manifestacion.AddText($"Firmado por {inspeccion.Usuario.Nombre} el {HoraColombia.Texto(cierre)} " +
                                  $"({HoraColombia.Zona}). Firma anterior a la Fase 4: no registra significado.");
        }

        var huella = seccion.AddParagraph();
        huella.Format.Font.Size = 7.5;
        huella.Format.Font.Color = Colors.DimGray;
        huella.AddText($"Algoritmo: {VerificadorIntegridad.NombreAlgoritmo(inspeccion)} · Huella: {inspeccion.FirmaHash}");
    }

    private static void Titulo(Section seccion, string texto)
    {
        var titulo = seccion.AddParagraph(texto);
        titulo.Format.Font.Size = 11;
        titulo.Format.Font.Bold = true;
        titulo.Format.SpaceBefore = Unit.FromPoint(10);
        titulo.Format.SpaceAfter = Unit.FromPoint(4);
    }

    private static string Valor(string? respuesta, string tipo) => string.IsNullOrWhiteSpace(respuesta)
        ? "Sin responder"
        : tipo == TiposRespuesta.SiNo && string.Equals(respuesta, "Si", StringComparison.OrdinalIgnoreCase)
            ? "Sí"
            : respuesta;
}
