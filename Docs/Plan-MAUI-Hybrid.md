# Plan de implementación — Opción B: .NET MAUI Blazor Hybrid

Este documento desarrolla en tareas concretas la **Fase 4** de
`Docs/Design-Piloto-Mobile-Android.md` ("Migrar a Opción B", ya decidida el
2026-09-09). Ese documento explica el *por qué* de la arquitectura; este es
el *cómo*, basado en el estado real del código hoy (22-set-2026): la Fase 1
(PWA/TWA) y la Fase 2 (fotos en Incidencias, storage local) ya están
implementadas y en producción -- este plan no las repite, arranca desde ahí.

---

## 0. Punto de partida real (verificado contra el código, no supuesto)

- **Un solo proyecto** (`SpiderHood/SpiderHood.csproj`, `net10.0`, SDK
  `Microsoft.NET.Sdk.Web`) contiene todo: páginas Razor, `Services/*.cs`,
  `BDLayout` (acceso a datos). No hay ningún controller REST (`find -iname
  "*Controller*.cs"` sigue dando cero) ni paquete de JWT instalado.
- **El seam de usuario actual ya está bien encapsulado**: casi todo
  `Services/*.cs` obtiene el usuario actual vía `IAuthService.GetCurrentUserAsync()`
  (que internamente delega a `CustomAuthenticationStateProvider`), no leyendo
  `HttpContext` directo en cada Service. Esto es una buena noticia concreta
  para este plan: la mayoría de la lógica de negocio **no necesita tocarse**
  para funcionar detrás de una API -- sólo hace falta una implementación
  alternativa de `IAuthService`/`AuthenticationStateProvider` que resuelva el
  usuario desde un JWT en vez de la cookie/circuito. `CustomAuthenticationStateProvider.cs`
  es el único archivo que sí necesita una hermana nueva para el mundo API/MAUI.
- **Entorno de este contenedor de desarrollo (Linux)**: no tiene el workload
  `maui-android` instalado (`dotnet workload list` -- vacío) ni Android
  SDK/emulador. **Se puede** instalar y compilar Android desde Linux (el
  workload MAUI-Android de .NET 8+ corre en Linux); **no se puede** compilar
  ni firmar la app iOS desde acá -- eso siempre requiere una Mac (local o
  builder en la nube tipo App Center/Codemagic) emparejada por red o USB.
  Esto no cambia la arquitectura, pero sí decide en qué máquina se hace cada
  parte del trabajo (ver sección 6).

---

## 1. Arquitectura objetivo

```
┌─────────────────────────┐        ┌──────────────────────────┐
│   SpiderHood (web)      │        │   SpiderHood.Maui         │
│   Blazor Server actual  │        │   MAUI Blazor Hybrid       │
│   (sin cambios de fondo)│        │   (WebView LOCAL, sin      │
│                          │        │    SignalR)                │
│  Components/Pages/...   │        │  Referencia a la RCL de    │
│  (Admin + Residente)    │        │  abajo, HttpClient + JWT    │
└─────────────┬────────────┘        └───────────┬────────────────┘
              │ usa directo                      │ HTTP/JSON (nuevo)
              ▼                                  ▼
┌───────────────────────────────┐    ┌──────────────────────────┐
│  SpiderHood.Resident.UI (RCL) │    │   SpiderHood.Api (nuevo)  │
│  Razor Class Library nueva --  │    │   ASP.NET Core Web API    │
│  sólo las páginas de Residente │    │   JWT auth, controllers   │
│  del piloto (MyPayments,       │    │   delgados sobre los       │
│  ViewBudget, Calendar,         │    │   Services existentes      │
│  Incidents), referenciada      │    └───────────┬────────────────┘
│  por ambos proyectos           │                │ mismo código
└────────────┬────────────────────┘                ▼
             │ referenciada también             ┌──────────────────────────┐
             └────────────────────────────────▶ │  Services/*.cs + BDLayout │
                                                  │  (SIN CAMBIOS DE FONDO)   │
                                                  └──────────────────────────┘
```

La app web sigue siendo Blazor Server tal cual (nadie migra el panel de
Administrador -- eso no es parte de ningún piloto mobile, sección 2 del
documento de diseño). Sólo las pantallas de Residente se extraen a una
librería compartida para no duplicar UI entre web y MAUI.

---

## 2. Fases y tareas concretas

### Fase 4.0 -- Entorno e infraestructura (antes de escribir código nuevo)
1. Instalar el workload MAUI (Android primero): `dotnet workload install
   maui-android` + Android SDK/JDK (puede hacerse en este mismo contenedor
   Linux). Confirmar `dotnet build -f net10.0-android` compila un proyecto
   MAUI vacío antes de avanzar.
2. Decidir dónde se compila/prueba iOS (Mac física del equipo, o un servicio
   de build en la nube tipo Codemagic/App Center que empareja con GitHub) --
   **pregunta abierta #5 de `Design-Piloto-Mobile-Android.md`, sigue sin
   respuesta**: si iOS es intención firme a mediano plazo, este paso hay que
   resolverlo ahora, no al final.
3. Crear un emulador Android local (o usar un dispositivo físico de prueba)
   para poder iterar sin depender de Play Store en cada build.

### Fase 4.1 -- API REST sobre los Services existentes (`SpiderHood.Api`, proyecto nuevo)
Alcance: sólo los endpoints que el piloto necesita (las mismas 4 pantallas
+ auth), no una API genérica de todo el sistema.
1. Nuevo proyecto `SpiderHood.Api` (`Microsoft.NET.Sdk.Web`, `net10.0`),
   referenciando el proyecto `SpiderHood` para reusar `Services/*.cs`,
   `Classes/*.cs` y `Data/BDLayout*.cs` tal cual (mismo `IServiceCollection`
   de inyección de dependencias que ya arma `Program.cs`, extraído a un
   método `AddSpiderHoodServices()` compartido si hoy vive todo inline en
   `Program.cs` -- revisar y refactorizar sólo lo necesario para que ambos
   `Program.cs` (web y API) lo llamen sin duplicar el registro de cada
   Service uno por uno).
2. **Auth por JWT**: paquete `Microsoft.AspNetCore.Authentication.JwtBearer`
   (no instalado hoy). Endpoint `POST /api/auth/login` (usuario/clave, mismo
   `IAuthService` de validación de credenciales que ya existe) que devuelve
   `{ accessToken, refreshToken, expiresIn }`. Claims mínimos: `sub` (IdUser),
   `role`, `idBuilding` (edificio activo), y lo que hoy arma
   `CustomAuthenticationStateProvider` para el `UserSession` en memoria.
   Refresh token con expiración larga (semanas, acorde a expectativa de app
   mobile) guardado hasheado en BD (tabla nueva, o reusar el patrón de
   `ISessionRevocationService` si aplica) para poder revocar sesiones mobile
   igual que ya se puede revocar la web.
3. **Nueva implementación de `IAuthService`/`AuthenticationStateProvider`**
   para el contexto API: en vez de leer la cookie/circuito, valida el JWT
   (vía middleware estándar de ASP.NET) y arma el mismo `UserSession` que la
   versión web -- éste es el punto exacto donde se conecta el "seam" ya
   identificado en la sección 0, sin tocar el resto de `Services/*.cs`.
4. **Controllers delgados** (uno por pantalla del piloto, llaman directo al
   Service existente, no reimplementan lógica):
   - `GET /api/me/installments` → `IInstallmentService` (equivalente a
     `MyPayments.razor`).
   - `GET /api/buildings/{id}/budget` → `IBudgetService` (equivalente a
     `ViewBudget.razor`).
   - `GET /api/me/calendar` / reservas de áreas comunes en solo lectura.
   - `GET/POST /api/incidents`, `POST /api/incidents/{id}/attachments`
     (multipart) → `IIncidentService` + `IFileStorageService` (ya
     implementado para el piloto PWA, reutilizable tal cual -- ver #18a/18b
     en `Docs/Pendientes-Negocio-Consolidado.md`).
5. DTOs livianos por endpoint (no exponer `Classes/*.cs` completos --
   varios tienen campos `[NotMapped]` de UI que no deberían viajar por la
   API, según ya lo advierte la sección 4 del documento de diseño).
6. Swagger/OpenAPI para poder probar cada endpoint sin esperar a que el
   cliente MAUI exista.

### Fase 4.2 -- Extraer la Razor Class Library compartida (`SpiderHood.Resident.UI`)
1. Nuevo proyecto `Microsoft.NET.Sdk.Razor` (RCL), `net10.0`.
2. Mover (no copiar) las páginas de Residente ya ajustadas en la Fase 1/2 del
   piloto PWA: `MyPayments.razor`, `ViewBudget.razor`, `CalendarPage.razor`,
   las páginas de Incidencias usadas por Residente/Junta (`/incidents`,
   modal "Nuevo Incidente" con `StagedAttachment`).
3. Sacar cualquier dependencia directa de `HttpContext`/`IHttpContextAccessor`
   de esas páginas si la tuvieran (la mayoría no debería, ya llaman a
   `Services` vía inyección) -- reemplazar cualquier acceso a Service local
   por llamadas a un `HttpClient` tipado (`ResidentApiClient`) que hable con
   `SpiderHood.Api`. **Esto es el cambio real de esta fase**: hoy esas
   páginas llaman al Service in-process; después de esta fase, llaman a un
   cliente HTTP -- mismo contrato de método (`Task<List<Installment>>
   GetMyInstallmentsAsync()`), implementación distinta por detrás según el
   host (web sigue in-process, MAUI usa HTTP).
4. El proyecto `SpiderHood` (web) referencia esta RCL y sigue sirviendo esas
   páginas exactamente igual que hoy a los usuarios de browser/PWA -- cero
   regresión para el piloto ya publicado.

### Fase 4.3 -- Proyecto MAUI Blazor Hybrid (`SpiderHood.Maui`)
1. Nuevo proyecto MAUI Blazor Hybrid (plantilla estándar `dotnet new
   maui-blazor`), Android primero (`net10.0-android`), iOS agregado como
   segundo target cuando la Fase 4.0 punto 2 esté resuelta.
2. Referencia a `SpiderHood.Resident.UI` (RCL) -- las pantallas se muestran
   dentro de un `BlazorWebView` local, sin servidor.
3. `AuthenticationStateProvider` propio para MAUI: login contra
   `POST /api/auth/login`, guarda el JWT/refresh token en
   `Microsoft.Maui.Storage.SecureStorage` (no en un archivo plano ni en
   memoria -- es lo que MAUI ofrece para credenciales sensibles en el
   dispositivo), agrega el `Authorization: Bearer` a cada request del
   `HttpClient` tipado.
4. **Cámara nativa para fotos de incidentes**: `MediaPicker.CapturePhotoAsync()`
   (paquete `Microsoft.Maui.Essentials`, incluido en la plantilla MAUI) en
   vez del `InputFile` de Blazor que usa hoy la versión web -- mismo
   `StagedAttachment` en memoria antes de subir, mismo criterio de "adjuntar
   como parte del formulario de reporte, subir recién al confirmar" ya
   decidido y documentado en la Fase 2 del documento de diseño.
5. Manejo de reconexión/offline básico: a diferencia de Blazor Server, acá no
   hay circuito que se caiga -- pero sí hay que manejar "sin internet" al
   llamar a la API (mostrar cache de la última respuesta conocida para "Mis
   cuotas"/"Presupuesto", con aviso de que puede estar desactualizada; no es
   offline real, es tolerancia a falta de red momentánea).

### Fase 4.4 -- Push nativo (Firebase Cloud Messaging)
1. Paquete `Plugin.Firebase` (o el SDK oficial de Firebase para .NET MAUI) en
   `SpiderHood.Maui`. Requiere una cuenta/proyecto de Firebase (decisión de
   producto, no técnica -- confirmar quién la crea y con qué dominio).
2. Endpoint nuevo en `SpiderHood.Api`: `POST /api/me/device-token` para que
   la app registre su token FCM al loguearse.
3. Trigger del lado servidor: cuando se emite un recibo nuevo o se responde
   un incidente (mismos eventos que ya dispara el email/WhatsApp existente,
   `Services/IEmailService.cs`/Twilio -- reusar el mismo punto de disparo,
   sólo agregar el envío push ahí) se llama a la API de FCM con el token
   guardado.

### Fase 4.5 -- Testing y distribución
1. Emulador/dispositivo Android local para todo el ciclo de desarrollo.
2. Build firmado (`keystore` de release) + subida a Play Store como piloto
   cerrado (internal testing track), o APK directo firmado para instalación
   manual si se quiere saltar el paso de Play Store para iterar más rápido
   al principio (mismo criterio que ya se usó para el TWA de la Fase 1).
3. iOS: requiere cuenta de Apple Developer (si no existe ya, es otra
   decisión de producto a resolver temprano) + certificado/perfil de
   provisioning + build en Mac o servicio de CI en la nube.

---

## 3. Qué NO se toca en este plan (alcance explícito)

- El panel de Administrador (Blazor Server web) sigue exactamente igual --
  no hay ningún plan de llevarlo a MAUI.
- Pago de cuota desde el celular sigue **fuera de alcance** (Fase 5,
  stretch), como ya estaba decidido en el documento de diseño -- este plan
  no agrega una pasarela de pago.
- No se reescribe `BDLayout` ni las stored procedures -- la API las llama
  exactamente igual que hoy las llama la web.

---

## 4. Decisiones que siguen abiertas (no bloquean empezar la Fase 4.0/4.1, sí bloquean 4.0.2/4.4/4.5)

Repetidas del documento de diseño porque nadie las contestó todavía:

1. **iOS: ¿intención firme a mediano plazo, o "eventualmente"?** Si es firme,
   conviene resolver ya la máquina/servicio de build (Mac o CI en la nube)
   en vez de dejarlo para el final de la Fase 4.
2. **Firebase**: ¿se crea un proyecto Firebase nuevo para SpiderHood, o ya
   existe uno de otra parte del negocio que se pueda reusar?
3. **Apple Developer Program** (US$ 99/año): sólo aplica si se confirma
   iOS -- necesario para firmar y publicar en App Store.
4. **Play Store**: ¿se publica como app real (requiere cuenta de developer,
   US$ 25 una vez) o se sigue distribuyendo el APK manualmente por ahora,
   como se dejó abierto para el TWA de la Fase 1?
5. **Timeline objetivo** (pregunta #4 del documento de diseño, seguía sin
   respuesta): cambia cuánto conviene invertir en Fase 4.4 (push) antes de
   salir con usuarios reales -- es la fase más fácil de dejar para después
   sin bloquear el resto.

---

## 5. Estimación de esfuerzo (orden de magnitud, no compromiso de fecha)

| Fase | Esfuerzo aprox. | Bloqueado por |
|---|---|---|
| 4.0 Entorno | 1-2 días | -- |
| 4.1 API + JWT | 1-1.5 semanas | 4.0 |
| 4.2 RCL compartida | 3-5 días | Puede correr en paralelo a 4.1 |
| 4.3 App MAUI (Android) | 1-1.5 semanas | 4.1 + 4.2 |
| 4.4 Push (FCM) | 3-4 días | 4.3 + decisión Firebase (sección 4.2) |
| 4.5 Testing y distribución Android | 2-3 días | 4.3 |
| iOS (build + ajustes de plataforma) | 1 semana adicional | Decisión iOS (sección 4.1) + máquina/CI Mac |

**Total Android-only, sin push:** ~3-3.5 semanas de desarrollo efectivo.
**Con push y iOS:** ~5-6 semanas, más el tiempo de aprobación de Apple/Google
para publicar (días a semanas, fuera del control del equipo).

---

## 6. Qué se puede hacer ya en este contenedor Linux vs. qué necesita otra máquina

| Tarea | Este entorno (Linux, sin GUI) | Requiere otra máquina |
|---|---|---|
| `SpiderHood.Api` (proyecto ASP.NET Core normal) | Sí, completo | -- |
| `SpiderHood.Resident.UI` (RCL) | Sí, completo | -- |
| `SpiderHood.Maui` -- compilar para Android | Sí (`dotnet workload install maui-android` + Android SDK) | -- |
| Probar la app Android en un emulador/dispositivo | Parcial -- sin GUI en este contenedor no hay forma de ver la pantalla; se puede compilar y correr headless/instrumentado, pero la verificación visual real necesita una máquina con GUI o un dispositivo físico conectado | Sí, para pruebas visuales/manuales |
| Compilar/firmar la app **iOS** | No | Sí, siempre (Mac local o CI en la nube) |
| Publicar en Play Store / App Store | No (requiere consola web + cuentas de developer) | Sí |

**Recomendación práctica de arranque:** las Fases 4.1 y 4.2 (API + RCL) se
pueden avanzar por completo en este entorno ahora mismo, sin esperar
ninguna decisión de infraestructura mobile. La Fase 4.3 (proyecto MAUI en
sí) también puede empezar a compilarse acá una vez instalado el workload
Android, aunque la verificación visual final va a necesitar un dispositivo
o emulador con pantalla en algún punto.

---

## 7. Impacto en el día a día del piloto: observaciones, fixes y funcionalidad nueva

Pregunta real del usuario (2026-09-22): si el piloto ya está corriendo y
aparecen observaciones que exigen tocar pantallas y/o lógica, ¿cómo afecta
eso a esta migración? La respuesta depende de en qué momento del plan y de
qué parte de la app se trate.

### Mientras se construyen las Fases 4.0-4.2 (API + RCL) -- impacto cero
`SpiderHood.Api` y `SpiderHood.Resident.UI` son proyectos **nuevos**, al
lado del proyecto `SpiderHood` que corre hoy en producción -- no lo tocan.
Cualquier observación sobre cualquier pantalla (Residente o Administrador)
se corrige exactamente como hasta ahora, sin fricción nueva, mientras este
plan avanza en paralelo.

La única excepción es la ventana corta de la **Fase 4.2** en sí misma: ahí
las 4 pantallas de Residente del piloto se **mueven** (no se copian) del
proyecto `SpiderHood` a la librería compartida. Si justo en esos días
aparece una observación sobre una de esas 4 pantallas puntuales, conviene
esperar a que termine el move (días, no semanas) antes de tocarla, para no
corregir el mismo código dos veces a mitad de una extracción mecánica. El
resto de la app (todo Administrador) nunca pasa por esta librería, así que
no tiene ninguna ventana de conflicto.

### Una vez que la app MAUI ya está instalada en celulares de usuarios reales
Acá aparece una asimetría de fondo entre web y mobile que conviene tener
clara para planificar el ritmo de publicaciones, no sólo para el día del
lanzamiento:

- **Lógica de negocio de una pantalla ya compartida** (una de las 4 del
  piloto): se corrige **una sola vez** en `SpiderHood.Resident.UI` y queda
  arreglada para los dos lados -- no hay versión duplicada que mantener
  sincronizada.
- **Pero la velocidad de entrega es distinta.** En la web el fix se
  despliega y ya está, todos los usuarios lo ven en el próximo refresh. En
  la app MAUI el fix vive **embebido dentro del binario** instalado en el
  celular -- para que le llegue a un usuario que ya tiene la app, hace
  falta compilar una versión nueva y publicarla (Play Store/App Store, o un
  APK nuevo si se sigue distribuyendo manual) **y que el usuario la
  actualice**. No hay forma de "empujar" un cambio a un celular sin pasar
  por ese ciclo.
- **Ajustes de layout/UX** (no de lógica) pueden necesitar tocarse aparte:
  una pantalla pensada para mouse/pantalla grande a veces necesita su
  propio ajuste de touch/pantalla chica -- acotado a diseño, nunca a la
  lógica de negocio (esa siempre es una sola copia).

### Agregar funcionalidad nueva de Administrador al Móvil, más adelante -- **mismo ciclo que un fix, confirmado**
El usuario preguntó explícitamente si esto aplica igual cuando, en vez de
un fix, se decida **agregar** una pantalla de Administrador nueva al
celular (hoy fuera de alcance, sección 3). **Sí, exactamente el mismo
mecanismo:** cualquier pantalla nueva que se agregue a `SpiderHood.Maui`
(sea de Residente o, si algún día se decide, de Administrador) también
queda embebida en el binario de la app -- no existe, para ningún tipo de
pantalla, una forma de que aparezca en el celular de un usuario sin
publicar una versión nueva de la app y que la actualice. La única
diferencia entre "agregar algo nuevo" y "corregir algo existente" es el
tamaño del cambio, no el mecanismo de entrega -- los dos casos requieren:
1. Compilar y probar la nueva versión de `SpiderHood.Maui`.
2. Publicarla (subida a la tienda con su proceso de revisión de
   Google/Apple -- días, a veces más -- o redistribuir el APK si se sigue
   por esa vía).
3. Esperar a que cada usuario actualice -- en Android/iOS esto suele ser
   automático si el usuario tiene las actualizaciones automáticas
   activadas, pero no es instantáneo ni está garantizado para el 100% de
   los usuarios el mismo día.

**Implicancia práctica para planificar el ritmo de publicaciones:** conviene
agrupar los cambios de pantallas compartidas o de Administrador-en-mobile en
lanzamientos, en vez de publicar una versión nueva de la app cada vez que
aparece una observación chica -- lo contrario a la web, donde cada fix se
puede desplegar de inmediato sin ese costo. Esto no es un problema del plan
en sí, es una característica inherente de cualquier app nativa/MAUI Hybrid
(no sería distinto con la Opción C tampoco) -- es parte de lo que hay que
aceptar como costo de tener push nativo, cámara nativa y mejor UX que la
Opción A (PWA), que sí se actualiza sola porque nunca deja de ser,
técnicamente, un sitio web.
