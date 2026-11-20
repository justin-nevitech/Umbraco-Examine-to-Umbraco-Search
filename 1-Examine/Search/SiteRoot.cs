using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;

namespace SearchDemo.Search;

public static class SiteRoot
{
    /// <summary>
    /// Path is "-1,rootId,...,thisId", so the site root is the second segment.
    /// Uses IIdKeyMap rather than the published cache, so it works during indexing.
    /// </summary>
    public static Guid? GetRootKey(string? path, IIdKeyMap idKeyMap)
    {
        var segments = path?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? [];
        if (segments.Length < 2 || int.TryParse(segments[1], out var rootId) is false)
        {
            return null;
        }

        var attempt = idKeyMap.GetKeyForId(rootId, UmbracoObjectTypes.Document);
        return attempt.Success ? attempt.Result : null;
    }
}
