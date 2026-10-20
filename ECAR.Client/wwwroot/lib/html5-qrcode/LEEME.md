# html5-qrcode 2.3.8

- **Origen:** paquete npm `html5-qrcode@2.3.8` (https://github.com/mebjas/html5-qrcode), descargado con `npm pack`
  el 18/10/2026; integridad `sha512-jsr4vafJhwoLVEDW3n1KvPnCCXWaQfRng0/EEYk1vNcQGcG/htAdhJX0be8YyqMoSz7+hZvOZSTAepsabiuhiQ==`.
- **Licencia:** Apache-2.0 (archivo `LICENSE` de esta carpeta; se conserva tal cual).
- **Uso:** solo `html5-qrcode.min.js`, a través de `wwwroot/js/escaner-qr.js` y el componente `EscanerQrDialog`.
  Se usa la clase `Html5Qrcode` (sin la interfaz prefabricada `Html5QrcodeScanner`). No hace llamadas de red.
- Se sirve desde el propio cliente, sin CDN: la planta puede no tener salida a internet.
