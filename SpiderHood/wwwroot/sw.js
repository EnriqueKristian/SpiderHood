// Service worker mínimo -- Docs/Design-Piloto-Mobile-Android.md, Fase 1 (Opción A,
// PWA/TWA). Su único propósito es cumplir el criterio de instalabilidad de Chrome
// (manifest + service worker con un handler de "fetch" + HTTPS) para que el TWA se
// pueda instalar como app -- NO cachea ni sirve nada offline a propósito.
//
// SpiderHood es Blazor Server: cada página vive en un circuito SignalR persistente
// contra el servidor (sección 3.1 del documento de diseño) -- no hay "modo sin
// conexión" real hoy, y cachear el HTML/JS acá daría una falsa sensación de que la
// app funciona sin red cuando en realidad el circuito va a estar caído. Mejor dejar
// que el navegador muestre su propio error de "sin conexión" que uno silenciosamente
// roto. Si más adelante se migra a la Opción B (MAUI Blazor Hybrid) o se agrega
// soporte offline real, ESTE es el lugar para sumar cacheo de assets estáticos.
self.addEventListener('install', () => {
    self.skipWaiting();
});

self.addEventListener('activate', (event) => {
    event.waitUntil(self.clients.claim());
});

self.addEventListener('fetch', () => {
    // Passthrough: no responde nada propio, deja que la red maneje cada request.
});
