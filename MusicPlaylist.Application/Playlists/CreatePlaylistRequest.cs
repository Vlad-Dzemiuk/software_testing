namespace MusicPlaylist.Application.Playlists;

public sealed record CreatePlaylistRequest(string Name, string? Description, bool IsPublic);
