using ECAR.Infrastructure.Entities;
using ECAR.Shared;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;

namespace ECAR.API.Services.Documentos;

/// <summary>
/// Copia legible y completa de una inspección firmada (endpoint 14, Parte 11 §11.10(b) y §11.50):
/// cabecera, respuestas con las novedades resaltadas, miniaturas de las evidencias vigentes, la
/// firma con su manifestación (nombre, fecha y hora con zona, significado), el algoritmo y la huella,
/// y el estado de cada novedad con la manifestación de la firma que la resolvió.
/// </summary>
public static class RegistroInspeccionPdf
{
    /// <param name="inspeccion">Con Equipo.Ubicacion, Usuario, Checklist.Preguntas, Respuestas, Evidencias y Novedades.</param>
    /// <param name="imagenes">Bytes de cada evidencia vigente por IdEvidencia; las que no se pudieron leer, ausentes.</param>
    public static byte[] Generar(Inspeccion inspeccion, IReadOnlyDictionary<long, byte[]> imagenes,
        string generadoPor, DateTime generadoUtc)
    {
        var codigo = $"ECAR-INSP-{inspeccion.IdInspeccion}";
        var pie = new PieDocumento($"Copia electrónica del registro {codigo}", $"Inspección {inspeccion.IdInspeccion}",
            generadoPor, generadoUtc);
        var documento = ExportadorTabla.Documento($"Registro de inspección {codigo}", pie, horizontal: false);
        var seccion = documento.LastSection;

        // Las palabras que no caben en su celda o en la hoja se parten (AjusteTexto, 29/10/2026).
        using var ajuste = new AjusteTexto(TamanoPt);
        var anchoCm = ExportadorTabla.AnchoUtilCm(seccion.PageSetup);

        Cabecera(seccion, inspeccion, ajuste, anchoCm);
        Respuestas(seccion, inspeccion, anchoCm);

        if (!string.IsNullOrWhiteSpace(inspeccion.Observaciones))
        {
            Titulo(seccion, "Observaciones generales");
            ajuste.Escribir(seccion.AddParagraph(), inspeccion.Observaciones, anchoCm);
        }

        Evidencias(seccion, inspeccion, imagenes, anchoCm);
        Firma(seccion, inspeccion, anchoCm);
        Novedades(seccion, inspeccion, ajuste, anchoCm);

        return ExportadorTabla.Renderizar(documento);
    }

    /// <summary>Tamaño de letra normal del documento (ExportadorTabla.Documento).</summary>
    private const double TamanoPt = 9;

    private static void Cabecera(Section seccion, Inspeccion inspeccion, AjusteTexto ajuste, double anchoCm)
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
            ("Estado", InspeccionEstados.Texto(inspeccion.Estado)),
            ("Resultado", inspeccion.Resultado == null ? "—" : InspeccionResultados.Texto(inspeccion.Resultado))
        };

        var tabla = seccion.AddTable();
        AjusteTexto.Preparar(tabla);
        tabla.Borders.Width = 0.4;
        tabla.Borders.Color = Colors.Gray;
        const double anchoEtiqueta = 4;
        tabla.AddColumn(Unit.FromCentimeter(anchoEtiqueta));
        tabla.AddColumn(Unit.FromCentimeter(anchoCm - anchoEtiqueta));
        foreach (var (etiqueta, valor) in filas)
        {
            var fila = tabla.AddRow();
            fila.Cells[0].Shading.Color = Color.FromRgb(0xF2, 0xF2, 0xF2);
            fila.Cells[0].AddParagraph().AddFormattedText(etiqueta, TextFormat.Bold);
            ajuste.Escribir(fila.Cells[1].AddParagraph(), valor, anchoCm - anchoEtiqueta);
        }
    }

    private static void Respuestas(Section seccion, Inspeccion inspeccion, double anchoCm)
    {
        Titulo(seccion, "Respuestas");
        var respuestas = inspeccion.Respuestas.ToDictionary(r => r.IdPregunta);

        // Primero el texto de cada fila, para repartir el ancho según lo que lleva cada columna.
        var numero = 0;
        var filas = inspeccion.Checklist.Preguntas.OrderBy(p => p.Orden).ThenBy(p => p.IdPregunta).Select(pregunta =>
        {
            respuestas.TryGetValue(pregunta.IdPregunta, out var respuesta);
            var esNovedad = pregunta.TipoRespuesta == TiposRespuesta.SiNo &&
                            string.Equals(respuesta?.Respuesta, "No", StringComparison.OrdinalIgnoreCase);
            return (EsNovedad: esNovedad, Textos: (IReadOnlyList<string>)
            [
                (++numero).ToString(),
                pregunta.Pregunta + (pregunta.Obligatoria ? " *" : string.Empty),
                Valor(respuesta?.Respuesta, pregunta.TipoRespuesta) + (esNovedad ? " (novedad)" : string.Empty),
                string.IsNullOrWhiteSpace(respuesta?.Observacion) ? "—" : respuesta!.Observacion
            ]);
        }).ToList();

        const double tamano = 8.5;
        using var ajuste = new AjusteTexto(tamano);
        string[] encabezados = ["#", "Pregunta", "Respuesta", "Observación"];
        var anchos = ajuste.Anchos(encabezados, filas.Select(f => f.Textos).ToList(), anchoCm);

        var tabla = seccion.AddTable();
        AjusteTexto.Preparar(tabla);
        tabla.Borders.Width = 0.4;
        tabla.Borders.Color = Colors.Gray;
        tabla.Format.Font.Size = tamano;
        foreach (var ancho in anchos)
        {
            tabla.AddColumn(Unit.FromCentimeter(ancho));
        }

        var encabezado = tabla.AddRow();
        encabezado.HeadingFormat = true;
        encabezado.Format.Font.Bold = true;
        encabezado.Shading.Color = Color.FromRgb(0xDC, 0xE6, 0xF2);
        for (var c = 0; c < encabezados.Length; c++)
        {
            ajuste.Escribir(encabezado.Cells[c].AddParagraph(), encabezados[c], anchos[c], negrita: true);
        }

        foreach (var (esNovedad, textos) in filas)
        {
            var fila = tabla.AddRow();
            if (esNovedad)
            {
                fila.Shading.Color = Color.FromRgb(0xFD, 0xE2, 0xE1);
            }

            for (var c = 0; c < textos.Count; c++)
            {
                // La respuesta de una novedad va en negrita.
                ajuste.Escribir(fila.Cells[c].AddParagraph(), textos[c], anchos[c], negrita: esNovedad && c == 2);
            }
        }

        var nota = seccion.AddParagraph("* Pregunta obligatoria.");
        nota.Format.Font.Size = 7;
        nota.Format.Font.Color = Colors.DimGray;
    }

    private static void Evidencias(Section seccion, Inspeccion inspeccion, IReadOnlyDictionary<long, byte[]> imagenes,
        double anchoCm)
    {
        var vigentes = inspeccion.Evidencias.Where(e => !e.Retirada).OrderBy(e => e.FechaCarga).ToList();
        Titulo(seccion, $"Evidencias ({vigentes.Count})");
        if (vigentes.Count == 0)
        {
            seccion.AddParagraph("La inspección no tiene evidencias fotográficas.");
            return;
        }

        const int porFila = 3;
        const double tamanoPie = 7;
        var anchoColumna = anchoCm / porFila;
        using var ajuste = new AjusteTexto(tamanoPie);
        var tabla = seccion.AddTable();
        AjusteTexto.Preparar(tabla);
        for (var c = 0; c < porFila; c++)
        {
            tabla.AddColumn(Unit.FromCentimeter(anchoColumna));
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

            // Los nombres de archivo de los teléfonos suelen ser una sola palabra larga: se parte.
            var pie = celda.AddParagraph();
            pie.Format.Font.Size = tamanoPie;
            ajuste.Escribir(pie, $"{evidencia.NombreOriginal} · {HoraColombia.Texto(evidencia.FechaCarga)}", anchoColumna);
            pie.Format.SpaceAfter = Unit.FromPoint(6);
        }
    }

    /// <summary>Cada novedad con su estado; la resuelta, con lo que se hizo y la manifestación de su firma.</summary>
    private static void Novedades(Section seccion, Inspeccion inspeccion, AjusteTexto ajuste, double anchoCm)
    {
        if (inspeccion.Novedades.Count == 0)
        {
            return;
        }

        Titulo(seccion, $"Novedades ({inspeccion.Novedades.Count})");
        foreach (var novedad in inspeccion.Novedades.OrderBy(n => n.IdNovedad))
        {
            var parrafo = seccion.AddParagraph();
            parrafo.Format.SpaceAfter = Unit.FromPoint(4);
            parrafo.AddFormattedText($"Novedad #{novedad.IdNovedad} — {novedad.Estado}. ", TextFormat.Bold);
            ajuste.Escribir(parrafo, novedad.Descripcion, anchoCm);
            if (novedad.Estado == NovedadEstados.Resuelta && novedad.FechaResolucion is { } resuelta)
            {
                var resolucion = seccion.AddParagraph();
                resolucion.Format.LeftIndent = Unit.FromCentimeter(0.5);
                resolucion.Format.Font.Size = 8.5;
                ajuste.Escribir(resolucion,
                    $"Lo que se hizo: {novedad.Solucion}. Firmado electrónicamente por {novedad.FirmaNombre} " +
                    $"el {HoraColombia.Texto(resuelta)} ({HoraColombia.Zona}). Significado: {novedad.FirmaSignificado}.",
                    anchoCm - 0.5);
            }
            else if (novedad.Estado == NovedadEstados.Anulada)
            {
                var anulacion = seccion.AddParagraph();
                ajuste.Escribir(anulacion, $"Anulada. Motivo: {novedad.MotivoAnulacion}", anchoCm - 0.5);
                anulacion.Format.LeftIndent = Unit.FromCentimeter(0.5);
                anulacion.Format.Font.Size = 8.5;
            }
        }
    }

    private static void Firma(Section seccion, Inspeccion inspeccion, double anchoCm)
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
        using var ajusteHuella = new AjusteTexto(7.5);
        ajusteHuella.Escribir(huella, $"Algoritmo: {VerificadorIntegridad.NombreAlgoritmo(inspeccion)} · Huella: {inspeccion.FirmaHash}",
            anchoCm);
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
