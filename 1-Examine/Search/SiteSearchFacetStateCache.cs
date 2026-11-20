using System.Runtime.CompilerServices;
using Lucene.Net.Facet.SortedSet;
using Lucene.Net.Index;

namespace SearchDemo.Search;

/// <summary>
/// The facet reader state is expensive to build, so keep one per index reader.
/// Examine opens a new reader after the index changes, which then gets new state.
/// </summary>
public class SiteSearchFacetStateCache
{
    private readonly ConditionalWeakTable<IndexReader, SortedSetDocValuesReaderState?> _states = new();

    /// <summary>
    /// Null when the index has no facet data yet, e.g. before it has been rebuilt.
    /// </summary>
    public SortedSetDocValuesReaderState? Get(IndexReader reader) => _states.GetValue(reader, CreateState);

    private static SortedSetDocValuesReaderState? CreateState(IndexReader reader)
    {
        try
        {
            return new DefaultSortedSetDocValuesReaderState(reader);
        }
        catch (ArgumentException)
        {
            // Thrown when no document has a "$facets" field
            return null;
        }
    }
}
