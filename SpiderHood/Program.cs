using Blazored.LocalStorage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SpiderHood.Components;
using SpiderHood.Data;
using SpiderHood.Services;

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
builder.Services.AddScoped<CustomAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp =>
    sp.GetRequiredService<CustomAuthenticationStateProvider>());

builder.Services.AddScoped<AuthService>();
builder.Services.AddCascadingAuthenticationState();

// FallbackPolicy: toda página requiere sesión autenticada por default (fail closed).
// Antes no existía ningún [Authorize] ni FallbackPolicy en la app — AuthorizeRouteView
// (Components/App.razor) estaba wireado pero no exigía nada, así que cualquiera con la
// URL entraba a cualquier página sin loguearse. Las páginas que sí deben ser públicas
// (login, confirmación de email, invitación, error, not-found) llevan
// `@attribute [AllowAnonymous]` explícito.
builder.Services.AddAuthorizationCore(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
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

// AddIdentity registra IdentityConstants.ApplicationScheme como el scheme de
// autenticación/challenge por default. El chequeo de autorización que Razor
// Components hace en SSR (el que impone el FallbackPolicy antes de que exista el
// circuito interactivo) dispara un challenge contra ese scheme cuando la request no
// está autenticada — y como esta app NO tiene una sesión basada en cookie (el login
// real es 100% propio, vía localStorage/CustomAuthenticationStateProvider, nunca pasa
// por SignInManager — ver nota arriba), HttpContext.User es SIEMPRE anónimo para
// TODA request SSR, incluida la del propio "/login" pese a estar marcado
// [AllowAnonymous]. Apuntar LoginPath a "/login" (intento anterior) no alcanzaba:
// cada visita a "/login" volvía a disparar el challenge contra sí misma, anidando
// ReturnUrl sin fin.
//
// La solución real es que este challenge NUNCA redirija — el único mecanismo de
// redirección a login que debe existir es el de Blazor (AuthorizeRouteView ->
// RedirectToLogin en Components/App.razor), que sí conoce la sesión real vía
// CustomAuthenticationStateProvider. Devolver 401 en vez de redirigir dejar pasar el
// render de la página (con su <NotAuthorized>) en vez de cortarlo con un 302 ciego.
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
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
builder.Services.AddHttpClient();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();