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

## Segunda vuelta: cobro real con Stripe (modo test)

Decisiones acordadas para esta parte:

1. **Recurrente, no pago único**: `Mode = "subscription"` (Stripe cobra solo
   todos los meses mientras la suscripción siga activa), no `Mode = "payment"`
   -- eso sería un cobro único que habría que repetir a mano cada vez.
2. **Prices pre-creados, no `PriceData` inline**: cada plan pago necesita un
   Product + Price recurrente creado a mano en el Dashboard de Stripe (modo
   test), guardado en `SubscriptionPlan.StripePriceId`. El plan Trial nunca
   tiene uno -- no se cobra.
3. **Activación por webhook, no por el redirect de éxito**: `/pago-exitoso` es
   sólo una pantalla de cortesía -- la Suscripción se activa cuando Stripe le
   avisa al servidor (`POST /api/stripe/webhook`, evento
   `checkout.session.completed`), verificado contra `Stripe:WebhookSecret`. El
   redirect del navegador no es confiable (el usuario puede cerrar la pestaña
   antes de que cargue).
4. **Secretos nunca commiteados**: `appsettings.json` trae `Stripe` con los 3
   valores vacíos a propósito (mismo patrón que ya usaba `SmtpPassword`). Los
   valores reales van con `dotnet user-secrets` en desarrollo, o como variables
   de entorno (`Stripe__SecretKey`, etc.) en el servidor real -- nunca en un
   archivo commiteado. Ver también la sección de abajo sobre la contraseña de
   SQL Server que sí había quedado expuesta.

### Qué se implementó

- **`Database/Scripts/2026-09-04_45_Subscription_Stripe.sql`**: agrega
  `SubscriptionPlan.StripePriceId`, `Subscription.StripeCustomerId`/
  `StripeSubscriptionId`, y `UPD_ActivateSubscription` (upsert: pisa la fila
  más reciente del usuario -- normalmente la del Trial -- o inserta una nueva
  si no tiene ninguna).
- **`SpiderHood/Services/IPaymentService.cs`** (nuevo):
  `CreateCheckoutSessionAsync(idUser, userEmail, idSubscriptionPlan, domain)`
  arma la Checkout Session y devuelve la URL. Tira si el plan no tiene
  `StripePriceId` cargado todavía.
- **`Program.cs`**: `StripeConfiguration.ApiKey` desde config,
  `POST /api/stripe/webhook` (público, sin autenticación de usuario --
  verificado por firma) que llama a
  `ISubscriptionService.ActivateSubscriptionAsync` en
  `checkout.session.completed`.
- **`Settings.razor`**: lista los planes con `StripePriceId` cargado (menos el
  actual) con botón "Suscribirse" -> redirige a Stripe Checkout.
- **`/pago-exitoso`, `/pago-cancelado`**: pantallas de cortesía post-checkout.

### Runbook -- lo que falta del lado del usuario

1. Cuenta de Stripe en **modo test**, tomar `pk_test_...` y `sk_test_...` desde
   el Dashboard.
2. Crear un **Product** por cada plan pago (Básico, Empresarial) con un
   **Price recurrente mensual** (Dashboard -> Product catalog -> + Add
   product -> Recurring). Copiar cada `price_...`.
3. Cargar los Price ID en la BD:
   ```sql
   UPDATE SubscriptionPlan SET StripePriceId = 'price_XXXX' WHERE Name = 'Basico';
   UPDATE SubscriptionPlan SET StripePriceId = 'price_YYYY' WHERE Name = 'Empresarial';
   ```
4. Secretos locales (nunca en `appsettings.json`):
   ```
   dotnet user-secrets init
   dotnet user-secrets set "Stripe:SecretKey" "sk_test_..."
   dotnet user-secrets set "Stripe:PublishableKey" "pk_test_..."
   ```
5. Instalar la **Stripe CLI** y correr, en paralelo a la app:
   ```
   stripe listen --forward-to https://localhost:7175/api/stripe/webhook
   ```
   Ese comando imprime un `whsec_...` -- cargarlo también:
   ```
   dotnet user-secrets set "Stripe:WebhookSecret" "whsec_..."
   ```
6. Probar: `/Settings` -> "Suscribirse" a un plan -> Stripe Checkout (tarjeta
   de test `4242 4242 4242 4242`, cualquier fecha futura/CVC) -> `/pago-exitoso`
   -> refrescar `/Settings` (la Stripe CLI tiene que mostrar el evento
   `checkout.session.completed` recibido) y confirmar que el Plan/Estado
   cambiaron.

### Sin hacer todavía (fuera de alcance de esta vuelta)

- Cancelación desde la UI, y su webhook (`customer.subscription.deleted`) que
  marque `Status = 'Cancelled'`.
- Pago fallido en una renovación (`invoice.payment_failed`) -- hoy no se
  entera nadie.
- Reusar el `StripeCustomerId` existente en una re-suscripción (hoy Stripe crea
  un Customer nuevo cada vez, vía `CustomerEmail`).
- Deploy real: falta decidir cómo se cargan los secretos de Stripe en el
  servidor de QA/producción (variables de entorno, igual que se dejó
  documentado para la connection string en `DEPLOY-Production.md`).

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
