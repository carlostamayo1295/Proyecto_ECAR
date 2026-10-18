// Lectura del QR de un equipo con la cámara (SRS §6 "Escaneo QR", regla 10), con html5-qrcode.
// La cámara solo se abre en un contexto seguro (https) y con el permiso del usuario.
window.ecarEscanerQr = {
    _lector: null,

    async iniciar(idElemento, componente) {
        if (!window.Html5Qrcode) {
            throw new Error("No se cargó el lector de códigos QR.");
        }

        await this.detener();
        const lector = new Html5Qrcode(idElemento, {
            verbose: false,
            formatsToSupport: [Html5QrcodeSupportedFormats.QR_CODE],
            // Donde el navegador tiene lector nativo (Chrome en Android) se usa ese, que es más rápido.
            experimentalFeatures: { useBarCodeDetectorIfSupported: true }
        });
        this._lector = lector;

        let leido = false;
        await lector.start(
            { facingMode: "environment" },
            { fps: 10, qrbox: (ancho, alto) => { const lado = Math.floor(Math.min(ancho, alto) * 0.7); return { width: lado, height: lado }; } },
            texto => {
                if (leido) {
                    return;
                }
                leido = true;
                componente.invokeMethodAsync("QrLeido", texto).then(seguir => { leido = !seguir; });
            },
            () => { /* fotograma sin QR: se sigue buscando */ });
    },

    async detener() {
        const lector = this._lector;
        this._lector = null;
        if (!lector) {
            return;
        }
        try {
            if (lector.isScanning) {
                await lector.stop();
            }
            lector.clear();
        } catch {
            // La cámara ya estaba cerrada.
        }
    }
};
