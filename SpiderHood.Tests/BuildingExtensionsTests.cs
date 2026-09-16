using SpiderHood.Models;

namespace SpiderHood.Tests;

public class BuildingExtensionsTests
{
    private static UserBuilding MakeUserBuilding(Guid buildingId) =>
        new() { Building = new Building { IdBuilding = buildingId } };

    [Fact]
    public void GetValidDefaultBuilding_WithNoPreference_ReturnsFirstBuilding()
    {
        var first = MakeUserBuilding(Guid.NewGuid());
        var buildings = new List<UserBuilding> { first, MakeUserBuilding(Guid.NewGuid()) };

        var result = buildings.GetValidDefaultBuilding(null);

        Assert.Same(first, result);
    }

    [Fact]
    public void GetValidDefaultBuilding_WithMatchingPreference_ReturnsThatBuilding()
    {
        var preferredId = Guid.NewGuid();
        var preferred = MakeUserBuilding(preferredId);
        var buildings = new List<UserBuilding> { MakeUserBuilding(Guid.NewGuid()), preferred };

        var result = buildings.GetValidDefaultBuilding(preferredId);

        Assert.Same(preferred, result);
    }

    [Fact]
    public void GetValidDefaultBuilding_WithPreferenceNotInList_FallsBackToFirstBuilding()
    {
        var first = MakeUserBuilding(Guid.NewGuid());
        var buildings = new List<UserBuilding> { first, MakeUserBuilding(Guid.NewGuid()) };

        var result = buildings.GetValidDefaultBuilding(Guid.NewGuid());

        Assert.Same(first, result);
    }

    [Fact]
    public void GetValidDefaultBuilding_WithEmptyList_ReturnsNull()
    {
        var result = Enumerable.Empty<UserBuilding>().GetValidDefaultBuilding(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public void BelongsToUser_WhenUserHasBuilding_ReturnsTrue()
    {
        var buildingId = Guid.NewGuid();
        var building = MakeUserBuilding(buildingId);
        var user = new UserSession { Buildings = [building] };

        Assert.True(building.BelongsToUser(user));
    }

    [Fact]
    public void BelongsToUser_WhenUserDoesNotHaveBuilding_ReturnsFalse()
    {
        var building = MakeUserBuilding(Guid.NewGuid());
        var user = new UserSession { Buildings = [MakeUserBuilding(Guid.NewGuid())] };

        Assert.False(building.BelongsToUser(user));
    }
}
