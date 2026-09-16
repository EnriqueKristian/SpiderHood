// Auto-cierra el menú lateral (hamburguesa) al navegar en mobile -- Bootstrap
// abre/cierra el collapse #nav-collapse con el botón toggler, pero nunca lo
// cierra solo porque se hizo clic en un link de adentro (bug reportado:
// después de tocar "Mis Pagos" en el celular, el menú se queda abierto
// tapando la pantalla hasta volver a tocar las rayas).
//
// Delegado desde document (no desde #nav-collapse directamente) porque
// LeftMenu.razor es @rendermode InteractiveServer y puede re-renderizar y
// reemplazar ese nodo -- un listener puesto directo ahí se perdería en el
// próximo render de Blazor.
document.addEventListener('click', function (event) {
    var link = event.target.closest('#nav-collapse a.nav-link');
    if (!link) return;

    // Los headers de submenú también son <a class="nav-link"> pero abren/cierran
    // un acordeón interno (data-bs-toggle="collapse") -- no navegan a ningún
    // lado, así que no hay que cerrar el menú completo en ese caso.
    if (link.hasAttribute('data-bs-toggle')) return;

    // Sólo aplica en mobile: en desktop el sidebar está siempre expandido y
    // el botón hamburguesa ni se muestra (ver NavMenu.razor.css, min-width: 641px).
    if (window.innerWidth >= 641) return;

    var collapseEl = document.getElementById('nav-collapse');
    if (!collapseEl || typeof bootstrap === 'undefined') return;

    var instance = bootstrap.Collapse.getInstance(collapseEl)
        || new bootstrap.Collapse(collapseEl, { toggle: false });
    instance.hide();
});
