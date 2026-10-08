using ClientCommissions.Commissions.Application;
using ClientCommissions.Commissions.Contracts;
using ClientCommissions.Commissions.Domain.Entities;

namespace ClientCommissions.Commissions.Endpoints;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/admin").WithTags("Admin");

        admin.MapGet("/schema", async (ISchemaService schema, CancellationToken ct) =>
                Results.Ok(new { schema = (await schema.GetCurrentAsync(ct)).ToString() }))
            .WithName("GetSchema")
            .WithSummary("Current commission schema")
            .Produces(StatusCodes.Status200OK);

        admin.MapPut("/schema", async (SwitchSchemaRequest request, ISchemaService schema, CancellationToken ct) =>
            {
                if (!Enum.TryParse<SchemaType>(request.Schema, ignoreCase: true, out var parsed))
                    return Results.BadRequest($"Unknown schema '{request.Schema}'. Expected: Linear or Fibonacci.");

                await schema.SwitchAsync(parsed, ct);
                return Results.Ok(new { schema = parsed.ToString() });
            })
            .WithName("SwitchSchema")
            .WithSummary("Switch schema (applies to new calculations only)")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);

        return app;
    }
}
