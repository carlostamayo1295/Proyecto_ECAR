using System.Text.Json.Serialization;

namespace ECAR.Shared.Responses;

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }
    public List<string>? Errors { get; set; }

    /// <summary>
    /// Solo en el cliente: el código HTTP con el que llegó la respuesta (lo pone LectorRespuestaApi); null si no
    /// hubo respuesta. No viaja en el JSON. Sirve para reconocer, por ejemplo, la cuenta bloqueada (423) sin leer
    /// el texto del mensaje (Fase 5, F-06).
    /// </summary>
    [JsonIgnore]
    public int? CodigoHttp { get; set; }

    public static ApiResponse<T> SuccessResponse(T data, string message = "Operación exitosa")
    {
        return new ApiResponse<T>
        {
            Success = true,
            Message = message,
            Data = data
        };
    }

    public static ApiResponse<T> ErrorResponse(string message, List<string>? errors = null)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Message = message,
            Errors = errors
        };
    }
}