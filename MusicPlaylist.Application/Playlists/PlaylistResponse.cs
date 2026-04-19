namespace MusicPlaylist.Application.Playlists;

public sealed record PlaylistResponse(
    long Id,
    string Name,
    string? Description,
    DateTimeOffset CreatedAt,
    bool IsPublic);
