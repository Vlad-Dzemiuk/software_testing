using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MusicPlaylist.Application.Common;
using MusicPlaylist.Application.Playlists;
using MusicPlaylist.Domain.Entities;
using Npgsql;

namespace MusicPlaylist.Infrastructure.Persistence;

public sealed class PlaylistRepository : IPlaylistRepository
{
    private const string PlaylistsUserNameIndex = "IX_Playlists_UserId_Name";
    private const string PlaylistSongsPlaylistSongIndex = "IX_PlaylistSongs_PlaylistId_SongId";
    private const string PlaylistSongsPlaylistPositionIndex = "IX_PlaylistSongs_PlaylistId_Position";

    private readonly AppDbContext _db;

    public PlaylistRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Playlist>> ListAsync(string userId, CancellationToken cancellationToken = default) =>
        await _db.Playlists
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .OrderBy(p => p.Id)
            .ToListAsync(cancellationToken);

    public async Task<(Playlist? Playlist, ServiceError? Error)> CreateAsync(
        Playlist playlist,
        CancellationToken cancellationToken = default)
    {
        _db.Playlists.Add(playlist);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            return (playlist, null);
        }
        catch (DbUpdateException ex) when (IsUniqueIndex(ex, PlaylistsUserNameIndex))
        {
            _db.Entry(playlist).State = EntityState.Detached;
            return (null, ServiceError.Conflict("A playlist with this name already exists."));
        }
    }

    public Task<Playlist?> GetOwnedTrackedAsync(
        long playlistId,
        string userId,
        bool includeSongs,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Playlist> query = _db.Playlists.Where(p => p.Id == playlistId && p.UserId == userId);
        if (includeSongs)
        {
            query = query.Include(p => p.PlaylistSongs);
        }

        return query.FirstOrDefaultAsync(cancellationToken);
    }

    public Task<bool> NameExistsForUserAsync(
        string userId,
        string name,
        long? excludePlaylistId,
        CancellationToken cancellationToken = default)
    {
        var q = _db.Playlists.AsNoTracking().Where(p => p.UserId == userId && p.Name == name);
        if (excludePlaylistId is not null)
        {
            q = q.Where(p => p.Id != excludePlaylistId.Value);
        }

        return q.AnyAsync(cancellationToken);
    }

    public async Task<ServiceError?> SavePlaylistUpdateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            return null;
        }
        catch (DbUpdateException ex) when (IsUniqueIndex(ex, PlaylistsUserNameIndex))
        {
            return ServiceError.Conflict("A playlist with this name already exists.");
        }
    }

    public async Task<ServiceError?> DeleteOwnedAsync(
        long playlistId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var playlist = await _db.Playlists
            .FirstOrDefaultAsync(p => p.Id == playlistId && p.UserId == userId, cancellationToken);
        if (playlist is null)
        {
            return ServiceError.NotFound("Playlist was not found.");
        }

        _db.Playlists.Remove(playlist);
        await _db.SaveChangesAsync(cancellationToken);
        return null;
    }

    public Task<bool> SongExistsAsync(long songId, CancellationToken cancellationToken = default) =>
        _db.Songs.AsNoTracking().AnyAsync(s => s.Id == songId, cancellationToken);

    public Task<ServiceError?> AddSongTransactionalAsync(
        long playlistId,
        string userId,
        long songId,
        CancellationToken cancellationToken = default) =>
        _db.Database.CreateExecutionStrategy().ExecuteAsync<ServiceError?>(async ct =>
        {
            await using var tx =
                await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            try
            {
                var playlist = await _db.Playlists
                    .Include(p => p.PlaylistSongs)
                    .FirstOrDefaultAsync(p => p.Id == playlistId && p.UserId == userId, ct);
                if (playlist is null)
                {
                    await SafeRollbackAsync(tx, ct);
                    return ServiceError.NotFound("Playlist was not found.");
                }

                if (!await _db.Songs.AnyAsync(s => s.Id == songId, ct))
                {
                    await SafeRollbackAsync(tx, ct);
                    return ServiceError.NotFound("Song was not found.");
                }

                if (playlist.PlaylistSongs.Any(ps => ps.SongId == songId))
                {
                    await SafeRollbackAsync(tx, ct);
                    return ServiceError.Conflict("This song is already in the playlist.");
                }

                var count = await _db.PlaylistSongs.CountAsync(ps => ps.PlaylistId == playlistId, ct);
                if (count >= PlaylistRules.MaxSongsPerPlaylist)
                {
                    await SafeRollbackAsync(tx, ct);
                    return ServiceError.BadRequest("Playlist has reached the maximum of 100 songs.");
                }

                var maxPos = await _db.PlaylistSongs
                    .Where(ps => ps.PlaylistId == playlistId)
                    .Select(ps => (int?)ps.Position)
                    .MaxAsync(ct) ?? 0;

                _db.PlaylistSongs.Add(
                    new PlaylistSong
                    {
                        PlaylistId = playlistId,
                        SongId = songId,
                        Position = maxPos + 1,
                        AddedAt = DateTimeOffset.UtcNow
                    });

                try
                {
                    await _db.SaveChangesAsync(ct);
                }
                catch (DbUpdateException ex) when (IsUniqueIndex(ex, PlaylistSongsPlaylistSongIndex))
                {
                    await SafeRollbackAsync(tx, ct);
                    return ServiceError.Conflict("This song is already in the playlist.");
                }
                catch (DbUpdateException ex) when (IsUniqueIndex(ex, PlaylistSongsPlaylistPositionIndex))
                {
                    await SafeRollbackAsync(tx, ct);
                    return ServiceError.BadRequest("Playlist has reached the maximum of 100 songs.");
                }

                await tx.CommitAsync(ct);
                return null;
            }
            catch
            {
                await SafeRollbackAsync(tx, ct);
                throw;
            }
        }, cancellationToken);

    public async Task<ServiceError?> RemoveSongAndRenumberAsync(
        long playlistId,
        string userId,
        long songId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _db.Database.CreateExecutionStrategy().ExecuteAsync<ServiceError?>(async ct =>
            {
                await using var tx = await _db.Database.BeginTransactionAsync(ct);
                try
                {
                    var playlist = await _db.Playlists
                        .Include(p => p.PlaylistSongs)
                        .FirstOrDefaultAsync(p => p.Id == playlistId && p.UserId == userId, ct);
                    if (playlist is null)
                    {
                        await SafeRollbackAsync(tx, ct);
                        return ServiceError.NotFound("Playlist was not found.");
                    }

                    var link = playlist.PlaylistSongs.FirstOrDefault(ps => ps.SongId == songId);
                    if (link is null)
                    {
                        await SafeRollbackAsync(tx, ct);
                        return ServiceError.NotFound("Song was not found in this playlist.");
                    }

                    _db.PlaylistSongs.Remove(link);
                    await _db.SaveChangesAsync(ct);

                    var remaining = await _db.PlaylistSongs
                        .Where(ps => ps.PlaylistId == playlistId)
                        .OrderBy(ps => ps.Position)
                        .ToListAsync(ct);
                    for (var i = 0; i < remaining.Count; i++)
                    {
                        remaining[i].Position = i + 1;
                    }

                    await _db.SaveChangesAsync(ct);
                    await tx.CommitAsync(ct);
                    return null;
                }
                catch (DbUpdateException ex) when (TryPostgresException(ex, out var pg)
                                                  && pg is not null
                                                  && (pg.SqlState == PostgresErrorCodes.SerializationFailure
                                                      || pg.SqlState == PostgresErrorCodes.DeadlockDetected))
                {
                    await SafeRollbackAsync(tx, ct);
                    return ServiceError.Conflict("Concurrent reorder detected. Please retry.");
                }
                catch (PostgresException pg) when (pg.SqlState == PostgresErrorCodes.SerializationFailure
                                                   || pg.SqlState == PostgresErrorCodes.DeadlockDetected)
                {
                    await SafeRollbackAsync(tx, ct);
                    return ServiceError.Conflict("Concurrent reorder detected. Please retry.");
                }
                catch (InvalidOperationException ex) when (TryFindPostgresSqlState(ex, out var sqlState)
                                                          && (sqlState == PostgresErrorCodes.SerializationFailure
                                                              || sqlState == PostgresErrorCodes.DeadlockDetected))
                {
                    await SafeRollbackAsync(tx, ct);
                    return ServiceError.Conflict("Concurrent reorder detected. Please retry.");
                }
                catch
                {
                    await SafeRollbackAsync(tx, ct);
                    throw;
                }
            }, cancellationToken);
        }
        catch (RetryLimitExceededException ex) when (TryFindPostgresSqlState(ex, out var sqlState)
                                                    && (sqlState == PostgresErrorCodes.SerializationFailure
                                                        || sqlState == PostgresErrorCodes.DeadlockDetected))
        {
            return ServiceError.Conflict("Concurrent reorder detected. Please retry.");
        }
        catch (Exception ex) when (TryFindPostgresSqlState(ex, out var sqlState)
                                  && (sqlState == PostgresErrorCodes.SerializationFailure
                                      || sqlState == PostgresErrorCodes.DeadlockDetected))
        {
            return ServiceError.Conflict("Concurrent reorder detected. Please retry.");
        }
    }

    public async Task<ServiceError?> ReorderTransactionalAsync(
        long playlistId,
        string userId,
        IReadOnlyList<ReorderItem> items,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _db.Database.CreateExecutionStrategy().ExecuteAsync<ServiceError?>(async ct =>
            {
                await using var tx =
                    await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                try
                {
                    var playlist = await _db.Playlists
                        .Include(p => p.PlaylistSongs)
                        .FirstOrDefaultAsync(p => p.Id == playlistId && p.UserId == userId, ct);
                    if (playlist is null)
                    {
                        await SafeRollbackAsync(tx, ct);
                        return ServiceError.NotFound("Playlist was not found.");
                    }

                    var links = playlist.PlaylistSongs.OrderBy(x => x.Position).ToList();
                    var n = links.Count;
                    var dbIds = links.Select(l => l.SongId).ToHashSet();
                    var validationError = PlaylistReorderValidator.Validate(items, dbIds);
                    if (validationError is not null)
                    {
                        await SafeRollbackAsync(tx, ct);
                        return validationError;
                    }

                    for (var i = 0; i < n; i++)
                    {
                        links[i].Position = 1_000_000 + i;
                    }

                    await _db.SaveChangesAsync(ct);

                    foreach (var item in items)
                    {
                        var link = links.First(l => l.SongId == item.SongId);
                        link.Position = item.Position;
                    }

                    await _db.SaveChangesAsync(ct);
                    await tx.CommitAsync(ct);
                    return null;
                }
                catch (DbUpdateException ex) when (TryPostgresException(ex, out var pg)
                                                  && pg is not null
                                                  && (pg.SqlState == PostgresErrorCodes.SerializationFailure
                                                      || pg.SqlState == PostgresErrorCodes.DeadlockDetected))
                {
                    await SafeRollbackAsync(tx, ct);
                    return ServiceError.Conflict("Concurrent reorder detected. Please retry.");
                }
                catch (PostgresException pg) when (pg.SqlState == PostgresErrorCodes.SerializationFailure
                                                   || pg.SqlState == PostgresErrorCodes.DeadlockDetected)
                {
                    await SafeRollbackAsync(tx, ct);
                    return ServiceError.Conflict("Concurrent reorder detected. Please retry.");
                }
                catch (InvalidOperationException ex) when (TryFindPostgresSqlState(ex, out var sqlState)
                                                          && (sqlState == PostgresErrorCodes.SerializationFailure
                                                              || sqlState == PostgresErrorCodes.DeadlockDetected))
                {
                    await SafeRollbackAsync(tx, ct);
                    return ServiceError.Conflict("Concurrent reorder detected. Please retry.");
                }
                catch
                {
                    await SafeRollbackAsync(tx, ct);
                    throw;
                }
            }, cancellationToken);
        }
        catch (RetryLimitExceededException ex) when (TryFindPostgresSqlState(ex, out var sqlState)
                                                    && (sqlState == PostgresErrorCodes.SerializationFailure
                                                        || sqlState == PostgresErrorCodes.DeadlockDetected))
        {
            return ServiceError.Conflict("Concurrent reorder detected. Please retry.");
        }
        catch (Exception ex) when (TryFindPostgresSqlState(ex, out var sqlState)
                                  && (sqlState == PostgresErrorCodes.SerializationFailure
                                      || sqlState == PostgresErrorCodes.DeadlockDetected))
        {
            return ServiceError.Conflict("Concurrent reorder detected. Please retry.");
        }
    }

    private static bool IsUniqueIndex(DbUpdateException ex, string indexName)
    {
        if (!TryPostgresException(ex, out var pg) || pg is null || pg.SqlState != PostgresErrorCodes.UniqueViolation)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(pg.ConstraintName)
            && string.Equals(pg.ConstraintName, indexName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return pg.Message.Contains(indexName, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryPostgresException(DbUpdateException ex, out PostgresException? postgresException)
    {
        if (ex.InnerException is PostgresException p1)
        {
            postgresException = p1;
            return true;
        }

        if (ex.InnerException?.InnerException is PostgresException p2)
        {
            postgresException = p2;
            return true;
        }

        postgresException = null;
        return false;
    }

    private static bool TryFindPostgresSqlState(Exception ex, out string? sqlState)
    {
        if (ex is PostgresException pg)
        {
            sqlState = pg.SqlState;
            return true;
        }

        if (ex is DbUpdateException db && TryPostgresException(db, out var pg2) && pg2 is not null)
        {
            sqlState = pg2.SqlState;
            return true;
        }

        if (ex.InnerException is null)
        {
            sqlState = null;
            return false;
        }

        return TryFindPostgresSqlState(ex.InnerException, out sqlState);
    }

    private static async Task SafeRollbackAsync(IDbContextTransaction tx, CancellationToken cancellationToken)
    {
        try
        {
            await tx.RollbackAsync(cancellationToken);
        }
        catch (ObjectDisposedException)
        {
            // Transaction already disposed; nothing to rollback.
        }
        catch (InvalidOperationException)
        {
            // Transaction already completed/aborted; nothing to rollback.
        }
    }
}
