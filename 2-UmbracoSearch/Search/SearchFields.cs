namespace SearchDemo.Search;

public static class SearchFields
{
    // Custom fields added in SiteSearchContentIndexer
    public const string SiteRootKey = "siteRootKey";
    public const string IsBlogPost = "isBlogPost";
    public const string HasTemplate = "hasTemplate";
    public const string CategoryKey = "categoryKey";

    public const string Yes = "1";
    public const string No = "0";

    // Clean's blog post property holding the picked categories
    public const string Categories = "categories";
}

public static class DocTypes
{
    public const string Article = "article";
    public const string Category = "category";

    // Same exclusions as Clean's own search page
    public static readonly string[] ExcludedFromSearch = ["category", "categoryList", "error", "search", "xMLSitemap"];
}
