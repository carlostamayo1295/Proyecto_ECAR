namespace ECAR.API.Configuration
{
    public class EvidenciasOptions
    {
        public string RutaBase { get; set; } = string.Empty;
        public int TamanoMaximoMB { get; set; } = 5;
        public string TiposPermitidos { get; set; } = string.Empty;
    } 
}
