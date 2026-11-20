namespace SearchDemo.Search;

public enum SearchTypeFilter
{
    All,
    BlogPosts,
    Pages
}

public enum SearchSortOrder
{
    Relevance,
    Latest,
    Oldest
}

public record SiteSearchRequest(
    Guid SiteRootKey,
    string? Query,
    int Page,
    int PageSize,
    SearchTypeFilter TypeFilter,
    SearchSortOrder SortOrder)
{
    // Selected category keys; a blog post matches if it has any of them
    public IReadOnlyList<Guid> Categories { get; init; } = [];
}

public record SiteSearchResultItem(
    string Title,
    string? Subtitle,
    string Url,
    bool IsBlogPost,
    DateTime UpdateDate);

public record SiteSearchResults(
    IReadOnlyList<SiteSearchResultItem> Items,
    long TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 0;

    // Category facet counts; empty when the engine doesn't provide facets
    public IReadOnlyList<SiteSearchFacetValue> Facets { get; init; } = [];
}

public record SiteSearchFacetValue(
    Guid Key,
    string Name,
    long Count,
    bool Selected);

public interface ISiteSearchService
{
    string EngineName { get; }

    Task<SiteSearchResults> SearchAsync(SiteSearchRequest request, CancellationToken cancellationToken = default);
}
