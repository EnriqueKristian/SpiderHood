# Pendientes de negocio — Administración de Permisos

## 1. Pantalla para crear/editar el catálogo de Permisos (sólo SysAdmin)

**Estado: implementado (2026-09-09), branch `claude/lista-pendientes-0gb03a`.**

A raíz de tener que sembrar `view_income_expense_report` (y los otros tres
permisos de reporte que tampoco existían) con un `INSERT` manual
(`Database/Scripts/2026-09-09_76_Seed_ReportPermissions.sql`), el usuario
pidió: "veo que no tenemos una pagina para administrar estos valores...
aunque solo es para el SysAdmin, ayuda a no tener que crear scripts".

Confirmado antes de implementar: no existía NINGÚN SP de escritura para
`dbo.Permissions` (`INS_Permission`/`UPD_Permission`) en todo el repo --
`IPermissionAdminService` (`Services/IPermissionAdminService.cs`) sólo
LEE permisos existentes (`GetAllPermissionsAsync`) para asignarlos a un rol
(`AssignPermissionsToRoleAsync`, usado por `RolePermissions.razor`); crear o
editar un permiso en sí siempre se hizo con SQL directo.

**Cambios:**
- `Database/Scripts/2026-09-09_77_Permission_CRUD.sql` -- dos SPs nuevos,
  `INS_Permission` (rechaza `PermissionKey` duplicada con `RAISERROR`) y
  `UPD_Permission`. No toca la tabla `dbo.Permissions` (ya existe, columnas
  confirmadas por el usuario con una consulta directa: `PermissionId`,
  `PermissionKey`, `Name`, `Description`, `Group`).
- `BDLayout.Core.cs`/`Add.cs`/`Update.cs`: constantes + `AddNewRecordAsync(PermissionDefinition)`/
  `UpdateRecordAsync(PermissionDefinition)`, mismo patrón que `ExpenseTemplate`.
- `IPermissionAdminService`: `GetAllPermissionDefinitionsAsync()` (antes
  privado, ahora público -- la pantalla nueva necesita la lista plana, no
  sólo agrupada por módulo como `GetAllPermissionsAsync`),
  `CreatePermissionAsync`/`UpdatePermissionAsync` nuevos.
- `Components/Pages/SettingPages/PermissionsAdmin.razor` (nuevo,
  `/Settings/Permissions`) -- lista de permisos (Clave/Nombre/Descripción/
  Grupo) + formulario de alta/edición inline. Restringido a SysAdmin con el
  mismo patrón que `SystemLogsPage.razor`
  (`currentUser.Roles.Contains("SysAdmin")`) -- es el único mecanismo real
  de "sólo Super Usuario" en este proyecto, no un permission key genérico
  (esos los puede tener también un Administrador de edificio).

**Decisión de diseño: `PermissionKey` es inmutable después de creado.**
Todo el código (`IPermissionService.HasPermissionAsync` y cada
`HasPermissionAsync(user, "view_xxx")` desperdigado en las páginas) compara
por ese string literal -- dejar que se edite después rompería en silencio
cualquier chequeo ya escrito contra el valor viejo. `UPD_Permission` ni
siquiera recibe ese parámetro; el campo queda deshabilitado en el
formulario de edición (se puede seguir editando Nombre/Descripción/Grupo).

**No se implementó (fuera de lo pedido):**
- Borrar un permiso -- el usuario pidió explícitamente "creación o
  modificación", no borrado. Borrar un permiso que ya esté asignado a un
  rol (`RolePermissions`) además necesitaría decidir qué hacer con esa
  asignación huérfana -- se dejó fuera de alcance a propósito.
- Agregar el ítem de menú "Permisos" -- el menú es 100% manejado desde BD
  vía la pantalla `/Settings/MenuItems` (`MenuItems.razor`), no hay que
  tocar código para eso: falta crearlo desde ahí (o desde
  `/Settings/Roles`, que es donde probablemente conviene un link, ya que
  hoy no hay ningún ítem de menú "Roles y Permisos" con hijos visibles
  para la sección Configuración -- no se pudo confirmar la estructura
  exacta del menú actual sin acceso a BD).

**Pendiente de probar con datos reales** (no hay acceso a BD en este
entorno):
1. Correr `Database/Scripts/2026-09-09_77_Permission_CRUD.sql`.
2. Entrar a `/Settings/Permissions` con un usuario SysAdmin, crear un
   permiso de prueba, confirmar que aparece en la lista y que después se
   puede asignar a un rol desde `/Settings/Roles/Permissions/{RoleId}`
   (agrupado bajo el "Grupo" elegido, con el nombre en español si el grupo
   es uno de los conocidos).
3. Confirmar que un usuario sin rol SysAdmin (aunque sea Administrador de
   edificio) ve el mensaje "Sólo el Super Usuario..." y no la pantalla.
4. Confirmar que crear un permiso con una `PermissionKey` ya existente
   muestra el error del `RAISERROR` (no una excepción genérica sin
   explicación) y no duplica la fila.
5. Agregar el ítem de menú "Permisos" desde `/Settings/MenuItems` para que
   sea alcanzable sin escribir la URL a mano.
