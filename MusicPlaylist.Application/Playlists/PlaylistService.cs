using MusicPlaylist.Application.Common;
using MusicPlaylist.Domain.Entities;

namespace MusicPlaylist.Application.Playlists;

public sealed class PlaylistService : IPlaylistService
{
    private readonly IPlaylistRepository _repository;

    public PlaylistService(IPlaylistRepository repository)
    {
        _repository = repository;
    }

    public async Task<(IReadOnlyList<PlaylistResponse>? List, ServiceError? Error)> ListAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var rows = await _repository.ListAsync(userId, cancellationToken);
        return (rows.Select(ToResponse).ToList(), null);
    }

    public async Task<(PlaylistResponse? Playlist, ServiceError? Error)> CreateAsync(
        string userId,
        CreatePlaylistRequest request,
        CancellationToken cancellationToken = default)
    {
        var nameError = ValidateName(request.Name);
        if (nameError is not null)
        {
            return (null, nameError);
        }

        var name = request.Name.Trim();
        if (await _repository.NameExistsForUserAsync(userId, name, null, cancellationToken))
        {
            return (null, ServiceError.Conflict("A playlist with this name already exists."));
        }

        var entity = new Playlist
        {
            Name = name,
            Description = NormalizeDescription(request.Description),
            UserId = userId,
            CreatedAt = DateTimeOffset.UtcNow,
            IsPublic = request.IsPublic
        };

        var (saved, error) = await _repository.CreateAsync(entity, cancellationToken);
        if (error is not null)
        {
            return (null, error);
        }

        return (ToResponse(saved!), null);
    }

    public async Task<(PlaylistResponse? Playlist, ServiceError? Error)> UpdateAsync(
        string userId,
        long playlistId,
        UpdatePlaylistRequest request,
        CancellationToken cancellationToken = default)
    {
        var nameError = ValidateName(request.Name);
        if (nameError is not null)
        {
            return (null, nameError);
        }

        var playlist = await _repository.GetOwnedTrackedAsync(playlistId, userId, false, cancellationToken);
        if (playlist is null)
        {
            return (null, ServiceError.NotFound("Playlist was not found."));
        }

        var name = request.Name.Trim();
        if (!string.Equals(playlist.Name, name, StringComparison.Ordinal)
            && await _repository.NameExistsForUserAsync(userId, name, playlistId, cancellationToken))
        {
            return (null, ServiceError.Conflict("A playlist with this name already exists."));
        }

        playlist.Name = name;
        playlist.Description = NormalizeDescription(request.Description);
        playlist.IsPublic = request.IsPublic;

        var saveError = await _repository.SavePlaylistUpdateAsync(cancellationToken);
        if (saveError is not null)
        {
            return (null, saveError);
        }

        return (ToResponse(playlist), null);
    }

    public Task<ServiceError?> DeleteAsync(
        string userId,
        long playlistId,
        CancellationToken cancellationToken = default) =>
        _repository.DeleteOwnedAsync(playlistId, userId, cancellationToken);

    public Task<ServiceError?> AddSongAsync(
        string userId,
        long playlistId,
        AddSongToPlaylistRequest request,
        CancellationToken cancellationToken = default)
    {
        return AddSongInternalAsync(userId, playlistId, request, cancellationToken);
    }

    private async Task<ServiceError?> AddSongInternalAsync(
        string userId,
        long playlistId,
        AddSongToPlaylistRequest request,
        CancellationToken cancellationToken)
    {
        var playlist = await _repository.GetOwnedTrackedAsync(playlistId, userId, true, cancellationToken);
        if (playlist is null)
        {
            return ServiceError.NotFound("Playlist was not found.");
        }

        var songId = request.SongId;
        if (!await _repository.SongExistsAsync(songId, cancellationToken))
        {
            return ServiceError.NotFound("Song was not found.");
        }

        if (playlist.PlaylistSongs.Any(ps => ps.SongId == songId))
        {
            return ServiceError.Conflict("This song is already in the playlist.");
        }

        if (playlist.PlaylistSongs.Count >= PlaylistRules.MaxSongsPerPlaylist)
        {
            return ServiceError.BadRequest("Playlist has reached the maximum of 100 songs.");
        }

        return await _repository.AddSongTransactionalAsync(playlistId, userId, songId, cancellationToken);
    }

    public Task<ServiceError?> RemoveSongAsync(
        string userId,
        long playlistId,
        long songId,
        CancellationToken cancellationToken = default) =>
        _repository.RemoveSongAndRenumberAsync(playlistId, userId, songId, cancellationToken);

    public async Task<ServiceError?> ReorderAsync(
        string userId,
        long playlistId,
        ReorderRequest request,
        CancellationToken cancellationToken = default)
    {
        var items = request.Items;
        if (items is null)
        {
            return ServiceError.BadRequest("Items are required.");
        }

        return await _repository.ReorderTransactionalAsync(playlistId, userId, items, cancellationToken);
    }

    private static ServiceError? ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return ServiceError.BadRequest("Name is required.");
        }

        return null;
    }

    private static string? NormalizeDescription(string? description) =>
        string.IsNullOrWhiteSpace(description) ? null : description.Trim();

    private static PlaylistResponse ToResponse(Playlist p) =>
        new(p.Id, p.Name, p.Description, p.CreatedAt, p.IsPublic);
}
