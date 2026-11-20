using Microsoft.Extensions.Options;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.ContentEditing;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;

namespace SearchDemo.DemoSites;

/// <summary>
/// Demo plumbing only, identical in both projects.
/// Clean installs one site. On first run this copies it to a second root called "Second Site",
/// publishes it, and assigns a hostname to each site. Pages keep the same names as site 1.
/// </summary>
public class DemoSiteSeeder : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    private const string HomeAlias = "home";
    private const string ArticleAlias = "article";
    private const string CategoryAlias = "category";
    private const string CategoriesProperty = "categories";

    private readonly IRuntimeState _runtimeState;
    private readonly IContentService _contentService;
    private readonly IDomainService _domainService;
    private readonly ILanguageService _languageService;
    private readonly DemoSiteOptions _options;
    private readonly ILogger<DemoSiteSeeder> _logger;

    public DemoSiteSeeder(
        IRuntimeState runtimeState,
        IContentService contentService,
        IDomainService domainService,
        ILanguageService languageService,
        IOptions<DemoSiteOptions> options,
        ILogger<DemoSiteSeeder> logger)
    {
        _runtimeState = runtimeState;
        _contentService = contentService;
        _domainService = domainService;
        _languageService = languageService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task HandleAsync(UmbracoApplicationStartedNotification notification, CancellationToken cancellationToken)
    {
        if (_runtimeState.Level != RuntimeLevel.Run)
        {
            return;
        }

        var sites = _contentService.GetRootContent()
            .Where(x => x.ContentType.Alias == HomeAlias)
            .OrderBy(x => x.SortOrder)
            .ToList();

        if (sites.Count == 0)
        {
            _logger.LogWarning("Demo seeding skipped: no Clean home page found. Has the Clean package migration run?");
            return;
        }

        if (sites.Count == 1)
        {
            sites.Add(CreateSecondSite(sites[0]));
        }

        await EnsureDomainAsync(sites[0], _options.Site1Domain);
        await EnsureDomainAsync(sites[1], _options.Site2Domain);
    }

    private IContent CreateSecondSite(IContent site1)
    {
        var site2 = _contentService.Copy(site1, Constants.System.Root, relateToOriginal: false, recursive: true)
            ?? throw new InvalidOperationException("Could not copy the Clean home page.");

        site2.Name = "Second Site";
        _contentService.Save(site2);

        RemapCategories(site1, site2);

        _contentService.PublishBranch(site2, PublishBranchFilter.All, []);
        _logger.LogInformation("Demo seeding: created and published '{Name}'", site2.Name);

        return site2;
    }

    /// <summary>
    /// Copying doesn't update pickers, so the copied blog posts still point at site 1's categories.
    /// Repoint them at site 2's category with the same name. Returns the number of posts changed.
    /// </summary>
    private int RemapCategories(IContent site1, IContent site2)
    {
        var site1CategoryNames = GetDescendants(site1)
            .Where(x => x.ContentType.Alias == CategoryAlias)
            .ToDictionary(x => x.Key, x => x.Name);

        var site2Descendants = GetDescendants(site2);
        var site2CategoryKeys = site2Descendants
            .Where(x => x.ContentType.Alias == CategoryAlias && x.Name is not null)
            .GroupBy(x => x.Name!)
            .ToDictionary(x => x.Key, x => x.First().Key);

        var changed = 0;
        foreach (var post in site2Descendants.Where(x => x.ContentType.Alias == ArticleAlias))
        {
            var current = post.GetValue<string>(CategoriesProperty);
            if (string.IsNullOrWhiteSpace(current))
            {
                continue;
            }

            var remapped = string.Join(',', current
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value =>
                    UdiParser.TryParse(value, out Udi? udi) && udi is GuidUdi guidUdi
                    && site1CategoryNames.TryGetValue(guidUdi.Guid, out var name) && name is not null
                    && site2CategoryKeys.TryGetValue(name, out var site2Key)
                        ? Udi.Create(Constants.UdiEntityType.Document, site2Key).ToString()
                        : value));

            if (remapped != current)
            {
                post.SetValue(CategoriesProperty, remapped);
                _contentService.Save(post);
                changed++;
            }
        }

        return changed;
    }

    private List<IContent> GetDescendants(IContent root) =>
        _contentService.GetPagedDescendants(root.Id, 0, 1000, out _).ToList();

    private async Task EnsureDomainAsync(IContent site, string domainName)
    {
        if (string.IsNullOrWhiteSpace(domainName))
        {
            return;
        }

        var existing = await _domainService.GetAssignedDomainsAsync(site.Key, includeWildcards: false);
        // Replaces any other domains, so changing the configured hostname takes effect on an existing site
        if (existing.Any(d => string.Equals(d.DomainName, domainName, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        var isoCode = await _languageService.GetDefaultIsoCodeAsync();
        var result = await _domainService.UpdateDomainsAsync(site.Key, new DomainsUpdateModel
        {
            DefaultIsoCode = isoCode,
            Domains = [new DomainModel { DomainName = domainName, IsoCode = isoCode }]
        });

        _logger.LogInformation("Demo seeding: assigned {Domain} to '{Name}' ({Status})", domainName, site.Name, result.Status);
    }
}
