# Plan de Estandarización de UI — Tema, Modales y 3 Pantallas a Rediseñar

Pedido del usuario (2026-09-22), tras aprobar la dirección de diseño Light/Dark
(ver el canvas "SpiderHood Look and Feel"): estandarizar ese mismo look en
**todas** las pantallas de la app, estandarizar el patrón de modal que ya se
usó en Crear/Editar Edificio-Unidad-Propietario para el resto de los modales,
y rediseñar 3 pantallas puntuales que se sienten poco intuitivas: Carga de
Estado de Cuenta, Carga de Agua y Presupuesto.

Este documento es el **plan propuesto, sin implementar todavía** -- pide
confirmación de orden/alcance antes de tocar los ~50-90 archivos que esto
puede llegar a tocar.

---

## 1. Estándar de Modales

### El patrón canónico (ya en producción, no inventar nada nuevo)
Definido esta sesión en `BuildingPage.razor` (edit-building), `ModalOwner.razor`
y `ModalUnit.razor`:
- Cuerpo con tabs (`activeTab` int + `@if (activeTab == N)`), cuando el
  formulario tiene más de ~6-8 campos.
- `IsScrollable="true"` (BlazorBootstrap) o `modal-dialog-scrollable` (hand-rolled)
  -- el body scrollea, el footer con los botones queda siempre visible.
- Footer: `<div class="d-flex justify-content-between w-100"><div>[Anterior, disabled en tab 1]</div><div class="d-flex gap-2">[Cancelar][Siguiente o Guardar/Actualizar]</div></div>`
  -- **Cancelar siempre antes que el botón de confirmar**, nunca al revés.

### Inventario real (auditado 2026-09-22, agente de exploración read-only)
**~65 diálogos modales en 50 archivos. Sólo 3 ya cumplen el patrón completo
(~5%).** Es un esfuerzo grande, pero divisible en fases con retorno
decreciente por fase -- la Fase 1 sola arregla el bug más visible.

| Grupo | Qué tienen | Qué falta | Archivos (ejemplos) |
|---|---|---|---|
| **A -- Canónico** | Todo | Nada | `BuildingPage.razor`, `ModalOwner.razor`, `ModalUnit.razor` |
| **B -- BlazorBootstrap `<Modal>`, pero con el botón de confirmar ANTES que Cancelar** (~25 diálogos / 12 archivos) | Componente correcto | Orden de botones invertido (el mismo bug que ya corregimos en Edificio/Unidad/Propietario), sin tabs, sin `IsScrollable`, footer sin agrupar | `Owners.razor`, `UnitGroups.razor`, `BuildingConfig.razor` (4), `Announcements.razor`, `BudgetGenerator.razor` (2), `BudgetList.razor`, `CategoryPage.razor`, `ReservationsAdmin.razor` (4), `Reservations.razor` (2 de 3) |
| **C -- Hand-rolled, orden de botones ya correcto** (~25 diálogos / ~20 archivos) | Cancelar ya antes que Guardar | Sin tabs/scrollable/footer agrupado -- formularios largos scrollean mal | Todo `EmployeePages/*` (7), `ModalExpense.razor`, `Meetings.razor`, `MeetingMinutesPage.razor`, `ModalMovement.razor`, `ServiceReadingModal.razor`, `IncidentList.razor`, `IncidentDetail.razor`, `WorkFlowPages/Index.razor` (2), `PeriodForm.razor`, `ModalCategory.razor`, `ParameterPage.razor`, `CalendarPage.razor` (2), `ReconcilePaymentModal.razor`, `ReconciliationWorkspace.razor` (2), `ModalOwnerUnit.razor` |
| **D -- Sin footer de acción** (sólo cerrar/ver) | -- | No aplica el estándar, dejar como está | `SettingPages/Users.razor`, `ExpensePage.razor`, `ManualInstallmentConciliation.razor`, `MyPayments.razor`, `BlockWaterReading.razor` (vista detalle), `BankStatementDetail.razor` |
| **E -- Patrón propio, aparte** | -- | Revisar caso por caso | `BudgetDetailModal.razor` (clases `modal-overlay`/`modal-content` propias, ni Bootstrap ni BlazorBootstrap), `Confirmemail.razor` (`data-bs-dismiss` en vez de cierre por componente) |

**Hallazgo clave:** los modales que ya usan el componente "correcto"
(BlazorBootstrap `<Modal>`, Grupo B) son paradójicamente los que tienen el
botón en el orden equivocado -- heredan el default de la librería en vez del
convenio que definimos. Los modales viejos hand-rolled (Grupo C), sin
querer, ya tenían el orden bien.

### Fases propuestas
1. **Fase 1 -- Grupo B: sólo reordenar botones** (Cancelar antes que
   Guardar/Eliminar). Cambio mecánico, bajo riesgo, alto impacto visual --
   mismo bug que ya se vio en Junta Directiva. ~12 archivos.
   **RESUELTO (2026-09-22)** -- 9 archivos, ~15 diálogos: `Owners.razor`,
   `UnitGroups.razor` (2 de 3), `BuildingConfig.razor` (4),
   `Announcements.razor` (1 de 2), `BudgetGenerator.razor` (2),
   `BudgetList.razor`, `CategoryPage.razor`, `ReservationsAdmin.razor` (4),
   `Reservations.razor` (2 de 3). Sólo el orden de botones -- scrollable/tabs
   quedan para la Fase 2.
2. **Fase 2 -- Grupo C: agregar `IsScrollable`/scroll interno + agrupar el
   footer** en los formularios largos (no todos necesitan tabs -- sólo los
   que hoy scrollean mal). Evaluar cuáles de los ~20 archivos realmente
   tienen ese problema antes de tocarlos todos.
   **RESUELTO (2026-09-23).** Auditados los ~20 archivos (agente de
   exploración, tabla completa con conteo de campos y estado de cada uno).
   La mayoría ya scrollea bien o tiene su propio scroll interno en la
   lista larga (`ReconcilePaymentModal.razor`, `ModalOwnerUnit.razor`) --
   se dejaron igual. Se agregó `modal-dialog-scrollable` sólo donde hacía
   falta: `EmployeeList.razor` (10 campos), `EmployeeDetail.razor`
   ("Registrar horas", salvaguarda barata), `Meetings.razor` (agenda sin
   límite de items), `CalendarPage.razor` ("Programar/Editar Item", hasta
   11 campos condicionales), `ModalCategory.razor` (grillas de
   icono/color altas), `ServiceReadingModal.razor` (modal-xl que delega en
   `BlockWaterReading`), `PeriodForm.razor` (justo en el umbral). Además,
   `ReconciliationWorkspace.razor` ("Conciliar Transacción") no tenía
   `modal-footer` -- sólo la X del header -- se le agregó uno con
   Cancelar. No se convirtió ningún formulario a tabs -- ninguno lo
   necesitaba tanto como para justificar ese cambio más grande.
3. **Fase 3 -- Grupo E:** normalizar `BudgetDetailModal.razor` al patrón
   Bootstrap/BlazorBootstrap real, corregir `Confirmemail.razor`.
4. **Grupo D:** no requiere cambios de este estándar.

---

## 2. Estándar de Tema Claro/Oscuro (rollout de los tokens ya definidos)

`SpiderHood/wwwroot/css/tokens.css` ya define todo lo necesario
(`--sh-surface`, `--sh-text`, `--sh-border`, `--sh-success-light`, etc.,
con su variante `[data-bs-theme="dark"]`). El problema no es que falte el
sistema -- es que **87 archivos todavía no lo usan**, con colores
hardcodeados que no van a cambiar cuando el usuario cambie de tema.

### Inventario real (mismo agente, 2026-09-22)
**87 archivos, ~272 apariciones de hex/rgb/rgba/clases Bootstrap fijas
(`bg-light`, `bg-white`, `text-dark`) fuera de `tokens.css`/`components.css`.**
Esfuerzo mediano-grande, pero **concentrado**: el 40% de las apariciones
están en los primeros ~10 archivos, y la mayoría cae en un solo patrón
repetido.

**Categorías, de mayor a menor impacto:**
1. **Badges de estado con `bg-light text-dark` / `bg-warning text-dark`**
   -- de lejos el patrón más común, repetido en decenas de archivos
   (Gobernanza, Conciliación, Incidencias, Reportes, Residente). Se ve
   bien en modo claro, **ilegible en modo oscuro** (texto oscuro sobre un
   fondo que en dark mode ya no es claro). Ejemplos: `MeetingDetail.razor`
   (21 apariciones), `ReconciliationWorkspace.razor` (9).
2. **Mapas de calor / selector de color de categoría** -- `DelinquencyReport.razor.css`
   (`.heat-1`...`.heat-5`, hex fijos) y `ModalCategory.razor` (24 swatches
   hex, es una paleta de colores intencional para categorías -- bajo
   riesgo, pero igual no reacciona al tema).
3. **Estilos de página sueltos** -- el peor caso es `ExpensePages/ExpenseNew.razor.css`
   (16 apariciones: gradientes, sombras y hex de Bootstrap pegados
   directo en el CSS) -- se va a ver plano/roto al lado de una pantalla
   ya tokenizada.

**Recomendación de arreglo -- no archivo por archivo:** el patrón de badges
(categoría 1, la mayoría de los 272 hits) se resuelve de una sola vez con
**un componente o clase CSS compartida** (`sh-badge-activo`,
`sh-badge-pendiente`, `sh-badge-vencido`, etc., usando
`--sh-success-light`/`--sh-warning-light`/`--sh-danger-light` ya
existentes) en vez de tocar cada `bg-light text-dark` a mano en 40+
lugares. Los mapas de calor y el CSS suelto de Expense sí necesitan
atención individual.

### Fases propuestas
1. **Fase 1:** crear las clases/badge compartidas basadas en tokens, y
   hacer un find-and-replace guiado archivo por archivo de `bg-* text-dark`
   → la clase nueva (resuelve la mayoría de los 272 hits de una).
   **RESUELTO (2026-09-22) -- más chico de lo estimado.** Al revisar el
   código real, `components.css` ya tenía un override centralizado para
   `.badge.bg-success/warning/danger/info/secondary` desde antes de este
   plan -- sólo faltaban dos huecos reales: `.badge.bg-light` (el patrón
   más repetido, sin ningún override) y el color de texto de
   `.badge.bg-warning`/`.badge.bg-info` en modo oscuro (pensado sólo para
   fondo claro). Se agregó lo que faltaba, más el mismo tratamiento para
   `.card-header`/`.card-footer` con esas mismas combinaciones (13
   archivos, ej. `UserRoles.razor`, `Security.razor`). **Un solo cambio
   en `components.css` -- no hizo falta tocar los ~30 archivos que usan
   estas clases**, todos ya pasan por ellas. Verificado visualmente con
   Playwright (luz y oscuro) antes de commitear. `bg-primary`/`bg-secondary`/
   `bg-dark` con `text-white` quedan igual -- esos ya leen bien en
   cualquier tema.
2. **Fase 2:** `ExpenseNew.razor.css` y los otros CSS de página con
   estilos propios (gradientes/sombras hardcodeadas) -- uno por uno.
   **RESUELTO (2026-09-23)** para `ExpenseNew.razor.css` -- el único bug
   real de contraste era `.expense-detail` (fondo casi blanco fijo
   #fcfcfc/#f5f5f5, texto claro de modo oscuro ilegible encima),
   corregido con `--sh-surface-muted`/`--sh-border` (mismo criterio que
   `.category-header` en el mismo archivo). Los gradientes de las 3
   tarjetas de resumen (`bg-gradient-primary/success/info`) usaban los
   mismos valores hex de Bootstrap por defecto -- no era un bug visual
   (mismo valor final), sólo duplicación; se apuntaron a
   `var(--sh-primary/success/info)`.
3. **Fase 3:** mapas de calor de Reportes (`DelinquencyReport`,
   `WaterConsumptionReport`, etc.) -- definir una escala de color
   theme-aware (probablemente tokens nuevos `--sh-heat-1`...`--sh-heat-5`
   con su variante dark).
   **RESUELTO (2026-09-23)** para `DelinquencyReport.razor.css` (único
   heat-map real en la app hoy -- `WaterConsumptionReport.razor` sólo
   tiene un color de línea de gráfico, no un fondo con texto encima, no
   hacía falta tocarlo). Los 5 pasteles fijos (`.heat-1`...`.heat-5`) se
   movieron a tokens `--sh-heat-1`...`--sh-heat-5` en `tokens.css`, con
   variante oscurecida bajo `[data-bs-theme="dark"]` (mismo problema que
   Expense: pastel claro + texto claro de modo oscuro = ilegible).
   Verificado con Playwright en ambos temas.
4. **Paleta de categorías** (`ModalCategory.razor`): queda igual a
   propósito -- es una selección de color intencional del usuario, no un
   bug de tema.

---

## 3. Rediseño de 3 pantallas puntuales

Las tres comparten el mismo problema de fondo: son procesos de **varios
pasos reales** (elegir contexto → cargar/completar datos → revisar →
confirmar) pero están armadas como una sola página larga sin ninguna
señal visual de en qué paso está el usuario. Propongo que las tres usen
el **mismo indicador de pasos** (un componente chico y reusable, la
segunda pieza de estandarización real de este plan, además de los
modales) -- ver el Design canvas para cómo se vería.

### 3.1 Carga de Estado de Cuenta (`UploadBankStatement.razor`)
**RESUELTO (2026-09-22).** Reorganizado en 3 pasos reales con el nuevo
componente compartido `StepIndicator` (`Components/Pages/Components/`):
**① Cuenta Bancaria** (el tipo de cambio ahora aparece DESPUÉS de elegir
la cuenta, ya no antes de todo sin contexto) → **② Subir Archivo** →
**③ Revisar y Confirmar**, con el resumen de Válidos/Duplicados/Errores
movido arriba de la tabla de detalle, y un resumen colapsado de
Cuenta/Archivo con links "Cambiar" para corregir sin recorrer todo el
wizard de nuevo. De paso se encontró y corrigió un bug real:
`fileName`/`fileSize` nunca se asignaban en `HandleFileSelected` -- el
recuadro "Archivo seleccionado" de la versión anterior nunca llegaba a
mostrarse. Verificado con Playwright end-to-end (login real, .xlsx de
prueba, los 3 pasos) en claro y oscuro.

### 3.2 Carga de Agua (`BlockWaterReading.razor`)
**RESUELTO (2026-09-22), con un ajuste real respecto al plan original.**
Al leer el archivo completo (1331 líneas, no las ~120 revisadas para este
plan) se confirmó que la tabla de lecturas no es sólo una vista previa --
también es donde se corrigen lecturas fila por fila (aplicar mínimo, otro
valor, quitar filas con error). Forzar un wizard con Siguiente/Anterior
propio ahí arriesgaba romper ese flujo real. Se optó por un indicador de
pasos **pasivo** (calculado de `CurrentReadingDetail.Any()`, no un
contador de wizard nuevo) en vez de replicar el mismo mecanismo de
Estado de Cuenta al pie de la letra. Sí se aplicó el resto de lo
propuesto: se sacó el anidado de tarjeta-dentro-de-tarjeta, la zona de
importar ahora usa el mismo lenguaje visual (drop-zone punteada) que
Estado de Cuenta, y "Lecturas procesadas" pasa a ser una tarjeta de
estadística real. Verificado con Playwright en claro y oscuro (sin datos
de prueba con lecturas cargadas en este edificio demo, pero la lógica de
qué se muestra no se tocó, sólo el layout alrededor).

### 3.3 Presupuesto (`BudgetGenerator.razor`)
**RESUELTO (2026-09-23).** Mapeo dedicado (lectura completa de las 2036
líneas + `BudgetHeaderComponent`, `InformationPanelComponent`,
`InstallmentTable`, `ServiceReadingModal`), propuesta validada con canvas
(preview de los estados Creado y Publicado) y luego implementada.

**Lo que hay hoy (resumen del mapeo):**
- La pantalla tiene dos modos: si el presupuesto es Extraordinario/Cargos
  (sin `BudgetDetail`, los montos se generan directo a `Installment`)
  muestra sólo una tarjeta chica de sólo lectura -- el flujo grande de
  abajo no aplica. El flujo real a rediseñar es el Ordinario (mensual).
- En el modo Ordinario **no hay una secuencia de pasos "llenar → revisar"
  como en las otras dos pantallas** -- hay una **máquina de estados de
  aprobación** (`Created → Check → Approved → Active → Closed`, con
  `Rejected` como bucle de vuelta a editable) que ya gatea qué botones
  aparecen (Enviar a Aprobación / Aprobar-Rechazar / Publicar / Cerrar),
  pero sin ninguna señal visual de en qué parte de esa cadena está el
  presupuesto -- sólo un badge de texto.
- La tabla de gastos (secciones + items) es una **grilla editable en
  vivo**: cada cambio recalcula el total del item, de la sección, el
  gran total, las tarjetas KPI del header y el panel lateral, todo al
  mismo tiempo. Entrada y resultado conviven siempre en la misma
  pantalla -- no hay un "paso de captura" separado de un "paso de
  revisión" (mismo hallazgo de riesgo que en Carga de Agua, pero aquí es
  el corazón de toda la pantalla, no un caso aislado).
- "Cal. Agua" (el modal que embebe la ya rediseñada `BlockWaterReading`)
  es una acción flotante disponible en casi cualquier momento del flujo
  Ordinario, no un paso fijo -- su falta sólo se bloquea recién al
  intentar Enviar a Aprobación/Publicar (toast reactivo, no una señal
  visible de antemano).
- Validación de completitud (secciones/items/montos/lectura de agua) ya
  existe (`ValidarPresupuestoParaAprobacion`), pero es invisible hasta
  que el usuario ya intentó avanzar y choca con el bloqueo.

**Por qué NO aplica el mismo `StepIndicator` de wizard que las otras dos:**
esta pantalla no tiene "paso 1, paso 2, paso 3" -- tiene un **estado de
aprobación** (que dura días/semanas, con roles distintos actuando en cada
uno) y una **grilla siempre-editable** debajo. Forzar un wizard con
Siguiente/Anterior sobre la grilla rompería el flujo real (el
administrador entra y sale de las secciones muchas veces mientras mira
los totales, exactamente el riesgo que ya se evitó en Carga de Agua).

**Propuesta (para validar antes de tocar código):**
1. **Barra de estado del flujo de aprobación**, arriba de la tabla de
   gastos (mismo lenguaje visual que `StepIndicator` -- círculos +
   conectores -- pero representando `Created → Check → Approved →
   Active/Closed`, con `Rejected` marcado como una alerta que vuelve a
   `Created`, no un 6to círculo en la línea). Reemplaza/complementa el
   badge de texto actual del header; usa las mismas clases
   `--sh-success`/`--sh-brand-gold`/`--sh-text-muted` ya definidas en
   `StepIndicator.razor.css`.
2. **Checklist de completitud visible ANTES de "Enviar a Aprobación"**
   (no sólo al chocar con el toast): un pequeño panel con 2-3 líneas tipo
   "✓ 3 secciones cargadas", "✓ 12 items", "✗ Lectura de agua pendiente"
   -- construido sobre la misma lógica que ya usa
   `ValidarPresupuestoParaAprobacion()`, sólo que evaluada y mostrada de
   forma proactiva en vez de reactiva.
3. **La tabla de gastos se queda como está** -- grilla editable en vivo,
   sin pasos forzados. Ningún cambio estructural ahí; sólo limpieza
   visual menor si hace falta para que combine con el resto (tokens de
   color, no lógica).
4. **"Cal. Agua" se queda como acción flotante del header** -- no se
   fuerza a paso fijo -- pero su estado (pendiente/lista) se refleja en
   el checklist del punto 2, así el admin ve la falta antes de intentar
   avanzar, no después.
5. El branch Extraordinario/Cargos (tarjeta chica de sólo lectura) queda
   igual -- no necesita nada de esto.

**Nota aparte, no bloqueante:** el mapeo encontró código muerto/duplicado
(`AddNewSectionFromModal1` sin usar, un bloque de "Fórmula de Cálculo"
comentado, varios métodos stub como `GenerateReport`/`CopyToClipboard`/
`NotifyOwners` que no hacen nada real) -- no forma parte de este plan de
UI, pero vale la pena registrarlo para una futura limpieza técnica
(agregado a `Docs/Pendientes-Negocio-Consolidado.md` si el usuario lo
confirma).

**Lo implementado (validado con canvas antes de tocar código):**
- Nuevo componente compartido `ApprovalStatusBar`
  (`Components/Pages/Components/`) -- mismo lenguaje visual que
  `StepIndicator` (círculos + conectores, mismos tokens) pero para el
  ESTADO real de aprobación (`Created→Check→Approved→Active/Closed`, con
  `Rejected` como píldora de alerta que vuelve a Creado) en vez de un
  wizard de captura. Se muestra sólo en el branch Ordinario -- el branch
  Extraordinario/Cargos queda igual, con su badge de texto de siempre.
- Checklist de completitud ("Antes de enviar a aprobación") visible de
  forma proactiva -- sólo mientras el presupuesto es editable
  (Creado/Rechazado) -- con los mismos 4 chequeos "duros" que ya usaba
  `ValidarPresupuestoParaAprobacion` (secciones, items, monto total,
  lectura de agua), extraídos a `EvaluarLecturaAguaPresupuesto()` +
  `ObtenerChecklistAprobacion()` para que ambos lugares (el checklist y
  la validación real al hacer click) usen exactamente el mismo criterio.
  El botón "Enviar a Aprobación" ahora se deshabilita hasta completar la
  lista, en vez de sólo avisar con un toast después del click.
- La tabla de gastos y la Vista de Cuotas (`InstallmentTable`) **no se
  tocaron** -- verificado en vivo (Playwright, presupuesto real
  "Junio 2026" ya Publicado) que siguen en el mismo lugar, con la misma
  condición de antes, coexistiendo con la barra de estado nueva sin
  ningún cambio visual ni de comportamiento.

---

## 4. Orden recomendado (a confirmar)

1. Modales Fase 1 (reordenar botones del Grupo B) -- mecánico, rápido,
   mismo bug ya conocido.
2. Tema Fase 1 (badges compartidos por tokens) -- resuelve la mayoría de
   los 272 hits de una sola vez.
3. Rediseño de Carga de Agua y Carga de Estado de Cuenta con el nuevo
   indicador de pasos (más chicas, validan el patrón antes de escalarlo).
4. Modales Fase 2-3, Tema Fase 2-3 -- Fase 2 de ambos **RESUELTA
   (2026-09-23)**. Queda Modales Fase 3 (Grupo E: `BudgetDetailModal.razor`,
   `Confirmemail.razor`).
5. Presupuesto -- mapeo dedicado **HECHO (2026-09-23)**, propuesta en §3.3
   pendiente de validar antes de implementar (no es un wizard como las
   otras dos -- es una barra de estado de aprobación + checklist de
   completitud, la tabla de gastos se queda igual).

**Antes de arrancar, confirmar:** ¿este orden sirve, o hay alguna pantalla
puntual que sea más urgente por uso real de los residentes/administradores
ahora mismo? ¿Se puede ver primero un mockup del indicador de pasos
(punto 3) antes de aplicarlo a las 2 pantallas, mismo criterio que el
canvas de Login/Dashboard?

---

## 5. Menú lateral agrupado y top bar simplificado

**RESUELTO (2026-09-23).** Parte de la dirección visual aprobada en el
canvas "SpiderHood Look and Feel" (Dashboard-Light) desde el principio de
la sesión, pero que nunca se había implementado -- el trabajo se fue
directo a Tema/Modales/Pantallas por el orden de prioridades del usuario
y este pendiente quedó en el tintero hasta que el usuario lo notó
revisando el resultado de Presupuesto.

### Top bar
Se sacó el breadcrumb (ícono casa + título de página, calculado por
`GetPageLabel()` contra un diccionario de ~50 rutas) -- la navegación a
Dashboard ya está en el menú lateral. Arriba sólo queda el selector de
Edificio (ahora a la izquierda) y la cuenta (rol + usuario, a la
derecha). `HeaderMainLayout.razor`.

### Menú lateral agrupado (GENERAL / COMUNIDAD / ADMINISTRACIÓN)
El menú no tenía ningún concepto de "grupo" -- ni en la tabla
`MenuItems`, ni en el modelo, ni en los stored procedures -- así que se
eligió (a pedido explícito del usuario, entre esa opción y un mapeo
cosmético sólo en el layout) agregar un campo real:

- `Database/Scripts/2026-09-23_143_MenuItems_AgregarGrupo.sql`: columna
  `GroupName NVARCHAR(50)` en `MenuItems`, backfill de los items raíz
  conocidos por `ItemKey` (con fallback por Título para "Reservas y
  Mantenimientos", que tiene el ItemKey vacío), y `ALTER PROCEDURE` de
  `GET_FullMenu`, `GET_MenuItem`, `INS_MenuItem`, `UPD_MenuItem` para que
  lean/escriban la columna nueva. **El usuario tiene que correr este
  script en su base real** -- el backfill por `ItemKey`/Título es
  best-effort, cualquier item que no matchee queda sin grupo (no rompe
  nada, sólo no muestra encabezado de sección) hasta asignárselo a mano.
- Agrupación aplicada: **General** (Dashboard, Adm. Edificio,
  Presupuesto, Conciliación, Reportes), **Comunidad** (Portal del
  Residente, Junta de Propietarios, Incidencias y Comun., Reservas y
  Mantenimientos), **Administración** (Personal, Configuración) -- las 3
  primeras validadas explícitamente con el usuario; "Junta de
  Propietarios" en Comunidad es una extensión razonable no confirmada
  palabra por palabra (no aparecía en el mockup original).
- `LeftMenu.razor`: reusa la clase CSS `.nav-section-header` que ya
  existía en `LeftMenu.razor.css` (definida, pero nunca usada -- parece
  un intento anterior de esto mismo que quedó a medias). Los items se
  reordenan por grupo ANTES de renderizar (`MenuAgrupado`, General →
  Comunidad → Administración → sin grupo) en vez de confiar en que el
  `DisplayOrder` de la base ya venga contiguo por grupo -- un ítem como
  "Reportes" con un `DisplayOrder` que cae en medio de otro grupo
  partía la sección en dos encabezados repetidos antes de este ajuste.
- `/Settings/MenuItems/Edit`: nuevo campo "Grupo" (General/Comunidad/
  Administración/sin grupo), visible sólo para items raíz -- probado en
  vivo un round-trip completo (cambiar grupo, guardar, verificar en BD,
  revertir).
- Sólo aplica a items raíz -- los hijos de un submenú siguen agrupados
  bajo su padre como siempre, no tienen grupo propio.

Verificado con Playwright (aplicado el script a la base local de
desarrollo) en Dashboard: claro, oscuro y mobile (menú hamburguesa).

### Refinamiento visual del sidebar (Opción A) -- RESUELTO (2026-09-23)
Segunda vuelta de diseño, a partir de feedback sobre contraste: se
prepararon dos direcciones en el canvas (`Sidebar-A-Refinado.dc.html` /
`Sidebar-B-ContrasteAlto.dc.html`), el usuario eligió la **A** y pidió
además sacar la tarjeta de usuario del sidebar (queda cubierta por el
menú de cuenta del top bar). Aplicado a la app real:

- **Tarjeta de usuario**: comentada (no borrada) en `LeftMenu.razor` y su
  CSS en `LeftMenu.razor.css`, por si más adelante se decide traerla de
  vuelta o mostrar el rol de forma sutil en el menú de cuenta del top bar
  (mencionado por el usuario, no implementado todavía).
- **Logo** (`NavMenu.razor`/`.css`): "SpiderHood" pasa a peso 800, y el
  subtítulo "Panel· v1.0" se reescribe como "PANEL · V1.0" con el punto
  medio en dorado y el resto en gris apagado (antes todo el subtítulo
  era dorado parejo) -- más jerarquía, más contraste con el título.
- **Encabezados de grupo**: `.nav-section-header` pierde el borde inferior
  y baja de peso/tamaño (más aire, menos "línea dura" entre grupos).
- **Bug real encontrado y corregido**: las reglas de `.nav-link` en
  `LeftMenu.razor.css` (color base, `:hover`, `.active` con el gradiente
  dorado) nunca aplicaban a los ítems de menú sin submenú -- esos usan
  `<NavLink>` (componente de framework), que renderiza su propio `<a>`
  SIN el atributo de scope de CSS isolation de este componente, así que
  selectores como `.nav-pills .nav-link.active` compilaban pidiendo ese
  atributo justo en el elemento que nunca lo tiene. En la práctica: el
  ítem activo del menú (ej. "Dashboard") se veía con el azul default de
  Bootstrap, no el dorado ya codeado -- esto es, con certeza, el "pill
  celeste/violeta" que se veía en los screenshots que motivaron todo este
  rediseño, no una decisión de diseño. Arreglado agregando el combinador
  `::deep` a esas 3 reglas (`.nav-pills ::deep .nav-link`, `:hover`,
  `.active`) para que alcancen el `<a>` de `<NavLink>` sin pedirle el
  atributo de scope. Verificado en vivo: el ítem activo ahora se ve con
  el gradiente dorado como estaba previsto.
- **Nota aparte (no arreglada, fuera de alcance de hoy)**: al navegar a
  un hijo de submenú (ej. "Edificio" bajo "Adm. Edificio") el `<NavLink>`
  no le agrega la clase `active` pese a que la URL coincide exactamente
  -- parece un problema de matching de rutas independiente del CSS, no
  investigado a fondo. Si se corrige más adelante, el estilo dorado ya
  va a aplicar solo (la regla `.active` ya alcanza cualquier `<NavLink>`
  después del fix de arriba).

Verificado con Playwright: build limpio, 172/172 tests, login real con
hash de contraseña temporal (revertido al terminar) y capturas del
Dashboard con el sidebar ya sin tarjeta de usuario y con el ítem activo
en dorado.
