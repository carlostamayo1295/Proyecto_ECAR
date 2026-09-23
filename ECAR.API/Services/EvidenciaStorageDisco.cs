using System;
using System.IO;
using System.Threading.Tasks;
using ECAR.API.Configuration;
using Microsoft.Extensions.Options;

namespace ECAR.API.Services
{
    public class EvidenciaStorageDisco : IEvidenciaStorage
    {
        private readonly string _rutaBase;

        public EvidenciaStorageDisco(IOptions<EvidenciasOptions> options)
        {
            // Tomamos la ruta base que pusimos en el appsettings y sacamos la ruta absoluta
            _rutaBase = Path.GetFullPath(options.Value.RutaBase);
        }

        public async Task<string> GuardarAsync(Stream stream, string extension, long idInspeccion)
        {
            // 1. Organizar por Año/Mes/IdInspeccion para que la carpeta no se llene de miles de archivos sueltos
            var año = DateTime.UtcNow.Year.ToString();
            var mes = DateTime.UtcNow.Month.ToString("D2");
            var nombreArchivo = $"{Guid.NewGuid()}{extension}";
            
            var rutaRelativa = Path.Combine(año, mes, idInspeccion.ToString(), nombreArchivo);
            var rutaFisica = Path.Combine(_rutaBase, rutaRelativa);

            // 2. Asegurar que toda la estructura de carpetas exista antes de guardar
            Directory.CreateDirectory(Path.GetDirectoryName(rutaFisica)!);

            // 3. Guardar el archivo físicamente
            using var fileStream = new FileStream(rutaFisica, FileMode.Create, FileAccess.Write);
            await stream.CopyToAsync(fileStream);

            // Devolvemos la ruta con slashes normales para que la base de datos y la web lo lean bien
            return rutaRelativa.Replace("\\", "/");
        }

        public Task<Stream> AbrirAsync(string rutaRelativa)
        {
            var rutaFisica = Path.Combine(_rutaBase, rutaRelativa);
            
            // Seguridad: Bloquear intentos de "Directory Traversal" (ej. ../../Windows/System32)
            if (!Path.GetFullPath(rutaFisica).StartsWith(_rutaBase))
                throw new UnauthorizedAccessException("Intento de acceso a ruta no permitida.");

            if (!File.Exists(rutaFisica))
                throw new FileNotFoundException("El archivo de evidencia no existe.");

            Stream stream = new FileStream(rutaFisica, FileMode.Open, FileAccess.Read);
            return Task.FromResult(stream);
        }

        public Task EliminarAsync(string rutaRelativa)
        {
            var rutaFisica = Path.Combine(_rutaBase, rutaRelativa);

            if (!Path.GetFullPath(rutaFisica).StartsWith(_rutaBase))
                throw new UnauthorizedAccessException("Intento de acceso a ruta no permitida.");

            if (File.Exists(rutaFisica))
            {
                File.Delete(rutaFisica);
            }

            return Task.CompletedTask;
        }
    }
}