-- =============================================================================
-- Pantalla de administración de Permisos (sólo SysAdmin,
-- Components/Pages/SettingPages/PermissionsAdmin.razor) -- pedido explícito del
-- usuario: "veo que no tenemos una pagina para administrar estos valores...
-- ayuda a no tener que crear scripts" (a raíz de tener que sembrar
-- view_income_expense_report a mano con
-- Database/Scripts/2026-09-09_76_Seed_ReportPermissions.sql).
--
-- dbo.Permissions ya existe (columnas confirmadas por el usuario con una
-- consulta directa: PermissionId, PermissionKey, Name, Description, Group) --
-- este script sólo agrega los dos SPs de escritura que nunca existieron
-- (confirmado por grep: no había ningún INS_Permission/UPD_Permission en el
-- repo ni usado en ningún otro lado), no toca la tabla.
--
-- UPD_Permission NO recibe/actualiza PermissionKey a propósito: es la clave de
-- negocio que compara IPermissionService.HasPermissionAsync (y el código en
-- general, ej. todos los "view_xxx" usados en los reportes) -- dejar que se
-- edite después de creado rompería en silencio cualquier chequeo ya escrito
-- contra el valor viejo. Si hace falta corregir un PermissionKey mal escrito,
-- es un caso manual (UPDATE directo), no parte de esta pantalla.
-- =============================================================================

CREATE OR ALTER PROCEDURE dbo.INS_Permission
    @PermissionId UNIQUEIDENTIFIER,
    @PermissionKey NVARCHAR(100),
    @Name NVARCHAR(200),
    @Description NVARCHAR(500) = NULL,
    @Group NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM dbo.Permissions WHERE PermissionKey = @PermissionKey)
    BEGIN
        RAISERROR('Ya existe un permiso con esa clave (PermissionKey).', 16, 1);
        RETURN;
    END

    INSERT INTO dbo.Permissions (PermissionId, PermissionKey, Name, Description, [Group])
    VALUES (@PermissionId, @PermissionKey, @Name, @Description, @Group);
END
GO

CREATE OR ALTER PROCEDURE dbo.UPD_Permission
    @PermissionId UNIQUEIDENTIFIER,
    @Name NVARCHAR(200),
    @Description NVARCHAR(500) = NULL,
    @Group NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.Permissions
    SET Name = @Name,
        Description = @Description,
        [Group] = @Group
    WHERE PermissionId = @PermissionId;
END
GO
