using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Search.Core.Extensions;
using Umbraco.Cms.Search.Core.Models.Searching.Faceting;
using Umbraco.Cms.Search.Core.Models.Searching.Filtering;
using Umbraco.Cms.Search.Core.Models.Searching.Sorting;
using Umbraco.Cms.Search.Core.Services;
using Umbraco.Extensions;
using SearchConstants = Umbraco.Cms.Search.Core.Constants;

namespace SearchDemo.Search;

public class SiteSearchService : ISiteSearchService
{
    private readonly ISearcher _searcher;
    private readonly IPublishedContentCache _contentCache;
    private readonly IPublishedContentTypeCache _contentTypeCache;
    private readonly IIdKeyMap _idKeyMap;

    public SiteSearchService(
        ISearcher searcher,
        IPublishedContentCache contentCache,
        IPublishedContentTypeCache contentTypeCache,
        IIdKeyMap idKeyMap)
    {
        _searcher = searcher;
        _contentCache = contentCache;
        _contentTypeCache = contentTypeCache;
        _idKeyMap = idKeyMap;
    }

    public string EngineName => "Umbraco Search (Umb_PublishedContent)";

    public async Task<SiteSearchResults> SearchAsync(SiteSearchRequest request, CancellationToken cancellationToken = default)
    {
        var filters = new List<Filter>
        {
            // was: RangeQuery<int>("templateID", 1, int.MaxValue)
            new KeywordFilter(SearchFields.HasTemplate, [SearchFields.Yes], Negate: false),

            // was: Field("siteRootKey", ...)
            new KeywordFilter(SearchFields.SiteRootKey, [request.SiteRootKey.ToString("N")], Negate: false)
        };

        // was: excludeItemTypes on the index validator.
        // Umbraco Search indexes the content type key, not the alias, so resolve the keys.
        var excludedTypeKeys = DocTypes.ExcludedFromSearch
            .Select(alias => _contentTypeCache.Get(PublishedItemType.Content, alias)?.Key)
            .OfType<Guid>()
            .Select(key => key.AsKeyword())
            .ToArray();

        if (excludedTypeKeys.Length > 0)
        {
            filters.Add(new KeywordFilter(SearchConstants.FieldNames.ContentTypeId, excludedTypeKeys, Negate: true));
        }

        // was: Field("isBlogPost", ...)
        var isBlogPost = request.TypeFilter switch
        {
            SearchTypeFilter.BlogPosts => SearchFields.Yes,
            SearchTypeFilter.Pages => SearchFields.No,
            _ => null
        };
        if (isBlogPost is not null)
        {
            filters.Add(new KeywordFilter(SearchFields.IsBlogPost, [isBlogPost], Negate: false));
        }

        // was: OrderBy(new SortableField("updateDate", SortType.Long))
        Sorter[]? sorters = request.SortOrder switch
        {
            SearchSortOrder.Latest => [new DateTimeOffsetSorter(SearchConstants.FieldNames.UpdateDate, Direction.Descending)],
            SearchSortOrder.Oldest => [new DateTimeOffsetSorter(SearchConstants.FieldNames.UpdateDate, Direction.Ascending)],
            _ => null
        };

        // was: GroupedOr over named fields with trailing wildcards per keyword.
        // The provider splits on spaces and adds trailing wildcards itself, so only hyphens need
        // turning into spaces to split keywords the same way. It searches every Text field.
        var query = request.Query?.Replace("/", string.Empty).Replace("-", " ");
        if (string.IsNullOrWhiteSpace(query))
        {
            query = null;
        }

        // Pages have no categories, so categories only apply when blog posts are included
        var useCategories = request.TypeFilter != SearchTypeFilter.Pages;

        // was: GroupedOr([categoryKey], ...), any of the selected categories
        List<Filter> resultFilters = useCategories && request.Categories.Count > 0
            ? [.. filters, new KeywordFilter(SearchFields.CategoryKey, request.Categories.Select(key => key.ToString("N")).ToArray(), Negate: false)]
            : filters;

        var result = await _searcher.SearchAsync(
            indexAlias: SearchConstants.IndexAliases.PublishedContent,
            query: query,
            filters: resultFilters,
            sorters: sorters,
            skip: (request.Page - 1) * request.PageSize,
            take: request.PageSize);

        var items = result.Documents
            .Select(document => _contentCache.GetById(document.Id))
            .OfType<IPublishedContent>()
            .Select(content => new SiteSearchResultItem(
                Title: content.Value<string>("title").IfNullOrWhiteSpace(content.Name),
                Subtitle: content.Value<string>("subtitle"),
                Url: content.Url(),
                // Result documents carry the key only, so read the type from the content
                IsBlogPost: content.ContentType.Alias == DocTypes.Article,
                UpdateDate: content.UpdateDate))
            .ToList();

        return new SiteSearchResults(items, result.Total, request.Page, request.PageSize)
        {
            Facets = useCategories ? await GetCategoryFacetsAsync(query, filters, request) : []
        };
    }

    // Counted in a second, facet-only search without the category filter, so the counts don't change
    // as categories are ticked. (SearcherOptions.ExpandFacetValues does this too, but for every search.)
    private async Task<List<SiteSearchFacetValue>> GetCategoryFacetsAsync(string? query, List<Filter> filters, SiteSearchRequest request)
    {
        var result = await _searcher.SearchAsync(
            indexAlias: SearchConstants.IndexAliases.PublishedContent,
            query: query,
            filters: filters,
            facets: [new KeywordFacet(SearchFields.CategoryKey)],
            take: 0);

        // Values are category keys; names come from the content cache, so renames show straight away
        var counts = result.Facets
            .FirstOrDefault(facet => facet.FieldName == SearchFields.CategoryKey)?.Values
            .OfType<KeywordFacetValue>()
            .ToDictionary(value => Guid.ParseExact(value.Key, "N"), value => value.Count) ?? [];

        // Selected categories without matches are still listed, so they can be unticked
        return counts.Keys
            .Union(request.Categories)
            .Select(key => _contentCache.GetById(key))
            .OfType<IPublishedContent>()
            .Where(content => content.ContentType.Alias == DocTypes.Category && SiteRoot.GetRootKey(content.Path, _idKeyMap) == request.SiteRootKey)
            .Select(category => new SiteSearchFacetValue(
                Key: category.Key,
                Name: category.Name,
                Count: counts.GetValueOrDefault(category.Key),
                Selected: request.Categories.Contains(category.Key)))
            .OrderByDescending(facet => facet.Count)
            .ThenBy(facet => facet.Name)
            .ToList();
    }
}
