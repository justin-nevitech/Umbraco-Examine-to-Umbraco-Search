using Examine;
using Examine.Lucene;
using Examine.Search;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Examine;
using Umbraco.Extensions;

namespace SearchDemo.Search;

public class SiteSearchService : ISiteSearchService
{
    private readonly IExamineManager _examineManager;
    private readonly IPublishedContentCache _contentCache;
    private readonly IIdKeyMap _idKeyMap;

    public SiteSearchService(IExamineManager examineManager, IPublishedContentCache contentCache, IIdKeyMap idKeyMap)
    {
        _examineManager = examineManager;
        _contentCache = contentCache;
        _idKeyMap = idKeyMap;
    }

    public string EngineName => "Examine 4 (custom SiteSearchIndex, alongside Umbraco Search)";

    public Task<SiteSearchResults> SearchAsync(SiteSearchRequest request, CancellationToken cancellationToken = default)
    {
        if (_examineManager.TryGetIndex(SearchFields.IndexName, out var index) is false)
        {
            throw new InvalidOperationException($"Examine index '{SearchFields.IndexName}' not found");
        }

        // Pages have no categories, so categories only apply when blog posts are included
        var useCategories = request.TypeFilter != SearchTypeFilter.Pages;

        var query = CreateQuery(index.Searcher, request);

        // Any of the selected categories
        if (useCategories && request.Categories.Count > 0)
        {
            query = query.And().GroupedOr([SearchFields.CategoryKey], request.Categories.Select(key => key.ToString("N")).ToArray());
        }

        IOrdering ordering = request.SortOrder switch
        {
            SearchSortOrder.Latest => query.OrderByDescending(new SortableField(SearchFields.UpdateDate, SortType.Long)),
            SearchSortOrder.Oldest => query.OrderBy(new SortableField(SearchFields.UpdateDate, SortType.Long)),
            _ => query
        };

        var skip = (request.Page - 1) * request.PageSize;
        var results = ordering.Execute(QueryOptions.SkipTake(skip, request.PageSize));

        var items = results
            .Select(ToItem)
            .OfType<SiteSearchResultItem>()
            .ToList();

        var facets = useCategories ? GetCategoryFacets(index.Searcher, request) : [];

        return Task.FromResult(new SiteSearchResults(items, results.TotalItemCount, request.Page, request.PageSize)
        {
            Facets = facets
        });
    }

    // Examine 4's built-in facets, counted in a second, facet-only search.
    // The query has no category clause, so the counts don't change as categories are ticked.
    private List<SiteSearchFacetValue> GetCategoryFacets(ISearcher searcher, SiteSearchRequest request)
    {
        ISearchResults results;
        try
        {
            // Every category is returned unless MaxCount is set
            results = CreateQuery(searcher, request)
                .WithFacets(facets => facets.FacetString(SearchFields.CategoryFacet))
                .Execute(QueryOptions.SkipTake(0, 0));
        }
        catch (ArgumentException)
        {
            // Thrown when no document has facet data yet, e.g. before the index has been rebuilt
            return [];
        }

        // Labels are category keys; names come from the content cache, so renames show straight away
        var counts = results.GetFacet(SearchFields.CategoryFacet)?.ToDictionary(value => Guid.ParseExact(value.Label, "N"), value => (long)value.Value) ?? [];

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

    // Built fresh for each use, because Examine's fluent queries are mutable
    private static IBooleanOperation CreateQuery(ISearcher searcher, SiteSearchRequest request)
    {
        var query = searcher.CreateQuery(IndexTypes.Content)
            // Only pages with a template
            .RangeQuery<int>([SearchFields.TemplateId], 1, int.MaxValue);

        // Scope to the current site
        query = query.And().Field(SearchFields.SiteRootKey, request.SiteRootKey.ToString("N"));

        // Each keyword must match at least one text field, with a trailing wildcard
        foreach (var keyword in SplitKeywords(request.Query))
        {
            query = query.And().GroupedOr(SearchFields.TextFields, keyword.MultipleCharacterWildcard());
        }

        return request.TypeFilter switch
        {
            SearchTypeFilter.BlogPosts => query.And().Field(SearchFields.IsBlogPost, SearchFields.Yes),
            SearchTypeFilter.Pages => query.And().Field(SearchFields.IsBlogPost, SearchFields.No),
            _ => query
        };
    }

    private SiteSearchResultItem? ToItem(ISearchResult result)
    {
        if (Guid.TryParse(result.Values.GetValueOrDefault(UmbracoExamineFieldNames.NodeKeyFieldName), out var key) is false)
        {
            return null;
        }

        var content = _contentCache.GetById(key);
        if (content is null)
        {
            return null;
        }

        return new SiteSearchResultItem(
            Title: content.Value<string>("title").IfNullOrWhiteSpace(content.Name),
            Subtitle: content.Value<string>("subtitle"),
            Url: content.Url(),
            // Read straight from the index value we added
            IsBlogPost: result.Values.GetValueOrDefault(SearchFields.IsBlogPost) == SearchFields.Yes,
            UpdateDate: content.UpdateDate);
    }

    private static IEnumerable<string> SplitKeywords(string? query) =>
        (query ?? string.Empty)
            .Replace("/", string.Empty)
            .Split([' ', '-'], StringSplitOptions.RemoveEmptyEntries);
}
