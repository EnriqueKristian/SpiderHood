using SpiderHood.Models;

namespace SpiderHood.Tests;

public class EmployeeTests
{
    [Fact]
    public void Employee_NombreCompleto_JoinsAndTrims()
    {
        var employee = new Employee { Nombres = "Juan", Apellidos = "Perez" };
        Assert.Equal("Juan Perez", employee.NombreCompleto);
    }

    [Fact]
    public void Employee_NombreCompleto_WithEmptyApellidos_TrimsTrailingSpace()
    {
        var employee = new Employee { Nombres = "Juan", Apellidos = "" };
        Assert.Equal("Juan", employee.NombreCompleto);
    }

    [Theory]
    [InlineData("1,2,3,4,5", new[] { 1, 2, 3, 4, 5 })]
    [InlineData("", new int[0])]
    [InlineData(" 1 , 2 ", new[] { 1, 2 })]
    [InlineData("1,abc,3", new[] { 1, 3 })]
    [InlineData("0,1,8,7", new[] { 1, 7 })]
    public void Shift_DiasSemanaList_ParsesAndFiltersOutOfRangeOrInvalidValues(string csv, int[] expected)
    {
        var shift = new Shift { DiasSemana = csv };
        Assert.Equal(expected, shift.DiasSemanaList);
    }

    [Fact]
    public void TimeEntry_TotalHoras_SumsOrdinariaAndBothExtraBrackets()
    {
        var entry = new TimeEntry { HorasOrdinarias = 8, HorasExtra25 = 1.5m, HorasExtra35 = 0.5m };
        Assert.Equal(10m, entry.TotalHoras);
    }

    [Fact]
    public void HolidayConfiguration_EsNacional_TrueWhenAccountIsNull()
    {
        Assert.True(new HolidayConfiguration { IdAccount = null }.EsNacional);
        Assert.False(new HolidayConfiguration { IdAccount = Guid.NewGuid() }.EsNacional);
    }

    [Fact]
    public void Employee_BelongsToAccount_TrueOnlyForItsOwnAccount()
    {
        var idAccount = Guid.NewGuid();
        var employee = new Employee { IdAccount = idAccount };

        Assert.True(employee.BelongsToAccount(idAccount));
        Assert.False(employee.BelongsToAccount(Guid.NewGuid()));
    }
}
