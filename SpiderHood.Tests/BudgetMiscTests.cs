using SpiderHood.Models;

namespace SpiderHood.Tests;

public class BudgetMiscTests
{
    [Fact]
    public void BankAccount_Clone_CopiesAllFieldsIndependently()
    {
        var original = new BankAccount { AccountName = "Cta Principal", AccountNumber = "123", Currency = "USD" };

        var clone = original.Clone();
        original.AccountName = "Otra cuenta";

        Assert.Equal("Cta Principal", clone.AccountName);
        Assert.Equal("123", clone.AccountNumber);
        Assert.Equal("USD", clone.Currency);
    }

    [Fact]
    public void BudgetHeader_MonthAndYear_ComeFromBudgetDate()
    {
        var header = new BudgetHeader { BudgetDate = new DateTime(2026, 3, 15) };

        Assert.Equal(3, header.Month);
        Assert.Equal(2026, header.Year);
    }

    [Fact]
    public void BudgetHeader_Mes_FormatsMonthNameUsingCurrentCulture()
    {
        var previousCulture = System.Threading.Thread.CurrentThread.CurrentCulture;
        System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("es-ES");
        try
        {
            var header = new BudgetHeader { BudgetDate = new DateTime(2026, 3, 15) };
            Assert.Equal("marzo", header.Mes);
        }
        finally
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = previousCulture;
        }
    }

    [Fact]
    public void GenerationResult_Exitoso_SetsSuccessAndCuotaId()
    {
        var result = GenerationResult.Exitoso(42);

        Assert.True(result.Exito);
        Assert.Equal(42, result.CuotaId);
        Assert.Equal("Cuota generada exitosamente", result.Mensaje);
    }

    [Fact]
    public void GenerationResult_Error_WithNullErrores_DefaultsToEmptyList()
    {
        var result = GenerationResult.Error("Fallo el cálculo", errores: null);

        Assert.False(result.Exito);
        Assert.Equal("Fallo el cálculo", result.Mensaje);
        Assert.Empty(result.Errores);
    }

    [Fact]
    public void GenerationResult_AgregarError_AddsMessageAndForcesFailure()
    {
        var result = GenerationResult.Exitoso(1);

        result.AgregarError("Algo salió mal");

        Assert.False(result.Exito);
        Assert.Contains("Algo salió mal", result.Errores);
    }

    [Fact]
    public void InstallmentViewModel_Estado_ReflectsProcesadaFlag()
    {
        var pendiente = new InstallmentViewModel { Procesada = false };
        var procesada = new InstallmentViewModel { Procesada = true };

        Assert.Equal("Pendiente", pendiente.Estado);
        Assert.Equal("warning", pendiente.EstadoColor);
        Assert.Equal("Procesada", procesada.Estado);
        Assert.Equal("success", procesada.EstadoColor);
    }

    [Fact]
    public void PendingExpenseViewModel_EstadoTexto_PrioritizesPagadoOverConsideradoEnCuota()
    {
        var pagado = new PendingExpenseViewModel { Pagado = true, ConsideradoEnCuota = true };
        var enCuota = new PendingExpenseViewModel { Pagado = false, ConsideradoEnCuota = true };
        var pendiente = new PendingExpenseViewModel { Pagado = false, ConsideradoEnCuota = false };

        Assert.Equal("Pagado", pagado.EstadoTexto);
        Assert.Equal("success", pagado.EstadoColor);

        Assert.Equal("En Cuota", enCuota.EstadoTexto);
        Assert.Equal("warning", enCuota.EstadoColor);

        Assert.Equal("Pendiente", pendiente.EstadoTexto);
        Assert.Equal("danger", pendiente.EstadoColor);
    }
}
