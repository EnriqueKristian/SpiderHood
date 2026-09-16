using SpiderHood.Models;

namespace SpiderHood.Tests;

public class OwnerTests
{
    [Fact]
    public void Owner_FullName_JoinsNamesAndSurname()
    {
        var owner = new Owner { Names = "Maria", Surname = "Lopez" };
        Assert.Equal("Maria Lopez", owner.FullName);
    }

    [Fact]
    public void Owner_FullName_WithNullSurname_KeepsTrailingSpace()
    {
        // FullName usa $"{Names} {Surname ?? ""}" -- a diferencia de Employee.NombreCompleto
        // esto NO hace .Trim(), así que un Surname nulo deja un espacio final.
        var owner = new Owner { Names = "Maria", Surname = null };
        Assert.Equal("Maria ", owner.FullName);
    }

    [Fact]
    public void OwnerUnit_Floor_ParsesFloorNumberFromGroupNameFormat()
    {
        // GroupName sigue el formato "Piso NNN" (ver comentario en Owner.cs) -- Floor
        // toma la segunda palabra y la divide entre 100 (ej. "Piso 205" -> piso 2).
        var unit = new OwnerUnit { GroupName = "Piso 205" };
        Assert.Equal(2, unit.Floor);
    }

    [Fact]
    public void OwnerUnit_Floor_WithGroupNameMissingTheNumberPart_ThrowsIndexOutOfRange()
    {
        // Caso borde real: si GroupName no tiene un segundo "token" separado por
        // espacio, Floor explota en vez de devolver algo razonable -- documentamos
        // el comportamiento actual (potencial bug) con un test de excepción.
        var unit = new OwnerUnit { GroupName = "SinEspacio" };
        Assert.Throws<IndexOutOfRangeException>(() => unit.Floor);
    }

    [Fact]
    public void OwnerUnit_Floor_WithNonNumericSecondToken_ThrowsFormatException()
    {
        var unit = new OwnerUnit { GroupName = "Piso ABC" };
        Assert.Throws<FormatException>(() => unit.Floor);
    }

    [Fact]
    public void OwnerUnit_Estado_IsAlwaysActivo()
    {
        // Estado devuelve el literal "Activo" sin importar el estado real de la
        // unidad -- documentado tal cual está implementado hoy (posible bug/TODO).
        var unit = new OwnerUnit();
        Assert.Equal("Activo", unit.Estado);
    }
}
