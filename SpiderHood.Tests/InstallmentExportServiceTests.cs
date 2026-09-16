using System.Reflection;
using SpiderHood.Models;

namespace SpiderHood.Tests;

// Docs/Pendientes-Negocio-Consolidado.md #28 -- InstallmentExportService.CalculateItemAmount
// es el método que arma el desglose impreso en el PDF del recibo. Antes no pesaba los ítems
// Fijos/Agua por la cantidad real de unidades del grupo (pesoFija), así que una cuota que
// agrupa más de una unidad (el grupo "Inmobiliaria", con varias unidades sin vender) se veía
// subestimada en el PDF aunque el monto real cobrado (Installment.Amount, calculado por
// BudgetCalculator.CalculateQuota) fuera correcto. CalculateItemAmount es privado -- se invoca
// por reflection para no tener que exponerlo sólo para testear, ni renderizar el PDF completo
// y parsear su texto (fragilísimo).
public class InstallmentExportServiceTests
{
    private static OwnerUnitView MakeUnit(Guid idGroupUnit, Guid idUnit) => new()
    {
        IdGroupUnit = idGroupUnit,
        IdUnit = idUnit,
        Role = 1,
        TypeUnit = 1, // Depto -- entra en el filtro Role==1 && TypeUnit in (1,4)
    };

    private static decimal InvokeCalculateItemAmount(InstallmentExportService service, Installment installment, BudgetDetail item)
    {
        var installmentField = typeof(InstallmentExportService).GetField("_installment", BindingFlags.NonPublic | BindingFlags.Instance)!;
        installmentField.SetValue(service, installment);

        var method = typeof(InstallmentExportService).GetMethod("CalculateItemAmount", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (decimal)method.Invoke(service, [item])!;
    }

    [Fact]
    public void CalculateItemAmount_ForFixedItem_WeighsByUnitCountInTheGroup_LikeBudgetCalculator()
    {
        var groupA = Guid.NewGuid(); // 1 unidad
        var groupB = Guid.NewGuid(); // 3 unidades (ej. Inmobiliaria)

        var owners = new List<OwnerUnitView>
        {
            MakeUnit(groupA, Guid.NewGuid()),
            MakeUnit(groupB, Guid.NewGuid()),
            MakeUnit(groupB, Guid.NewGuid()),
            MakeUnit(groupB, Guid.NewGuid()),
        };
        // totalApartments = 4 (1 + 3), pesoFija(groupB) = 3.

        var building = new Building { Configuration = new BuildingConfiguration() };
        var service = new InstallmentExportService(
            installments: [],
            budget: new BudgetHeader(),
            waterReadings: [],
            exonerations: [],
            building: building,
            categories: [],
            owners: owners);

        var installmentGroupB = new Installment { IdGroupUnit = groupB, Percent = 75 };
        var item = new BudgetDetail { Type = 1, MonthlyAmount = 300m, IdCategory = Guid.NewGuid() };

        var amount = InvokeCalculateItemAmount(service, installmentGroupB, item);

        // 300 / (4 apartamentos - 0 exoneraciones) * 3 unidades del grupo = 225.
        // Antes (sin pesoFija) esto daba 75 -- una cuota consolidada de 3 unidades se
        // veía facturada por el desglose como si tuviera 1 sola.
        Assert.Equal(225m, amount);
    }

    [Fact]
    public void CalculateItemAmount_ForFixedItem_SingleUnitGroup_MatchesPreviousBehaviour()
    {
        var groupA = Guid.NewGuid();
        var owners = new List<OwnerUnitView> { MakeUnit(groupA, Guid.NewGuid()) };

        var building = new Building { Configuration = new BuildingConfiguration() };
        var service = new InstallmentExportService(
            installments: [],
            budget: new BudgetHeader(),
            waterReadings: [],
            exonerations: [],
            building: building,
            categories: [],
            owners: owners);

        var installment = new Installment { IdGroupUnit = groupA };
        var item = new BudgetDetail { Type = 1, MonthlyAmount = 100m, IdCategory = Guid.NewGuid() };

        var amount = InvokeCalculateItemAmount(service, installment, item);

        // Un grupo de 1 sola unidad no cambia de comportamiento (pesoFija = 1).
        Assert.Equal(100m, amount);
    }

    [Fact]
    public void CalculateItemAmount_ForWaterCategory_WeighsTheSharedDifferenceByUnitCount()
    {
        var groupB = Guid.NewGuid();
        var waterCategory = Guid.NewGuid();

        var owners = new List<OwnerUnitView>
        {
            MakeUnit(Guid.NewGuid(), Guid.NewGuid()), // otro grupo, 1 unidad
            MakeUnit(groupB, Guid.NewGuid()),
            MakeUnit(groupB, Guid.NewGuid()),
            MakeUnit(groupB, Guid.NewGuid()),
        };
        // totalApartments = 4, pesoFija(groupB) = 3.

        var building = new Building { Configuration = new BuildingConfiguration { WaterReadingDefault = waterCategory } };
        var waterReadings = new List<ServiceReadingDetail> { new() { CalculatedAmount = 100m } };

        var service = new InstallmentExportService(
            installments: [],
            budget: new BudgetHeader(),
            waterReadings: waterReadings,
            exonerations: [],
            building: building,
            categories: [],
            owners: owners);

        var installmentGroupB = new Installment { IdGroupUnit = groupB };
        var item = new BudgetDetail { MonthlyAmount = 400m, IdCategory = waterCategory };

        var amount = InvokeCalculateItemAmount(service, installmentGroupB, item);

        // |400 - 100| / 4 apartamentos * 3 unidades del grupo = 225.
        Assert.Equal(225m, amount);
    }

    [Fact]
    public void CalculateItemAmount_ExoneratedGroup_StillReturnsZeroRegardlessOfUnitCount()
    {
        var groupB = Guid.NewGuid();
        var category = Guid.NewGuid();
        var owners = new List<OwnerUnitView>
        {
            MakeUnit(groupB, Guid.NewGuid()),
            MakeUnit(groupB, Guid.NewGuid()),
        };

        var building = new Building { Configuration = new BuildingConfiguration() };
        var exonerations = new List<Exoneration> { new() { IdGroupUnit = groupB, IdCategory = category } };

        var service = new InstallmentExportService(
            installments: [],
            budget: new BudgetHeader(),
            waterReadings: [],
            exonerations: exonerations,
            building: building,
            categories: [],
            owners: owners);

        var installment = new Installment { IdGroupUnit = groupB };
        var item = new BudgetDetail { Type = 1, MonthlyAmount = 500m, IdCategory = category };

        var amount = InvokeCalculateItemAmount(service, installment, item);

        Assert.Equal(0m, amount);
    }
}
