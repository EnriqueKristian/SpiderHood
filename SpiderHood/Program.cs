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