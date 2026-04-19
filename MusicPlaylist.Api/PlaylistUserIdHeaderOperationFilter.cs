using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MusicPlaylist.Api;

public sealed class PlaylistUserIdHeaderOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var path = context.ApiDescription.RelativePath;
        if (path is null || !path.StartsWith("api/playlists", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        operation.Parameters ??= new List<OpenApiParameter>();
        if (operation.Parameters.Any(p => p.Name == "X-User-Id" && p.In == ParameterLocation.Header))
        {
            return;
        }

        operation.Parameters.Add(
            new OpenApiParameter
            {
                Name = "X-User-Id",
                In = ParameterLocation.Header,
                Required = true,
                Description = "Owner user identifier for playlist operations.",
                Schema = new OpenApiSchema { Type = "string" }
            });
    }
}
