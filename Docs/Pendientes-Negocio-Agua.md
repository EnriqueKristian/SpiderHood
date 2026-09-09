# Pendientes de negocio — Lecturas de Agua / Consumo

Backlog de temas de **negocio** detectados al verificar el flujo de Lecturas
de Agua (`Components/Pages/WaterCalculationPages/BlockWaterReading.razor`,
usado tanto en `/waterreadings` como embebido en `ServiceReadingModal.razor`
dentro de la generación de Presupuesto).

---

## 1. `CargarPeriodo()` recalculaba (y podía re-guardar) el monto de lecturas ya guardadas, con la tarifa vigente HOY

**Estado: resuelto (2026-09-09), branch `claude/lista-pendientes-0gb03a`.**

El usuario preguntó si el **cálculo** de agua (no sólo la lectura cruda) se
guarda de verdad en BD, y pidió verificar que los reportes/vistas de
periodos ya cerrados usen ese valor guardado en vez de recalcular en
pantalla.

**Lo que se verificó:**

- El cálculo SÍ se guarda: `GuardarLecturas()` → `ServiceReadingService.AddServiceReadingDetailAsync(...)`
  → `INS_ServiceReadingDetail` incluye `CalculatedAmount` en el INSERT
  (`BDLayout.Add.cs`). No es un stub -- se graba en el momento de guardar la
  lectura, no recién al publicar el presupuesto (más estricto todavía que lo
  que se esperaba).
- Para presupuestos ya **Active/Closed**, `BudgetState.CalculateQuota()` ya
  usa `Installments.Sum(i => i.Amount)` (valores reales de BD) en vez de
  recalcular con `BudgetCalculator` -- eso ya estaba bien.
- **Bug real encontrado:** `BlockWaterReading.razor.CargarPeriodo()`, al
  abrir CUALQUIER período que ya tuviera lecturas guardadas (incluido uno de
  un presupuesto ya publicado/cerrado), llamaba
  `MarcarTodasComoProcesadas() + RecalcularTodas()`. `RecalcularTodas()`
  volvía a correr `ICalculoService.CalcularConsumoAsync(...)` -- con la
  tarifa **vigente hoy** (`/configwater`, guardada en memoria en el
  `CalculoService`, no versionada por periodo) -- sobre cada lectura ya
  cargada desde BD, pisando en memoria el `CalculatedAmount` real. Si el
  usuario después tocaba "Guardar Lecturas" (por ejemplo, para corregir una
  sola unidad), ese monto recalculado con la tarifa de HOY se **volvía a
  guardar en BD**, sobrescribiendo el monto real que se cobró en su momento
  -- para cualquier período, sin importar si el presupuesto ya estaba
  publicado.
- Los dos casos de uso reales que sí necesitan recalcular (`onKeyPress` al
  editar una lectura puntual, `ApplyMinimun`, `CambiarCargoFijo` al cambiar
  el cargo fijo) ya llamaban a `RecalcularLectura` por su cuenta, de forma
  explícita -- el `RecalcularTodas()` automático en `CargarPeriodo()` era
  puramente redundante para el caso de edición y activamente dañino para el
  caso de sólo estar viendo un período viejo.
- Los reportes nuevos (`ReportPages/WaterConsumptionReport.razor`,
  `ResidentPages/MyWaterConsumption.razor`) ya usaban `CalculatedAmount`
  directamente sin recalcular -- no tenían este problema.

**Cambio (versión final):** `CargarPeriodo()` ahora sólo recalcula
automáticamente si el período **todavía está en borrador** -- chequea
`_ServiceReadingState.CurrentReading.Status != 2`. `Status == 2` es el mismo
flag que `IBudgetService.SaveInstallment` le pone al `ServiceReading` al
publicar el presupuesto que lo consume (queda así para siempre, incluso si
ese presupuesto después pasa de Active a Closed -- `ClosePastBudgetsAsync`
sólo toca `BudgetHeader`, no `ServiceReading`).

- Período **ya publicado** (`Status == 2`): nunca se recalcula solo por
  abrir la pantalla -- el monto que se cobró en su momento queda intacto.
  Esto es lo que estaba roto antes (ver más arriba).
- Período **todavía en borrador** (`Status != 2`, sin presupuesto publicado
  que dependa de este monto): sí se recalcula al abrir -- si cambiaron las
  tarifas en `/configwater` desde la última vez que se guardó, lo que se ve
  en pantalla (y lo que se graba si el usuario toca "Guardar Lecturas")
  refleja la tarifa actual. Pedido explícito del usuario: mantener esto para
  no perder la posibilidad de que un cambio de tarifa a mitad de mes se vea
  reflejado antes de publicar.

**Pendiente de verificar con datos reales** (no hay acceso a BD en este
entorno):
- Reabrir un período de un presupuesto YA PUBLICADO en `/waterreadings`
  debe mostrar el mismo monto que quedó guardado la primera vez, incluso
  después de cambiar las tarifas en `/configwater`.
- Reabrir un período TODAVÍA EN BORRADOR debe reflejar la tarifa actual si
  cambió desde la última vez que se guardó.

---

## 2. Modificar/guardar Lecturas de Agua no quedaba en la auditoría

**Estado: resuelto (2026-09-09), branch `claude/lista-pendientes-0gb03a`.**

`GuardarLecturas()` guardaba de verdad en BD (ver punto 1) pero no llamaba a
`IWorkflowAuditService` -- a diferencia de Conciliación, Presupuesto,
Incidentes, etc., no quedaba ningún registro de quién cargó o modificó las
lecturas de un período. Se agregó `WorkflowAction.WaterReadingSaved` y un
`WorkflowAuditService.LogAsync("WaterReading", IdServiceReading, ...)` justo
después del `transaction.CommitAsync()`, con el período y la cantidad de
lecturas guardadas como comentario.

---

## 3. (No verificable en este entorno) Comportamiento del INSERT de `ServiceReadingDetail` al re-guardar un período existente

**Estado: no verificado -- sin acceso a BD, SP no versionado en el repo.**

`INS_ServiceReadingDetail` (llamado por `AddServiceReadingDetailAsync`) no
tiene su definición SQL en `Database/Scripts/` (a diferencia de
`WorkflowAuditLog`, por ejemplo) -- no se pudo confirmar si es un INSERT
puro (fallaría con una PK duplicada al re-guardar un período ya existente,
cuyos `ServiceReadingDetail.IdServiceReadingDetail` se recargan con el mismo
Id) o si internamente hace upsert (`MERGE`/`IF EXISTS UPDATE ELSE INSERT`).
Dado que `GuardarLecturas()` sí permite recargar y volver a guardar un
período existente (ver punto 1) sin que nadie haya reportado un error de PK
duplicada, lo más probable es que el SP ya soporte upsert -- pero no se
pudo confirmar leyendo el código. Si el usuario puede compartir la
definición del SP, se puede verificar/documentar de forma definitiva.
