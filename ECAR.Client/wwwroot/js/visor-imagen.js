// Visor de evidencias (29/10/2026), solo de lectura: acerca con la rueda del mouse, los botones o
// pellizcando en el teléfono; con la imagen acercada se mueve arrastrando; doble clic alterna entre
// ajustada y 2x. No modifica la imagen: solo cambia cómo se ve.
window.visorImagen = (function () {
    const ESCALA_MAXIMA = 8;
    const estados = new WeakMap();

    function aplicar(e) {
        e.img.style.transform = `translate(${e.x}px, ${e.y}px) scale(${e.escala})`;
        e.contenedor.classList.toggle("acercada", e.escala > 1);
        e.contenedor.classList.toggle("arrastrando", e.punteros.size > 0 && e.escala > 1);
    }

    // La imagen no se aleja tanto que se pierda: como máximo, su borde llega al borde del visor.
    function limitar(e) {
        const maxX = Math.max(0, (e.img.clientWidth * e.escala - e.contenedor.clientWidth) / 2);
        const maxY = Math.max(0, (e.img.clientHeight * e.escala - e.contenedor.clientHeight) / 2);
        e.x = Math.min(maxX, Math.max(-maxX, e.x));
        e.y = Math.min(maxY, Math.max(-maxY, e.y));
    }

    // Cambia la escala dejando quieto el punto (px, py), medido desde el centro del visor.
    function escalar(e, factor, px, py) {
        const nueva = Math.min(ESCALA_MAXIMA, Math.max(1, e.escala * factor));
        const k = nueva / e.escala;
        e.x = px - (px - e.x) * k;
        e.y = py - (py - e.y) * k;
        e.escala = nueva;
        if (nueva === 1) {
            e.x = 0;
            e.y = 0;
        }
        limitar(e);
        aplicar(e);
    }

    function desdeCentro(e, clientX, clientY) {
        const r = e.contenedor.getBoundingClientRect();
        return [clientX - r.left - r.width / 2, clientY - r.top - r.height / 2];
    }

    function distancia(a, b) {
        return Math.hypot(a.x - b.x, a.y - b.y);
    }

    return {
        iniciar(contenedor) {
            const img = contenedor.querySelector("img");
            if (!img || estados.has(contenedor)) {
                return;
            }

            const e = { contenedor, img, escala: 1, x: 0, y: 0, punteros: new Map(), pellizco: 0 };
            estados.set(contenedor, e);

            contenedor.addEventListener("wheel", ev => {
                ev.preventDefault();
                const [px, py] = desdeCentro(e, ev.clientX, ev.clientY);
                escalar(e, ev.deltaY < 0 ? 1.2 : 1 / 1.2, px, py);
            }, { passive: false });

            contenedor.addEventListener("pointerdown", ev => {
                // Capturar el puntero hace que el arrastre siga aunque el mouse salga del visor; si el
                // navegador no lo permite, el arrastre igual funciona dentro del visor.
                try {
                    contenedor.setPointerCapture(ev.pointerId);
                } catch {
                    // Sin captura.
                }
                e.punteros.set(ev.pointerId, { x: ev.clientX, y: ev.clientY });
                if (e.punteros.size === 2) {
                    const [a, b] = [...e.punteros.values()];
                    e.pellizco = distancia(a, b);
                }
                aplicar(e);
            });

            contenedor.addEventListener("pointermove", ev => {
                const anterior = e.punteros.get(ev.pointerId);
                if (!anterior) {
                    return;
                }

                const actual = { x: ev.clientX, y: ev.clientY };
                e.punteros.set(ev.pointerId, actual);

                if (e.punteros.size === 2) {
                    // Dos dedos: acerca o aleja alrededor del punto medio.
                    const [a, b] = [...e.punteros.values()];
                    const d = distancia(a, b);
                    if (e.pellizco > 0) {
                        const [px, py] = desdeCentro(e, (a.x + b.x) / 2, (a.y + b.y) / 2);
                        escalar(e, d / e.pellizco, px, py);
                    }
                    e.pellizco = d;
                } else if (e.escala > 1) {
                    e.x += actual.x - anterior.x;
                    e.y += actual.y - anterior.y;
                    limitar(e);
                    aplicar(e);
                }
            });

            const soltar = ev => {
                e.punteros.delete(ev.pointerId);
                e.pellizco = 0;
                aplicar(e);
            };
            contenedor.addEventListener("pointerup", soltar);
            contenedor.addEventListener("pointercancel", soltar);

            contenedor.addEventListener("dblclick", ev => {
                if (e.escala > 1) {
                    escalar(e, 1 / e.escala, 0, 0);
                } else {
                    const [px, py] = desdeCentro(e, ev.clientX, ev.clientY);
                    escalar(e, 2, px, py);
                }
            });

            aplicar(e);
        },

        acercar(contenedor, factor) {
            const e = estados.get(contenedor);
            if (e) {
                escalar(e, factor, 0, 0);
            }
        },

        ajustar(contenedor) {
            const e = estados.get(contenedor);
            if (e) {
                escalar(e, 1 / e.escala, 0, 0);
            }
        }
    };
})();
