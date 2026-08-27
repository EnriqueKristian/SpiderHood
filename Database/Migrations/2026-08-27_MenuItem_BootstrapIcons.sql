-- Migra Icon de FontAwesome (fas/fa-solid) a Bootstrap Icons (bi-*).
--
-- App.razor solo carga bootstrap-icons (ver <link ... bootstrap-icons.min.css>), no
-- FontAwesome, así que TODOS los íconos del menú (guardados como "fas fa-x" / "fa-solid
-- fa-x") están rotos en la app: <i class="fas fa-x"> no matchea ninguna regla CSS
-- cargada, el ícono simplemente no aparece. Este script los reemplaza uno por uno por su
-- equivalente en Bootstrap Icons, eligiendo el ícono más parecido en significado.
--
-- De paso limpia 3 filas que traían un "me-2" pegado al valor de Icon (Administrar Menú,
-- Roles y Permisos, Asignar Roles) — LeftMenu.razor ya agrega " me-2" al renderizar
-- (<i class="@menuItem.Icon me-2">), así que quedaba duplicado.
--
-- NOTA sobre el nombre de la tabla: se asume dbo.MenuItems (confirmado por el usuario), aunque el nombre no coincide con el
-- nombre del stored procedure UPD_MenuItem (singular) — a diferencia de BuildingConfiguration,
-- aquí el nombre de la tabla SÍ lleva plural aunque el del SP no.
--
-- Ejecutar contra la base de datos de SpiderHood (SSMS / Azure Data Studio / sqlcmd).

BEGIN TRAN;

UPDATE dbo.MenuItems SET Icon = 'bi-cash-stack' WHERE IdMenu = '49568505-992F-48DF-AC28-0190F260E137'; -- Ingresos y egresos (antes: fas fa-cash-register)
UPDATE dbo.MenuItems SET Icon = 'bi-bell' WHERE IdMenu = 'E70091F1-596F-4D0D-8E59-025259908549'; -- Comunicados (antes: fas fa-bell)
UPDATE dbo.MenuItems SET Icon = 'bi-sliders' WHERE IdMenu = '608F146A-4505-463D-876A-06B12CC40BBA'; -- Parámetros (antes: fas fa-sliders-h)
UPDATE dbo.MenuItems SET Icon = 'bi-hammer' WHERE IdMenu = 'D3C91599-FF24-4E5E-AEED-0C7904E3AC6E'; -- Autorizar gastos (antes: fas fa-gavel)
UPDATE dbo.MenuItems SET Icon = 'bi-file-earmark-arrow-up' WHERE IdMenu = '947E6BC2-72B0-4738-9683-12F307D9C289'; -- Exportar recibos (antes: fas fa-file-export)
UPDATE dbo.MenuItems SET Icon = 'bi-gear' WHERE IdMenu = '6ACF696A-49F0-445C-8CFA-15F65D5C1A81'; -- Configuración (antes: fas fa-gear)
UPDATE dbo.MenuItems SET Icon = 'bi-receipt' WHERE IdMenu = 'BDD2A071-85A1-4786-A963-1AD08B56EAC8'; -- Conciliación (antes: fas fa-receipt)
UPDATE dbo.MenuItems SET Icon = 'bi-chat-dots' WHERE IdMenu = '0DAA1EB5-7D1F-475C-B240-1DA0B57F0644'; -- Incidencias y Comun. (antes: fas fa-message)
UPDATE dbo.MenuItems SET Icon = 'bi-file-earmark-check' WHERE IdMenu = '730586AA-FA9E-465E-927F-211A4E1DFD1A'; -- Presupuestos (antes: fas fa-file-signature)
UPDATE dbo.MenuItems SET Icon = 'bi-cash-coin' WHERE IdMenu = '0A1CC2F2-6287-46F3-BE37-218A0E7F6206'; -- Presupuesto (antes: fas fa-file-invoice-dollar)
UPDATE dbo.MenuItems SET Icon = 'bi-calendar-check' WHERE IdMenu = 'D15B2E1B-B0FC-4580-A2DB-242D2D6D2FF6'; -- Reunión de Prop. (antes: fas fa-calendar-check)
UPDATE dbo.MenuItems SET Icon = 'bi-droplet' WHERE IdMenu = '1A8FE72D-EF48-418C-98F3-270D13EA6796'; -- Mi consumo de agua (antes: fas fa-water)
UPDATE dbo.MenuItems SET Icon = 'bi-layers' WHERE IdMenu = '950DD57D-96CD-46DB-AED9-272920CB5482'; -- Grupos de Unidades (antes: fas fa-layer-group)
UPDATE dbo.MenuItems SET Icon = 'bi-file-earmark-arrow-down' WHERE IdMenu = '4AA2004A-4CE2-4300-8FE2-325F8D748E0A'; -- Estado de cuenta (antes: fas fa-file-import)
UPDATE dbo.MenuItems SET Icon = 'bi-exclamation-circle' WHERE IdMenu = 'E19A0644-403F-4764-9C2E-3C73708BA4A5'; -- Incidencias (antes: fas fa-circle-exclamation)
UPDATE dbo.MenuItems SET Icon = 'bi-person' WHERE IdMenu = '9CADF66E-2A5B-447E-BD5A-4B894F7132EB'; -- Mi perfil (antes: fas fa-user)
UPDATE dbo.MenuItems SET Icon = 'bi-sliders' WHERE IdMenu = '260CD1AB-8D31-4DB3-9926-607384A01327'; -- Confg. Periodo (antes: fas fa-sliders-h)
UPDATE dbo.MenuItems SET Icon = 'bi-check2-all' WHERE IdMenu = '65D98A89-3EBE-4F00-8A35-75581B70123B'; -- Aprob. Presupuesto (antes: fas fa-check-double)
UPDATE dbo.MenuItems SET Icon = 'bi-list-ul' WHERE IdMenu = '13CF0AEB-D090-41EC-8A16-82C02808CF8D'; -- Ver comunicados (antes: fas fa-list)
UPDATE dbo.MenuItems SET Icon = 'bi-house-door' WHERE IdMenu = 'C30303F7-DF5D-4526-976E-85C0881A1C79'; -- Portal del Residente (antes: fas fa-house-user)
UPDATE dbo.MenuItems SET Icon = 'bi-people' WHERE IdMenu = '10A205D5-D823-4511-8C73-8FC766CAC73E'; -- Usuarios (antes: fa-solid fa-users)
UPDATE dbo.MenuItems SET Icon = 'bi-link-45deg' WHERE IdMenu = 'C2029674-05FE-4310-B9F9-9002603F7F2C'; -- Asignar unidades (antes: fas fa-link)
UPDATE dbo.MenuItems SET Icon = 'bi-percent' WHERE IdMenu = 'F58D9DE4-9515-4EA2-85A5-94821984A59E'; -- Morosidad (antes: fas fa-percent)
UPDATE dbo.MenuItems SET Icon = 'bi-bar-chart' WHERE IdMenu = '77D3827B-4D28-4B4D-B872-96FFEFEB90B4'; -- Ejecución Preps. (antes: fas fa-chart-bar)
UPDATE dbo.MenuItems SET Icon = 'bi-graph-up-arrow' WHERE IdMenu = '816542AE-4BD4-4423-9362-97310D9BEB17'; -- Ver presupuesto (antes: fas fa-chart-pie)
UPDATE dbo.MenuItems SET Icon = 'bi-shield-lock' WHERE IdMenu = 'CF5DC86C-AF3E-4B70-91A8-A0EA7D730CD4'; -- Seguridad (antes: fas fa-shield)
UPDATE dbo.MenuItems SET Icon = 'bi-clock-history' WHERE IdMenu = '940F1D20-124A-43CA-9989-A65BC97C11AB'; -- Historial (antes: fas fa-clock-rotate-left)
UPDATE dbo.MenuItems SET Icon = 'bi-info-circle' WHERE IdMenu = 'E52FB66E-C164-4B20-9154-A7FFEE264AC6'; -- Acerca de (antes: fas fa-info-circle)
UPDATE dbo.MenuItems SET Icon = 'bi-clock' WHERE IdMenu = 'DF9863D5-4545-4B55-BF6C-A96EE0643ABD'; -- Convocar asamblea (antes: fas fa-clock)
UPDATE dbo.MenuItems SET Icon = 'bi-graph-up' WHERE IdMenu = 'D61790BD-72F8-43B1-98B2-ABD87C8DBAB1'; -- Reportes financieros (antes: fas fa-chart-line)
UPDATE dbo.MenuItems SET Icon = 'bi-list' WHERE IdMenu = '90F0C916-065E-436A-85FE-AC82B10C2689'; -- Administrar Menú (antes: fas fa-bars me-2)
UPDATE dbo.MenuItems SET Icon = 'bi-person-badge' WHERE IdMenu = '467674F2-BD3B-41C7-A4FB-B13B5152B914'; -- Junta de Propietarios (antes: fas fa-user-tie)
UPDATE dbo.MenuItems SET Icon = 'bi-file-earmark-pdf' WHERE IdMenu = 'A926ADEB-B71B-4CD7-A408-B2DD4ED111E1'; -- Emitir recibos PDF (antes: fas fa-file-pdf)
UPDATE dbo.MenuItems SET Icon = 'bi-droplet' WHERE IdMenu = '79F8874C-C44A-45D1-885C-B2F65A06E67E'; -- Consumo x unidad (antes: fas fa-water)
UPDATE dbo.MenuItems SET Icon = 'bi-people-fill' WHERE IdMenu = 'AD7D3C45-5D14-4D54-A70D-B47136D65D49'; -- Residentes (antes: fas fa-user-group)
UPDATE dbo.MenuItems SET Icon = 'bi-tags' WHERE IdMenu = 'C60CBF37-912B-4A8B-9108-B80722D86726'; -- Categorías (antes: fa-solid fa-tags)
UPDATE dbo.MenuItems SET Icon = 'bi-exclamation-triangle' WHERE IdMenu = '6FF2FA10-7E13-4349-82E3-C154D00053D8'; -- Reportar incidencia (antes: fas fa-triangle-exclamation)
UPDATE dbo.MenuItems SET Icon = 'bi-pie-chart' WHERE IdMenu = '34FD5E0D-CA9B-4193-AA4D-D046C95E4D92'; -- Reportes (antes: fa-solid fa-chart-pie)
UPDATE dbo.MenuItems SET Icon = 'bi-pencil-square' WHERE IdMenu = 'EDC8B933-B231-4906-A391-D0EFEDD50ED5'; -- Edificio (antes: fas fa-pen-to-square)
UPDATE dbo.MenuItems SET Icon = 'bi-file-earmark-text' WHERE IdMenu = 'CC2A3189-E289-4ED2-8163-D76FC945BE8B'; -- Actas de reunión (antes: fas fa-file-lines)
UPDATE dbo.MenuItems SET Icon = 'bi-megaphone' WHERE IdMenu = '2166F900-1896-43A7-96C5-E23046792060'; -- Crear comunicado (antes: fas fa-bullhorn)
UPDATE dbo.MenuItems SET Icon = 'bi-building' WHERE IdMenu = 'D7991968-BA5D-47B8-AEA3-E2CA0D6F9BE3'; -- Adm. Edificio (antes: fas fa-building)
UPDATE dbo.MenuItems SET Icon = 'bi-wallet2' WHERE IdMenu = '25B61CEB-29AA-4179-8785-EF27C69B7FDC'; -- Listado de Cuotas (antes: fas fa-hand-holding-usd)
UPDATE dbo.MenuItems SET Icon = 'bi-person-vcard' WHERE IdMenu = '16EA23B6-CD1F-41E2-BA7B-F0B3FDEBFEFB'; -- Roles y Permisos (antes: fas fa-user-tag me-2)
UPDATE dbo.MenuItems SET Icon = 'bi-house' WHERE IdMenu = 'E8FE8BA6-291E-4CAF-980F-F2BE630FD5E5'; -- Dashboard (antes: fa-solid fa-house)
UPDATE dbo.MenuItems SET Icon = 'bi-receipt-cutoff' WHERE IdMenu = 'A53FC2DE-0191-49BE-9EDF-F84B084C754C'; -- Mis recibos (antes: fas fa-file-invoice)
UPDATE dbo.MenuItems SET Icon = 'bi-person-gear' WHERE IdMenu = '1E825A3E-EDFD-4025-988E-F8A1CF26F969'; -- Asignar Roles (antes: fas fa-users-cog me-2)
UPDATE dbo.MenuItems SET Icon = 'bi-droplet' WHERE IdMenu = '16FEAD5E-5CA9-47A8-8800-FABFCF13C1F4'; -- Lecturas de agua (antes: fas fa-water)
UPDATE dbo.MenuItems SET Icon = 'bi-arrow-left-right' WHERE IdMenu = 'D12E3B2B-3305-4C69-A787-FC286569F6F8'; -- Conciliación (antes: fas fa-arrow-right-arrow-left)

-- Deberían ser 49 filas afectadas en total (una por cada MenuItem existente al momento
-- de generar este script). Si el número no cuadra, hagan ROLLBACK e investiguen antes de
-- confirmar.
-- COMMIT;
-- ROLLBACK;
