using SpiderHood.Utilities;

namespace SpiderHood.Tests;

public class PaginationClassTests
{
    private sealed class Widget
    {
        public string Name { get; set; } = string.Empty;
        public int Rank { get; set; }
    }

    private static PaginationClass<Widget> CreatePagination(int itemCount)
    {
        var sortExpressions = new Dictionary<string, Func<Widget, object>>
        {
            { "Name", w => w.Name },
            { "Rank", w => w.Rank },
        };

        var pagination = new PaginationClass<Widget>(new Dictionary<string, string>(), sortExpressions, "Rank");

        var data = Enumerable.Range(1, itemCount)
            .Select(i => new Widget { Name = $"Widget {i}", Rank = i })
            .ToList();

        pagination.Initialize(data);
        return pagination;
    }

    [Fact]
    public void Initialize_WithFewerItemsThanPageSize_PutsEverythingOnFirstPage()
    {
        var pagination = CreatePagination(10);

        Assert.Equal(1, pagination.CurrentPage);
        Assert.Equal(1, pagination.TotalPages);
        Assert.Equal(10, pagination.TotalRecords);
        Assert.Equal(10, pagination.CurrentPageData.Count);
    }

    [Fact]
    public void Initialize_WithMoreItemsThanPageSize_SplitsIntoPages()
    {
        var pagination = CreatePagination(60); // PageSize por defecto = 25

        Assert.Equal(3, pagination.TotalPages);
        Assert.Equal(25, pagination.CurrentPageData.Count);
        Assert.Equal(1, pagination.StartRecord);
        Assert.Equal(25, pagination.EndRecord);
    }

    [Fact]
    public void GoToPage_ReturnsCorrectSliceOfData()
    {
        var pagination = CreatePagination(60);

        pagination.GoToPage(3);

        Assert.Equal(3, pagination.CurrentPage);
        Assert.Equal(51, pagination.StartRecord);
        Assert.Equal(60, pagination.EndRecord);
        Assert.Equal(10, pagination.CurrentPageData.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(999)]
    public void GoToPage_WithOutOfRangePage_IsIgnored(int page)
    {
        var pagination = CreatePagination(60);

        pagination.GoToPage(page);

        Assert.Equal(1, pagination.CurrentPage);
    }

    [Fact]
    public void GoToNextPage_And_GoToPreviousPage_MoveOnePageAtATime()
    {
        var pagination = CreatePagination(60);

        pagination.GoToNextPage();
        Assert.Equal(2, pagination.CurrentPage);

        pagination.GoToPreviousPage();
        Assert.Equal(1, pagination.CurrentPage);
    }

    [Fact]
    public void ChangePageSize_RecalculatesTotalPagesAndResetsToFirstPage()
    {
        var pagination = CreatePagination(60);
        pagination.GoToPage(3);

        pagination.ChangePageSize(10);

        Assert.Equal(10, pagination.PageSize);
        Assert.Equal(1, pagination.CurrentPage);
        Assert.Equal(6, pagination.TotalPages);
    }

    [Fact]
    public void SortByColumn_TogglesDirectionOnRepeatedCalls()
    {
        var pagination = CreatePagination(5);

        pagination.SortByColumn("Rank");
        Assert.Equal("Rank", pagination.SortColumn);
        Assert.True(pagination.SortAscending);
        Assert.Equal(1, pagination.CurrentPageData.First().Rank);

        pagination.SortByColumn("Rank");
        Assert.False(pagination.SortAscending);
        Assert.Equal(5, pagination.CurrentPageData.First().Rank);
    }

    [Fact]
    public void Search_FiltersByDefaultToStringImplementation()
    {
        var pagination = CreatePagination(15);

        // El filtro por defecto usa item.ToString(); Widget no lo sobrescribe, así que
        // sólo matchea por el nombre del tipo -- confirma que sin override no hay falsos
        // positivos por Name/Rank.
        pagination.Search("Widget 1");

        Assert.Equal(0, pagination.TotalRecords);
    }

    [Fact]
    public void ApplyCustomFilter_NarrowsDownResultsAndRecomputesPaging()
    {
        var pagination = CreatePagination(20);

        pagination.ApplyCustomFilter(w => w.Rank % 2 == 0);

        Assert.Equal(10, pagination.TotalRecords);
        Assert.All(pagination.CurrentPageData, w => Assert.True(w.Rank % 2 == 0));
    }

    [Fact]
    public void ClearFilters_RestoresFullDataset()
    {
        var pagination = CreatePagination(20);
        pagination.ApplyCustomFilter(w => w.Rank % 2 == 0);

        pagination.ClearFilters();

        Assert.Equal(20, pagination.TotalRecords);
    }

    [Fact]
    public void GetPaginationInfo_ReportsRangeWhenThereAreRecords()
    {
        var pagination = CreatePagination(60);
        pagination.GoToPage(2);

        Assert.Equal("Mostrando 26-50 de 60 registros", pagination.GetPaginationInfo());
    }

    [Fact]
    public void GetPaginationInfo_ReportsEmptyWhenNoRecords()
    {
        var pagination = CreatePagination(20);
        pagination.ApplyCustomFilter(w => false);

        Assert.Equal("No hay registros", pagination.GetPaginationInfo());
    }

    [Fact]
    public void GetPageNumbers_WithFewPages_ReturnsAllOfThemWithoutEllipsis()
    {
        var pagination = CreatePagination(60); // 3 páginas con PageSize 25

        var pages = pagination.GetPageNumbers(maxPagesToShow: 5);

        Assert.Equal([1, 2, 3], pages);
    }

    [Fact]
    public void GetPageNumbers_WithManyPagesNearTheStart_EndsWithEllipsisAndLastPage()
    {
        var pagination = CreatePagination(250); // 10 páginas
        pagination.GoToPage(1);

        var pages = pagination.GetPageNumbers(maxPagesToShow: 5);

        Assert.Equal([1, 2, 3, 4, -1, 10], pages);
    }
}
