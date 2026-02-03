using vkine.Models;

namespace vkine.Services.Caching;

/// <summary>
/// FIFO cache for movies addressed by their zero-based index in the backing store.
/// </summary>
public sealed class MovieCache
{
    private readonly int _capacity;
    private readonly Dictionary<int, Movie> _items = new();
    private readonly Queue<int> _order = new();
    private readonly object _sync = new();

    public MovieCache(int capacity)
    {
        _capacity = capacity;
    }

    public Dictionary<int, Movie> SnapshotForIndexes(IEnumerable<int> indexes, out List<int> missingIndexes)
    {
        if (_capacity <= 0)
        {
            missingIndexes = indexes.Distinct().Order().ToList();
            return new Dictionary<int, Movie>();
        }

        var hits = new Dictionary<int, Movie>();
        missingIndexes = new List<int>();

        lock (_sync)
        {
            foreach (var index in indexes)
            {
                if (_items.TryGetValue(index, out var movie))
                {
                    hits[index] = movie;
                }
                else
                {
                    missingIndexes.Add(index);
                }
            }
        }

        missingIndexes.Sort();
        return hits;
    }

    public void Store(int index, Movie movie)
    {
        if (_capacity <= 0)
        {
            return;
        }

        lock (_sync)
        {
            if (_items.ContainsKey(index))
            {
                _items[index] = movie;
                return;
            }

            _items[index] = movie;
            _order.Enqueue(index);

            while (_items.Count > _capacity && _order.TryDequeue(out var toRemove))
            {
                if (_items.Remove(toRemove))
                {
                    break;
                }
            }
        }
    }
}
