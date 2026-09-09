# Diagnóstico y plan — Piloto Mobile (Android primero)

Evaluación pedida por el usuario: si se lleva SpiderHood al celular (Android u
iOS), ¿qué funcionalidad llevamos, está preparada la arquitectura, qué se
reutiliza y qué cambia? Este documento es el diagnóstico + plan de piloto,
arrancando por Android. Basado en revisar el código real (`Program.cs`,
`Services/`, `Components/Pages/ResidentPages`, `IncidentPages`, csproj), no en
suposiciones.

---

## 1. Resumen ejecutivo

**La arquitectura actual (Blazor Server) no está preparada para mobile tal
cual está.** No es un tema de "falta pulir" -- el modelo entero (sesión
atada a un circuito SignalR persistente, sin API, sin auth por token, sin
push, sin subida de archivos, sin pasarela de pago para el residente) fue
construido para un sitio web de escritorio/navegador, no para una app nativa
ni para un piloto mobile liviano.

La buena noticia: la lógica de **negocio** (Services, stored procedures,
modelo de roles/permisos) SÍ se puede reutilizar casi entera -- lo que falta
es la capa de acceso (API + auth por token) y algunas piezas nuevas
(push, fotos, pagos del residente). La decisión más importante no es de
código: es **qué camino de arquitectura tomar**, porque cambia radicalmente
cuánto hay que construir. Ver sección 5.

---

## 2. ¿Qué funcionalidad llevamos al piloto?

El caso de uso mobile más fuerte es el **portal del residente**, no el panel
de administración (ese tiene demasiadas pantallas, tablas, formularios
complejos -- pensado para pantalla grande, no para un piloto mobile). Lo que
ya existe hoy como páginas de residente (`Components/Pages/ResidentPages/`,
`/incidents`):

| Función | Existe hoy (web) | Lista para mobile tal cual |
|---|---|---|
| Ver mis cuotas/recibos (`MyPayments.razor`) | Sí, solo lectura | Sí -- reusar lógica |
| Ver presupuesto del edificio (`ViewBudget.razor`) | Sí | Sí |
| Ver/reportar incidentes (`/incidents`) | Sí, compartida con staff | Falta: sin adjuntar fotos |
| Calendario (`CalendarPage.razor`) | Sí | Sí |
| Anuncios/comunicados | **No existe** -- está en la lista de permisos (`view_announcements`) pero no hay página construida | No |
| **Pagar mi cuota desde el celular** | **No existe ni en web** -- el único flujo de pago real (MercadoPago) es para que el Administrador pague su suscripción SaaS, no para que el residente pague su cuota (`Services/IPaymentService.cs`, `PaymentSimulated.razor`) | No -- hay que construirlo desde cero |
| Notificaciones (recibo nuevo, incidente actualizado) | No existe infraestructura de push en ningún lado | No |

**Recomendación de alcance para el piloto (Fase 1, ver plan):**
1. Login.
2. Ver mis cuotas / recibos (solo lectura) -- el mayor valor con el menor
   riesgo, reutiliza lógica ya probada.
3. Ver presupuesto del edificio.
4. Reportar incidente (con foto -- la razón #1 por la que un residente abre
   el celular en vez de la web es sacarle una foto a algo roto).
5. Notificaciones push básicas (recibo emitido, incidente respondido).

**Fuera del piloto inicial, a propósito:** pagar la cuota desde el celular
(pasarela de pago real, plata de por medio -- alto riesgo/esfuerzo, mejor una
vez validado que el piloto tiene uso real) y cualquier función de
Administrador (gestión de edificios, conciliación, presupuestos -- eso sigue
siendo trabajo de escritorio).

---

## 3. ¿Está preparada la arquitectura? Diagnóstico detallado

### 3.1 Blazor Server + SignalR: el problema de fondo

Toda la UI hoy es **Blazor Server** (`AddInteractiveServerComponents()`,
`Program.cs:26`): el HTML se renderiza en el servidor y viaja al navegador
por un circuito SignalR persistente -- cada click, cada campo que se escribe,
es un mensaje ida y vuelta al servidor. Esto **no es exportable a una app
nativa** sin repensar el modelo, por varias razones:
- Requiere conexión permanente al servidor. Sin internet (o con la app en
  background, que es exactamente cuándo Android mata la conexión), el
  circuito se cae -- ya se documentó en esta misma sesión
  (`Docs/Pendientes-Negocio-Sesion.md`) cómo de frágil es hoy el manejo de
  sesión/circuito incluso en un navegador de escritorio.
- Cero soporte offline -- no hay forma de ver "mis últimas 3 cuotas" sin red.
- La sesión vive atada al circuito + una cookie HttpOnly de 8hs (con
  sliding expiration) pensada para un browser, no para "quedar logueado en
  el celular durante semanas" como espera cualquier usuario de app mobile.

### 3.2 No hay capa de API

Se buscó en todo el repo: **no existe un solo controller ni endpoint REST**
(`find -iname "*Controller*.cs"` -- cero resultados). Las páginas Razor
llaman **directo** a los `Services/*.cs` (`IInstallmentService`,
`IIncidentService`, etc.), que a su vez llaman a `BDLayout` (stored
procedures). No hay ningún límite/contrato HTTP hoy -- todo corre en el mismo
proceso, mismo circuito.

### 3.3 Autenticación no sirve para una app nativa

`Program.cs:47-62`: `AddCookie(...)`, `ExpireTimeSpan = 8h`,
`SlidingExpiration = true`, cookie `HttpOnly`. Es un modelo 100% de sesión
de navegador (cookie ligada al dominio, invisible/inutilizable desde una app
nativa Android/iOS). Para mobile hace falta un esquema de **token** (JWT o
similar) con refresh token, coexistiendo con el esquema de cookie actual
(ASP.NET Core soporta múltiples esquemes de auth en paralelo sin tocar el
existente).

### 3.4 Sin push notifications

No hay ninguna integración (Firebase Cloud Messaging, APNs, ni siquiera un
paquete NuGet relacionado). Hay que construirlo desde cero.

### 3.5 Sin subida de archivos/fotos

Se buscó `InputFile`/`IBrowserFile` en toda la app: sólo se usa para subir
**Excel** (plantillas de migración, estado de cuenta, lecturas de agua) --
nunca para fotos. El modelo `Incident` (`Classes/Incidents/Incident.cs`) no
tiene ni una columna para adjuntar una imagen. No hay integración de storage
de archivos (Azure Blob, S3, ni siquiera una carpeta local en `wwwroot`) en
ningún lado del código o `appsettings*.json`.

### 3.6 Sin pasarela de pago para el residente

Como se ve en la tabla de la sección 2: el único integrador de pagos real
(MercadoPago, `Services/IPaymentService.cs`) es exclusivamente para la
suscripción SaaS del Administrador. "Pagar mi cuota" para el residente es
una integración de pago **nueva**, con las implicancias de seguridad/PCI y
de producto (¿qué pasarela? Perú: Culqi, Niubiz, PagoEfectivo, o el mismo
MercadoPago) que eso conlleva -- no hay nada que reutilizar acá salvo el
patrón de código de `IPaymentService`.

---

## 4. ¿Qué se reutiliza?

Todo lo de abajo se puede reutilizar **si se le pone una capa de API (o se
elige MAUI Blazor Hybrid, ver sección 5) delante** -- no está atado a Blazor
Server en sí:

- **Lógica de negocio** (`Services/*.cs`): cálculo de cuotas, presupuestos,
  conciliación, permisos -- es C# puro que llama a `BDLayout`, no depende de
  `HttpContext`/SignalR salvo en los pocos lugares donde lee
  `AuthService.GetCurrentUserAsync()` (que en una API se reemplaza por leer
  el usuario del token, patrón estándar).
- **Capa de datos** (`BDLayout` + stored procedures): 100% reutilizable tal
  cual, es independiente de cómo se expone hacia afuera.
- **Modelo de roles/permisos** (`Role`/`RolePermissions`/
  `UserBuildingAssociation`): el concepto se traslada directo a un JWT con
  claims (rol, edificio actual, permisos) armado en el login.
  **Cuidado**, documentado ya en esta sesión (Docs/Pendientes-Negocio-Sesion.md):
  el catálogo de `Permission`/`RolePermissions` no tiene DDL en este repo, no
  se pudo verificar su schema real -- antes de construir el login de la API
  hay que confirmarlo contra la BD real.
- **Modelos de dominio** (`Classes/*.cs`, ej. `Installment`, `Incident`,
  `BankAccount`): sirven de base para los DTOs de la API, con limpieza
  (varios tienen campos `[NotMapped]` de UI mezclados con datos reales).
- **Integración de pagos** (patrón de `IPaymentService` con MercadoPago): no
  el pago del residente en sí, pero sí el patrón de cómo se integró
  MercadoPago (útil como referencia al construir el pago de cuotas).

---

## 5. Camino de arquitectura: 3 opciones reales

Esta es la decisión que más cambia el esfuerzo. Las tres son viables técnicamente:

### Opción A -- PWA / TWA (envolver lo que ya existe)
Reusar las páginas de residente de Blazor Server tal cual, empaquetadas como
"Trusted Web Activity" (un wrapper Android mínimo que abre el sitio web como
si fuera una app, instalable, con ícono propio). **Costo más bajo, validación
más rápida.**
- ✅ Cero reescritura de UI -- las 3 páginas de residente ya existen.
- ✅ Un solo código para Android e iOS (PWA/Safari, con limitaciones de push
  en iOS).
- ❌ Hereda TODOS los problemas de la sección 3.1 (sesión atada a circuito,
  sin offline, reconexión SignalR frágil al cambiar de wifi a datos o volver
  del background -- justo el patrón de uso típico de una app mobile).
- ❌ Push notifications en Android via PWA es posible (Web Push) pero más
  limitado/menos confiable que FCM nativo.
- Uso recomendado: **piloto rápido para validar demanda**, no destino final.

### Opción B -- .NET MAUI Blazor Hybrid
Los mismos componentes Razor (`.razor`) corren embebidos en un WebView LOCAL
dentro de una app MAUI -- sin SignalR, sin servidor persistente: la UI se
renderiza en el dispositivo y llama a una API (o directo a los `Services` si
se linkea el proyecto) por HTTP normal.
- ✅ Reutiliza las páginas Razor existentes con cambios moderados (sacar
  dependencias de `HttpContext`, reemplazar `AuthenticationStateProvider`
  por uno basado en token guardado en el dispositivo).
- ✅ Un solo código para Android e iOS (y Windows/Mac si hiciera falta).
- ✅ Aprovecha que el equipo ya es 100% .NET/Blazor -- no hay que aprender
  Kotlin/Swift ni Flutter.
- ✅ Soporta funciones nativas (cámara para fotos de incidentes, push via
  plugins) mucho mejor que la Opción A.
- ❌ Requiere SÍ construir la API (sección 3.2) -- MAUI Hybrid no habla
  directo con `BDLayout`/SQL Server desde el celular.
- ❌ Curva de aprendizaje de MAUI si el equipo no lo conoce (aunque el
  lenguaje/framework de UI, Blazor, ya lo conocen).

### Opción C -- Nativo (Kotlin para Android, y Swift para iOS más adelante) o Flutter
Reescribir la UI mobile desde cero en Kotlin/Jetpack Compose, consumiendo una
API nueva.
- ✅ Mejor performance/UX nativa posible, mejor integración con el SO.
- ❌ Cero reutilización de UI -- hay que reconstruir cada pantalla.
- ❌ Duplica el trabajo para iOS más adelante (a menos que se use Flutter,
  que comparte UI entre plataformas pero tampoco reutiliza nada de Blazor).
- ❌ Requiere un equipo/skillset que hoy el proyecto no tiene evidencia de
  tener (todo el código es C#/.NET).
- Uso recomendado: sólo si el piloto valida demanda fuerte y se justifica la
  inversión de una app "de verdad", con presupuesto y timeline mayores.

### Recomendación
Dado que se pidió explícitamente **"piloto"** y **"empecemos con Android"**
(con la puerta abierta a iOS después, según la pregunta original):
1. **Piloto inicial: Opción A (PWA/TWA)** -- valida en semanas, no meses, si
   los residentes realmente usan esto desde el celular, con el menor
   compromiso de ingeniería. Tolerar sus limitaciones (reconexión,
   sin push confiable) es aceptable para medir demanda, no para escala.
2. **Si el piloto valida demanda: migrar a Opción B (MAUI Blazor Hybrid)**
   para la versión real -- mismo equipo, mismo lenguaje, reutiliza las
   páginas ya adaptadas del piloto, cubre Android e iOS con una sola base de
   código.
3. **Opción C sólo si en algún punto se necesita la UX nativa tope de
   gama** -- no parece justificado para un piloto ni para el tamaño actual
   del producto.

---

## 6. Qué construir nuevo (para cualquiera de las 3 opciones, en distinto grado)

| Pieza | Necesaria para A (PWA) | Necesaria para B (MAUI) | Necesaria para C (Nativo) |
|---|---|---|---|
| API REST/HTTP sobre los `Services` existentes | No (usa las páginas Blazor tal cual) | Sí | Sí |
| Auth por token (JWT + refresh) | No (sigue con cookie) | Sí | Sí |
| Manifest PWA + wrapper TWA | Sí | No | No |
| Push notifications (FCM) | Limitado (Web Push) | Sí, plugin nativo | Sí, SDK nativo |
| Subida de fotos (incidentes) + storage (Blob/S3) | Sí (ya se puede con `InputFile` en Blazor) | Sí (API + storage) | Sí (API + storage) |
| Pasarela de pago del residente | Igual en cualquier opción -- decisión de producto aparte, no depende de la arquitectura mobile |

---

## 7. Plan de piloto propuesto (fases)

**Fase 0 -- Decisión (antes de escribir código):**
- Confirmar la Opción A como piloto (o ajustar si el usuario prefiere otra).
- Decidir pasalera de pago para el residente (si el pago entra en el piloto
  o queda para después -- se recomienda dejarlo fuera del piloto inicial,
  ver sección 2).
- Si se elige B o C directamente (saltando el piloto PWA), confirmarlo --
  cambia todo el plan de abajo.

**Fase 1 -- Piloto PWA/TWA (Opción A), sólo lectura:**
- Manifest PWA (`manifest.json`, íconos, `theme-color`) sobre las páginas de
  residente existentes.
- Wrapper TWA mínimo para Android (proyecto Android Studio de ~1 archivo,
  Google tiene el tooling (Bubblewrap) para generarlo automático desde el
  manifest).
- Ajustar `MyPayments.razor`/`ViewBudget.razor`/`CalendarPage.razor` para
  layout mobile-first (hoy están pensadas para desktop -- revisar
  responsive).
- Publicar en Play Store como piloto cerrado (o incluso sin Play Store,
  instalación directa del APK del TWA, para launch más rápido).

**Fase 2 -- Reportar incidentes con foto:**
- Agregar columna `PhotoUrl`/tabla `IncidentAttachment` a `Incident`.
- Storage de archivos (decisión: Azure Blob si ya hay cuenta Azure -- no se
  encontró ninguna referencia en el repo, S3, o simple disco local para el
  piloto).
- `InputFile` en `IncidentDetail.razor` (o donde se cree el incidente) +
  endpoint/servicio de subida.

**Fase 3 -- Notificaciones push básicas (Web Push, dentro de las
limitaciones de la Opción A):**
- Nuevo recibo emitido, incidente respondido.

**Fase 4 (si el piloto valida demanda) -- Migrar a Opción B:**
- Construir la API (auth por token + endpoints sobre los `Services`
  existentes).
- Adaptar páginas Razor del piloto a MAUI Blazor Hybrid.
- Push nativo (FCM), cámara nativa para fotos.

**Fase 5 (stretch, en cualquier momento posterior) -- Pago de cuota desde
el celular:** integración de pasarela real, fuera del piloto inicial por
riesgo/esfuerzo.

---

## 8. Preguntas abiertas para el usuario

1. ¿Confirmás la Opción A (PWA/TWA) como punto de partida del piloto, o
   preferís saltar directo a MAUI (Opción B)?
2. ¿El pago de cuota desde el celular es un requisito del piloto, o puede
   esperar a una fase posterior (recomendado)?
3. Para las fotos de incidentes: ¿hay ya alguna cuenta de storage (Azure,
   AWS) del proyecto, o arrancamos con algo simple (disco local) para el
   piloto?
4. ¿Hay timeline/fecha objetivo para el piloto? Cambia cuánto conviene
   invertir en Fase 2/3 antes de salir a producción con usuarios reales.
5. Confirmar iOS: ¿es una intención firme a mediano plazo, o sólo
   "eventualmente"? Si es firme, refuerza la recomendación de migrar a MAUI
   (Opción B) más temprano en vez de invertir tiempo extendiendo la Opción A.
