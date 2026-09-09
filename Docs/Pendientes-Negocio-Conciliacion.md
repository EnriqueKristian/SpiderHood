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

**Estado: implementado (2026-09-09), branch `claude/lista-pendientes-0gb03a`.**

Al implementar Fase B se había encontrado que `BankAccountService.GuardarConciliacionAsync`
y `ObtenerUltimaConciliacionAsync` (usados por "Finalizar Conciliación" y la
tarjeta "Última Conciliación") eran stubs -- pero resultó peor que "no
implementado": `ObtenerUltimaConciliacionAsync` devolvía **siempre el mismo
registro inventado** (`Id=1`, fecha fija "hace 3 días", `Usuario="Admin
Principal"`, 42/45 transacciones, `Diferencia S/125.50`), sin tocar la BD.
La tarjeta "Última Conciliación" mostraba ese dato falso sin importar lo que
hubiera pasado realmente -- confirmado con el usuario, se decidió
implementarlo de verdad en vez de sólo apagar el dato falso.

**Cambios:**
- Tabla nueva `dbo.ReconciliationSession` + SPs `INS_ReconciliationSession`/
  `GET_LastReconciliationSession` (`Database/Scripts/2026-09-09_71_ReconciliationSession.sql`)
  -- mismo patrón que `WorkflowAuditLog` (tabla 100% nueva, no toca nada
  existente). No se reutilizó `WorkflowAuditLog` (que sí es real, por
  transacción) porque no tiene forma de agrupar qué transacciones se
  procesaron juntas en un mismo "Finalizar Conciliación" -- sólo tiene
  `EntityId` (una transacción) y `PerformedOn`, sin un identificador de sesión.
- `Classes/Budget/Conciliacion.cs`: `Id` pasó de `int` a `Guid` (se generaba
  pero nunca se guardaba antes, ahora sí importa), y se agregó `IdBuilding`
  (no existía).
- `BankAccountService.ObtenerUltimaConciliacionAsync` ahora recibe
  `idBankAccount` y trae la sesión real más reciente de esa cuenta
  (`GET_LastReconciliationSession`, `TOP 1 ORDER BY Fecha DESC`) --
  `GuardarConciliacionAsync` ahora inserta de verdad.
- `ReconciliationWorkspace.razor`: la carga de "última conciliación" se
  movió de `CargarDatosIniciales()` (donde `cuentaSeleccionadaId` todavía era
  `Guid.Empty`) a `CargarTransacciones()`, así se refresca también al cambiar
  de cuenta bancaria, no sólo en la carga inicial.

**Pendiente de verificar con datos reales** (no hay acceso a BD en este
entorno): confirmar que la tarjeta "Última Conciliación" queda vacía/sin
mostrar nada la primera vez que se usa una cuenta bancaria nueva (antes de
que exista ninguna sesión guardada), y que después de "Finalizar
Conciliación" el próximo `ObtenerUltimaConciliacionAsync` trae esa misma
sesión recién guardada.

---

## 4. "Crear Gasto desde Transacción": Distribución no seguía a la Categoría, plantilla sin funcionalidad, y "Finalizar Conciliación" con feedback engañoso

**Estado: resuelto (2026-09-09), branch `claude/lista-pendientes-0gb03a`.**

El usuario probó el flujo real (cargar estado de cuenta → crear gasto desde
una transacción de egreso → Finalizar Conciliación) y reportó tres cosas.
Las tres eran reales:

1. **"Tipo Distribución" no cambiaba al elegir Categoría.** `CreateExpenseFromTransactionModal.razor`
   dejaba `Distribution = TypeDistribution.Fija` fijo desde la
   inicialización del formulario, sin importar la categoría elegida --
   pero cada `Category` ya tiene su propio `Distribution` configurado
   (`Classes/Category.cs`), que es lo que debería definir el reparto real.
   Mismo bug, copiado tal cual, en `ExpensePages/ModalExpense.razor` y
   `MovementPages/ModalMovement.razor` (los otros dos modales de
   crear/editar Gasto). Se agregó `@bind:after` al `<select>` de Categoría
   en los tres para tomar el `Distribution` de la categoría elegida --
   el usuario todavía puede cambiarlo a mano después, el select sigue
   editable.
2. **"Guardar como plantilla para transacciones similares" no tenía
   ninguna funcionalidad.** Confirmado: el checkbox sólo escribía una
   variable local (`guardarComoPlantilla`) que ningún otro código leía --
   no existe en el proyecto ningún concepto de "plantilla de transacción"
   (tabla, matching automático, nada). En vez de dejar un control que
   miente sobre lo que hace, se sacó. Implementar la función real (guardar
   un patrón descripción→categoría/distribución y usarlo para sugerir
   automáticamente en transacciones futuras similares) sería una feature
   nueva de verdad, no pedida todavía -- si se quiere, es un tema aparte.
3. **"Finalizar Conciliación" parecía "no hacer nada".** Tres causas
   reales encontradas, ninguna del botón en sí:
   - `ReconciliationWorkspace.GastoCreadoExitosamente` pisaba el mensaje
     correcto que ya dejaba `ConciliarConGasto` ("Propuesta: ... Usa
     'Enviar a Conciliar' para confirmar.") con uno genérico y **falso**:
     "Gasto creado y conciliado exitosamente" -- el gasto en ese punto
     todavía es sólo una PROPUESTA (Fase B), no está conciliado. Eso hacía
     creer al usuario que la conciliación ya había terminado en ese paso,
     así que al tocar después "Finalizar Conciliación" no esperaba que
     hiciera falta nada más.
   - `ConfirmarAsync` (el modal de confirmación reutilizable) llamaba a
     `_confirmationModal.Show(type)` **antes** de fijar
     `_confirmationModal.Message = mensaje` -- `Show()` ya dispara su
     propio render adentro del componente, así que ese primer render podía
     salir con el mensaje de la invocación anterior (o el default "¿Está
     seguro de realizar esta acción?"), leyéndose como un modal "vacío" o
     con el texto pegado de otra acción. Se invirtió el orden.
   - `FinalizarConciliacion()` cortaba en silencio si al usuario le
     faltaba el permiso (`_canReconcileExpenses`/`_canReconcileInstallments`)
     -- un click sin ningún efecto visible. Ahora deja `mensajeError`.

**Pendiente de probar con datos reales** (no hay acceso a BD en este
entorno): confirmar que, tras crear un gasto desde una transacción, el
mensaje ahora sí dice "Propuesta... Usa 'Enviar a Conciliar'" (no
"conciliado exitosamente"), y que "Finalizar Conciliación" muestra el modal
de confirmación con el texto correcto desde el primer render.
