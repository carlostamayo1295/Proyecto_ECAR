// F-13 (Fase 5): en los formularios marcados con la clase «form-enter», Enter pasa al siguiente
// campo. En el último campo lleva el foco al botón marcado con «enter-guardar»: nunca guarda solo,
// hay que pulsar Enter otra vez sobre el botón o hacer clic. El login no lleva la clase y conserva
// «Enter inicia sesión». Las áreas de texto (Lines > 1) conservan Enter para el salto de línea.
(function () {
    const campos = "input:not([type=\"hidden\"]), textarea";

    function seVe(elemento) {
        return !elemento.disabled && elemento.offsetParent !== null;
    }

    document.addEventListener("keydown", function (e) {
        if (e.key !== "Enter" || e.shiftKey || e.ctrlKey || e.altKey || e.isComposing) {
            return;
        }

        // Solo los campos de una línea: en un botón Enter lo pulsa y en un área de texto salta de línea.
        const campo = e.target;
        if (!(campo instanceof HTMLInputElement)) {
            return;
        }

        const formulario = campo.closest(".form-enter");
        if (!formulario) {
            return;
        }

        // Con una lista desplegable abierta (MudSelect, MudAutocomplete), Enter elige la opción.
        if (document.querySelector(".mud-popover-open")) {
            return;
        }

        const lista = Array.from(formulario.querySelectorAll(campos)).filter(seVe);
        const posicion = lista.indexOf(campo);
        if (posicion === -1) {
            return;
        }

        e.preventDefault();
        e.stopPropagation();

        const siguiente = lista[posicion + 1];
        if (siguiente) {
            siguiente.focus();
        } else {
            const guardar = formulario.querySelector(".enter-guardar");
            if (guardar) {
                guardar.focus();
            }
        }
    }, true);
})();