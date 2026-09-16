using SpiderHood.Models;

namespace SpiderHood.Tests;

public class MenuItemWithRolesTests
{
    [Fact]
    public void ChildrenCount_ReflectsChildrenListSize()
    {
        var item = new MenuItemWithRoles { Children = [new MenuItemWithRoles(), new MenuItemWithRoles()] };
        Assert.Equal(2, item.ChildrenCount);
        Assert.True(item.HasChildren);
    }

    [Fact]
    public void ChildrenCount_IsZeroWhenListIsEmpty()
    {
        var item = new MenuItemWithRoles { Children = [] };
        Assert.Equal(0, item.ChildrenCount);
        Assert.False(item.HasChildren);
    }

    [Fact]
    public void ChildrenCount_IsZeroWhenChildrenIsNull()
    {
        var item = new MenuItemWithRoles { Children = null! };
        Assert.Equal(0, item.ChildrenCount);
        Assert.False(item.HasChildren);
    }
}
