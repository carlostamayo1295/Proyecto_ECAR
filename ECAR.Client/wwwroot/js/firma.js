window.FirmaCanvas ={
    init: function(canvas) {
        const ctx = canvas.getContext("2d");
        let isDrawing = false;
        let lastX = 0;
        let lastY = 0;
        
        // Fondo blanco inicial obligatorio
        ctx.fillStyle = "white";
        ctx.fillRect(0, 0, canvas.width, canvas.height);
        
        //Estilo del trazo (2 px y bordes redondeados)
        ctx.lineWidth = 2;
        ctx.lineCap = "round";
        ctx.strokeStyle = "black";
        
        //Evita el scroll de la pantalla al dibujar en moviles
        canvas.style.touchAction = "none";
        
        function getCoordinates(e){
            const rect = canvas.getBoundingClientRect();
            return {
                x:e.clientX - rect.left,
                y:e.clientY - rect.top
            };
            
        }
        
        function startDrawing(e) {
            isDrawing = true;
            const coords = getCoordinates(e);
            lastX = coords.x;
            lastY = coords.y;
            canvas.setPointerCapture(e.pointerId);
            
        }
        
        function draw(e) {
            if (!isDrawing) return;
            e.preventDefault();
            
            const coords = getCoordinates(e);
            ctx.beginPath();
            ctx.moveTo(lastX, lastY);
            ctx.lineTo(coords.x, coords.y);
            ctx.stroke();
            
            lastX = coords.x;
            lastY = coords.y;
        }
        
        function stopDrawing(e) {
            if (isDrawing) {
                isDrawing = false;
                canvas.releasePointerCapture(e.pointerId);
            }
        }
        
        //Pointer events cubren mouse, dedos y stylus nativamente
        canvas.addEventListener("pointerdown", startDrawing);
        canvas.addEventListener("pointermove", draw);
        canvas.addEventListener("pointerup", stopDrawing);
        canvas.addEventListener("pointercancel", stopDrawing);
        canvas.addEventListener("pointerout", stopDrawing);
        
    },
    
    limpiar: function(canvas) {
        const ctx = canvas.getContext("2d");
        ctx.fillStyle = "white";
        ctx.fillRect(0, 0, canvas.width, canvas.height);
    },
    
    obtenerBase64: function(canvas) {
        return canvas.toDataURL("image/png");
    }
};