using Examine;
using Examine.Search;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Search.Querying;
using Umbraco.Cms.Core.Search.Querying.Faceting;
using Umbraco.Cms.Core.Search.Querying.Filtering;
using Umbraco.Cms.Core.Search.Querying.Sorting;
using ISearcher = Umbraco.Cms.Core.Search.ISearcher;
using SearchResult = Umbraco.Cms.Core.Search.Querying.SearchResult;

namespace SearchDemo.Search;

/// <summary>
/// Umbraco Search needs a searcher for every index it maintains, so that anything searching through
/// Umbraco Search gets results from this index. The site search itself queries the Examine index
/// directly (see SiteSearchService), so this is a minimal keyword search: filters, facets, sorters,
/// cultures and access are ignored.
/// </summary>
public class SiteSearchIndexSearcher : ISearcher
{
    private readonly IExamineManager _examineManager;

    public SiteSearchIndexSearcher(IExamineManager examineManager) => _examineManager = examineManager;

    public Task<SearchResult> SearchAsync(
        string indexAlias,
        string? query = null,
        IEnumerable<Filter>? filters = null,
        IEnumerable<Facet>? facets = null,
        IEnumerable<Sorter>? sorters = null,
        string? culture = null,
        string? segment = null,
        AccessContext? accessContext = null,
        int skip = 0,
        int take = 10,
        int maxSuggestions = 0)
    {
        if (_examineManager.TryGetIndex(indexAlias, out var index) is false)
        {
            return Task.FromResult(new SearchResult(0, [], []));
        }

        var searchQuery = index.Searcher.CreateQuery(SiteSearchIndexer.ValueSetCategory);
        var keywords = (query ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // Each keyword must match at least one text field, with a trailing wildcard
        var examineQuery = keywords.Length == 0
            ? searchQuery.All()
            : keywords.Skip(1).Aggregate(
                searchQuery.GroupedOr(SearchFields.TextFields, keywords[0].MultipleCharacterWildcard()),
                (current, keyword) => current.And().GroupedOr(SearchFields.TextFields, keyword.MultipleCharacterWildcard()));

        var results = examineQuery.Execute(QueryOptions.SkipTake(skip, take));

        var documents = results
            .Select(result => Guid.TryParse(result.Id, out var key) ? new Document(key, UmbracoObjectTypes.Document) : null)
            .OfType<Document>()
            .ToArray();

        return Task.FromResult(new SearchResult(results.TotalItemCount, documents, []));
    }
}
