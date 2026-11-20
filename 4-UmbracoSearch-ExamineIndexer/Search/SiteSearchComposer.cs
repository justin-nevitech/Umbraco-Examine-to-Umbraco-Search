using Examine;
using Examine.Lucene;
using Examine.Lucene.Providers;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Util;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Models;
using Umbraco.Extensions;
// Umbraco 19: ConfigurationEnabledDirectoryFactory moves to Umbraco.Cms.Search.Provider.Examine.Lucene
using Umbraco.Cms.Infrastructure.Examine;
using Umbraco.Cms.Search.BackOffice.DependencyInjection;
using Umbraco.Cms.Search.Core.DependencyInjection;
using Umbraco.Cms.Search.Core.Services.ContentIndexing;
using Umbraco.Cms.Search.Provider.Examine.DependencyInjection;
using IndexOptions = Umbraco.Cms.Search.Core.Configuration.IndexOptions;

namespace SearchDemo.Search;

/// <summary>
/// Umbraco 19-ready approach: the site search keeps its own Examine index and queries, but Umbraco Search
/// keeps the index up to date. It is a plain LuceneIndex (no UmbracoContentIndex), registered with
/// Umbraco Search like the built-in indexes, so it gets the same content changes and can be rebuilt
/// from Settings > Search.
/// </summary>
public class SiteSearchComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder
            .AddSearchCore()
            .AddBackOfficeSearch()
            .AddExamineSearchProvider();

        // Umbraco 17/18 only: stop maintaining the old default Examine indexes.
        // Remove this line when upgrading to Umbraco 19.
        builder.DisableDefaultExamineIndexes();

        builder.Services.AddExamineLuceneIndex<LuceneIndex, ConfigurationEnabledDirectoryFactory>(SearchFields.IndexName);
        builder.Services.ConfigureOptions<ConfigureSiteSearchIndexOptions>();

        // Replaces TransformingIndexValues: adds siteRootKey, isBlogPost, hasTemplate and categoryKey
        builder.Services.AddTransient<IContentIndexer, SiteSearchContentIndexer>();

        // Umbraco Search sends published content changes for this index to SiteSearchIndexer.
        // Both types are resolved from DI, so they must be registered.
        builder.Services.AddTransient<SiteSearchIndexer>();
        builder.Services.AddTransient<SiteSearchIndexSearcher>();
        builder.Services.Configure<IndexOptions>(options =>
            options.RegisterContentIndex<SiteSearchIndexer, SiteSearchIndexSearcher, IPublishedContentChangeStrategy>(
                SearchFields.IndexName,
                UmbracoObjectTypes.Document));

        builder.Services.AddScoped<ISiteSearchService, SiteSearchService>();
    }
}

public class ConfigureSiteSearchIndexOptions : IConfigureNamedOptions<LuceneDirectoryIndexOptions>
{
    public void Configure(string? name, LuceneDirectoryIndexOptions options)
    {
        if (name != SearchFields.IndexName)
        {
            return;
        }

        options.Analyzer = new StandardAnalyzer(LuceneVersion.LUCENE_48);
        options.UnlockIndex = true;

        // Examine 4 stores facets in a taxonomy sidecar index by default; plain doc values are enough here
        options.UseTaxonomyIndex = false;

        // Fields not listed here are full text
        options.FieldDefinitions = new FieldDefinitionCollection(
            new FieldDefinition(SearchFields.UpdateDate, FieldDefinitionTypes.DateTime),
            new FieldDefinition(SearchFields.PathKeys, FieldDefinitionTypes.Raw),
            new FieldDefinition(SearchFields.SiteRootKey, FieldDefinitionTypes.Raw),
            new FieldDefinition(SearchFields.IsBlogPost, FieldDefinitionTypes.Raw),
            new FieldDefinition(SearchFields.HasTemplate, FieldDefinitionTypes.Raw),
            new FieldDefinition(SearchFields.CategoryKey, FieldDefinitionTypes.Raw),
            // Examine 4 has no raw facet type, and a full text field changes the ranking (shorter fields score higher),
            // so count from a copy of the keys. Umbraco Search's Examine provider does the same for keyword facets.
            new FieldDefinition(SearchFields.CategoryFacet, FieldDefinitionTypes.FacetFullText));

        // A blog post can be in several categories
        options.FacetsConfig.SetMultiValued(SearchFields.CategoryFacet, true);
    }

    public void Configure(LuceneDirectoryIndexOptions options) => Configure(string.Empty, options);
}
