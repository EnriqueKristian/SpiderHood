// Muestra una sombra en el borde derecho de las tablas que desbordan su
// contenedor (Docs/Pendientes-Negocio-Consolidado.md #22 -- reportado en
// dispositivo real: sin esto, una tabla con columnas ocultas a la derecha se
// ve "cortada", no "deslizable"). Ver los estilos .has-hscroll/.at-scroll-end
// en wwwroot/css/components.css.
//
// Funciona por polling liviano en vez de MutationObserver: Blazor Server
// reemplaza filas de tabla constantemente (filtros, orden, paginado) y estas
// pantallas no son tan numerosas como para que un intervalo de 500ms pese.
(function () {
    function sync(el) {
        var overflows = el.scrollWidth > el.clientWidth + 1;
        el.classList.toggle('has-hscroll', overflows);
        if (overflows) {
            var atEnd = el.scrollLeft + el.clientWidth >= el.scrollWidth - 2;
            el.classList.toggle('at-scroll-end', atEnd);
        } else {
            el.classList.remove('at-scroll-end');
        }
    }

    function syncAll() {
        document.querySelectorAll('.table-responsive').forEach(sync);
    }

    document.addEventListener('scroll', function (event) {
        if (event.target && event.target.classList && event.target.classList.contains('table-responsive')) {
            sync(event.target);
        }
    }, true);

    window.addEventListener('resize', syncAll);
    window.addEventListener('load', syncAll);
    setInterval(syncAll, 500);
})();
