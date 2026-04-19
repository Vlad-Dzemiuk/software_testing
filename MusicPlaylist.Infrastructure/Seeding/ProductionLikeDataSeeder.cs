using AutoFixture;
using Microsoft.EntityFrameworkCore;
using MusicPlaylist.Domain.Entities;
using MusicPlaylist.Infrastructure.Persistence;

namespace MusicPlaylist.Infrastructure.Seeding;

/// <summary>
/// Seeds a production-like volume: large song catalog, many playlists per user cohort,
/// and playlist–song links sized between parents (many songs reused across playlists).
/// Total row count is at least 10_000 across Songs, Playlists, and PlaylistSongs.
/// </summary>
public static class ProductionLikeDataSeeder
{
    private const int SongCount = 4200;
    private const int PlaylistCount = 900;
    private const int PlaylistSongTotal = 4900;
    private const int MaxSongsPerPlaylist = 100;

    private static int TotalRowCount => SongCount + PlaylistCount + PlaylistSongTotal;

    private static readonly string[] Genres =
    [
        "Rock", "Pop", "Jazz", "Electronic", "HipHop", "Classical", "Metal", "R&B", "Indie", "Folk"
    ];

    private const int UserPoolSize = 180;

    private static int SongCountForPlaylist(int playlistIndex)
    {
        var baseCount = PlaylistSongTotal / PlaylistCount;
        var remainder = PlaylistSongTotal % PlaylistCount;
        var n = baseCount + (playlistIndex < remainder ? 1 : 0);
        return Math.Min(n, MaxSongsPerPlaylist);
    }

    public static async Task SeedAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        if (TotalRowCount < 10_000)
        {
            throw new InvalidOperationException(
                $"Seed totals must be at least 10_000 rows; got {TotalRowCount}.");
        }

        var distributedLinks = Enumerable.Range(0, PlaylistCount).Sum(SongCountForPlaylist);
        if (distributedLinks != PlaylistSongTotal)
        {
            throw new InvalidOperationException(
                $"PlaylistSong distribution sums to {distributedLinks}, expected {PlaylistSongTotal}. " +
                $"Adjust counts so each playlist is at most {MaxSongsPerPlaylist} songs and totals match.");
        }

        var fixture = CreateFixture();

        var songs = new List<Song>(SongCount);
        for (var i = 0; i < SongCount; i++)
        {
            songs.Add(CreateSong(fixture, i));
        }

        const int songBatch = 100;
        for (var offset = 0; offset < songs.Count; offset += songBatch)
        {
            db.Songs.AddRange(songs.Skip(offset).Take(songBatch));
            await db.SaveChangesAsync(cancellationToken);
        }

        var playlists = new List<Playlist>(PlaylistCount);
        var nextPlaylistOrdinalByUser = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < PlaylistCount; i++)
        {
            var userIndex = (i % UserPoolSize) + 1;
            var userId = $"user-{userIndex:D3}";
            playlists.Add(CreatePlaylist(fixture, userId, nextPlaylistOrdinalByUser));
        }

        const int playlistBatch = 100;
        for (var offset = 0; offset < playlists.Count; offset += playlistBatch)
        {
            db.Playlists.AddRange(playlists.Skip(offset).Take(playlistBatch));
            await db.SaveChangesAsync(cancellationToken);
        }

        var links = new List<PlaylistSong>(PlaylistSongTotal);
        for (var p = 0; p < PlaylistCount; p++)
        {
            var playlist = playlists[p];
            var n = SongCountForPlaylist(p);
            var rnd = new Random(9_001 + p);
            var pickedIndices = new HashSet<int>();
            while (pickedIndices.Count < n)
            {
                pickedIndices.Add(rnd.Next(0, SongCount));
            }

            var orderedSongIds = pickedIndices
                .OrderBy(_ => rnd.Next())
                .Select(idx => songs[idx].Id)
                .ToList();

            for (var pos = 0; pos < n; pos++)
            {
                links.Add(
                    CreatePlaylistSong(
                        fixture,
                        playlist.Id,
                        orderedSongIds[pos],
                        position: pos + 1));
            }
        }

        const int linkBatch = 250;
        for (var offset = 0; offset < links.Count; offset += linkBatch)
        {
            db.PlaylistSongs.AddRange(links.Skip(offset).Take(linkBatch));
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static IFixture CreateFixture() => new Fixture();

    private static Song CreateSong(IFixture fixture, int index)
    {
        var duration = 45 + Math.Abs(fixture.Create<int>()) % (420 - 45 + 1);
        var releaseDate = fixture.Create<DateOnly>();
        var yearCap = DateOnly.FromDateTime(DateTime.UtcNow);
        if (releaseDate > yearCap)
        {
            releaseDate = yearCap;
        }

        return new Song
        {
            Title = Truncate(fixture.Create<string>(), 500),
            Artist = Truncate(fixture.Create<string>(), 300),
            Album = Truncate(fixture.Create<string>(), 300),
            DurationSeconds = duration,
            Genre = Genres[index % Genres.Length],
            ReleaseDate = releaseDate
        };
    }

    private static Playlist CreatePlaylist(
        IFixture fixture,
        string userId,
        Dictionary<string, int> nextOrdinalByUser)
    {
        nextOrdinalByUser.TryGetValue(userId, out var ordinal);
        nextOrdinalByUser[userId] = ordinal + 1;

        var label = Truncate(fixture.Create<string>(), 140);
        var name = Truncate($"{label} · {ordinal + 1:D5}", 200);

        var createdDaysAgo = Math.Abs(fixture.Create<int>()) % (3 * 365);
        var createdAt = new DateTimeOffset(
            DateTime.UtcNow.Date.AddDays(-createdDaysAgo),
            TimeSpan.Zero);

        var description = Truncate(fixture.Create<string?>(), 2000);

        return new Playlist
        {
            Name = name,
            Description = string.IsNullOrEmpty(description) ? null : description,
            UserId = userId,
            CreatedAt = createdAt,
            IsPublic = Math.Abs(fixture.Create<int>()) % 7 is 0 or 1 or 2
        };
    }

    private static PlaylistSong CreatePlaylistSong(
        IFixture fixture,
        long playlistId,
        long songId,
        int position)
    {
        var addedMinutesAgo = Math.Abs(fixture.Create<int>()) % (60 * 24 * 60);
        var addedAt = DateTimeOffset.UtcNow.AddMinutes(-addedMinutesAgo);

        return new PlaylistSong
        {
            PlaylistId = playlistId,
            SongId = songId,
            Position = position,
            AddedAt = addedAt
        };
    }

    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
