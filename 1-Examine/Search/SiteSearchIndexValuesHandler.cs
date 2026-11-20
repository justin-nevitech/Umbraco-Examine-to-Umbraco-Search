using Examine;
using Examine.Lucene;
using Examine.Lucene.Providers;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Examine;

namespace SearchDemo.Search;

/// <summary>
/// The classic TransformingIndexValues approach, as in the original ExamineComponent.
/// </summary>
public class SiteSearchIndexValuesHandler : INotificationHandler<UmbracoApplicationStartingNotification>
{
    private readonly IExamineManager _examineManager;
    private readonly IIdKeyMap _idKeyMap;

    public SiteSearchIndexValuesHandler(IExamineManager examineManager, IIdKeyMap idKeyMap)
    {
        _examineManager = examineManager;
        _idKeyMap = idKeyMap;
    }

    public void Handle(UmbracoApplicationStartingNotification notification)
    {
        if (notification.RuntimeLevel != RuntimeLevel.Run)
        {
            return;
        }

        if (_examineManager.TryGetIndex(SearchFields.IndexName, out var index) && index is LuceneIndex luceneIndex)
        {
            luceneIndex.TransformingIndexValues += OnTransformingIndexValues;
            luceneIndex.DocumentWriting += OnDocumentWriting;
        }
    }

    // Runs after OnTransformingIndexValues, with the Lucene document about to be written
    private static void OnDocumentWriting(object? sender, DocumentWritingEventArgs e)
    {
        if (e.ValueSet.Category == IndexTypes.Content && e.ValueSet.Values.TryGetValue(SearchFields.CategoryKey, out var categoryKeys))
        {
            SiteSearchFacets.AddCategoryFacets(e.Document, categoryKeys.OfType<string>());
        }
    }

    private void OnTransformingIndexValues(object? sender, IndexingItemEventArgs e)
    {
        if (e.ValueSet.Category != IndexTypes.Content)
        {
            return;
        }

        // The value set is immutable, so copy it
        var values = e.ValueSet.Values.ToDictionary(x => x.Key, x => (IEnumerable<object>)x.Value.ToList());

        var rootKey = SiteRoot.GetRootKey(e.ValueSet.GetValue("path") as string, _idKeyMap);
        if (rootKey is not null)
        {
            values[SearchFields.SiteRootKey] = [rootKey.Value.ToString("N")];
        }

        values[SearchFields.IsBlogPost] = [e.ValueSet.ItemType == DocTypes.Article ? SearchFields.Yes : SearchFields.No];

        // The categories picker is indexed as comma-separated UDIs; store one exact-match key per category
        var categoryKeys = GetCategoryKeys(e.ValueSet.GetValue(SearchFields.Categories) as string);
        if (categoryKeys.Count > 0)
        {
            values[SearchFields.CategoryKey] = categoryKeys;
        }

        e.SetValues(values);
    }

    private static List<object> GetCategoryKeys(string? udis) =>
        (udis ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => UdiParser.TryParse(value, out Udi? udi) && udi is GuidUdi guidUdi ? guidUdi.Guid.ToString("N") : null)
            .OfType<object>()
            .ToList();
}
