# Suscripción SaaS del Administrador

Diseño acordado en sesión de trabajo, implementado el mismo día. Mismo formato
corto que `Design-SelfService-Registro-Piloto.md`: decisiones + qué se tocó,
sin plan por pasos.

## Qué NO es esto

No es la "Conciliación" ni las cuotas/expensas que un Residente le paga al
edificio (eso ya existe, sección Pagos/Gastos). Esto es lo que un
**Administrador le paga a SpiderHood** por usar el sistema -- billing del
negocio, no del edificio.

## Decisiones acordadas

1. **A qué se ata**: a la cuenta del **Administrador** (`UserModel`), no al
   `Building`. Motivo: el Plan define **cuántos edificios puede administrar**
   esa cuenta (Básico = 1, Empresarial = 1 o N) -- no tiene sentido una
   suscripción por edificio si un solo Plan cubre varios.
2. **Catálogo de Planes**: tabla propia `SubscriptionPlan` (no el `Parameter`
   genérico que ya usa el resto de la app para catálogos tipo `Building.Type`)
   porque un Plan necesita guardar un límite real (`MaxBuildings`), y
   `Parameter` no tiene columna para eso. Sembrado con 3 filas: **Trial**
   (`MaxBuildings = NULL`, sin límite), **Básico** (`MaxBuildings = 1`),
   **Empresarial** (`MaxBuildings = NULL`, sin límite -- 1 o N edificios).
3. **Alta automática (Trial)**: `RegisterNewAdministratorAsync` (registro
   "Piloto" desde la landing, ver `Design-SelfService-Registro-Piloto.md`)
   crea una `Subscription` en plan Trial apenas se otorga el rol Administrador
   global -- sin fecha de vencimiento real todavía, sin bloquear nada. Es sólo
   la estructura; lógica de vencimiento/cobro real queda para un cambio
   aparte, igual que ya se había dejado pendiente el "Período de Prueba" en el
   diseño anterior.
4. **Enforcement -- única pieza que SÍ afecta comportamiento real**:
   `BuildingService.CreateBuildingAsync` chequea, antes de crear, cuántos
   edificios administra ya ese usuario (`UserBuildingAssociation` con
   `Role = "Administrador"`, aprobadas) contra `MaxBuildings` de su Plan
   vigente. Si lo alcanzó, devuelve `OperationResult.Failure(...)` con mensaje
   claro (ya se muestra vía `alert()` en `BuildingPage.razor.cs.SaveBuilding`,
   sin tocar esa pantalla). **Fail-open**: una cuenta sin ninguna fila en
   `Subscription` (cualquier Administrador de antes de este feature) no se
   restringe -- sólo se aplica el límite cuando existe una suscripción real
   con `MaxBuildings` no nulo. SysAdmin nunca se chequea (no crea edificios
   como "Administrador").
5. **Sin pasarela de pago todavía**: no hay Stripe/MercadoPago ni nada
   parecido conectado en esta vuelta -- eso es un cambio aparte cuando haga
   falta cobrar de verdad.
6. **UI**: sólo lectura, en `Settings.razor` -- reemplaza la columna "Your
   cart" (maqueta de checkout de Bootstrap sin cablear, con `$40` hardcodeado
   y "Promo code") por una tarjeta real: Plan, Estado, edificios usados/límite,
   fecha de inicio. Sin botones de upgrade/pago (no hay a dónde mandarlos
   todavía).

## Qué se implementó

- **`Database/Scripts/2026-09-04_44_Subscription.sql`**: `CREATE TABLE
  SubscriptionPlan` y `Subscription`, seed de los 3 planes,
  `INS_Subscription`, `GET_SubscriptionByUser` (join con el plan),
  `GET_AllSubscriptionPlans`.
- **`SpiderHood/Classes/Subscription.cs`**: `SubscriptionPlan` y
  `Subscription` (esta última denormalizada -- trae `PlanName`/`MaxBuildings`
  directo del join, sin necesidad de otro round-trip).
- **`SpiderHood/Data/SpiderHoodContext.cs`**: `HasNoKey()` para ambos, mismo
  patrón que el resto de las entidades que sólo se leen vía stored procedure.
- **`SpiderHood/Data/BDLayout.*.cs`**: `AddNewRecordAsync(Subscription)`,
  `GetSubscriptionByUserAsync`, `GetAllSubscriptionPlansAsync`.
- **`SpiderHood/Services/ISubscriptionService.cs`** (nuevo):
  `GetSubscriptionByUserAsync`, `CreateTrialSubscriptionAsync`,
  `EnsureCanCreateBuildingAsync(idUser, role)` (la regla del punto 4).
- **`AuthService.RegisterNewAdministratorAsync`**: llama a
  `CreateTrialSubscriptionAsync` después de `GrantGlobalAdministradorRoleAsync`.
- **`BuildingService.CreateBuildingAsync`**: llama a
  `EnsureCanCreateBuildingAsync` antes de crear el edificio.
- **`Settings.razor`**: tarjeta de suscripción read-only en vez del carrito
  falso.

## Segunda vuelta: cobro real con Stripe (modo test) -- abandonada

Se implementó primero con Stripe (`Mode="subscription"`, Price recurrente
pre-creado en su Dashboard, webhook `checkout.session.completed`). **Se
descartó por completo** porque Stripe no está disponible para cuentas de
Perú -- nunca llegó a activarse ninguna suscripción real con esa integración.
Reemplazada por MercadoPago en la vuelta siguiente (mismo día).

## Tercera vuelta: cobro real con MercadoPago (modo test)

Decisiones acordadas para esta parte:

1. **Recurrente vía Preapproval**: la API de "pago recurrente" de MercadoPago
   se llama *Preapproval* -- equivalente a una Subscription de Stripe. Cobra
   solo todos los meses mientras siga "authorized", no hay que repetirlo a
   mano.
2. **Monto inline, sin "Plan" pre-creado**: a diferencia de Stripe (que exigía
   crear el Price en su Dashboard antes), la Preapproval API acepta el
   monto/moneda directo en la llamada (`AutoRecurring.TransactionAmount`) --
   así que el precio se guarda directo en `SubscriptionPlan.Amount`/
   `CurrencyId`, sin ningún paso manual en el Dashboard de MercadoPago. Más
   simple que el runbook de Stripe. Precios elegidos: **Básico S/49.00/mes,
   Empresarial S/99.00/mes** (fáciles de cambiar con un `UPDATE`).
3. **Activación por webhook, no por el redirect (`BackUrl`)**: igual razón que
   con Stripe -- el navegador no es confiable. El webhook
   (`POST /api/mercadopago/webhook`) llega con el evento
   `subscription_preapproval`; ahí se vuelve a pedir el recurso completo a la
   API (`PreapprovalClient.GetAsync(id)`, nunca se confía en el body de la
   notificación) y, si `Status == "authorized"`, recién ahí se activa.
4. **Firma verificada a mano**: MercadoPago no tiene un helper tipo
   `EventUtility.ConstructEvent` de Stripe -- la validación del header
   `x-signature` (manifest `id:{dataId};request-id:{requestId};ts:{ts};` +
   HMAC-SHA256 contra el secreto de webhook, comparación en tiempo constante)
   se implementó a mano en `Program.cs` (`IsValidMercadoPagoSignature`),
   siguiendo el mismo patrón que ya usa `AuthService.VerifyLegacySha256Password`
   para comparar hashes.
5. **Identificación del usuario/plan**: en vez del `Metadata` de Stripe, la
   Preapproval usa `ExternalReference` (string libre) -- se guarda como
   `"{IdUser}:{IdSubscriptionPlan}"` y se parsea de vuelta en el webhook.
6. **Secretos nunca commiteados**: mismo criterio que con Stripe --
   `appsettings.json` trae `MercadoPago` con los 2 valores vacíos a propósito.
   Los reales van con `dotnet user-secrets` en desarrollo, o como variable de
   entorno (`MercadoPago__AccessToken`, etc.) en el servidor real.

### Qué se implementó

- **`Database/Scripts/2026-09-04_46_Subscription_MercadoPago.sql`**: saca las
  columnas de Stripe (nunca llegaron a usarse con datos reales) y agrega
  `SubscriptionPlan.Amount`/`CurrencyId` (con el `UPDATE` de los precios de
  arriba) y `Subscription.MercadoPagoPreapprovalId`. Reescribe
  `GET_SubscriptionByUser`, `GET_AllSubscriptionPlans` y
  `UPD_ActivateSubscription` (mismo upsert de antes, ahora con un solo Id en
  vez de dos).
- **`SpiderHood/Services/IPaymentService.cs`**: reemplazado -- ahora arma un
  `PreapprovalCreateRequest` (paquete NuGet `mercadopago-sdk`) y devuelve
  `preapproval.InitPoint`.
- **`Program.cs`**: `MercadoPagoConfig.AccessToken` desde config,
  `POST /api/mercadopago/webhook` (público, verificado por firma en vez de
  autenticación de usuario) que llama a
  `ISubscriptionService.ActivateSubscriptionAsync`.
- **`Settings.razor`**: los botones de plan ahora muestran el precio real
  (`Amount`/`CurrencyId`).
- **`/pago-exitoso`**: sigue sirviendo -- MercadoPago sólo tiene un `BackUrl`
  único (no hay success/cancel separados como en Stripe), así que
  `/pago-cancelado` queda sin usar por ahora (la página sigue existiendo, no
  se borró, pero nada redirige ahí).

### Runbook -- lo que falta del lado del usuario

1. Cuenta de MercadoPago (ya en trámite) y, adentro, una aplicación en **Tus
   integraciones** (developers.mercadopago.com.pe) -- de ahí salen las
   credenciales de **prueba** (Access Token, `TEST-...`).
2. Correr `Database/Scripts/2026-09-04_46_Subscription_MercadoPago.sql` contra
   la BD (ya deja cargados los precios de Básico/Empresarial).
3. Secretos locales (nunca en `appsettings.json`):
   ```
   dotnet user-secrets init
   dotnet user-secrets set "MercadoPago:AccessToken" "TEST-..."
   ```
4. Webhook: en la misma aplicación, **Webhooks -> Configurar notificaciones**,
   activar el evento **"Suscripciones"** (`subscription_preapproval`) y pegar
   la URL pública de `/api/mercadopago/webhook`. A diferencia de Stripe (que
   tiene una CLI que reenvía directo a `localhost`), acá hace falta exponer el
   puerto local con un túnel -- por ejemplo `ngrok http https://localhost:7175`
   -- y usar esa URL de ngrok en el Dashboard. Al guardar, el Dashboard
   muestra la **clave secreta** del webhook:
   ```
   dotnet user-secrets set "MercadoPago:WebhookSecret" "..."
   ```
5. Probar con un **usuario de prueba comprador** (se crean en el mismo panel
   de developers, "Usuarios de prueba" -- necesario porque con tu propia
   cuenta de vendedor no podés pagarte a vos mismo): loguearse en la app real
   como Administrador, `/Settings` -> "Suscribirse" -> te redirige a
   MercadoPago -> loguearse ahí con el usuario de prueba comprador, autorizar
   -> vuelve a `/pago-exitoso` -> confirmar en el log que llegó el webhook y
   que `/Settings` ya muestra el Plan/Estado actualizados.

### Sin hacer todavía (fuera de alcance de esta vuelta)

- Cancelación desde la UI y su lado del webhook (`status` pasando a
  `cancelled`/`paused`) -- hoy no se refleja.
- Pago fallido en una renovación mensual (evento
  `subscription_authorized_payment` con el cargo rechazado) -- hoy no se
  entera nadie.
- `/pago-cancelado`: quedó sin ruta real que la use (MercadoPago no tiene
  cancel_url separado) -- se podría aprovechar leyendo el query param que
  MercadoPago agrega al volver a `BackUrl`, si en algún momento hace falta
  distinguir el caso.
- Deploy real: falta decidir cómo se cargan los secretos de MercadoPago en el
  servidor de QA/producción (variables de entorno, igual que se dejó
  documentado para la connection string en `DEPLOY-Production.md`), y cuándo
  pasar de credenciales de prueba a las reales.

## Nota de seguridad encontrada de paso

Al revisar cómo manejar los secretos de Stripe se encontró que
`appsettings.Production.json` tenía la contraseña real de SQL Server
commiteada en texto plano desde hace varios commits, en un repo público. Se
vació ese valor (mismo patrón que ya usaba `SmtpPassword`) y se actualizó
`DEPLOY-Production.md` -- pero **la contraseña ya estuvo pública**, así que
rotarla en el SQL Server real queda pendiente del lado del usuario (sacarla
del archivo no deshace la exposición).

## Cabos sueltos / sin confirmar

- **Migración de administradores existentes**: no se les crea ninguna fila de
  `Subscription` retroactivamente -- quedan sin límite (fail-open, punto 4)
  hasta que alguien decida qué plan les corresponde. No bloqueante, pero es
  deuda: en algún momento alguien tiene que decidir el plan real de cada
  cuenta ya existente.
- **Vencimiento/cobro real, pasarela de pago, upgrade de plan desde la UI**:
  todo fuera de alcance de esta vuelta a propósito (ver punto 5).
- **Nombre exacto de los planes** ("Básico"/"Empresarial") y sus
  `MaxBuildings`: valores razonables elegidos ahora, no confirmados con
  pricing real -- fáciles de ajustar (`UPDATE SubscriptionPlan ...`) el día
  que haya un modelo de precios definitivo.
