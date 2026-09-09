# Pendientes de negocio — Reportes financieros

## 1. Reporte "Ingresos y Egresos" (nuevo) + gráfico real en el Dashboard

**Estado: implementado (2026-09-09), branch `claude/lista-pendientes-0gb03a`.**

Pedido del usuario, ya con datos reales cargados (estados de cuenta con
Ingresos y Gastos, más Expenses creados vía Conciliación): "ya deberia
poder generar el reporte de Ingresos y Egresos. Asi como el grafico de
ingresos que esta en el dashboard". Antes de esto:

- El ítem de menú "Ingresos y Egresos" (bajo Finanzas) no tenía ninguna
  página real detrás -- no existía el archivo `.razor` en el proyecto.
- El gráfico "Ingresos 2024" del Dashboard (`Home.razor`) era 100%
  estático: un ícono + el texto "Gráfico de ingresos mensuales", con el
  comentario literal `<!-- Aquí iría tu gráfico (Chart.js, etc.) -->`. No
  llamaba a ningún servicio ni usaba Chart.js (que sí está cargado
  globalmente desde `App.razor`, pero hasta este cambio nunca se había
  usado de verdad en el Dashboard).

**Diseño (retomando la idea que había propuesto el usuario más arriba en
este mismo hilo):** 3 líneas -- Ingresos, Egresos, y una tercera de
"Resultado" que seguía la idea original de "la diferencia que vendría a
ser el AccountBalance". Con una salvedad importante: NO es el saldo real
de una cuenta bancaria (`BankAccount.InitialBalance`/`CurrentBalance`),
porque tanto el reporte como el gráfico del Dashboard pueden sumar
movimientos de VARIAS cuentas bancarias juntas si el usuario no filtra por
una sola -- se la etiquetó como "Resultado acumulado" (suma corrida de
Ingresos - Egresos dentro del rango elegido), no como saldo de cuenta.

**Fuente de datos:** movimientos bancarios reales
(`IBankAccountService.GetStatementDetailsByBuildingAsync`), los mismos que
ya muestra la pestaña "Detalle" de Estados de Cuenta -- NO una
reconstrucción a partir de Cuotas pagadas/Expenses. Ingresos = `Amount >
0`, Egresos = `Amount < 0`, agrupados por mes. Deliberadamente no filtra
por `ReconciliationStatus`: un Ingreso o Gasto todavía sin conciliar sigue
siendo un movimiento real de la cuenta bancaria. Tampoco excluye
transacciones "Ignoradas" (`AccountStatementDetailView` no expone ese
campo hoy -- ver "Pendiente" más abajo).

**Cambios:**
- `Components/Pages/ReportPages/IncomeExpenseReport.razor` (nuevo,
  `/reportes/ingresos-egresos`) -- mismo patrón que
  `CollectionReport.razor`/`DelinquencyReport.razor`: selector de Cuenta
  Bancaria (Todas/una) + rango de fechas, tarjetas de resumen (Total
  Ingresos/Egresos/Resultado Neto), tabla por mes (Ingresos, Egresos,
  Resultado, Acumulado), gráfico de líneas (`spiderHoodReportCharts.lineChart`,
  ya existía en `wwwroot/js/reportCharts.js` -- no hizo falta agregar
  ninguna función nueva) y exportar a Excel (ClosedXML, mismo patrón que
  Recaudación). Permiso nuevo: `view_income_expense_report` (mismo caveat
  que el resto de los reportes -- catálogo de Permission/RolePermissions
  no se puede tocar desde este entorno, asignar desde `/roles`).
- `Home.razor`/`Home.razor.cs`: el placeholder del Dashboard se reemplazó
  por un `<canvas>` real con Ingresos/Egresos mensuales (2 líneas, sin la
  tercera de Resultado acumulado -- el widget del Dashboard es más chico,
  se dejó el detalle completo de 3 líneas para el reporte). El dropdown
  "Este Mes/Últimos 3 Meses/Este Año" (antes decorativo, `href="#"` sin
  ningún handler) ahora sí filtra de verdad -- `CambiarRangoGraficoIngresos`
  recalcula en memoria sobre los movimientos ya traídos en
  `LoadDashboardStatsAsync` (una sola consulta a BD por carga del
  Dashboard, no una por cada click del dropdown). Se agregó un link "Ver
  reporte completo" al lado del dropdown, hacia `/reportes/ingresos-egresos`.

**No se tocó (fuera de alcance, no pedido):**
- La ruta exacta que hoy tiene configurado en BD el ítem de menú
  "Ingresos y Egresos" -- no hay acceso a BD desde este entorno para
  verificarla. La página nueva quedó en `/reportes/ingresos-egresos`
  (mismo patrón `/reportes/<nombre>` que Recaudación/Morosidad/Consumo de
  Agua) -- si el ítem de menú ya apuntaba a otra URL, hay que actualizarlo
  desde Configuración > Items de Menú para que apunte acá.
- Filtrar transacciones "Ignoradas" (Docs/Pendientes-Negocio-Conciliacion.md
  #1) fuera del reporte/gráfico: `AccountStatementDetailView` (el tipo que
  devuelve `GetStatementDetailsByBuildingAsync`) no expone hoy el campo
  `Ignored` -- agregarlo requeriría tocar el SP
  `GET_AccountStatementDetailByHeader`, que no está versionado en el repo
  (mismo cuidado de siempre: no se toca un SP existente sin ver su texto
  real primero). Hoy una transacción ignorada (ej. un error bancario
  revertido) todavía suma en este reporte y en el gráfico del Dashboard --
  probablemente deba excluirse una vez que se pueda confirmar/editar ese SP.
- Un saldo de cuenta bancaria real (partiendo de `InitialBalance`) como
  cuarta serie -- se descartó a propósito por la razón de "Resultado
  acumulado" explicada arriba (no tiene sentido mezclado entre varias
  cuentas, y una sola cuenta ya se puede ver eligiendo el filtro).

**Actualización:** el usuario confirmó con una consulta directa
(`select * from Permissions where PermissionKey = 'view_income_expense_report'`)
que la fila no existe -- como era de esperar, no hay ninguna pantalla en la
app para CREAR un permiso nuevo (`IPermissionAdminService` sólo lee/asigna
permisos YA EXISTENTES a un rol). Se agregó
`Database/Scripts/2026-09-09_76_Seed_ReportPermissions.sql`, que siembra
este permiso y, de paso, los otros tres que tienen el mismo problema
(`view_budget_execution`/`view_delinquency`/`view_consumption_report` --
Recaudación/Morosidad/Consumo de Agua, agregados antes en esta misma
sesión y nunca sembrados tampoco). `IF NOT EXISTS` por cada uno, seguro de
correr aunque alguno ya se haya agregado a mano.

**Pendiente de probar con datos reales** (no hay acceso a BD en este
entorno):
1. Correr `2026-09-09_76_Seed_ReportPermissions.sql` y después asignar cada
   permiso a los roles correspondientes desde `/roles` -- el script sólo
   crea el catálogo, la asignación a un rol sigue siendo manual desde ahí.
2. Confirmar que `/reportes/ingresos-egresos` con datos reales carga la
   tabla, el gráfico y el resumen sin errores, y que el filtro por Cuenta
   Bancaria da los mismos totales que "Resumen de Conciliación" en
   `/ConciliacionGastos`/`/ConciliacionPagos` para la misma cuenta/periodo.
3. Confirmar que el gráfico del Dashboard se dibuja con datos reales y que
   el dropdown "Este Mes/Últimos 3 Meses/Este Año" cambia de verdad la
   serie mostrada.
4. Si el menú "Ingresos y Egresos" ya apuntaba a otra URL, actualizarlo
   para que apunte a `/reportes/ingresos-egresos`.
