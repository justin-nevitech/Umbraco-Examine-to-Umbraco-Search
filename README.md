# Examine vs Umbraco Search: four migration paths on Umbraco 17, and one on Umbraco 19

Five demo projects built on Paul Seal's Clean starter kit, each implementing the same multi-site search with category
facets: four Umbraco 17 sites, one per way of moving from Examine to Umbraco Search, and project 4 upgraded to the
Umbraco 19 RC:

| Folder | Search | Index kept up to date by | Facets | Umbraco 19 |
|---|---|---|---|---|
| `1-Examine` | Custom `SiteSearchIndex` (`UmbracoContentIndex`), Examine fluent API | Umbraco core | Lucene.Net facet module (Examine 3.10 has none) | ✗ core Examine plumbing removed |
| `2-UmbracoSearch` | Built-in `Umb_PublishedContent`, `ISearcher` | Umbraco Search | `KeywordFacet` | ✓ |
| `3-UmbracoSearch-CustomIndex` | Umbraco Search installed, search still on a custom `SiteSearchIndex` (`UmbracoContentIndex`) | Umbraco core | Examine 4's built-in facets | ✗ works on 17 and 18 only |
| `4-UmbracoSearch-ExamineIndexer` | Umbraco Search installed, search on a plain Examine `LuceneIndex` | Umbraco Search, through a custom `IIndexer` | Examine 4's built-in facets | ✓ see project 5 |
| `5-UmbracoSearch-v19` | Project 4 on Umbraco 19.0.0-rc1 and Clean 8 | Umbraco Search (built into 19), through the same `IIndexer` | Examine 4's built-in facets | ✓ runs on 19.0.0-rc1 |

**The front end is the same in all four.** Views, script, URLs, query parameters, facet panel, counts, chips and
AJAX behaviour are shared; only `Search/`, package references and ports differ. The banner ("Searching … using …") names the engine. The only
differences in results are the keyword-matching ones listed under [Keyword matching](#keyword-matching).
Project 5 is the exception: Umbraco 19 needs Clean 8, so its views and content differ (see
[Tested on Umbraco 19.0.0-rc1](#tested-on-umbraco-1900-rc1)).

Diffs of each project against `1-Examine` are in `diffs/` (`1-vs-2.diff`, `1-vs-3.diff`, `1-vs-4.diff`), and
`diffs/4-vs-5.diff` is the Umbraco 19 upgrade of project 4.
The slides for the accompanying talk are in `Examine-to-Umbraco-Search.pptx`.

## Packages

| Package | Version | Notes |
|---|---|---|
| Umbraco.Cms | 17.7.1 | Umbraco 17 LTS (17.8.2 has since been released; the demo hasn't moved to it) |
| Clean | 7.0.8 | Latest Clean for Umbraco 17 (8.x targets Umbraco 18) |
| uSync | 17.4.3 | |
| Umbraco.Cms.Search.Core, .BackOffice, .Provider.Examine | 17.2.0 | Projects 2–4. The Examine provider is now stable |
| Examine | 4.0.0 | Projects 2–4, brought in by the Examine provider (project 1 stays on Umbraco's Examine 3.10.0) |

Project 5 uses Umbraco.Cms 19.0.0-rc1 (on .NET 11; it brings in Umbraco Search and Examine 4.0.1), Clean 8.0.2
(the Umbraco 18 line; Clean 19.0.0-rc03, built against 19.0.0-rc1, has since been released) and uSync 19.0.0-beta01
(uSync 19.0.0-rc1 has since been released).

`dotnet build` reports NuGet vulnerability warnings in projects 1–4 for packages that come in through Umbraco:
SixLabors.ImageSharp 3.1.12, System.Security.Cryptography.Pkcs 10.0.0 and MessagePack 3.1.8. Project 5 has none.
No overrides are applied.

## Running

Requires the .NET 10 SDK, plus the .NET 11 SDK (RC 3 or later) for project 5. Each project installs itself unattended
with SQLite into its own `umbraco/Data/`.

```bash
dotnet run --project 1-Examine
dotnet run --project 2-UmbracoSearch
dotnet run --project 3-UmbracoSearch-CustomIndex
dotnet run --project 4-UmbracoSearch-ExamineIndexer
dotnet run --project 5-UmbracoSearch-v19
```

| | 1-Examine | 2-UmbracoSearch | 3-UmbracoSearch-CustomIndex | 4-UmbracoSearch-ExamineIndexer | 5-UmbracoSearch-v19 |
|---|---|---|---|---|---|
| Backoffice | https://localhost:44310/umbraco | https://localhost:44320/umbraco | https://localhost:44330/umbraco | https://localhost:44340/umbraco | https://localhost:44350/umbraco |
| Site 1 | https://localhost:44310 | https://localhost:44320 | https://localhost:44330 | https://localhost:44340 | https://localhost:44350 |

Login: `admin@example.com` / `1234567890`

The second site is given `https://localhost:50000`, a placeholder that nothing listens on. It isn't meant to be
browsed: it's there so the index holds a second copy of every page, which site 1's search filters out.

### What happens on first run

1. Umbraco installs unattended.
2. Clean's package migration imports its document types, content and views, and publishes the home page.
3. `DemoSiteSeeder` copies the Clean site to a second root called "Second Site", publishes it, and
   assigns a hostname to each site. Every page keeps the same name, and so the same URL, as on site 1.
   Copying doesn't update pickers, so the seeder also repoints each copied blog post's categories at
   site 2's category with the same name.
4. In projects 2–4, Umbraco Search indexes the second site in the background, about a minute after it is
   created: its indexing queue first works through the index rebuilds queued at startup.

On the first boot after an install, Clean's site name and "Menu" label can be blank. They appear after a restart.

### Things to try

- Visit `/search` and search for "umbraco". Every page exists twice in the index with the same title, once per
  site, but results only come from site 1.
- Filter by Blog posts or Pages. Each result shows a Blog post or Page badge.
- Sort by Latest or Oldest, and page through results (5 per page).
- Tick one or more categories in the Categories panel. A blog post matches if it has any ticked category. The
  counts reflect the current search and ignore the ticked categories, so they don't shrink as you tick more.
  The panel is hidden for Pages. Ticked categories also appear as removable chips under the result count.
- Searching, ticking categories, removing chips and paging update the results without a page reload
  (`wwwroot/js/site-search.js`). The URL still changes, so Back, bookmarks and shared links work, and the page
  works the same with JavaScript off. This is switched on by `DemoSites:AjaxSearch` in `appsettings.Development.json`.
- In projects 2–5, Settings > Search lists the Umbraco Search indexes; in projects 4 and 5 `SiteSearchIndex` is one
  of them and can be rebuilt there.

## What's different

| Concern | 1-Examine | 2-UmbracoSearch | 3-UmbracoSearch-CustomIndex | 4-UmbracoSearch-ExamineIndexer | 5-UmbracoSearch-v19 |
|---|---|---|---|---|---|
| Index | Custom `UmbracoContentIndex` | Built-in `Umb_PublishedContent` | Custom `UmbracoContentIndex` | Plain `LuceneIndex`, registered with Umbraco Search (`RegisterContentIndex`) | As project 4 |
| Populated by | Umbraco's value set builders | Umbraco Search | Umbraco's value set builders | Umbraco Search, calling `SiteSearchIndexer` (`IIndexer`) | As project 4 (Umbraco Search is built into 19) |
| Extra fields | `TransformingIndexValues` | `IContentIndexer` | `TransformingIndexValues` | `IContentIndexer` | `IContentIndexer`; category keys parsed from the element picker's JSON |
| Excluding doc types, protected content | Index validator | `KeywordFilter` on `ContentTypeId` (negated); protection is filtered by the provider | Index validator | Checked in `SiteSearchIndexer` | As project 4 |
| Pages with templates only | `RangeQuery<int>("templateID", …)` | `hasTemplate` keyword | `RangeQuery<int>("templateID", …)` | `hasTemplate` keyword | As project 4 |
| Keyword search | `GroupedOr` over 5 named fields, trailing wildcards | `query` over every text field | As project 1 | As project 1, over the fields `SiteSearchIndexer` maps | As project 4 |
| Sort by date | `SortableField("updateDate", SortType.Long)` | `DateTimeOffsetSorter` on `Umb_UpdateDate` | As project 1 | As project 1 (`updateDate` mapped from `Umb_UpdateDate`) | As project 4 |
| Category filter | `GroupedOr` on raw `categoryKey` | `KeywordFilter` on `categoryKey` | As project 1 | As project 1 | As project 1 |
| Category counts | Lucene.Net facet module, one pass (`SiteSearchFacets`, `SiteSearchFacetStateCache`) | `KeywordFacet` in a second, facet-only search | Examine 4 `WithFacets(FacetString(...))`, second query | As project 3 | As project 3; names from `IPublishedElementCache` |
| Old default indexes | Untouched | `DisableDefaultExamineIndexes()` | `DisableDefaultExamineIndexes()` | `DisableDefaultExamineIndexes()` | Removed in 19 |
| Rebuild | Settings > Examine Management | Settings > Search | Management API `indexer/SiteSearchIndex/rebuild` (not an Umbraco Search index, so not in Settings > Search) | Settings > Search; also rebuilt on boot if the index folder is missing | Settings > Search; 19 rebuilds a missing index about two minutes after startup |

### Facets in each project

The rule is the same everywhere: counts reflect the current search and filters except the category filter itself,
and ticked categories with no matches stay listed so they can be unticked.

- **1-Examine.** Examine 3.10 has no facet API. `SiteSearchIndexValuesHandler` handles `LuceneIndex.DocumentWriting`
  and adds a `SortedSetDocValuesFacetField` per category (`SiteSearchFacets`). `SiteSearchService` takes the Lucene
  query Examine's fluent API builds (`LuceneSearchQuery.Query`), adds the `__IndexType` clause and counts every
  category in one pass with a `FacetsCollector` on the raw `IndexSearcher`. `LuceneSearchQuery.Query` and
  `ISearcherReference` are public but low level, so an Examine upgrade could change them.
- **2-UmbracoSearch.** `FieldOptions` marks `categoryKey` as `Facetable` (the provider only builds facet data for
  configured fields). The results query filters with `KeywordFilter`; the counts come from a second search with
  `KeywordFacet("categoryKey")`, `take: 0` and no category filter. `SearcherOptions.ExpandFacetValues` would do the
  same, but it is a global setting that applies to every search, so it is left off.
- **3 and 4.** Examine 4's built-in facets. The keys are indexed twice: `categoryKey` (raw, for filtering) and
  `categoryFacet` (`FacetFullText`, for counting). Examine 4 has no raw facet type, and filtering on the analysed
  field changes the ranking because shorter fields score higher. Umbraco Search's own provider does the same for
  keyword facets (`Field_x_keywords` to filter, `__Query_Field_x_keywords` to facet and sort). Examine 4 stores facets
  in a taxonomy sidecar index by default; the provider turns that off for every index, and both projects also set
  `UseTaxonomyIndex = false` in their own options.

### Keyword matching

Checked by comparing the search page HTML of all four on fresh installs, for 72 searches (a spread of type, sort,
paging and category-filter cases, plus edge cases).

- **Project 3 matches project 1 exactly** on every page.
- **Project 4 matches project 1** except for picker references in blocks: project 1's `contentRows` text is Umbraco
  core's summary of the nested block values, which includes `umb://document/...` content picker references, so "umb"
  also finds the Blog page. Umbraco Search turns block content into plain text and indexes picked content as keyword
  keys, so project 4 doesn't match it. Reproducing project 1 exactly would mean recreating core's block text, which
  comes from `IPropertyIndexValueFactory`, removed in Umbraco 19.
- **Project 2** searches every text field Umbraco Search indexed, with its own relevance boosts:
  - "mastodon" also finds Home, through the `socialIconLinks` block list.
  - "umb" doesn't find the Blog page (as project 4).
  - Results come back in a different order for "umbraco", "umbraco community", "clean", "paul" and "a/b" (11 of the
    72 pages); the totals are the same, ranked differently.
- Filters, facets, sorting and paging give identical pages in all four.

### Identical in all four projects

`Program.cs`, `appsettings.json` (apart from the site ID and imaging secret each install writes),
`Controllers/SearchController.cs`, `Models/SearchPageViewModel.cs`, `Views/`, `wwwroot/`, `Search/SiteSearchModels.cs`,
`Search/SiteRoot.cs` and everything in `DemoSites/`. `appsettings.Development.json` and `Properties/launchSettings.json`
differ only in ports.

## Umbraco 19

Checked against the Umbraco-CMS `v19/dev` source at
[9d3eff0](https://github.com/umbraco/Umbraco-CMS/tree/9d3eff07bf005bb29af1e73f4fa2862cd15eb341/src) (20 October 2026).
Umbraco 19 isn't released, so this section is a source comparison. Project 5 then ran it on 19.0.0-beta1 and 19.0.0-rc1: see
[Tested on Umbraco 19.0.0-rc1](#tested-on-umbraco-1900-rc1) below.

**Removed in 19:** the `Umbraco.Examine.Lucene` project (`UmbracoContentIndex`), and Umbraco's Examine types in
`Umbraco.Infrastructure` (`ContentValueSetValidator`, `UmbracoFieldDefinitionCollection`, `UmbracoExamineFieldNames`,
`IndexTypes`, `ExamineUmbracoIndexingHandler` and the value set builders); `IPropertyIndexValueFactory`;
`DisableDefaultExamineIndexes()`; `AddBackOfficeSearch()` and the separate `Umbraco.Cms.Search.BackOffice` package
(back office search is built in). Examine's own `TransformingIndexValues` event still fires, but only for the
indexes Umbraco Search's provider writes, with Umbraco Search's fields; core's value set builders and
`UmbracoContentIndex` are gone. Projects 1 and 3 can't be upgraded as they are.

**Index folders on upgrade:** the 19 migration `RemoveLegacyExamineIndexFiles` deletes every folder in
`umbraco/Data/TEMP/ExamineIndexes` except the four `Umb_*` ones, custom indexes included. Project 4's `SiteSearchIndex`
is registered with Umbraco Search, and 19 rebuilds any missing registered index in a background job about two minutes
after startup.

**Built in:** Umbraco Search Core and the Examine provider are part of the CMS and register themselves
([SearchCoreComposer](https://github.com/umbraco/Umbraco-CMS/blob/9d3eff07bf005bb29af1e73f4fa2862cd15eb341/src/Umbraco.Cms.Search.Core/DependencyInjection/SearchCoreComposer.cs),
[ExamineSearchProviderComposer](https://github.com/umbraco/Umbraco-CMS/blob/9d3eff07bf005bb29af1e73f4fa2862cd15eb341/src/Umbraco.Cms.Search.Provider.Examine/DependencyInjection/ExamineSearchProviderComposer.cs)).
Examine stays on 4 (4.0.1 in 19.0.0-rc1).

**Upgrading project 2 or 4:**

1. Remove the three `Umbraco.Cms.Search.*` package references and upgrade `Umbraco.Cms` to 19.
2. Remove `.AddBackOfficeSearch()` and `DisableDefaultExamineIndexes()` from the composer. `AddSearchCore()` and
   `AddExamineSearchProvider()` can stay (both are idempotent) or go.
3. Change namespaces. The types are unchanged, including `IIndexer`, `ISearcher`, `IContentIndexer` and
   `IndexOptions.RegisterContentIndex`:

| 17/18 (Umbraco.Cms.Search.*) | 19 |
|---|---|
| `Umbraco.Cms.Search.Core.Services` (`IIndexer`, `ISearcher`) | [`Umbraco.Cms.Core.Search`](https://github.com/umbraco/Umbraco-CMS/blob/9d3eff07bf005bb29af1e73f4fa2862cd15eb341/src/Umbraco.Core/Search/IIndexer.cs) |
| `Umbraco.Cms.Search.Core.Services.ContentIndexing` (`IContentIndexer`, `IPublishedContentChangeStrategy`) | [`Umbraco.Cms.Core.Search.Indexing`](https://github.com/umbraco/Umbraco-CMS/blob/9d3eff07bf005bb29af1e73f4fa2862cd15eb341/src/Umbraco.Core/Search/Indexing/IContentIndexer.cs) |
| `Umbraco.Cms.Search.Core.Models.Indexing` (`IndexField`, `IndexValue`, `Variation`, `ContentProtection`, `IndexMetadata`, `HealthStatus`) | [`Umbraco.Cms.Core.Search.Indexing`](https://github.com/umbraco/Umbraco-CMS/tree/9d3eff07bf005bb29af1e73f4fa2862cd15eb341/src/Umbraco.Core/Search/Indexing) |
| `Umbraco.Cms.Search.Core.Models.Searching` (`SearchResult`, `Document`, `AccessContext`) and `.Filtering`, `.Sorting`, `.Faceting` | [`Umbraco.Cms.Core.Search.Querying`](https://github.com/umbraco/Umbraco-CMS/tree/9d3eff07bf005bb29af1e73f4fa2862cd15eb341/src/Umbraco.Core/Search/Querying) and `.Filtering`, `.Sorting`, `.Faceting` |
| `Umbraco.Cms.Search.Core.Configuration.IndexOptions` | [`Umbraco.Cms.Core.Search.Configuration`](https://github.com/umbraco/Umbraco-CMS/blob/9d3eff07bf005bb29af1e73f4fa2862cd15eb341/src/Umbraco.Core/Search/Configuration/IndexOptions.cs) |
| `Umbraco.Cms.Search.Core.Constants` (`IndexAliases`, `FieldNames`) | [`Umbraco.Cms.Core.Constants.Search`](https://github.com/umbraco/Umbraco-CMS/blob/9d3eff07bf005bb29af1e73f4fa2862cd15eb341/src/Umbraco.Core/Constants-Search.cs) |
| `Umbraco.Cms.Search.Core.Extensions` (`AsKeyword`) | [`Umbraco.Cms.Core.Extensions`](https://github.com/umbraco/Umbraco-CMS/blob/9d3eff07bf005bb29af1e73f4fa2862cd15eb341/src/Umbraco.Core/Extensions/GuidExtensions.cs) |
| `Umbraco.Cms.Infrastructure.Examine.ConfigurationEnabledDirectoryFactory` (project 4) | [`Umbraco.Cms.Search.Provider.Examine.Lucene`](https://github.com/umbraco/Umbraco-CMS/blob/9d3eff07bf005bb29af1e73f4fa2862cd15eb341/src/Umbraco.Cms.Search.Provider.Examine/Lucene/ConfigurationEnabledDirectoryFactory.cs) |
| `Umbraco.Cms.Search.Provider.Examine.Configuration` (`FieldOptions`, `FieldValues`) | Unchanged |

Project 4 uses nothing else from Umbraco's Examine plumbing, so these are the only edits it needs. 19 also adds a
`SearchEnabledPublishedContentQuery`, so `IPublishedContentQuery.Search()` (Clean's own search) works again on Umbraco
Search, according to [Kenn Jacobsen's migration post](https://kjac.dev/posts/from-examine-to-umbraco-search/); not tested here.

### Tested on Umbraco 19.0.0-rc1

`5-UmbracoSearch-v19` is project 4 upgraded with the steps above, on 19.0.0-beta1 (11 November 2026). Moving it to
19.0.0-rc1 (12 November) needed only the package versions; the beta1 database ran on the RC unchanged, and a fresh
install on the RC hasn't been tested. The full change is `diffs/4-vs-5.diff`.

**The search code upgraded exactly as described.** Apart from Clean 8's categories (below), `Search/` needed only the
package and namespace changes, and removing `AddBackOfficeSearch()` and `DisableDefaultExamineIndexes()` (with `AddSearchCore()` and
`AddExamineSearchProvider()`, which 19's own composers cover). It builds with no warnings. 19 rebuilt the missing
`SiteSearchIndex` in its background job two minutes after startup (`Queued rebuild of index SiteSearchIndex`).

**Outside the search code**, Umbraco 19 broke:

- **.NET 11.** Umbraco 19 targets `net11.0`.
- **`PublishedContentWrapped`** lost its `IPublishedValueFallback` constructor parameter (`SearchPageViewModel`).
- **Typed models** ([#23853](https://github.com/umbraco/Umbraco-CMS/releases/tag/release-19.0.0-beta1)): a property's
  type now follows its editor, not its configuration. Single dropdowns, media pickers and URL pickers have their own
  editors, and a multinode tree picker is single only when its range maximum is 1; the 19 upgrade migrates existing
  data types to match. Clean 8's package creates its data types with the old settings, which 19 ignores, so on a fresh
  install they return `IEnumerable<…>` and Clean's views needed `.FirstOrDefault()` on `MainImage`, `Author`, `Image`,
  `Icon`, `Link` and the spacing dropdowns.
- **`IPublishedContent.Children` and `.Parent`** are gone; the `Children()` and `Parent()` extension methods remain.
- **`BlockListItem.ContentUdi`** is gone; `ContentKey` remains.
- **Clean 7 can't run on 19.** It's compiled against Swashbuckle's SwaggerGen, which 19 no longer ships (it uses
  Microsoft.AspNetCore.OpenApi). With SwaggerGen added back, boot fails on
  `Method not found: PublishResult.get_Content()`. So project 5 uses Clean 8.0.2, which runs on 19 once the views above
  are fixed.

**Clean 8 changes the content**, so project 5 doesn't match projects 1–4 page for page:

- **Categories are library Elements** (`categoryElement`, picked with `Umbraco.ElementPicker` and stored as a JSON
  array of keys) shared by both sites, instead of content pages under each site. Project 5's content indexer parses
  the keys, and the facet names come from `IPublishedElementCache`. Some posts are categorised differently, so
  "Resources", unused in Clean 7, now has a post.
- **One blog post is replaced** ("Codegarden 2026" instead of "Join the Umbraco Community on Mastodon").
- **The rich text block property is `richContent`**, not `content` (Clean 8's own package).

**Verified on both sites** (checking behaviour, because the content differs):
- every facet count equals the result count with that category ticked;
- counts don't change as categories are ticked;
- multi-category filters are OR;
- All = blog posts + pages;
- paging returns every result once, five per page;
- each site returns only its own pages;
- the AJAX test behaves as on project 4.

Not tested on 19: counts updating after a publish (that needs a back office session), and
`IPublishedContentQuery.Search()`.

## Caveats

- **Content indexer output is cached** in `umbracoIndexDocument`. After changing a `SiteSearchContentIndexer`,
  clear that table (or republish the content) and rebuild the indexes.
- **Copied content isn't in the draft index.** Umbraco Search 17.2.0 doesn't react to copies, so the seeder's copies
  are missing from `Umb_Content` (back office search) until it is rebuilt. The published indexes are fine, because
  the seeder publishes the copies.
- **Project 3's index isn't rebuilt on a normal boot** if its folder is deleted. Rebuild it with `POST /umbraco/management/api/v1/indexer/SiteSearchIndex/rebuild`. Until then the search is
  empty and the facet panel hidden.
- **Clean's own search.** `DisableDefaultExamineIndexes()` breaks `IPublishedContentQuery.Search()`, which Clean's
  `search.cshtml` and headless search API use. The demo's `SearchController` route-hijacks the search page, so
  Clean's view isn't used.
- **Project 5 runs a release candidate.** 19.0.0-rc1 and uSync 19.0.0-beta01 can change before release, it was built
  with the .NET 11 RC 3 SDK, and Clean 8 targets Umbraco 18.
- **uSync** is installed so you can export and import the demo setup. Each project's `uSync/` folder holds the content
  and domains exported on save, but nothing is imported on boot: Clean's own package migration creates the content.
