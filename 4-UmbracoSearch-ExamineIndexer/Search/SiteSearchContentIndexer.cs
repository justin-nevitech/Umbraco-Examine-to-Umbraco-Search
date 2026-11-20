using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Search.Core.Models.Indexing;
using Umbraco.Cms.Search.Core.Services.ContentIndexing;

namespace SearchDemo.Search;

/// <summary>
/// Replaces the TransformingIndexValues handler. Runs when content changes;
/// the output is cached in the database, so it is not re-run on index rebuilds.
/// </summary>
public class SiteSearchContentIndexer : IContentIndexer
{
    private readonly IIdKeyMap _idKeyMap;

    public SiteSearchContentIndexer(IIdKeyMap idKeyMap) => _idKeyMap = idKeyMap;

    public Task<IEnumerable<IndexField>> GetIndexFieldsAsync(
        IContentBase content,
        string?[] cultures,
        bool published,
        CancellationToken cancellationToken)
    {
        // Content indexers run for documents, media and members
        if (content is not IContent document)
        {
            return Task.FromResult(Enumerable.Empty<IndexField>());
        }

        var fields = new List<IndexField>
        {
            Keyword(SearchFields.IsBlogPost, document.ContentType.Alias == DocTypes.Article ? SearchFields.Yes : SearchFields.No),
            // templateID is not a system field in Umbraco Search, so index a flag instead
            Keyword(SearchFields.HasTemplate, document.TemplateId is > 0 ? SearchFields.Yes : SearchFields.No)
        };

        var rootKey = SiteRoot.GetRootKey(document.Path, _idKeyMap);
        if (rootKey is not null)
        {
            fields.Add(Keyword(SearchFields.SiteRootKey, rootKey.Value.ToString("N")));
        }

        // The categories picker is stored as comma-separated UDIs; index one exact-match key per category
        var categoryKeys = GetCategoryKeys(document.GetValue<string>(SearchFields.Categories, published: published));
        if (categoryKeys.Length > 0)
        {
            fields.Add(new IndexField(SearchFields.CategoryKey, new IndexValue { Keywords = categoryKeys }, Culture: null, Segment: null));
        }

        return Task.FromResult<IEnumerable<IndexField>>(fields);
    }

    private static string[] GetCategoryKeys(string? udis) =>
        (udis ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => UdiParser.TryParse(value, out Udi? udi) && udi is GuidUdi guidUdi ? guidUdi.Guid.ToString("N") : null)
            .OfType<string>()
            .ToArray();

    private static IndexField Keyword(string name, string value)
        => new(FieldName: name, Value: new IndexValue { Keywords = [value] }, Culture: null, Segment: null);
}
