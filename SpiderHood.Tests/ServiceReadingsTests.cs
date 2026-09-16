using SpiderHood.Models;

namespace SpiderHood.Tests;

public class ServiceReadingsTests
{
    [Fact]
    public void Departamento_ConsumoActual_IsDifferenceBetweenReadings()
    {
        var dpto = new Departamento { LecturaActual = 150, LecturaAnterior = 100 };
        Assert.Equal(50, dpto.ConsumoActual);
    }

    [Fact]
    public void Departamento_ConsumoActual_WithResetMeter_NeverGoesNegative()
    {
        // Medidor reseteado (lectura actual menor que la anterior) -- debe dar 0, no negativo.
        var dpto = new Departamento { LecturaActual = 10, LecturaAnterior = 900 };
        Assert.Equal(0, dpto.ConsumoActual);
    }

    [Fact]
    public void WaterRate_Total_SumsPotableAndAlcantarillado()
    {
        var rate = new WaterRate { Potable = 3.5m, Alcantarillado = 1.5m };
        Assert.Equal(5.0m, rate.Total);
    }

    [Fact]
    public void WaterRate_DescripcionRango_WithOpenEndedMax_ShowsAMas()
    {
        var rate = new WaterRate { Id = 4, Minimo = 100, Maximo = -1 };
        Assert.Equal("R4: 100 a más", rate.DescripcionRango);
    }

    [Fact]
    public void WaterRate_DescripcionRango_WithBoundedMax_ShowsRange()
    {
        var rate = new WaterRate { Id = 1, Minimo = 0, Maximo = 20 };
        Assert.Equal("R1: 0 a 20", rate.DescripcionRango);
    }

    [Fact]
    public void CalculationResult_TotalConIGV_AppliesConfiguredIgvRate()
    {
        var result = new CalculationResult { TotalSinIGV = 100m, IGV = 0.18m };
        Assert.Equal(118m, result.TotalConIGV);
    }

    [Fact]
    public void ServiceReading_HasErrors_FalseWhenValidationErrorsIsNull()
    {
        var reading = new ServiceReading { ValidationErrors = null };
        Assert.False(reading.HasErrors);
    }

    [Fact]
    public void ServiceReadingState_ShowSaveReadings_TrueOnlyWhenStatusOneAndNoPendingOrErrors()
    {
        var state = new ServiceReadingState();
        state.CurrentReading.Status = 1;
        state.CurrentReadingDetail = [new ServiceReadingDetail { Procesed = true }];

        Assert.True(state.ShowSaveReadings);
    }

    [Fact]
    public void ServiceReadingState_ShowSaveReadings_FalseWhenAnyDetailIsUnprocessed()
    {
        var state = new ServiceReadingState();
        state.CurrentReading.Status = 1;
        state.CurrentReadingDetail = [new ServiceReadingDetail { Procesed = false }];

        Assert.False(state.ShowSaveReadings);
    }

    [Fact]
    public void ServiceReadingState_ShowSaveReadings_FalseWhenStatusIsNotOne()
    {
        var state = new ServiceReadingState();
        state.CurrentReading.Status = 0;
        state.CurrentReadingDetail = [new ServiceReadingDetail { Procesed = true }];

        Assert.False(state.ShowSaveReadings);
    }

    [Fact]
    public void ServiceReadingState_LoadFromExcel_TrueWhenStatusIsNotOne()
    {
        var state = new ServiceReadingState();
        state.CurrentReading.Status = 0;
        Assert.True(state.LoadFromExcel);

        state.CurrentReading.Status = 1;
        Assert.False(state.LoadFromExcel);
    }

    [Fact]
    public void ServiceReadingState_OpenFromModal_FalseWhenPeriodIsDefault()
    {
        var state = new ServiceReadingState();
        Assert.False(state.OpenFromModal);

        state.Period = new DateTime(2026, 3, 1);
        Assert.True(state.OpenFromModal);
    }
}
