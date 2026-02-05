using Blazored.LocalStorage;
using Blazored.Toast;
using Blazored.Toast.Services;
using Microsoft.EntityFrameworkCore;
using SpiderHood.Components;
using SpiderHood.Data;
using SpiderHood.Models;
using SpiderHood.Services;


var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContextFactory<SpiderHoodContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("SpiderHoodContext") ?? throw new InvalidOperationException("Connection string 'SpiderHoodContext' not found.")));

builder.Services.AddQuickGridEntityFrameworkAdapter();

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddBlazorBootstrap();

// Singleton para mantener el estado global
builder.Services.AddScoped<ParameterService>();

// Singleton para usuarios
builder.Services.AddScoped<UsuarioService>();

builder.Services.AddScoped<BankAccountService>();

builder.Services.AddScoped<ExpenseService>();

// Register services
builder.Services.AddScoped<IBudgetService, BudgetService>();

// Add other services
//builder.Services.AddScoped<SpiderHood.Services.ToastService>();
builder.Services.AddSingleton<ICalculoService, CalculoService>();
builder.Services.AddScoped<IExceptionService, ExceptionService>();
builder.Services.AddScoped<IPeriodService, PeriodService>();

// Registrar servicios
builder.Services.AddScoped<AuthenticationService>();
builder.Services.AddHttpClient(); // registers HttpClient via IHttpClientFactory
builder.Services.AddBlazoredLocalStorage(); // register ILocalStorageService


builder.Services.AddScoped<ICuotaService, CuotaService>();
builder.Services.AddScoped<IGastoService, GastoService>();
builder.Services.AddScoped<IDepartamentoService, DepartamentoService>();
builder.Services.AddScoped<IWorkflowService, WorkflowService>();


// Configurar autenticación
builder.Services.AddAuthorizationCore();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
    app.UseMigrationsEndPoint();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
