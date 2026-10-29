using MigraDoc.DocumentObjectModel;
using PdfSharp.Drawing;

namespace ECAR.API.Services.Documentos;

/// <summary>
/// Evita que el texto se salga de su celda en los PDF (29/10/2026). MigraDoc parte las líneas en los
/// espacios, pero una palabra más ancha que la columna (un correo, el JSON de la auditoría, un nombre de
/// archivo, una huella) se dibujaba encima de la celda vecina. Aquí se mide el texto con la misma fuente
/// del documento para:
/// <list type="bullet">
/// <item>repartir el ancho de las columnas según lo que llevan: las cortas quedan en una línea y el resto
/// se lo lleva la columna con más información;</item>
/// <item>partir, con un salto de línea, las palabras que aun así no caben. El texto no cambia: solo se
/// reparte en más renglones.</item>
/// </list>
/// </summary>
internal sealed class AjusteTexto : IDisposable
{
    /// <summary>Relleno a cada lado de la celda; se fija en la tabla con <see cref="Preparar"/>.</summary>
    internal const double RellenoLadoCm = 0.1;

    /// <summary>
    /// Lo que la celda resta al texto: el relleno de los dos lados y un margen, porque MigraDoc parte una
    /// palabra que llega justo al borde («BOM-0 / 1»).
    /// </summary>
    internal const double RellenoCm = 2 * RellenoLadoCm + 0.15;

    /// <summary>Fija el relleno de las celdas con el que se midió, en lugar de suponer el de MigraDoc.</summary>
    public static void Preparar(MigraDoc.DocumentObjectModel.Tables.Table tabla)
    {
        tabla.LeftPadding = Unit.FromCentimeter(RellenoLadoCm);
        tabla.RightPadding = Unit.FromCentimeter(RellenoLadoCm);
    }

    /// <summary>Una columna corta (fecha, estado, código) que cabe en este ancho se deja en una sola línea.</summary>
    private const double FraccionColumnaCorta = 0.22;

    /// <summary>Una palabra larga no reserva más de esta parte de la hoja: el resto se parte en renglones.</summary>
    private const double FraccionPalabraMaxima = 0.15;

    private readonly XGraphics _graficos;
    private readonly XFont _normal;
    private readonly XFont _negrita;

    public AjusteTexto(double tamanoPt)
    {
        ExportadorTabla.ConfigurarFuentes();
        _graficos = XGraphics.CreateMeasureContext(new XSize(10_000, 10_000), XGraphicsUnit.Point, XPageDirection.Downwards);
        _normal = new XFont("Arial", tamanoPt);
        _negrita = new XFont("Arial", tamanoPt, XFontStyleEx.Bold);
    }

    /// <summary>Ancho del texto en cm, en una sola línea.</summary>
    public double AnchoCm(string texto, bool negrita = false) =>
        string.IsNullOrEmpty(texto) ? 0 : PuntosACm(_graficos.MeasureString(texto, negrita ? _negrita : _normal).Width);

    /// <summary>Ancho, en cm, de la palabra más larga (lo mínimo para no partir palabras).</summary>
    public double PalabraMasLargaCm(string texto, bool negrita = false) =>
        string.IsNullOrEmpty(texto)
            ? 0
            : texto.Split([' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries)
                .Select(p => AnchoCm(p, negrita)).DefaultIfEmpty(0).Max();

    /// <summary>
    /// Anchos de las columnas, en cm, que suman <paramref name="anchoUtilCm"/>: las cortas completas, sin
    /// partir; lo que sobra, a las que llevan más texto, en proporción a lo que les falta para caber en una
    /// línea. Si todo cabe en una línea, el sobrante se reparte en proporción a lo que ocupa cada una.
    /// </summary>
    public IReadOnlyList<double> Anchos(IReadOnlyList<string> encabezados, IReadOnlyList<IReadOnlyList<string>> filas,
        double anchoUtilCm)
    {
        var n = encabezados.Count;
        var minimo = new double[n];
        var maximo = new double[n];
        for (var c = 0; c < n; c++)
        {
            minimo[c] = PalabraMasLargaCm(encabezados[c], negrita: true);
            maximo[c] = AnchoCm(encabezados[c], negrita: true);
        }

        foreach (var fila in filas)
        {
            for (var c = 0; c < n && c < fila.Count; c++)
            {
                minimo[c] = Math.Max(minimo[c], PalabraMasLargaCm(fila[c]));
                maximo[c] = Math.Max(maximo[c], AnchoCm(fila[c]));
            }
        }

        var tope = anchoUtilCm * FraccionPalabraMaxima;
        for (var c = 0; c < n; c++)
        {
            minimo[c] = Math.Min(minimo[c], tope) + RellenoCm;
            maximo[c] = Math.Max(maximo[c] + RellenoCm, minimo[c]);
        }

        var anchos = Repartir(minimo, maximo, anchoUtilCm);
        return anchos;
    }

    internal static double[] Repartir(double[] minimo, double[] maximo, double anchoUtilCm)
    {
        var n = minimo.Length;
        var sumaMaximo = maximo.Sum();
        if (sumaMaximo <= anchoUtilCm)
        {
            return maximo.Select(m => m * anchoUtilCm / sumaMaximo).ToArray();
        }

        var sumaMinimo = minimo.Sum();
        if (sumaMinimo >= anchoUtilCm)
        {
            return Recortar(minimo, anchoUtilCm);
        }

        var anchos = (double[])minimo.Clone();
        var sobrante = anchoUtilCm - sumaMinimo;

        // Primero las columnas cortas, completas: de la que menos necesita a la que más.
        foreach (var c in Enumerable.Range(0, n).OrderBy(c => maximo[c] - minimo[c]))
        {
            var falta = maximo[c] - anchos[c];
            if (maximo[c] <= anchoUtilCm * FraccionColumnaCorta && falta <= sobrante)
            {
                anchos[c] = maximo[c];
                sobrante -= falta;
            }
        }

        // Lo que queda, a las columnas con más información: más a la que más le falta, pero suavizado (raíz
        // cuadrada) para que unas observaciones muy largas no dejen el nombre del equipo en seis renglones.
        var pesos = Enumerable.Range(0, n).Select(c => Math.Sqrt(Math.Max(0, maximo[c] - anchos[c]))).ToArray();
        var sumaPesos = pesos.Sum();
        for (var c = 0; c < n && sumaPesos > 0; c++)
        {
            anchos[c] += sobrante * pesos[c] / sumaPesos;
        }

        return anchos;
    }

    /// <summary>
    /// Cuando ni las palabras más largas caben, se recortan por igual las columnas más anchas y las
    /// angostas (códigos, fechas, números) quedan enteras: se busca el tope que hace sumar el ancho útil.
    /// </summary>
    private static double[] Recortar(double[] minimo, double anchoUtilCm)
    {
        var orden = minimo.OrderByDescending(m => m).ToArray();
        for (var k = 1; k <= orden.Length; k++)
        {
            var resto = orden.Skip(k).Sum();
            var tope = (anchoUtilCm - resto) / k;
            if (k == orden.Length || tope >= orden[k])
            {
                return tope > 0
                    ? minimo.Select(m => Math.Min(m, tope)).ToArray()
                    : minimo.Select(m => m * anchoUtilCm / minimo.Sum()).ToArray();
            }
        }

        return minimo;
    }

    /// <summary>
    /// Escribe el texto en el párrafo. Las palabras que no caben en <paramref name="anchoCm"/> (el de la
    /// columna, con su relleno) se parten con saltos de línea; los saltos que ya traía el texto se respetan.
    /// </summary>
    public void Escribir(Paragraph parrafo, string texto, double anchoCm, bool negrita = false)
    {
        var limite = anchoCm - RellenoCm;
        var lineas = texto.Replace("\r\n", "\n").Split('\n');
        for (var l = 0; l < lineas.Length; l++)
        {
            if (l > 0)
            {
                parrafo.AddLineBreak();
            }

            var trozos = Partir(lineas[l], limite, negrita);
            for (var t = 0; t < trozos.Count; t++)
            {
                if (t > 0)
                {
                    parrafo.AddLineBreak();
                }

                if (trozos[t].Length > 0)
                {
                    if (negrita)
                        parrafo.AddFormattedText(trozos[t], TextFormat.Bold);
                    else
                        parrafo.AddText(trozos[t]);
                }
            }
        }
    }

    /// <summary>
    /// Una línea en trozos que MigraDoc ya puede acomodar: las palabras que caben quedan juntas (él las
    /// parte en los espacios); cada palabra más ancha que el límite se corta en pedazos que sí caben.
    /// </summary>
    internal List<string> Partir(string linea, double limiteCm, bool negrita)
    {
        // La columna puede medir justo lo que la palabra más su relleno; al restar, el redondeo dejaba el
        // límite una millonésima por debajo y partía «Código» en «Códig / o».
        limiteCm += 0.001;
        var trozos = new List<string>();
        var actual = new System.Text.StringBuilder();
        foreach (var palabra in linea.Split(' '))
        {
            if (AnchoCm(palabra, negrita) <= limiteCm)
            {
                if (actual.Length > 0)
                    actual.Append(' ');
                actual.Append(palabra);
                continue;
            }

            // La palabra no cabe ni sola: lo acumulado sigue en su renglón y la palabra va en pedazos.
            var pedazo = new System.Text.StringBuilder();
            foreach (var letra in palabra)
            {
                if (pedazo.Length > 0 && AnchoCm(pedazo.ToString() + letra, negrita) > limiteCm)
                {
                    trozos.Add(actual.Length > 0 ? actual + " " + pedazo : pedazo.ToString());
                    actual.Clear();
                    pedazo.Clear();
                }

                pedazo.Append(letra);
            }

            if (actual.Length > 0)
                actual.Append(' ');
            actual.Append(pedazo);
        }

        trozos.Add(actual.ToString());
        return trozos;
    }

    private static double PuntosACm(double puntos) => puntos / 72 * 2.54;

    public void Dispose() => _graficos.Dispose();
}
