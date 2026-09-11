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

## Módulos / funcionalidad que todavía no existe (agregado 2026-09-11)

A diferencia de los puntos 1-16 (bugs/gaps encontrados trabajando en algo
que ya existía), esto es funcionalidad que **no está construida en absoluto**
-- verificado buscando en todo el repo, no por sospecha.

### 17. Comunicados / Anuncios
**Estado: no existe -- ni tabla, ni servicio, ni página. Decisión tomada
(2026-09-11): el canal prioritario es WhatsApp, no un tablón dentro de la
app.**

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

### 18. Incidencias: subir fotos/video -- ¿en la BD o en carpetas del servidor?
**Estado: pregunta técnica -- respuesta recomendada abajo.** Ya estaba
anotada como pregunta abierta #3 en `Docs/Design-Piloto-Mobile-Android.md`
(sección 9) al evaluar el piloto mobile -- hoy `Incident` (`Classes/Incidents/Incident.cs`)
no tiene ninguna columna para adjuntar nada, no existe integración de storage
de archivos en ningún lado (`appsettings*.json` tampoco tiene nada de Azure
Blob/S3), y `InputFile`/`IBrowserFile` sólo se usa hoy para subir Excel.

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
| 17 | Comunicados vía WhatsApp (canal prioritario, decidido) | Alta* | Producto + integración externa |
| 18 | Fotos/video en Incidencias: disco/storage, no BD | Alta* | Decisión + código |
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

