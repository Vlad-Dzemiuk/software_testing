namespace MusicPlaylist.Application.Playlists;

public sealed record ReorderRequest(IReadOnlyList<ReorderItem> Items);
