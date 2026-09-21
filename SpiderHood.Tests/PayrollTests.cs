using SpiderHood.Models;

namespace SpiderHood.Tests;

public class PayrollTests
{
    [Theory]
    [InlineData(nameof(LaborRegimeType.Microempresa), 15, false, false, false)]
    [InlineData(nameof(LaborRegimeType.PequenaEmpresa), 15, true, false, true)]
    [InlineData(nameof(LaborRegimeType.RegimenGeneral), 30, true, true, true)]
    public void LaborRegimeConfiguration_DerivesBenefitsFromTipoRegimen(
        string tipoRegimen, int diasVacacionesEsperados, bool gratificacion, bool asignacionFamiliar, bool esSalud)
    {
        var config = new LaborRegimeConfiguration { TipoRegimen = tipoRegimen };

        Assert.Equal(diasVacacionesEsperados, config.DiasVacationPorAnio);
        Assert.Equal(gratificacion, config.AplicaGratificacion);
        Assert.Equal(asignacionFamiliar, config.AplicaAsignacionFamiliar);
        Assert.Equal(esSalud, config.AplicaEsSalud);
    }

    [Fact]
    public void LaborRegimeConfiguration_WithUnknownTipoRegimen_TreatsItAsNotMicroempresa()
    {
        // AplicaGratificacion/AplicaEsSalud comparan con "!= Microempresa", no con una
        // lista cerrada de valores válidos -- así que un TipoRegimen inválido activa
        // esos beneficios igual que PequeñaEmpresa/RegimenGeneral (posible bug/falta de
        // validación, documentado tal cual está implementado hoy).
        var config = new LaborRegimeConfiguration { TipoRegimen = "ValorInvalido" };

        Assert.Equal(15, config.DiasVacationPorAnio);
        Assert.True(config.AplicaGratificacion);
        Assert.False(config.AplicaAsignacionFamiliar);
        Assert.True(config.AplicaEsSalud);
    }

    [Fact]
    public void LeaveRequest_DiasSolicitados_CountsBothEndpointsInclusive()
    {
        var request = new LeaveRequest
        {
            FechaInicio = new DateTime(2026, 3, 1),
            FechaFin = new DateTime(2026, 3, 5)
        };

        Assert.Equal(5, request.DiasSolicitados);
    }

    [Fact]
    public void LeaveRequest_DiasSolicitados_SameDay_ReturnsOne()
    {
        var day = new DateTime(2026, 3, 1);
        var request = new LeaveRequest { FechaInicio = day, FechaFin = day };

        Assert.Equal(1, request.DiasSolicitados);
    }

    [Fact]
    public void LeaveRequest_DiasSolicitados_WithFechaFinBeforeFechaInicio_ReturnsNegative()
    {
        // Sin protección contra fechas invertidas -- documenta el comportamiento actual.
        var request = new LeaveRequest
        {
            FechaInicio = new DateTime(2026, 3, 5),
            FechaFin = new DateTime(2026, 3, 1)
        };

        Assert.Equal(-3, request.DiasSolicitados);
    }

    [Fact]
    public void VacationBalance_DiasPendientes_SubtractsGozadosFromGanados()
    {
        var balance = new VacationBalance { DiasGanados = 30, DiasGozados = 10 };
        Assert.Equal(20, balance.DiasPendientes);
    }

    [Fact]
    public void VacationBalance_DiasPendientes_WithMoreGozadosThanGanados_IsNegative()
    {
        var balance = new VacationBalance { DiasGanados = 10, DiasGozados = 15 };
        Assert.Equal(-5, balance.DiasPendientes);
    }

    [Fact]
    public void Payslip_NombrePeriodo_FormatsSpanishMonthAndYear()
    {
        var payslip = new Payslip { Anio = 2026, Mes = 3 };
        Assert.Equal("Marzo 2026", payslip.NombrePeriodo);
    }

    [Fact]
    public void Payslip_NombrePeriodo_WithInvalidMonth_Throws()
    {
        var payslip = new Payslip { Anio = 2026, Mes = 13 };
        Assert.Throws<ArgumentOutOfRangeException>(() => payslip.NombrePeriodo);
    }

    [Fact]
    public void Payslip_NombreEmployee_JoinsAndTrims()
    {
        var payslip = new Payslip { Nombres = "Carlos", Apellidos = "Ruiz" };
        Assert.Equal("Carlos Ruiz", payslip.NombreEmployee);
    }

    [Fact]
    public void Payslip_BelongsToAccount_TrueOnlyForItsOwnAccount()
    {
        var idAccount = Guid.NewGuid();
        var payslip = new Payslip { IdAccount = idAccount };

        Assert.True(payslip.BelongsToAccount(idAccount));
        Assert.False(payslip.BelongsToAccount(Guid.NewGuid()));
    }

    [Fact]
    public void Payslip_BelongsToAccount_WithNullIdAccount_IsAlwaysFalse()
    {
        // El join contra Employee puede no traer IdAccount (dato faltante) -- en ese
        // caso nunca debe considerarse que "pertenece" a la Cuenta del Administrador.
        var payslip = new Payslip { IdAccount = null };

        Assert.False(payslip.BelongsToAccount(Guid.NewGuid()));
    }
}
