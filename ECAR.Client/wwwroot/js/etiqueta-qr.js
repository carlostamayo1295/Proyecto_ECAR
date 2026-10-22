// Etiqueta del código QR de un equipo (EquipoQrModal). Se imprime en un iframe aparte que solo
// contiene la etiqueta: antes se imprimía el diálogo ocultando el resto de la página con CSS, y el
// recorte y el desplazamiento del diálogo dejaban el QR cortado. La imagen se ajusta a la hoja, así
// que también cabe en un papel de etiqueta pequeño.
window.ecarEtiquetaQr = (function () {
    function escapar(texto) {
        return String(texto ?? '').replace(/[&<>"']/g, function (c) {
            return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
        });
    }

    function html(imagen, codigo, nombre) {
        return '<!doctype html><html lang="es"><head><meta charset="utf-8">' +
            '<title>Etiqueta QR ' + escapar(codigo) + '</title><style>' +
            '@page { margin: 8mm; }' +
            'html, body { margin: 0; padding: 0; height: 100%; }' +
            'body { font-family: Arial, Helvetica, sans-serif; color: #000; }' +
            // La etiqueta ocupa a lo sumo la hoja: el QR (60 mm como máximo) se encoge al espacio que dejan
            // los textos, y los textos se achican en papel angosto. Así no pasa a una segunda hoja.
            '.etiqueta { height: 100%; display: flex; flex-direction: column; align-items: center;' +
            ' text-align: center; break-inside: avoid; page-break-inside: avoid; }' +
            '.etiqueta img { display: block; flex: 0 1 60mm; min-height: 0; width: 100%; max-width: 60mm;' +
            ' object-fit: contain; image-rendering: pixelated; }' +
            '.codigo { flex: none; max-width: 100%; margin-top: 2mm; font-size: clamp(7pt, 4.5vw, 14pt);' +
            ' font-weight: bold; overflow-wrap: anywhere; }' +
            '.nombre { flex: none; max-width: 100%; margin-top: 1mm; font-size: clamp(6pt, 3vw, 10pt);' +
            ' overflow-wrap: anywhere; }' +
            '</style></head><body><div class="etiqueta">' +
            '<img id="qr" src="' + escapar(imagen) + '" alt="Código QR ' + escapar(codigo) + '">' +
            '<div class="codigo">' + escapar(codigo) + '</div>' +
            '<div class="nombre">' + escapar(nombre) + '</div>' +
            '</div></body></html>';
    }

    function imprimir(imagen, codigo, nombre) {
        return new Promise(function (resolve) {
            var marco = document.createElement('iframe');
            marco.setAttribute('aria-hidden', 'true');
            marco.style.cssText = 'position:fixed;right:0;bottom:0;width:0;height:0;border:0;';
            document.body.appendChild(marco);

            var documento = marco.contentDocument;
            documento.open();
            documento.write(html(imagen, codigo, nombre));
            documento.close();

            var listo = false;
            function lanzar() {
                if (listo) return;
                listo = true;
                marco.contentWindow.focus();
                marco.contentWindow.print();
                // print() espera a que se cierre el diálogo de impresión; después se quita el iframe.
                setTimeout(function () { marco.remove(); resolve(); }, 500);
            }

            var img = documento.getElementById('qr');
            if (img.complete) {
                lanzar();
            } else {
                img.onload = lanzar;
                img.onerror = lanzar;
            }
        });
    }

    return { html: html, imprimir: imprimir };
})();
