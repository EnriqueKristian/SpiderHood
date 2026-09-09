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

## 3. `GET_ServiceReadingDetailList` devuelve cada fila duplicada, y sin filtrar por edificio

**Estado: resuelto y verificado (2026-09-09), branch `claude/lista-pendientes-0gb03a`
-- el usuario corrió los dos scripts y confirmó en "Mi Consumo de Agua" que
ya no hay periodos duplicados.**

**Actualización:** el usuario compartió la definición real del SP. La causa
del punto 2 (filas duplicadas) es exactamente la hipótesis planteada más
abajo: `JOIN GroupUnit g ON wr.IdGroupUnit = g.IdGroupUnit` -- `GroupUnit`
tiene una fila por cada unidad física que compone un Grupo de Unidades (mismo
concepto de "Grupo de Unidades con más de una unidad física" ya documentado
al corregir el % de Morosidad del dashboard), así que un grupo con más de una
unidad física (depto + cochera, por ejemplo) multiplica (fan-out) cada fila
de `VW_ServiceReadingDetail` por esa cantidad. Corregido de raíz en
`Database/Scripts/2026-09-09_73_Fix_GET_ServiceReadingDetailList_Duplicates.sql`
-- se une contra una subconsulta que colapsa `GroupUnit` a una fila por
`IdGroupUnit` (`MIN(GroupNumber)`) en vez de contra la tabla directa. Misma
firma, mismas columnas, mismo orden -- no rompe ningún caller existente.
La mitigación del lado del cliente (`BDLayout.GetServiceReadingDetailbyPeriodAsync`,
dedup + preferir el `PreviousReading` más alto) se deja como red de
seguridad por ahora -- no hace nada si el SP ya no duplica, así que no hay
apuro en sacarla hasta confirmar que el script corrió bien en producción.

**Actualización 2 (mismo día):** el punto 1 (no filtraba por `IdBuilding`)
también se corrigió de raíz. `GET_ServiceReadingDetailList` ahora toma
`@IdBuilding` además de `@Period`
(`Database/Scripts/2026-09-09_74_GET_ServiceReadingDetailList_FiltraPorEdificio.sql`,
join a `dbo.ServiceReading` por `IdServiceReading`, tabla ya confirmada con
`IdBuilding` real). Cambia la firma del SP, así que se actualizaron a la vez
todos los callers de `GetServiceReadingDetailbyPeriodAsync`/
`ObtenerLecturasPorPeriodoAsync` para pasar `idBuilding`:
`BlockWaterReading.razor` (las dos llamadas, período actual y anterior),
`MyReceipts.razor`, `MyPayments.razor`, `InstallmentList.razor`,
`BudgetGenerator.razor` y `GetServiceReadingDetailsByBuildingAsync` (que de
paso se simplificó -- ya no necesita el filtro post-consulta por
`IdGroupUnit`, ni la consulta extra a `GetOwnersByBuildingAsync` que sólo
existía para eso).

Con esto, los dos problemas de `GET_ServiceReadingDetailList` quedan
corregidos de raíz en la BD (scripts `_73` y `_74`, hay que correr los
dos). El dedup del lado del cliente
(`BDLayout.GetServiceReadingDetailbyPeriodAsync`) se mantiene como red de
seguridad.

**Diagnóstico original (para referencia):**

Reportado por el usuario: "Mi Consumo de Agua" mostraba dos filas para el
mismo periodo (ej. dos "Abril 2026") con `Lec. Ant.` distinto para la misma
unidad. Se investigó con consultas directas a BD (el usuario) + revisión de
código (acá), y se encontraron DOS bugs reales, ambos en
`GET_ServiceReadingDetailList` (usado por `GetServiceReadingDetailbyPeriodAsync`,
que a su vez usan `BlockWaterReading.razor`, `MyPayments.razor`,
`MyReceipts.razor`, `InstallmentDetailModal.razor`, `BudgetGenerator.razor` y
los reportes de Consumo de Agua):

1. **No filtra por `IdBuilding`** (sólo toma `@Period`) -- confirmado con una
   consulta real: para el mismo `Period`, devolvía filas de un
   `IdServiceReading` que NO estaba en la lista de cabeceras del edificio en
   cuestión (`GET_ServiceReadingList @IdBuilding=...`), con el mismo
   `GroupNumber` ("101") pero un `IdGroupUnit` distinto -- es decir, la
   unidad "101" de OTRO edificio, coincidencia de número de puerta, no de
   `IdGroupUnit`. Mitigado en `ICalculoService.GetServiceReadingDetailsByBuildingAsync`
   (ver punto siguiente), que ahora filtra por los `IdGroupUnit` reales del
   edificio antes de devolver nada -- pero sólo protege a los dos reportes
   nuevos, no a `BlockWaterReading.razor` ni al resto de las pantallas que
   llaman a `GetServiceReadingDetailbyPeriodAsync` directamente (ver Pendiente
   más abajo).
2. **Devuelve cada fila duplicada.** Confirmado con la misma consulta: CADA
   `IdServiceReadingDetail` (la PK) aparecía exactamente DOS veces, con
   `CurrentReading`, `Consumption`, `CalculatedAmount` e `IdServiceReading`
   IDÉNTICOS entre ambas copias -- sólo `PreviousReading` cambiaba (una copia
   traía el valor real, la otra 0). Patrón 100% consistente en decenas de
   filas de dos periodos distintos. Todo indica un JOIN/subquery del lado del
   SP para resolver "el `CurrentReading` del periodo anterior" que hace
   fan-out (dos filas candidatas) en vez de matchear una sola -- la fila
   "perdedora" queda con `PreviousReading = 0` (`ISNULL(...)` o similar). No
   se pudo confirmar la causa exacta ni corregir el SP: no está versionado en
   `Database/Scripts/`, sin acceso a BD desde este entorno.

**Mitigación aplicada (`BDLayout.GetServiceReadingDetailbyPeriodAsync`):**
deduplica por `IdServiceReadingDetail` y, dentro de cada duplicado, prefiere
el `PreviousReading` más alto -- nunca inventa un valor nuevo, sólo elige
entre los que el propio SP ya devolvió (el más alto fue el correcto en los
dos periodos verificados). Al vivir en `BDLayout.Get.cs`, corrige la
duplicación para TODA la app de una sola vez (no sólo los reportes nuevos).

**Verificado:** el usuario corrió `2026-09-09_73_...sql` y `2026-09-09_74_...sql`
en su BD y confirmó en "Mi Consumo de Agua" que cada periodo (marzo-setiembre
2026) aparece una sola vez, sin duplicados.

---

## 4. (No verificable en este entorno) Comportamiento del INSERT de `ServiceReadingDetail` al re-guardar un período existente

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

---

## 5. La migración de histórico de agua no traía el monto real ya calculado (`CalculatedAmount` quedaba en 0)

**Estado: resuelto (2026-09-09), branch `claude/lista-pendientes-0gb03a`.**

Seguimiento del punto 3: ahí se explicó que `ImportarLecturasAguaAsync`
guardaba `CalculatedAmount = 0` a propósito porque no tiene forma de saber
qué tarifa aplicaba en el momento histórico. El usuario planteó la duda de
fondo: ese monto real **sí existe** -- lo tenía calculado el sistema
anterior (Excel, en su caso: columna `Monto` + columna `MontoAgua` sumadas
dan la cuota del mes) -- y si la migración no lo captura, esa información
se pierde para siempre, aunque después SpiderHood recalcule con sus propias
tarifas (que van a dar un monto distinto al que realmente se cobró en su
momento).

Aclaración importante del usuario: **no** hay que replicar la fórmula del
Excel de Nova Alzamora en el código -- cada edificio que se migre puede
traer esa plantilla armada distinto. La idea es que **la plantilla de
SpiderHood** tenga un lugar fijo para ese dato, y que cada migración lo
llene desde su propio Excel de origen (como ya pasa con el resto de la
plantilla de Lecturas de Agua Históricas).

**Cambio:** se agregó una columna opcional "Monto de Agua" a la plantilla
"Lecturas de Agua Históricas" (`IMigrationTemplateService.GenerarPlantillaLecturasAguaAsync`)
-- se completa por fila igual que 'Lectura'/'Lectura Inicial', junto a la
lectura del medidor de ese periodo, en vez de vivir en otra plantilla o
mezclarse con una cuota Extraordinaria aparte. `ImportarLecturasAguaAsync`
ahora la lee y, si viene completa, graba ese valor **tal cual** en
`ServiceReadingDetail.CalculatedAmount` -- nunca lo recalcula. Si se deja
vacía, el comportamiento es el mismo de antes (`CalculatedAmount = 0`,
mostrado como "No calculado" en los reportes de consumo) y ahora además
genera una advertencia en el resultado del import ("no trae 'Monto de
Agua'... aunque tiene X m³ de consumo") para que no pase desapercibido.

**Pendiente de probar con datos reales** (no hay acceso a BD en este
entorno): cargar un archivo con la columna nueva completa y confirmar que
"Mi Consumo de Agua"/el reporte de Consumo de Agua muestran el monto real
en vez de "No calculado".
