# Pendientes de negocio — Conciliación Bancaria

Backlog de temas de **negocio** (no bugs) que salieron durante el trabajo de
la Fase B de conciliación (`Components/Pages/ReconciliationPages/ReconciliationWorkspace.razor`)
pero que el usuario pidió dejar anotados para evaluar más adelante, en vez de
implementarlos a ciegas ahora.

---

## 1. "Ignorar transacción" necesita motivo + tipo, no solo un flag

**Estado: implementado (2026-09-09), branch `claude/lista-pendientes-0gb03a`.**

**Hallazgo al retomar esto: "Ignorar" no hacía nada de verdad.** No era sólo
que faltara motivo/tipo -- ni siquiera el flag booleano se guardaba:
- `TransactionBankDetail.Ignored` era `[NotMapped]` (no existía la columna en
  `dbo.AccountStatementDetail`).
- `GET_BankTransactionsNoConcilied` devolvía `Ignored` como el literal
  `@FALSE`, no leído de ninguna columna real.
- `IBankAccountService.MarcarTransaccionComoIgnoradaAsync` era un stub
  (`Task.Delay(200)` + `Console.WriteLine`, sin tocar la BD) -- mismo patrón
  que `GuardarConciliacionAsync`/`ObtenerUltimaConciliacionAsync` (ver punto 3
  de este mismo documento).
- Ninguna pantalla leía `transaccion.Ignored` para filtrar ni mostrar nada.

En los hechos: click en "Ignorar", nada visible pasaba, y recargar la página
hacía que la transacción "ignorada" volviera a aparecer como pendiente, sin
excepción, para cualquier edificio. Antes de poder agregar motivo/tipo hizo
falta implementar la persistencia real que nunca existió.

**Qué se implementó:**
- Columnas reales `Ignored`/`IgnoredReason`/`IgnoredType` en
  `dbo.AccountStatementDetail` (`Database/Scripts/2026-09-09_70_AccountStatementDetail_Ignored.sql`),
  que también extiende `GET_BankTransactionsNoConcilied` para leerlas (mismo
  cuidado que en el punto 7 de `Pendientes-Negocio-Migracion.md`: se partió
  del texto conocido más reciente de ese SP, no de una suposición) y agrega
  el nuevo SP `UPD_AccountStatementDetail_Ignored`.
- `IgnoredReasonType` (`Classes/Movement.cs`): enum C# con los 3 valores que
  proponía este mismo punto -- `ErrorBancario`/`DepositoRevertido`/`Otro` --
  en vez de un grupo de `Parameter`: es un catálogo chico y cerrado, igual
  para todos los edificios, y `Parameter` en este código es por-edificio (o
  requiere el mecanismo Sistema/Mixto) -- de más para 3 valores fijos que el
  propio punto dejaba "a evaluar casos". Si más adelante cada edificio
  necesita agregar los suyos, ahí sí conviene migrar a `Parameter`.
- `IgnorarSeleccionadas` ahora pide **tipo (select) + motivo (texto libre,
  obligatorio)** en un modal antes de ignorar el lote seleccionado (mismo
  look & feel que el modal de motivo de "Corregir"), y llama a
  `MarcarTransaccionComoIgnoradaAsync(transaccion, motivo, tipo)` -- ya no al
  stub.
- Auditoría: cada transacción ignorada queda en `WorkflowAuditEntry` (nueva
  acción `Ignored`), mismo mecanismo que Conciliar/Corregir.
- Como "Ignorar" ahora persiste de verdad, se le dio el efecto que se
  esperaba de él pero nunca tuvo: las transacciones ignoradas **salen** de la
  cola de "No Conciliados" (`transaccionesNoConciliadas` y
  `GetTransaccionesFiltradas` las excluyen) y aparecen en una pestaña nueva
  **"Ignorados"**, con su motivo (tooltip) y tipo visibles -- ahí es donde
  vive el "poder reportar cuántas transacciones se ignoraron y por qué" que
  pedía este punto. Los botones de conciliar/crear gasto se ocultan para una
  transacción ignorada (ya no tiene sentido conciliar algo que se decidió
  ignorar).

**No se implementó (fuera del alcance de este punto, no pedido explícitamente):**
- Deshacer un "Ignorado" (no hay botón para volver una transacción ignorada a
  pendiente) -- si hace falta, es un agregado chico sobre lo mismo (misma idea
  que "Corregir": otro `UPD_AccountStatementDetail_Ignored` con `@Ignored = 0`).
- El caso específico "depósito por error + su reverso deberían quedar
  vinculados entre sí como un par" sigue "a evaluar" -- lo que se implementó
  cubre clasificar y explicar CADA transacción ignorada por separado, no
  vincular dos transacciones entre sí como un mismo evento.

**Sin verificar en un browser real** (sin acceso a BD en este entorno) --
antes de darlo por cerrado, confirmar que el modal pide motivo/tipo, que la
transacción sale de "No Conciliados", que aparece en "Ignorados" con el dato
correcto, y que sobrevive a un F5.

## 2. Garantía de reserva de área común (devolución total o parcial)

**Estado: pendiente, sin empezar -- ni siquiera hay una decisión de diseño
todavía, solo la mención del caso.**

Cuando un residente/arrendatario reserva un área común, paga una garantía que
después se le devuelve -- total o parcialmente, según una decisión (ej. si
hubo daños se descuenta algo). Ese flujo de ida (cobro) y vuelta (devolución
parcial/total) no tiene hoy un lugar claro dentro de la conciliación bancaria:
no es un Ingreso normal (cuota) ni un Gasto normal, y el reverso no es lo
mismo que "Corregir" una conciliación mal hecha -- es una devolución real de
dinero que sí debería moverse en el banco.

**Lo que falta:** todo el diseño -- cómo se registra el cobro de la garantía,
cómo se registra la devolución (¿como Gasto? ¿como un tipo de movimiento
aparte?), y cómo/si eso pasa por esta misma pantalla de conciliación.

## 3. (Encontrado en el camino) El resumen de "sesión de conciliación" no se guarda de verdad

**Estado: identificado, no arreglado -- reportado al usuario, no se decidió
si vale la pena.**

Al implementar Fase B se encontró que `BankAccountService.GuardarConciliacionAsync`
y `ObtenerUltimaConciliacionAsync` (usados por "Finalizar Conciliación" y la
tarjeta "Última Conciliación") son stubs (`Task.Delay(...)` + `Console.WriteLine`,
sin tocar la BD) -- el registro de "sesión de conciliación completa" (fecha,
cuántas transacciones, diferencia) nunca se guardó realmente, a diferencia del
registro POR TRANSACCIÓN que sí es real (`IWorkflowAuditService`, tabla
`WorkflowAuditEntry`, ya usado por Conciliar/Corregir desde la Fase B).

Como el requisito de "que quede registrado quién concilió" ya se cumple a
nivel de cada transacción individual, este resumen de sesión quedó
como algo "bonito de tener" (dashboard/reporte de sesiones de conciliación),
no bloqueante -- se documenta acá en vez de implementarlo sin que el usuario
lo haya pedido.
