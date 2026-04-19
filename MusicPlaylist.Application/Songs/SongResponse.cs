namespace MusicPlaylist.Application.Songs;

public sealed record SongResponse(
    long Id,
    string Title,
    string Artist,
    string Album,
    int DurationSeconds,
    string Genre,
    DateOnly ReleaseDate);
