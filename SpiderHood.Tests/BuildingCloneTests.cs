using SpiderHood.Models;

namespace SpiderHood.Tests;

public class BuildingCloneTests
{
    [Fact]
    public void Building_Clone_CopiesScalarFieldsAndDeepCopiesConfiguration()
    {
        var original = new Building
        {
            IdBuilding = Guid.NewGuid(),
            Number = 5,
            Name = "Torre A",
            Location = "Lima",
            TotalArea = 1234.56m,
            IsActive = true,
            IsTemplate = false,
            Configuration = new BuildingConfiguration { Currency = "USD" }
        };

        var clone = original.Clone();

        Assert.Equal(original.IdBuilding, clone.IdBuilding);
        Assert.Equal(original.Name, clone.Name);
        Assert.Equal(original.TotalArea, clone.TotalArea);
        Assert.Equal("USD", clone.Configuration.Currency);

        // El clon debe ser independiente: mutar el original no debe afectar al clon.
        original.Name = "Torre B";
        original.Configuration.Currency = "PEN";
        Assert.Equal("Torre A", clone.Name);
        Assert.Equal("USD", clone.Configuration.Currency);
    }

    [Fact]
    public void BuildingConfiguration_Clone_DeepCopiesListsAndNestedContacts()
    {
        var original = new BuildingConfiguration
        {
            Currency = "PEN",
            BankAccounts = [new BankAccount { AccountName = "Cta 1" }],
            PaymentMethods = ["Transferencia", "Efectivo"],
            Exonerations = [new Exoneration { Description = "Ascensor" }],
            AdminContact = new Contact { Name = "Juan" }
        };

        var clone = original.Clone();

        Assert.Single(clone.BankAccounts);
        Assert.Equal(2, clone.PaymentMethods.Count);
        Assert.Single(clone.Exonerations);
        Assert.Equal("Juan", clone.AdminContact.Name);

        // Independencia de las listas y sub-objetos clonados.
        original.BankAccounts.Add(new BankAccount { AccountName = "Cta 2" });
        original.PaymentMethods.Add("Yape");
        original.AdminContact.Name = "Pedro";

        Assert.Single(clone.BankAccounts);
        Assert.Equal(2, clone.PaymentMethods.Count);
        Assert.Equal("Juan", clone.AdminContact.Name);
    }

    [Fact]
    public void BuildingConfiguration_Clone_WithEmptyLists_ProducesEmptyClone()
    {
        var original = new BuildingConfiguration();

        var clone = original.Clone();

        Assert.Empty(clone.BankAccounts);
        Assert.Empty(clone.PaymentMethods);
        Assert.Empty(clone.Exonerations);
        Assert.Null(clone.ExpenseApprovalThreshold);
    }

    [Fact]
    public void Contact_Clone_CopiesAllFields()
    {
        var original = new Contact { Name = "Ana", Email = "ana@test.com", Phone = "123" };

        var clone = original.Clone();

        Assert.Equal(original.Name, clone.Name);
        Assert.Equal(original.Email, clone.Email);
        Assert.Equal(original.Phone, clone.Phone);
    }
}
