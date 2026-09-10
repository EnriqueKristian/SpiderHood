# Checklist de pruebas manuales — sesión 2026-09-09/10

Todo lo de acá ya tiene el código aplicado (ver `Docs/Pendientes-Negocio-*.md`). Los
scripts `55` a `77` en `Database/Scripts/` ya están confirmados corridos (el usuario
verificó con `2026-09-10_78_Verificar_Pendientes_Aplicados.sql`). Lo que falta es
**probarlo en la app real** con datos reales -- esta sesión no tuvo acceso a BD ni
pudo levantar el proyecto (sin SDK de .NET), así que nada de esto se probó todavía
en un browser.

Marcá con `[x]` a medida que vayas confirmando. Si algo falla, anotá qué pasó al
lado del ítem para retomarlo.

---

## ⚠️ SQL pendiente de correr

- [x] `Database/Scripts/2026-09-10_81_DEL_Expense.sql` -- SP `DEL_Expense`.
      Confirmado corrido (el usuario probó eliminar un gasto y funcionó).
- [x] `Database/Scripts/2026-09-10_84_Fix_GET_ExpensesByBuilding_Reconciled_Bool.sql`
      -- versión final de `GET_ExpensesByBuilding` + `UPD_Expense` (reemplaza
      a los scripts `_82` y `_83`, que quedaron como pasos intermedios en el
      historial -- no hace falta correrlos, alcanza con el `_84`). Confirmado
      corrido.

---

## Selección de Edificio / Login con rol ambiguo (`/select-building`)

**Estado: verificado (2026-09-10) por el usuario, funciona correctamente.**

Reportado por el usuario probando con un usuario real: al loguearse con un
edificio/rol ambiguo (ej. se le agregó el rol "Junta" a un usuario que ya tenía
"Administrador" en el mismo edificio), la pantalla `/select-building` aparecía
pero el menú lateral y el header ya estaban cargados y navegables, como si el
edificio/rol ya estuviera confirmado -- clickear cualquier ítem del menú
navegaba usando un contexto que el usuario nunca llegó a elegir ahí. Tomó dos
vueltas: el primer fix (`9eb9b3d`) dejaba el menú oculto en `/select-building`
pero introdujo una regresión (el menú quedaba oculto para siempre después de
confirmar); el segundo fix (`fb6f1df` + `ac51954`, suscripción a
`NavigationManager.LocationChanged`) corrigió eso.

- [x] Repetir el escenario exacto: un usuario con "Administrador" ya elegido
      antes (localStorage) en un edificio, al que se le agrega el rol "Junta"
      sobre ESE MISMO edificio -- al loguearse, `/select-building` debe
      aparecer con el menú lateral OCULTO (no debe haber nada para clickear
      hasta elegir)
- [x] Elegir un edificio/rol en esa pantalla y confirmar que el menú aparece
      recién ahí, con el rol correcto
- [ ] Un usuario con un solo edificio/rol (caso normal, no ambiguo) sigue
      entrando derecho al Dashboard sin pasar por `/select-building`
- [ ] Cambiar de rol desde el dropdown del header (`OnRoleSelected`) sigue
      funcionando igual que antes (no se tocó esa lógica)

## Permisos (`/Settings/Permissions`)

- [ ] Entrar como SysAdmin, crear un permiso de prueba, confirmar que aparece en la lista
- [ ] Ese permiso nuevo se puede asignar a un rol desde `/Settings/Roles/Permissions/{RoleId}`
      (agrupado bajo el "Grupo" elegido)
- [ ] Un usuario sin rol SysAdmin (aunque sea Administrador de edificio) ve el
      mensaje "Sólo el Super Usuario..." y no la pantalla
- [ ] Crear un permiso con una `PermissionKey` ya existente muestra el error del
      `RAISERROR` (no una excepción genérica) y no duplica la fila
- [ ] Agregar el ítem de menú "Permisos" desde `/Settings/MenuItems` (todavía no existe)

## Reportes (`/reportes/ingresos-egresos` + Dashboard)

- [ ] Asignar los 4 permisos nuevos (`view_income_expense_report`,
      `view_budget_execution`, `view_delinquency`, `view_consumption_report`)
      a los roles correspondientes desde `/roles`
- [ ] `/reportes/ingresos-egresos` carga tabla, gráfico y resumen sin errores
- [ ] El filtro por Cuenta Bancaria da los mismos totales que "Resumen de
      Conciliación" en `/ConciliacionGastos`/`/ConciliacionPagos` para la misma
      cuenta/periodo
- [ ] El gráfico del Dashboard se dibuja con datos reales
- [ ] El dropdown "Este Mes/Últimos 3 Meses/Este Año" del Dashboard cambia de
      verdad la serie mostrada
- [ ] Si el menú "Ingresos y Egresos" ya apuntaba a otra URL, actualizarlo desde
      Configuración > Items de Menú

## Sesión / Autenticación

- [ ] Dejar la app sin tocar mouse/teclado 20 minutos y confirmar que la sesión
      expira sola (redirige a `/login`)
- [ ] Interactuar con la página antes de los 20 min reinicia el conteo (no expira)

## Lecturas de Agua (`/waterreadings`)

- [ ] Reabrir un período de un presupuesto YA PUBLICADO: debe mostrar el mismo
      monto guardado la primera vez, incluso después de cambiar tarifas en
      `/configwater`
- [ ] Reabrir un período TODAVÍA EN BORRADOR: debe reflejar la tarifa actual si
      cambió desde la última vez que se guardó
- [ ] Cargar un archivo de migración con la columna "Monto de Agua" completa y
      confirmar que "Mi Consumo de Agua"/reporte de Consumo muestran el monto
      real en vez de "No calculado"

## Conciliación Bancaria (`/conciliacion`, `/ConciliacionGastos`, `/ConciliacionPagos`)

- [ ] "Ignorar transacción": el modal pide tipo + motivo (obligatorio)
- [ ] La transacción ignorada sale de "No Conciliados" y aparece en la pestaña
      "Ignorados" con su motivo/tipo visibles
- [ ] Sobrevive a un F5 (no vuelve a aparecer como pendiente)
- [ ] La tarjeta "Última Conciliación" aparece vacía la primera vez que se usa
      una cuenta bancaria nueva (antes de que exista ninguna sesión guardada)
- [ ] Después de "Finalizar Conciliación", la tarjeta trae esa misma sesión
      recién guardada
- [ ] Al crear un gasto desde una transacción, el mensaje dice "Propuesta...
      Usa 'Enviar a Conciliar'" (ya NO dice "conciliado exitosamente")
- [ ] "Finalizar Conciliación" muestra el modal de confirmación con el texto
      correcto desde el primer render (no vacío ni con texto de otra acción)
- [ ] Guardar una plantilla desde un gasto real, y confirmar que una transacción
      posterior con descripción parecida la aplica sola al abrir el modal
- [ ] Crear gasto desde una transacción, tocar "Cancelar" (no confirmar): NO debe
      quedar ningún gasto huérfano en BD
- [ ] Crear Gasto → Finalizar Conciliación sigue dejando todo bien (un solo
      gasto, transacción conciliada)
- [ ] Crear Gasto (o matchear uno existente) → Finalizar Conciliación → F5 (o
      cambiar de período y volver): la transacción queda en
      "Conciliados"/"Auto"/"Manual", NO en "No Conciliadas"
- [ ] Desde `/ConciliacionGastos`, con un pago 1:1 pendiente de propuesta
      automática de fondo: confirmar Gastos y verificar que el pago sigue
      como propuesta pendiente al entrar después a `/ConciliacionPagos`
      (no se conciliaron solos)
- [ ] Cargar un Excel de Estado de Cuenta con alguna fila inválida (fecha
      futura, moneda que no sea PEN/USD): el mensaje dice "Fila N: ..." y la
      vista previa muestra esa fila en rojo junto con el resto de filas válidas
- [ ] Botón "Marcar como Saldo Inicial" en un movimiento: actualiza el Saldo
      Inicial mostrado y persiste tras recargar la página

## Gastos (`/expense`)

**Estado: verificado (2026-09-10) por el usuario -- "funciona bien".**

Reportado por el usuario: la pantalla no tenía filtros, paginación ni selector
de registros por página. Al reescribirla salió un hallazgo grande: `Classes/Expense.cs`
(usado por esta pantalla desde siempre) no coincidía con NINGUNA columna real
de `dbo.Expense` -- la clase correcta es `ViewExpense.cs` (la de Conciliación).
Se migró toda la pantalla (listar/crear/editar/eliminar) a `ViewExpense`, con
varias vueltas de fixes en el SP `GET_ExpensesByBuilding` (columna inexistente,
`Reconciled` devuelto como string en vez de bit) y en `UPD_Expense` (columnas
inexistentes). Ver commits `65a773a`, `757f8c5`, `ac678c5` para el detalle.

SQL corrido: `2026-09-10_81_DEL_Expense.sql` (borrado) +
`2026-09-10_84_Fix_GET_ExpensesByBuilding_Reconciled_Bool.sql` (reemplaza a
`_82`/`_83`, alcanza con el `_84`).

- [x] Las 3 tarjetas de resumen (Total/Fijos/Proporcionales) muestran los
      totales de TODO el edificio, con ícono + color (pedido de diseño aparte)
- [x] Filtro por Mes funciona (incluye "Todos los meses")
- [x] Filtro por Categoría (raíz) trae también sus subcategorías -- fix
      aparte, comparaba contra la raíz pero el gasto siempre guarda la hoja
- [x] Columnas "Categ." (raíz) / "Sub Categ." (real) / "Estado" (Conciliado/
      No Conciliado) -- pedido de diseño aparte, ya en la tabla
- [x] El selector "Mostrar: 10/25/50/100" y la paginación andan bien
- [x] Crear un gasto nuevo funciona (con los defaults que completa el código
      para los campos que INS_Expense exige pero el formulario no expone)
- [x] Editar un gasto funciona
- [x] Eliminar un gasto funciona (confirmación + toast)
- [ ] Pendiente de confirmar puntualmente: **editar un gasto YA conciliado**
      bloquea Monto/Fecha (sólo Categoría/Descripción editables) y muestra el
      aviso -- agregado en un cambio posterior al "funciona bien" general
- [ ] Pendiente de confirmar puntualmente: **eliminar un gasto YA conciliado**
      muestra el error sin intentar el borrado (no se probó ese caso puntual
      todavía, sólo el flujo general)

## Administración del Menú (`/Settings/MenuItems`)

**Estado: verificado (2026-09-10) por el usuario -- "el menu quedo bien".**

Reorganización del menú lateral: se creó el grupo nuevo "Conciliación"
(`bi-bank`), se movieron ahí Estado de Cuenta / Conciliar Gastos / Conciliar
Pagos / Gastos (la vista combinada de Conciliación queda creada pero
`IsVisible=0`, no se activa todavía), se renumeraron los hijos de
Presupuesto, se movió Migración de Datos bajo Configuración, se corrigieron
`DisplayOrder` duplicados y 7 íconos placeholder. Scripts `_85`/`_86`
(`_86` corrigió 8 filas que fallaron en `_85` por `ItemKey NULL` cuando la
columna real es `NOT NULL`).

Aparte, se corrigió que la cabecera de la tabla de roles/permisos no
quedaba fija al hacer scroll (causa real: `.table-responsive` de Bootstrap
ya era su propio contenedor de scroll por el `overflow-x:auto`, pero sin
altura máxima nunca scrolleaba él mismo, así que el `sticky` se anclaba a
ese contenedor "fantasma" en vez de a la página). Fix: `max-height` +
`overflow-y:auto` en `.table-responsive` y `top:0` en el `th` (commit
`f184726`). Efecto secundario bueno: los botones "Guardar Todos los
Permisos"/"Resetear"/"Guardar Orden" quedan siempre visibles, fuera del
área con scroll.

- [x] El árbol del menú lateral se ve reorganizado correctamente (grupo
      "Conciliación" con sus 4 ítems, Migración de Datos bajo Configuración,
      íconos correctos)
- [x] La cabecera de la tabla en `/Settings/MenuItems` queda fija al
      scrollear y los botones de acción no requieren bajar toda la página
- [ ] Confirmar que la vista combinada "Conciliación" (creada con
      `IsVisible=0`) sigue oculta -- se activará más adelante cuando el
      equipo se acostumbre al flujo separado de Gastos/Pagos

## Migración de Datos (si se corre un rango de una década)

- [ ] Si "Conciliación de Pagos" con un rango de fecha amplio (ej. 2015-2026)
      sigue fallando, confirmar que el mensaje de error ahora muestra la causa
      real (ej. "Timeout expired") en vez del wrapper genérico

## Nota aparte — no es una prueba rápida

El botón de borrar edificio (`/buildings`, sólo SysAdmin) sólo se probó
conceptualmente. **Antes de confiar en él para algo más que un edificio de
prueba genuinamente vacío**, conviene confirmar contra una copia de la BD real
que las ~12 tablas con `IdBuilding` que no se pudieron revisar (`RealEstateUnit`,
`Owner`, `BankAccount`, `BudgetHeader`, `Incident`, `CalendarItem`,
`WorkflowAuditEntry`, `SystemLogEntry`, `UserBuildingRoleAssignment`, etc.)
tienen FK real -- si alguna no la tiene, borrar un edificio con filas ahí las
deja huérfanas en vez de fallar.
