using SpiderHood.Models;

namespace SpiderHood.Tests;

public class InstallmentExtensionsTests
{
    [Theory]
    [InlineData("PEN", "S/")]
    [InlineData("USD", "$")]
    [InlineData("EUR", "€")]
    [InlineData(null, "S/")]
    [InlineData("", "S/")]
    [InlineData("XXX", "XXX")]
    public void ToCurrencySymbol_MapsKnownCodesAndFallsBackToInputOrDefault(string? code, string expected)
    {
        Assert.Equal(expected, code.ToCurrencySymbol());
    }

    [Fact]
    public void FormatoMoneda_UsesCurrencySymbolAndInvariantThousandsSeparator()
    {
        Assert.Equal("$ 1,234.50", 1234.50m.FormatoMoneda("USD"));
    }

    [Fact]
    public void FormatoMoneda_WithoutCurrencyCode_DefaultsToSoles()
    {
        Assert.Equal("S/ 10.00", 10m.FormatoMoneda());
    }

    [Fact]
    public void FormatoPorcentaje_AppendsPercentSign()
    {
        Assert.Equal("12.50%", 12.5m.FormatoPorcentaje());
    }

    [Fact]
    public void FormatoFecha_UsesDayMonthYear()
    {
        Assert.Equal("05/03/2026", new DateTime(2026, 3, 5).FormatoFecha());
    }

    [Fact]
    public void FormatoFechaHora_IncludesTime()
    {
        Assert.Equal("05/03/2026 14:30", new DateTime(2026, 3, 5, 14, 30, 0).FormatoFechaHora());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    [InlineData(-1)]
    public void GetNombreMes_ReturnsUnknownForOutOfRangeMonth(int mes)
    {
        Assert.Equal("Desconocido", mes.GetNombreMes());
    }

    [Fact]
    public void GetMesesSelectList_ReturnsTwelveMonthsInOrder()
    {
        var meses = InstallmentExtensions.GetMesesSelectList();

        Assert.Equal(12, meses.Count);
        Assert.Equal("1", meses[0].Value);
        Assert.Equal("12", meses[11].Value);
    }

    [Fact]
    public void GetAniosSelectList_ReturnsRequestedRangeDescending()
    {
        var anios = InstallmentExtensions.GetAniosSelectList(aniosAtras: 2, aniosAdelante: 1);
        var anioActual = DateTime.Now.Year;

        Assert.Equal(4, anios.Count);
        Assert.Equal((anioActual + 1).ToString(), anios.First().Value);
        Assert.Equal((anioActual - 2).ToString(), anios.Last().Value);
    }

    [Fact]
    public void CalcularMontoPorcentual_RoundsToTwoDecimals()
    {
        Assert.Equal(33.33m, 100m.CalcularMontoPorcentual(33.333m));
    }

    [Fact]
    public void CalcularMontoFijo_DistributesRemainderSoTotalMatchesExactly()
    {
        // 100 / 3 no es exacto: el ajuste por redondeo debe evitar perder/ganar centavos.
        var montoPorDepartamento = 100m.CalcularMontoFijo(3);
        var totalDistribuido = montoPorDepartamento * 3;

        Assert.Equal(100m, totalDistribuido);
    }

    [Fact]
    public void CalcularMontoFijo_WithZeroOrNegativeUnits_ReturnsZero()
    {
        Assert.Equal(0m, 100m.CalcularMontoFijo(0));
        Assert.Equal(0m, 100m.CalcularMontoFijo(-5));
    }
}
