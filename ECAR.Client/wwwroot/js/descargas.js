// Entrega al navegador un archivo descargado del API (reportes PDF/Excel, registro de una
// inspección). HttpClientService.DescargarArchivoAsync lo pasa como DotNetStreamReference.
window.ecarDescargas = {
    guardar: async function (nombre, tipo, streamRef) {
        const contenido = await streamRef.arrayBuffer();
        const blob = new Blob([contenido], { type: tipo });
        const url = URL.createObjectURL(blob);
        const enlace = document.createElement("a");
        enlace.href = url;
        enlace.download = nombre;
        document.body.appendChild(enlace);
        enlace.click();
        enlace.remove();
        // El navegador necesita la URL hasta que empieza la descarga.
        setTimeout(() => URL.revokeObjectURL(url), 10000);
    }
};
