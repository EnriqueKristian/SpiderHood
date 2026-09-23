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

---

## 2. "Recaudación" (`/reports/BudgetExecution`): la columna "Presupuesto" no coincide con el total que muestra Lista de Presupuestos

**Estado: causa confirmada -- falta una decisión de negocio antes de tocar
código (no es un bug de cálculo, es una ambigüedad de qué significa
"Presupuesto" acá).**

Reportado por el usuario con datos reales (edificio "Los Geranios de SJ"):
en el reporte de Recaudación, la columna "Presupuesto" de cada periodo no
coincide con el "Total" que muestra esa misma cuota en Lista de
Presupuestos (`/budgetlist`) -- ej. Enero 2026 aparece con dos montos
distintos según qué pantalla se mire.

**Causa confirmada** (reproducida con datos locales, mismo patrón en los 4
periodos disponibles -- ver tabla abajo): son dos números DISTINTOS por
diseño, no un error de suma.

- **`BudgetHeader.Amount`** (lo que muestra Lista de Presupuestos) sale de
  `BudgetCalculator.CalculateTotals()` (`Classes/BudgetState.cs:108-124`):
  la suma de `MonthlyAmount` de las SECCIONES/ítems del presupuesto
  (Administración, Mantenimientos, Reservas, Serv. Básicos, etc.) -- gastos
  compartidos del edificio, nada más.
- **`Installment.Amount`** sumado por unidad (lo que usa Recaudación,
  `CollectionReport.razor:378`) sale de `BudgetCalculator.CalculateQuota()`
  (`Classes/BudgetState.cs:126-239`): la distribución de esas MISMAS
  secciones por unidad, **más el consumo de agua individual de cada
  unidad** (`wateritem.CalculatedAmount`, línea 176-182) -- un cargo real
  que sí se le cobra a cada unidad, pero que nunca formó parte del total
  de "secciones" del presupuesto (el agua de cada departamento no es un
  gasto compartido del edificio, es un cargo de traspaso según su propio
  medidor).

Confirmado con datos locales (edificio NOVA Alzamora - DEMO, únicos 4
presupuestos Ordinarios disponibles en este entorno):

| Periodo | BudgetHeader.Amount (Lista) | Σ Installment.Amount (Recaudación) | Diferencia |
|---|---|---|---|
| Marzo 2026 | S/ 6,266.00 | S/ 6,376.36 | +S/ 110.36 (1.76%) |
| Abril 2026 | S/ 5,536.00 | S/ 5,630.43 | +S/ 94.43 (1.71%) |
| Mayo 2026 | S/ 5,926.00 | S/ 6,023.52 | +S/ 97.52 (1.65%) |
| Junio 2026 | S/ 5,771.00 | S/ 5,867.04 | +S/ 96.04 (1.66%) |

La diferencia porcentual es consistente entre periodos (~1.7%) -- coincide
con que el consumo de agua es una porción chica y relativamente estable
del total, no con un error aleatorio. Ya se había tocado este mismo tema
al calcular la cuota (ver el comentario "Agua Áreas Comunes... son dos
cargos independientes" en `BudgetState.cs:186-196`, de una sesión
anterior) -- pero nunca se reconcilió contra lo que muestra Lista de
Presupuestos.

**Nota de código:** el propio `CollectionReport.razor` (línea 367-376) ya
documenta por qué eligió sumar `Installment.Amount` en vez de leer
`BudgetHeader.Amount` directo -- para que coincida con el reporte de
Morosidad, que usa las mismas cuotas. Es decir: Recaudación y Morosidad SÍ
están de acuerdo entre sí, el desacuerdo es sólo contra Lista de
Presupuestos. Nadie decidió todavía cuál de los dos es "el Presupuesto"
correcto para mostrar comparado contra "Recaudado".

**Dos opciones, sin decidir:**

**A) "Presupuesto" = `BudgetHeader.Amount`** (igual a Lista de
Presupuestos). Más intuitivo para comparar contra esa pantalla, pero
entonces "Recaudado" (que si sigue viniendo de cuotas, incluye el agua)
dejaría de ser comparable 1-a-1 contra "Presupuesto" -- quedaría una
pequeña brecha estructural en la columna "Dif" que no es morosidad real,
sino la porción de agua nunca contada en el Presupuesto.

**B) Mantener como está, pero renombrar/aclarar la columna** (ej.
"Presupuesto + Agua" o una nota al pie) para dejar explícito que este
número no es el mismo que Lista de Presupuestos, y por qué. No cambia
ningún cálculo, sólo la comunicación en pantalla.

Ninguna se implementó -- afecta también a cómo se lee "Dif" en este mismo
reporte y posiblemente al reporte de Morosidad, así que conviene decidir
una sola vez para los dos.

### Evaluación pedida por el usuario: ¿conviene mover los reportes a una consulta directa a BD?

**Mi recomendación: no reemplazar LINQ por SQL en general, pero sí vale la
pena centralizar esta lógica puntual en un solo lugar reusable.**

Este hallazgo es un buen ejemplo de POR QUÉ pasó lo que pasó, y ayuda a
responder la pregunta: no fue un error de "usar LINQ en vez de SQL" -- fue
que "cuánto se presupuestó" (`BudgetCalculator.CalculateTotals`) y "cuánto
se le cobra a cada unidad" (`BudgetCalculator.CalculateQuota`) son DOS
métodos separados, ya en C#, que nunca se compararon entre sí. El mismo
problema podría existir igual de fácil escrito en SQL si la lógica de
"sumar secciones" y "sumar cuotas por unidad" vivieran en dos stored
procedures distintos sin relación entre sí -- cambiar el lenguaje no
fuerza la reconciliación por sí solo.

Lo que sí ayudaría, tanto en LINQ como en SQL: que **todos** los reportes
que necesitan "el Presupuesto de un periodo" y "lo recaudado de un
periodo" (hoy: Recaudación y Morosidad, cada uno con su propia consulta)
llamen a un ÚNICO método/vista compartido en vez de repetir la misma
agregación en cada pantalla -- así una decisión de negocio (ej. "el agua
sí/no cuenta como Presupuesto") se toma una sola vez y se propaga sola a
todos los reportes que la usan, en vez de tener que acordarse de tocar
cada `.razor` por separado. Es más una cuestión de **una fuente de verdad
compartida** que de "LINQ vs. SQL" -- se puede lograr igual con un método
en `IBudgetService`/`IInstallmentService` reusado por ambos reportes, sin
necesidad de mover nada a un stored procedure nuevo.

Dónde SÍ un stored procedure/vista dedicada tendría una ventaja real de
mantenimiento (más allá de la reconciliación): reportes que hoy traen
TODAS las filas de una tabla completa a memoria para agregar en C# (ej.
`GetInstallmentsByBuildingAsync` trae todas las cuotas del edificio,
histórico completo, para después filtrar/agrupar en el `foreach`) -- con
años de datos acumulados eso escala peor que un `GROUP BY` hecho en SQL,
que sólo devuelve los totales ya armados. Hoy con el volumen de datos de
prueba no se nota, pero es un problema real de escala a futuro si el
edificio lleva varios años de historial. No es urgente, pero si se decide
invertir en esto, empezaría por ahí (los reportes con más historial,
Recaudación/Morosidad/Ejecución de Presupuesto) antes que por reescribir
reportes más chicos.

No se tocó código para este punto -- queda para decidir con el usuario
(opción A o B de la ambigüedad Presupuesto/Agua) antes de tocar el
reporte, y por separado, si vale la pena invertir en un método compartido
para "Presupuesto/Recaudado por periodo".
