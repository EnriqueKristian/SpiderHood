# Pendientes de negocio — Conciliación Bancaria

Backlog de temas de **negocio** (no bugs) que salieron durante el trabajo de
la Fase B de conciliación (`Components/Pages/ReconciliationPages/ReconciliationWorkspace.razor`)
pero que el usuario pidió dejar anotados para evaluar más adelante, en vez de
implementarlos a ciegas ahora.

---

## 1. "Ignorar transacción" necesita motivo + tipo, no solo un flag

**Estado: pendiente, sin empezar.**

Hoy "Ignorar transacciones" (`IgnorarSeleccionadas`/`MarcarTransaccionComoIgnoradaAsync`)
solo pone `Ignored = true` en la transacción, sin ningún motivo ni
clasificación de por qué se ignora.

Casos reales que el usuario mencionó y que hoy no se distinguen entre sí:
- Un depósito por error a la cuenta del edificio, que luego se debita/revierte
  -- el movimiento (o el par depósito+reverso) debería quedar marcado como tal,
  con el motivo, no solo "ignorado" a secas.
- Otros casos todavía por identificar ("a evaluar casos").

**Lo que falta diseñar:**
- Un campo de motivo (texto libre u obligatorio) al ignorar.
- Un "tipo" de ignorado (catálogo: error bancario/depósito revertido/otro) en
  vez de un simple booleano -- para poder reportar cuántas transacciones se
  ignoraron y por qué a lo largo del tiempo.
- Si esto también debería dejar registro en el log de auditoría
  (`IWorkflowAuditService`, mismo mecanismo ya usado para Conciliar/Corregir).

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
