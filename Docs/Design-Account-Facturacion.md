# Cuenta de Facturación (Account) + Colaboradores

Diseño acordado en sesión de trabajo, implementado el mismo día. Sigue a
`Design-Subscripcion-Administrador.md` -- lo complementa, no lo reemplaza.

## Qué NO es esto

No reemplaza `UserBuildingAssociation` (que sigue gobernando el acceso real
persona-a-persona-por-edificio, para Administrador/Junta/Residente por igual).
`Account` es sólo la entidad de **facturación y límite de plan** -- a quién se
le cobra, y de qué "pool" de edificios sale el `MaxBuildings`.

## Decisiones acordadas

1. **Por qué separar `Account` de `UserModel`**: hoy `Subscription` cuelga
   directo de la persona (`IdUser`) -- pero la facturación real (Razón
   Social/RUC) es de un negocio, no de una persona, y un mismo negocio puede
   tener más de un Administrador operando (colaboradores). `Account` pasa a
   ser el dueño de la Subscription y del límite de edificios; la persona se
   **asocia** a una Account (tabla `AccountUser`), no al revés.
2. **Datos de facturación pedidos al registrarse**: `RazonSocial`, `RucDni`,
   `Telefono` (de facturación, distinto del teléfono personal que ya se
   pedía). Se agregan a `RegisterAdminModel`/`/register-admin`.
3. **`AccountUser` con roles**: `Owner` (quien creó la cuenta, uno solo por
   ahora) y `Colaborador` (invitado). El dueño puede invitar colaboradores por
   email desde `/Settings`; el colaborador ve/administra los mismos edificios
   de esa Account.
4. **`Building.IdAccount`**: cada edificio pasa a pertenecer a una Account
   (no sólo a la persona que lo creó vía `UserBuildingAssociation`). Esto es
   lo que permite que **el límite de plan (`MaxBuildings`) se cuente por
   Account**, no por usuario -- si el dueño y un colaborador administran los
   mismos 3 edificios de una cuenta Profesional, cuentan como 3, no como 6.
   Nullable a propósito: los edificios creados antes de este feature quedan
   con `IdAccount = NULL` (fail-open, no se retroactivan).
5. **`Subscription` pasa a colgar de `Account`** (`IdAccount`, ya no
   `IdUser`), pero **los métodos públicos de `ISubscriptionService` mantienen
   la misma firma** (`GetSubscriptionByUserAsync(idUser)`,
   `ActivateSubscriptionAsync(idUser, ...)`,
   `EnsureCanCreateBuildingAsync(idUser, role)`) -- resuelven la Account del
   usuario puertas adentro. Esto evita tocar `Settings.razor`,
   `IPaymentService` y el webhook de `Program.cs`, que ya llamaban a estos
   métodos con `idUser`.
6. **Invitación de colaboradores**: mecanismo nuevo y separado del que ya
   existe para invitar Residentes a un edificio (`InvitationModel` está
   modelado 1:1 para eso -- `IdBuilding`, `ApartmentNumber` -- no aplica acá).
   `AccountInvitation` (Email, Code, IdAccount, Status) + página
   `/aceptar-invitacion?code=...` que resuelve dos casos: el email invitado
   **ya tiene cuenta** en SpiderHood (backend: agrega el `AccountUser`,
   listo) o **es nuevo** (pide nombre/contraseña, crea el `UserModel` +
   `AccountUser` en el mismo paso, autologin).

## Qué se implementó

- `Database/Scripts/2026-09-04_48_Account.sql`: tablas `Account`/`AccountUser`/
  `AccountInvitation`, `Building.IdAccount` y `Subscription.IdAccount`
  (nullable, fail-open), backfill de cuentas/edificios existentes, y los
  stored procedures nuevos + los reescritos (`INS_Building`/`UPD_Building`,
  `GET_AllBuildings`/`GET_AllBuildingsPublic`/`GET_BuildingById`/
  `GET_BuildingsByAccount`, `GET_SubscriptionByUser`/`INS_Subscription`/
  `UPD_ActivateSubscription`).
- `Models.Account`/`AccountUserView`/`AccountInvitation` (`Classes/Account.cs`)
  + `Building.IdAccount`/`Subscription.IdAccount`.
- `IAccountService`/`AccountService` (`Services/IAccountService.cs`):
  `CreateAccountAsync`, `GetAccountByUserAsync`, `GetCollaboratorsAsync`,
  `InviteCollaboratorAsync` (envía email best-effort + deja el link visible en
  Settings), `GetInvitationByCodeAsync`, `AcceptInvitationAsync` (agrega el
  `AccountUser`, replica `UserBuildingAssociation` de todos los edificios de
  la cuenta, otorga el rol global Administrador).
- `ISubscriptionService`: `EnsureCanCreateBuildingAsync` ahora cuenta
  edificios por Account (fail-open a UserBuildingAssociation si el usuario no
  tiene Account); `CreateTrialSubscriptionAsync` pasa a pedir `idAccount`
  (único caller: `AuthService.RegisterNewAdministratorAsync`, justo después
  de crear la Account).
- `RegisterAdmin.razor`/`RegisterAdminModel`: campos RazonSocial/RucDni/
  Telefono (obligatorios) -- `RegisterNewAdministratorAsync` crea la Account
  (Owner) antes del Trial.
- `BuildingService.CreateBuildingAsync`: resuelve la Account del creador y
  setea `Building.IdAccount` antes de persistir.
- `Settings.razor`: sección "Colaboradores" (lista + invitar por email) y
  `/aceptar-invitacion?code=...` (`AcceptInvitation.razor` +
  `AuthService.RegisterCollaboratorAsync`): cubre tanto el email que ya tenía
  cuenta en SpiderHood (login y "Aceptar") como el que es nuevo (mini
  registro + autologin), ambos casos delegan en
  `IAccountService.AcceptInvitationAsync`.

Pendiente (no bloqueante, fuera del pedido original): página propia de
"cancelar invitación" desde Settings (hoy sólo se listan); notificación por
email real de la aceptación al Owner.
