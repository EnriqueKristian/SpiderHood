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
3. **#17** Comunicados vía WhatsApp -- ya decidido como prioridad; arrancar
   ya con la verificación de negocio en Meta (no es instantánea) mientras se
   define proveedor (Cloud API vs. Twilio) y plantillas.
4. **#22** Piloto Móvil -- wrapper PWA/TWA + sumar alcance de Junta
   (solo lectura: presupuesto, incidencias, calendario).
5. **#18b** Storage de archivos + PDFs de Recibos -- **implementado
   2026-09-11** (disco, no BD; corrige el bug de integridad -- ver punto
   18). Falta correr `Database/Scripts/2026-09-11_95_ReceiptFile.sql` y
   probar con datos reales. El mismo storage se conecta después a **#18a**
   (fotos/video en Incidencias, más caro, depende de la Fase 2 del piloto
   móvil) -- ver la evaluación de impacto/costo/beneficio en el punto 18.

**Grupo 2 -- importante, no bloquea el lanzamiento:**
6. **#2** Reportes financieros suman transacciones Ignoradas.
7. **#6** Bug compartido en modales de confirmación (`ConfirmationUtil`) --
   fix chico, pero toca 4+ pantallas.
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

**Grupo 3 -- baja urgencia, manual, o investigación sin bloqueo real:**
17. **#16** Verificar URL del menú "Ingresos y Egresos".
18. **#13** Causa raíz del timeout en Conciliación de Pagos.
19. **#14** Confirmar upsert de `ServiceReadingDetail`.
20. **#15** Borrar un permiso (fuera de alcance).
21. **#12** Caso sin match en el Excel de Nova Alzamora (manual).
22. **#21** Módulo de Reuniones/Citas/Votaciones -- el más grande de todos,
    sin nada de qué partir en el código; conviene arrancarlo recién con
    tiempo/alcance dedicado, no intercalado con el resto.

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
**Estado: en progreso (2026-09-11) -- servicio de envío por WhatsApp
construido (`IWhatsAppService`/`WhatsAppService`, vía Twilio); la pantalla de
Comunicados en sí (tabla, quién publica, a quién le llega) todavía no.**

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

**Falta además, del lado de negocio/diseño (sin resolver todavía):** quién
publica (¿sólo Administrador/Junta?), a quién le llega (¿todo el edificio,
por torre/unidad, por rol?), si el comunicado también queda visible dentro
de la app (mejor para historial/auditoría, aunque WhatsApp sea el aviso
inmediato) o vive sólo en WhatsApp, y qué pasa con el residente que no dio
opt-in o no tiene teléfono cargado (¿cae a email como respaldo? -- ya existe
`IEmailService` para eso).

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
inmanejable a mediano plazo. Se cambió a `SaveAsync` para que `category`
acepte niveles (`receipts/{idBuilding}`, sanitizando cada nivel por
separado) y a **una carpeta por edificio**:
`receipts/{IdBuilding}/{Periodo:yyyyMM}_{UnitName}_{IdInstallment}.pdf`.
Deliberadamente **sin** subcarpeta por unidad/departamento -- si una unidad
se renumera con el tiempo, sus recibos viejos no quedan "perdidos" en una
carpeta con el nombre viejo; en cambio, Periodo+Unidad van en el NOMBRE del
archivo (mismo criterio que ya usaba el ZIP de `GenerateAllReceiptsZip`
para sus entradas), así la carpeta del edificio ya se puede ordenar/filtrar
a simple vista sin más anidamiento. El `IdInstallment` se mantiene al final
del nombre para garantizar unicidad aunque dos cuotas compartan
Unidad+Periodo (ej. una Ordinaria y una Multa del mismo mes). **No rompe
los recibos ya guardados antes de este cambio** (siguen en la ruta plana
vieja, registrada tal cual en su fila de `ReceiptFile` -- son inmutables,
nunca se mueven ni se regeneran).

**Verificado en este entorno:** se instaló el SDK de .NET 10 (ver
Docs/Pendientes-Negocio-Consolidado.md #17) y `dotnet build` compila sin
errores (0 errores, mismos 139 warnings preexistentes, ninguno nuevo). Se
probó además `LocalFileStorageService` con un programa aparte (fuera del
repo): guardar/leer funciona, un archivo inexistente devuelve `null` sin
tirar excepción, y dos variantes de path traversal (`../../../etc/passwd` y
`receipts/../../../../etc/passwd`) quedaron bloqueadas correctamente.

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

### 21. Módulo de Reuniones, Citas y Votaciones
**Estado: no existe -- cero código relacionado en todo el repo** (sólo
existe `CalendarItem`/`CalendarPage.razor`, que es un calendario genérico de
eventos, sin ningún concepto de convocatoria, quorum, agenda, acta o
votación).

Esto es el módulo más grande de los 6 -- probablemente 3 funcionalidades
separadas que conviene NO tratar como una sola:
- **Reuniones/Asambleas:** convocatoria (fecha, agenda, quorum requerido),
  registro de asistencia, acta.
- **Citas:** agendar una cita puntual (¿con el Administrador? ¿para usar un
  área común, si eso no vive ya en otro lado?) -- falta confirmar qué "cita"
  significa en este contexto, se presta a confusión con reserva de áreas
  comunes.
- **Votaciones:** puede ser standalone (una encuesta simple) o atada a una
  Asamblea (votar un punto de la agenda) -- tiene implicancias de peso legal
  si reemplaza una votación presencial (evidencia de quién votó qué, no
  necesariamente anónima en una junta de propietarios).

Falta por completo: decidir alcance real (¿las 3 juntas o empezar por una?),
y diseño de datos/pantallas -- no hay nada de qué partir en el código
existente.

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
| 17 | Comunicados vía WhatsApp (canal prioritario, decidido) | Alta* | Producto + integración externa |
| 18 | Storage de archivos: 18b Recibos PDF **implementado**, 18a Incidencias pendiente | Alta* | Código (18b) / Decisión + código (18a) |
| 19 | Login social Google/Facebook/Apple | Media* | Producto + código |
| 20 | Reportes de Incidencias | Media* | Código (patrón ya existe) |
| 21 | Módulo de Reuniones/Citas/Votaciones | Baja-Media* | Diseño + código (grande) |
| 22 | Piloto Móvil (sumar alcance de Junta) | Alta* | Diseño + código |

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

