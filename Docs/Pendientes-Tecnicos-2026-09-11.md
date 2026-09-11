# Pendientes técnicos — para retomar 2026-09-12

Reportados por el usuario al cierre de la sesión del 2026-09-11, todavía sin
investigar. Ninguno tiene código escrito todavía — son sólo los síntomas tal
cual los describió, para arrancar el día siguiente sin perder contexto.

1. **`/users` — el filtro "Estado" (Activo/Inactivo) no funciona.**
   No especificó si no filtra nada, filtra mal, o tira error. Falta reproducir.
   Sospecha propia (sin confirmar): puede estar relacionado al rediseño de
   `Estado` de esta misma sesión (ahora depende de `FilasEstado`/alcance por
   edificio para un Administrador, y del flag global para SysAdmin — ver
   commit "Desactivar por edificio para Administrador..." del 2026-09-10) —
   revisar `AplicarFiltros`/`FiltrarPorEstado` en `Users.razor` contra ese
   cambio.

2. **Modo oscuro no se mantiene al navegar.** La app entra en modo oscuro
   (correcto, está guardado en preferencias), pero al hacer clic en
   cualquier pantalla se aclara sola. Revisar `wwwroot/js/theme.js` y el
   script inline de `App.razor` (aplica el tema ANTES del primer paint) —
   sospecha: algo en la navegación entre páginas (¿Blazor Server
   re-renderiza sin re-ejecutar el script del tema, o hay un lugar que
   resetea `data-bs-theme`/localStorage a "light" en cada navegación?).

3. **Pantalla de Login "se pierde" al loguear.** Descripción vaga todavía —
   preguntar al usuario qué significa exactamente (¿flash de contenido sin
   estilo, layout roto un instante, redirección rara?) antes de intentar
   reproducir. Screenshot de esta sesión (ver mensaje del cierre) muestra el
   layout del login con el sidebar oscuro vacío a la izquierda incluso antes
   de loguearse -- posible pista: ¿el `MainLayout`/sidebar se está
   renderizando de fondo en la pantalla de Login cuando no debería?

4. **Revisar "envío de mails".** Sin detalle todavía -- preguntar qué
   síntoma exactamente (¿no llegan, tardan, van a spam, contenido
   incorrecto, cuál flujo -- confirmación de email, invitación, bienvenida,
   reset de contraseña?). Candidatos ya vistos en el código esta sesión:
   `IEmailService`/`IEmailConfirmationService`, `SendWelcomeEmailAsync` en
   `AuthService`.

5. **`/buildings` — "Texto del pie del recibo" se guarda en BD pero no se ve
   en modo lectura hasta salir y volver a entrar a la pantalla.** Posible
   pista propia: hoy mismo (commit de Fase 2, revisión de
   `BuildingConfiguration.Clone()`) se encontró que `Clone()` no copiaba
   `ReceiptFooterText` (bug ya arreglado ahí) -- confirmar si el usuario ya
   probó con ese fix aplicado o si el síntoma persiste igual; si persiste,
   el problema está en otro lado (revisar el flujo completo de guardado en
   `BuildingPage.razor.cs`: qué pasa con `SelectedBuilding` después de
   `SaveSection` -- ¿se re-lee de servidor, o se sigue mostrando la
   instancia en memoria de antes de guardar?).

6. **`/buildings` — evaluar rediseño de la pantalla de Configuración.** El
   edificio ya acumuló bastantes secciones de configuración (Multas y Mora,
   Contactos, etc.) -- decidir si se mantiene el layout actual (tarjetas
   editables en una sola pestaña larga) o si conviene una pantalla aparte
   con mejor organización (¿tabs por categoría? ¿acordeón? evaluar con el
   usuario antes de tocar nada, es una decisión de diseño, no un bug).

## Antes de arrancar

- Para el punto 1, pedirle al usuario un screenshot o repro exacta (¿qué
  filtro aprieta, qué esperaba ver, qué ve en realidad?).
- Para los puntos 3 y 4, pedir más detalle -- están descritos muy en
  general todavía.
