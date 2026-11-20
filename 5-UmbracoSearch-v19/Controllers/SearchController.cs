using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using SearchDemo.Models;
using SearchDemo.Search;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Services.Navigation;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Web.Common.Controllers;

namespace SearchDemo.Controllers;

/// <summary>
/// Route hijacks Clean's "search" document type, so Clean's own search.cshtml
/// (which the Clean package overwrites on build) is left untouched.
/// Identical in both projects.
/// </summary>
public class SearchController : RenderController
{
    private const int PageSize = 5;

    private readonly ISiteSearchService _searchService;
    private readonly IDocumentNavigationQueryService _navigationQueryService;
    private readonly IPublishedContentCache _contentCache;

    public SearchController(
        ILogger<SearchController> logger,
        ICompositeViewEngine compositeViewEngine,
        IUmbracoContextAccessor umbracoContextAccessor,
        ISiteSearchService searchService,
        IDocumentNavigationQueryService navigationQueryService,
        IPublishedContentCache contentCache)
        : base(logger, compositeViewEngine, umbracoContextAccessor)
    {
        _searchService = searchService;
        _navigationQueryService = navigationQueryService;
        _contentCache = contentCache;
    }

    [NonAction]
    public sealed override IActionResult Index() => throw new NotImplementedException();

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var page = CurrentPage!;

        // Scope to the current site: walk up to the root of this page's tree.
        var siteRootKey = page.Key;
        while (_navigationQueryService.TryGetParentKey(siteRootKey, out var parentKey) && parentKey is not null)
        {
            siteRootKey = parentKey.Value;
        }

        var query = Request.Query;
        var request = new SiteSearchRequest(
            SiteRootKey: siteRootKey,
            Query: query["q"].ToString(),
            Page: int.TryParse(query["p"], out var p) && p > 0 ? p : 1,
            PageSize: PageSize,
            TypeFilter: Enum.TryParse<SearchTypeFilter>(query["type"], true, out var type) ? type : SearchTypeFilter.All,
            SortOrder: Enum.TryParse<SearchSortOrder>(query["sort"], true, out var sort) ? sort : SearchSortOrder.Relevance)
        {
            Categories = query["category"]
                .Select(value => Guid.TryParse(value, out var key) ? key : (Guid?)null)
                .OfType<Guid>()
                .Distinct()
                .ToList()
        };

        var results = await _searchService.SearchAsync(request, cancellationToken);

        var model = new SearchPageViewModel(page)
        {
            Request = request,
            Results = results,
            SiteName = _contentCache.GetById(siteRootKey)?.Name ?? "Unknown site",
            EngineName = _searchService.EngineName
        };

        return View("~/Views/SiteSearch/Results.cshtml", model);
    }
}
