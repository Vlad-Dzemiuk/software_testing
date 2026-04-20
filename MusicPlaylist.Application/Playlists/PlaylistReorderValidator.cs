using MusicPlaylist.Application.Common;

namespace MusicPlaylist.Application.Playlists;

public static class PlaylistReorderValidator
{
    public static ServiceError? Validate(
        IReadOnlyList<ReorderItem> items,
        IReadOnlyCollection<long> existingSongIds)
    {
        var n = existingSongIds.Count;
        if (items.Count != n)
        {
            return ServiceError.BadRequest($"Items must contain exactly {n} entries.");
        }

        if (items.Select(i => i.SongId).Distinct().Count() != items.Count)
        {
            return ServiceError.BadRequest("Duplicate songId in items.");
        }

        var reqIds = items.Select(i => i.SongId).ToHashSet();
        if (!reqIds.SetEquals(existingSongIds))
        {
            return ServiceError.BadRequest("Items must reference exactly the songs in this playlist.");
        }

        var positions = items.Select(i => i.Position).ToList();
        var distinctPos = new HashSet<int>(positions);
        if (distinctPos.Count != n || positions.Any(p => p < 1 || p > n))
        {
            return ServiceError.BadRequest("Positions must be a permutation of 1 through N with no duplicates.");
        }

        return null;
    }
}

