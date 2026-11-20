using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Extensions;
using Umbraco.Cms.Search.BackOffice.DependencyInjection;
using Umbraco.Cms.Search.Core.DependencyInjection;
using Umbraco.Cms.Search.Core.Services.ContentIndexing;
using Umbraco.Cms.Search.Provider.Examine.Configuration;
using Umbraco.Cms.Search.Provider.Examine.DependencyInjection;

namespace SearchDemo.Search;

/// <summary>
/// No custom index to register: Umbraco Search maintains Umb_PublishedContent.
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
        // Note: this breaks IPublishedContentQuery.Search(), which Clean's own search view
        // and headless search API use. The demo's SearchController replaces the search page.
        builder.DisableDefaultExamineIndexes();

        // Replaces TransformingIndexValues: adds siteRootKey, isBlogPost, hasTemplate and categoryKey
        builder.Services.AddTransient<IContentIndexer, SiteSearchContentIndexer>();

        // The Examine provider only builds facet data for fields configured here
        builder.Services.Configure<FieldOptions>(options => options.Fields =
        [
            new() { PropertyName = SearchFields.CategoryKey, FieldValues = FieldValues.Keywords, Facetable = true }
        ]);

        builder.Services.AddScoped<ISiteSearchService, SiteSearchService>();
    }
}
