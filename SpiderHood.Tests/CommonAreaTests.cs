using SpiderHood.Models;

namespace SpiderHood.Tests;

public class CommonAreaTests
{
    [Fact]
    public void Clone_CopiesAllFieldsIncludingNullableOnes()
    {
        var original = new CommonArea
        {
            IdCommonArea = Guid.NewGuid(),
            Nombre = "Piscina",
            AforoMaximo = null,
            DiasMinimosSinPenalidad = null,
            GarantiaInternos = 100m,
            GarantiaExternos = 200m,
            PenalidadCancelacionHabilitada = true
        };

        var clone = original.Clone();

        Assert.Equal(original.Nombre, clone.Nombre);
        Assert.Null(clone.AforoMaximo);
        Assert.Null(clone.DiasMinimosSinPenalidad);
        Assert.Equal(100m, clone.GarantiaInternos);
        Assert.True(clone.PenalidadCancelacionHabilitada);
    }

    [Fact]
    public void Clone_IsIndependentFromOriginal()
    {
        var original = new CommonArea { Nombre = "Piscina", Activo = true };

        var clone = original.Clone();
        original.Nombre = "Sala de eventos";
        original.Activo = false;

        Assert.Equal("Piscina", clone.Nombre);
        Assert.True(clone.Activo);
    }
}
