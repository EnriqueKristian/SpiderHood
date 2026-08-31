
// Modo oscuro de SpiderHood.
//
// El tema vive en localStorage bajo la clave simple "theme" (no
// "preferences_{userId}") a propósito: tiene que poder leerse ANTES de que
// Blazor sepa quién es el usuario — ver el script inline en App.razor que
// aplica el tema apenas carga la página, para evitar el flash de tema
// equivocado. El objeto de preferencias completo (Profile > Preferencias)
// sigue viviendo en "preferences_{userId}" vía PreferenceService; al guardar
// ahí, Profile.razor llama a spiderHoodTheme.set(...) para mantener esta
// clave simple sincronizada y aplicar el cambio al instante.
(function () {
    var STORAGE_KEY = "theme";

    function resolveEffectiveTheme(theme) {
        if (theme === "dark" || theme === "light") {
            return theme;
        }
        // "system" o cualquier valor desconocido: seguir la preferencia del SO.
        return (window.matchMedia && window.matchMedia("(prefers-color-scheme: dark)").matches)
            ? "dark"
            : "light";
    }

    function apply(theme) {
        document.documentElement.setAttribute("data-bs-theme", resolveEffectiveTheme(theme));
    }

    function readStored() {
        try {
            return localStorage.getItem(STORAGE_KEY) || "system";
        } catch (e) {
            return "system";
        }
    }

    window.spiderHoodTheme = {
        // Aplica el tema y lo persiste — se usa desde Profile.razor al guardar.
        set: function (theme) {
            try { localStorage.setItem(STORAGE_KEY, theme); } catch (e) { /* ignorar */ }
            apply(theme);
        },
        // Sólo aplica lo que ya esté guardado (o "system" si no hay nada). El script
        // inline de App.razor ya lo hace apenas carga la página; esto queda disponible
        // por si algún componente necesita reforzarlo tras un cambio de DOM.
        applyStored: function () {
            var stored = readStored();
            apply(stored);
            return stored;
        }
    };

    // Con "Sistema" elegido, seguir los cambios de tema del SO mientras la pestaña
    // sigue abierta (sin esto, cambiar el tema del SO no se reflejaría hasta recargar).
    if (window.matchMedia) {
        window.matchMedia("(prefers-color-scheme: dark)").addEventListener("change", function () {
            if (readStored() === "system") {
                apply("system");
            }
        });
    }
})();