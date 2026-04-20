using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MusicPlaylist.Application.Playlists;
using MusicPlaylist.Domain.Entities;
using MusicPlaylist.Infrastructure.Persistence;
using Xunit;

namespace MusicPlaylist.Application.Tests.Infrastructure;

public class PlaylistRepositoryReorderTests
{
    private sealed record SongPosition(long SongId, int Position);

    [Fact]
    public async Task ReorderTransactionalAsync_UpdatesPositions_AsRequested()
    {
        await using var conn = new SqliteConnection("DataSource=:memory:");
        await conn.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(conn)
            .Options;

        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var userId = "user-001";

        var s1 = new Song { Title = "t1", Artist = "a", Album = "al", DurationSeconds = 10, Genre = "Rock", ReleaseDate = new DateOnly(2020, 1, 1) };
        var s2 = new Song { Title = "t2", Artist = "a", Album = "al", DurationSeconds = 10, Genre = "Rock", ReleaseDate = new DateOnly(2020, 1, 1) };
        var s3 = new Song { Title = "t3", Artist = "a", Album = "al", DurationSeconds = 10, Genre = "Rock", ReleaseDate = new DateOnly(2020, 1, 1) };
        db.Songs.AddRange(s1, s2, s3);

        var playlist = new Playlist
        {
            Name = "p",
            UserId = userId,
            CreatedAt = DateTimeOffset.UtcNow,
            IsPublic = false
        };
        db.Playlists.Add(playlist);
        await db.SaveChangesAsync();

        db.PlaylistSongs.AddRange(
            new PlaylistSong { PlaylistId = playlist.Id, SongId = s1.Id, Position = 1, AddedAt = DateTimeOffset.UtcNow },
            new PlaylistSong { PlaylistId = playlist.Id, SongId = s2.Id, Position = 2, AddedAt = DateTimeOffset.UtcNow },
            new PlaylistSong { PlaylistId = playlist.Id, SongId = s3.Id, Position = 3, AddedAt = DateTimeOffset.UtcNow }
        );
        await db.SaveChangesAsync();

        var repo = new PlaylistRepository(db);
        var items = new[]
        {
            new ReorderItem(s1.Id, 3),
            new ReorderItem(s2.Id, 1),
            new ReorderItem(s3.Id, 2)
        };

        var error = await repo.ReorderTransactionalAsync(playlist.Id, userId, items);
        Assert.Null(error);

        var actual = await db.PlaylistSongs
            .AsNoTracking()
            .Where(ps => ps.PlaylistId == playlist.Id)
            .OrderBy(ps => ps.Position)
            .Select(ps => new SongPosition(ps.SongId, ps.Position))
            .ToListAsync();

        Assert.Equal(
            new[] { new SongPosition(s2.Id, 1), new SongPosition(s3.Id, 2), new SongPosition(s1.Id, 3) },
            actual);
    }
}

