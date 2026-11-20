using Lucene.Net.Documents;
using Lucene.Net.Facet;
using Lucene.Net.Facet.SortedSet;

namespace SearchDemo.Search;

/// <summary>
/// Category facets written with Lucene.Net's facet module, because Examine 3 has no facet API (Examine 4 does).
/// They are stored as sorted set doc values in Lucene's default "$facets" field.
/// </summary>
public static class SiteSearchFacets
{
    private static readonly FacetsConfig Config = CreateConfig();

    private static FacetsConfig CreateConfig()
    {
        // A blog post can be in several categories
        var config = new FacetsConfig();
        config.SetMultiValued(SearchFields.CategoryKey, true);
        return config;
    }

    public static void AddCategoryFacets(Document document, IEnumerable<string> categoryKeys)
    {
        var facets = new Document();
        foreach (var key in categoryKeys)
        {
            facets.Add(new SortedSetDocValuesFacetField(SearchFields.CategoryKey, key));
        }

        // Build turns the facet fields into the "$facets" fields Lucene actually indexes
        foreach (var field in Config.Build(facets))
        {
            document.Add(field);
        }
    }
}
