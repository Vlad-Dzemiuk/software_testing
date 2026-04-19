namespace MusicPlaylist.Application.Playlists;

public sealed record UpdatePlaylistRequest(string Name, string? Description, bool IsPublic);
