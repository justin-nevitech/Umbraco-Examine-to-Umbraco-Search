using Examine;
using Examine.Lucene.Providers;
using Examine.Lucene.Search;
using Examine.Search;
using Lucene.Net.Facet;
using Lucene.Net.Facet.SortedSet;
using Lucene.Net.Index;
using Lucene.Net.Search;
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
    private readonly SiteSearchFacetStateCache _facetStateCache;

    public SiteSearchService(
        IExamineManager examineManager,
        IPublishedContentCache contentCache,
        IIdKeyMap idKeyMap,
        SiteSearchFacetStateCache facetStateCache)
    {
        _examineManager = examineManager;
        _contentCache = contentCache;
        _idKeyMap = idKeyMap;
        _facetStateCache = facetStateCache;
    }

    public string EngineName => "Examine (custom SiteSearchIndex)";

    public Task<SiteSearchResults> SearchAsync(SiteSearchRequest request, CancellationToken cancellationToken = default)
    {
        if (_examineManager.TryGetIndex(SearchFields.IndexName, out var index) is false)
        {
            throw new InvalidOperationException($"Examine index '{SearchFields.IndexName}' not found");
        }

        // Pages have no categories, so categories only apply when blog posts are included
        var useCategories = request.TypeFilter != SearchTypeFilter.Pages;

        var query = CreateQuery(index.Searcher, request, out _);

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

    // Examine 3 has no facet API, so count with Lucene.Net's facet module, in one pass over the matches.
    // The query has no category clause, so the counts don't change as categories are ticked.
    // (Lucene's DrillSideways could count and fetch the results in one search, but is harder to follow.)
    private List<SiteSearchFacetValue> GetCategoryFacets(ISearcher searcher, SiteSearchRequest request)
    {
        CreateQuery(searcher, request, out var luceneQuery);

        // Examine only adds the category clause when it runs a query, so add it here
        var query = new BooleanQuery
        {
            { new TermQuery(new Term(ExamineFieldNames.CategoryFieldName, IndexTypes.Content)), Occur.MUST },
            { luceneQuery.Query, Occur.MUST }
        };

        using var searcherReference = ((BaseLuceneSearcher)searcher).GetSearchContext().GetSearcher();
        var indexSearcher = searcherReference.IndexSearcher;

        // No state until the index has been rebuilt with facets, no range until a document has a category
        var state = _facetStateCache.Get(indexSearcher.IndexReader);
        var range = state?.GetOrdRange(SearchFields.CategoryKey);
        if (state is null || range is null)
        {
            return [];
        }

        var collector = new FacetsCollector();
        indexSearcher.Search(query, collector);

        // Ask for every category, so none are cut off
        var result = new SortedSetDocValuesFacetCounts(state, collector)
            .GetTopChildren(range.End - range.Start + 1, SearchFields.CategoryKey);

        // Labels are category keys; names come from the content cache, so renames show straight away
        var counts = result?.LabelValues.ToDictionary(value => Guid.ParseExact(value.Label, "N"), value => (int)value.Value) ?? [];

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

    // Built fresh for each use, because Examine's fluent queries are mutable.
    // The fluent calls all add to luceneQuery.Query, the Lucene query the facet count runs.
    private static IBooleanOperation CreateQuery(ISearcher searcher, SiteSearchRequest request, out LuceneSearchQuery luceneQuery)
    {
        luceneQuery = (LuceneSearchQuery)searcher.CreateQuery(IndexTypes.Content);

        // Only pages with a template
        var query = luceneQuery.RangeQuery<int>([SearchFields.TemplateId], 1, int.MaxValue);

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
