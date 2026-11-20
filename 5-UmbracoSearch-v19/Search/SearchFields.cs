namespace SearchDemo.Search;

public static class SearchFields
{
    public const string IndexName = "SiteSearchIndex";

    // Custom fields added in SiteSearchContentIndexer
    public const string SiteRootKey = "siteRootKey";
    public const string IsBlogPost = "isBlogPost";
    public const string HasTemplate = "hasTemplate";
    public const string CategoryKey = "categoryKey";

    // The same keys again, as a facet field (Examine 4 has no raw facet type, see ConfigureSiteSearchIndexOptions)
    public const string CategoryFacet = "categoryFacet";

    public const string Yes = "1";
    public const string No = "0";

    // Clean's blog post property holding the picked categories
    public const string Categories = "categories";

    // Fields SiteSearchIndexer maps from Umbraco Search's system fields
    public const string NodeName = "nodeName";
    public const string UpdateDate = "updateDate";
    public const string PathKeys = "pathKeys";

    // Fields searched for each keyword: the name plus these properties' texts
    public static readonly string[] TextProperties = ["title", "subtitle", "metaDescription", "contentRows"];
    public static readonly string[] TextFields = [NodeName, .. TextProperties];
}

public static class DocTypes
{
    public const string Article = "article";
    // Clean 8 makes categories library elements, shared by both sites, instead of content pages under each site
    public const string Category = "categoryElement";

    // Same exclusions as Clean's own search page
    public static readonly string[] ExcludedFromSearch = ["category", "categoryList", "error", "search", "xMLSitemap"];
}
