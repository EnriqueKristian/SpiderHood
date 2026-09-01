# Deploy a QA (IIS + SQL Server, Windows 11)

Runbook para publicar SpiderHood en un ambiente de QA sobre IIS con SQL Server,
todo en la misma máquina Windows 11.

## 1. Preparar la máquina (una sola vez)

1. **Rol IIS** (si no está): Panel de Control > Programas > Activar o desactivar
   características de Windows > **Internet Information Services**. Dentro de
   *World Wide Web Services > Application Development Features*, asegurate de
   tildar **WebSocket Protocol** -- Blazor Server lo necesita para el circuito
   SignalR; sin esto la app carga pero se queda "colgada" sin reaccionar a
   clicks.
2. **.NET 10 Hosting Bundle**: descargalo desde
   https://dotnet.microsoft.com/download/dotnet/10.0 (sección "Hosting Bundle",
   no el SDK ni el runtime sueltos) e instalalo. Este paquete es el que registra
   el módulo ASP.NET Core (ANCM) dentro de IIS -- sin él, IIS no sabe qué hacer
   con una app .NET y tira 502.5 o "no se puede ver esta página". Después de
   instalarlo, reiniciá IIS: `iisreset` desde una consola como Administrador.
3. Confirmá que **SQL Server** ya está corriendo y accesible desde esta misma
   máquina (`localhost` o el nombre de instancia, ej. `localhost\SQLEXPRESS`).

## 2. Base de datos

1. Restaurá el backup (.bak) de dev/prod en tu SQL Server de QA, con el nombre
   que prefieras (ej. `SpiderHoodContext_QA`). Esto trae todo el esquema base
   (tablas y Stored Procedures) -- el repo no tiene ese script base, solo los
   incrementales de esta sesión.
2. Corré, en este orden, los scripts de `Database/Scripts/` que se generaron en
   esta sesión (todos son idempotentes, no pasa nada si corrés alguno dos
   veces):
   1. `2026-09-01_01_Audit_HeaderColumns.sql`
   2. `2026-09-01_02_WorkflowAuditLog.sql`
   3. `2026-09-01_03_SystemLog.sql`
   4. `2026-09-02_04_Audit_MoreHeaders.sql`
3. **Revisalos antes de correrlos** contra el esquema real que quedó en tu
   backup restaurado -- estos scripts asumen los nombres de tabla que se ven en
   tu diagrama (ej. `dbo.ApartmentOwner`, `dbo.Periods`), pero si tu backup es
   de otro momento/rama podrían no coincidir exactamente.
4. **Permisos**: el Application Pool de IIS corre por defecto como
   `ApplicationPoolIdentity` (una cuenta virtual, `IIS AppPool\<NombreDelPool>`),
   que normalmente **no** tiene acceso a SQL Server. Como la connection string
   de QA usa autenticación de Windows (`Trusted_Connection=True`), tenés que
   crear un login en SQL Server para esa cuenta virtual y darle permisos sobre
   `SpiderHoodContext_QA` (db_datareader + db_datawriter + EXECUTE sobre los
   SPs alcanza). Se hace después de crear el sitio en el paso 3, una vez que
   sabés el nombre exacto del Application Pool.

## 3. Sitio en IIS

1. Abrí **IIS Manager**. Creá un Application Pool nuevo (ej. `SpiderHoodQA`):
   - .NET CLR version: **No Managed Code** (obligatorio -- ASP.NET Core no corre
     bajo el pipeline clásico de IIS).
   - Managed pipeline mode: Integrated.
2. Creá un sitio nuevo (o una aplicación dentro de Default Web Site) apuntando
   como *Physical path* a la carpeta donde vas a publicar (por defecto en el
   perfil de publish: `C:\inetpub\wwwroot\SpiderHoodQA\`), usando el
   Application Pool que acabás de crear.
3. Volvé al paso 2.4 y otorgale el login de SQL Server a la identidad de este
   Application Pool.

## 4. Configurar la connection string de QA

El repo trae `SpiderHood/appsettings.QA.json` con placeholders:

```json
"SpiderHoodContext": "Server=CAMBIAR_SERVIDOR_SQL;Database=SpiderHoodContext_QA;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
```

Editalo con el nombre real de tu instancia SQL (ej. `localhost\SQLEXPRESS`) y
el nombre de base que usaste al restaurar. También ajustá `BaseUrl` al
host/puerto real del sitio de QA (se usa para armar links en los correos de
confirmación/invitación).

Este archivo **no** se sobreescribe con datos de producción ni de tu
`appsettings.json` de desarrollo -- ASP.NET Core lo carga solo cuando
`ASPNETCORE_ENVIRONMENT=QA`, que el perfil de publish (`QA-IIS.pubxml`) ya deja
grabado en el `web.config` generado. No hace falta tocar nada en IIS Manager
para esto.

## 5. Publicar

1. En Visual Studio: click derecho sobre el proyecto **SpiderHood** > **Publish**
   > elegí el perfil **QA-IIS** (ya viene en el repo, en
   `Properties/PublishProfiles/QA-IIS.pubxml`).
2. Revisá que el **Target location** (carpeta destino) coincida con el
   *Physical path* del sitio IIS que creaste en el paso 3.2 -- si no, ajustalo
   ahí mismo en la ventana de Publish.
3. **Publish**. Esto compila en Release, publica framework-dependent (usa el
   Hosting Bundle instalado en el paso 1.2, no incluye el runtime en la
   carpeta) y escribe `ASPNETCORE_ENVIRONMENT=QA` en el `web.config` generado.

## 6. Verificar

1. Abrí el sitio en el navegador (la URL/puerto que configuraste en IIS).
2. Login normal.
3. **Cosas puntuales de esta sesión que conviene probar**:
   - Crear/editar un `Building`/`Owner`/Presupuesto y confirmar que quedan
     `CreatedBy`/`ModifiedBy`/`CreatedOn`/`ModifiedOn` en la fila.
   - Aprobar/rechazar/publicar un presupuesto y confirmar que aparece la fila
     correspondiente en `WorkflowAuditLog`.
   - Como usuario `SysAdmin`, ir a `/Settings/SystemLogs`, activar el logging,
     forzar algún error y confirmar que se guarda en `SystemLog`.
   - Las páginas renombradas del módulo Presupuesto/Cuotas (`/cuotas`,
     `/generacioncuota`, `/cuotahistorico`, `/budgetlist`, etc. -- las rutas
     no cambiaron) siguen funcionando igual que antes del rename.

## Problemas comunes

- **502.5 Process Failure** al abrir el sitio: casi siempre es el Hosting
  Bundle no instalado (o instalado *antes* de instalar IIS -- en ese caso hay
  que reinstalar el Hosting Bundle después de habilitar IIS) o la versión de
  .NET no coincide (`net10.0`, verificá con `dotnet --info` en la máquina de
  QA). Revisá el log en `C:\inetpub\wwwroot\SpiderHoodQA\logs\` (si
  `stdoutLogEnabled` está prendido en el `web.config` generado).
- **La app carga pero no reacciona a ningún click/botón**: falta habilitar
  WebSocket Protocol en IIS (paso 1.1) -- Blazor Server necesita esa conexión
  persistente para el circuito.
- **Error de login/timeout hacia SQL Server**: falta el login del Application
  Pool en SQL Server (paso 2.4), o el nombre de servidor en
  `appsettings.QA.json` está mal.
