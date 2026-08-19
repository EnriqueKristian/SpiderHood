# SpiderHood

Sistema de administración de edificios/condominios: gestión de propietarios,
unidades, presupuestos, cuotas, lecturas de agua y conciliación bancaria.

**Stack:** Blazor Server (.NET 10), Entity Framework Core + Dapper contra SQL
Server, Bootstrap.

## Development Setup

1. **Requisitos**: .NET 10 SDK, SQL Server (LocalDB sirve para desarrollo).

2. **Configurar la cadena de conexión** en `appsettings.json` (o
   `appsettings.Development.json`), clave `ConnectionStrings:SpiderHoodContext`.
   Por defecto usa LocalDB.

3. **Aplicar las migraciones**:

   ```bash
   dotnet ef database update
   ```

4. **Correr la app**:

   ```bash
   dotnet run
   ```

   Por defecto queda disponible en `https://localhost:7175`.

## Notas

- El login real usa un sistema de autenticación propio (`AuthService` +
  `CustomAuthenticationStateProvider`), no `SignInManager` de ASP.NET Core
  Identity — aunque Identity está registrado en `Program.cs` porque
  `IEmailConfirmationService` sí depende de `UserManager<IdentityUser>` para
  la confirmación de correo.
- La sesión se persiste en `localStorage` del navegador.
