using Blazored.LocalStorage;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SpiderHood.Components;
using SpiderHood.Data;
using MercadoPago.Client.Preapproval;
using MercadoPago.Config;
using SpiderHood.Services;
using SpiderHood.Services.Logging;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContextFactory<SpiderHoodContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("SpiderHoodContext") ?? throw new InvalidOperationException("Connection string 'SpiderHoodContext' not found."),
        // Default de SqlClient es 30s -- Docs/Pendientes-Negocio-Migracion.md #6.6: con
        // rangos de fecha amplios (ej. Conciliación con todo el histórico migrado de un
        // edificio, 2015-2026) algunas consultas legítimamente tardan más que eso. Sube el
        // límite a todas las consultas/SPs que pasan por BDLayout (ExecuteQueryListAsync /
        // ExecuteStoredProcedureAsync comparten el mismo DbContext), no sólo a esa pantalla
        // -- no hay forma de fijarlo por consulta sin tocar cada método de BDLayout.Get.cs.
        sqlOptions => sqlOptions.CommandTimeout(120)));

builder.Services.AddQuickGridEntityFrameworkAdapter();
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// Blazor
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddBlazorBootstrap();

// ✅ Blazored LocalStorage (ESTO ES LO IMPORTANTE)
builder.Services.AddBlazoredLocalStorage();

// Authentication
//
// La sesión ya NO se persiste en localStorage (ver CustomAuthenticationStateProvider):
// la identidad viaja en una cookie de autenticación HttpOnly emitida por
// HttpContext.SignInAsync (Login.razor) y leída por el middleware de abajo antes de
// que se arme el árbol de componentes. localStorage queda para preferencias (tema,
// edificio por defecto, sidebar) — ver AuthService.SetDefaultBuildingAsync y afines.
builder.Services.AddHttpContextAccessor();

// Lista de revocación de sesiones (en memoria) — ver ISessionRevocationService. Tiene
// que ser Singleton: la consultan requests de circuitos y usuarios distintos, y tiene
// que sobrevivir más que el scope de un solo circuito.
builder.Services.AddSingleton<ISessionRevocationService, SessionRevocationService>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.Name = "SpiderHood.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;

        // Sin esto, una cookie ya emitida sigue siendo válida hasta que expira sola —
        // cambiar la contraseña o pedir "cerrar sesión en todos los dispositivos" no
        // invalidaba ninguna sesión ya abierta (la tuya en otro navegador, o una
        // robada). Esto se ejecuta en cada request autenticado (no en cada interacción
        // dentro de un circuito ya conectado, que viaja por SignalR) y reimplementa a
        // mano el patrón de SecurityStampValidator de ASP.NET Core Identity, porque acá
        // el login no pasa por Identity (usa UserModel propio vía AuthService).
        options.Events = new CookieAuthenticationEvents
        {
            OnValidatePrincipal = async context =>
            {
                var userIdClaim = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!Guid.TryParse(userIdClaim, out var userId))
                {
                    context.RejectPrincipal();
                    return;
                }

                var revocation = context.HttpContext.RequestServices.GetRequiredService<ISessionRevocationService>();
                var issuedUtc = context.Properties.IssuedUtc ?? DateTimeOffset.MinValue;

                if (revocation.IsRevoked(userId, issuedUtc))
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    return;
                }

                // No se re-chequea el hash de contraseña en CADA request (sería un
                // round-trip a la base de datos por cada carga de página) — sólo cada
                // pocos minutos, guardando cuándo fue la última vez dentro de la misma
                // cookie (igual que SecurityStampValidator).
                var lastCheckedRaw = context.Properties.Items.TryGetValue("stamp_checked_at", out var raw) ? raw : null;
                var lastChecked = lastCheckedRaw != null && DateTimeOffset.TryParse(lastCheckedRaw, out var parsed)
                    ? parsed
                    : DateTimeOffset.MinValue;

                if (DateTimeOffset.UtcNow - lastChecked < TimeSpan.FromMinutes(5))
                {
                    return;
                }

                var authService = context.HttpContext.RequestServices.GetRequiredService<AuthService>();
                var currentStamp = context.Principal!.FindFirst("security_stamp")?.Value;
                var freshStamp = await authService.GetSecurityStampAsync(userId);

                if (freshStamp == null || !string.Equals(currentStamp, freshStamp, StringComparison.Ordinal))
                {
                    // La contraseña cambió (o el usuario ya no existe) después de que se
                    // emitió esta cookie — invalidarla.
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    return;
                }

                context.Properties.Items["stamp_checked_at"] = DateTimeOffset.UtcNow.ToString("O");
                context.ShouldRenew = true;
            }
        };
    });

builder.Services.AddScoped<IUserSessionLoader, UserSessionLoader>();
builder.Services.AddScoped<CustomAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp =>
    sp.GetRequiredService<CustomAuthenticationStateProvider>());

builder.Services.AddScoped<AuthService>();
builder.Services.AddCascadingAuthenticationState();

// "Seguro por defecto": ninguna página de Components/Pages tenía [Authorize] (se
// confirmó revisando el proyecto entero) -- AuthorizeRouteView sólo redirige a
// /login cuando la página TIENE un requisito de autorización explícito, así que en
// la práctica CUALQUIER ruta (/buildings, /Owners, /Settings/UserRoles, etc.) era
// alcanzable sin sesión con solo escribir la URL directamente (confirmado por el
// usuario navegando como "Invitado"). Las únicas protecciones que existían eran
// checks sueltos dentro de cada página (ej. _canManageUsers), que muchas páginas
// simplemente no tenían. Este FallbackPolicy exige sesión para TODO por defecto;
// las páginas realmente públicas (login, registro, invitación, confirmación de
// email, error/not-found, resultado de pago) se marcan explícitamente con
// [AllowAnonymous]. El webhook de MercadoPago y los archivos estáticos
// (CSS/JS/imágenes) se eximen aparte más abajo, donde se registran sus endpoints.
builder.Services.AddAuthorizationCore(options =>
{
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// 2. Identity
//
// NOTA: esto se mantiene registrado porque `IEmailConfirmationService`
// (usado por Confirmemail.razor) inyecta `UserManager<IdentityUser>` — si se
// quita esto, esa página se rompe al cargar (DI no puede resolver
// UserManager<IdentityUser>).
//
// Pero ojo: el login real de la app NO pasa por aquí — `AuthService.LoginAsync`
// usa su propio `UserModel` (tabla propia, vía BDLayout/Dapper), no
// `SignInManager`/`UserManager`. Y nadie en el código llama a
// `UserManager.CreateAsync()`, así que nunca se crea un `IdentityUser` real
// en la tabla `AspNetUsers`. Eso significa que `Confirmemail.razor` /
// `IEmailConfirmationService` (que sí buscan un `IdentityUser` por id/email)
// en la práctica nunca van a encontrar nada — es un flujo que compila pero
// no funciona de verdad, porque le falta la mitad (crear el usuario).
// Documentado así para no volver a confundirlo con configuración muerta.
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedEmail = true;
})
.AddEntityFrameworkStores<SpiderHoodContext>()
.AddDefaultTokenProviders();

// AddIdentity(...) de arriba registra sus propios esquemas de cookie (uno para
// IdentityConstants.ApplicationScheme, etc.) y pone SU cookie como default — lo que
// pisaría nuestra cookie de sesión si no se corrige. PostConfigure corre siempre
// DESPUÉS de todos los Configure<AuthenticationOptions> (sin importar el orden de
// registro), así que esto garantiza que el default real sea nuestro esquema.
builder.Services.PostConfigure<Microsoft.AspNetCore.Authentication.AuthenticationOptions>(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
});

// Otros servicios
builder.Services.AddScoped<ParameterService>();
builder.Services.AddScoped<IParameterPromotionService, ParameterPromotionService>();
builder.Services.AddScoped<IBankAccountService, BankAccountService>();
builder.Services.AddScoped<IBudgetService, BudgetService>();
builder.Services.AddScoped<IExpenseService, ExpenseService>();
builder.Services.AddScoped<ICalculoService, CalculoService>();
builder.Services.AddScoped<IExceptionService, ExceptionService>();
builder.Services.AddScoped<IPeriodService, PeriodService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IOwnerService, OwnerService>();
builder.Services.AddScoped<IInstallmentService, InstallmentService>();
builder.Services.AddScoped<IMonthlyInstallmentService, MonthlyInstallmentService>();
builder.Services.AddScoped<IPendingExpenseService, PendingExpenseService>();
builder.Services.AddScoped<IBuildingService, BuildingService>();
builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IAccountService, AccountService>();

// MercadoPago (Docs/Design-Subscripcion-Administrador.md): las claves NUNCA
// van commiteadas -- appsettings.json trae "MercadoPago" vacío a propósito, el
// valor real se setea con `dotnet user-secrets` en desarrollo (o una variable
// de entorno MercadoPago__AccessToken en el servidor real).
MercadoPagoConfig.AccessToken = builder.Configuration["MercadoPago:AccessToken"];
builder.Services.AddScoped<IWorkflowService, WorkflowService>();
builder.Services.AddScoped<IWorkflowAuditService, WorkflowAuditService>();
builder.Services.AddScoped<IIncidentService, IncidentService>();
builder.Services.AddScoped<ICalendarService, CalendarService>();

// Logs de sistema: sink a BD (Singleton, ver DatabaseLoggerProvider) + purga diaria por
// retención (Singleton, vive mientras vive el host). Apagado por defecto -- se habilita
// desde /Settings/SystemLogs (Super Usuario / SysAdmin).
builder.Services.AddSingleton<ILoggerProvider, DatabaseLoggerProvider>();
builder.Services.AddHostedService<SystemLogPurgeService>();
builder.Services.AddScoped<ISystemLogAdminService, SystemLogAdminService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IWhatsAppService, WhatsAppService>();
builder.Services.AddScoped<IComunicadoService, ComunicadoService>();
builder.Services.AddScoped<IAreaComunService, AreaComunService>();
builder.Services.AddScoped<IReservaService, ReservaService>();
builder.Services.AddSingleton<IFileStorageService, LocalFileStorageService>();
builder.Services.AddScoped<IReceiptStorageService, ReceiptStorageService>();
builder.Services.AddScoped<IEmailConfirmationService, EmailConfirmationService>();
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddScoped<IPermissionAdminService, PermissionAdminService>();
builder.Services.AddScoped<IMenuAdminService, MenuAdminService>();
builder.Services.AddScoped<IServiceReadingService, ServiceReadingService>();
builder.Services.AddScoped<IExpenseTemplateService, ExpenseTemplateService>();
builder.Services.AddScoped<IExtraChargeService, ExtraChargeService>();
//builder.Services.AddScoped<IFinancialService, FinancialService>();
//builder.Services.AddScoped<ISessionService, SessionService>();
builder.Services.AddScoped<IPreferenceService, PreferenceService>();
builder.Services.AddScoped<IMigrationTemplateService, MigrationTemplateService>();
builder.Services.AddScoped<IMigrationImportService, MigrationImportService>();
builder.Services.AddHttpClient();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAuthentication();

// Landing pública (wwwroot/index.html) en "/" -- pero SÓLO para quien no tiene
// sesión iniciada. Home.razor (@page "/") sigue siendo el Dashboard para
// cualquier usuario ya autenticado que visite "/" -- varios links internos
// ("Volver al inicio", breadcrumbs, y posiblemente el ítem "Dashboard" del
// menú lateral, que viene de MenuItems en BD) asumen exactamente eso, así que
// tocar la ruta de Home.razor rompía más de lo que arreglaba.
//
// Tiene que ir DESPUÉS de UseAuthentication() (necesita ctx.User ya resuelto
// desde la cookie para distinguir logueado/anónimo) pero ANTES de
// UseAuthorization() -- si no, el FallbackPolicy (RequireAuthenticatedUser)
// agregado para "seguro por defecto" intercepta primero cualquier request
// anónimo a "/" con un 302 a /login?ReturnUrl=%2F, porque Home.razor (el
// único componente registrado en esa ruta) no tiene [AllowAnonymous] --
// y este branch nunca llegaba a ejecutarse (bug real, encontrado por el
// usuario: entrando a "/" sin sesión, redirigía derecho a /login en vez de
// mostrar la landing pública).
app.MapWhen(
    ctx => ctx.Request.Path == "/" && ctx.User.Identity?.IsAuthenticated != true,
    branch => branch.Run(async ctx =>
    {
        ctx.Response.ContentType = "text/html";
        await ctx.Response.SendFileAsync(Path.Combine(app.Environment.WebRootPath, "index.html"));
    }));

app.UseAuthorization();
app.UseAntiforgery();

// Webhook de MercadoPago (Docs/Design-Subscripcion-Administrador.md): confirma
// la suscripción del lado del servidor y recién ahí la activa -- nunca desde
// el redirect de éxito del navegador (BackUrl), que no es confiable (el
// usuario puede cerrar la pestaña antes de que cargue). Público a propósito --
// MercadoPago llama sin cookie de sesión; la seguridad la da la firma (header
// x-signature) verificada contra MercadoPago:WebhookSecret, no autenticación
// de usuario. Para probarlo en local hace falta exponer el puerto con un túnel
// (ngrok/Cloudflare Tunnel) y registrar esa URL en el Dashboard de MercadoPago
// -- a diferencia de Stripe, no hay una CLI que reenvíe directo a localhost.
app.MapPost("/api/mercadopago/webhook", async (HttpRequest request, ISubscriptionService subscriptionService, IConfiguration configuration, ILogger<Program> logger) =>
{
    var dataId = request.Query["data.id"].ToString();
    var type = request.Query["type"].ToString();
    var requestId = request.Headers["x-request-id"].ToString();
    var signatureHeader = request.Headers["x-signature"].ToString();
    var webhookSecret = configuration["MercadoPago:WebhookSecret"];

    if (!IsValidMercadoPagoSignature(signatureHeader, requestId, dataId, webhookSecret))
    {
        // Diagnóstico temporal -- ver Docs/Design-Subscripcion-Administrador.md.
        // No loguea el WebhookSecret en sí, sólo si está configurado o no.
        logger.LogWarning(
            "Webhook de MercadoPago: firma inválida. QueryString={QueryString} x-signature={Signature} x-request-id={RequestId} WebhookSecret configurado={HasSecret}",
            request.QueryString, signatureHeader, requestId, !string.IsNullOrEmpty(webhookSecret));
        return Results.BadRequest();
    }

    if (type == "subscription_preapproval" && !string.IsNullOrEmpty(dataId))
    {
        // No se confía en el body de la notificación -- se vuelve a pedir el
        // recurso a la API con el Id, que es la fuente confiable de verdad.
        var preapproval = await new PreapprovalClient().GetAsync(dataId);
        if (preapproval.Status == "authorized" &&
            TryParseExternalReference(preapproval.ExternalReference, out var idUser, out var idSubscriptionPlan))
        {
            await subscriptionService.ActivateSubscriptionAsync(idUser, idSubscriptionPlan, preapproval.Id);
        }
        else
        {
            logger.LogInformation("Webhook de MercadoPago: preapproval {Id} con status {Status} ignorado", preapproval.Id, preapproval.Status);
        }
    }
    else
    {
        logger.LogInformation("Webhook de MercadoPago: evento {Type} ignorado (no es subscription_preapproval)", type);
    }

    return Results.Ok();
}).AllowAnonymous(); // MercadoPago llama sin cookie -- ver el comentario de arriba.

// Formulario de contacto de la landing pública (wwwroot/index.html, sección
// #contacto) -- un <form method="post" action="/api/contacto"> plano, sin
// JS/fetch, así que el navegador hace un POST normal application/x-www-form-
// urlencoded. AllowAnonymous porque lo llena gente sin sesión; DisableAntiforgery
// porque el HTML es estático (SendFileAsync, no un componente Razor) y no tiene
// forma de incrustar un token -- igual que el webhook de MercadoPago de arriba,
// la superficie pública es aceptable acá: en el peor caso es spam, no hay ninguna
// acción sobre datos de un usuario autenticado detrás de este endpoint.
// El campo "_empresa" es un honeypot (input oculto vía CSS, invisible para una
// persona real) -- si viene lleno, se responde OK sin enviar nada, para no
// pelear con captchas por unos bots de formulario.
app.MapPost("/api/contacto", async (HttpRequest request, IEmailService emailService, IConfiguration configuration, ILogger<Program> logger) =>
{
    var form = await request.ReadFormAsync();
    string Campo(string nombre) => form[nombre].ToString().Trim();

    if (!string.IsNullOrEmpty(Campo("_empresa")))
    {
        return Results.Redirect("/?contacto=ok");
    }

    var nombre = Campo("nombre");
    var email = Campo("email");
    var telefono = Campo("telefono");
    var comunidad = Campo("comunidad");
    var interes = Campo("interes");
    var mensaje = Campo("mensaje");

    if (string.IsNullOrEmpty(nombre) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(mensaje)
        || !await emailService.IsValidEmailAsync(email))
    {
        return Results.Redirect("/?contacto=error");
    }

    // Sin un "buzón de ventas" propio todavía -- llega al mismo correo desde el
    // que la app manda el resto de sus notificaciones (Email:SmtpUser), salvo
    // que se configure explícitamente Email:ContactRecipient más adelante.
    var destinatario = configuration["Email:ContactRecipient"] ?? configuration["Email:SmtpUser"];
    if (string.IsNullOrEmpty(destinatario))
    {
        logger.LogWarning("Formulario de contacto: no hay Email:ContactRecipient ni Email:SmtpUser configurado, no se puede enviar.");
        return Results.Redirect("/?contacto=error");
    }

    var cuerpo = $"""
        <p>Nuevo mensaje desde el formulario de contacto de la landing:</p>
        <ul>
          <li><strong>Nombre:</strong> {WebUtility.HtmlEncode(nombre)}</li>
          <li><strong>Email:</strong> {WebUtility.HtmlEncode(email)}</li>
          <li><strong>Teléfono:</strong> {WebUtility.HtmlEncode(telefono)}</li>
          <li><strong>Comunidad:</strong> {WebUtility.HtmlEncode(comunidad)}</li>
          <li><strong>Interés:</strong> {WebUtility.HtmlEncode(interes)}</li>
        </ul>
        <p><strong>Mensaje:</strong></p>
        <p>{WebUtility.HtmlEncode(mensaje).Replace("\n", "<br>")}</p>
        """;

    try
    {
        await emailService.SendEmailAsync(destinatario, $"Nuevo contacto SpiderHood: {nombre}", cuerpo);
        return Results.Redirect("/?contacto=ok");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error enviando el formulario de contacto de la landing.");
        return Results.Redirect("/?contacto=error");
    }
}).AllowAnonymous().DisableAntiforgery();

// AllowAnonymous explícito: sin esto, el FallbackPolicy de arriba (RequireAuthenticatedUser)
// también alcanzaría a CSS/JS/imágenes -- incluido _framework/blazor.web.js, sin el
// cual la página de /login ni siquiera podría conectar el circuito interactivo para
// dejar loguearse a nadie. Los estáticos no tienen datos sensibles: no hay razón
// para protegerlos.
app.MapStaticAssets().AllowAnonymous();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    // El circuito interactivo compartido de Blazor Server (/_blazor, /_blazor/negotiate,
    // /_blazor/initializers, /_blazor/disconnect) es UN SOLO endpoint genérico que usan
    // TODAS las páginas interactivas por igual -- no lleva el [AllowAnonymous] de la
    // página puntual que lo abrió. El FallbackPolicy de arriba lo bloqueaba sin más
    // (302 a /login), lo que rompía en silencio CUALQUIER evento (click, submit) en
    // páginas anónimas con @rendermode InteractiveServer (/register, /invitation/{code},
    // /aceptar-invitacion): la página cargaba bien (su GET inicial sí respeta su propio
    // [AllowAnonymous]), pero como el circuito nunca llegaba a conectar, ningún botón
    // hacía nada -- caso real: "Aceptar Invitación" sin ningún efecto visible.
    // Eximir sólo estos 4 endpoints es seguro: no exponen contenido por sí mismos, y el
    // request inicial de una página protegida sigue bloqueado en SU PROPIO endpoint
    // (confirmado con /diag temporal: "/" anónimo sigue devolviendo 302 a /login) --
    // esto sólo permite que una página YA permitida se vuelva interactiva.
    .Add(endpointBuilder =>
    {
        if (endpointBuilder is Microsoft.AspNetCore.Routing.RouteEndpointBuilder routeEndpoint &&
            routeEndpoint.RoutePattern.RawText is string rawPattern &&
            rawPattern.TrimStart('/').StartsWith("_blazor", StringComparison.OrdinalIgnoreCase))
        {
            routeEndpoint.Metadata.Add(new Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute());
        }
    });

app.Run();

// Valida el header x-signature de un webhook de MercadoPago. Formato del
// header: "ts=<epoch-ms>,v1=<hmac-hex>". El manifest se arma como
// "id:{dataId};request-id:{requestId};ts:{ts};" -- el segmento id:/request-id:
// se OMITE ENTERO (no vacío) si no vino ese dato, y dataId va en minúsculas.
// Documentado en el Dashboard de MercadoPago (Tus integraciones > Webhooks >
// clave secreta) -- no es la misma clave que el Access Token.
static bool IsValidMercadoPagoSignature(string signatureHeader, string requestId, string dataId, string? webhookSecret)
{
    if (string.IsNullOrEmpty(webhookSecret) || string.IsNullOrEmpty(signatureHeader))
        return false;

    string? ts = null;
    string? v1 = null;
    foreach (var part in signatureHeader.Split(','))
    {
        var kv = part.Split('=', 2);
        if (kv.Length != 2) continue;

        var key = kv[0].Trim();
        if (key == "ts") ts = kv[1].Trim();
        else if (key == "v1") v1 = kv[1].Trim();
    }

    if (ts == null || v1 == null)
        return false;

    var manifest = new StringBuilder();
    if (!string.IsNullOrEmpty(dataId))
        manifest.Append("id:").Append(dataId.ToLowerInvariant()).Append(';');
    if (!string.IsNullOrEmpty(requestId))
        manifest.Append("request-id:").Append(requestId).Append(';');
    manifest.Append("ts:").Append(ts).Append(';');

    using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(webhookSecret));
    var computed = Convert.ToHexStringLower(hmac.ComputeHash(Encoding.UTF8.GetBytes(manifest.ToString())));

    var a = Encoding.UTF8.GetBytes(computed);
    var b = Encoding.UTF8.GetBytes(v1.ToLowerInvariant());
    return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
}

// ExternalReference se arma en IPaymentService.CreateCheckoutSessionAsync
// como "{IdUser}:{IdSubscriptionPlan}".
static bool TryParseExternalReference(string? externalReference, out Guid idUser, out int idSubscriptionPlan)
{
    idUser = Guid.Empty;
    idSubscriptionPlan = 0;

    if (string.IsNullOrEmpty(externalReference))
        return false;

    var parts = externalReference.Split(':', 2);
    return parts.Length == 2
        && Guid.TryParse(parts[0], out idUser)
        && int.TryParse(parts[1], out idSubscriptionPlan);
}