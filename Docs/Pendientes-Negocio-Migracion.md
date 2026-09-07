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
