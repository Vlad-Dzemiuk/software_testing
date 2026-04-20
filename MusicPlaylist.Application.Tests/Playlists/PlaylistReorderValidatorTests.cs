using MusicPlaylist.Application.Playlists;
using Xunit;

namespace MusicPlaylist.Application.Tests.Playlists;

public class PlaylistReorderValidatorTests
{
    [Fact]
    public void Validate_OkPermutation_ReturnsNull()
    {
        var existing = new HashSet<long> { 10, 20, 30 };
        var items = new[]
        {
            new ReorderItem(10, 2),
            new ReorderItem(20, 3),
            new ReorderItem(30, 1)
        };

        var error = PlaylistReorderValidator.Validate(items, existing);

        Assert.Null(error);
    }

    [Fact]
    public void Validate_MissingPosition_ReturnsBadRequest()
    {
        var existing = new HashSet<long> { 10, 20, 30 };
        var items = new[]
        {
            new ReorderItem(10, 1),
            new ReorderItem(20, 3),
            new ReorderItem(30, 3)
        };

        var error = PlaylistReorderValidator.Validate(items, existing);

        Assert.NotNull(error);
        Assert.Contains("Positions must be a permutation", error!.Message);
    }

    [Fact]
    public void Validate_DuplicatePosition_ReturnsBadRequest()
    {
        var existing = new HashSet<long> { 10, 20, 30 };
        var items = new[]
        {
            new ReorderItem(10, 1),
            new ReorderItem(20, 1),
            new ReorderItem(30, 2)
        };

        var error = PlaylistReorderValidator.Validate(items, existing);

        Assert.NotNull(error);
        Assert.Contains("Positions must be a permutation", error!.Message);
    }

    [Fact]
    public void Validate_WrongN_ReturnsBadRequest()
    {
        var existing = new HashSet<long> { 10, 20, 30 };
        var items = new[]
        {
            new ReorderItem(10, 1),
            new ReorderItem(20, 2)
        };

        var error = PlaylistReorderValidator.Validate(items, existing);

        Assert.NotNull(error);
        Assert.Contains("exactly 3", error!.Message);
    }

    [Fact]
    public void Validate_UnknownSongId_ReturnsBadRequest()
    {
        var existing = new HashSet<long> { 10, 20, 30 };
        var items = new[]
        {
            new ReorderItem(10, 1),
            new ReorderItem(20, 2),
            new ReorderItem(999, 3)
        };

        var error = PlaylistReorderValidator.Validate(items, existing);

        Assert.NotNull(error);
        Assert.Contains("reference exactly the songs", error!.Message);
    }

    [Fact]
    public void Validate_DuplicateSongId_ReturnsBadRequest()
    {
        var existing = new HashSet<long> { 10, 20, 30 };
        var items = new[]
        {
            new ReorderItem(10, 1),
            new ReorderItem(10, 2),
            new ReorderItem(30, 3)
        };

        var error = PlaylistReorderValidator.Validate(items, existing);

        Assert.NotNull(error);
        Assert.Contains("Duplicate songId", error!.Message);
    }
}

