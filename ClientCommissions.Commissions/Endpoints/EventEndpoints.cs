using ClientCommissions.Commissions.Application;
using ClientCommissions.Commissions.Contracts;

namespace ClientCommissions.Commissions.Endpoints;

public static class EventEndpoints
{
    public static IEndpointRouteBuilder MapEventEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/events", async (
                CreateEventRequest request,
                IEventService service,
                CancellationToken ct) =>
            {
                await service.IngestAsync(request, ct);
                return Results.Accepted();
            })
            .WithName("IngestEvent")
            .WithTags("Events")
            .WithSummary("Include profit events in commission calculation")
            .Produces(StatusCodes.Status202Accepted);

        app.MapGet("/events", async (
                Guid userId,
                IEventQueryService query,
                CancellationToken ct) =>
            {
                var list = await query.GetUserEventsAsync(userId, ct);
                return Results.Ok(list);
            })
            .WithName("GetUserEvents")
            .WithTags("Events")
            .WithSummary("User events for commission calculation (query: userId)")
            .Produces<IReadOnlyList<EventSummaryResponse>>(StatusCodes.Status200OK);

        app.MapGet("/events/{id:guid}", async (
                Guid id,
                IEventQueryService query,
                CancellationToken ct) =>
            {
                var detail = await query.GetEventDetailAsync(id, ct);
                return detail is null ? Results.NotFound() : Results.Ok(detail);
            })
            .WithName("GetEventDetail")
            .WithTags("Events")
            .WithSummary("Fetch commission breakdown: recipient, amount, schema, and payout status")
            .Produces<EventDetailResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }
}
