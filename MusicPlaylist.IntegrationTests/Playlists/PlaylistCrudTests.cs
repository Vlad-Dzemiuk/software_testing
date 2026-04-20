using System.Net;
using System.Net.Http.Json;
using MusicPlaylist.Application.Playlists;
using Xunit;

namespace MusicPlaylist.IntegrationTests.Playlists;

[Collection(nameof(IntegrationTestCollection))]
public class PlaylistCrudTests
{
    private readonly HttpClient _client;

    public PlaylistCrudTests(PostgresFixture fixture)
    {
        _client = fixture.Factory.CreateClient();
    }

    [Fact]
    public async Task ListPlaylists_WithoutUserHeader_ReturnsBadRequest()
    {
        var resp = await _client.GetAsync("/api/playlists");
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Playlists_AreIsolated_PerUserId()
    {
        var user1 = "user-001";
        var user2 = "user-002";
        var name1 = $"User1 Playlist {Guid.NewGuid():N}";

        using var create = new HttpRequestMessage(HttpMethod.Post, "/api/playlists")
        {
            Content = JsonContent.Create(new CreatePlaylistRequest(name1, null, false))
        };
        create.Headers.Add("X-User-Id", user1);
        var createResp = await _client.SendAsync(create);
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);

        var created = await createResp.Content.ReadFromJsonAsync<PlaylistResponse>();
        Assert.NotNull(created);

        using var listUser2 = new HttpRequestMessage(HttpMethod.Get, "/api/playlists");
        listUser2.Headers.Add("X-User-Id", user2);
        var listResp = await _client.SendAsync(listUser2);
        Assert.Equal(HttpStatusCode.OK, listResp.StatusCode);

        var playlists = await listResp.Content.ReadFromJsonAsync<List<PlaylistResponse>>();
        Assert.NotNull(playlists);
        Assert.DoesNotContain(playlists!, p => p.Id == created!.Id);
    }

    [Fact]
    public async Task Crud_Playlist_WithUserHeader_Works()
    {
        var userId = "user-001";
        var name = $"My Playlist {Guid.NewGuid():N}";

        var createReq = new CreatePlaylistRequest(name, "desc", true);
        using var create = new HttpRequestMessage(HttpMethod.Post, "/api/playlists")
        {
            Content = JsonContent.Create(createReq)
        };
        create.Headers.Add("X-User-Id", userId);

        var createResp = await _client.SendAsync(create);
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);

        var created = await createResp.Content.ReadFromJsonAsync<PlaylistResponse>();
        Assert.NotNull(created);
        Assert.Equal(name, created!.Name);

        using var list = new HttpRequestMessage(HttpMethod.Get, "/api/playlists");
        list.Headers.Add("X-User-Id", userId);
        var listResp = await _client.SendAsync(list);
        Assert.Equal(HttpStatusCode.OK, listResp.StatusCode);

        var playlists = await listResp.Content.ReadFromJsonAsync<List<PlaylistResponse>>();
        Assert.NotNull(playlists);
        Assert.Contains(playlists!, p => p.Id == created.Id);

        var updateReq = new UpdatePlaylistRequest("My Playlist v2", null, false);
        using var update = new HttpRequestMessage(HttpMethod.Put, $"/api/playlists/{created.Id}")
        {
            Content = JsonContent.Create(updateReq)
        };
        update.Headers.Add("X-User-Id", userId);
        var updateResp = await _client.SendAsync(update);
        Assert.Equal(HttpStatusCode.OK, updateResp.StatusCode);

        var updated = await updateResp.Content.ReadFromJsonAsync<PlaylistResponse>();
        Assert.NotNull(updated);
        Assert.Equal("My Playlist v2", updated!.Name);
        Assert.False(updated.IsPublic);

        using var delete = new HttpRequestMessage(HttpMethod.Delete, $"/api/playlists/{created.Id}");
        delete.Headers.Add("X-User-Id", userId);
        var deleteResp = await _client.SendAsync(delete);
        Assert.Equal(HttpStatusCode.NoContent, deleteResp.StatusCode);

        using var list2 = new HttpRequestMessage(HttpMethod.Get, "/api/playlists");
        list2.Headers.Add("X-User-Id", userId);
        var list2Resp = await _client.SendAsync(list2);
        var playlists2 = await list2Resp.Content.ReadFromJsonAsync<List<PlaylistResponse>>();
        Assert.DoesNotContain(playlists2!, p => p.Id == created.Id);
    }
}

