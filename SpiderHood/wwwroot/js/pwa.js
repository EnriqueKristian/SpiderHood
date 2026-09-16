// Registro del service worker -- ver wwwroot/sw.js para por qué es solo un
// passthrough (Docs/Design-Piloto-Mobile-Android.md, Fase 1). Sin esto el TWA/PWA
// no cumple el criterio de instalabilidad de Chrome.
if ('serviceWorker' in navigator) {
    window.addEventListener('load', function () {
        navigator.serviceWorker.register('/sw.js').catch(function (error) {
            console.error('No se pudo registrar el service worker:', error);
        });
    });
}
