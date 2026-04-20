using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicPlaylist.Application.Playlists;
using MusicPlaylist.Application.Songs;
using MusicPlaylist.Domain.Entities;
using MusicPlaylist.Infrastructure.Persistence;
using Npgsql;
using Xunit;

namespace MusicPlaylist.DatabaseTests;

[Collection(nameof(DatabaseTestCollection))]
public sealed class PlaylistDatabaseConstraintTests
{
    private readonly PostgresFixture _fixture;
    private readonly HttpClient _client;

    public PlaylistDatabaseConstraintTests(PostgresFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Factory.CreateClient();
    }

    [Fact]
    public async Task Db_Unique_PlaylistSongs_PlaylistId_SongId_ThrowsUniqueViolation()
    {
        using var scope = _fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var playlist = new Playlist
        {
            Name = $"p-{Guid.NewGuid():N}",
            Description = null,
            UserId = "user-unique-ps",
            CreatedAt = DateTimeOffset.UtcNow,
            IsPublic = false
        };

        var song = new Song
        {
            Title = $"t-{Guid.NewGuid():N}",
            Artist = "a",
            Album = "al",
            DurationSeconds = 120,
            Genre = "Rock",
            ReleaseDate = new DateOnly(2020, 1, 1)
        };

        db.Playlists.Add(playlist);
        db.Songs.Add(song);
        await db.SaveChangesAsync();

        db.PlaylistSongs.Add(
            new PlaylistSong
            {
                PlaylistId = playlist.Id,
                SongId = song.Id,
                Position = 1,
                AddedAt = DateTimeOffset.UtcNow
            });
        await db.SaveChangesAsync();

        db.PlaylistSongs.Add(
            new PlaylistSong
            {
                PlaylistId = playlist.Id,
                SongId = song.Id,
                Position = 2,
                AddedAt = DateTimeOffset.UtcNow
            });

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        var pg = FindPostgresException(ex);
        Assert.NotNull(pg);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, pg!.SqlState);
        Assert.Contains("IX_PlaylistSongs_PlaylistId_SongId", pg.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Db_Unique_Playlists_UserId_Name_ThrowsUniqueViolation()
    {
        using var scope = _fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var userId = "user-unique-playlist";
        var name = $"SameName-{Guid.NewGuid():N}";

        db.Playlists.Add(
            new Playlist
            {
                Name = name,
                Description = null,
                UserId = userId,
                CreatedAt = DateTimeOffset.UtcNow,
                IsPublic = false
            });
        await db.SaveChangesAsync();

        db.Playlists.Add(
            new Playlist
            {
                Name = name,
                Description = null,
                UserId = userId,
                CreatedAt = DateTimeOffset.UtcNow,
                IsPublic = false
            });

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        var pg = FindPostgresException(ex);
        Assert.NotNull(pg);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, pg!.SqlState);
        Assert.Contains("IX_Playlists_UserId_Name", pg.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Db_Unique_PlaylistSongs_PlaylistId_Position_ThrowsUniqueViolation()
    {
        using var scope = _fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var playlist = new Playlist
        {
            Name = $"p-{Guid.NewGuid():N}",
            Description = null,
            UserId = "user-unique-pos",
            CreatedAt = DateTimeOffset.UtcNow,
            IsPublic = false
        };

        var song1 = new Song
        {
            Title = $"t1-{Guid.NewGuid():N}",
            Artist = "a",
            Album = "al",
            DurationSeconds = 120,
            Genre = "Rock",
            ReleaseDate = new DateOnly(2020, 1, 1)
        };
        var song2 = new Song
        {
            Title = $"t2-{Guid.NewGuid():N}",
            Artist = "a",
            Album = "al",
            DurationSeconds = 120,
            Genre = "Rock",
            ReleaseDate = new DateOnly(2020, 1, 1)
        };

        db.Playlists.Add(playlist);
        db.Songs.AddRange(song1, song2);
        await db.SaveChangesAsync();

        db.PlaylistSongs.AddRange(
            new PlaylistSong
            {
                PlaylistId = playlist.Id,
                SongId = song1.Id,
                Position = 1,
                AddedAt = DateTimeOffset.UtcNow
            },
            new PlaylistSong
            {
                PlaylistId = playlist.Id,
                SongId = song2.Id,
                Position = 1,
                AddedAt = DateTimeOffset.UtcNow
            });

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        var pg = FindPostgresException(ex);
        Assert.NotNull(pg);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, pg!.SqlState);
        Assert.Contains("IX_PlaylistSongs_PlaylistId_Position", pg.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Db_CascadeDelete_Playlist_RemovesPlaylistSongs()
    {
        using var scope = _fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var playlist = new Playlist
        {
            Name = $"p-{Guid.NewGuid():N}",
            Description = null,
            UserId = "user-cascade",
            CreatedAt = DateTimeOffset.UtcNow,
            IsPublic = false
        };
        var song = new Song
        {
            Title = $"t-{Guid.NewGuid():N}",
            Artist = "a",
            Album = "al",
            DurationSeconds = 120,
            Genre = "Rock",
            ReleaseDate = new DateOnly(2020, 1, 1)
        };

        db.Playlists.Add(playlist);
        db.Songs.Add(song);
        await db.SaveChangesAsync();

        db.PlaylistSongs.Add(
            new PlaylistSong
            {
                PlaylistId = playlist.Id,
                SongId = song.Id,
                Position = 1,
                AddedAt = DateTimeOffset.UtcNow
            });
        await db.SaveChangesAsync();

        db.Playlists.Remove(playlist);
        await db.SaveChangesAsync();

        var remainingLinks = await db.PlaylistSongs
            .AsNoTracking()
            .Where(ps => ps.PlaylistId == playlist.Id)
            .CountAsync();

        Assert.Equal(0, remainingLinks);
    }

    [Fact]
    public async Task Db_RenumberAfterDeleteViaApi_HasNoPositionGaps()
    {
        var userId = "user-db-renumber";

        var song1 = await CreateSongAsync("db-r1");
        var song2 = await CreateSongAsync("db-r2");
        var song3 = await CreateSongAsync("db-r3");

        var playlist = await CreatePlaylistAsync(userId, "p-db-renumber");
        await AddSongAsync(userId, playlist.Id, song1.Id);
        await AddSongAsync(userId, playlist.Id, song2.Id);
        await AddSongAsync(userId, playlist.Id, song3.Id);

        using var delete = new HttpRequestMessage(HttpMethod.Delete, $"/api/playlists/{playlist.Id}/songs/{song2.Id}");
        delete.Headers.Add("X-User-Id", userId);
        var resp = await _client.SendAsync(delete);
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);

        using var scope = _fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var links = await db.PlaylistSongs
            .AsNoTracking()
            .Where(ps => ps.PlaylistId == playlist.Id)
            .OrderBy(ps => ps.Position)
            .Select(ps => new { ps.SongId, ps.Position })
            .ToListAsync();

        Assert.Equal(2, links.Count);
        Assert.Equal(new[] { 1, 2 }, links.Select(x => x.Position).ToArray());
        Assert.Equal(new[] { song1.Id, song3.Id }, links.Select(x => x.SongId).ToArray());
    }

    private async Task<SongResponse> CreateSongAsync(string title)
    {
        var req = new CreateSongRequest(
            Title: $"{title}-{Guid.NewGuid():N}",
            Artist: "artist",
            Album: "album",
            DurationSeconds: 120,
            Genre: "Rock",
            ReleaseDate: new DateOnly(2020, 1, 1));

        var resp = await _client.PostAsJsonAsync("/api/songs", req);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<SongResponse>())!;
    }

    private async Task<PlaylistResponse> CreatePlaylistAsync(string userId, string name)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/playlists")
        {
            Content = JsonContent.Create(new CreatePlaylistRequest($"{name}-{Guid.NewGuid():N}", null, false))
        };
        req.Headers.Add("X-User-Id", userId);
        var resp = await _client.SendAsync(req);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<PlaylistResponse>())!;
    }

    private async Task AddSongAsync(string userId, long playlistId, long songId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/playlists/{playlistId}/songs")
        {
            Content = JsonContent.Create(new AddSongToPlaylistRequest(songId))
        };
        req.Headers.Add("X-User-Id", userId);
        var resp = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
    }

    private static PostgresException? FindPostgresException(Exception ex)
    {
        if (ex is PostgresException pg)
        {
            return pg;
        }

        return ex.InnerException is null ? null : FindPostgresException(ex.InnerException);
    }
}

