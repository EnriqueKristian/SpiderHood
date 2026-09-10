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
   (tabla, matching automático, nada). Se había sacado por eso, pero el
   usuario sí la necesita -- implementada de verdad (ver punto 5).
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

**Encontrado de paso (2026-09-10, sesión de borrado de Gastos), sin corregir
todavía:** el mismo bug de orden (`Show(type)` antes de fijar `Message`) sigue
vivo en el helper COMPARTIDO `ConfirmationUtil.ExecuteWithConfirmation`
(`Classes/Utilities.cs:39-41`) -- acá arriba sólo se había corregido la copia
local de `ReconciliationWorkspace.ConfirmarAsync`, no este helper genérico.
Lo usan al menos `ModalOwnerUnit.razor`, `BudgetGenerator.razor`,
`ServiceReadingModal.razor` y `ManualInstallmentConciliation.razor` -- en
cualquiera de esas pantallas, la primera vez que se dispara una confirmación
puede mostrarse con el mensaje default ("¿Está seguro de realizar esta
acción?") o el de una invocación anterior, en vez del mensaje real, hasta que
un segundo render lo corrija. Fix sería el mismo: invertir el orden (fijar
`Message`/`IsCancelOnly` antes de `Show()`) dentro de `ExecuteWithConfirmation`
-- no se tocó en esta sesión porque no era lo pedido y afecta a 4+ pantallas
a la vez.

---

## 5. "Guardar como plantilla para transacciones similares" -- implementación real

**Estado: implementado (2026-09-09), branch `claude/lista-pendientes-0gb03a`.**

Diseño acordado con el usuario (3 decisiones):

1. **Criterio de match: la descripción del banco EMPIEZA con el mismo
   texto** que se guardó como patrón (case-insensitive) -- no exacto, no
   fuzzy-matching. Si más de una plantilla matchea, gana la de patrón más
   largo (más específica).
2. **Nivel de automatización: sólo pre-llena el formulario, el usuario
   confirma.** Nunca crea ni concilia nada por su cuenta -- al abrir
   "Crear Gasto desde Transacción" para una transacción que matchea, el
   formulario ya viene con Categoría/Distribución/Proveedor completos (con
   un aviso visible de qué plantilla se aplicó), pero sigue siendo el
   usuario quien revisa y toca "Crear Gasto".
3. **v1 sin pantalla de gestión.** Guardar el checkbox hace upsert por
   (edificio, texto exacto de la descripción) -- volver a guardar la misma
   descripción con otra categoría/distribución actualiza la plantilla
   existente en vez de duplicarla. Ver/editar/borrar plantillas desde una
   pantalla dedicada queda para más adelante si hace falta.

**Cambios:**
- Tabla nueva `dbo.ExpenseTemplate` + `INS_ExpenseTemplate`/
  `UPD_ExpenseTemplate`/`GET_ExpenseTemplatesByBuilding`
  (`Database/Scripts/2026-09-09_75_ExpenseTemplate.sql`) -- mismo patrón
  que `ReconciliationSession`/`WorkflowAuditLog`, tabla 100% nueva.
- `IExpenseTemplateService` nuevo: `BuscarPlantillaAsync` (prefijo más
  largo que matchee) y `GuardarPlantillaAsync` (upsert por descripción
  exacta).
- `CreateExpenseFromTransactionModal.razor`: al abrir para una transacción,
  busca una plantilla que aplique y pre-llena Categoría/Distribución/
  Proveedor (con aviso visible, no silencioso) -- el checkbox "Guardar como
  plantilla" vuelve a existir, ahora con función real detrás. El patrón se
  guarda contra el texto ORIGINAL del banco (`Transaccion.Description`), no
  contra una descripción que el usuario haya editado a mano, porque las
  transacciones futuras a reconocer también van a traer el texto crudo del
  banco.
- De paso: `OnParametersSet` (síncrono) pasó a `OnParametersSetAsync`
  (necesario para poder buscar la plantilla), y ahora sólo reinicializa el
  formulario cuando la transacción realmente CAMBIÓ (antes lo hacía en
  cada ciclo de parámetros, así que un re-render del padre mientras el
  modal seguía abierto podía borrar lo que el usuario ya había tipeado).

**Pendiente de probar con datos reales** (no hay acceso a BD en este
entorno): guardar una plantilla desde un gasto real, y confirmar que una
transacción posterior con descripción parecida la aplica sola al abrir el
modal.

---

## 6. "Crear Gasto desde Transacción" grababa en BD de una, sin poder deshacer con "Cancelar"

**Estado: resuelto (2026-09-09), branch `claude/lista-pendientes-0gb03a`.**

Reportado por el usuario probando con datos reales (TUPAY-TUPAY / Portero
Seguro): "el botón guardar, guarda directamente en BD lo que se ha creado,
si le doy en 'Finalizar Conciliación' vuelve a guardar, y si le doy
cancelar, no hay forma de deshacer lo que acabamos de hacer".

Confirmado: violaba el contrato de Fase B (propuesta en memoria → recién se
graba de verdad en `EnviarAConciliar`/"Finalizar Conciliación"), que sí se
respeta para Ingresos/Cuotas (`InstallmentService.AplicarPagoAsync` sólo se
llama ahí adentro). Para Gastos NUEVOS creados desde el modal, en cambio:

- `GastoCreadoExitosamente` llamaba `ExpenseService.AddExpenseAsync(nuevoGasto)`
  -- un INSERT real -- apenas se tocaba "Crear Gasto" en el modal, antes de
  cualquier confirmación.
- `GenerarGastosSeleccionados` (generación en lote) tenía el mismo patrón
  con `ExpenseService.CrearGastoAsync(nuevoGasto)`.
- Como ambos ya dejaban el gasto guardado, "Finalizar Conciliación" hacía
  una segunda escritura real (`BankService.ConciliarTransaccionAsync`,
  sobre la transacción bancaria) -- de ahí el "vuelve a guardar". Y como
  "Cancelar" en el modal nunca llamaba a ningún método de borrado, el gasto
  ya insertado quedaba huérfano en BD para siempre si el usuario abandonaba
  el flujo antes de confirmar.

**Cambio:** el INSERT real de un gasto NUEVO se movió a
`EnviarAConciliar()` (rama "Gasto"), igual que ya pasa con
`AplicarPagoAsync` para Ingresos. Para lograrlo sin romper los otros 5
lugares que llaman a `ConciliarConGasto` con un gasto YA EXISTENTE en BD
(dropdown de posibles matches, match automático por monto exacto,
"Conciliar seleccionadas"), se agregó un flag nuevo,
`TransactionBankDetail.PropuestaGastoNuevo` (`[NotMapped]`, en memoria):

- `ConciliarConGasto(transaccion, gasto, automatico, esGastoNuevo)` -- nuevo
  parámetro opcional, default `false`. Sólo `GastoCreadoExitosamente` y
  `GenerarGastosSeleccionados` lo pasan en `true`.
- `EnviarAConciliar()`: en la rama "Gasto", si `PropuestaGastoNuevo` es
  `true` llama a `ExpenseService.AddExpenseAsync(transaccion.GastoConciliado)`
  ANTES de `ConciliarTransaccionAsync` -- recién ahí existe en BD. Si es
  `false` (match con algo ya existente), no se toca -- se sigue comportando
  como antes.
- `QuitarPropuesta` (sacar una propuesta antes de confirmar): si el gasto
  era nuevo, ya no se lo vuelve a ofrecer como "posible match" a otra
  transacción vía `gastosPendientes` (ese objeto nunca se guardó en BD) --
  se descarta. Si era uno existente, se comporta igual que antes.
- Resultado: "Cancelar" en el modal, o cerrar la página sin llegar a
  "Enviar a Conciliar"/"Finalizar Conciliación", ya no deja ningún gasto
  huérfano en BD -- nada se graba hasta la confirmación real.

**Pendiente de probar con datos reales** (no hay acceso a BD en este
entorno): repetir el caso reportado (crear gasto desde una transacción,
tocar "Cancelar" en vez de confirmar) y verificar que no aparece ningún
gasto nuevo en BD; y que "Crear Gasto" → "Finalizar Conciliación" sigue
dejando todo bien (un solo gasto, transacción conciliada) como antes.

---

## 7. "Finalizar Conciliación" (Gasto) grababa, pero la transacción volvía a "No Conciliadas" al recargar

**Estado: resuelto (2026-09-09), branch `claude/lista-pendientes-0gb03a`.**

Reportado por el usuario probando con datos reales: tras "Finalizar
Conciliación" (rama Gasto), la pantalla decía que se había grabado, pero al
recargar la transacción volvía a aparecer en "No Conciliadas" como si nada
hubiera pasado.

**Causa:** en `EnviarAConciliar()`, el orden de las líneas era:
```
await BankService.ConciliarTransaccionAsync(transaccion, transaccion.GastoConciliado);
transaccion.ReconciliationStatus = ConcilationType.Conciliada;
```
`ConciliarTransaccionAsync` (`IBankAccountService.cs`) no recibe el nuevo
estado como parámetro -- simplemente hace `ec.UpdateRecordAsync(transaccion)`,
un UPDATE que graba el ESTADO ACTUAL del objeto `transaccion` tal cual está
en memoria en ese momento. Como el `UPDATE` se disparaba ANTES de la línea
que cambia `ReconciliationStatus` a `Conciliada`, lo que quedaba grabado en
BD era el `NoConciliada` viejo -- la UI en memoria sí mostraba "Conciliada"
hasta que algo recargaba desde BD (F5, cambiar de pestaña/período), momento
en el que volvía a "No Conciliadas" porque nunca se había grabado de verdad.

**Cambio:** se invirtió el orden -- `transaccion.ReconciliationStatus`/
`ReconciliationDate` se fijan ANTES de llamar a `ConciliarTransaccionAsync`,
así el UPDATE graba el estado correcto.

**Pendiente de probar con datos reales** (no hay acceso a BD en este
entorno): "Crear Gasto" (o matchear uno existente) → "Finalizar
Conciliación" → F5 (o cambiar de período y volver) → confirmar que la
transacción queda en "Conciliados"/"Auto"/"Manual" según corresponda, no en
"No Conciliadas".

---

## 8. "Finalizar Conciliación" en /ConciliacionGastos también conciliaba Pagos (y viceversa)

**Estado: resuelto (2026-09-09), branch `claude/lista-pendientes-0gb03a`.**

Reportado por el usuario: trabajó y confirmó Gastos desde /ConciliacionGastos
("Conciliación de Gastos"), y al entrar después a /ConciliacionPagos
("Conciliación de Pagos") se encontró con que los pagos con match exacto
1:1 YA estaban conciliados, sin haber tocado nada en esa pantalla.
"Deberían ser distinto si se hacen de pantallas distintas, Gastos concilia
Gastos, Pagos concilia Pagos".

**Causa:** `ReconciliationWorkspace.razor` es un único componente
compartido por `/ConciliacionGastos` (`ModoFijo="Gasto"`),
`/ConciliacionPagos` (`ModoFijo="Ingreso"`) y la vista combinada vieja
(`ModoFijo=null`) -- mismos datos, mismo motor. Al cargar transacciones se
auto-proponen matches exactos (1:1) tanto de Gasto como de Ingreso, sin
mirar `ModoFijo` (es intencional: son coincidencias reales del período,
independientemente de qué pantalla las mostró primero). El problema estaba
en la confirmación: `EnviarAConciliar()` tomaba `transacciones.Where(t =>
t.PropuestaPendiente)` **sin filtrar por `Tipo`/`ModoFijo`** -- así que
tocar "Finalizar Conciliación" (o el botón "Enviar a Conciliar (N)") desde
/ConciliacionGastos confirmaba de una TODAS las propuestas pendientes,
incluidas las de Ingreso que el usuario nunca llegó a revisar en esa
pantalla (y que, para colmo, contaban en el número mostrado en el botón).
`FinalizarConciliacion()` tenía el mismo problema en dos chequeos
adicionales (`transacciones.Any(t => t.PropuestaPendiente)`).

**Cambio:** las tres lecturas pasaron a usar `TransaccionesDelModo` (la
misma propiedad que ya acotaba `transaccionesConciliadas`/
`transaccionesNoConciliadas` por `ModoFijo`, pero que `EnviarAConciliar`/
`FinalizarConciliacion` no estaban usando):
- El contador del botón "Enviar a Conciliar (N)".
- `EnviarAConciliar()`: sólo confirma propuestas del tipo de la pantalla
  actual.
- `FinalizarConciliacion()`: sólo mira si hay propuestas pendientes -- antes
  y después de confirmar -- del tipo de la pantalla actual, para no cortar
  ni registrar la sesión en base a propuestas de la otra pantalla.
En la vista combinada (`ModoFijo=null`, `/ReconcileExpenses`) el
comportamiento no cambia -- ahí sigue confirmando todo junto, como siempre.

**Pendiente de probar con datos reales** (no hay acceso a BD en este
entorno): desde /ConciliacionGastos, con un pago 1:1 pendiente de propuesta
automática de fondo, confirmar Gastos y verificar que el pago sigue
apareciendo como propuesta pendiente (no conciliado) al entrar después a
/ConciliacionPagos.

---

## 9. "Cargar Estado de Cuenta": error genérico sin decir cuál fila ni cuál dato falló

**Estado: resuelto (2026-09-09), branch `claude/lista-pendientes-0gb03a`.**

Reportado por el usuario: al cargar un Excel de estado de cuenta,
`/movement/cargar` mostraba "No se guardaron datos debido a errores." sin
indicar dónde ni cuál era el error -- y encima la "Vista previa de datos"
quedaba vacía (0 de 0 registros), así que no había ninguna forma de ubicar
la fila problemática en el Excel.

**Causa:** `LeerExcel()` (`CargarEstadoCuentaConciliacion.razor`) ya arma
una lista `errores` con el detalle exacto fila por fila (ej. "Fila 5: Fecha
inválida", "Fila 12: Moneda inválida (solo PEN o USD)") mientras valida
cada celda -- pero `HandleFileSelected`, apenas detectaba que esa lista no
estaba vacía, pisaba todo con el mensaje genérico y hacía `return`
INMEDIATAMENTE, antes de llegar a `pagination.Initialize(datos.ToList())`
-- por eso la tabla de vista previa (que sí resalta en rojo cada fila con
`Validation != "Ok"`) nunca llegaba a mostrarse.

**Cambio:** se sacó el `return` -- ahora, si hay errores, el mensaje lista
hasta 5 de ellos con el detalle real ("Fila N: ...") y avisa que el resto
se puede ver marcado en rojo en la tabla, pero el flujo sigue de largo:
la vista previa se llena igual con TODAS las filas (válidas e inválidas),
así el usuario puede ubicar exactamente cuáles corregir en su Excel. Esto
no cambia qué se guarda: `SaveData`/`SaveItemAsync` ya sólo insertaban las
filas con `Validation == "Ok"` -- las inválidas (incluida cualquier fila con
`StatementDate` sin parsear) seguían sin llegar nunca a la BD, antes y
después de este cambio.

**Pendiente de probar con datos reales** (no hay acceso a BD en este
entorno): cargar un Excel con alguna fila con dato inválido (fecha futura,
moneda que no sea PEN/USD, etc.) y confirmar que el mensaje ahora dice
"Fila N: ..." y que la vista previa muestra esa fila marcada en rojo junto
con el resto de filas válidas.
