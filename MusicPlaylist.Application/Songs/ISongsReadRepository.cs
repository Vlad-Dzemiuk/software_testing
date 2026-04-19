using MusicPlaylist.Domain.Entities;

namespace MusicPlaylist.Application.Songs;

public interface ISongsReadRepository
{
    Task<IReadOnlyList<Song>> ListAsync(
        string? genre,
        string? artist,
        CancellationToken cancellationToken = default);
}
