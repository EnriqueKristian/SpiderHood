# Registro self-service ("Piloto") desde la landing pública

Diseño acordado en sesión de trabajo, implementado el mismo día. A diferencia de
`Design-Defaults-Sistema-Mixto.md`, este documento es corto porque el alcance
también lo es -- decisiones + qué se tocó, sin plan por pasos.

## Decisiones acordadas

1. **Alcance**: el registro desde la landing (botón "Piloto"/"Acceso al Sistema")
   es exclusivamente para alguien que **no tiene ningún edificio todavía** -- se
   registra, queda como **Administrador** y va a **crear su propio edificio** a
   continuación. Unirse a un edificio **existente** sigue siendo, sin excepción,
   por **link de invitación** (`RegisterWithInvitationAsync`, ya existía). Son dos
   caminos separados, sin superposición -- ninguno reemplaza al otro.
2. **Flujo post-registro**: autologin inmediato (no hay nadie que tenga que
   aprobar a un Administrador creando su propio edificio, a diferencia del
   `/register` existente que siempre pide un edificio *ajeno* y queda pendiente),
   y caída directa al wizard de "Nuevo Edificio" -- no un dashboard vacío con un
   botón a buscar.
3. **"Período de Prueba"**: por ahora es sólo el copy de marketing de la landing.
   Sin fecha de expiración, sin límite de funciones, sin plan que confirmar
   después -- la cuenta queda como Administrador normal. Si más adelante hace
   falta lógica real de prueba (vencimiento, plan), es un cambio aparte.
4. **Actualizado**: la landing HTML sí terminó entrando a este repo (decisión
   posterior, ver "Landing pública en `wwwroot`" más abajo) -- el usuario la
   diseñó aparte, pero se integró como parte de la misma app/deploy en vez de
   quedar en un sitio separado.
5. **Dominio**: `spiderhoodapp.com` (uno solo, servido desde su propia PC vía
   túnel de Cloudflare) -- landing en la raíz `/`, sistema en `/login`,
   `/register-admin`, etc., todo bajo el mismo dominio.

## Qué se implementó

- **`SpiderHood/Classes/User.cs`**: `RegisterAdminModel` nuevo (como
  `RegisterModel` pero sin `BuildingId` -- a propósito, acá no hay ningún
  edificio existente al que unirse).
- **`SpiderHood/Services/IAuthService.cs`**:
  - `RegisterNewAdministratorAsync(RegisterAdminModel)`: crea el `UserModel`,
    llama a `GrantGlobalAdministradorRoleAsync` (ver abajo) y hace autologin
    (`LoginAsync` con las mismas credenciales) -- sin paso de aprobación, a
    diferencia de `RegisterSelfServiceAsync`.
  - `GrantGlobalAdministradorRoleAsync`: inserta una fila en `UserRole`
    (`AddUserRoleAsync`, usuario↔rol SIN edificio) con el rol "Administrador" --
    **mismo mecanismo que ya usa SysAdmin** para reconocerse globalmente sin
    depender de ninguna fila en `UserBuildingAssociation` (ver
    `GrantSysAdminAccessToAllBuildingsAsync`/`GetRoleByUserIdAsync`, ya
    existentes).
  - `ResolveDefaultBuildingAndRole` (duplicado en `AuthService.cs` y
    `UserSessionLoader.cs`, mismo motivo que la duplicación ya existente de
    `GrantSysAdminAccessToAllBuildingsAsync`: ambos reconstruyen la sesión, uno en
    el login, el otro en cada circuito nuevo): generalizado el mecanismo de
    SysAdmin a cualquier rol global -- nuevo parámetro `rolGlobalNombre`. Si
    `buildings` está vacío (nadie asociado todavía) y hay un rol global
    reconocido, resuelve a `(Guid.Empty, rolGlobalNombre)` -- sin esto, un
    Administrador recién registrado caía en el mismo callejón sin salida que
    tenía SysAdmin antes de corregirse (`Role=""`, sin permiso ni para crear su
    primer edificio). A diferencia de SysAdmin (que SIEMPRE prioriza su rol
    global, tenga o no edificios), esto sólo aplica mientras `buildings` esté
    vacío -- apenas el Administrador crea su primer edificio, `buildings` deja de
    estar vacío y esta rama no vuelve a aplicar: resuelve como cualquier
    Administrador normal, por su `UserBuildingAssociation` real.
- **`SpiderHood/Components/Pages/RegisterAdmin.razor`** (`/register-admin`):
  mismo look que `/register`, sin selector de edificio. Llama a
  `RegisterNewAdministratorAsync` y, si sale bien, navega a `/buildings` (el
  autologin ya deja la sesión armada).
- **`SpiderHood/Components/Pages/BuildingPages/BuildingPage.razor.cs`**: en
  `CargarDatosPagina`, si `Buildings` queda vacío Y el usuario tiene el permiso
  `create_building`, abre el modal de "Nuevo Edificio" automáticamente -- es el
  "wizard" pedido, reusando la pantalla que ya existe (no se armó una pantalla
  aparte). De paso mejora el caso ya existente de una instalación nueva sin
  ningún Building (SysAdmin también cae acá).

## Landing pública en `wwwroot` (decisión posterior a la implementación de arriba)

El usuario diseñó la landing (HTML/CSS estático, sin build step) y decidió que
entrara al mismo repo/deploy en vez de vivir aparte -- un solo dominio
(`spiderhoodapp.com`), un solo sitio IIS, sin piezas de infraestructura extra.

- **`SpiderHood/wwwroot/index.html`**: el HTML de la landing, con los links ya
  apuntando al sistema: nav "Acceso al Sistema" -> `/login`, "Piloto gratuito"
  (nav, hero, sección software) -> `/register-admin`. El formulario de
  contacto le sacó la opción "Solo piloto del software" del combo "¿Qué te
  interesa?" -- ese caso ahora tiene su propio botón directo a
  `/register-admin` en vez de pasar por un formulario que además nunca tuvo
  `action`/backend real (sigue sin tenerlo -- las otras 2 opciones del combo,
  Administración/Paquete completo, quedan como quedaban, fuera de alcance de
  hoy conectarlas a algo).
  **Pendiente del lado del usuario**: falta copiar `logo3.png` a
  `wwwroot/logo3.png` -- el `<img>` del header lo referencia por nombre
  relativo y hoy no existe ese archivo en el repo.
- **`SpiderHood/Program.cs`**: el problema real a resolver era que
  `Home.razor` (`@page "/"`) ya es el Dashboard, y bastantes lugares del app
  asumen que `/` significa exactamente eso (breadcrumbs "Inicio", botones
  "Volver al inicio", probablemente el ítem "Dashboard" del menú lateral que
  sale de `MenuItems` en BD -- no confirmado desde el repo). Mover la ruta de
  `Home.razor` habría roto todo eso. En cambio se agregó un `app.MapWhen(...)`
  ANTES de `MapRazorComponents`, que intercepta `/` sólo cuando
  `ctx.User.Identity?.IsAuthenticated != true` y sirve `wwwroot/index.html`
  directo (`SendFileAsync`) -- para cualquier request YA autenticado a `/`,
  el `MapWhen` no matchea y el pipeline sigue de largo hasta el router de
  Blazor de siempre. Resultado: alguien sin sesión que entra a
  `spiderhoodapp.com/` ve la landing; alguien logueado que entra a `/` sigue
  viendo su Dashboard, sin cambios.
  - Se actualizaron 3 links internos que apuntaban a `/` asumiendo que
    siempre era el Dashboard (`BuildingSelection.razor` x4,
    `BankReconciliation.razor` x1) a `/dashboard` explícito -- no era
    estrictamente necesario dado el chequeo de autenticación de arriba (para
    un usuario logueado `/` sigue funcionando igual), pero queda más claro y
    no depende de esa sutileza.

**Sin probar contra el deploy real todavía** -- falta que el usuario copie
`logo3.png`, recompile, y confirme que `/` sirve la landing sin sesión y el
Dashboard con sesión.
## Bug encontrado al probar (segunda vuelta), ya corregido

`/register-admin` navega directo a `/buildings` después del autologin, así que
en ESE camino puntual el modal de "Nuevo Edificio" se abre solo, como se
diseñó. Pero si el mismo usuario (recién registrado, sin ningún edificio
todavía) se desloguea y vuelve a entrar por `/login` (camino normal), termina
en `Home.razor.cs` con `CurrentBuildingId == Guid.Empty` -- que redirige a
`/select-building`, no a `/buildings`. `SelectBuilding.razor` sólo conocía un
estado para "cero edificios": ofrecer **"Solicitar acceso"** (`/building-request`,
pensado para un Residente uniéndose a un edificio ajeno) -- sin distinguir a un
Administrador que en realidad tiene que CREAR el suyo. Confirmado en vivo por
el usuario: llegó a "Solicitar Acceso a Edificio" y vio "Ya tenés una solicitud
o membresía como Residente en todos los edificios disponibles" (mensaje además
engañoso en este caso puntual -- la causa real era que la BD de prueba no tenía
ningún edificio real todavía, sólo el Template).

Corregido en `SelectBuilding.razor`: en el estado "cero edificios", si el
usuario tiene el permiso `create_building` (chequeado igual que en
`BuildingPage.razor.cs`), ofrece **"Crear mi primer edificio"** -> `/buildings`
(que abre el modal solo) en vez de "Solicitar acceso" -- unirse a un edificio
ajeno no es un camino válido para este tipo de usuario, según la decisión de
alcance de más arriba. Quien NO tiene ese permiso (un Residente típico) sigue
viendo el flujo de siempre, sin cambios.

## Bugs encontrados al probar (tercera vuelta), ya corregidos

Dos hallazgos más de la primera prueba end-to-end real de un edificio creado
por un Administrador (no relacionados entre sí, ni con Paso 5 puntualmente --
son bugs generales de `BuildingPage.razor` que nunca se habían notado porque
nadie había inspeccionado tan de cerca un edificio recién creado):

- **`Type` del `<select>` "Tipo" del modal "Nuevo Edificio" nunca se guardaba
  de verdad**: `Building.Type` es `int` (el `Value` del grupo Sistema "Tipo
  Edificio", `IdParent=34`), pero el `<select>` tenía las 3 opciones
  hardcodeadas con el NOMBRE como `value` (`"Familiar"/"Comercial"/"Mixto"`),
  que nunca puede parsear a un `int` -- la selección nunca se aplicaba,
  `Type` quedaba siempre en `0` sin importar qué se eligiera. Por eso el
  badge de Tipo mostraba "No se encontró coincidencia" en "Prueba 1" pese a
  que los datos de `Parameter` (`IdParent=34`) están perfectos -- confirmado
  por el usuario. Corregido: el `<select>` ahora sale de
  `ParameterService.ListParameters.Where(p => p.IdParent == 34)`, con el
  `Value` real como `option value`; `ShowCreateModal` además arranca con el
  primer valor real en vez de dejar el default `0` de `int`.
  **"Prueba 1" quedó con `Type=0` en BD de antes del fix** -- se corrige
  volviendo a editar ese edificio desde la UI una vez recompilado (elegir
  cualquier Tipo y Guardar), o a mano: `UPDATE Building SET Type = 1 WHERE
  Name = 'Prueba 1';` (1 = Familiar, ver `Parameter` `IdParent=34`).
- **`/users` dejaba ver y asignar el rol SysAdmin a cualquier Administrador**:
  `manage_users` está otorgado a Administrador y SysAdmin por igual, pero la
  pantalla nunca filtraba el rol SysAdmin para quien no lo es -- escalación
  de privilegios real. Confirmado por el usuario (vio la cuenta SysAdmin en
  la lista y pudo elegir "SysAdmin" al crear un usuario nuevo). Corregido en
  `Users.razor`: se filtra `_availableRoles` y la lista de usuarios excluyendo
  SysAdmin cuando quien gestiona no es SysAdmin.

## Cabo suelto conocido, no bloqueante

Después de crear su primer edificio, la fila global en `UserRole`
("Administrador", sin edificio) queda huérfana -- no se borra. No causa ningún
problema (la rama de `ResolveDefaultBuildingAndRole` que la usa sólo aplica
mientras `buildings` esté vacío, así que deja de leerse apenas exista una
`UserBuildingAssociation` real), pero es basura acumulándose en `UserRole`. Se
podría limpiar en `CreateBuildingAsync` después de la primera asociación exitosa
si en algún momento molesta -- no se hizo ahora para no tocar ese método por algo
cosmético.

## Sin confirmar todavía

- **Permiso `create_building` para el rol "Administrador"**: no está verificado
  si ya está otorgado en `RolePermissions` en la BD real (no hay ningún script en
  el repo que lo garantice, y `PermissionService.GetPermissionsForRoleAsync`
  consulta la tabla en vivo). Si al probar el registro el modal de "Nuevo
  Edificio" no se abre solo (o el botón no aparece), es la primera causa a
  revisar -- otorgar el permiso a mano en `RolePermissions` para el rol
  Administrador.
- **Nombre exacto de la ruta** `/register-admin`: elegido por default, sin
  confirmar con el usuario -- es donde tiene que apuntar el botón "Piloto"/"Acceso
  al Sistema" de la landing.

## Bug preexistente encontrado al probar, ya corregido

Primera prueba end-to-end real de `/register-admin`: la cuenta se creaba pero el
autologin (y después el login manual) fallaba -- `IsActive` quedaba en `0` en BD
pese a que `RegisterNewAdministratorAsync` arma el `UserModel` con
`IsActive = true`. Causa: `INS_User` (confirmado con su `CREATE PROCEDURE` real)
nunca tomaba `@IsActive` como parámetro -- lo insertaba **hardcodeado en 0**.
`AddNewRecordAsync(UserModel)` en `BDLayout.Add.cs` tampoco lo mandaba (sólo
IdUser/Email/PasswordHash/FirstName/LastName/PhoneNumber). No es un bug de
Paso 5 -- es preexistente y afecta a **cualquier** alta de usuario desde la app
(`RegisterSelfServiceAsync`, y un admin dando de alta a alguien desde
Configuración > Usuarios); recién se detectó ahora porque es la primera vez en
la sesión que se probó un alta + login inmediato de punta a punta.

Corregido en `Database/Scripts/2026-09-02_31_Fix_INS_User_IsActive.sql`
(`@IsActive BIT = 1` nuevo, default activo) y `BDLayout.Add.cs` (manda
`user.IsActive` como 7mo parámetro). De paso se corrigió el nombre de operación
copiado y pegado (`"AddInstallmentExoneration"` -> `"AddUser"`) que quedaba en
el mismo método, usado sólo para el mensaje de error si la operación falla.

**Diagnóstico confirmado por el usuario** (`sp_helptext INS_User` real pegado,
cuenta de prueba activada a mano mientras tanto con
`UPDATE Users SET IsActive = 1` para poder seguir probando). **La corrección
en sí (script `_31` + `BDLayout.Add.cs`) todavía no se corrió/probó contra la
BD real.**
