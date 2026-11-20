namespace SearchDemo.Search;

public static class SearchFields
{
    public const string IndexName = "SiteSearchIndex";

    // Custom fields added in SiteSearchIndexValuesHandler
    public const string SiteRootKey = "siteRootKey";
    public const string IsBlogPost = "isBlogPost";
    public const string CategoryKey = "categoryKey";

    // The same keys again, as a facet field (Examine 4 has no raw facet type, see ConfigureSiteSearchIndexOptions)
    public const string CategoryFacet = "categoryFacet";

    public const string Yes = "1";
    public const string No = "0";

    // Clean's blog post property holding the picked categories
    public const string Categories = "categories";

    // Umbraco fields used by the query
    public const string TemplateId = "templateID";
    public const string UpdateDate = "updateDate";

    // Fields searched for each keyword
    public static readonly string[] TextFields = ["nodeName", "title", "subtitle", "metaDescription", "contentRows"];
}

public static class DocTypes
{
    public const string Article = "article";
    public const string Category = "category";

    // Same exclusions as Clean's own search page
    public static readonly string[] ExcludedFromSearch = ["category", "categoryList", "error", "search", "xMLSitemap"];
}
