namespace ECAR.Shared;

public static class InspeccionEstados
{
    public const string EnCurso = "EnCurso";
    public const string Cerrada = "Cerrada";

    public static bool EsValido(string estado) => estado is EnCurso or Cerrada;
}
