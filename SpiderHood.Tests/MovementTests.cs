using SpiderHood.Models;

namespace SpiderHood.Tests;

public class MovementTests
{
    [Fact]
    public void TransactionBankDetail_NegativeAmount_IsClassifiedAsGasto()
    {
        var detail = new TransactionBankDetail { Amount = -150m, SequenceNumber = 7 };

        Assert.Equal("G0007", detail.Reference);
        Assert.Equal("Gasto #0007", detail.Notes);
        Assert.Equal("Gasto", detail.Tipo);
    }

    [Fact]
    public void TransactionBankDetail_PositiveAmount_IsClassifiedAsIngreso()
    {
        var detail = new TransactionBankDetail { Amount = 150m, SequenceNumber = 3 };

        Assert.Equal("I0003", detail.Reference);
        Assert.Equal("Ingreso #0003", detail.Notes);
        Assert.Equal("Ingreso", detail.Tipo);
    }

    [Fact]
    public void TransactionBankDetail_FinalAmount_AddsItfToAmount()
    {
        var detail = new TransactionBankDetail { Amount = 100m, ITF = 0.5m };
        Assert.Equal(100.5m, detail.FinalAmount);
    }

    [Fact]
    public void TransactionBankDetail_KeyDuplicate_FormatsDateDescriptionAndAmountWithTwoDecimals()
    {
        var detail = new TransactionBankDetail
        {
            StatementDate = new DateTime(2026, 3, 5),
            Description = "Pago mantenimiento",
            Amount = 150m
        };

        Assert.Equal("20260305|Pago mantenimiento|150.00", detail.KeyDuplicate);
    }

    [Fact]
    public void MovDetKey_DbKey_UsesDefaultAmountFormattingNotTwoDecimals()
    {
        // A diferencia de TransactionBankDetail.KeyDuplicate (que fuerza "F2"), DbKey usa
        // el formato por defecto del decimal -- no son intercambiables aunque representen
        // la misma transacción.
        var key = new MovDetKey
        {
            StatementDate = new DateTime(2026, 3, 5),
            Description = "Pago mantenimiento",
            Amount = 150m
        };

        Assert.Equal("20260305|Pago mantenimiento|150", key.DbKey);
        Assert.NotEqual(
            new TransactionBankDetail { StatementDate = key.StatementDate, Description = key.Description, Amount = key.Amount }.KeyDuplicate,
            key.DbKey);
    }
}
