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
