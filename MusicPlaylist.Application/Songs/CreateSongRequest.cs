namespace MusicPlaylist.Application.Songs;

public sealed record CreateSongRequest(
    string Title,
    string Artist,
    string Album,
    int DurationSeconds,
    string Genre,
    DateOnly ReleaseDate);
