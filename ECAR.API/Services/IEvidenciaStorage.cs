namespace ECAR.API.Services
{
    public interface IEvidenciaStorage
    {
        Task<string> GuardarAsync(Stream stream, string extension, long idInspeccion);
        Task<Stream> AbrirAsync(string rutaRelativa);
        Task EliminarAsync(string rutaRelativa);
    }
}