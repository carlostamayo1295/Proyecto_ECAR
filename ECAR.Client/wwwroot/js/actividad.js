// Última actividad del usuario, para el cierre de sesión por inactividad (Fase 4, PLAN §3.8).
// Se guarda en localStorage para que todas las pestañas abiertas compartan el mismo reloj: quien
// trabaja en una pestaña no pierde la sesión en la otra.
window.ecarActividad = (function () {
    const clave = "ecar_ultima_actividad";
    let ultimaEscritura = 0;

    function registrar() {
        const ahora = Date.now();
        // Como mucho una escritura cada 5 s: mover el ratón dispara cientos de eventos.
        if (ahora - ultimaEscritura < 5000) {
            return;
        }
        ultimaEscritura = ahora;
        try {
            localStorage.setItem(clave, String(ahora));
        } catch (e) {
            // Sin localStorage (modo privado estricto) no se cierra por inactividad.
        }
    }

    ["pointerdown", "pointermove", "keydown", "wheel", "touchstart", "scroll"].forEach(function (evento) {
        window.addEventListener(evento, registrar, { passive: true, capture: true });
    });
    document.addEventListener("visibilitychange", function () {
        if (!document.hidden) {
            registrar();
        }
    });

    return {
        // Marca actividad ahora, aunque no haya pasado el intervalo (p. ej. al iniciar sesión).
        reiniciar: function () {
            ultimaEscritura = 0;
            registrar();
        },
        // Segundos desde la última actividad en cualquier pestaña; 0 si nunca se registró.
        segundosInactivo: function () {
            let ultima = 0;
            try {
                ultima = parseInt(localStorage.getItem(clave) || "0", 10) || 0;
            } catch (e) {
                return 0;
            }
            return ultima === 0 ? 0 : Math.max(0, Math.floor((Date.now() - ultima) / 1000));
        }
    };
})();
