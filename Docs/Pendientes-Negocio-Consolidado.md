# Backlog consolidado de pendientes de negocio (priorizado)

Este documento junta **solo lo que sigue abierto** (sin implementar, sin
corregir de raíz, o con una decisión de negocio todavía pendiente) de los
seis documentos `Docs/Pendientes-Negocio-*.md`. No repite lo que ya está
**implementado/resuelto** en esos documentos -- para el detalle completo,
diagnóstico y decisiones ya tomadas de cada punto, ir a la fuente indicada
entre paréntesis.

No incluye el checklist de pruebas manuales de cosas YA implementadas
(`Docs/Checklist-Pruebas-Manuales-2026-09-10.md`) -- eso es QA de código que
ya existe, no backlog.

La prioridad (Alta/Media/Baja) es una propuesta para revisar juntos, no una
decisión tomada -- avisame si el orden real de negocio es otro.

---

## Prioridad Alta -- afectan dinero/datos reales hoy, en producción

### 1. Unidades sin propietario no le facturan a nadie
*(Migración #1 — pendiente, sin empezar)*

Mientras un Depto/Oficina no tiene comprador, su cuota (por %) debería
cobrársele a la inmobiliaria (`BuildingConfiguration.RealEstateCompany`) --
hoy simplemente no se genera ningún `Installment` para esa unidad. Es
dinero que se deja de facturar activamente en cualquier edificio con
unidades sin dueño. Decisión de negocio ya tomada (usar `RealEstateCompany`
como pagador, no un `Owner` ficticio); falta el diseño de cómo esas
unidades entran al pipeline de generación (`IdGroupUnit` sintético, etc.).

### 2. Reportes financieros suman transacciones "Ignoradas"
*(Reportes #1, cruza con Conciliación #1)*

El reporte "Ingresos y Egresos" y el gráfico del Dashboard NO excluyen
transacciones marcadas como "Ignorado" en Conciliación (ej. un error
bancario revertido) -- siguen sumando en los totales. Requiere exponer
`Ignored` en `AccountStatementDetailView`/`GET_AccountStatementDetailByHeader`
(SP no versionado en el repo, hay que pedir su texto real antes de tocarlo).

### 3. Tolerancia de redondeo en conciliación de cuotas (< S/ 0.05)
*(Migración #8 — pendiente, sin empezar, "a confirmar si aplica")*

Cuotas migradas quedan como "Parcial" con centavos de deuda que son solo
redondeo del Excel origen, no deuda real -- infla la morosidad reportada.
Falta: confirmar con el usuario si aplica solo a datos migrados o a la
conciliación diaria en general, y aplicarlo donde se decide
`Parcial`/`Conciliada`.

### 4. Borrado de edificio: riesgo de datos huérfanos no descartado
*(Migración #6.3 — mitigado, no confirmado de raíz)*

`DeleteBuildingAsync` sólo borra `UserBuildingAssociation` +
`BuildingConfiguration` + `Building`, sin cascada. Sólo se confirmó FK real
en 2 de ~14 tablas con `IdBuilding` (`Category`, `Contact`/`Parameter`) --
las otras ~12 (`RealEstateUnit`, `Owner`, `BankAccount`, `BudgetHeader`,
`Incident`, etc.) no tienen su FK confirmada. Hoy está gateado a SysAdmin y
pensado sólo para edificios de prueba vacíos, pero **antes de confiar en
este botón para algo más que eso**, alguien con acceso a la BD real debe
confirmar `sys.foreign_keys` sobre esas tablas.

### 5. Soporte real de multimoneda
*(Conciliación #10 — pendiente, sin empezar)*

Hoy `BuildingConfiguration.Currency` es una sola moneda por edificio (solo
etiqueta) y la carga de Estado de Cuenta valida hardcodeado PEN/USD. No hay
tipo de cambio ni definición de qué pasa si conviven montos en más de una
moneda (cuotas, gastos, reportes, conciliación). Falta todo el diseño.

---

## Prioridad Media -- funcionalidad de negocio real, pero no sangra dinero hoy

### 6. Bug compartido en modales de confirmación (`ConfirmationUtil.ExecuteWithConfirmation`)
*(Conciliación #4, encontrado de paso -- sin corregir)*

El mismo bug de orden (`Show(type)` antes de fijar `Message`) que ya se
corrigió en `ReconciliationWorkspace.ConfirmarAsync` sigue vivo en el
helper compartido `Classes/Utilities.cs:39-41`, usado por
`ModalOwnerUnit.razor`, `BudgetGenerator.razor`, `ServiceReadingModal.razor`
y `ManualInstallmentConciliation.razor` -- la primera confirmación en esas
pantallas puede mostrar el mensaje default o el de una acción anterior en
vez del real. Fix es el mismo, un solo cambio de orden, pero toca 4+
pantallas a la vez.

### 7. Garantía de reserva de área común (cobro y devolución)
*(Conciliación #2 — pendiente, sin empezar, sin diseño todavía)*

El cobro de garantía y su devolución (total/parcial según daños) no tienen
hoy un lugar claro en la conciliación bancaria -- no es Ingreso normal, no
es Gasto normal, y la devolución no es lo mismo que "Corregir". Falta todo
el diseño de cómo se registra cada lado.

### 8. Historial de propietarios por periodo
*(Migración #2 — pendiente, sin empezar)*

El sistema no versiona por fecha quién era dueño de una unidad -- hoy sólo
existe el propietario vigente. Necesario para poder reconstruir "quién era
dueño de la 301 en marzo de 2019" de forma estructurada (hoy sólo queda el
texto libre en `Installment.OwnerName`). Falta diseñar una tabla de
historial con vigencia.

### 9. `GET_UnitsByType` no tolera unidades sin grupo
*(Migración #5 — mitigado solo en un lugar, sin corregir de raíz)*

Tira `SqlNullValueException` si una unidad no tiene `IdGroupUnit` asignado
-- afecta a **cualquier edificio a mitad de configurar unidades**, no sólo
al importador de migración (ej. "Descargar plantilla" de Lecturas de Agua
en `BlockWaterReading.razor` también lo usa). Sólo se puso un `try/catch`
local en el importador; la causa (SP/mapeo EF) sigue sin filtrar/manejar
unidades sin grupo en el resto de la app.

### 10. Estado de Cuenta migrado no crea Gastos categorizados
*(Migración #7 — fuera de alcance por ahora)*

La columna "Categoría" de la plantilla de migración de Estado de Cuenta se
valida pero no se guarda en ningún lado -- cargar egresos históricos ya
categorizados como `Expense` real es un alcance más grande que sólo
registrar el movimiento bancario, todavía no incluido.

### 11. Falta el ítem de menú "Permisos" en Configuración
*(Permisos — pantalla `/Settings/Permissions` ya existe, falta enlazarla)*

La pantalla de administración de Permisos ya está implementada, pero no
hay forma de llegar ahí desde el menú -- hay que crear el ítem desde
`/Settings/MenuItems` (tarea de configuración, no de código).

---

## Prioridad Baja -- deuda técnica menor, casos puntuales o decisiones ya tomadas de dejar afuera

### 12. Un caso sin match en el Excel de Nova Alzamora
*(Migración #4 — a resolver a mano, no requiere código)*

1 de 1,602 `InstallmentPaid` sin match verificado contra `Consolidado.ID`.
El usuario ya decidió corregirlo directamente en el Excel al momento de la
migración real -- sólo queda anotado para no perderlo de vista.

### 13. Causa raíz del timeout en "Conciliación de Pagos" con rangos amplios
*(Migración #6.6 — mitigado subiendo el `CommandTimeout` a 120s)*

No se pudo confirmar la causa exacta (el SP `GET_BankTransactionsNoConcilied`
no está versionado en el repo) -- si con el timeout más alto el problema
persiste, hace falta revisar el SP en sí (índices, o paginar el rango en
el cliente).

### 14. Confirmar si `INS_ServiceReadingDetail` hace upsert real
*(Agua #4 — no verificado, sin acceso a BD ni al SP)*

No se pudo confirmar si re-guardar un período ya existente hace upsert o
fallaría por PK duplicada -- por ahora no hay ningún reporte de error, así
que es más una duda documentada que un bug confirmado.

### 15. Borrar un permiso desde `/Settings/Permissions`
*(Permisos — explícitamente no pedido, fuera de alcance)*

El usuario pidió sólo "creación o modificación". Si se llegara a necesitar,
falta decidir qué hacer con la asignación a roles si el permiso ya está en
uso.

### 16. Verificar la URL del ítem de menú "Ingresos y Egresos"
*(Reportes — tarea operativa, no código)*

La página nueva quedó en `/reportes/ingresos-egresos` -- si el ítem de menú
ya apuntaba a otra ruta, actualizarlo desde Configuración > Items de Menú.

---

## Resumen rápido

| # | Tema | Prioridad | Tipo |
|---|------|-----------|------|
| 1 | Unidades sin propietario no facturan | Alta | Diseño + código |
| 2 | Reportes suman transacciones Ignoradas | Alta | Código (requiere ver SP) |
| 3 | Tolerancia de redondeo en conciliación | Alta | Decisión + código |
| 4 | Borrado de edificio: FKs sin confirmar | Alta | Verificación de BD |
| 5 | Soporte real de multimoneda | Alta | Diseño + código |
| 6 | Bug `ConfirmationUtil` (4+ pantallas) | Media | Código (fix chico, alcance ancho) |
| 7 | Garantía de reserva de área común | Media | Diseño + código |
| 8 | Historial de propietarios por periodo | Media | Diseño + código |
| 9 | `GET_UnitsByType` sin manejar unidades sin grupo | Media | Código |
| 10 | Estado de Cuenta no crea Gastos categorizados | Media | Diseño + código |
| 11 | Falta ítem de menú "Permisos" | Media | Configuración |
| 12 | Caso sin match Excel Nova Alzamora | Baja | Manual (Excel) |
| 13 | Causa raíz timeout Conciliación de Pagos | Baja | Investigación |
| 14 | Confirmar upsert de `ServiceReadingDetail` | Baja | Investigación |
| 15 | Borrar un permiso | Baja | Fuera de alcance |
| 16 | Verificar URL de menú "Ingresos y Egresos" | Baja | Configuración |
