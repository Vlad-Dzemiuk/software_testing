using AutoFixture;
using Moq;
using MusicPlaylist.Application.Playlists;
using MusicPlaylist.Domain.Entities;
using Xunit;

namespace MusicPlaylist.Application.Tests.Playlists;

public class PlaylistServiceRulesTests
{
    private readonly Fixture _fixture = new();

    [Fact]
    public async Task AddSongAsync_WhenPlaylistAlreadyHasSong_ReturnsConflict_AndDoesNotCallRepositoryAdd()
    {
        var userId = _fixture.Create<string>();
        var playlistId = _fixture.Create<long>();
        var songId = _fixture.Create<long>();

        var playlist = new Playlist
        {
            Id = playlistId,
            UserId = userId,
            Name = _fixture.Create<string>(),
            CreatedAt = DateTimeOffset.UtcNow,
            IsPublic = false,
            PlaylistSongs = new List<PlaylistSong> { new() { PlaylistId = playlistId, SongId = songId, Position = 1 } }
        };

        var repo = new Mock<IPlaylistRepository>(MockBehavior.Strict);
        repo.Setup(r => r.GetOwnedTrackedAsync(playlistId, userId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(playlist);
        repo.Setup(r => r.SongExistsAsync(songId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = new PlaylistService(repo.Object);

        var error = await service.AddSongAsync(userId, playlistId, new AddSongToPlaylistRequest(songId));

        Assert.NotNull(error);
        Assert.Contains("already in the playlist", error!.Message);
        repo.Verify(
            r => r.AddSongTransactionalAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AddSongAsync_WhenPlaylistHas100Songs_ReturnsBadRequest_AndDoesNotCallRepositoryAdd()
    {
        var userId = _fixture.Create<string>();
        var playlistId = _fixture.Create<long>();
        var songId = 101;

        var playlistSongs = Enumerable.Range(1, PlaylistRules.MaxSongsPerPlaylist)
            .Select(i => new PlaylistSong
            {
                PlaylistId = playlistId,
                SongId = i,
                Position = i
            })
            .ToList();

        var playlist = new Playlist
        {
            Id = playlistId,
            UserId = userId,
            Name = _fixture.Create<string>(),
            CreatedAt = DateTimeOffset.UtcNow,
            IsPublic = false,
            PlaylistSongs = playlistSongs
        };

        var repo = new Mock<IPlaylistRepository>(MockBehavior.Strict);
        repo.Setup(r => r.GetOwnedTrackedAsync(playlistId, userId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(playlist);
        repo.Setup(r => r.SongExistsAsync(songId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = new PlaylistService(repo.Object);

        var error = await service.AddSongAsync(userId, playlistId, new AddSongToPlaylistRequest(songId));

        Assert.NotNull(error);
        Assert.Contains("maximum of 100 songs", error!.Message);
        repo.Verify(
            r => r.AddSongTransactionalAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()),
            Times.Never);

        repo.Verify(r => r.SongExistsAsync(songId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddSongAsync_WhenPlaylistNotFound_ReturnsNotFound_AndDoesNotCallRepositorySongChecksOrAdd()
    {
        var userId = _fixture.Create<string>();
        var playlistId = _fixture.Create<long>();
        var songId = _fixture.Create<long>();

        var repo = new Mock<IPlaylistRepository>(MockBehavior.Strict);
        repo.Setup(r => r.GetOwnedTrackedAsync(playlistId, userId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Playlist?)null);

        var service = new PlaylistService(repo.Object);

        var error = await service.AddSongAsync(userId, playlistId, new AddSongToPlaylistRequest(songId));

        Assert.NotNull(error);
        Assert.Contains("Playlist was not found", error!.Message);
        repo.Verify(r => r.SongExistsAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
        repo.Verify(
            r => r.AddSongTransactionalAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AddSongAsync_WhenSongDoesNotExist_ReturnsNotFound_AndDoesNotCallRepositoryAdd()
    {
        var userId = _fixture.Create<string>();
        var playlistId = _fixture.Create<long>();
        var songId = _fixture.Create<long>();

        var playlist = new Playlist
        {
            Id = playlistId,
            UserId = userId,
            Name = _fixture.Create<string>(),
            CreatedAt = DateTimeOffset.UtcNow,
            IsPublic = false,
            PlaylistSongs = new List<PlaylistSong>()
        };

        var repo = new Mock<IPlaylistRepository>(MockBehavior.Strict);
        repo.Setup(r => r.GetOwnedTrackedAsync(playlistId, userId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(playlist);
        repo.Setup(r => r.SongExistsAsync(songId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var service = new PlaylistService(repo.Object);

        var error = await service.AddSongAsync(userId, playlistId, new AddSongToPlaylistRequest(songId));

        Assert.NotNull(error);
        Assert.Contains("Song was not found", error!.Message);
        repo.Verify(
            r => r.AddSongTransactionalAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AddSongAsync_WhenRulesPass_CallsRepositoryAdd_WithCorrectArgs()
    {
        var userId = _fixture.Create<string>();
        var playlistId = _fixture.Create<long>();
        var songId = _fixture.Create<long>();

        var playlist = new Playlist
        {
            Id = playlistId,
            UserId = userId,
            Name = _fixture.Create<string>(),
            CreatedAt = DateTimeOffset.UtcNow,
            IsPublic = false,
            PlaylistSongs = new List<PlaylistSong>()
        };

        var repo = new Mock<IPlaylistRepository>(MockBehavior.Strict);
        repo.Setup(r => r.GetOwnedTrackedAsync(playlistId, userId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(playlist);
        repo.Setup(r => r.SongExistsAsync(songId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        repo.Setup(r => r.AddSongTransactionalAsync(playlistId, userId, songId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MusicPlaylist.Application.Common.ServiceError?)null);

        var service = new PlaylistService(repo.Object);

        var error = await service.AddSongAsync(userId, playlistId, new AddSongToPlaylistRequest(songId));

        Assert.Null(error);
        repo.Verify(r => r.AddSongTransactionalAsync(playlistId, userId, songId, It.IsAny<CancellationToken>()), Times.Once);
    }
}

