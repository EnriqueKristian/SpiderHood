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

## Estructura del proyecto

Un solo proyecto (`SpiderHood.csproj`), organizado por carpeta/namespace en vez de
proyectos separados:

- **`Components/`** — FrontEnd. Páginas y componentes Blazor (`.razor`). No debería
  definir clases de dominio ni lógica de negocio propia — sólo UI, que llama a `Services/`.
- **`Services/`** + **`Data/`** — BackEnd. `Services/` tiene la lógica de negocio
  (`I<X>Service` + su implementación en el mismo archivo); `Data/BDLayout.*` es la capa
  de acceso a datos (Dapper + Stored Procedures contra SQL Server).
- **`Classes/`** — entidades, DTOs, ViewModels y enums compartidos entre FrontEnd y
  BackEnd (antes `Models/`; el nombre de la carpeta cambió pero el namespace sigue
  siendo `SpiderHood.Models` por compatibilidad — ver backlog de rename a inglés).

## Notas

- El login real usa un sistema de autenticación propio (`AuthService` +
  `CustomAuthenticationStateProvider`), no `SignInManager` de ASP.NET Core
  Identity — aunque Identity está registrado en `Program.cs` porque
  `IEmailConfirmationService` sí depende de `UserManager<IdentityUser>` para
  la confirmación de correo.
- La sesión se persiste en una cookie de autenticación HttpOnly de ASP.NET Core
  (emitida en `Login.razor` vía `HttpContext.SignInAsync`), no en `localStorage`.
  Cada circuito nuevo (recarga de página, reconexión) reconstruye el `UserSession`
  completo (edificios, roles) desde la base de datos a partir de esa cookie —
  ver `CustomAuthenticationStateProvider` / `IUserSessionLoader`. `localStorage`
  se usa únicamente para preferencias del usuario (tema, sidebar, edificio por
  defecto) — ver `AuthService.SetDefaultBuildingAsync` y afines.
