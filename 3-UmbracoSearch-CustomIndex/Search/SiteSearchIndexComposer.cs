using Examine;
using Examine.Lucene;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Util;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Extensions;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Examine;
using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Cms.Search.BackOffice.DependencyInjection;
using Umbraco.Cms.Search.Core.DependencyInjection;
using Umbraco.Cms.Search.Provider.Examine.DependencyInjection;

namespace SearchDemo.Search;

/// <summary>
/// Interim approach for Umbraco 17/18: Umbraco Search is installed, but the site search still uses a
/// dedicated Examine index, populated and kept up to date by Umbraco's built-in content index plumbing
/// because it is an UmbracoContentIndex. Umbraco 19 removes that plumbing (see 4-UmbracoSearch-ExamineIndexer).
/// </summary>
public class SiteSearchIndexComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder
            .AddSearchCore()
            .AddBackOfficeSearch()
            .AddExamineSearchProvider();

        // Stops Umbraco maintaining its four default Examine indexes. SiteSearchIndex is not one of them,
        // so it is still populated and queryable. Remove this line when upgrading to Umbraco 19.
        builder.DisableDefaultExamineIndexes();

        builder.Services.AddExamineLuceneIndex<UmbracoContentIndex, ConfigurationEnabledDirectoryFactory>(SearchFields.IndexName);
        builder.Services.ConfigureOptions<ConfigureSiteSearchIndexOptions>();

        // Adds siteRootKey, isBlogPost and categoryKey to every document (see SiteSearchIndexValuesHandler)
        builder.AddNotificationHandler<UmbracoApplicationStartingNotification, SiteSearchIndexValuesHandler>();

        builder.Services.AddScoped<ISiteSearchService, SiteSearchService>();
    }
}

public class ConfigureSiteSearchIndexOptions : IConfigureNamedOptions<LuceneDirectoryIndexOptions>
{
    private readonly IPublicAccessService _publicAccessService;
    private readonly IScopeProvider _scopeProvider;

    public ConfigureSiteSearchIndexOptions(IPublicAccessService publicAccessService, IScopeProvider scopeProvider)
    {
        _publicAccessService = publicAccessService;
        _scopeProvider = scopeProvider;
    }

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

        options.FieldDefinitions = new UmbracoFieldDefinitionCollection();
        options.FieldDefinitions.AddOrUpdate(new FieldDefinition(SearchFields.TemplateId, FieldDefinitionTypes.Integer));
        options.FieldDefinitions.AddOrUpdate(new FieldDefinition(SearchFields.SiteRootKey, FieldDefinitionTypes.Raw));
        options.FieldDefinitions.AddOrUpdate(new FieldDefinition(SearchFields.IsBlogPost, FieldDefinitionTypes.Raw));
        options.FieldDefinitions.AddOrUpdate(new FieldDefinition(SearchFields.CategoryKey, FieldDefinitionTypes.Raw));

        // Examine 4 has no raw facet type, and a full text field changes the ranking (shorter fields score higher),
        // so count from a copy of the keys. Umbraco Search's Examine provider does the same for keyword facets.
        options.FieldDefinitions.AddOrUpdate(new FieldDefinition(SearchFields.CategoryFacet, FieldDefinitionTypes.FacetFullText));

        // A blog post can be in several categories
        options.FacetsConfig.SetMultiValued(SearchFields.CategoryFacet, true);

        // Published content only, no protected content, Clean's non-page types excluded
        options.Validator = new ContentValueSetValidator(
            publishedValuesOnly: true,
            supportProtectedContent: false,
            publicAccessService: _publicAccessService,
            scopeProvider: _scopeProvider,
            excludeItemTypes: DocTypes.ExcludedFromSearch);
    }

    public void Configure(LuceneDirectoryIndexOptions options) => Configure(string.Empty, options);
}
