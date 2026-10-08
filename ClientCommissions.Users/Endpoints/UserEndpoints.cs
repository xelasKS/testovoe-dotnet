using ClientCommissions.Shared;
using ClientCommissions.Shared.Contracts;
using ClientCommissions.Users.Application;
using ClientCommissions.Users.Contracts;

namespace ClientCommissions.Users.Endpoints;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/users").WithTags("Users");

        group.MapPost("/", async (CreateUserRequest request, IUserService users, CancellationToken ct) =>
            {
                try
                {
                    var user = await users.CreateAsync(request.Id, request.ReferrerId, ct);
                    return Results.Created($"/users/{user.Id}",
                        new CreateUserResponse(user.Id, user.ReferrerId));
                }
                catch (ReferrerNotFoundException e)
                {
                    return Results.BadRequest(e.Message);
                }
            })
            .WithName("CreateUser")
            .WithSummary("Create user")
            .Produces<CreateUserResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest);

        group.MapGet("/{id:guid}/ancestors", async (
                Guid id, int? maxDepth, IUserService users, CancellationToken ct) =>
            {
                var depth = Math.Clamp(maxDepth ?? ChainDepth.Max, 1, ChainDepth.Max);
                var chain = await users.GetAncestorsAsync(id, depth, ct);
                return chain is null ? Results.NotFound() : Results.Ok(chain);
            })
            .WithName("GetAncestors")
            .WithSummary("Hierarchy chain to up")
            .Produces<IReadOnlyList<AncestorContract>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/descendants", async (
                Guid id, int? maxDepth, IUserService users, CancellationToken ct) =>
            {
                var depth = Math.Clamp(maxDepth ?? ChainDepth.Max, 1, ChainDepth.Max);
                var tree = await users.GetDescendantsAsync(id, depth, ct);
                return tree is null ? Results.NotFound() : Results.Ok(tree);
            })
            .WithName("GetDescendants")
            .WithSummary("Hierarchy chain to down")
            .Produces<IReadOnlyList<DescendantResponse>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }
}
