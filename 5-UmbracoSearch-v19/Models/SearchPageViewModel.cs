using SearchDemo.Search;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace SearchDemo.Models;

/// <summary>
/// Wraps the Clean "search" page so Clean's master layout still gets an IPublishedContent.
/// </summary>
public class SearchPageViewModel : PublishedContentWrapped
{
    // Umbraco 19 drops PublishedContentWrapped's IPublishedValueFallback parameter
    public SearchPageViewModel(IPublishedContent content)
        : base(content)
    {
    }

    public required SiteSearchRequest Request { get; init; }

    public required SiteSearchResults Results { get; init; }

    public required string SiteName { get; init; }

    public required string EngineName { get; init; }
}
