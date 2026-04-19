namespace MusicPlaylist.Application.Songs;

public interface ISongService
{
    Task<IReadOnlyList<SongResponse>> ListAsync(
        string? genre,
        string? artist,
        CancellationToken cancellationToken = default);

    Task<(SongResponse? Song, string? ErrorMessage)> CreateAsync(
        CreateSongRequest request,
        CancellationToken cancellationToken = default);
}
