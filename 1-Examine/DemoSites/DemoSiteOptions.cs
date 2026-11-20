namespace SearchDemo.DemoSites;

public class DemoSiteOptions
{
    public string Site1Domain { get; set; } = string.Empty;

    public string Site2Domain { get; set; } = string.Empty;

    // Updates search results without a page reload (wwwroot/js/site-search.js)
    public bool AjaxSearch { get; set; }
}
