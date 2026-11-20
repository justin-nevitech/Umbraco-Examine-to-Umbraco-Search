using SearchDemo.Search;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace SearchDemo.Models;

/// <summary>
/// Wraps the Clean "search" page so Clean's master layout still gets an IPublishedContent.
/// </summary>
public class SearchPageViewModel : PublishedContentWrapped
{
    public SearchPageViewModel(IPublishedContent content, IPublishedValueFallback publishedValueFallback)
        : base(content, publishedValueFallback)
    {
    }

    public required SiteSearchRequest Request { get; init; }

    public required SiteSearchResults Results { get; init; }

    public required string SiteName { get; init; }

    public required string EngineName { get; init; }
}
