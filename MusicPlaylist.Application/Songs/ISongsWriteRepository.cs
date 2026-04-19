using MusicPlaylist.Domain.Entities;

namespace MusicPlaylist.Application.Songs;

public interface ISongsWriteRepository
{
    Task<Song> AddAsync(Song song, CancellationToken cancellationToken = default);
}
