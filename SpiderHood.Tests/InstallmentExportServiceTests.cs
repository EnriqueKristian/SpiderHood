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

    [Fact]
    public void CalculateItemAmount_MultipleGroupsExoneratedFromSameCategory_AllPayZero()
    {
        // Antes se comparaba contra exonerations.Where(...).Select(...).FirstOrDefault(),
        // así que con MÁS DE UN grupo exonerado de la misma categoría solo el primero de
        // la lista se libraba de verdad -- el segundo caía al cálculo normal y salía
        // cobrado. Mismo fix ya aplicado en BudgetState.cs CalculateQuota().
        var groupA = Guid.NewGuid();
        var groupB = Guid.NewGuid();
        var category = Guid.NewGuid();
        var owners = new List<OwnerUnitView> { MakeUnit(groupA, Guid.NewGuid()), MakeUnit(groupB, Guid.NewGuid()) };

        var building = new Building { Configuration = new BuildingConfiguration() };
        var exonerations = new List<Exoneration>
        {
            new() { IdGroupUnit = groupA, IdCategory = category },
            new() { IdGroupUnit = groupB, IdCategory = category },
        };

        var service = new InstallmentExportService(
            installments: [],
            budget: new BudgetHeader(),
            waterReadings: [],
            exonerations: exonerations,
            building: building,
            categories: [],
            owners: owners);

        var item = new BudgetDetail { Type = 1, MonthlyAmount = 500m, IdCategory = category };

        Assert.Equal(0m, InvokeCalculateItemAmount(service, new Installment { IdGroupUnit = groupA }, item));
        Assert.Equal(0m, InvokeCalculateItemAmount(service, new Installment { IdGroupUnit = groupB }, item));
    }

    [Fact]
    public void CalculateItemAmount_ForWaterCategory_ExoneratedGroupPaysZero_AndOthersAbsorbTheDifference()
    {
        // El agua áreas comunes hoy no aplicaba exoneraciones a su divisor -- a pedido
        // explícito del negocio, ahora respeta exoneraciones igual que Fija.
        var groupA = Guid.NewGuid(); // exonerado
        var groupB = Guid.NewGuid();
        var waterCategory = Guid.NewGuid();
        var owners = new List<OwnerUnitView> { MakeUnit(groupA, Guid.NewGuid()), MakeUnit(groupB, Guid.NewGuid()) };

        var building = new Building { Configuration = new BuildingConfiguration { WaterReadingDefault = waterCategory } };
        var waterReadings = new List<ServiceReadingDetail> { new() { CalculatedAmount = 0m } };
        var exonerations = new List<Exoneration> { new() { IdGroupUnit = groupA, IdCategory = waterCategory } };

        var service = new InstallmentExportService(
            installments: [],
            budget: new BudgetHeader(),
            waterReadings: waterReadings,
            exonerations: exonerations,
            building: building,
            categories: [],
            owners: owners);

        var item = new BudgetDetail { MonthlyAmount = 100m, IdCategory = waterCategory };

        Assert.Equal(0m, InvokeCalculateItemAmount(service, new Installment { IdGroupUnit = groupA }, item));
        // Denominador ahora resta el exonerado (2-1=1): groupB absorbe el 100%, no el 50%.
        Assert.Equal(100m, InvokeCalculateItemAmount(service, new Installment { IdGroupUnit = groupB }, item));
    }

    [Fact]
    public void CalculateItemAmount_UsesFrozenNroApartments_InsteadOfCurrentBuildingComposition()
    {
        // BudgetDetail.NroApartments congela el divisor al momento de publicar (ver
        // BudgetCalculator.CalculateQuota). Si luego el edificio cambia (acá: se agrega
        // una unidad más, pasando de 2 a 3), el desglose de un presupuesto ya publicado
        // no debe cambiar -- debe seguir usando el valor congelado, no GetTotalUnits().
        var groupA = Guid.NewGuid();
        var owners = new List<OwnerUnitView>
        {
            MakeUnit(groupA, Guid.NewGuid()),
            MakeUnit(Guid.NewGuid(), Guid.NewGuid()),
            MakeUnit(Guid.NewGuid(), Guid.NewGuid()), // unidad agregada DESPUÉS de publicar
        };

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
        // NroApartments=2: el divisor real al publicar, antes de la 3ra unidad.
        var item = new BudgetDetail { Type = 1, MonthlyAmount = 100m, IdCategory = Guid.NewGuid(), NroApartments = 2 };

        var amount = InvokeCalculateItemAmount(service, installment, item);

        // 100 / 2 (congelado) = 50, no 100 / 3 (composición actual) = 33.33.
        Assert.Equal(50m, amount);
    }
}
