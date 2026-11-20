using Examine;
using Examine.Search;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Search;
using Umbraco.Cms.Core.Search.Indexing;
using SearchConstants = Umbraco.Cms.Core.Constants.Search;

namespace SearchDemo.Search;

/// <summary>
/// Replaces Umbraco's UmbracoContentIndex plumbing (removed in Umbraco 19). Umbraco Search calls this when
/// published content changes and when the index is rebuilt, with the fields its content indexers collected
/// (including SiteSearchContentIndexer's). It maps them to our own field names and writes an Examine document.
/// </summary>
public class SiteSearchIndexer : IIndexer
{
    private const string ProviderName = "Demo site search (Examine)";

    // Examine's document category; every document in this index is a page
    public const string ValueSetCategory = "content";

    private readonly IExamineManager _examineManager;
    private readonly IContentTypeService _contentTypeService;

    public SiteSearchIndexer(IExamineManager examineManager, IContentTypeService contentTypeService)
    {
        _examineManager = examineManager;
        _contentTypeService = contentTypeService;
    }

    public Task AddOrUpdateAsync(
        string indexAlias,
        Guid id,
        UmbracoObjectTypes objectType,
        IEnumerable<Variation> variations,
        IEnumerable<IndexField> fields,
        ContentProtection? protection)
    {
        var index = GetIndex(indexAlias);
        var fieldsAsArray = fields as IndexField[] ?? fields.ToArray();

        // was: the index validator (supportProtectedContent: false, excludeItemTypes).
        // Delete rather than skip, in case the page was indexed before it was protected.
        if (protection is not null || IsExcludedFromSearch(fieldsAsArray))
        {
            index.DeleteFromIndex([id.ToString()]);
            return Task.CompletedTask;
        }

        index.IndexItem(new ValueSet(id.ToString(), ValueSetCategory, ToValues(fieldsAsArray)));
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string indexAlias, IEnumerable<Guid> ids)
    {
        var index = GetIndex(indexAlias);
        var keys = ids.Select(id => id.ToString("D")).ToArray();
        if (keys.Length == 0)
        {
            return Task.CompletedTask;
        }

        // A deleted or unpublished page takes its descendants with it, and they have its key in their path.
        // Examine caps the take at the number of documents in the index.
        var documentIds = index.Searcher
            .CreateQuery(ValueSetCategory)
            .GroupedOr([SearchFields.PathKeys], keys)
            .SelectField(ExamineFieldNames.ItemIdFieldName)
            .Execute(QueryOptions.SkipTake(0, int.MaxValue))
            .Select(result => result.Id)
            .Union(ids.Select(id => id.ToString()))
            .ToArray();

        index.DeleteFromIndex(documentIds);
        return Task.CompletedTask;
    }

    public Task ResetAsync(string indexAlias)
    {
        // Called at the start of a rebuild; creates an empty index
        GetIndex(indexAlias).CreateIndex();
        return Task.CompletedTask;
    }

    public Task<IndexMetadata> GetMetadataAsync(string indexAlias)
    {
        // Shown in Settings > Search
        if (_examineManager.TryGetIndex(indexAlias, out var index) is false || index.IndexExists() is false)
        {
            return Task.FromResult(new IndexMetadata(0, HealthStatus.Unknown, ProviderName));
        }

        var documentCount = index is IIndexStats stats ? stats.GetDocumentCount() : 0;
        return Task.FromResult(new IndexMetadata(
            documentCount,
            documentCount > 0 ? HealthStatus.Healthy : HealthStatus.Empty,
            ProviderName));
    }

    private bool IsExcludedFromSearch(IndexField[] fields)
    {
        // Umbraco Search indexes the content type key, not the alias
        var contentTypeKey = fields.FirstOrDefault(field => field.FieldName == SearchConstants.FieldNames.ContentTypeId)?.Value.Keywords?.FirstOrDefault();
        var alias = Guid.TryParse(contentTypeKey, out var key) ? _contentTypeService.Get(key)?.Alias : null;
        return alias is not null && DocTypes.ExcludedFromSearch.Contains(alias);
    }

    // Clean's content is invariant, so only invariant fields are mapped
    private static Dictionary<string, IEnumerable<object>> ToValues(IndexField[] fields)
    {
        var values = new Dictionary<string, IEnumerable<object>>();

        foreach (var field in fields.Where(field => field.Culture is null && field.Segment is null))
        {
            (string Name, IEnumerable<object>? Values) mapped = field.FieldName switch
            {
                SearchConstants.FieldNames.Name => (SearchFields.NodeName, Texts(field.Value)),
                SearchConstants.FieldNames.UpdateDate => (SearchFields.UpdateDate, field.Value.DateTimeOffsets?.Select(date => (object)date.DateTime)),
                SearchConstants.FieldNames.PathIds => (SearchFields.PathKeys, field.Value.Keywords),

                // Added by SiteSearchContentIndexer
                SearchFields.SiteRootKey or SearchFields.IsBlogPost or SearchFields.HasTemplate or SearchFields.CategoryKey
                    => (field.FieldName, field.Value.Keywords),

                // Properties searched by keyword
                _ when SearchFields.TextProperties.Contains(field.FieldName) => (field.FieldName, Texts(field.Value)),

                _ => (field.FieldName, null)
            };

            var list = mapped.Values?.ToList();
            if (list is { Count: > 0 })
            {
                values[mapped.Name] = list;
            }
        }

        // The same keys again, for counting (see ConfigureSiteSearchIndexOptions)
        if (values.TryGetValue(SearchFields.CategoryKey, out var categoryKeys))
        {
            values[SearchFields.CategoryFacet] = categoryKeys;
        }

        return values;
    }

    // Umbraco Search splits text by relevance; one Examine field per property is enough here
    private static IEnumerable<object> Texts(IndexValue value) =>
        new[] { value.TextsR1, value.TextsR2, value.TextsR3, value.Texts }
            .SelectMany(texts => texts ?? [])
            .Cast<object>();

    private IIndex GetIndex(string indexAlias) =>
        _examineManager.TryGetIndex(indexAlias, out var index)
            ? index
            : throw new InvalidOperationException($"Examine index '{indexAlias}' not found");
}
