using SpiderHood.Models;

namespace SpiderHood.Tests;

// BudgetCalculator es el motor de cálculo de presupuestos/cuotas (BudgetState.cs) --
// la lógica más rica del repo sin dependencia de base de datos: reparte gastos Fijos
// y Proporcionales entre grupos de unidades, aplica exoneraciones y agua. Se
// instancia directo (sin pasar por BudgetState.CalculateQuota(), que además tiene un
// guard de "lectura de agua pendiente") para poder controlar totalApartments y
// aislar cada escenario.
public class BudgetCalculatorTests
{
    private static OwnerUnitView MakeOwnerUnit(Guid idGroupUnit, decimal totalArea, string unitNumber, string firstName) => new()
    {
        IdGroupUnit = idGroupUnit,
        TotalArea = totalArea,
        UnitNumber = unitNumber,
        IdUnit = Guid.NewGuid(),
        FirstName = firstName
    };

    [Fact]
    public void CalculateTotals_SkipsHeaderRowsAndAppliesFrequencyMultiplier()
    {
        var state = new BudgetState { TotalApartments = 2 };
        state.Budget.Details.AddRange(
        [
            new BudgetDetail { IsHeader = true, MonthlyAmount = 999999m },
            new BudgetDetail { IsHeader = false, Frequency = 1, MonthlyAmount = 10m }, // anual x12
            new BudgetDetail { IsHeader = false, Frequency = 2, MonthlyAmount = 10m }, // anual x6
            new BudgetDetail { IsHeader = false, Frequency = 3, MonthlyAmount = 10m }, // anual x4
            new BudgetDetail { IsHeader = false, Frequency = 4, MonthlyAmount = 10m }, // anual x3
            new BudgetDetail { IsHeader = false, Frequency = 99, MonthlyAmount = 10m }, // desconocida -> x1
        ]);

        var (monthly, annual) = new BudgetCalculator(state).CalculateTotals();

        Assert.Equal(50m, monthly); // 5 items no-header x 10, el header no cuenta
        Assert.Equal(120m + 60m + 40m + 30m + 10m, annual);
    }

    [Fact]
    public void CalculateQuota_SplitsFixedEvenlyAndProportionalByArea()
    {
        var groupA = Guid.NewGuid();
        var groupB = Guid.NewGuid();

        var state = new BudgetState
        {
            TotalArea = 200m,
            Owners = [MakeOwnerUnit(groupA, 100m, "101", "Owner A"), MakeOwnerUnit(groupB, 100m, "102", "Owner B")]
        };
        var catFixed = Guid.NewGuid();
        var catProp = Guid.NewGuid();
        state.Budget.Details.AddRange(
        [
            new BudgetDetail { IsHeader = false, Type = 1, MonthlyAmount = 100m, IdCategory = catFixed },
            new BudgetDetail { IsHeader = false, Type = 2, MonthlyAmount = 1000m, IdCategory = catProp },
        ]);

        var total = new BudgetCalculator(state).CalculateQuota(totalApartments: 2);

        // Fija: 100 / 2 apartamentos = 50 c/u. Proporcional: 1000 * (100/200) = 500 c/u.
        Assert.Equal(1100m, total);
        Assert.Equal(2, state.Installments.Count);
        Assert.All(state.Installments, i => Assert.Equal(550m, i.Amount));
        Assert.All(state.Installments, i => Assert.Equal(50m, i.Percent)); // 100 * (100/200)
    }

    [Fact]
    public void CalculateQuota_ExoneratedGroupPaysZeroAndOthersAbsorbTheDifference()
    {
        var groupA = Guid.NewGuid(); // exonerado
        var groupB = Guid.NewGuid();
        var catFixed = Guid.NewGuid();

        var state = new BudgetState
        {
            TotalArea = 200m,
            Owners = [MakeOwnerUnit(groupA, 100m, "101", "Owner A"), MakeOwnerUnit(groupB, 100m, "102", "Owner B")],
            Exonerations = [new Exoneration { IdGroupUnit = groupA, IdCategory = catFixed }]
        };
        state.Budget.Details.Add(new BudgetDetail { IsHeader = false, Type = 1, MonthlyAmount = 100m, IdCategory = catFixed });

        var total = new BudgetCalculator(state).CalculateQuota(totalApartments: 2);

        var byGroup = state.Installments.ToDictionary(i => i.IdGroupUnit, i => i.Amount);
        Assert.Equal(0m, byGroup[groupA]);
        // El denominador de Fija resta la cantidad de exonerados (2-1=1), así que el
        // grupo que sí paga absorbe el 100% del monto, no el 50% que le tocaría sin
        // exoneración.
        Assert.Equal(100m, byGroup[groupB]);
        Assert.Equal(100m, total);
    }

    [Fact]
    public void CalculateQuota_AguaAreasComunesIsFlatAndAddsToIndividualConsumption()
    {
        // Agua Áreas Comunes es una categoría Fija (Type==1) más -- el negocio
        // confirmó que son dos cargos independientes: el consumo propio del dpto
        // (agregado aparte, siempre) y el presupuesto de áreas comunes (repartido
        // flat entre unidades). Antes se restaba el consumo medido total del
        // presupuesto de la categoría, como si el segundo ya incluyera al primero --
        // eso no es como funciona el negocio.
        var groupA = Guid.NewGuid();
        var groupB = Guid.NewGuid();
        var waterCategory = Guid.NewGuid();

        var state = new BudgetState
        {
            TotalArea = 200m,
            Owners = [MakeOwnerUnit(groupA, 100m, "101", "Owner A"), MakeOwnerUnit(groupB, 100m, "102", "Owner B")],
            WaterReadings =
            [
                new ServiceReadingDetail { IdGroupUnit = groupA, CalculatedAmount = 30m },
                new ServiceReadingDetail { IdGroupUnit = groupB, CalculatedAmount = 20m },
            ],
            Configuration = new BuildingConfiguration { WaterReadingDefault = waterCategory }
        };
        state.Budget.Details.Add(new BudgetDetail { IsHeader = false, Type = 1, MonthlyAmount = 100m, IdCategory = waterCategory });

        var total = new BudgetCalculator(state).CalculateQuota(totalApartments: 2);

        var byGroup = state.Installments.ToDictionary(i => i.IdGroupUnit, i => i.Amount);
        // Consumo individual + porción flat del presupuesto (100/2=50 c/u).
        Assert.Equal(30m + 50m, byGroup[groupA]);
        Assert.Equal(20m + 50m, byGroup[groupB]);
        Assert.Equal(150m, total); // 100 (agua flat) + 50 (consumo total medido)
    }

    [Fact]
    public void CalculateQuota_AguaAreasComunesStaysFlat_EvenWhenConsumptionExceedsBudget()
    {
        // Bug real reportado: con consumo medido alto (más que el presupuesto de la
        // categoría), la resta hacía que la línea entera desapareciera (mostraba y
        // sumaba S/0.00 en el recibo). Al ser flat, el presupuesto de áreas comunes
        // no depende en absoluto del consumo medido.
        var groupA = Guid.NewGuid();
        var groupB = Guid.NewGuid();
        var waterCategory = Guid.NewGuid();

        var state = new BudgetState
        {
            TotalArea = 200m,
            Owners = [MakeOwnerUnit(groupA, 100m, "101", "Owner A"), MakeOwnerUnit(groupB, 100m, "102", "Owner B")],
            WaterReadings =
            [
                new ServiceReadingDetail { IdGroupUnit = groupA, CalculatedAmount = 75m },
                new ServiceReadingDetail { IdGroupUnit = groupB, CalculatedAmount = 75m },
            ],
            Configuration = new BuildingConfiguration { WaterReadingDefault = waterCategory }
        };
        state.Budget.Details.Add(new BudgetDetail { IsHeader = false, Type = 1, MonthlyAmount = 100m, IdCategory = waterCategory });

        var total = new BudgetCalculator(state).CalculateQuota(totalApartments: 2);

        var byGroup = state.Installments.ToDictionary(i => i.IdGroupUnit, i => i.Amount);
        // 75 (consumo individual) + 50 (100/2 flat, sin restar nada) = 125 c/u.
        Assert.Equal(125m, byGroup[groupA]);
        Assert.Equal(125m, byGroup[groupB]);
        Assert.Equal(250m, total);
    }

    [Fact]
    public void CalculateQuota_WaterExoneratedGroupPaysOnlyIndividualConsumption()
    {
        var groupA = Guid.NewGuid(); // exonerado del área común de agua
        var groupB = Guid.NewGuid();
        var waterCategory = Guid.NewGuid();

        var state = new BudgetState
        {
            TotalArea = 200m,
            Owners = [MakeOwnerUnit(groupA, 100m, "101", "Owner A"), MakeOwnerUnit(groupB, 100m, "102", "Owner B")],
            WaterReadings =
            [
                new ServiceReadingDetail { IdGroupUnit = groupA, CalculatedAmount = 30m },
                new ServiceReadingDetail { IdGroupUnit = groupB, CalculatedAmount = 20m },
            ],
            Configuration = new BuildingConfiguration { WaterReadingDefault = waterCategory },
            Exonerations = [new Exoneration { IdGroupUnit = groupA, IdCategory = waterCategory }]
        };
        state.Budget.Details.Add(new BudgetDetail { IsHeader = false, Type = 1, MonthlyAmount = 100m, IdCategory = waterCategory });

        var total = new BudgetCalculator(state).CalculateQuota(totalApartments: 2);

        var byGroup = state.Installments.ToDictionary(i => i.IdGroupUnit, i => i.Amount);
        // groupA exonerado: paga solo su consumo individual medido (30), nada del
        // área común compartida.
        Assert.Equal(30m, byGroup[groupA]);
        // Denominador resta el exonerado (2-1=1): groupB absorbe TODO el presupuesto
        // flat (100), no solo la mitad que le tocaría sin exoneración.
        Assert.Equal(20m + 100m, byGroup[groupB]);
        Assert.Equal(150m, total);
    }

    [Fact]
    public void CalculateQuota_FreezesNroApartmentsOnFijaCategoriesOnly()
    {
        var groupA = Guid.NewGuid(); // exonerado de la categoría Fija
        var groupB = Guid.NewGuid();
        var catFixed = Guid.NewGuid();
        var catProp = Guid.NewGuid();
        var waterCategory = Guid.NewGuid();

        var state = new BudgetState
        {
            TotalArea = 200m,
            Owners = [MakeOwnerUnit(groupA, 100m, "101", "Owner A"), MakeOwnerUnit(groupB, 100m, "102", "Owner B")],
            Configuration = new BuildingConfiguration { WaterReadingDefault = waterCategory },
            Exonerations = [new Exoneration { IdGroupUnit = groupA, IdCategory = catFixed }]
        };
        var fijaItem = new BudgetDetail { IsHeader = false, Type = 1, MonthlyAmount = 100m, IdCategory = catFixed };
        var propItem = new BudgetDetail { IsHeader = false, Type = 2, MonthlyAmount = 1000m, IdCategory = catProp };
        // Agua Áreas Comunes es Type==1 en producción -- una Fija más.
        var waterItem = new BudgetDetail { IsHeader = false, Type = 1, MonthlyAmount = 100m, IdCategory = waterCategory };
        state.Budget.Details.AddRange([fijaItem, propItem, waterItem]);

        new BudgetCalculator(state).CalculateQuota(totalApartments: 2);

        // Fija: 2 unidades - 1 exonerada = 1.
        Assert.Equal(1, fijaItem.NroApartments);
        // Agua (Type==1): sin exoneraciones en esta categoría, las 2 unidades completas.
        Assert.Equal(2, waterItem.NroApartments);
        // %-based (por área) no usa "número de unidades" -- se deja sin tocar.
        Assert.Null(propItem.NroApartments);
    }

    [Fact]
    public void CalculateQuota_WithZeroApartments_ThrowsDivideByZero()
    {
        var group = Guid.NewGuid();
        var state = new BudgetState
        {
            TotalArea = 100m,
            Owners = [MakeOwnerUnit(group, 100m, "101", "Owner A")]
        };
        state.Budget.Details.Add(new BudgetDetail { IsHeader = false, Type = 1, MonthlyAmount = 100m, IdCategory = Guid.NewGuid() });

        Assert.Throws<DivideByZeroException>(() => new BudgetCalculator(state).CalculateQuota(totalApartments: 0));
    }

    [Fact]
    public void CalculateQuota_WithZeroTotalArea_ThrowsDivideByZeroForProportionalItems()
    {
        var group = Guid.NewGuid();
        var state = new BudgetState
        {
            TotalArea = 0m,
            Owners = [MakeOwnerUnit(group, 100m, "101", "Owner A")]
        };
        state.Budget.Details.Add(new BudgetDetail { IsHeader = false, Type = 2, MonthlyAmount = 100m, IdCategory = Guid.NewGuid() });

        Assert.Throws<DivideByZeroException>(() => new BudgetCalculator(state).CalculateQuota(totalApartments: 1));
    }
}
