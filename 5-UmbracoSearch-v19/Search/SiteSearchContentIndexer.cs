using System.Text.Json;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Search.Indexing;

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

        // Clean 8's categories element picker stores a JSON array of element keys; index one exact-match key per category
        var categoryKeys = GetCategoryKeys(document.GetValue<string>(SearchFields.Categories, published: published));
        if (categoryKeys.Length > 0)
        {
            fields.Add(new IndexField(SearchFields.CategoryKey, new IndexValue { Keywords = categoryKeys }, Culture: null, Segment: null));
        }

        return Task.FromResult<IEnumerable<IndexField>>(fields);
    }

    private static string[] GetCategoryKeys(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return (JsonSerializer.Deserialize<string[]>(json) ?? [])
                .Select(value => Guid.TryParse(value, out var key) ? key.ToString("N") : null)
                .OfType<string>()
                .ToArray();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static IndexField Keyword(string name, string value)
        => new(FieldName: name, Value: new IndexValue { Keywords = [value] }, Culture: null, Segment: null);
}
