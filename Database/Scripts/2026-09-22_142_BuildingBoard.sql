-- =============================================================================
-- Constitución de la Junta Directiva (Presidente/Secretario/Tesorero/Vocal)
-- (Docs/Pendientes-Negocio-Consolidado.md #34) -- hasta hoy "Junta" era sólo
-- un Rol (UserBuildingAssociation), sin ningún registro de quién ocupa qué
-- cargo ni desde cuándo. Es un prerrequisito real de las Actas de gobernanza
-- ya planeadas en #21 (asumen firmas de presidente/secretario).
--
-- BuildingBoard: una fila por PERÍODO de Junta (no por edificio) -- un
-- edificio acumula historial a lo largo de los años, útil para que las
-- Actas referencien "según la Junta electa el DD/MM/AAAA". Sólo una Junta
-- activa (IsActive=1) por edificio a la vez -- CreateBoardAsync cierra
-- cualquier Junta previamente activa antes de crear la nueva.
--
-- BuildingBoardMember: un User por cargo, referencia directa a Users (no a
-- Owner -- el usuario decidido en #34 fue validar la pertenencia a
-- propietario como ADVERTENCIA blanda desde la aplicación, no como
-- constraint de BD, dado que hoy no existe vínculo User-Owner confiable).
-- Cargo es un INT que respalda Models.BoardMemberRole (1=Presidente,
-- 2=Secretario, 3=Tesorero, 4=Vocal, 5=Otro) -- mismo criterio que
-- Account.AccountType. OtroDescripcion sólo tiene sentido con Cargo=Otro.
--
-- Idempotente: se puede correr más de una vez.
-- =============================================================================

SET NOCOUNT ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'BuildingBoard')
BEGIN
    CREATE TABLE dbo.BuildingBoard (
        IdBuildingBoard UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        IdBuilding UNIQUEIDENTIFIER NOT NULL,
        FechaInicio DATETIME2 NOT NULL,
        FechaFin DATETIME2 NULL,
        IsActive BIT NOT NULL DEFAULT (1),
        CreatedBy UNIQUEIDENTIFIER NOT NULL,
        CreatedOn DATETIME2 NOT NULL,
        CONSTRAINT FK_BuildingBoard_Building FOREIGN KEY (IdBuilding) REFERENCES dbo.Building(IdBuilding)
    );
    CREATE INDEX IX_BuildingBoard_Building ON dbo.BuildingBoard(IdBuilding, IsActive);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'BuildingBoardMember')
BEGIN
    CREATE TABLE dbo.BuildingBoardMember (
        IdBuildingBoardMember UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        IdBuildingBoard UNIQUEIDENTIFIER NOT NULL,
        IdUser UNIQUEIDENTIFIER NOT NULL,
        Cargo INT NOT NULL,
        OtroDescripcion NVARCHAR(100) NULL,
        CONSTRAINT FK_BuildingBoardMember_Board FOREIGN KEY (IdBuildingBoard) REFERENCES dbo.BuildingBoard(IdBuildingBoard)
    );
    CREATE INDEX IX_BuildingBoardMember_Board ON dbo.BuildingBoardMember(IdBuildingBoard);
END
GO

CREATE OR ALTER PROCEDURE dbo.INS_BuildingBoard
    @IdBuildingBoard UNIQUEIDENTIFIER,
    @IdBuilding UNIQUEIDENTIFIER,
    @FechaInicio DATETIME2,
    @CreatedBy UNIQUEIDENTIFIER,
    @CreatedOn DATETIME2
AS
BEGIN
    SET NOCOUNT ON;
    -- Sólo una Junta activa por edificio a la vez -- cierra cualquier otra
    -- antes de insertar la nueva (BuildingBoardService.CreateBoardAsync no
    -- depende de que el caller haya cerrado la anterior a mano).
    UPDATE dbo.BuildingBoard
    SET IsActive = 0, FechaFin = ISNULL(FechaFin, @FechaInicio)
    WHERE IdBuilding = @IdBuilding AND IsActive = 1;

    INSERT INTO dbo.BuildingBoard (IdBuildingBoard, IdBuilding, FechaInicio, FechaFin, IsActive, CreatedBy, CreatedOn)
    VALUES (@IdBuildingBoard, @IdBuilding, @FechaInicio, NULL, 1, @CreatedBy, @CreatedOn);
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_ActiveBuildingBoard
    @IdBuilding UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP 1
        IdBuildingBoard,
        IdBuilding,
        FechaInicio,
        FechaFin,
        IsActive,
        CreatedBy,
        CreatedOn
    FROM dbo.BuildingBoard
    WHERE IdBuilding = @IdBuilding AND IsActive = 1
    ORDER BY FechaInicio DESC;
END
GO

CREATE OR ALTER PROCEDURE dbo.INS_BuildingBoardMember
    @IdBuildingBoardMember UNIQUEIDENTIFIER,
    @IdBuildingBoard UNIQUEIDENTIFIER,
    @IdUser UNIQUEIDENTIFIER,
    @Cargo INT,
    @OtroDescripcion NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.BuildingBoardMember (IdBuildingBoardMember, IdBuildingBoard, IdUser, Cargo, OtroDescripcion)
    VALUES (@IdBuildingBoardMember, @IdBuildingBoard, @IdUser, @Cargo, @OtroDescripcion);
END
GO

CREATE OR ALTER PROCEDURE dbo.DEL_BuildingBoardMember
    @IdBuildingBoardMember UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM dbo.BuildingBoardMember WHERE IdBuildingBoardMember = @IdBuildingBoardMember;
END
GO

-- Denormalizada (nombre/email del User vía JOIN) para pintar la pestaña
-- "Junta Directiva" de BuildingPage.razor sin round-trips aparte por
-- miembro, mismo criterio que GET_AccountUsersByAccount.
CREATE OR ALTER PROCEDURE dbo.GET_BuildingBoardMembers
    @IdBuildingBoard UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        m.IdBuildingBoardMember,
        m.IdBuildingBoard,
        m.IdUser,
        u.FirstName,
        u.LastName,
        u.Email,
        m.Cargo,
        m.OtroDescripcion
    FROM dbo.BuildingBoardMember m
    INNER JOIN dbo.Users u ON u.IdUser = m.IdUser
    WHERE m.IdBuildingBoard = @IdBuildingBoard
    ORDER BY m.Cargo;
END
GO
