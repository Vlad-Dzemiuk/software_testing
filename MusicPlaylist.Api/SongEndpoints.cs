using Microsoft.AspNetCore.Mvc;
using MusicPlaylist.Application.Songs;

namespace MusicPlaylist.Api;

public static class SongEndpoints
{
    public static void MapSongEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/songs").WithTags("Songs");

        group.MapGet(
                "",
                async Task<IResult> (
                    [FromServices] ISongService songs,
                    string? genre,
                    string? artist,
                    CancellationToken cancellationToken) =>
                {
                    var list = await songs.ListAsync(genre, artist, cancellationToken);
                    return TypedResults.Ok(list);
                })
            .WithName("ListSongs")
            .Produces<IReadOnlyList<SongResponse>>(StatusCodes.Status200OK);

        group.MapPost(
                "",
                async Task<IResult> (
                    [FromServices] ISongService songs,
                    [FromBody] CreateSongRequest body,
                    CancellationToken cancellationToken) =>
                {
                    var (song, error) = await songs.CreateAsync(body, cancellationToken);
                    if (error is not null)
                    {
                        return TypedResults.BadRequest(new ErrorResponse(error));
                    }

                    return TypedResults.Created($"/api/songs/{song!.Id}", song);
                })
            .WithName("CreateSong")
            .Produces<SongResponse>(StatusCodes.Status201Created)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest);
    }
}
