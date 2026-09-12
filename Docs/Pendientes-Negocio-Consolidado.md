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

## Orden de ataque (lista única, actualizada 2026-09-11)

Los números remiten al detalle de cada punto más abajo en este mismo
documento. Un solo orden, pensado para el objetivo actual (lanzar el piloto
Web Administrador + Mobile Residente/Junta, con WhatsApp ya decidido como
prioridad del módulo de Comunicados) -- no es sólo "Alta/Media/Baja" a
secas, es la secuencia en la que conviene tocarlos.

**Grupo 1 -- lo que toca antes/durante el lanzamiento del piloto:**
1. **#3** Tolerancia de redondeo en conciliación (< S/ 0.05) -- si el
   edificio piloto tiene cuotas migradas, hoy se ven "Parcial" sin serlo.
2. **#1** Unidades sin propietario no facturan a la inmobiliaria -- si el
   edificio piloto tiene unidades sin vender.
3. **#17** Comunicados vía WhatsApp -- **IMPLEMENTADO** (2026-09-11):
   3 alcances (Público/Reservado/Privado) + 4 categorías, pantalla admin y
   de residente construidas y compilando. Falta probar contra BD real,
   asignar el permiso `create_announcements` vía `/Settings/Roles`, y
   arrancar la verificación de negocio en Meta + aprobación de las 4
   plantillas (no es instantáneo -- mientras tanto todo sale como texto
   libre, que puede fallar fuera de la ventana de 24hs).
4. **#22** Piloto Móvil -- wrapper PWA/TWA + sumar alcance de Junta
   (solo lectura: presupuesto, incidencias, calendario).
5. **#18** Storage de archivos -- **18b (Recibos PDF) y 18a (fotos/video en
   Incidencias) ambos implementados (2026-09-11)**. Falta correr los dos
   scripts (`Database/Scripts/2026-09-11_95_ReceiptFile.sql` y
   `_96_IncidentAttachment.sql`) y probar los dos flujos con datos reales
   -- ver el detalle de cada uno en el punto 18.
6. **#25** Email -- generar la Contraseña de Aplicación de Gmail y cargarla
   en `Email:SmtpPassword` (sin eso, ningún correo sale de verdad hoy,
   aunque ningún flujo se rompe por eso -- son todos "best effort"). Probar
   desde `/Settings/TestNotificaciones` (nuevo). Es lo que destraba
   notificaciones/links de confirmación/invitaciones para todo lo demás.

**Grupo 2 -- importante, no bloquea el lanzamiento:**
6. **#2** Reportes financieros suman transacciones Ignoradas.
7. **#6** Bug compartido en modales de confirmación (`ConfirmationUtil`) --
   **resuelto (2026-09-11)**, ver detalle del punto 6.
8. **#11** Falta el ítem de menú "Permisos" -- 5 minutos de configuración.
9. **#20** Reportes de Incidencias -- mismo patrón que los otros 4 reportes.
10. **#4** Borrado de edificio: FKs sin confirmar -- sólo urge si se va a
    usar el botón sobre algo más que un edificio de prueba vacío.
11. **#9** `GET_UnitsByType` no tolera unidades sin grupo.
12. **#5** Soporte real de multimoneda -- no urge si el piloto es una sola
    moneda.
13. **#7** Garantía de reserva de área común.
14. **#8** Historial de propietarios por periodo.
15. **#10** Estado de Cuenta migrado no crea Gastos categorizados.
16. **#19** Login social Google/Facebook/Apple -- no crítico si el alta de
    usuarios en el piloto sigue siendo manual/por Administrador.
17. **#24** Configuración de Edificio: página propia con Tabs -- **HECHO**
    (2026-09-11), falta probar en vivo y conversar con el usuario qué
    campos le faltan agregar (ahora es más fácil, hay una página por tab).

**Grupo 3 -- baja urgencia, manual, o investigación sin bloqueo real:**
17. **#16** Verificar URL del menú "Ingresos y Egresos".
18. **#13** Causa raíz del timeout en Conciliación de Pagos.
19. **#14** Confirmar upsert de `ServiceReadingDetail`.
20. **#15** Borrar un permiso (fuera de alcance).
21. **#12** Caso sin match en el Excel de Nova Alzamora (manual).
22. **#21** Módulo de Reservas y Gobernanza (Reuniones, Votación, Actas,
    Encuestas) -- rediseñado 2026-09-11. **Reservas -- IMPLEMENTADO
    (2026-09-11)**: Áreas Comunes (nueva pestaña en BuildingConfig), estado
    completo (Pendiente -> Aprobada/Rechazada -> Cancelada/NoPresentado ->
    Entregada -> Finalizada -> Cerrada), aprobación por la Junta integrada
    al badge de Aprobaciones, check-in/check-out con checklist + fotos por
    el Administrador, liquidación de garantía (devuelta/retenida/cuota
    extraordinaria vía `IExtraChargeService` si el daño la supera) y
    páginas `/reservas` (residente) + `/reservas-admin` (administrador) --
    `dotnet build` en 0 errores, mismo baseline de warnings. Falta correr
    el script SQL contra la BD real, asignar `approve_reservations` /
    `manage_reservations` vía `/Settings/Roles`, y probar el flujo
    completo con datos reales. **Gobernanza (Reuniones/Votación/Actas/
    Encuestas) sigue sin construir** -- es el siguiente módulo de la cola,
    con la arquitectura ya definida (ver detalle abajo).

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
*(Conciliación #4 -- **resuelto (2026-09-11)**, reportado de nuevo por el
usuario probando `BudgetGenerator` sin lectura de agua completa)*

El mismo bug de orden (`Show(type)` antes de fijar `Message`) que ya se
había corregido en `ReconciliationWorkspace.ConfirmarAsync` seguía vivo en
el helper compartido `Classes/Utilities.cs` (línea 39-41 en su momento),
usado por `ModalOwnerUnit.razor`, `BudgetGenerator.razor`,
`ServiceReadingModal.razor` y `ManualInstallmentConciliation.razor`. El
usuario lo encontró en producción: al publicar un presupuesto sin lectura
de agua, el modal de confirmación no mostraba con claridad la advertencia
real y dejaba avanzar sin que quedara claro qué se estaba confirmando --
exactamente el síntoma que este punto anticipaba. **Corregido** invirtiendo
el orden (`Message`/`IsCancelOnly` antes de `Show()`) en el único lugar
compartido -- arregla los 4 usos a la vez.

**Actualización (2026-09-11):** al preguntarle al usuario si prefería
mantener "advertencia con confirmación" (lo que este fix ya dejaba
funcionando bien) o pasar a bloqueo total, eligió **bloqueo total** -- ver
punto 6b más abajo. Con eso, el fix de este punto (orden `Show()`/`Message`)
sigue siendo válido y necesario para los OTROS 3 usos compartidos
(`ModalOwnerUnit.razor`, `ServiceReadingModal.razor`,
`ManualInstallmentConciliation.razor`, que siguen usando confirmación con
advertencia blanda), pero en `BudgetGenerator` específicamente la lectura
de agua ya ni siquiera llega a mostrar ese modal -- corta antes, ver 6b.

**Sin verificar en un browser real** (sin acceso a BD en este entorno):
confirmar que en las pantallas que SÍ siguen usando el modal de
confirmación compartido (`ModalOwnerUnit`, `ServiceReadingModal`,
`ManualInstallmentConciliation`), la primera confirmación de la sesión
muestra el mensaje real de entrada, no uno genérico o vacío.

### 6b. Lectura de agua incompleta: de advertencia a bloqueo total
*(Nuevo 2026-09-11, decisión del usuario -- **implementado**)*

Al reportar el bug de arriba, el usuario aclaró la regla de negocio real:
"se supone que es bloqueante, sin lectura no avanza el presupuesto" -- pero
el código (desde una sesión anterior) lo trataba como advertencia blanda
que el Administrador podía aceptar y continuar. Confirmado explícitamente
con el usuario: quiere **bloqueo total**, no advertencia.

**Cambio en `ValidarPresupuestoParaAprobacion`
(`BudgetGenerator.razor`):** si el presupuesto tiene una sección de
Categoría "Agua" y la lectura está incompleta (sin cargar, con unidades
faltantes, o con consumos inválidos), ahora corta de una con un toast de
error ("Lectura de agua incompleta: ...") y `blocked = true` -- mismo
patrón que las otras validaciones duras (sin secciones, sin items, monto
total en cero). Ya no pasa por el modal de "¿Desea continuar de todos
modos?" -- no hay forma de publicar o enviar a aprobación un presupuesto
con Agua sin lectura completa, para ningún edificio.

**Verificado en este entorno:** `dotnet build` compila sin errores (0
errores, sin warnings nuevos).

**Sin verificar en un browser real** (sin acceso a BD en este entorno):
crear/editar un presupuesto con sección de Agua, dejar la lectura sin
cargar (o con alguna unidad faltante), y confirmar que "Enviar a
Aprobación"/"Publicar" muestra el toast de error y NO deja avanzar bajo
ninguna circunstancia (a diferencia de antes, que ofrecía "Continuar de
todos modos").

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

## Módulos / funcionalidad que todavía no existe (agregado 2026-09-11)

A diferencia de los puntos 1-16 (bugs/gaps encontrados trabajando en algo
que ya existía), esto es funcionalidad que **no está construida en absoluto**
-- verificado buscando en todo el repo, no por sospecha.

### 17. Comunicados / Anuncios
**Estado: IMPLEMENTADO (2026-09-11) -- `dotnet build` en 0 errores, mismo
baseline de warnings (139). Falta probar contra una BD real (correr
`Database/Scripts/2026-09-11_97_Comunicado.sql`) y asignar el permiso
`create_announcements` a Administrador/Junta desde `/Settings/Roles`.**

**Qué se construyó:**
- `Database/Scripts/2026-09-11_97_Comunicado.sql` -- tablas `Comunicado`
  (cabecera) + `ComunicadoDestinatario` (detalle, a quién le llegó y con
  qué resultado por canal), SPs `INS_Comunicado`/`GET_ComunicadosByBuilding`/
  `INS_ComunicadoDestinatario`/`GET_ComunicadoDestinatariosByComunicado`/
  `GET_ComunicadosParaUsuario`, permisos `view_announcements`/
  `create_announcements` (idempotentes), grupo de Parameter "Categoría de
  Comunicado" con las 4 categorías (Mantenimiento Programado, Corte de
  Servicio, Convocatoria de Reunión, Aviso General -- NO idempotente ese
  bloque, no re-correr), y el ítem de menú admin "Comunicados" (`/comunicados`).
- `Classes/Communication/Comunicado.cs` -- `Comunicado`, `ComunicadoDestinatario`,
  enums `AlcanceComunicado`/`EstadoEnvioWhatsApp`/`EstadoEnvioCorreo`,
  `PublicarComunicadoResultado`.
- `Services/IComunicadoService.cs` -- `PublicarComunicadoAsync` resuelve
  destinatarios según Alcance (Público/Privado contra `OwnerUnitView`
  filtrado `Role==1 && TypeUnit==1`, mismo criterio que
  `IExtraChargeService.GetUnidadesAsync`; Reservado contra
  `UserBuildingAssociation` por rol de portal, con lookup de teléfono por
  usuario -- aceptable porque la audiencia de un Reservado suele ser
  chica), crea cabecera+destinatarios, manda por WhatsApp siempre (texto
  libre vía `SendMessageAsync` -- **todavía no usa plantillas de Meta
  porque no existe ninguna aprobada**, cuando exista cambiar a
  `SendTemplateMessageAsync`) y por correo sólo si se marcó el check,
  "best effort" (un fallo de un canal no tumba el otro).
- `IWhatsAppService` ganó una propiedad nueva `IsSimulate` -- sin esto, el
  Comunicado no podía distinguir "Enviado" de "Simulado" en su registro de
  entrega (el `SendMessageAsync` existente devuelve `true` en ambos casos).
- `Components/Pages/CommunicationPages/Comunicados.razor` (admin: listado
  paginado+buscable con `ComunicadoPagination`, modal "Nuevo Comunicado"
  con selector de Categoría/Alcance/Rol/Unidades según corresponda, y
  `ConfirmationModal`/`ConfirmationUtil` antes de publicar -- nunca un
  `alert()`/`confirm()` de JS) y
  `Components/Pages/ResidentPages/MyAnnouncements.razor` (residente: lista
  de comunicados visibles según su rol/unidad -- esta ruta y su permiso
  `view_announcements` ya estaban seedeados sin nada detrás, ahora sí
  apuntan a una pantalla real).
- Gateo de acceso: `PermissionService.HasPermissionAsync(user,
  "create_announcements")` en la pantalla admin -- falta que alguien
  asigne ese permiso a Administrador/Junta desde `/Settings/Roles` (no se
  hace por script, mismo criterio que el resto de permisos de este repo).

**Pendiente:** probar contra una BD real (crear un Comunicado de cada
Alcance, confirmar que `GET_ComunicadosParaUsuario` filtra bien por rol/
unidad, confirmar que el envío real de WhatsApp -- no sólo Simulado --
funciona una vez que existan credenciales de Twilio activas). Las
plantillas de Meta siguen sin existir -- mientras tanto todo comunicado
sale como texto libre de WhatsApp, lo cual puede fallar fuera de la
ventana de 24hs de conversación (ver el error "ContentSid Required" que
ya se vio esta sesión).

**Qué se hizo:** `Services/IWhatsAppService.cs` (patrón calcado de
`IEmailService`/`IPaymentService`) -- `SendMessageAsync` (texto libre, sirve
para probar contra el sandbox de Twilio) y `SendTemplateMessageAsync`
(Content Template de Twilio, para cuando exista una plantilla real aprobada
por Meta -- hoy no hay ninguna). Incluye `NormalizeToE164` porque
`PhoneNumber` en `Classes/User.cs` es texto libre. Mismo modo `Simulate` que
`IPaymentService`/MercadoPago: sin `Twilio:AccountSid`/`AuthToken`
configurados (`appsettings.json`, vacíos a propósito) no llama a Twilio de
verdad, sólo loguea -- así se puede seguir construyendo/probando el resto
sin esperar la cuenta de Twilio ni la verificación de negocio en Meta.
Paquete `Twilio` 8.0.1 agregado al `.csproj`, registrado en `Program.cs`.

**Verificado (2026-09-11):** se instaló el SDK de .NET 10 en este entorno
(no venía instalado) y `dotnet build` compila sin errores (0 errores, mismos
139 warnings preexistentes, ninguno nuevo). Se probó además con un programa
de prueba aparte (fuera del repo) instanciando `WhatsAppService` sin
credenciales de Twilio: `SendMessageAsync` corre en modo simulado como se
esperaba (loguea y devuelve `true`, sin llamar a Twilio) y rechaza
correctamente un número inválido (`false` + warning). La prueba encontró un
caso real de `NormalizeToE164` mal manejado -- números con prefijo troncal
"0" (fijos de Lima, ej. "01-4567890") quedaban con un "0" de más después del
código de país -- ya corregido (se descarta el prefijo troncal antes de
anteponer el código de país). Sigue sin probarse contra una cuenta de
Twilio real (no existe todavía).

**Decisión tomada (2026-09-11): el canal prioritario es WhatsApp, no un
tablón dentro de la app.**

Hay un permiso `view_announcements` y un ítem de menú "Comunicados"
(`MyAnnouncements`, agregado en `Database/Scripts/2026-09-10_85_Reorganizar_Menu.sql`)
pero **no hay ningún componente `.razor`, servicio ni tabla detrás** -- mismo
patrón que se encontró con "Ingresos y Egresos" antes de implementarlo
(item de menú apuntando a nada).

**Por qué WhatsApp primero, antes que un tablón dentro de la app:** un
comunicado que sólo vive en un tablón dentro de SpiderHood depende de que el
residente entre a la app para verlo -- justo el problema que este módulo
busca resolver (que la gente se entere). WhatsApp es el canal que la
mayoría de residentes ya revisa a diario, sin fricción de login.
`Classes/User.cs` ya tiene `PhoneNumber` en `User`/`Owner`/`Contact` -- el
dato de contacto ya existe, falta el canal de envío.

**Qué se necesita para integrarlo (verificado: hoy no hay ninguna
integración de mensajería en el repo, sólo `IEmailService` como patrón a
imitar -- SMTP puro, sin plantillas ni proveedor externo):**

1. **Decisión de proveedor** -- WhatsApp no se integra directo con Meta
   sin pasar por un Business Solution Provider (BSP) o una API intermedia:
   - **WhatsApp Cloud API (Meta, directo)**: gratis por mensaje dentro de
     ciertos límites, pero requiere Meta Business Manager verificado,
     configurar el número, webhooks propios -- más control, más trabajo de
     integración.
   - **Twilio / similar (BSP)**: más rápido de integrar (SDK/HTTP simple,
     ya tienen SDK .NET), pero con costo por mensaje/conversación adicional
     al de Meta -- recomendado para un piloto por velocidad de arranque.
2. **Verificación de negocio en Meta** -- el número de WhatsApp Business
   necesita el nombre del negocio (SpiderHood o la administradora del
   edificio, a decidir) verificado ante Meta -- este paso no es instantáneo,
   conviene arrancarlo ya si se prioriza este canal.
3. **Plantillas de mensaje pre-aprobadas** -- WhatsApp Business API **no
   permite mandar texto libre** para mensajes iniciados por el negocio (un
   comunicado es exactamente eso): hay que dar de alta plantillas
   (`message templates`) en Meta, con variables (ej. "Se cortará el agua el
   {{fecha}} de {{hora}} a {{hora}}"), y esperan aprobación de Meta antes de
   poder usarse. Sólo dentro de una ventana de 24h después de que el
   residente escribe primero se puede mandar texto libre -- no aplica para
   comunicados masivos que el edificio inicia.
4. **Opt-in explícito** -- Meta exige que el usuario haya dado consentimiento
   para recibir mensajes de ese negocio (no alcanza con tener el teléfono
   cargado en el sistema) -- hay que sumar un check/aceptación en el alta o
   configuración del residente, y guardar cuándo lo aceptó.
5. **Formato del número** -- `PhoneNumber` hoy es texto libre; WhatsApp
   requiere formato E.164 (código de país + número, sin espacios/guiones) --
   falta validar/normalizar los números ya cargados antes de poder usarlos.
6. **Costo por conversación** -- Meta cobra por conversación de 24h iniciada
   (varía por país), no por mensaje individual dentro de esa ventana --
   relevante para estimar costo de un comunicado a todo un edificio.
7. **Servicio nuevo** (`IWhatsAppService` o similar, mismo patrón que
   `IEmailService`): encapsula la llamada al proveedor elegido, resuelve
   plantilla + variables, y registra éxito/fallo por destinatario (para
   saber a quién no le llegó).

**Diseño de negocio -- cerrado con el usuario el 2026-09-11. Módulo
chico a propósito: "empecemos con lo básico, y si tiene funcionalidad lo
vamos desarrollando" (piloto real decide qué tanto crece).**

*Alcance (v1) -- 3 tipos, sin mensajería vecino-a-vecino (se descartó
explícitamente: "conllevaría a un chat y no es el objetivo") ni "Público
Global" de SysAdmin a todos los edificios (es una feature de plataforma,
distinta en naturaleza a un comunicado de condominio -- se deja fuera de
este módulo, no está descartada, solo no es v1):*
- **Público:** Administrador o Junta -> todos los residentes del edificio.
- **Reservado:** Administrador o Junta -> un rol específico dentro del
  edificio (ej. solo Junta).
- **Privado:** Administrador o Junta -> una unidad o grupo de unidades
  puntual (NO vecino a vecino).

*Estructura del Comunicado:*
- Título + Cuerpo (lo que se ve en la app).
- **Categoría** -- define qué plantilla de WhatsApp usa (ver plantillas
  abajo).
- Alcance (uno de los 3 de arriba) + destinatario exacto (rol, o unidad/
  grupo de unidades, según corresponda).
- Quién publicó y cuándo (auditoría).
- Checkbox **"Enviar también por correo"** al momento de publicar --
  WhatsApp se manda automático si el edificio lo tiene configurado (si no,
  corre en modo Simulado, igual que hoy); el correo es una decisión
  explícita de quien publica, no automático.
- **Siempre queda visible en la app** según el alcance -- WhatsApp/correo
  son el aviso, la app es el registro/historial (resuelve lo que antes
  era una pregunta abierta: no vive solo en WhatsApp).
- Por destinatario se guarda si le llegó por WhatsApp (Enviado/Simulado/
  Falló) y por correo (si se marcó el check) -- para saber a quién no le
  llegó.

*Categorías/Plantillas -- arrancamos en cero, plantillas propias
(4 categorías iniciales, ampliable según lo que el piloto pida):*
1. **Mantenimiento Programado** -- ej. "Se realizará mantenimiento de
   {{área/equipo}} el {{fecha}} de {{hora inicio}} a {{hora fin}}.
   {{recomendación}}" (caso de referencia: aviso de mantenimiento de
   ascensor).
2. **Corte de Servicio** (agua/luz/gas) -- misma estructura que
   Mantenimiento, distinto rubro.
3. **Convocatoria de Reunión/Asamblea** -- conecta directo con el módulo
   de Gobernanza (item #21): cuando ese módulo exista, una Convocatoria
   podría disparar un Comunicado de esta categoría automáticamente.
4. **Aviso General** -- la más libre, para lo que no encaja en las otras
   3.

*Sigue bloqueando el envío REAL por WhatsApp (no el resto del módulo, que
se puede construir y probar en modo Simulado sin esperar esto):* que Meta
apruebe estas 4 plantillas -- corre en paralelo a la construcción de la
pantalla, no la frena.

### 18. Storage de archivos -- fotos/video en Incidencias Y PDFs de Recibos
**Estado: pregunta técnica -- respuesta recomendada abajo. Ampliado
2026-09-11: sumado el caso de los recibos PDF (pedido del usuario), que
termina necesitando el mismo storage pero con un problema más urgente que
Incidencias -- ver evaluación al final de este punto.**

#### 18a. Fotos/video en Incidencias

Ya estaba anotada como pregunta abierta #3 en
`Docs/Design-Piloto-Mobile-Android.md` (sección 9) al evaluar el piloto
mobile -- hoy `Incident` (`Classes/Incidents/Incident.cs`) no tiene ninguna
columna para adjuntar nada, no existe integración de storage de archivos en
ningún lado (`appsettings*.json` tampoco tiene nada de Azure Blob/S3), y
`InputFile`/`IBrowserFile` sólo se usa hoy para subir Excel.

**Recomendación: archivos en disco/storage, NO en la base de datos.**
- Guardar el archivo (foto/video) en una carpeta del servidor (o Azure Blob
  Storage/S3 si ya hay o se va a tener más de un servidor/escala horizontal)
  y en `Incident`/una tabla nueva `IncidentAttachment` guardar sólo la
  **ruta o URL** + metadatos (nombre original, tipo, tamaño, quién subió).
- Por qué no en la BD (`varbinary(max)`): infla el tamaño y los backups de
  la base de datos (un video de 30-60 seg puede pesar varios MB, fotos de
  celular actuales 3-8 MB cada una -- esto crece rápido con uso real),
  degrada el rendimiento de cualquier `SELECT *`/backup/restore sobre esas
  tablas, y no se puede servir directo por URL/CDN -- cada vista tendría que
  pasar por la app para bajar el blob. Guardar solo la ruta es el patrón
  estándar (y consistente con lo que ya se decidió para Excel: se procesan
  en memoria, no se guarda el archivo original en BD).
- Para el piloto (según `Design-Piloto-Mobile-Android.md`, Fase 2): alcanza
  con una carpeta local del servidor (simple, sin costo de servicio externo)
  si el volumen esperado es bajo; migrar a Blob Storage (Azure, dado que el
  resto del stack es .NET) cuando el piloto escale o si el hosting cambia a
  algo sin disco persistente entre despliegues (contenedores efímeros, por
  ejemplo) -- ahí un disco local se pierde en cada deploy.
- Falta igual: límite de tamaño/tipo de archivo (validar extensión real, no
  sólo el nombre), y si video entra al piloto o sólo fotos (video pesa
  bastante más y complica más el storage/streaming).

**Estructura de carpetas:** una carpeta por INCIDENTE, adentro de la
carpeta del edificio -- `incidents/{IdBuilding}/{IdIncident}/{IdAttachment}.{ext}`
-- ya que un mismo incidente puede tener varias fotos/videos y conviene que
viajen juntos. A diferencia de Recibos, acá no hace falta año/mes como
nivel aparte: la cantidad de incidentes por edificio es mucho menor que la
de cuotas mensuales por unidad, así que una carpeta por incidente ya
alcanza para que sea manejable a simple vista.

**Estado: implementado (2026-09-11).** Reusa el mismo `IFileStorageService`
armado para Recibos (18b) -- no hizo falta tocar el servicio de storage en
sí.

- `Database/Scripts/2026-09-11_96_IncidentAttachment.sql` -- tabla nueva
  `dbo.IncidentAttachment` (100% nueva, no toca nada existente) +
  `INS_IncidentAttachment`/`GET_IncidentAttachmentsByIncident`. Sin
  `UPD`/`DEL` -- mismo criterio de inmutabilidad que `ReceiptFile` (18b):
  un adjunto no se edita, si hace falta sacarlo es un punto aparte
  (borrado/reemplazo de adjuntos no estaba pedido).
- `IIncidentService.UploadAttachmentAsync` -- valida **whitelist de
  extensiones** (`.jpg .jpeg .png .webp .heic .mp4 .mov` -- cualquier otra
  se rechaza, no es una blacklist) y **tamaño máximo configurable**
  (`IncidentAttachments:MaxSizeBytes` en `appsettings.json`, default 15 MB
  -- cubre una foto de celular actual con margen, o un video corto). Guarda
  el archivo con `{IdAttachment}.{ext}` como nombre en disco (nunca el
  nombre original tal cual, evita colisiones entre dos fotos con el mismo
  nombre de cámara subidas por personas distintas) y recién después
  registra la fila en BD.
- UI en `IncidentDetail.razor`: sección "Fotos / Videos" con miniaturas
  (imagen/video inline, ícono genérico + link de descarga para otros
  tipos), quién subió cada una y cuándo, más un `InputFile` para agregar
  nuevas -- disponible para cualquiera que pueda ver el incidente (mismo
  criterio de permisos que ya tenían los comentarios, sin gate adicional).
- **Corrección de UX (2026-09-11), reportada por el usuario probándolo:**
  la primera versión sólo dejaba adjuntar desde el detalle del incidente ya
  creado -- no había forma de subir una foto al REPORTARLO. Se agregó el
  mismo `InputFile` (con selección múltiple) al modal "Nuevo Incidente"
  (`IncidentList.razor`): los archivos elegidos se leen y quedan en memoria
  (`StagedAttachment`), y recién se suben después de que
  `IncidentService.ReportAsync` confirma que el incidente ya existe en BD
  -- así, si el usuario cierra el modal antes de completar "Reportar", no
  queda ningún archivo huérfano en storage. Si algún adjunto falla la
  validación real al subirlo, el incidente igual queda creado (ya se
  confirmó) y se redirige al detalle para reintentar desde ahí. **Aplica
  directo al diseño del piloto móvil** -- ver
  `Docs/Design-Piloto-Mobile-Android.md`, Fase 2, actualizada con el mismo
  criterio (adjuntar como parte del formulario de reporte, no como paso
  aparte después).

**Dos bugs reales encontrados por el usuario probándolo (2026-09-11),
ambos corregidos:**

1. **"Se ve otra imagen en el preview" -- bug de fondo, no de storage.**
   El archivo en disco era el correcto (confirmado por el propio usuario
   mirando la carpeta) -- lo que fallaba era la pantalla. `IncidentDetail.razor`
   sólo cargaba datos en `OnInitializedAsync`, que en Blazor corre **una
   sola vez por instancia del componente**. Si el usuario entraba al
   detalle de un incidente, volvía a la lista (`Navigation.NavigateTo`, sin
   `forceLoad`), y entraba a OTRO incidente, Blazor reutilizaba la misma
   instancia (misma ruta `/incidents/{Id}`) y nunca recargaba -- la pantalla
   se quedaba mostrando título/comentarios/fotos del incidente ANTERIOR
   aunque la URL ya tuviera el Id correcto. Corregido agregando
   `OnParametersSetAsync` que recarga cuando `Id` cambia respecto del
   último cargado. **Este mismo patrón (sólo `OnInitializedAsync`, sin
   `OnParametersSetAsync`) puede repetirse en cualquier otra pantalla con
   parámetro de ruta** -- no se auditó el resto del proyecto todavía, queda
   como sospecha a revisar si aparece un síntoma parecido en otra pantalla
   (ver candidato nuevo en la tabla resumen).
2. **"Al hacerle clic no abre".** La miniatura navegaba la pestaña entera a
   la URL `data:image/...;base64,...` -- Chrome/Edge tienen un límite de
   tamaño para NAVEGAR a una URL `data:` así (aunque mostrarla en un
   `<img>` inline funciona sin problema), así que cualquier foto de celular
   real dejaba la pestaña nueva en blanco. Corregido con un lightbox DENTRO
   de la misma página (modal con la imagen a tamaño completo) en vez de
   navegar -- evita el límite del navegador por completo.

**Decisión de diseño consciente, no un descuido:** las miniaturas se
arman como `data:` URI (bytes en base64 incrustados en el HTML) en vez de
servirse desde un endpoint HTTP propio -- evita construir y asegurar un
endpoint autenticado aparte (fuera del circuito de Blazor Server, sin el
`UserSession` ya armado) sólo para esto. Funciona bien para el piloto con
pocos adjuntos livianos (tope de 15 MB), pero cada vista de un incidente
con adjuntos manda esos bytes por el circuito de SignalR -- si el uso real
mete muchos adjuntos pesados por incidente, o hace falta servirlos fuera
de un circuito Blazor (ej. la app móvil de la Fase B de
`Design-Piloto-Mobile-Android.md`), ahí sí conviene un endpoint HTTP
autenticado de verdad -- ese endpoint de todas formas hace falta para la
Fase B (la API que reemplaza el acceso directo a los `Services`), así que
no es trabajo perdido, sólo adelantado.

**Verificado en este entorno:** `dotnet build` compila sin errores (0
errores, mismos 139 warnings preexistentes, ninguno nuevo). La validación
de extensión/tamaño se probó aislada (fuera del repo, sin necesitar BD):
extensión permitida (jpg, mp4, insensible a mayúsculas) aceptada;
extensión no permitida (.exe) y archivo sin extensión rechazados con el
mensaje real; archivo vacío rechazado; archivo que excede el máximo
rechazado con el tamaño real en el mensaje; archivo justo en el límite
aceptado.

**Sin verificar (no hay acceso a BD real en este entorno):** correr
`2026-09-11_96_IncidentAttachment.sql`, y probar el flujo completo con
datos reales -- subir una foto real desde el detalle de un incidente,
confirmar que aparece la miniatura, que queda un archivo en
`incidents/{IdBuilding}/{IdIncident}/` y una fila en `IncidentAttachment`,
y que un archivo no permitido (ej. un `.pdf` o `.docx`) muestra el mensaje
de error sin romper la pantalla.

#### 18b. PDFs de Recibos (agregado 2026-09-11, pedido del usuario)

**Verificado en el código: los recibos tampoco se guardan en ningún lado
hoy.** `InstallmentExportService.GenerateReceipt`/`GenerateAllReceiptsZip`
(`Classes/Utilities.cs:761,784`) generan el PDF 100% en memoria con
QuestPDF, **desde cero, cada vez** que alguien lo pide -- botón "Imprimir"
en `MyReceipts.razor` (Residente), `InstallmentTable.razor`/
`InstallmentList.razor` (Administrador), o el ZIP con un PDF por cuota que
arma `BudgetGenerator.razor` al publicar un presupuesto. Nunca se persiste
el archivo generado -- mismo síntoma que Incidencias (cero capacidad de
storage en el proyecto), pero acá con un problema de fondo más grave que
sólo performance:

**Bug de integridad encontrado:** `ComposeFooter`
(`Classes/Utilities.cs:1047,1053,1062-1063`) arma el pie del recibo con la
configuración VIGENTE del edificio **en el momento en que alguien lo
descarga** -- cuenta bancaria (`BankAccounts.FirstOrDefault()`), texto del
pie (`ReceiptFooterText`), nombre/email del Administrador (`AdminContact`)
-- no con una foto de cómo era esa configuración cuando la cuota se emitió
originalmente. Si el edificio cambia de banco, de administrador, o edita el
texto del pie, **cualquier recibo viejo que se vuelva a descargar sale con
los datos NUEVOS**: un recibo de enero descargado en julio, después de un
cambio de cuenta bancaria, muestra la cuenta de julio, no la de enero. Para
un comprobante financiero que un residente puede necesitar como respaldo,
esto es un problema real de integridad/auditoría (el documento "reescribe"
su propio pasado), no sólo algo que se podría optimizar.

**Misma recomendación que 18a: guardar el PDF ya generado (bytes en
disco/Blob), no en la BD** -- acá con un motivo extra para no usar la BD:
el volumen es más previsible (un PDF por cuota por periodo, no fotos de
tamaño variable) pero el ritmo de generación es mucho más alto (TODOS los
residentes de TODOS los edificios, todos los meses).

**Estado: implementado (2026-09-11).** Se construyó el storage compartido y
se conectó primero acá (siguiendo la recomendación de la evaluación de más
abajo) -- fix del bug de integridad incluido, no sólo la optimización.

- `Services/IFileStorageService.cs` (`IFileStorageService`/
  `LocalFileStorageService`, `AddSingleton`) -- storage genérico a disco,
  fuera de `wwwroot` a propósito (`Storage:LocalBasePath` en
  `appsettings.json`, vacío = usa `App_Data/storage` bajo la carpeta de
  publicación). `SaveAsync`/`ReadAsync` por categoría + nombre de archivo,
  con sanitización de ambos segmentos y verificación de que la ruta
  resuelta sigue adentro del directorio base (sin esto, un `relativePath`
  armado con `../..` podría leer cualquier archivo del servidor). Pensado
  para servir también a 18a (Incidencias) cuando se conecte.
- `Database/Scripts/2026-09-11_95_ReceiptFile.sql` -- tabla nueva
  `dbo.ReceiptFile` (100% nueva, no toca nada existente) con
  `IX_ReceiptFile_Installment` **UNIQUE** (un recibo por cuota, para
  siempre) + `INS_ReceiptFile`/`GET_ReceiptFileByInstallment`. A propósito
  no hay `UPD_ReceiptFile`: un recibo ya generado nunca se vuelve a generar
  con datos distintos, sólo se sirve el archivo guardado.
- `Services/IReceiptStorageService.cs` -- capa de persistencia sobre
  `InstallmentExportService` (que sigue siendo sólo el renderizador PDF, sin
  cambios): `GetOrGenerateReceiptAsync` busca un `ReceiptFile` existente
  para la cuota, sirve ese archivo si está; si no existe (o el archivo se
  perdió de storage), genera, guarda, y registra. Maneja el caso de dos
  generaciones concurrentes de la misma cuota (`IX_ReceiptFile_Installment`
  rechaza el segundo INSERT -- se sirve la del que ganó la carrera en vez de
  fallar o dejar un archivo huérfano). `GetOrGenerateAllReceiptsZipAsync`
  hace lo mismo por cada cuota de un lote, para el ZIP.
- Conectado en los 4 lugares que generaban recibos:
  `MyReceipts.razor` (Residente), `InstallmentTable.razor`/
  `InstallmentList.razor` (Administrador), y `BudgetGenerator.razor`
  (`PublicarPresupuesto` -- reemplaza `GenerateAllReceiptsZip()`, así que
  publicar el presupuesto pasa a ser el momento en que TODOS los recibos
  del periodo quedan guardados de una vez, no sólo el primero que alguien
  pida).

**Actualización (2026-09-11, pedido del usuario probándolo):** con datos
reales confirmó que el flujo completo funciona (filas en `ReceiptFile` +
archivo en disco), pero notó que TODOS los recibos de TODOS los edificios
caían en una sola carpeta plana (`receipts/<IdInstallment>.pdf`) --
inmanejable a mediano plazo. Primera vuelta: una carpeta por edificio con
Periodo+Unidad en el nombre del archivo. El usuario pidió ir más allá --
carpeta por Unidad, y adentro por Año/Mes -- y compartió cómo ya organizan
los recibos hoy A MANO en Google Drive: `Edificio > DPTO > Año > Mes`
(meses con nombre en español, ej. "ABRIL"). Aclaró también que no le
preocupa el riesgo de renumeración de unidad que había motivado la primera
versión ("dudo que un DPTO cambie de nombre, de hecho lo podemos
bloquear"). **Estructura final, calcada de ese orden con un ajuste:**

```
receipts/{IdBuilding}/{UnitName}/{Año}/{MM-NombreMes}/{IdInstallment}.pdf
```

Ej.: `receipts/<guid-edificio>/902/2026/04-Abril/<guid-installment>.pdf`

**El ajuste sobre el ejemplo de Drive:** ahí los meses quedan ordenados
ALFABÉTICAMENTE (ABRIL, AGOSTO, ENERO, FEBRERO...) porque el nombre del mes
solo no ordena cronológicamente -- efecto secundario de usar el nombre tal
cual como carpeta. Acá se antepone el número de mes ("04-Abril", no sólo
"Abril"), así la carpeta ordena Ene→Dic en cualquier explorador de
archivos y sigue siendo legible. El nombre del mes se arma siempre con
`CultureInfo("es-PE")` explícito (no `CurrentCulture`, que depende del
locale del servidor y no está garantizado) -- probado que da "Enero",
"Abril", ..., y de paso "Setiembre" (no "Septiembre"), la forma que usa el
`es-PE` de .NET.

`SaveAsync` (`IFileStorageService`) pasó de recibir un `category` como
string con `/` a recibir `string[] categorySegments` -- cada elemento se
sanitiza como una unidad completa, así un nombre de unidad que en la
práctica trajera una "/" (ej. "Cochera 12/A") nunca crea un nivel de
carpeta de más por accidente (se probó explícitamente este caso). El
nombre del archivo queda simple (sólo el `IdInstallment`), ya que
Edificio/Unidad/Año/Mes quedan expresados en la carpeta. **No rompe los
recibos ya guardados con esquemas anteriores** (siguen en su ruta vieja,
registrada tal cual en su fila de `ReceiptFile` -- son inmutables, nunca se
mueven ni se regeneran).

**Decidido (2026-09-11):** el nivel de Edificio queda como `IdBuilding`
(Guid), no el nombre. El usuario confirmó: para la aplicación es
irrelevante que el Administrador no pueda "reconocer" la carpeta a simple
vista en el explorador de archivos del servidor -- nadie navega ese disco
a mano en el uso normal, todo pasa por la app -- y prefiere no meterle
esfuerzo a un esquema más elaborado (ej. un slug legible + sufijo
aleatorio) sólo para ganar legibilidad ahí. Cierra el punto -- no queda
pendiente.

**Verificado en este entorno:** se instaló el SDK de .NET 10 (ver
Docs/Pendientes-Negocio-Consolidado.md #17) y `dotnet build` compila sin
errores (0 errores, mismos 139 warnings preexistentes, ninguno nuevo). Se
probó además `LocalFileStorageService` con programas aparte (fuera del
repo): guardar/leer funciona, un archivo inexistente devuelve `null` sin
tirar excepción, dos variantes de path traversal quedaron bloqueadas, dos
edificios distintos quedan en carpetas separadas, y una unidad con "/" en
el nombre queda sanitizada a un solo nivel de carpeta (no se parte en dos).

**Sin verificar (no hay acceso a BD real en este entorno):** correr
`2026-09-11_95_ReceiptFile.sql`, y probar el flujo completo con datos
reales -- generar un recibo, confirmar que queda un archivo en
`App_Data/storage/receipts/` y una fila en `ReceiptFile`, volver a pedir el
mismo recibo y confirmar que sirve el archivo guardado (no vuelve a
generar), y -- el caso que motivó todo esto -- cambiar la cuenta
bancaria/pie de recibo del edificio y confirmar que un recibo YA GENERADO
antes del cambio se sigue viendo igual que antes (no adopta los datos
nuevos).

#### Evaluación -- impacto / costo / beneficio, para priorizar cuál atacar primero

| | 18a. Fotos/video en Incidencias | 18b. PDFs de Recibos |
|---|---|---|
| **Impacto de no hacerlo** | Sigue sin poder reportarse un incidente con evidencia visual -- la razón #1 (según el diagnóstico del piloto móvil) de por qué alguien abre el celular. Sin foto, el residente describe en texto y Administrador/Junta deciden a ciegas. | Cada descarga recalcula todo desde BD (presupuesto, lecturas de agua, exoneraciones, categorías, cargos adicionales) y regenera el PDF -- costo repetido para el MISMO documento. Más grave: el contenido puede cambiar retroactivamente (bug de integridad de arriba) -- nadie puede confiar en que un recibo viejo muestre lo que decía originalmente. |
| **Costo de implementarlo** | Alto: además del storage en sí, hace falta UI de carga (`InputFile`/cámara en mobile), validación de tipo/tamaño, y depende de llegar a la Fase 2 del piloto móvil (ya estaba secuenciado ahí). Funcionalidad nueva de punta a punta. | Bajo-medio: el PDF YA se genera (QuestPDF, funciona hoy) -- sólo falta guardarlo la primera vez (el momento natural es al publicar el presupuesto, `GenerateAllReceiptsZip` ya recorre todas las cuotas del periodo) y, en descargas siguientes, servir el archivo guardado en vez de regenerar. No hay UI de carga ni validación de archivos de terceros que construir. |
| **Beneficio** | Habilita un caso de uso nuevo (evidencia visual) -- valor claro, pero condicionado a que el piloto móvil llegue a Fase 2. | Corrige un bug real de integridad en un documento financiero, reduce carga repetida de BD/CPU en el flujo más usado de la app (recibos, que TODOS los residentes descargan todos los meses), y deja la base de storage lista para cuando haga falta para Incidencias. |
| **Urgencia si el piloto ya está corriendo** | Baja si la Fase 1 del piloto (solo lectura, sin foto) ya cubre el lanzamiento -- reportar sin foto sigue funcionando. | Más urgente de lo que parecía al anotarlo: si el piloto ya tiene residentes reales pagando cuotas y descargando recibos, el bug de integridad ya está activo hoy, en silencio. |

**Recomendación de orden:** construir el storage genérico (carpeta/Blob +
un endpoint propio que sirva el archivo validando permisos -- **nunca**
archivos estáticos servidos sin autenticación desde `wwwroot`, porque un
recibo tiene datos personales/financieros y una foto de incidente puede ser
sensible, así que la ruta no debe alcanzar para descargarlo, hace falta
verificar que quien pide el archivo tiene derecho a verlo) una sola vez, y
conectarlo primero a **18b (Recibos)** -- más barato, corrige un bug real
que ya puede estar afectando al piloto, y toca el flujo de mayor volumen de
toda la app. Recién después conectarlo a **18a (Incidencias)**, que es más
caro y de todos modos depende de la Fase 2 del piloto móvil, que no arranca
todavía. Es la misma infraestructura para las dos -- no es trabajo
duplicado, es sólo invertir qué se conecta primero.

### 19. Alta de usuarios con Google / Facebook / Apple -- qué se necesita
**Estado: no existe -- hoy sólo hay autenticación por cookie/usuario y
contraseña propios (`Program.cs:55`, `AddAuthentication(CookieAuthenticationDefaults...)`,
sin ningún paquete ni configuración de proveedor externo).**

Lo que hace falta, en orden:
1. **Decisión de producto:** ¿reemplaza o se suma al login actual
   (usuario/contraseña)? ¿para qué rol (Residente probablemente sí,
   Administrador/SysAdmin probablemente mejor que sigan con
   usuario/contraseña por control)?
2. **Registro de cada proveedor** (esto lo hace el negocio, no el código):
   - Google: proyecto en Google Cloud Console, pantalla de consentimiento
     OAuth, Client ID/Secret.
   - Facebook: app en Meta for Developers, revisión de la app si se piden
     permisos más allá del básico (email/perfil).
   - Apple: cuenta de Apple Developer Program (pago, ~US$99/año), Service ID
     + Sign in with Apple configurado -- **importante:** si la app llega a
     publicarse en la App Store de iOS y ya ofrece Google/Facebook como
     login social, Apple **exige** (App Store Review Guideline 4.8) que
     también se ofrezca "Sign in with Apple" -- no es opcional en ese caso.
3. **Lado del código (ASP.NET Core):** agregar los paquetes de autenticación
   externa (`Microsoft.AspNetCore.Authentication.Google`/`...Facebook`, y
   para Apple no hay paquete oficial de Microsoft -- se usa un paquete de
   terceros u OpenID Connect genérico apuntando al endpoint de Apple) como
   esquemas adicionales junto al cookie actual (no hace falta sacar el
   existente).
4. **Modelo de datos:** `User` necesita poder asociarse a uno o más
   proveedores externos (tabla tipo `UserExternalLogin` con
   proveedor+id externo), y definir qué pasa si el email del proveedor
   externo ya existe como usuario local (¿se vincula automático, o pide
   confirmación?).
5. **Flujo de "primera vez"**: un usuario que entra por Google/Facebook/Apple
   por primera vez sin cuenta previa en SpiderHood -- ¿se crea solo (self
   -service) o necesita que un Administrador lo asocie antes a una unidad/
   edificio? Esto cruza con `Docs/Design-SelfService-Registro-Piloto.md`
   (no leído en detalle en esta sesión, pero es el documento donde
   probablemente ya se pensó parte de este flujo de auto-registro).

### 20. Reportes de Incidencias
**Estado: no existe -- sólo hay listado operativo, no reporte analítico.**

Existen `IncidentList.razor`/`IncidentDetail.razor` (gestión día a día,
`/incidents`), pero ningún reporte agregado -- a diferencia de
Recaudación/Morosidad/Consumo de Agua/Ingresos y Egresos
(`Components/Pages/ReportPages/`), no hay una vista de, por ejemplo,
incidentes por tipo/prioridad/estado, tiempo promedio de resolución, o
incidentes abiertos por unidad/edificio en un rango de fechas. Mismo patrón
que ya se usó para los otros 4 reportes (selector de rango + tarjetas de
resumen + tabla + export a Excel) se podría reutilizar acá.

### 21. Módulo de Reservas y Gobernanza (Reuniones, Votación, Actas, Encuestas)
*(Rediseñado 2026-09-11 a partir del análisis de mercado -- sección 13 --
compartido por el usuario. **"Citas" descartado a pedido explícito del
usuario**: "el tema de cita como está planteado aquí, no suma" -- no forma
parte del alcance de este item.)*

**Estado: Reservas IMPLEMENTADO (2026-09-11); Gobernanza (Reuniones/
Votación/Actas/Encuestas) sigue sin construir** -- `CalendarItem`/
`CalendarPage.razor` sigue siendo el único calendario genérico de eventos,
sin ningún concepto de convocatoria, quorum, agenda, acta o votación.

**Son dos mecanismos distintos, no tres módulos sueltos ni uno solo:**
uno de **agenda** (Reservas -- quién usa qué recurso físico, cuándo) y uno
de **gobernanza** (Reuniones + Votación + Actas, que no son 3 pantallas
separadas sino 3 momentos de un mismo flujo legal, con Encuestas como su
versión ligera sin peso legal). Diseñarlos así desde el inicio evita
terminar con un formulario de votación que no se conecta con el acta.

**Reservas -- agenda de un recurso físico compartido. Diseño cerrado con
el usuario el 2026-09-11, IMPLEMENTADO el mismo día.** Lo construido:
- `Database/Scripts/2026-09-11_98_Reserva.sql`: tablas `AreaComun`,
  `Reserva`, `ReservaChecklistItem`, `ReservaAttachment`,
  `IngresoComunidad`; SPs de INS/UPD/GET para cada una (incluye
  `GET_ReservasConflicto` para el chequeo de solapamiento y
  `GET_ReservasProximasByAreaComun`); permisos `approve_reservations`
  (Junta) y `manage_reservations` (Administrador); ítems de menú standalone
  `/reservas` y `/reservas-admin`.
- `Services/IAreaComunService.cs` (CRUD) e `Services/IReservaService.cs`
  (el grueso de la lógica: `SolicitarAsync` valida ventanas de
  anticipación/duración/tope y chequea conflicto antes de guardar;
  `AprobarAsync`/`RechazarAsync`; `CancelarAsync`/`MarcarNoPresentadoAsync`
  aplican la penalidad configurada; `HacerCheckInAsync`/
  `HacerCheckOutAsync` guardan checklist + fotos vía `IFileStorageService`;
  `CerrarAsync` liquida la garantía -- Alquiler/Limpieza como Ingreso real
  si la reserva se completó, la garantía retenida como Ingreso separado
  ["Reposición de Daños - Reserva" / penalidad], y genera una cuota
  extraordinaria vía `IExtraChargeService.GenerarCuotaExtraordinariaAsync`
  si el daño supera la garantía).
- UI: pestaña nueva "Áreas Comunes" en `BuildingConfig.razor` (CRUD,
  persistencia propia, no toca `BuildingConfiguration`); página de
  residente `/reservas` (solicitar + "Mis Reservas" paginado); página de
  administrador `/reservas-admin` (check-in/check-out con checklist +
  fotos, cerrar/liquidar garantía); sección "Reservas Pendientes de
  Aprobación" nueva en `Approvals.razor` para la Junta, sumando al mismo
  badge de `LeftMenu.ContarAprobacionesPendientesAsync`.
- **Una simplificación deliberada que sigue en pie (no es un olvido):**
  `IngresoComunidad` es un registro propio y simple para Alquiler/Limpieza/
  Garantía retenida -- **no** está conectado todavía al Reporte de Ingresos
  y Egresos (100% conciliación bancaria importada hoy). El propio diseño
  (ver más abajo, "Cobro") dejó esto abierto -- "se resuelve al diseñar la
  pantalla de conciliación específica de Reservas, no antes" -- así que no
  se resuelve acá.
- `dotnet build` en 0 errores, mismo baseline de 139 warnings (+9 `BL0005`
  esperados por el mismo patrón ya usado en `Approvals.razor`/
  `ExpensePage.razor` de setear `Title`/`Message`/`ConfirmText` en
  `ConfirmationModal` desde el code-behind).
- **Ronda de fixes tras probar en vivo (2026-09-11):**
  - `ExecuteStoredProcedureAsync` (usado por TODO `AddNewRecordAsync`/
    `UpdateXAsync` de la app, no sólo Reservas) pasaba un `DBNull.Value`
    "pelado" a `ExecuteSqlRawAsync`, y EF no le puede inferir un store type
    a `DBNull` -- crasheaba el circuito al guardar cualquier campo opcional
    vacío (visto en vivo guardando la primera Área Común). Ahora envuelve
    cada parámetro en un `SqlParameter` real, mismo patrón que ya usaban
    `ExecuteQuerySingleAsync`/`ExecuteQueryListAsync`.
  - `GET_ReservasConflicto` y `GET_ReservasProximasByAreaComun` hacían
    `SELECT * FROM Reserva` sin el `LEFT JOIN` a `AreaComun`/`Users` que sí
    tienen los otros 4 SPs de Reserva -- `Models.Reserva` es keyless y EF
    exige `NombreAreaComun`/`CreatedByName` en TODOS los SPs que la
    devuelven. Corregido agregando el mismo JOIN a los dos.
  - **Integración visual con el Calendario -- ya NO es una simplificación
    pendiente, se implementó:** cada `Reserva` crea un `CalendarItem`
    (Type=Event) al solicitarse, para que otro propietario vea visualmente
    que el área ya está comprometida en ese horario incluso antes de que la
    Junta apruebe (más allá del chequeo de conflicto de `SolicitarAsync`).
    Se actualiza al Aprobar y se borra si se Rechaza/Cancela/marca
    NoPresentado (libera el horario). Se inserta por `BDLayout` directo, sin
    pasar por `ICalendarService.CreateAsync`, a propósito: ese método manda
    un correo a TODOS los residentes del edificio por cada `CalendarItem`
    nuevo -- bien para un evento real, pero saldría un correo masivo por
    cada Solicitud de reserva, incluso antes de aprobarse. Agregada la
    columna `Reserva.IdCalendarItem` para el vínculo. `AprobarAsync` se
    autocura solo si encuentra una reserva sin `IdCalendarItem` (le crea uno
    recién ahí); para una reserva que YA estaba Aprobada antes de este fix
    (visto en vivo 2026-09-12, sin botón "Aprobar" para volver a dispararlo)
    se agregó un botón manual "Sincronizar con Calendario"
    (`IReservaService.AsegurarCalendarItemAsync`) en `/reservas-admin`,
    visible sólo en las filas sin `IdCalendarItem` que todavía lo necesitan.
  - Aprobar/Rechazar una reserva ahora también está disponible **inline en
    `/reservas-admin`** (no sólo en `/aprobaciones`) -- feedback del usuario
    de que, con el permiso de Junta ya asignado, no encontraba dónde
    aprobar desde la pantalla de gestión.
  - `/reservas-admin` ahora también permite al Administrador **cargar una
    reserva en nombre de una unidad** (ej. alguien llamó a pedir el salón
    para un evento externo) -- mismo `SolicitarAsync`, elige la unidad
    responsable de una lista en vez de resolverla del usuario actual (que
    no tiene DPTO si es Administrador/Junta puro).
  - Todos los modales nuevos de este módulo (y el de Comunicados) ahora
    usan `UseStaticBackdrop="true" CloseOnEscape="false"` -- feedback del
    usuario: un clic afuera del modal perdía todo lo cargado en el
    formulario. Mismo patrón ya usado en varios modales de `BuildingPages`.
- **Falta:** volver a correr el script SQL contra la base real (agregó la
  columna `IdCalendarItem` y corrigió los 2 SPs), y probar el flujo
  completo (solicitar -> aprobar -> check-in -> check-out -> cerrar) de
  punta a punta -- no se pudo probar la UI en vivo en este entorno (sin
  conexión a una BD real disponible), todo lo de acá se corrigió a partir
  de los stack traces que compartió el usuario.

*Configuración del Área Común (por edificio, en `BuildingConfig` -- ver
item #24, encajaría como una pestaña nueva "Áreas Comunes"):*
- Catálogo por edificio: salón de eventos, piscina, parrilla, gimnasio,
  cancha -- nombre, descripción, aforo máximo.
- Reglas de disponibilidad: duración mín/máx por reserva, buffer entre
  reservas consecutivas (tiempo para que limpieza prepare el área),
  ventana de anticipación mín/máx para reservar, tope de reservas activas
  por unidad.
- Costos en la moneda del edificio (`BuildingConfiguration.Currency`,
  mismo patrón que el resto de la config). **Garantía y Alquiler, cada uno
  como par `{Internos, Externos}`** -- decisión cerrada 2026-09-11: la
  garantía de un externo es normalmente mucho mayor que la de un
  propietario (ej. Garantía: S/200 internos / S/1,000 externos), así que
  no alcanza con un solo monto + "distinto para externos" como se planteó
  al inicio. Alquiler sigue el mismo patrón (0 en Internos por default si
  el admin no quiere cobrarle a propietarios, configurable). Limpieza
  queda como un solo monto (no varía por interno/externo, salvo que en la
  práctica se pida lo mismo -- se agrega el par si hace falta).
- **Penalidad por cancelación/no-show -- decisión cerrada:** dos toggles
  independientes, cada uno con su propio campo de días, apagados por
  default (se prenden solo si el edificio los necesita):
  - `PenalidadCancelacionHabilitada` (bool) + `DiasMinimosSinPenalidad`
    (int) -- cancelar con MENOS anticipación que ese número de días
    retiene la garantía completa; cancelar con más, la devuelve entera.
  - `PenalidadNoPresentadoHabilitada` (bool) -- si la unidad no se
    presenta el día de la reserva (no hay check-in), retiene la garantía
    completa. Sin campo de días propio (es binario: se presentó o no).
  - Ambas retienen el 100% de la garantía cuando aplican, sin un
    porcentaje configurable aparte -- más simple de construir e implica
    menos decisiones en el momento de cobrar. Si en la práctica el
    edificio quiere una penalidad parcial, se revisa después de ver el
    piloto en uso real, no antes.

*Reserva:*
- El propietario ve el calendario de disponibilidad del área (comparte
  motor de calendario con Mantenimiento -- ver `CalendarItem` -- así que
  un bloqueo por mantenimiento aparece automáticamente como no disponible
  al reservar, sin duplicar lógica). Elige una franja libre; el sistema
  bloquea el traslape automáticamente.
- Siempre tiene un propietario responsable (`IdOwner`, obligatorio) que
  respalda financiera y legalmente la reserva, más un campo opcional de
  "Organizador/Contacto externo" (nombre, DNI, teléfono) para cuando el
  edificio alquila el área a un tercero no propietario -- así el checklist
  de daños y el descuento de garantía siempre tienen a quién cobrarle,
  sin modelar un "usuario externo" completo en el sistema de auth.

*Aprobación -- decisión cerrada:* la aprueba o rechaza la **Junta**, no el
Administrador -- el criterio no es el monto (eso ya lo define la
configuración del área), sino que la fecha/horario no incumpla las normas
del edificio (ej. evento de madrugada, superposición con otra actividad no
reflejada en el calendario). El **Administrador hace seguimiento** del
pedido hasta que la Junta lo apruebe o rechace -- mismo patrón de rol que
ya existe en `approve_budget`/`approve_expenses` (`Components/Pages/
ApprovalsPages/Approvals.razor`), así que una reserva pendiente debería
sumar al mismo badge de "Aprobaciones" del menú izquierdo
(`LeftMenu.ContarAprobacionesPendientesAsync`, tocado esta misma sesión)
en vez de crear una bandeja aparte. Falta un permiso nuevo
`approve_reservations` para la Junta, consistente con el resto de
permisos de aprobación ya existentes.

*Check-in / Check-out -- decisión cerrada:* lo hace el **Administrador**
(no un rol de conserje separado, no autogestión del propietario) --
alcanza con el permiso que ya tiene, o un `manage_reservations` dedicado
si conviene separarlo de la config general del edificio. Checklist de
bienes del área (ej. "Salón de Eventos: 10 sillas, 2 mesas, sonido,
proyector") con estado por ítem (OK/Dañado/Falta) tanto al entregar como
al devolver, **con fotos** -- mismo patrón ya construido esta sesión para
Incidencias (`IFileStorageService`, `IncidentAttachment`, whitelist de
extensiones, carpeta por entidad): sin foto, un descuento de garantía
queda en "tu palabra contra la mía" con el propietario.

*Estados de la reserva (mismo patrón de `Status` que ya usan
Installment/Expense/Budget en este código):*
1. **Pendiente de Aprobación** -- recién creada, esperando decisión de la
   Junta.
2. **Aprobada** / **Rechazada** -- decisión de la Junta.
3. **Cancelada** -- por el propietario o el administrador antes del
   evento; guarda si se aplicó penalidad según `DiasMinimosSinPenalidad`.
4. **No Presentado** -- pasó la fecha/hora sin check-in; aplica penalidad
   si `PenalidadNoPresentadoHabilitada` está prendida.
5. **Entregada** -- check-in hecho (checklist inicial completo), área
   físicamente entregada.
6. **Finalizada** -- check-out hecho (checklist final completo), evento
   terminado.
7. **Cerrada** -- garantía liquidada (devuelta entera / retenida por
   penalidad / retenida por daños según el checklist final).

*Impacto financiero -- decisión cerrada 2026-09-11, con un matiz contable
importante que el usuario señaló y que es correcto:*

- **Alquiler y Limpieza son Ingreso real** -- se cobran porque el edificio
  prestó un servicio. Generan un Ingreso categorizado reutilizando el
  sistema de Categorías/Movimientos ya existente, van derecho a
  Dashboard/reportes financieros, y se concilian contra el registro de la
  reserva con el mismo patrón de plantillas/auto-match que ya usa la
  conciliación de gastos (para no quedar huérfanos en el estado de cuenta
  ni contarse doble).
- **Garantía NO es Ingreso -- es custodia/pasivo.** Es plata que puede
  volver íntegra al propietario; contarla como Ingreso al recibirla y
  como Gasto al devolverla infla ambos lados del reporte de Ingresos y
  Egresos con movimiento de plata que nunca fue del edificio. Al recibirla
  se registra como custodia (se concilia igual contra el estado de cuenta,
  para no quedar huérfana, pero clasificada aparte de un Ingreso normal).
  - Si se devuelve completa: sin impacto en Ingresos ni Egresos -- entró y
    salió, neto cero.
  - Si se retiene (por la penalidad de cancelación/no-show, o por daños
    según el checklist de cierre): el monto retenido recién ahí se
    convierte en Ingreso, pero en una **categoría propia y distinguible**
    ("Reposición de Daños - Reserva", no genérica) -- el usuario aclaró
    correctamente que conceptualmente no es ganancia del edificio, es
    cobertura del costo de reponer lo dañado, así que separarla de
    ingresos reales (Alquiler) deja los reportes financieros honestos
    sobre cuánto genera el edificio de verdad. El monto efectivamente
    devuelto (si hay devolución parcial) se concilia como egreso bancario
    marcado explícitamente "Devolución de Garantía", nunca como un
    Gasto/categoría normal.
- **Si el daño supera la garantía -- ya existe el mecanismo, no hay que
  construir nada nuevo:** `Services/IExtraChargeService.cs` ->
  `GenerarCuotaExtraordinariaAsync(idBuilding, descripcion,
  fechaVencimiento, montosPorUnidad, usuario)`. Verificado leyendo la
  implementación completa: SIEMPRE crea un `BudgetHeader` (tipo
  "Extraordinario"), sin importar si `montosPorUnidad` trae 1 unidad, un
  grupo, o todas -- el método recorre todas las unidades activas del
  edificio y sólo genera una `Installment` para las que vengan en el
  diccionario con monto > 0, saltando el resto. Sirve tal cual para "el
  daño de esta reserva superó la garantía del DPTO responsable" (pasando
  solo su `IdGroupUnit`) -- el mismo mecanismo que ya sirve para el caso
  general de "se malogró el botón del ascensor de un piso, cobrarle solo a
  ese grupo de DPTOs" (fuera del alcance de Reservas, pero confirma que el
  servicio no está atado a "aplica a todo el edificio").

*Cobro -- sin pasarela de pago digital todavía (ver brecha de cobro de
cuotas en el análisis de mercado):* Garantía/Alquiler/Limpieza se
registran y concilian manualmente por ahora, igual que el resto de la
cobranza actual -- no bloquea empezar a construir el módulo, pero sí
significa que "cobrar la garantía" y "devolverla" son, por ahora, marcar
un estado, no una transacción automática. **Abierto, sin bloquear el
diseño:** confirmar si Garantía + Alquiler + Limpieza llegan como una
sola transferencia del propietario o por separado -- define si la
conciliación matchea un solo monto combinado por reserva o hasta 3 líneas
distintas en el estado de cuenta. Se resuelve al diseñar la pantalla de
conciliación específica de Reservas, no antes.

**Gobernanza -- Reuniones, Votación y Actas como un solo flujo:**
El dato que gobierna todo esto: el **Decreto Legislativo 1568** (nuevo
régimen de propiedad horizontal en Perú, aún sin reglamento publicado en
su versión final -- ver fuente oficial abajo) establece que el voto se
computa por **porcentaje de participación (alícuota) de cada unidad, no
por cabeza** (Art. 14.1, fija además 75% de participación para desafectar
bienes comunes). El Art. 25 reconoce sesiones presenciales, virtuales o
híbridas como igualmente válidas -- la reunión virtual ya es régimen
permanente, no un parche pandémico. El quórum/mayorías para acuerdos
ordinarios (más allá del 75% legal) quedan delegados al Reglamento Interno
de cada edificio, así que el sistema no debe asumir un número fijo.

- **Alícuota -- decisión cerrada 2026-09-11: se deriva, no es un campo
  nuevo.** Se calcula como el área del **Grupo Unidad** (el DPTO más sus
  unidades asociadas -- cochera, depósito -- bajo el mismo grupo) sobre el
  área total del edificio. El dato ya existe: `OwnerUnitView.TotalArea`
  (`Classes/Unit.cs`) ya vive a nivel de grupo (DPTO + asociados, no la
  unidad suelta), y `Building.TotalArea` ya existe también -- la fórmula
  es `OwnerUnitView.TotalArea / Building.TotalArea` sin necesidad de
  ningún campo ni tabla nueva.
- **Reuniones:** Ordinaria (periódica) o Extraordinaria (tema puntual --
  gasto grande, elección de junta). Convocatoria con fecha, agenda y
  documentos adjuntos; notificación con acuse de recibo (email + WhatsApp,
  cuando el módulo de Comunicaciones esté listo). Modalidad presencial,
  virtual o híbrida. Quórum configurable por edificio.
- **Agenda -- aclarado 2026-09-11:** la Reunión tiene una agenda con N
  puntos; no todos generan votación. Cada punto puede ser **Informativo**
  (queda anotado en el Acta, sin votar -- ej. "se informa el avance de la
  obra") o **Sujeto a Votación** (genera su propia Votación, con su propio
  tipo nominal/secreta y su propia mayoría requerida). Una reunión de 5
  puntos puede terminar en 0, 1, 3 o 5 votaciones -- no es "una votación
  por reunión" ni "todos los puntos votan", depende punto por punto.
- **Votación:** ponderada por alícuota (no por persona). En vivo durante
  la Reunión, o asíncrona con fecha límite si el Reglamento Interno lo
  permite (voto adelantado). Nominal (queda registrado quién votó qué --
  típico para acuerdos de gasto) o secreta (típico en elección de junta
  directiva), configurable por punto de agenda. El sistema valida
  automáticamente si el resultado alcanza la mayoría requerida para ese
  tipo de acuerdo (simple, calificada, o el 75% legal).
- **Revotación -- decisión cerrada 2026-09-11: flexible, no rígida.**
  Cuando una votación no alcanza la mayoría requerida, si permitir un
  nuevo intento sobre el mismo punto depende del caso (a veces sí tiene
  sentido, a veces el punto queda Rechazado y ahí termina) -- se resuelve
  con un simple check `PermiteRevotacion` por punto de agenda (o al
  configurar la votación), no con una regla fija en el sistema. Si está
  prendido y la primera votación no alcanza mayoría, se habilita un nuevo
  intento (Ronda 2, 3...) sobre el mismo punto, dentro de la misma
  Reunión; si está apagado (o se agotan los intentos que el Administrador
  decida dar), el punto queda Rechazado. El Acta debería reflejar todos
  los intentos hechos, no solo el último, para que quede claro qué pasó.
- **Actas:** se genera un borrador automático a partir de lo ya capturado
  en Reunión + Votación (fecha, modalidad, asistentes con su % de
  participación, quórum verificado, agenda, resultado de cada punto) --
  reduce el riesgo de un acta redactada de memoria días después. Firmas de
  presidente y secretario según Reglamento Interno; una vez firmada, queda
  inmutable y buscable en el historial del edificio.
- **Flujo completo:** Convocatoria → Reunión (registra asistencia, suma
  alícuotas presentes) → ¿Quórum alcanzado? → si NO: se agenda Segunda
  Convocatoria con quórum reducido (según Reglamento Interno) como una
  nueva Reunión → si SÍ: por cada punto de agenda que lo requiera,
  Votación (voto ponderado) → si no alcanza mayoría y `PermiteRevotacion`
  está prendido, nueva Ronda sobre el mismo punto; si no, queda Rechazado
  → el resultado final de cada punto (y sus rondas, si hubo más de una) se
  vuelca automáticamente en el Acta.
- **Encuestas (versión sin peso legal):** no requiere quórum, no genera un
  acuerdo formal ni un Acta -- solo consulta de opinión. Uso típico:
  sondear interés antes de convocar una asamblea formal, medir
  satisfacción, priorizar mejoras menores. Mecánica mínima: pregunta(s),
  plazo de respuesta, resultado agregado, opción de anonimato -- la
  funcionalidad de menor esfuerzo de las cuatro piezas de gobernanza.

**Por qué esto también es un diferenciador de negocio, no solo una
feature:** ningún competidor peruano identificado se posiciona hoy como
"listo para el D.L. 1568" -- el reglamento aún no se publica, así que
nadie tiene ventaja consolidada todavía. Un Acta bien estructurada
(fecha, asistentes con alícuota, quórum verificado, resultado por punto)
es exactamente el tipo de expediente que respalda un acuerdo si algún día
se cuestiona judicialmente, y conecta con el reporte de morosidad que ya
existe (`DelinquencyReport.razor`) como base para el Registro de
Deudores/título ejecutivo que la misma ley habilita (ver también el punto
sobre precios y diferenciación más abajo en este documento).

**Falta por completo:** decidir alcance real de la primera versión (¿las
4 piezas de gobernanza juntas, o Reuniones+Votación+Actas primero y
Encuestas después, dado que es la de menor esfuerzo?) y el diseño de
pantallas -- no hay nada de qué partir en el código existente (la
alícuota ya quedó resuelta arriba, se deriva sin campo nuevo). Fuente
legal: [Decreto Legislativo 1568 -- texto oficial en El
Peruano](https://busquedas.elperuano.pe/dispositivo/NL/2181939-6). El
reglamento definitivo puede ajustar los quórum/mayorías exactos para
acuerdos ordinarios, pero la arquitectura de fondo (voto por alícuota,
quórum configurable por edificio, reunión-votación-acta como un solo
flujo) ya está confirmada en el texto vigente del decreto y no debería
cambiar.

### 22. Piloto para Móvil
**Estado: ya diagnosticado en detalle en `Docs/Design-Piloto-Mobile-Android.md`
-- no hace falta repetirlo acá, sólo lo que cambió con el pedido de hoy.**

Ese documento ya cubre arquitectura (Opción A PWA/TWA ahora → Opción B MAUI
Blazor Hybrid después), qué pantallas de Residente reusar, y deja abierta la
pregunta de storage de fotos (ver punto 18 de acá arriba, ya resuelta con la
recomendación de este documento). Lo que ese documento **no** cubre todavía,
a raíz de lo pedido ahora (ver sección "Plan de lanzamiento" más abajo): el
rol **Junta** no estaba en su alcance (sólo evaluó Residente) -- falta sumar
qué pantallas/acciones de Junta entran al piloto mobile y con qué nivel de
madurez (sólo lectura vs. acciones como aprobar gastos).

### 23. Auditar otras pantallas por el mismo bug de "no recarga al cambiar de Id en la URL"
*(Nuevo 2026-09-11 -- sospecha sin confirmar, sólo se corrigió el caso
puntual encontrado en Incidencias)*

`IncidentDetail.razor` sólo cargaba datos en `OnInitializedAsync` (corre
una sola vez por instancia de componente en Blazor) -- navegar de un
incidente a otro sin recargar la página entera (`NavigateTo` interno, sin
`forceLoad`) dejaba la pantalla mostrando datos del incidente ANTERIOR
mientras la URL ya apuntaba a otro Id (ver punto 18a, bug encontrado por el
usuario). Ya corregido ahí con `OnParametersSetAsync`, pero **no se revisó
si el mismo patrón existe en otras pantallas con parámetro de ruta**
(cualquier `@page "/algo/{Id:guid}"` que sólo cargue en
`OnInitializedAsync` es sospechoso -- candidatos obvios: cualquier
"detalle de X" navegable desde una lista del mismo tipo, ej. si existiera
un patrón similar en Presupuestos, Gastos, Edificios). Falta: grep de
`OnInitializedAsync` en páginas con `{Id...}` en la ruta, y confirmar cuáles
tienen (o no) el mismo problema.

### 24. Configuración de Edificio: pasar a página propia con Tabs
*(Nuevo 2026-09-11, pedido del usuario -- **IMPLEMENTADO** el mismo día,
`dotnet build` en 0 errores, falta probar en vivo)*

**Estado: hecho.** El usuario aclaró que "faltan cosas" se refiere a
agregados chicos futuros, no un prerequisito para el refactor -- así que se
arrancó de las 8 secciones existentes tal cual, sin esperar a decidir qué
falta.

- Página nueva `BuildingConfig.razor`/`.razor.cs`/`.razor.css` en
  `Components/Pages/BuildingPages/`, ruta `/buildings/{IdBuilding:guid}/config`.
  Las 8 secciones (Moneda y Cuentas, Pagos, Defaults/Multas y Mora,
  Administración, Inmobiliaria, Mantenimiento, Categorías Default,
  Excepciones de Pago) se movieron TAL CUAL -- misma lógica de
  edición/guardado por sección (`StartEditSection`/`SaveSection`/
  `CancelEdit`) -- envueltas en pestañas Bootstrap (`nav-tabs` + `@if` por
  tab, mismo patrón ya usado en `ReconciliationWorkspace.razor`, no el
  componente `Tabs` de BlazorBootstrap).
  - Fix de lifecycle aplicado de entrada (mismo bug que ya se había
    encontrado y corregido esta sesión en `IncidentDetail`): como la ruta
    tiene un parámetro (`{IdBuilding:guid}`), navegar de un edificio a otro
    reutiliza la misma instancia del componente -- se usa
    `OnParametersSetAsync` + un `_loadedId` trackeado, NO
    `OnInitializedAsync`, para que sí recargue al cambiar de edificio.
  - El modal de "Configuración Rápida" (copiar config de otro edificio) y
    los modales de Cuenta Bancaria y Exoneración se movieron con las
    secciones (se abren desde adentro de la página de config, ya no desde
    el listado).
- `BuildingPage.razor`/`.razor.cs` (el listado, `/buildings`) se
  simplificó: de 1518+909 líneas quedó en 360+~250. Ya no carga
  `BuildingConfiguration` completa (BankAccounts, Exonerations, categorías,
  unidades) sólo para mostrar la lista -- eso ahora lo carga
  `BuildingConfig` al entrar. El ícono de engranaje por fila (antes
  "Configuración Rápida" -> modal) y el click en toda la fila ahora
  navegan a `/buildings/{id}/config`.
- CSS: `BuildingPage.razor.css` es scoped al archivo viejo, así que no
  aplicaba solo por mover el markup -- se creó `BuildingConfig.razor.css`
  con una copia de las clases que usan las 8 secciones
  (`card-hover`, `config-card`, `edit-mode`, `section-title`,
  `edit-button`, `form-compact`, `payment-methods`, `currency-selector`,
  `action-buttons`).
- **Pendiente:** probar en vivo (crear/editar cada sección desde la página
  nueva, confirmar que Configuración Rápida y los 2 modales siguen
  funcionando igual, confirmar que el ícono de engranaje navega bien).
  También quedó pendiente la conversación con el usuario sobre qué campos
  de configuración le faltan (la razón original por la que pidió Tabs) --
  ahora que hay una página por tab es más fácil agregarlos sin reabrir este
  refactor.

**Feedback del usuario tras probar en vivo (2026-09-11): esperaba un
rediseño, no sólo mover las cards a pestañas.** Confirmado -- el refactor
movió las 8 secciones TAL CUAL (mismas cards con el lápiz flotante, mismo
`form-compact`, mismo layout de una sola columna angosta dentro de cada
tab) sólo para reducir riesgo y no romper la lógica de guardado en el
mismo cambio. El resultado visual (ver capturas del usuario: tab "Moneda y
Cuentas" e "Inmobiliaria") es funcional pero se nota que cada pestaña
sigue pensada para competir por espacio con una lista al lado, no para
ocupar una página completa -- mucho aire vacío a los costados, formularios
angostos y verticales cuando ahora hay ancho de sobra para 2-3 columnas,
sin components de tabla (ej. Cuentas Bancarias) para una lista de más de
2-3 items.

**Pendiente (rediseño, separado del refactor estructural que ya está
hecho):**
- Usar el ancho completo de la página: formularios en grid de 2-3 columnas
  en vez de una sola columna angosta, especialmente en Contactos/
  Inmobiliaria/Mantenimiento (campos cortos: Nombre, Teléfono, Email,
  Dirección) y Defaults/Multas (ya tiene varios `col-md-6`, pero dentro de
  un contenedor que sigue angosto).
- Cuentas Bancarias: hoy es una lista de filas con inputs en modo edición;
  con ancho de sobra podría ser una tabla o cards en grid en vez de filas
  apiladas.
- Revisar si el patrón "lápiz flotante -> modo edición inline" (heredado
  del panel viejo) sigue siendo el mejor ahora que cada sección tiene su
  propia pestaña dedicada, o si conviene un patrón más simple (ej. la
  pestaña entera en modo lectura con un solo botón "Editar" arriba a la
  derecha, en vez de un lápiz por card).
- Definir esto CON el usuario antes de tocar CSS/markup de nuevo -- es
  trabajo de diseño, no un bug a resolver solo.

**Verificado en el código -- confirma el problema que señaló el usuario.**
`/buildings` (`BuildingPage.razor`, **1518 líneas** de markup + 909 de
code-behind) hoy mezcla dos superficies de configuración distintas en la
misma pantalla:
- Columna izquierda: lista de edificios -- cada fila ya tiene un ícono de
  engranaje ("Configuración Rápida", `ShowQuickConfig`) que abre un
  **modal** chico y separado.
- Columna derecha: al seleccionar un edificio de la lista, se despliega un
  **panel único y larguísimo** con 8 secciones apiladas una debajo de la
  otra, todas en el mismo scroll: Moneda y Bancos, Pagos, Multas y Mora,
  Contactos, Inmobiliaria, Mantenimiento, Categoría Default, Excepciones de
  Pago.

El usuario señaló que la configuración de un edificio se está volviendo
cada vez más compleja -- y es verificable: varios puntos de este mismo
backlog fueron agregando campos a `BuildingConfiguration` sobre esta misma
pantalla (`ExpenseApprovalThreshold`, `ReceiptFooterText`,
`DebtWarningDays`/`DebtCriticalDays`, entre otros ya existentes) sin que la
pantalla en sí se haya reorganizado -- y sospecha que, todo mezclado así,
puede haber algo faltando o difícil de encontrar.

**Propuesta del usuario:** que cada edificio tenga su PROPIA página de
configuración (no un panel al costado de la lista), organizada en **Tabs**
(una pestaña por sección -- las 8 actuales, más lugar para las que falten).
Se llega ahí desde `/buildings` con un ícono de configuración por fila --
en el mismo lugar donde hoy está el gear icon de "Configuración Rápida".

**Lo que falta diseñar/decidir antes de tocar código:**
- Si el modal actual de "Configuración Rápida" desaparece (absorbido por
  la página nueva) o se mantiene aparte para los 2-3 campos que se editan
  más seguido (uso rápido sin entrar a la página completa).
- Ruta de la página nueva (ej. `/buildings/{id}/config`) y el listado
  definitivo de tabs -- arrancar de las 8 secciones actuales como base, y
  de ahí el usuario mencionó "creo que faltan cosas": conviene revisar
  junto con él qué configuración falta ANTES de mover las secciones
  existentes, para no tener que reordenar los tabs dos veces.
- Migrar las 8 secciones (hoy todas en un solo archivo) a una página con
  tabs, cuidando no romper la lógica de guardado actual
  (`BuildingPage.razor.cs`).

**Alcance:** es un refactor de UI + reorganización, no builds nuevos de
lógica de negocio -- pero por las 1518 líneas involucradas, conviene
tratarlo como su propio bloque de trabajo, no intercalado línea por línea
con otros puntos del backlog.

### 25. Email y WhatsApp: qué funciona hoy, qué falta, y cómo probarlos
*(Nuevo 2026-09-11, a raíz de la pregunta del usuario -- diagnóstico +
herramienta de prueba, sin tocar la lógica de negocio)*

**Diagnóstico de Email -- verificado revisando TODOS los lugares que
llaman a `IEmailService.SendEmailAsync` en el repo:**

- `appsettings.json` tiene `Email:SmtpPassword` **vacío** -- sin eso, Gmail
  rechaza la autenticación y cualquier envío falla. Además, desde ~2022
  Gmail **no acepta la contraseña normal de la cuenta por SMTP** -- hace
  falta activar la Verificación en 2 Pasos y generar una **Contraseña de
  Aplicación** (Cuenta de Google > Seguridad > Contraseñas de aplicaciones,
  16 caracteres) específica para esto, y poner ESA en
  `Email:SmtpPassword` -- nunca la contraseña real de la cuenta, y nunca
  commiteada al repo (`dotnet user-secrets` o `appsettings.Development.json`,
  que ya está en `.gitignore`).
- `EmailService` (`Services/IEmailService.cs`) **no tiene modo Simulate**
  (a diferencia de WhatsApp/MercadoPago) -- sin la Contraseña de Aplicación
  configurada, cualquier intento de mandar un correo **falla de verdad**
  (excepción real de `SmtpClient`), no se simula.
- **Buena noticia: ningún flujo real de la app se rompe por esto hoy.**
  Los 4 lugares que mandan email (bienvenida al registrarse --
  `IAuthService.SendWelcomeEmailAsync`; notificar incidentes --
  `IIncidentService`; notificar eventos de calendario -- `ICalendarService`;
  invitar a un colaborador -- `IAccountService.InviteCollaboratorAsync`)
  están **todos** envueltos en `try/catch` "best effort" -- si el email
  falla, se loguea el error y el flujo real (el registro, el incidente, el
  evento, la invitación) sigue andando igual. La invitación de colaborador
  además tiene respaldo visible: el link queda mostrado en `Settings.razor`
  aunque el correo nunca haya salido.
- **Dos flujos que parecían mandar correo, pero NO mandan nada hoy** (el
  código está comentado, no es un bug de configuración):
  - `EmailConfirmationService.ResendConfirmationEmailAsync` -- el
    `SendEmailAsync` real está comentado (`Services/IEmailConfirmationService.cs:195-198`);
    en su lugar sólo queda el link en el log del servidor
    (`_logger.LogInformation("ConfirmationLink {ConfirmationLink}"...)`).
    Parece deliberado para poder probar el flujo de confirmación sin SMTP
    configurado, pero significa que reenviar el correo de confirmación no
    le llega a nadie todavía.
  - `BudgetGenerator.NotifyOwners()` ("notificar a propietarios al
    publicar presupuesto") -- el cuerpo entero está comentado, y
    `ShouldNotifyOwners()` devuelve `false` siempre (línea 1727) -- esta
    función nunca se ejecuta, quedó como esqueleto sin terminar.

**Diagnóstico de WhatsApp:** el servicio (`IWhatsAppService`, punto #17)
está construido y probado en modo simulado, pero **no hay ningún botón en
la app que lo dispare todavía** -- se construyó como infraestructura para
el futuro módulo de Comunicados, sin ninguna pantalla conectada aún.

**Herramienta agregada para poder probar los dos (2026-09-11):**
`Components/Pages/SettingPages/TestNotificaciones.razor`
(`/Settings/TestNotificaciones`, gateado a SysAdmin, mismo patrón que
`PermissionsAdmin.razor`) -- dos formularios simples (Para/Asunto/Mensaje
para Email, Número/Mensaje para WhatsApp) que llaman directo a
`IEmailService`/`IWhatsAppService` y muestran el resultado real (incluido
el mensaje de error real de Gmail si el SMTP falla) en pantalla, sin tener
que ir a mirar los logs del servidor. Es una herramienta de diagnóstico,
no queda registrada en ningún lado de la BD.

**Para dejar Email funcionando de verdad:** generar la Contraseña de
Aplicación en la cuenta de Gmail configurada (`enriquek@gmail.com`) y
cargarla en `Email:SmtpPassword` (nunca en `appsettings.json` commiteado).
Con eso puesto, probar desde `/Settings/TestNotificaciones` antes de
confiar en que los correos de bienvenida/invitación/notificaciones ya
están saliendo de verdad.

**Para dejar WhatsApp funcionando de verdad:** los pasos ya quedaron
descritos en el punto #17 (cuenta de Twilio, Sandbox, `AccountSid`/
`AuthToken` en `Twilio:*`) -- una vez cargados, probar desde la misma
pantalla nueva.

### 26. Perf: menú izquierdo demoraba hasta un minuto en la primera carga
*(Nuevo 2026-09-11, reportado por el usuario -- **IMPLEMENTADO**, falta
confirmar en vivo cuánto mejoró)*

**3 problemas reales encontrados en la cadena de carga inicial de cada
circuito (login, F5, reconexión) -- todos corregidos:**

1. `PermissionService.GetUserPermissionsAsync` tenía un campo con nombre
   de caché (`_userPermissionsCache`) que **sólo se escribía, nunca se
   leía antes de pisarlo** -- cada `HasPermissionAsync` volvía a consultar
   los permisos del rol contra la BD. Sólo
   `LeftMenu.LoadBadgeCountsAsync` ya dispara eso 3 veces en la primera
   carga (+ 1 más desde `GetMenuForUserAsync`) -- 4 consultas idénticas en
   cascada por nada. Ahora se cachea por rol (mismo patrón que ya usaba
   `_menuCacheByRole` para el menú).
2. `LeftMenu.LoadMenuAsync` esperaba a que terminaran los badges de
   "pendientes" (solicitudes de acceso + aprobaciones, 2-4 consultas
   encadenadas) ANTES de poder pintar el menú -- toda la barra lateral
   quedaba en blanco hasta que esas consultas terminaban, aunque no
   tuvieran nada que ver con la mayoría de los links. Ahora el menú se
   pinta apenas están los ítems; los badges llegan un instante después con
   su propio `StateHasChanged` (y las 2 bandejas de badges corren en
   paralelo entre sí).
3. `UserSessionLoader.LoadAsync` (reconstruye la sesión completa a partir
   de la cookie -- lo PRIMERO que espera cualquier circuito nuevo, antes
   de que CUALQUIER cosa pueda mostrarse, no sólo el menú) encadenaba 4
   consultas independientes (`GetUserBuildingAssociationAsync`,
   `GetAllBuildingByOwnerAsync`, `GetAllBuildingsConfigAsync`,
   `GetRoleByUserIdAsync`) una atrás de la otra. Ahora corren en paralelo
   con `Task.WhenAll` -- seguro porque `BDLayout` ya está diseñado
   justamente para esto (cada llamada abre su propio `DbContext` de corta
   vida vía `IDbContextFactory`, confirmado en el comentario del propio
   constructor de `BDLayout.Core.cs`).

**Lo que esto NO explica:** si la demora persiste incluso después de este
fix, lo más probable es el costo de arranque normal de .NET en modo
Development (JIT de un montón de componentes Blazor la primera vez que se
piden, más LocalDB arrancando si no estaba corriendo) -- eso no se arregla
con cambios de código, sólo se nota la primera vez que se corre la app
después de compilar/reiniciar, no en cada F5 normal. Pedirle al usuario
que confirme si mejoró y en qué medida.

---

## Resumen rápido

| # | Tema | Prioridad | Tipo |
|---|------|-----------|------|
| 1 | Unidades sin propietario no facturan | Alta | Diseño + código |
| 2 | Reportes suman transacciones Ignoradas | Alta | Código (requiere ver SP) |
| 3 | Tolerancia de redondeo en conciliación | Alta | Decisión + código |
| 4 | Borrado de edificio: FKs sin confirmar | Alta | Verificación de BD |
| 5 | Soporte real de multimoneda | Alta | Diseño + código |
| 6 | Bug `ConfirmationUtil` (4+ pantallas) | Media | **Resuelto** (2026-09-11) |
| 6b | Lectura de agua incompleta bloquea publicar (antes era advertencia) | Alta | **Resuelto** (2026-09-11) |
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
| 17 | Comunicados vía WhatsApp -- **IMPLEMENTADO**, falta probar en vivo + plantillas de Meta | Alta* | Implementado, falta validar |
| 18 | Storage de archivos: 18a (Incidencias) y 18b (Recibos PDF) **ambos implementados** | Alta* | **Resuelto** (2026-09-11), falta probar con BD real |
| 19 | Login social Google/Facebook/Apple | Media* | Producto + código |
| 20 | Reportes de Incidencias | Media* | Código (patrón ya existe) |
| 21 | Módulo de Reservas -- **IMPLEMENTADO**, falta probar en vivo; Gobernanza (Reuniones/Votación/Actas/Encuestas) sigue en diseño, "Citas" descartado | Baja-Media* | Reservas implementado; Gobernanza diseño + código (grande) |
| 22 | Piloto Móvil (sumar alcance de Junta) | Alta* | Diseño + código |
| 23 | Auditar otras pantallas por el bug "no recarga al cambiar Id en URL" | Baja | Investigación |
| 24 | Configuración de Edificio: página propia con Tabs -- estructura **HECHA**, falta **rediseño visual** (usuario esperaba más que mover cards a pestañas) | Media | Diseño UI |
| 25 | Email: falta Contraseña de Aplicación de Gmail + 2 flujos comentados | Alta | Configuración + decisión |
| 26 | Perf: menú izquierdo demoraba hasta 1 min en la primera carga -- **HECHO**, falta confirmar en vivo | Alta | Código (bug de caché + paralelizar consultas) |

`*` Prioridad pensada en función del piloto (ver "Plan de lanzamiento" abajo),
no del mismo criterio de "dinero en riesgo hoy" que los puntos 1-16.

---

## Plan de lanzamiento del piloto (pedido 2026-09-11)

Objetivo del usuario: lanzar el piloto en **Web completo para Administrador
tal como está**, y en **Mobile empezar con Residente y Junta** (o ver qué se
puede lanzar de eso).

### Web -- Administrador: se puede lanzar como está

El panel de Administrador ya cubre el ciclo completo (edificios, unidades,
presupuestos, cuotas, conciliación bancaria, gastos, reportes, permisos,
incidencias, lecturas de agua). Ningún punto de los 1-16 de arriba es un
bloqueante técnico para encender el piloto -- son riesgos/deuda a atender en
paralelo, no un "no funciona". Antes de lanzar, priorizar sólo lo que puede
afectar la confianza del Administrador piloto desde el día 1 (los ✅ ya
listados como Alta 1-5): sobre todo **#3 tolerancia de redondeo** (si el
edificio piloto tiene cuotas migradas, van a verse "Parcial" sin serlo de
verdad) y **#1 unidades sin propietario** (si el edificio piloto tiene
unidades sin vender, hoy no se les factura a la inmobiliaria). El resto
(multimoneda, Ignoradas en reportes, FKs de borrado) es menor si el piloto
es un solo edificio, una sola moneda, y nadie va a borrar el edificio.

### Mobile -- Residente y Junta: lo que ya se puede lanzar hoy

Retomando el alcance de Fase 1 de `Design-Piloto-Mobile-Android.md`
(Opción A, PWA/TWA -- cero reescritura, reusa las páginas web tal cual):

**Residente (ya evaluado en ese documento):**
- Login, ver mis cuotas/recibos, ver presupuesto del edificio, calendario.
- Reportar incidente **sin foto todavía** (foto es Fase 2, depende de la
  decisión de storage del punto 18 -- ya resuelta arriba, falta construirla).

**Junta (nuevo, no evaluado en el documento original) -- mismo criterio
de "solo lectura primero" que se usó para Residente:**
- Ver presupuesto del edificio -- ya existe (`ViewBudget.razor`), mismo
  camino que Residente.
- Ver incidencias (Junta tiene visibilidad total, sin botones de acción --
  `IncidentList.razor:193-195`) -- lista para reusar tal cual.
- Calendario -- ya existe, mismo componente que Residente.
- **Aprobar gastos** (`ExpensePage.razor`, el flujo real de aprobación de
  Junta para gastos sobre el umbral configurado) -- existe en web, pero es
  una pantalla de escritorio (tabla/grid) no evaluada para mobile todavía.
  Recomendación: dejarlo **fuera de la Fase 1** del piloto mobile (como se
  dejó "pagar cuota" fuera para Residente) y sumarlo en una fase siguiente
  una vez confirmado que el layout responde bien en celular -- aprobar un
  gasto es una acción con plata de por medio, no conviene apurarla sin
  probar la UX en pantalla chica primero.

**Conclusión:** el piloto mobile Residente + Junta de solo lectura (cuotas,
presupuesto, calendario, incidencias) se puede armar con las páginas que YA
EXISTEN, empaquetadas como PWA/TWA (Fase 1 del plan existente) -- no
requiere ninguno de los 6 módulos nuevos de arriba para arrancar. Reportar
incidentes con foto y "aprobar gastos" desde el celular quedan para la fase
siguiente del mismo plan.

