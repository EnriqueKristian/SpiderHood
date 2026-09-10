# Checklist de pruebas manuales — sesión 2026-09-09/10

Todo lo de acá ya tiene el código y el SQL aplicados (ver `Docs/Pendientes-Negocio-*.md`
y los scripts `55` a `77` en `Database/Scripts/`, ya confirmados corridos). Lo que
falta es **probarlo en la app real** con datos reales -- esta sesión no tuvo acceso
a BD ni pudo levantar el proyecto (sin SDK de .NET), así que nada de esto se probó
todavía en un browser.

Marcá con `[x]` a medida que vayas confirmando. Si algo falla, anotá qué pasó al
lado del ítem para retomarlo.

---

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
