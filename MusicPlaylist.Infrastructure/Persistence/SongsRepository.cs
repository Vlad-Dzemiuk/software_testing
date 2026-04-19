using Microsoft.EntityFrameworkCore;
using MusicPlaylist.Application.Songs;
using MusicPlaylist.Domain.Entities;

namespace MusicPlaylist.Infrastructure.Persistence;

public sealed class SongsRepository : ISongsReadRepository, ISongsWriteRepository
{
    private readonly AppDbContext _db;

    public SongsRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Song>> ListAsync(
        string? genre,
        string? artist,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Songs.AsNoTracking();

        if (genre is not null)
        {
            var genrePattern = ToContainsPattern(genre);
            query = query.Where(s => EF.Functions.ILike(s.Genre, genrePattern));
        }

        if (artist is not null)
        {
            var artistPattern = ToContainsPattern(artist);
            query = query.Where(s => EF.Functions.ILike(s.Artist, artistPattern));
        }

        return await query
            .OrderBy(s => s.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<Song> AddAsync(Song song, CancellationToken cancellationToken = default)
    {
        _db.Songs.Add(song);
        await _db.SaveChangesAsync(cancellationToken);
        return song;
    }

    /// <summary>
    /// Builds a case-insensitive LIKE pattern; strips characters that would break LIKE semantics.
    /// </summary>
    private static string ToContainsPattern(string filter)
    {
        var literal = filter.Replace("\\", "", StringComparison.Ordinal)
            .Replace("%", "", StringComparison.Ordinal)
            .Replace("_", "", StringComparison.Ordinal);

        return $"%{literal}%";
    }
}
