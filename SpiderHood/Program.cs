using Blazored.LocalStorage;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SpiderHood.Components;
using SpiderHood.Data;
using SpiderHood.Services;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContextFactory<SpiderHoodContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("SpiderHoodContext") ?? throw new InvalidOperationException("Connection string 'SpiderHoodContext' not found.")));

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
builder.Services.AddAuthorizationCore();

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
builder.Services.AddScoped<IBankAccountService, BankAccountService>();
builder.Services.AddScoped<IBudgetService, BudgetService>();
builder.Services.AddScoped<IExpenseService, ExpenseService>();
builder.Services.AddScoped<ICalculoService, CalculoService>();
builder.Services.AddScoped<IExceptionService, ExceptionService>();
builder.Services.AddScoped<IPeriodService, PeriodService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IOwnerService, OwnerService>();
builder.Services.AddScoped<IInstallmentService, InstallmentService>();
builder.Services.AddScoped<ICuotaService, CuotaService>();
builder.Services.AddScoped<IGastoService, GastoService>();
builder.Services.AddScoped<IBuildingService, BuildingService>();
builder.Services.AddScoped<IWorkflowService, WorkflowService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IEmailConfirmationService, EmailConfirmationService>();
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddScoped<IPermissionAdminService, PermissionAdminService>();
builder.Services.AddScoped<IMenuAdminService, MenuAdminService>();
builder.Services.AddScoped<IServiceReadingService, ServiceReadingService>();
builder.Services.AddScoped<IExtraChargeService, ExtraChargeService>();
//builder.Services.AddScoped<IFinancialService, FinancialService>();
//builder.Services.AddScoped<ISessionService, SessionService>();
builder.Services.AddScoped<IPreferenceService, PreferenceService>();
builder.Services.AddHttpClient();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();