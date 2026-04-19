using MusicPlaylist.Domain.Entities;

namespace MusicPlaylist.Application.Songs;

public sealed class SongService : ISongService
{
    private readonly ISongsReadRepository _readRepository;
    private readonly ISongsWriteRepository _writeRepository;

    public SongService(ISongsReadRepository readRepository, ISongsWriteRepository writeRepository)
    {
        _readRepository = readRepository;
        _writeRepository = writeRepository;
    }

    public async Task<IReadOnlyList<SongResponse>> ListAsync(
        string? genre,
        string? artist,
        CancellationToken cancellationToken = default)
    {
        var songs = await _readRepository.ListAsync(NormalizeFilter(genre), NormalizeFilter(artist), cancellationToken);
        return songs.Select(ToResponse).ToList();
    }

    public async Task<(SongResponse? Song, string? ErrorMessage)> CreateAsync(
        CreateSongRequest request,
        CancellationToken cancellationToken = default)
    {
        var error = Validate(request);
        if (error is not null)
        {
            return (null, error);
        }

        var entity = new Song
        {
            Title = request.Title.Trim(),
            Artist = request.Artist.Trim(),
            Album = request.Album.Trim(),
            DurationSeconds = request.DurationSeconds,
            Genre = request.Genre.Trim(),
            ReleaseDate = request.ReleaseDate
        };

        var saved = await _writeRepository.AddAsync(entity, cancellationToken);
        return (ToResponse(saved), null);
    }

    private static string? Validate(CreateSongRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return "Title is required.";
        }

        if (string.IsNullOrWhiteSpace(request.Artist))
        {
            return "Artist is required.";
        }

        if (string.IsNullOrWhiteSpace(request.Album))
        {
            return "Album is required.";
        }

        if (string.IsNullOrWhiteSpace(request.Genre))
        {
            return "Genre is required.";
        }

        if (request.DurationSeconds <= 0)
        {
            return "DurationSeconds must be greater than zero.";
        }

        return null;
    }

    private static string? NormalizeFilter(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private static SongResponse ToResponse(Song song) =>
        new(
            song.Id,
            song.Title,
            song.Artist,
            song.Album,
            song.DurationSeconds,
            song.Genre,
            song.ReleaseDate);
}
