using Microsoft.AspNetCore.Http;

namespace MusicPlaylist.Application.Common;

public sealed record ServiceError(string Message, int StatusCode)
{
    public static ServiceError BadRequest(string message) => new(message, StatusCodes.Status400BadRequest);
    public static ServiceError NotFound(string message) => new(message, StatusCodes.Status404NotFound);
    public static ServiceError Conflict(string message) => new(message, StatusCodes.Status409Conflict);
}
