# Pendientes de negocio — Sesión y Autenticación

Backlog de temas de **negocio/seguridad** detectados al revisar el manejo de
sesión de la aplicación (no ligado a un branch de trabajo específico como los
otros documentos de esta carpeta).

---

## 1. La sesión no expira por inactividad (~20 min) — cambiar de página la "renueva" sin revisar el tiempo real

**Estado: resuelto (2026-09-09), branch `claude/lista-pendientes-0gb03a`.**

Regla de negocio esperada (indicada por el usuario): tras **20 minutos o más
de inactividad**, el usuario debería perder la sesión y tener que volver a
loguearse. Hoy eso no pasa: **no existe ningún mecanismo de expiración por
inactividad**. Al cambiar de página dentro de la app, "se refresca todo y
sigue como si nada" -- la sesión nunca se corta por más tiempo que pase sin
que el usuario haga nada.

**Lo que hay hoy (verificado en el código):**

- La cookie de autenticación se configura con `ExpireTimeSpan =
  TimeSpan.FromHours(8)` y **`SlidingExpiration = true`**
  (`Program.cs:52-53`). Esto renueva la cookie por 8 horas más, pero solo
  ante un **request HTTP nuevo** -- no es la causa directa del síntoma
  reportado.
- La causa real es de arquitectura Blazor Server: la gran mayoría de la
  navegación interna usa `Navigation.NavigateTo(...)` **sin `forceLoad`**
  (confirmado por búsqueda en todo el repo: solo 3 sitios usan
  `forceLoad: true` -- `PaymentSimulated.razor:97`,
  `SelectBuilding.razor:188`, `Settings.razor:260` -- más los redirects a
  `/login` cuando ya no hay usuario, ej. `Home.razor.cs:135`). Sin
  `forceLoad`, cambiar de página es 100% client-side sobre el mismo circuito
  SignalR: **no genera un request HTTP nuevo**, así que nunca pasa por el
  middleware de autenticación ni por `OnValidatePrincipal`
  (`Program.cs:66-117`).
- `CustomAuthenticationStateProvider` (`Services/CustomAuthenticationStateProvider.cs:40,51-58`)
  captura el `ClaimsPrincipal` **una sola vez por circuito** (al conectarse)
  y lo cachea en memoria (`_hydrationTask`). Cada componente que pide el
  usuario actual recibe ese mismo objeto cacheado, sin volver a validar nada
  contra el tiempo transcurrido.
- El circuito de Blazor Server (y el usuario en memoria) permanece vivo
  mientras la conexión SignalR siga viva. `AddInteractiveServerComponents()`
  se registra **sin opciones** (`Program.cs:26`) -- no hay
  `CircuitOptions`, `DisconnectedCircuitMaxRetained` ni ningún timer de
  idle configurado en el proyecto.
- Existe un campo `SessionExpiry` (fijado a `AddHours(8)` en
  `Services/IUserSessionLoader.cs:77` y en `Services/IAuthService.cs:188`) y
  una propiedad `UserSession.IsAuthenticated => SessionExpiry >
  DateTime.UtcNow` (`Classes/User.cs:136`) que **parecen** implementar esto,
  pero es código muerto: su único punto de uso está comentado en
  `Components/Pages/Shared/AuthGuard.razor:34,43`, y `AuthGuard.razor` en sí
  **no se usa en ningún componente** (0 referencias `<AuthGuard` en todo el
  repo). Tampoco hay `[Authorize]` en ningún componente ni
  `AuthorizeRouteView` en `Components/Routes.razor` -- el gating de páginas
  es ad-hoc por componente (ej. `Home.razor.cs:106-137`: si
  `GetCurrentUserAsync()` devuelve `null` recién ahí redirige a `/login`,
  pero eso solo pasa si la cookie realmente no está o es inválida, nunca por
  inactividad).

**Lo que falta diseñar/implementar:**

- Un mecanismo que trackee la última interacción real del usuario dentro del
  circuito activo (los 8 hs de la cookie HTTP no sirven para esto, porque no
  se tocan mientras la navegación sea client-side vía SignalR) -- por
  ejemplo un timer server-side por circuito, o detección de inactividad en
  el cliente (JS) que dispare el cierre de sesión.
- Que ese chequeo, al vencer el umbral de inactividad (~20 min, a confirmar
  con el usuario), dispare `SignOutAsync` + redirect a `/login` con
  `forceLoad: true` -- hoy no existe ninguna de las dos partes.
- Decidir si conviene reactivar/adaptar `AuthGuard.razor` (o el patrón
  `[Authorize]`/`AuthorizeRouteView` estándar de Blazor) como gate central,
  en vez de que cada página maneje su propia verificación de sesión de forma
  ad-hoc.

**Cómo se resolvió:** ya que la navegación interna no genera requests HTTP y
el usuario autenticado vive cacheado en memoria por circuito (ver arriba),
el timer no podía depender del servidor por request -- se agregó
`wwwroot/js/idleTimeout.js`, un timer 100% client-side que escucha actividad
real del usuario (`mousemove`/`mousedown`/`keydown`/`scroll`/`touchstart`) y,
tras 20 minutos sin ninguna, redirige con `window.location.href = "/logout"`
-- un *full page load* a propósito, porque `/logout` (`Logout.razor`) es la
única página sin `@rendermode` que puede llamar `HttpContext.SignOutAsync`
para de verdad borrar la cookie (mismo motivo que evita hacerlo desde un
circuito ya conectado, documentado en el propio `Logout.razor`).

El script se referencia una vez en `App.razor` (junto a `theme.js`, etc.) y
se arranca/para desde `HeaderMainLayout.razor` (`UpdateIdleTimeoutAsync`,
llamado al final de `LoadUserDataAsync`) -- ese método ya corre una vez por
circuito en `OnAfterRenderAsync(firstRender)` y de nuevo en cada
`AuthenticationStateChanged`, así que el timer arranca recién cuando hay un
`_currentUser` real (no en `/login`, donde este mismo componente también se
inicializa pero todavía anónimo) y se apaga si el usuario deja de estar
autenticado en el mismo circuito. El umbral vive como constante
`IdleTimeoutMs` en `HeaderMainLayout.razor`.

**Pendiente de verificar manualmente** (no se pudo levantar el proyecto en
este entorno, no hay SDK de .NET instalado): probar en un browser real que
tras 20 minutos sin tocar mouse/teclado la sesión efectivamente expira, y
que interactuar con la página sí reinicia el conteo.
