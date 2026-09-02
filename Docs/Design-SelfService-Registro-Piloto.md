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
4. **La landing HTML vive fuera de este repo** (la arma el usuario aparte). Acá
   sólo se construyó la página de destino a la que sus botones apuntan.

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
- **Sin probar contra la BD real todavía.**
