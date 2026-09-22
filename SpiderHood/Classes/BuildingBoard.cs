namespace SpiderHood.Models
{
    // Cargo dentro de la Junta Directiva (Docs/Pendientes-Negocio-Consolidado.md #34).
    // Otro con texto libre (BuildingBoardMember.OtroDescripcion), mismo criterio que
    // Owner.RelationshipType para casos que no encajan en el catálogo fijo.
    public enum BoardMemberRole { Presidente = 1, Secretario = 2, Tesorero = 3, Vocal = 4, Otro = 5 }

    // Una fila por PERÍODO de Junta, no por edificio -- un edificio acumula historial
    // de Juntas a lo largo de los años (útil para que un Acta futura de #21 pueda
    // referenciar "según la Junta electa el DD/MM/AAAA"). Sólo una Junta activa por
    // edificio a la vez -- INS_BuildingBoard cierra cualquier otra automáticamente.
    public class BuildingBoard
    {
        public Guid IdBuildingBoard { get; set; }
        public Guid IdBuilding { get; set; }
        public DateTime FechaInicio { get; set; }
        public DateTime? FechaFin { get; set; }
        public bool IsActive { get; set; } = true;
        public Guid CreatedBy { get; set; }
        public DateTime CreatedOn { get; set; }
    }

    public class BuildingBoardMember
    {
        public Guid IdBuildingBoardMember { get; set; }
        public Guid IdBuildingBoard { get; set; }
        public Guid IdUser { get; set; }
        public BoardMemberRole Cargo { get; set; }
        // Sólo tiene sentido con Cargo == Otro.
        public string? OtroDescripcion { get; set; }
    }

    // Fila de GET_BuildingBoardMembers -- denormalizada (nombre/email del User vía
    // JOIN) para pintar la pestaña "Junta Directiva" de BuildingPage.razor sin un
    // round-trip aparte por miembro, mismo criterio que AccountUserView.
    public class BuildingBoardMemberView
    {
        public Guid IdBuildingBoardMember { get; set; }
        public Guid IdBuildingBoard { get; set; }
        public Guid IdUser { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public BoardMemberRole Cargo { get; set; }
        public string? OtroDescripcion { get; set; }

        public string FullName => $"{FirstName} {LastName}".Trim();
    }
}
