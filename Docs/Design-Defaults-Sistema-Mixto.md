# Valores default por Edificio: Parámetros, Categorías y Configuración

Diseño acordado en sesión de trabajo. Este documento es el plan a seguir, con los
gaps detectados en el código actual y las decisiones tomadas. Actualizar esta lista
de TAREAS a medida que se implemente cada parte.

## Estado de implementación

- [x] **Paso 1 — Persistir creación de Building** (§2, §8 orden sugerido). **Probado
  end-to-end y funcionando**: crear, editar y recargar sesión con el edificio nuevo,
  todo OK. `INS_Building` + `UPD_Building` completo
  (`Database/Scripts/2026-09-02_17_Persist_BuildingCreation.sql` +
  `2026-09-02_18_Fix_Building_NumberIsIdentity.sql` -- `Number` es IDENTITY, no se
  manda), `AddNewRecordAsync(Building)`, `IBuildingService.CreateBuildingAsync`/
  `UpdateBuildingAsync` (crea también la `BuildingConfiguration` inicial y la
  `UserBuildingAssociation` de quien lo creó). `BuildingPage.razor.cs.SaveBuilding()`
  llama a estos métodos en vez de sólo tocar la lista en memoria.
  Bugs encontrados y corregidos en el camino: `Number` IDENTITY (no seteable),
  falta `using Microsoft.JSInterop` en el code-behind, y el más serio --
  `BuildingConfiguration.DefaultCategory`/`WaterReadingDefault` quedaban `NULL` en
  un edificio sin Categorías propias todavía, y como el modelo los tipaba `Guid` no-
  nullable, `GET_AllBuildingsConfig` explotaba con `SqlNullValueException` al leer
  CUALQUIER edificio con esos campos en NULL -- rompía la reconstrucción de sesión
  completa (bucle de login) para todo usuario con acceso a ese edificio, SysAdmin
  incluido. Se resolvió pasándolos a `Guid?` en el modelo.
  **Bug encontrado después, ya corregido**: crear/editar un Building sí quedaba
  persistido en BD, pero no se veía en la cabecera (selector de edificio) ni
  sobrevivía a navegar a otra página y volver -- sólo un F5 real lo mostraba
  bien en todos lados. Causa: `CustomAuthenticationStateProvider` hidrata el
  `UserSession` (con su `Buildings`) UNA sola vez por circuito (primer pedido) y
  lo cachea en memoria de ahí en adelante; `BuildingPage.razor.cs.SaveBuilding()`
  sólo agregaba el edificio nuevo a su lista local, nunca a esa sesión cacheada
  -- así que la cabecera (que lee `AuthService.GetCurrentUserAsync().Buildings`)
  seguía viendo la lista vieja, y hasta la propia página de Buildings volvía a
  leer esa misma sesión stale al reinicializarse por navegación. Se agregó
  `AuthService.RefreshCurrentUserBuildingsAsync()` (recarga `Buildings`/`Roles`
  desde BD y llama a `MarkUserAsAuthenticated` sobre la sesión en memoria, lo que
  dispara `AuthenticationStateChanged`), invocado desde `SaveBuilding()` al crear
  y al editar. `HeaderMainLayout`/`LeftMenu` ya escuchaban ese evento para
  refrescarse solos -- no hizo falta tocarlos.
- [x] **Paso 2 — Edificio Template + clonado de `BuildingConfiguration`** — **compila
  y corre sin errores, confirmado**. (decisión:
  flag `Building.IsTemplate`, editado con las mismas pantallas que un edificio real
  -- no hay tope de uno solo, deliberado, para poder tener más de un
  template/demo). `Database/Scripts/2026-09-02_19_Building_IsTemplate.sql`: columna
  `IsTemplate`, `INS_Building`/`UPD_Building` la incluyen, `GET_TemplateBuilding`
  nuevo (con `SELECT *` a propósito, para no repetir el desfasaje de columnas que
  ya pasó con `Number` en pasos anteriores). `CreateBuildingAsync` clona de
  `GET_TemplateBuilding` (si existe alguno) los campos de la lista original
  (Moneda, Métodos de Pago, Periodo de Pago, Día de Vencimiento, Consumo Mínimo,
  Cargo Fijo, Monto Multa, Tasa de Interés, Día Emisión Recibos, Alerta/Deuda
  Crítica, Texto Pie de Recibo) -- NO clona BankAccounts/Contacts/
  DefaultCategory/WaterReadingDefault/Exonerations (no tiene sentido copiar una
  cuenta bancaria real del template, y Category todavía no se clona -- Paso 4). Si
  no hay ningún template marcado, sigue el fallback hardcodeado de siempre. Checkbox
  "Es edificio template" en el modal de Building, visible sólo para SysAdmin;
  badge "Template" en la lista; excluido de `/register` y `/building-request` (no
  es un edificio real al que nadie deba poder unirse).
  **Confirmado el riesgo anotado**: `GET_AllBuildings`/`GET_AllBuildingsPublic`/
  `GET_BuildingById` (los 3 procs pre-existentes que devuelven `Building`, no
  tocados por el script anterior) tenían columnas explícitas sin `IsTemplate` --
  EF exige que TODA columna del modelo esté en el resultado de cualquier
  `FromSqlRaw<Building>`, así que rompía con `InvalidOperationException: The
  required column 'IsTemplate' was not present` apenas alguien reconstruía su
  sesión (mismo síntoma que el bug de `DefaultCategory`/`WaterReadingDefault` del
  Paso 1: bucle de login). Corregido en
  `Database/Scripts/2026-09-02_20_Fix_Building_IsTemplate_MissingColumns.sql`
  (texto real de los 3 procs confirmado por el usuario, sólo se agregó la
  columna faltante a cada `SELECT`, JOIN/WHERE/ORDER BY intactos).
- [x] **Paso 3 — `Parameter`: `IsSystemDefault`, split Sistema/Mixto, cerrar creación de
  grupos raíz en `/parameter`, clonado de hijos Mixto al crear Building.**
  **6 de 6 sub-pasos hechos, sin probar contra la BD real todavía.**
  - **1-2 (schema + migración)**: `Database/Scripts/2026-09-02_21_Parameter_SistemaMixto.sql`.
    `Parameter.IdBuilding` admite `NULL` (`NULL` = Sistema/global, un guid = Mixto
    de ese edificio). `IsSystemDefault BIT` nuevo -- doble sentido según el tipo de
    fila: en la RAÍZ de un grupo, 1=Sistema/0=Mixto (hace falta esta marca aparte
    porque la raíz siempre tiene `IdBuilding NULL` en los dos casos, no hay forma
    de distinguirlos sólo con eso); en un HIJO de un grupo Mixto, 1=vino del
    template/0=lo agregó un admin. Migra por `ShortDescription` los 13 grupos ya
    cargados: 11 a Sistema (raíz e hijos a `IdBuilding NULL`, raíz con
    `IsSystemDefault=1`), Método de Pago y Tipo de Incidente quedan Mixto (sólo la
    raíz a `NULL`, hijos actuales conservan su `IdBuilding` y quedan
    `IsSystemDefault=1`).
  - **3 (`GET_AllParameters`)**: `Database/Scripts/2026-09-02_22_GetAllParameters_SistemaMixto.sql`,
    con el texto real que pasó el usuario. Dos fixes sobre esa base: `IdBuilding`
    se coalesa con `ISNULL(IdBuilding, '00000000-...')` (si no, explota igual que
    `DefaultCategory` en el Paso 1 apenas exista una fila Sistema) y el `WHERE`
    pasa a `IdBuilding = @IdBuilding OR IdBuilding IS NULL` (si no, un edificio
    nunca vería sus valores de Sistema). Se agrega `IsSystemDefault` al `SELECT`.
  - **Bug encontrado de paso, ya corregido**: `INS_Parameter` (confirmado con su
    CREATE PROCEDURE real) declara `@Estado BIT`, pero el fix anterior de
    `UPD_Parameter` mandaba el int crudo del enum (`Inactivo=2`) a un parámetro
    declarado `@Estado INT` en ESE proc -- SQL Server lo convertía implícito a BIT
    al asignarlo (`2` -> `1`), así que marcar un Parameter Inactivo en realidad lo
    guardaba Activo. Se volvió a mandar como `bool` en los dos procs.
    `AddNewRecordAsync(Parameter)` (`INS_Parameter`) también se reescribió con
    `SqlParameter` explícitos (mismo patrón que `UPD_Parameter`) para poder
    traducir `IdBuilding == Guid.Empty` a `DBNull` al crear un valor de Sistema, y
    de paso el mismo tratamiento para `IdParent=0` al crear un grupo raíz nuevo
    (mismo problema de FK que ya vimos en `UPD_Parameter`, no reportado todavía
    porque nadie había creado un grupo raíz desde que se corrigió eso).
  - **4 (`/parameter` cerrado)**: cambió el alcance respecto a lo hablado en su
    momento -- en vez de "SysAdmin-only para Sistema", quedó **de sólo lectura
    para todos, SysAdmin incluido** (regla única: `IdBuilding == Guid.Empty`
    identifica un valor de Sistema o la raíz de cualquier grupo -- ninguno de los
    dos se toca desde `/parameter`, sólo vía script). Se sacó la opción "(Ninguno
    - Grupo Principal)" del combo Padre -- nadie crea grupos raíz nuevos, ni
    siquiera SysAdmin, porque un grupo nuevo no tiene ninguna pantalla que lo
    consuma todavía. `ShowAddChildModal` sigue disponible pero sólo para grupos
    Mixto (bloqueado si `parent.IsSystemDefault`). También se sacó el botón
    "Eliminar" (root e hijos) -- `DeleteParameterAsync` estaba comentado/"Not
    Implemented" desde antes, así que nunca funcionó; el método de la página que
    lo llamaba quedó dead code y se borró.
  - **5 (clonado de Mixto al crear Building)**: `IBuildingService.CreateBuildingAsync`
    ahora también clona los hijos Mixto propios del template (`CloneMixtoParametersAsync`,
    mismo mecanismo que `ApplyTemplateDefaultsAsync` del Paso 2) -- sólo los hijos
    que pertenecen de verdad al template (`IdBuilding == template.IdBuilding`), no
    su raíz (ya global). Quedan `IsSystemDefault=true` en el edificio nuevo.
  - **6 (los 5 lugares con `IdTabla` hardcodeado)**: se resuelven solos, sin tocar
    código -- como los grupos Sistema (Tipo Unidad=4, Distribución=8, Tipo
    Doc=11, Tipo Edificio=34, y el orden de `Value` de Prioridad de Incidente) son
    ahora una sola fila global y de sólo lectura desde `/parameter`, el `IdTabla`
    fijo que esas 5 pantallas asumían va a estar bien para cualquier edificio, y
    nadie puede reordenar los `Value` de Prioridad para romper el mapeo de
    colores. No hizo falta editar ninguna de las 5.
- [x] Paso 4 — `Category`: FK real, clonado del set default, alta inline desde Presupuesto.
  **Confirmado end-to-end contra la BD real.** Empezado por el clonado (a pedido del usuario, antes que la FK):
  `IBuildingService.CreateBuildingAsync` ahora también clona el set completo de
  Categorías del template (`CloneCategoriesAsync`). A diferencia de Parameter,
  `Category` usa `Guid` como PK (no un `IDENTITY int`), así que clonar necesita dos
  pasadas -- primero las raíces (`Nivel == 0`), generando un Guid nuevo para cada
  una y armando un mapeo template→nuevo; después los hijos (`Nivel != 0`), usando
  ese mapeo para que su `ParentId` apunte al padre YA CLONADO en el edificio nuevo,
  no al del template.
  **Riesgo pre-existente encontrado, sin tocar todavía**: `AddNewRecordAsync
  (Category)` (`BDLayout.Add.cs`) arma `@ParentId` con
  `category.ParentId == Guid.Empty ? null! : category.ParentId!` -- un `null`
  pelado (no una excepción DBNull sin tipo como las que ya corregimos, pero la
  misma familia de problema) pasado al mecanismo posicional de
  `ExecuteStoredProcedureAsync`. Si esto explota al clonar/crear una Categoría
  RAÍZ (`ParentId = Guid.Empty`), es sospechoso #1 -- mismo fix que ya usamos para
  `Parameter.IdParent`/`IdBuilding` (`SqlParameter` explícito), pero hace falta el
  `CREATE PROCEDURE` real de `INS_Category` antes de tocarlo (no lo tengo,
  a diferencia de `INS_Parameter`/`INS_Building`).
  **Alta inline desde Presupuesto: ya estaba implementada, no hizo falta tocar
  nada.** `BudgetGenerator.razor` (`AddNewSectionFromModal`, modal "Nueva Sección")
  ya permite escribir un nombre que no matchea ninguna Categoría existente; el
  guardado (`BudgetService.SaveBudgetAsync` → `SaveCategoriesAsync`/
  `SaveCategoryAsync`) inserta la `Category` real ANTES que los `BudgetDetail` que
  la referencian, dentro de la misma transacción -- justo lo que hace falta para
  que el FK nuevo (abajo) no rompa este flujo. Ojo: `SaveCategoriesAsync` sólo
  corre cuando `state.Status` es `Created` o `Rejected` -- si se agrega una sección
  nueva con el presupuesto ya en `Check`/`Active`, la Category no se crea antes del
  `BudgetDetail` y el INSERT va a fallar contra el FK nuevo (antes esto quedaba
  como fila huérfana silenciosa). No se tocó porque no está confirmado que ese
  camino sea alcanzable desde la UI hoy -- si aparece el error, es acá.
  También se encontró código muerto en el mismo archivo,
  `AddNewSectionFromModal1` (variante vieja, no la llama ningún botón) -- no se
  tocó, fuera de alcance de este paso.
  **FK real**: `Database/Scripts/2026-09-02_24_Category_RealFK.sql` -- agrega
  `FK_Expense_Category`/`FK_Exoneration_Category`/`FK_BudgetDetail_Category`/
  `FK_CalendarItem_Category`, cada tabla independiente (se salta sola si ya tiene
  un FK o si tiene filas huérfanas, sin abortar las otras tres). El propio
  comentario de `SaveCategoriesAsync` ya nombraba `FK_BudgetDetail_Category` como
  causa de un error real visto antes, así que puede que ya exista a mano en la BD
  -- el script lo detecta y no lo duplica. `DeleteCategoryAsync` ahora devuelve
  `OperationResult` y distingue la violación de FK (SQL error 547) para mostrar
  "está en uso" en vez de tragarse el error en silencio como antes (bug de la
  misma familia que el de `SaveBuilding` de más arriba: `CategoryPage.razor`
  igual no mostraba nada aunque el borrado fallara).
  **Corrido contra la BD real, confirmado por el usuario**: diagnóstico en 0
  huérfanos en las 4 tablas. `Expense`, `Exoneration` y `BudgetDetail` YA tenían
  un FK real puesto a mano (confirma la sospecha de arriba sobre
  `FK_BudgetDetail_Category`); sólo `CalendarItem` no lo tenía y quedó creado
  (`FK_CalendarItem_Category`) por este script. Las 4 tablas quedan protegidas.
- [ ] Paso 5 — `ReplacedByIdTabla` (sin apuro). **Alcance de hoy: schema + campo, y
  pantalla de Promoción (SysAdmin). Reportes usando la cadena queda para después,
  a pedido explícito del usuario -- no se tocó ningún reporte/PDF.**
  **Semántica de "promover" confirmada con el usuario**: la fila elegida como
  canónica pasa a `IdBuilding = NULL` de verdad -- se vuelve global (la ve
  cualquier edificio, mismo mecanismo que ya trae los valores Sistema) aunque el
  grupo siga clasificado Mixto en su raíz. Es la única excepción a "los hijos de
  un grupo Mixto siempre tienen IdBuilding" (§5.2) -- deliberada, sólo para la
  fila que un SysAdmin promueve a mano.
  **Schema**: `Database/Scripts/2026-09-02_25_Parameter_Promotion.sql` -- columna
  `Parameter.ReplacedByIdTabla INT NULL` + FK auto-referenciada hacia
  `Parameter.IdTabla` (segura, arranca NULL para todo lo existente). Tres procs
  nuevos, autoría propia (no tocan `INS_Parameter`/`UPD_Parameter` existentes):
  `GET_MixtoParameterCandidates` (todos los hijos Mixto activos y todavía no
  globales, de TODOS los edificios a la vez, con nombre de grupo y de edificio --
  la vista base para que el SysAdmin detecte duplicados a ojo, la detección sigue
  siendo manual a propósito), `UPD_PromoteParameterToGlobal` (`IdBuilding -> NULL`
  sobre la fila canónica) y `UPD_MergeParameterInto` (la fila duplicada queda
  `Estado = Inactivo` + `ReplacedByIdTabla` apuntando a la canónica -- nunca se
  borra, mismo criterio que el resto de Parameter).
  **UI**: `ParameterPromotion.razor` (`/parameter-promotion`, SysAdmin-only,
  entrada nueva "Fusionar duplicados" en `/parameter`) agrupa los candidatos por
  grupo + descripción corta normalizada (trim + mayúsculas) para sugerir clusters
  de duplicados: cada cluster deja elegir cuál fila es la canónica (radio) y
  cuáles se fusionan hacia ella (checkbox, todas las demás del cluster
  preseleccionadas). Un switch "Mostrar todos" saca también los valores sin
  duplicado detectado, por si el nombre difiere entre edificios y hace falta
  fusionar a mano algo que el agrupamiento automático no encontró.
  `IParameterPromotionService` (nuevo, separado de `ParameterService` porque este
  opera sobre TODOS los edificios a la vez, no sobre `CurrentBuilding`) orquesta
  promover + fusionar; ambos pasos son idempotentes así que no hace falta una
  transacción compartida entre las dos llamadas.
  **Riesgo encontrado de paso, no corregido acá (fuera de alcance) -- tarea
  separada sugerida**: no quedó confirmado si `Parameter.Estado` (enum
  `Activo=1/Inactivo=2`) se lee bien desde la columna `Estado BIT` (valores 0/1)
  sin un `HasConversion` configurado en `SpiderHoodContext` -- si no lo hay, una
  fila que este mismo paso deja Inactivo (`Estado=0`) podría materializarse como
  un valor de enum indefinido en cualquier lectura fuera de
  `GET_MixtoParameterCandidates` (que filtra `Estado = 1` y por eso no lo pisa).
  **Bug encontrado al probar, ya corregido**: `GET_AllParameters` (el proc que
  llena TODO `Parameter` en la app, no sólo `/parameter-promotion`) no había sido
  actualizado con la columna nueva -- mismo síntoma ya visto varias veces esta
  sesión (`InvalidOperationException: required column 'ReplacedByIdTabla' not
  present`), esta vez rompiendo hasta el Dashboard para cualquier usuario.
  Corregido en `Database/Scripts/2026-09-02_26_Fix_GetAllParameters_ReplacedByIdTabla.sql`
  (agrega la columna al `SELECT`, sin `ISNULL` porque el modelo ya la tipa `int?`).
  **Segundo bug encontrado al probar, ya corregido**: `GET_MixtoParameterCandidates`
  traía TODOS los hijos Mixto activos de cada edificio, incluidos los clonados del
  template al crear el edificio (los 4 defaults de Método de Pago, por ejemplo) --
  esos no son duplicados reales, son la misma fila copiada a propósito una vez por
  edificio (§5.2), así que salían como "2 edificios" en CUALQUIER par de edificios
  con el mismo default, sin que ningún admin haya agregado nada dos veces.
  Confirmado en vivo por el usuario. La marca para distinguirlos ya existía
  (`IsSystemDefault` en el hijo: 1 = clonado del template, 0 = lo agregó el admin
  a mano) -- sólo faltaba usarla en el `WHERE`. Corregido en
  `Database/Scripts/2026-09-02_27_Fix_PromotionCandidates_ExcludeDefaults.sql`
  (agrega `AND c.IsSystemDefault = 0`) -- la lista ahora sólo muestra valores que
  un Administrador agregó de verdad por su cuenta.
  **Sin confirmar contra la BD real todavía** -- falta que el usuario corra los
  dos últimos scripts (`_26` y `_27`).

## 1. Problema de fondo

Hoy, cuando se crea un Building nuevo, no viene con ningún valor default:
`BuildingConfiguration`, `Parameter` y `Category` quedan vacíos, y hay que cargarlos
a mano (scripts SQL one-off, como
`Database/Scripts/2026-09-02_14_Seed_IncidentTypeAndPriorityParameters.sql`, que el
propio script advierte que hay que ajustar y correr a mano por cada edificio).

Se evaluó hardcodear los defaults en C#, pero se descartó: no cubre `Parameter`/
`Category` (que son datos de tabla, no config de código), y cualquier ajuste a los
defaults requeriría un deploy.

## 2. Gap previo detectado: crear un Building no persiste

`BuildingPage.razor.cs` (`SaveBuilding()`, líneas ~154-217) sólo agrega el objeto a
una lista en memoria (`Buildings.Add(...)`) — no hay `INS_Building` ni ningún método
que lo grabe en la BD. Todo lo de abajo depende de que crear un Building sea una
operación real contra la base, así que esto es **prerrequisito**, no parte separada.

## 3. Mecanismo compartido: "Edificio Template"

Un Building especial, visible y editable sólo por SysAdmin (con las mismas pantallas
que un edificio normal: Configuración, Parámetros, Categorías), que sirve como fuente
de los defaults. Falta decidir cómo se identifica en código (flag `IsTemplate` en
`Building`, o un Guid reservado/bien conocido) — **pendiente de decisión**, ver §7.

Al crear un Building real, el flujo de creación clona desde el template:

- `BuildingConfiguration` completa (ver §4).
- Los **hijos** de cada grupo `Parameter` Mixto (ver §5) — la raíz del grupo NO se
  clona, es global.
- Todas las `Category` default (ver §6).

## 4. BuildingConfiguration

Ya existe un fallback parcialmente hardcodeado:
`BuildingService.CreateDefaultConfigurationAsync()` (`IBuildingService.cs:151-189`)
devuelve una config con valores fijos en C# (moneda "PEN", DueDay=5, dos métodos de
pago fijos, contactos de ejemplo). Este método pasa a ser innecesario: los defaults
salen de clonar la fila de `BuildingConfiguration` del Edificio Template, no de código.

Campos pedidos como default (de la lista original):

- Moneda
- Métodos de Pago → **no es un campo de `BuildingConfiguration`, sale de `Parameter`**
  (grupo Mixto "Método de Pago", ver §5)
- Periodo de Pago
- Día de Vencimiento
- Consumo Mínimo
- Cargo Fijo
- Monto Multa
- Tasa de Interés
- Día Emisión Recibos
- Alerta Deuda / Deuda Crítica (hoy sólo en memoria, `BuildingConfiguration.cs:68-69`
  — falta persistirlos en `INS_BuildingConfiguration`/`UPD_BuildingConfiguration`,
  que hoy sólo insertan ~9 de los campos del modelo)
- Texto Pie de Recibo (sugerido, editable después por el edificio real)

## 5. Parameter: Sistema vs Mixto

Dos niveles únicamente. No hay un tercero donde el Administrador cree grupos raíz
nuevos — no tendría dónde consumirse (ninguna pantalla referencia un grupo que el
admin inventó), así que esa capacidad no existe para nadie salvo SysAdmin/dev.

### 5.1 Sistema

Grupo raíz + todos sus hijos son **globales**: una sola fila por grupo/valor, sin
`IdBuilding` (o con un sentinel), compartida por todos los edificios. El Administrador
de edificio no los ve en `/parameter` ni puede tocarlos.

Como nunca se duplican por edificio, **no hace falta clonarlos** al crear un Building
— se leen directo del set global. De regalo, esto arregla en el mismo movimiento el
bug de los `IdTabla` hardcodeados (ver §7): al no haber una copia por edificio, el
`IdTabla` de un grupo Sistema queda fijo para siempre.

### 5.2 Mixto

La **raíz** del grupo es una sola fila global (igual que Sistema, sólo para que el
`IdParent` sea estable). Los **hijos** son siempre por edificio (`IdBuilding`
seteado), sin excepción:

- Al crear un Building, se clonan los hijos default del template a la BD del edificio
  nuevo, marcados `IsSystemDefault = 1`.
- El Administrador del edificio puede agregar sus propios hijos (`IsSystemDefault = 0`,
  `IdBuilding` = su edificio) — sólo visibles/usables en su edificio, nunca en otros.
- El Administrador puede **inactivar** cualquier hijo de su copia (default o propio)
  si no lo usa.
- **Nunca se elimina un hijo de verdad**, ni siquiera los que agregó el propio
  Administrador — no hay FK real hacia `Parameter` en ninguna tabla que lo consuma
  (confirmado: cero `REFERENCES ... Parameter` en los scripts), así que no hay forma
  barata de saber si un valor está en uso antes de borrarlo. Sólo inactivar +
  ocultar de las listas activas.

Campo nuevo necesario en `Parameter`: `IsSystemDefault BIT`.

### 5.3 Promoción / fusión de duplicados (a futuro, sin apuro)

Si el mismo valor termina siendo agregado independientemente por varios
Administradores (ej. "Yape" como Método de Pago en 5 edificios distintos), se puede
promover a Sistema sin tocar histórico:

1. Se crea (o ya existe) el valor global en Sistema.
2. La fila vieja por-edificio se deja `Inactivo` y se le setea un nuevo campo
   `ReplacedByIdTabla` (nullable, apunta a `Parameter.IdTabla`) = el ID del nuevo
   valor global.
3. Ningún registro histórico (`Incident`, `Expense`, etc.) se toca — siguen apuntando
   al `Value` viejo, que sigue existiendo (sólo inactivo), así que el detalle de
   transacciones viejas se sigue viendo bien.
4. Los reportes que agrupan por este Parameter usan la regla: "si tiene
   `ReplacedByIdTabla`, agrupar por ese destino en vez de por sí mismo" — así el
   total no queda fragmentado entre el valor viejo y el nuevo.

La **detección** de candidatos a promover queda manual (SysAdmin corriendo una query
que cuente nombres duplicados entre edificios) — no se arma nada automático por ahora.

### 5.4 Clasificación de los 13 grupos existentes hoy

| Grupo | Nivel |
|---|---|
| Estado (Activo/Inactivo) | Sistema |
| Tipo de Unidad (DPTO/EST/DEP) | Sistema — *ver bug §7, hoy hardcodeado `IdParent==4`* |
| Distribución Gasto (Fija/%) | Sistema — *hoy hardcodeado `IdParent==8`* |
| Tipo de Documento (DNI/RUC/...) | Sistema — *hoy hardcodeado `ParamParent.DocumentType`=11* |
| Estado de Gastos | Sistema |
| Estado de Conciliación | Sistema |
| Tipo de Cuenta (Ahorro/Corriente) | Sistema |
| Tipo de Edificio (Familiar/Comercial/Mixto) | Sistema |
| Frecuencia Item Presupuesto | Sistema |
| Estado Presupuesto | Sistema |
| Prioridad de Incidente | Sistema — *`IncidentList.PriorityBadgeClass` asume orden fijo 1-4 por `Value`, no puede reordenarse por edificio* |
| Método de Pago | **Mixto** |
| Tipo de Incidente | **Mixto** |

## 6. Category

Más simple que Parameter: **sin nivel Sistema**, todo vive por edificio, y
explícitamente **sin mecanismo de fusión/promoción** de duplicados entre edificios (a
diferencia de Parameter — decisión explícita, no se va a homologar en el futuro).

- El Edificio Template define el set default de Categorías.
- Al crear un Building, se clona el set completo a la BD del edificio nuevo.
- El Administrador puede agregar categorías nuevas libremente, incluyendo **inline al
  crear un ítem de Presupuesto** (requisito de UX — falta definir la pantalla exacta).
- El Administrador puede **eliminar de verdad** una categoría de su copia (no la del
  template) — a diferencia de Parameter, acá sí se permite borrado real.
- Esto requiere agregar **FK real** `IdCategory → Category.IdCategory` en las tablas
  que lo consumen, hoy sin ninguna restricción real (confirmado: el propio script
  `2026-09-02_12_CalendarCategoryFromCategoryTable.sql` documenta la relación como
  "FK lógica", no física). Tablas identificadas con columna `IdCategory`:
  - `Expense`
  - `Exoneration`
  - `BudgetDetail`
  - `CalendarItem`

  Antes de crear cada constraint hay que verificar que no haya `IdCategory`
  huérfanos ya existentes en esas tablas (la creación de la FK falla si los hay).

## 7. Gaps / bugs actuales detectados durante esta sesión (a corregir junto con lo anterior)

- **Building no persiste al crearse** (§2) — bloqueante para todo lo demás.
- **`IdTabla` hardcodeado en 5 pantallas** (confirmado en vivo: crear un Building
  sin Parámetros propios rompe el badge de Tipo de Edificio hasta para el edificio
  viejo, porque la búsqueda depende de qué edificio esté "activo" en la sesión):
  `ModalUnit.razor:30`, `UnitGroups.razor:469` (`IdParent==4`),
  `BudgetGenerator.razor:216` (`IdParent==8`), `ModalOwner.razor:95`
  (`ParamParent.DocumentType`=11), `BuildingPage.razor:118`
  (`GetChildParameterDescription(34, building.Type)`). Se resuelven solos al hacer
  Sistema global (§5.1) — no hace falta tocar estas 5 pantallas si el `IdTabla` de
  esos grupos deja de duplicarse por edificio.
- **`/parameter` permite crear grupos raíz** a cualquiera con el permiso
  `manage_parameters` (`ParameterPage.razor`, `ShowAddModal`, opción "(Ninguno -
  Grupo Principal)"). Hay que restringirlo — nadie salvo SysAdmin crea grupos raíz
  (§5, nota inicial).
- **`ParameterService.LoadParametersAsync`/`GET_AllParameters`** filtran todo por
  `@IdBuilding`. Con Sistema global, la consulta tiene que traer dos cosas a la vez:
  los grupos Sistema completos (sin filtrar por edificio) + los grupos Mixto
  filtrados por `IdBuilding` (raíz global + sólo los hijos de ese edificio).

## 8. Decisiones pendientes / a confirmar

- Cómo se identifica el Edificio Template en código (flag `IsTemplate` en `Building`
  vs. Guid reservado). Afecta permisos, queries y la pantalla de selección de
  edificio (que no debería listar el template para un Administrador normal).
- ¿El SysAdmin edita el template con las mismas pantallas que un edificio real
  (Configuración/Parámetros/Categorías), o se arma una pantalla dedicada?
- Orden sugerido de implementación (a validar):
  1. Persistir creación de Building (§2).
  2. Edificio Template + clonado de `BuildingConfiguration` (§3, §4) — el más simple,
     una sola fila a copiar.
  3. `Parameter`: campo `IsSystemDefault`, split Sistema/Mixto, ajustar
     `LoadParametersAsync`/`GET_AllParameters`, cerrar creación de grupos raíz en
     `/parameter`, clonado de hijos Mixto al crear Building.
  4. `Category`: FK real en las 4 tablas, clonado del set default al crear Building,
     alta inline desde Presupuesto.
  5. `ReplacedByIdTabla` (§5.3) — sin apuro, cuando aparezca el primer caso real de
     duplicado a promover.
