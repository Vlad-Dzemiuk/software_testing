using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicPlaylist.Application.Playlists;
using MusicPlaylist.Application.Songs;
using MusicPlaylist.Domain.Entities;
using MusicPlaylist.Infrastructure.Persistence;
using Xunit;

namespace MusicPlaylist.IntegrationTests.Playlists;

[Collection(nameof(IntegrationTestCollection))]
public class PlaylistSongsAndReorderTests
{
    private readonly PostgresFixture _fixture;
    private readonly HttpClient _client;

    public PlaylistSongsAndReorderTests(PostgresFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Factory.CreateClient();
    }

    [Fact]
    public async Task AddAndRemoveSong_RenumbersPositions_Sequentially()
    {
        var userId = "user-001";

        var song1 = await CreateSongAsync("t1");
        var song2 = await CreateSongAsync("t2");
        var song3 = await CreateSongAsync("t3");

        var playlist = await CreatePlaylistAsync(userId, "p1");

        await AddSongAsync(userId, playlist.Id, song1.Id);
        await AddSongAsync(userId, playlist.Id, song2.Id);
        await AddSongAsync(userId, playlist.Id, song3.Id);

        await RemoveSongAsync(userId, playlist.Id, song2.Id);

        using var scope = _fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var links = await db.PlaylistSongs
            .AsNoTracking()
            .Where(ps => ps.PlaylistId == playlist.Id)
            .OrderBy(ps => ps.Position)
            .ToListAsync();

        Assert.Equal(2, links.Count);
        Assert.Equal(1, links[0].Position);
        Assert.Equal(song1.Id, links[0].SongId);
        Assert.Equal(2, links[1].Position);
        Assert.Equal(song3.Id, links[1].SongId);
    }

    [Fact]
    public async Task AddSong_Duplicate_ReturnsConflict()
    {
        var userId = "user-001";

        var song = await CreateSongAsync($"dup-{Guid.NewGuid():N}");
        var playlist = await CreatePlaylistAsync(userId, "p-dup");

        await AddSongAsync(userId, playlist.Id, song.Id);

        using var dupReq = new HttpRequestMessage(HttpMethod.Post, $"/api/playlists/{playlist.Id}/songs")
        {
            Content = JsonContent.Create(new AddSongToPlaylistRequest(song.Id))
        };
        dupReq.Headers.Add("X-User-Id", userId);
        var dupResp = await _client.SendAsync(dupReq);

        Assert.Equal(HttpStatusCode.Conflict, dupResp.StatusCode);
    }

    [Fact]
    public async Task AddSong_WhenExceeding100_ReturnsBadRequest()
    {
        var userId = "user-001";
        var playlist = await CreatePlaylistAsync(userId, "p-max100");

        // Add 100 distinct songs.
        for (var i = 0; i < 100; i++)
        {
            var song = await CreateSongAsync($"m{i:D3}-{Guid.NewGuid():N}");
            await AddSongAsync(userId, playlist.Id, song.Id);
        }

        var extraSong = await CreateSongAsync($"m101-{Guid.NewGuid():N}");
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/playlists/{playlist.Id}/songs")
        {
            Content = JsonContent.Create(new AddSongToPlaylistRequest(extraSong.Id))
        };
        req.Headers.Add("X-User-Id", userId);
        var resp = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Reorder_Valid_UpdatesPositions_InDatabase()
    {
        var userId = "user-001";

        var song1 = await CreateSongAsync("r1");
        var song2 = await CreateSongAsync("r2");
        var song3 = await CreateSongAsync("r3");

        var playlist = await CreatePlaylistAsync(userId, "p-reorder");
        await AddSongAsync(userId, playlist.Id, song1.Id);
        await AddSongAsync(userId, playlist.Id, song2.Id);
        await AddSongAsync(userId, playlist.Id, song3.Id);

        var reorder = new ReorderRequest(
            new[]
            {
                new ReorderItem(song1.Id, 3),
                new ReorderItem(song2.Id, 1),
                new ReorderItem(song3.Id, 2)
            });

        using var req = new HttpRequestMessage(HttpMethod.Put, $"/api/playlists/{playlist.Id}/reorder")
        {
            Content = JsonContent.Create(reorder)
        };
        req.Headers.Add("X-User-Id", userId);
        var resp = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);

        using var scope = _fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var links = await db.PlaylistSongs
            .AsNoTracking()
            .Where(ps => ps.PlaylistId == playlist.Id)
            .OrderBy(ps => ps.Position)
            .Select(ps => new { ps.SongId, ps.Position })
            .ToListAsync();

        Assert.Equal(new[] { song2.Id, song3.Id, song1.Id }, links.Select(x => x.SongId).ToArray());
        Assert.Equal(new[] { 1, 2, 3 }, links.Select(x => x.Position).ToArray());
    }

    [Fact]
    public async Task Reorder_Invalid_DuplicatePosition_ReturnsBadRequest()
    {
        var userId = "user-001";

        var song1 = await CreateSongAsync("d1");
        var song2 = await CreateSongAsync("d2");

        var playlist = await CreatePlaylistAsync(userId, "p-invalid");
        await AddSongAsync(userId, playlist.Id, song1.Id);
        await AddSongAsync(userId, playlist.Id, song2.Id);

        var reorder = new ReorderRequest(
            new[]
            {
                new ReorderItem(song1.Id, 1),
                new ReorderItem(song2.Id, 1)
            });

        using var req = new HttpRequestMessage(HttpMethod.Put, $"/api/playlists/{playlist.Id}/reorder")
        {
            Content = JsonContent.Create(reorder)
        };
        req.Headers.Add("X-User-Id", userId);
        var resp = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Reorder_Invalid_UnknownSongId_ReturnsBadRequest()
    {
        var userId = "user-001";

        var song1 = await CreateSongAsync($"u1-{Guid.NewGuid():N}");
        var song2 = await CreateSongAsync($"u2-{Guid.NewGuid():N}");

        var playlist = await CreatePlaylistAsync(userId, "p-unknown");
        await AddSongAsync(userId, playlist.Id, song1.Id);
        await AddSongAsync(userId, playlist.Id, song2.Id);

        var reorder = new ReorderRequest(
            new[]
            {
                new ReorderItem(song1.Id, 1),
                new ReorderItem(9_999_999, 2)
            });

        using var req = new HttpRequestMessage(HttpMethod.Put, $"/api/playlists/{playlist.Id}/reorder")
        {
            Content = JsonContent.Create(reorder)
        };
        req.Headers.Add("X-User-Id", userId);
        var resp = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Reorder_Invalid_WrongN_ReturnsBadRequest()
    {
        var userId = "user-001";

        var song1 = await CreateSongAsync($"n1-{Guid.NewGuid():N}");
        var song2 = await CreateSongAsync($"n2-{Guid.NewGuid():N}");
        var song3 = await CreateSongAsync($"n3-{Guid.NewGuid():N}");

        var playlist = await CreatePlaylistAsync(userId, "p-wrongn");
        await AddSongAsync(userId, playlist.Id, song1.Id);
        await AddSongAsync(userId, playlist.Id, song2.Id);
        await AddSongAsync(userId, playlist.Id, song3.Id);

        var reorder = new ReorderRequest(
            new[]
            {
                new ReorderItem(song1.Id, 1),
                new ReorderItem(song2.Id, 2)
            });

        using var req = new HttpRequestMessage(HttpMethod.Put, $"/api/playlists/{playlist.Id}/reorder")
        {
            Content = JsonContent.Create(reorder)
        };
        req.Headers.Add("X-User-Id", userId);
        var resp = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    private async Task<SongResponse> CreateSongAsync(string title)
    {
        var req = new CreateSongRequest(
            Title: title,
            Artist: "artist",
            Album: "album",
            DurationSeconds: 120,
            Genre: "Rock",
            ReleaseDate: new DateOnly(2020, 1, 1));

        var resp = await _client.PostAsJsonAsync("/api/songs", req);
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<SongResponse>();
        return body!;
    }

    private async Task<PlaylistResponse> CreatePlaylistAsync(string userId, string name)
    {
        name = $"{name}-{Guid.NewGuid():N}";
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/playlists")
        {
            Content = JsonContent.Create(new CreatePlaylistRequest(name, null, false))
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

    private async Task RemoveSongAsync(string userId, long playlistId, long songId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Delete, $"/api/playlists/{playlistId}/songs/{songId}");
        req.Headers.Add("X-User-Id", userId);
        var resp = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
    }
}

