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

namespace SearchDemo.Search;

/// <summary>
/// Classic approach: a dedicated Examine index, populated and kept up to date by
/// Umbraco's built-in content index plumbing because it is an UmbracoContentIndex.
/// </summary>
public class SiteSearchIndexComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.AddExamineLuceneIndex<UmbracoContentIndex, ConfigurationEnabledDirectoryFactory>(SearchFields.IndexName);
        builder.Services.ConfigureOptions<ConfigureSiteSearchIndexOptions>();

        // Adds siteRootKey, isBlogPost and categoryKey to every document, plus the category facets (see SiteSearchIndexValuesHandler)
        builder.AddNotificationHandler<UmbracoApplicationStartingNotification, SiteSearchIndexValuesHandler>();

        builder.Services.AddSingleton<SiteSearchFacetStateCache>();
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

        options.FieldDefinitions = new UmbracoFieldDefinitionCollection();
        options.FieldDefinitions.AddOrUpdate(new FieldDefinition(SearchFields.TemplateId, FieldDefinitionTypes.Integer));
        options.FieldDefinitions.AddOrUpdate(new FieldDefinition(SearchFields.SiteRootKey, FieldDefinitionTypes.Raw));
        options.FieldDefinitions.AddOrUpdate(new FieldDefinition(SearchFields.IsBlogPost, FieldDefinitionTypes.Raw));
        options.FieldDefinitions.AddOrUpdate(new FieldDefinition(SearchFields.CategoryKey, FieldDefinitionTypes.Raw));

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
