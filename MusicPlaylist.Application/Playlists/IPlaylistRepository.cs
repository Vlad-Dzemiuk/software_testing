using MusicPlaylist.Application.Common;
using MusicPlaylist.Domain.Entities;

namespace MusicPlaylist.Application.Playlists;

public interface IPlaylistRepository
{
    Task<IReadOnlyList<Playlist>> ListAsync(string userId, CancellationToken cancellationToken = default);

    Task<(Playlist? Playlist, ServiceError? Error)> CreateAsync(
        Playlist playlist,
        CancellationToken cancellationToken = default);

    Task<Playlist?> GetOwnedTrackedAsync(
        long playlistId,
        string userId,
        bool includeSongs,
        CancellationToken cancellationToken = default);

    Task<bool> NameExistsForUserAsync(
        string userId,
        string name,
        long? excludePlaylistId,
        CancellationToken cancellationToken = default);

    Task<ServiceError?> SavePlaylistUpdateAsync(CancellationToken cancellationToken = default);

    Task<ServiceError?> DeleteOwnedAsync(
        long playlistId,
        string userId,
        CancellationToken cancellationToken = default);

    Task<bool> SongExistsAsync(long songId, CancellationToken cancellationToken = default);

    Task<ServiceError?> AddSongTransactionalAsync(
        long playlistId,
        string userId,
        long songId,
        CancellationToken cancellationToken = default);

    Task<ServiceError?> RemoveSongAndRenumberAsync(
        long playlistId,
        string userId,
        long songId,
        CancellationToken cancellationToken = default);

    Task<ServiceError?> ReorderTransactionalAsync(
        long playlistId,
        string userId,
        IReadOnlyList<ReorderItem> items,
        CancellationToken cancellationToken = default);
}
