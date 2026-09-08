# Pendientes de negocio — Migración de Datos Históricos

Backlog de decisiones/cambios de **lógica de negocio** que salieron durante el
trabajo de migración de datos históricos (plantillas de carga en
`/migracion/plantillas`, `Services/IMigrationTemplateService.cs`), pero que
**no** se implementan en ese branch a propósito -- son cambios de
comportamiento que afectan a todos los edificios en producción, no solo a
los que se migran, y merecen su propio branch/revisión en vez de mezclarse
con las plantillas.

Cada ítem: qué falta, por qué se detectó acá, y dónde vive el código relevante.

---

## 1. Unidades sin propietario deberían facturarle a la inmobiliaria

**Estado: pendiente, sin empezar.**

Regla de negocio (confirmada por el usuario): mientras un Departamento/Oficina
no tiene comprador, la cuota de esa unidad (calculada por %) le corresponde a
la inmobiliaria que vende el edificio, no a nadie más.

**Hoy no pasa nada de eso.** Se verificó leyendo el código:
`IBudgetService.LoadDataDefaultAsync` (`Services/IBudgetService.cs:278-283`)
arma `state.Owners` a partir de `GET_OwnerByBuilding`, filtrado a
`Role == 1 && TypeUnit == 1` -- un JOIN Owner↔Unidad. Una unidad sin
propietario asignado **no aparece en el resultado**, así que no se le genera
ningún `Installment`. No es que facture mal; no factura.

**Decisión ya tomada con el usuario**: no crear un `Owner` ficticio -- usar el
contacto `RealEstateCompany` que ya existe en `BuildingConfiguration`
(`Contact.TypeContact == 2`) como pagador por defecto de las unidades libres.

**Lo que falta diseñar/implementar:**
- Cómo entran las unidades libres (`RealEstateUnit` sin `GroupUnit`) al mismo
  pipeline de generación que hoy solo recorre `OwnerUnitView` -- `Installment`
  necesita `IdGroupUnit`, `OwnerName`, `Percent`, y las libres no tienen
  `GroupUnit` real todavía.
- Si conviene sintetizar un `IdGroupUnit` por unidad libre (una por una) o
  agruparlas todas bajo un solo grupo "Inmobiliaria" del edificio.
- Qué pasa si el edificio no tiene `RealEstateCompany` configurado (hoy es un
  campo opcional de `BuildingConfiguration`).

---

## 2. Historial de propietarios por periodo

**Estado: pendiente, sin empezar.** (Punto 4 del análisis de migración original)

El Excel de control (`Recibos`) guarda el nombre del propietario en cada fila
-- pudo cambiar en los ~10 años de historial. El usuario confirmó: **el
sistema debería guardar historial de propietarios**, no solo el vigente.

**Estado actual del modelo**: `Owner`/`OwnerUnit`/`GroupUnit` no versionan por
fecha -- un `IdGroupOwner` representa la relación vigente, sin rango de
vigencia. `Installment.OwnerName` es texto libre (no FK), así que hoy ya
tolera guardar un nombre "histórico" por cuota sin romper nada, pero no hay
forma de consultar "quién era dueño de la 301 en marzo de 2019" de manera
estructurada.

**Lo que falta diseñar:** probablemente una tabla de historial
(`OwnerUnitHistory` o similar) con vigencia (`FechaDesde`/`FechaHasta`) por
`IdGroupOwner`↔`IdUnit`, sin tocar el modelo "vigente" que ya usa el resto de
la app.

---

## 3. Marcar un movimiento bancario como "Saldo Inicial"

**Estado: pendiente, sin empezar** (solo existe la columna en la plantilla,
sin la función real).

Al crear un edificio, las cuentas bancarias se asocian con su
`InitialBalance` (una sola vez, no editable después --
`Classes/Budget/BankAccount.cs`). El usuario pidió una función adicional: en
la pantalla de conciliación/carga de estado de cuenta, poder marcar UN
movimiento como "este es el saldo inicial" y que eso alimente
`InitialBalance` directamente, en vez de que el admin lo tipee a mano en la
ficha de la cuenta.

La plantilla `Plantilla_EstadoDeCuenta` (`GenerarPlantillaEstadoDeCuentaAsync`)
ya tiene la columna "Es Saldo Inicial" pensada para esto, pero **el
importador que la lea y la función en la UI de conciliación
(`ReconciliationPages/`) todavía no existen.**

---

## 4. Un caso sin conciliar en el Excel de Nova Alzamora

**Estado: pendiente, a resolver manualmente en el Excel** (decisión ya tomada
con el usuario, punto 5 del análisis original).

De 1,602 `InstallmentPaid` con `IDRepFIN` real, 1,601 coinciden con
`Consolidado.ID` -- queda **1 caso sin match verificado**. El usuario indicó
que lo corrige a mano en el Excel cuando se llegue a la fase de migración real
de datos (no antes). No requiere cambio de código; queda anotado para no
perderlo de vista al ejecutar el ETL final.

---

## 5. (Técnico, no de negocio) `GET_UnitsByType` no tolera unidades sin grupo

**Estado: mitigado en `IMigrationTemplateService`, sin corregir de raíz.**

`BuildingService.GetGroupUnitsByTypeAsync` (`GET_UnitsByType`) tira
`SqlNullValueException` si alguna unidad del edificio tiene una columna Guid
en NULL (típicamente `IdGroupUnit` de una unidad sin grupo/propietario
asignado). Mismo llamado que ya usa el botón "Descargar plantilla" de
Lecturas de Agua en producción (`BlockWaterReading.razor`) -- afecta a
**cualquier edificio a mitad de configurar unidades**, no solo a
`IMigrationTemplateService`.

Se agregó un `try/catch` local en `ObtenerCodigosUnidadAsync`
(`Services/IMigrationTemplateService.cs`) para que esa falla no tumbe la
descarga de plantillas, pero la causa de fondo (el SP/mapeo EF no filtra ni
maneja unidades sin grupo) sigue viva en el resto de la app. Diagnóstico
reutilizable en
`Database/Scripts/2026-09-07_54_Diagnostico_UnitsNullGuidColumns.sql`.

---

## 6. Bugs encontrados probando el importador (no causados por él)

**Estado: 6.1, 6.2 y 6.4 resueltos (2026-09-08, branch
`claude/fixes-al-aplicativo-8mtchm`); 6.3 mitigado (FK preventiva, sigue sin
existir borrado de edificios).** Encontrados por el usuario probando
`/migracion/plantillas` con datos reales de varios edificios -- son fallas
generales de la app (gestión de edificios/contactos/cuentas), no del
importador ni de las plantillas.

### 6.1 Contactos no se graban con el `IdRelatedEntity` correcto

**Resuelto (2026-09-08) -- causa real distinta de lo que se sospechaba acá
abajo.** No era `SelectBuilding` (ese bug, real y ya arreglado, es el de 6.2,
pero no era la causa de ESTE síntoma) ni la escritura -- era el SP de lectura,
confirmado con `sp_helptext GET_AllContacts` en producción:

```sql
CREATE PROCEDURE dbo.GET_AllContacts
@IdRelatedEntity    UNIQUEIDENTIFIER
AS
BEGIN
    SELECT IdContact, TypeContact, Name, Phone, Email, Address,
           ISNULL(OfficePhone,'') AS OfficePhone,
           ISNULL(MobilePhone,'') AS MobilePhone,
           IdRelatedEntity
    FROM   Contact
    -- sin WHERE -- devolvía TODA la tabla Contact, ignorando @IdRelatedEntity
END
```

Cada `Contact` individual SÍ tenía el `IdRelatedEntity` correcto grabado (por
eso una consulta directa a la tabla no mostraba nada raro) -- el problema es
que el SP los devolvía TODOS juntos para cualquier edificio, y
`BuildingService.GetConfigurationAsync` hace
`contacts.FirstOrDefault(c => c.TypeContact == 1)` sobre esa lista completa:
cualquier edificio terminaba mostrando el contacto Admin/Inmobiliaria/
Mantenimiento del PRIMER `Contact` de ese tipo que hubiera en TODA la base
(en la práctica, el del edificio Template, creado primero). Fix en
`Database/Scripts/2026-09-08_58_Fix_GET_AllContacts_SinFiltro.sql` --
agrega `WHERE IdRelatedEntity = @IdRelatedEntity`.

`Contact.IdRelatedEntity` debería apuntar a
`BuildingConfiguration.IdBuildingConfiguration` -- así es como
`GetAllContactsAsync` filtra cuál contacto (Admin/Inmobiliaria/Mantenimiento)
pertenece a cada edificio.

Revisado el lado C#, y ahí se ve correcto en los dos sentidos:
- Escritura: `BuildingPage.razor.cs:469/484/499` setea
  `IdRelatedEntity = interno.IdBuildingConfiguration` antes de
  `AddContactAsync`, y `BDLayout.Add.cs:626` lo pasa tal cual a `INS_Contact`.
- Lectura: `BDLayout.Get.cs:164-172` (`GetAllContactsAsync`) pasa
  `idBuildingConfiguration` tal cual a `GET_AllContacts`.

Como ambos lados parecen correctos desde acá, los sospechosos son: (a) el
stored procedure `INS_Contact`/`GET_AllContacts` en sí (no está en
`Database/Scripts/`, no se pudo revisar), o (b) que `interno`/`config`
tenga un `IdBuildingConfiguration` viejo/de otro edificio al momento de
guardar -- mismo patrón sospechado en el punto 6.2.

### 6.2 Cuentas bancarias se graban todas con el mismo `IdBuilding`

**Resuelto (2026-09-08).** Confirmado: `SelectBuilding` sólo hacía
`SelectedBuilding = building;` -- nunca volvía a pedir la `Configuration`
completa de ese edificio. Sólo el primer edificio (el que carga
`CargarDatosPagina` al entrar a la página, vía `GetConfigurationAsync`) tenía
la versión completa (con `BankAccounts`, `Exonerations`, etc.); cualquier otro
edificio seleccionado después seguía con la versión liviana que ya traía
`currentUser.Buildings` desde el login (`GetAllBuildingsConfigAsync`, ver
`IUserSessionLoader`). `SelectBuilding` ahora es `async Task` y llama a
`GetConfigurationAsync(SelectedBuilding.IdBuilding)` cada vez que cambia el
edificio seleccionado, antes de que el usuario pueda tocar nada de esa
configuración.

`BuildingPage.razor.cs:447`:
```csharp
bankaccount.IdBuilding = interno.IdBuilding;
```
Se asigna en el momento de guardar, tomando `interno.IdBuilding` -- si
`interno` (el estado de configuración del componente) no se refresca al
cambiar de edificio dentro de la misma sesión/circuito de Blazor Server
(navegar de un edificio a otro sin que el componente se recree), toda cuenta
bancaria que se guarde después queda con el `IdBuilding` del primer edificio
cargado, no el que se está editando. Revisar el ciclo de vida de `interno`/
`SelectedBuilding` en `BuildingPage.razor.cs` (`OnParametersSetAsync` o
equivalente) al cambiar de edificio.

### 6.3 Sin FK real: borrar un edificio deja Contact y Parameter huérfanos

**Mitigado (2026-09-08)** -- FK preventiva agregada en
`Database/Scripts/2026-09-08_57_Contact_Parameter_RealFK.sql`, mismo patrón
autocontenido/re-ejecutable que `2026-09-02_24_Category_RealFK.sql`
(se saltea sola por tabla si ya existe el FK o si hay filas huérfanas, sin
ON DELETE en cascada). Sigue sin existir un `DeleteBuildingAsync` real en la
app -- este script sólo dispara si ese borrado se implementa a futuro o si
alguien vuelve a borrar un `Building` directo en la BD.

No existe una función de borrado de edificios en la app (no hay
`DeleteBuildingAsync` en `IBuildingService`, se comprobó buscando en todo el
repo) -- el borrado se hizo directo en la BD durante pruebas. Ahí se vio que
`Contact.IdRelatedEntity` y `Parameter.IdBuilding` no tienen una FK real
hacia `Building`, así que borrar un `Building` deja filas de `Contact` y
`Parameter` huérfanas en vez de fallar (como sí pasa hoy con `RealEstateUnit`
y `Category`, que sí tienen FK real -- ver
`IBuildingService.DeleteUnitAsync`/`ICategoryService.DeleteCategoryAsync`,
que atrapan el error 547 de SQL). Mismo patrón que resolvió
`Database/Scripts/2026-09-02_24_Category_RealFK.sql` para Category, pendiente
de replicar para Contact y Parameter -- o, si nunca va a haber borrado real
de edificios desde la app, documentar que es intencional.

### 6.4 Cuentas bancarias se pueden guardar con espacios al inicio/fin

**Resuelto (2026-09-08)** -- `IBankAccountService.AddBankAccount`/
`UpdateBankAccount` ahora recortan `AccountNumber` y `CCI` antes de guardar.

`AccountNumber` no se recorta (`.Trim()`) antes de guardarse -- confirmado en
producción: una cuenta de Nova Alzamora se guardó con un espacio de más al
inicio y al final desde la UI de Edificios (`BuildingPage.razor`), y cualquier
comparación exacta contra ese número (el importador de Estado de Cuenta, en
este caso) fallaba con "la cuenta no existe", aunque fuera visualmente la
misma cuenta. El importador ya se blindó recortando ambos lados de la
comparación (`Services/IMigrationImportService.cs`,
`ImportarEstadoDeCuentaAsync`) y `IMigrationTemplateService.ObtenerCuentasAsync`
también recorta al armar el dropdown de la plantilla, pero la causa de fondo
-- que se pueda guardar así desde la UI -- sigue sin corregirse. Revisar si
conviene hacer `.Trim()` en `IBankAccountService.AddBankAccount`/
`UpdateBankAccount` (o antes, en el formulario) para que no se pueda guardar
con espacios de entrada.

### 6.5 "Listado de Cuotas" (`/cuotas`) no respeta el orden que le pide la página

**Estado: pendiente, sin empezar.** Encontrado revisando cuotas migradas de
Nova Alzamora, pero es un bug general de `Classes/PaginationClass.cs` -- pasa
con cualquier edificio, no solo con datos migrados.

`InstallmentList.razor:383` arma la lista con
`.OrderByDescending(i => i.Period).ThenBy(i => i.UnitName)` antes de pasarla a
`pagination.Initialize(...)`, pero ese orden nunca llega a aplicarse:
`InstallmentPagination` (en `PaginationClass.cs`) se configura con
`InitializeConfiguration(..., "Period")`, y mientras no haya una columna
elegida a mano (`SortColumn` vacío), `ApplyFilterAndSort()` siempre reordena
con `FilteredData.OrderBy(defaultSort)` -- ascendente, de un solo criterio --
descartando por completo el orden (y el `ThenBy`) que le pasó el llamador.
Confirmado con el usuario: la pantalla debería mostrar lo más reciente
primero (descendente), no ascendente como sale hoy.

Dos formas de arreglarlo, a decidir cuando se aborde:
- Puntual: que `InstallmentPagination` arranque con orden descendente por
  columna 'Period' (revisar si `PaginationClass` ya soporta un
  `defaultSortAscending` o hay que agregarlo).
- De fondo: que `ApplyFilterAndSort()` respete el orden ya aplicado por el
  llamador cuando no hay `SortColumn` explícito, en vez de siempre re-ordenar
  por `_defaultSortColumn` ascendente -- afecta a cualquier otra pantalla que
  use `PaginationClass<T>` con esta misma suposición implícita, así que
  conviene revisar los demás usos antes de tocarlo.

### 6.6 "Conciliación de Pagos" falla con rangos de fecha amplios

**Estado: pendiente, sin diagnosticar la causa real.** El usuario reportó
`Error al cargar transacciones: Operation GetBankTransactionsNoConciliedAsync
failed` en `/conciliacion` (`ReconciliationPages/ReconciliationWorkspace.razor`)
al ampliar el filtro de fechas a todo el historial migrado (2015-2026) de
Nova Alzamora -- no se pudo confirmar la causa de fondo (el `GET_BankTransactionsNoConcilied`
no está en este repo, el mensaje visible es el wrapper genérico de
`RepositoryException`, sin el error real de SQL). Hipótesis más probable: un
timeout (`CommandTimeout` fijo en 30s en `BDLayout.ExecuteStoredProcedureAsync`)
al filtrar/mapear miles de movimientos en un rango de una década -- la
pantalla probablemente nunca se probó antes con tanto historial junto,
migración incluida. A confirmar con el detalle real del error la próxima vez
que se reproduzca.

De paso, revisando `CargarTransacciones()` (`ReconciliationWorkspace.razor:1148`)
se encontró un bug de UI aparte, menor pero real: `mensajeExito` y
`mensajeError` no se limpian al inicio del método, así que un mensaje de
éxito de una carga anterior se queda pegado en pantalla junto al error de una
carga posterior que sí falló -- confuso, pero no la causa del error en sí.

---

## 7. Los 5 importadores ya existen, pero no concilian entre sí

**Estado: los 5 importadores están construidos y funcionando**
(`Services/IMigrationImportService.cs`, uno por cada plantilla de
`/migracion/plantillas`).

- ~~Cuotas y Pagos no concilia contra Estado de Cuenta~~ -- **resuelto**
  (2026-09-08): ambas plantillas tienen ahora una columna opcional
  ('Referencia Original' en Estado de Cuenta, 'Referencia de Pago' + 'Cuenta
  Bancaria del Pago' en Cuotas y Pagos) para enlazar cada `InstallmentPaid`
  migrado con el `TransactionBankDetail` real que lo pagó, usando el ID que
  traía el sistema anterior del edificio (NO una conciliación automática por
  fecha/monto -- se decidió así tras confirmar con datos reales de Nova
  Alzamora que el `SequenceNumber` que asigna SpiderHood al cargar Estado de
  Cuenta se desfasa del ID original apenas se descarta una fila del archivo,
  y que fechas de pago pueden ser meses posteriores a la cuota que cubren).
  Necesitó una columna nueva, `TransactionBankDetail.OriginalReference` (ver
  `Database/Scripts/2026-09-08_56_TransactionBankDetail_OriginalReference.sql`)
  y 2 Stored Procedures nuevos, usados solo por el importador -- ninguna
  pantalla ni flujo de uso diario los toca. Si las columnas quedan vacías (la
  mayoría de edificios no van a tener este nivel de detalle), el
  comportamiento es igual que antes.
- **Estado de Cuenta no crea `Expense` categorizados.** La columna
  'Categoría' de la plantilla se lee y se valida, pero no se guarda en
  ningún lado -- `TransactionBankDetail` no tiene columna de categoría
  propia (la categorización real vive en `Expense`, conciliado aparte). Cargar
  egresos históricos ya categorizados como gasto es un alcance más grande que
  "registrar el movimiento bancario", no incluido todavía.

---

## 8. Tolerancia de redondeo al conciliar cuotas (diferencias < S/ 0.05)

**Estado: pendiente, sin empezar -- a confirmar si aplica** (palabras del
usuario: "la regla lo veremos luego a ver si aplica").

Con cuotas migradas de Nova Alzamora, `/cuotas` muestra varias como
"Parcial" con una `Deuda` de centavos (S/ 0.01 a S/ 0.05) que en la práctica
es solo redondeo acumulado del Excel original (LecAgua/MontoAgua con más
decimales de los que se guardan en `Monto`/`Fraccionado`), no una deuda real
pendiente de cobro. El usuario pidió: **diferencias menores a S/ 0.05 se
deberían dar por conciliadas totalmente**, no como "Parcial".

Es un cambio a la lógica de conciliación que corre para todos los edificios
a diario (dónde se calcula `Installment.Status`/`ConcilationType`, no solo
en la migración), así que queda fuera de este branch aunque salió a la luz
acá. Falta:
- Confirmar si aplica solo a cuotas migradas (donde el redondeo es
  conocido) o a la conciliación en general (edificios con datos cargados
  normalmente desde la app, sin este problema de origen).
- Ubicar dónde se decide hoy `Parcial` vs `Conciliada` (candidatos:
  `ImportarCuotasYPagosAsync` para lo migrado -- ya usa `deuda > 0`
  estrictamente -- y el flujo de conciliación en vivo,
  `ReconciliationWorkspace.razor`/`IInstallmentService`, para lo diario) y
  aplicar la tolerancia de forma consistente en ambos si corresponde.
