namespace vkine.Utilities;

/// <summary>
/// Utility class for building contiguous ranges from a list of indexes.
/// </summary>
public static class IndexRangeBuilder
{
    /// <summary>
    /// Converts a list of sorted indexes into contiguous ranges.
    /// For example, [1, 2, 3, 5, 6, 9] becomes [(1, 3), (5, 2), (9, 1)].
    /// </summary>
    /// <param name="indexes">List of sorted indexes to convert into ranges.</param>
    /// <returns>Enumerable of (start, length) tuples representing contiguous ranges.</returns>
    public static IEnumerable<(int start, int length)> BuildRanges(List<int> indexes)
    {
        if (indexes.Count == 0)
        {
            yield break;
        }

        var start = indexes[0];
        var length = 1;

        for (var i = 1; i < indexes.Count; i++)
        {
            var current = indexes[i];
            var previous = indexes[i - 1];

            if (current == previous + 1)
            {
                length++;
            }
            else
            {
                yield return (start, length);
                start = current;
                length = 1;
            }
        }

        yield return (start, length);
    }
}
