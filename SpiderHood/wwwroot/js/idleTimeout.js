
// Cierre de sesión por inactividad -- ver Docs/Pendientes-Negocio-Sesion.md #1.
//
// La navegación interna de Blazor Server (NavigateTo sin forceLoad) no genera
// requests HTTP nuevos, así que el SlidingExpiration de la cookie de
// autenticación (Program.cs) nunca se entera de que el usuario está inactivo:
// simplemente no hay ningún request que pudiera haber cortado la sesión. Este
// timer corre enteramente en el browser, sobre el DOM real (no sobre eventos
// de Blazor), para no depender de que haya tráfico HTTP.
(function () {
    var ACTIVITY_EVENTS = ["mousemove", "mousedown", "keydown", "scroll", "touchstart"];
    var timeoutId = null;
    var timeoutMs = null;

    function resetTimer() {
        if (timeoutId) {
            clearTimeout(timeoutId);
        }
        timeoutId = setTimeout(onIdle, timeoutMs);
    }

    function onIdle() {
        stop();
        // Full page load a propósito: HttpContext.SignOutAsync (el cierre de sesión
        // real, que borra la cookie) no puede correr dentro de un circuito
        // InteractiveServer ya conectado -- ver el comentario en Logout.razor.
        // /logout es la página estática que sí lo hace, y de ahí redirige a /login.
        window.location.href = "/logout";
    }

    function stop() {
        if (timeoutId) {
            clearTimeout(timeoutId);
            timeoutId = null;
        }
        ACTIVITY_EVENTS.forEach(function (evt) {
            document.removeEventListener(evt, resetTimer, true);
        });
    }

    window.spiderHoodIdleTimeout = {
        // Llamado desde HeaderMainLayout cada vez que hay un usuario autenticado
        // (firstRender y cada cambio de AuthenticationState) -- idempotente: reinicia
        // desde cero si ya estaba corriendo, en vez de acumular listeners.
        start: function (ms) {
            stop();
            timeoutMs = ms;
            ACTIVITY_EVENTS.forEach(function (evt) {
                document.addEventListener(evt, resetTimer, true);
            });
            resetTimer();
        },
        // Llamado si el usuario deja de estar autenticado en este mismo circuito, para
        // no terminar redirigiendo a /logout a alguien que ya no tiene sesión.
        stop: stop
    };
})();
