using Microsoft.AspNetCore.Mvc;
using MusicPlaylist.Application.Common;
using MusicPlaylist.Application.Playlists;

namespace MusicPlaylist.Api;

public static class PlaylistEndpoints
{
    public static void MapPlaylistEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/playlists").WithTags("Playlists");

        group.MapGet(
                "",
                async Task<IResult> (
                    HttpContext http,
                    [FromServices] IPlaylistService playlists,
                    CancellationToken cancellationToken) =>
                {
                    if (!TryGetUserId(http, out var userId, out var errorMessage))
                    {
                        return TypedResults.BadRequest(new ErrorResponse(errorMessage));
                    }

                    var (list, error) = await playlists.ListAsync(userId, cancellationToken);
                    return error is not null
                        ? TypedResults.BadRequest(new ErrorResponse(error.Message))
                        : TypedResults.Ok(list);
                })
            .WithName("ListPlaylists")
            .Produces<IReadOnlyList<PlaylistResponse>>(StatusCodes.Status200OK)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest);

        group.MapPost(
                "",
                async Task<IResult> (
                    HttpContext http,
                    [FromServices] IPlaylistService playlists,
                    [FromBody] CreatePlaylistRequest body,
                    CancellationToken cancellationToken) =>
                {
                    if (!TryGetUserId(http, out var userId, out var errorMessage))
                    {
                        return TypedResults.BadRequest(new ErrorResponse(errorMessage));
                    }

                    var (created, error) = await playlists.CreateAsync(userId, body, cancellationToken);
                    if (error is not null)
                    {
                        return ToErrorResult(error);
                    }

                    return TypedResults.Created($"/api/playlists/{created!.Id}", created);
                })
            .WithName("CreatePlaylist")
            .Produces<PlaylistResponse>(StatusCodes.Status201Created)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status409Conflict);

        group.MapPut(
                "{id:long}",
                async Task<IResult> (
                    HttpContext http,
                    [FromServices] IPlaylistService playlists,
                    long id,
                    [FromBody] UpdatePlaylistRequest body,
                    CancellationToken cancellationToken) =>
                {
                    if (!TryGetUserId(http, out var userId, out var errorMessage))
                    {
                        return TypedResults.BadRequest(new ErrorResponse(errorMessage));
                    }

                    var (updated, error) = await playlists.UpdateAsync(userId, id, body, cancellationToken);
                    if (error is not null)
                    {
                        return ToErrorResult(error);
                    }

                    return TypedResults.Ok(updated);
                })
            .WithName("UpdatePlaylist")
            .Produces<PlaylistResponse>(StatusCodes.Status200OK)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces<ErrorResponse>(StatusCodes.Status409Conflict);

        group.MapDelete(
                "{id:long}",
                async Task<IResult> (
                    HttpContext http,
                    [FromServices] IPlaylistService playlists,
                    long id,
                    CancellationToken cancellationToken) =>
                {
                    if (!TryGetUserId(http, out var userId, out var errorMessage))
                    {
                        return TypedResults.BadRequest(new ErrorResponse(errorMessage));
                    }

                    var error = await playlists.DeleteAsync(userId, id, cancellationToken);
                    return error is not null ? ToErrorResult(error) : TypedResults.NoContent();
                })
            .WithName("DeletePlaylist")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost(
                "{id:long}/songs",
                async Task<IResult> (
                    HttpContext http,
                    [FromServices] IPlaylistService playlists,
                    long id,
                    [FromBody] AddSongToPlaylistRequest body,
                    CancellationToken cancellationToken) =>
                {
                    if (!TryGetUserId(http, out var userId, out var errorMessage))
                    {
                        return TypedResults.BadRequest(new ErrorResponse(errorMessage));
                    }

                    var error = await playlists.AddSongAsync(userId, id, body, cancellationToken);
                    return error is not null ? ToErrorResult(error) : TypedResults.NoContent();
                })
            .WithName("AddSongToPlaylist")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces<ErrorResponse>(StatusCodes.Status409Conflict);

        group.MapDelete(
                "{id:long}/songs/{songId:long}",
                async Task<IResult> (
                    HttpContext http,
                    [FromServices] IPlaylistService playlists,
                    long id,
                    long songId,
                    CancellationToken cancellationToken) =>
                {
                    if (!TryGetUserId(http, out var userId, out var errorMessage))
                    {
                        return TypedResults.BadRequest(new ErrorResponse(errorMessage));
                    }

                    var error = await playlists.RemoveSongAsync(userId, id, songId, cancellationToken);
                    return error is not null ? ToErrorResult(error) : TypedResults.NoContent();
                })
            .WithName("RemoveSongFromPlaylist")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPut(
                "{id:long}/reorder",
                async Task<IResult> (
                    HttpContext http,
                    [FromServices] IPlaylistService playlists,
                    long id,
                    [FromBody] ReorderRequest body,
                    CancellationToken cancellationToken) =>
                {
                    if (!TryGetUserId(http, out var userId, out var errorMessage))
                    {
                        return TypedResults.BadRequest(new ErrorResponse(errorMessage));
                    }

                    var error = await playlists.ReorderAsync(userId, id, body, cancellationToken);
                    return error is not null ? ToErrorResult(error) : TypedResults.NoContent();
                })
            .WithName("ReorderPlaylist")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static bool TryGetUserId(HttpContext http, out string userId, out string errorMessage)
    {
        errorMessage = string.Empty;
        if (!http.Request.Headers.TryGetValue("X-User-Id", out var values))
        {
            userId = string.Empty;
            errorMessage = "X-User-Id header is required.";
            return false;
        }

        var raw = values.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(raw))
        {
            userId = string.Empty;
            errorMessage = "X-User-Id header is required.";
            return false;
        }

        userId = raw.Trim();
        return true;
    }

    private static IResult ToErrorResult(ServiceError error) =>
        error.StatusCode switch
        {
            StatusCodes.Status404NotFound => TypedResults.NotFound(),
            StatusCodes.Status409Conflict => TypedResults.Conflict(new ErrorResponse(error.Message)),
            StatusCodes.Status400BadRequest => TypedResults.BadRequest(new ErrorResponse(error.Message)),
            _ => TypedResults.Json(new ErrorResponse(error.Message), statusCode: error.StatusCode)
        };
}
