using MusicPlaylist.Application.Common;

namespace MusicPlaylist.Application.Playlists;

public interface IPlaylistService
{
    Task<(IReadOnlyList<PlaylistResponse>? List, ServiceError? Error)> ListAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<(PlaylistResponse? Playlist, ServiceError? Error)> CreateAsync(
        string userId,
        CreatePlaylistRequest request,
        CancellationToken cancellationToken = default);

    Task<(PlaylistResponse? Playlist, ServiceError? Error)> UpdateAsync(
        string userId,
        long playlistId,
        UpdatePlaylistRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceError?> DeleteAsync(
        string userId,
        long playlistId,
        CancellationToken cancellationToken = default);

    Task<ServiceError?> AddSongAsync(
        string userId,
        long playlistId,
        AddSongToPlaylistRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceError?> RemoveSongAsync(
        string userId,
        long playlistId,
        long songId,
        CancellationToken cancellationToken = default);

    Task<ServiceError?> ReorderAsync(
        string userId,
        long playlistId,
        ReorderRequest request,
        CancellationToken cancellationToken = default);
}
