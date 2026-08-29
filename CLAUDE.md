# SpiderHood

Blazor Server app (.NET 10) para administración de condominios/edificios. Data layer
Dapper-style vía stored procedures (`BDLayout` partial class en `SpiderHood/Data/`), no
EF migrations — cambios de esquema se entregan como scripts SQL en
`Database/Migrations/` para que el usuario los corra manualmente (este entorno no tiene
acceso a la base de datos).

## Permisos configurables por rol

El sistema tiene una infraestructura de permisos completa y ya construida —
`Role` / `Permissions` / `RolePermissions` en BD, `IPermissionService.HasPermissionAsync`,
`IPermissionAdminService`, y una pantalla admin ya funcional en
`/Settings/Roles/Permissions/{RoleId}` (`RolePermissions.razor`) para asignar permisos a
cada rol con checkboxes, sin tocar código. El error fácil de cometer es agregar una
página nueva sin conectarla a esa infraestructura y volver a hardcodear
`currentUser.Role == "Administrador"` — varias páginas se construyeron así antes de que
esto quedara documentado. **Toda página o acción que cree/edite/elimine algo debe usar
este patrón**, no comparar `Role` contra un string.

### Patrón a replicar en cada página nueva

1. Inyectar `AuthService` (para obtener `UserSession`) e `IPermissionService`:
   ```razor
   @inject SpiderHood.Services.AuthService AuthService
   @inject SpiderHood.Services.IPermissionService PermissionService
   ```

2. En `OnInitializedAsync` (después de resolver `currentUser`), cargar un `bool` por
   cada acción distinta que la página ofrezca:
   ```csharp
   var currentUser = await AuthService.GetCurrentUserAsync() ?? new UserSession();
   _canEditarAlgo = await PermissionService.HasPermissionAsync(currentUser, "edit_algo");
   ```

3. Gatear el botón en el markup (ocultarlo, no sólo deshabilitarlo) **y además** poner un
   `if (!_canEditarAlgo) return;` al inicio del método que ejecuta la acción real —
   defensa en profundidad, por si el método se llama desde otro lado.

4. Si la página abre un modal hijo (patrón `Modal*.razor` con `OnSave`/`OnSaveItem` como
   `EventCallback`), **no** hace falta repetir el permiso dentro del modal: alcanza con
   gatear el botón que lo abre en la página padre y el método que procesa el callback
   (`SaveItem`, `SaveGroupUnit`, etc.), porque el modal es inalcanzable sin pasar por ahí.

### Granularidad de las claves

- **Una sola clave por módulo** (`manage_periods`, `manage_water_readings`,
  `edit_building`) cuando no hay un flujo con distintos actores por paso — no hace falta
  partir "crear"/"editar"/"eliminar" si en la práctica sólo el Administrador hace las
  tres.
- **Una clave por operación** (`create_owner`/`edit_owner`/`delete_owner`,
  `create_unit`/`edit_unit`/`delete_unit`) cuando sí puede tener sentido que un rol
  pueda, por ejemplo, editar pero no eliminar.
- **Una clave por paso del flujo** (ver Presupuesto: `create_budget`, `edit_budget`,
  `submit_budget`, `approve_budget`, `publish_budget`, `close_budget`) cuando distintos
  roles participan en distintos pasos — ahí sí vale la pena separar.

Antes de inventar una clave nueva, revisar si ya existe una que sirva —
`IPermissionAdminService.GetAllPermissionsAsync()` trae todas, agrupadas por `Group`
(mismo agrupamiento que usa `RolePermissions.razor`). Varias páginas (Edificios,
Unidades, Periodos, Lectura de Agua) ya tenían sus claves seedeadas en BD desde antes sin
que ningún botón las usara — confirmar antes de duplicar.

### Agregar una clave nueva

No hay acceso a BD desde este entorno. Entregar un script SQL en `Database/Migrations/`
(ver `2026-08-28_Permisos_Presupuesto_CuotasExtraordinarias.sql` y
`2026-08-28_Permisos_Edificios_Propietarios_Unidades.sql` como plantilla) que:

1. Inserte la(s) definición(es) nueva(s) en `Permissions`, idempotente
   (`WHERE NOT EXISTS`).
2. Reconstruya el set de Administrador/SysAdmin (`DEL_RolePermissionsByRole` +
   `INS_RolePermissions` por cada clave del set completo) en vez de intentar un INSERT
   directo a `RolePermissions` — esa tabla usa nombres de columna que no coinciden con
   el modelo C# (`RoleId`, no `IdRole`), así que pasar por los procs existentes evita
   tener que adivinarlos.
3. Nunca tocar Junta/Residente salvo que el usuario lo pida explícitamente — su set por
   defecto es de solo lectura salvo excepciones ya conocidas (Junta con
   `approve_budget`).

El usuario corre el script manualmente y confirma el resultado antes de que el código
que depende de esa clave se considere terminado — si se despliega el código antes de que
la clave exista/esté asignada, la acción queda bloqueada para todos por defecto (fail
closed), así que conviene avisar explícitamente en qué orden aplicar cada parte.

## Nomenclatura de Pages y componentes

`Components/Pages/` creció con varios estilos a la vez (inglés/español mezclado, "Modal"
como prefijo y como sufijo, `Index.razor` repetido en media docena de carpetas). No se
fuerza una traducción masiva de lo que ya existe — alto riesgo, bajo valor, sin
`dotnet build` disponible en este entorno para verificar cada cambio — pero todo archivo
nuevo, y cualquier archivo que se toque de paso, sigue estas reglas:

- **El idioma del nombre lo decide el módulo, no una regla global.** Si la carpeta ya es
  mayoritariamente español (`BudgetPages`, `ConciliacionPages`, `WaterCalculationPages`),
  el archivo nuevo va en español; si es mayoritariamente inglés, va en inglés. No
  traducir nombres de negocio ya establecidos (`Cuota`, `Presupuesto`, `Edificio`) solo
  por consistencia con otra carpeta.
- **Nunca `Index.razor`/`Index.razor.cs` fuera de una carpeta de un solo archivo.** El
  nombre tiene que decir qué lista/gestiona sin depender de la carpeta contenedora (ej.
  `ListadoPresupuestos.razor`, no `Index.razor` dentro de `BudgetPages/`).
- **Los modales van con sufijo `...Modal.razor`, nunca con prefijo `Modal...`.** Un
  componente que no es un modal en sí (ej. contenido que un padre envuelve en su propio
  `.modal`) no lleva el sufijo — nombrarlo por lo que hace.
- **Ningún nombre de archivo se repite entre carpetas distintas**, aunque el contenido
  sea distinto — confunde más de lo que ahorra. Si dos features comparten un nombre
  natural, uno de los dos suma contexto al nombre (ej. `CargarEstadoCuentaConciliacion.razor`
  vs. `MovementPages/CargarEstadoCuenta.razor`).
- Antes de renombrar un archivo existente: `grep` el nombre de la clase/tag en todo el
  repo (uso como `<Componente>`, no solo el nombre de archivo) y actualizar cada
  referencia en el mismo commit — el nombre de clase de un `.razor` lo deriva el
  compilador del nombre de archivo, no hay declaración explícita que editar.
