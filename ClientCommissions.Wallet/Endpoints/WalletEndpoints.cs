using ClientCommissions.Wallet.Application;
using ClientCommissions.Wallet.Contracts;

namespace ClientCommissions.Wallet.Endpoints;

public static class WalletEndpoints
{
    public static IEndpointRouteBuilder MapWalletEndpoints(this IEndpointRouteBuilder app)
    {
        var accounts = app.MapGroup("/accounts").WithTags("Accounts");

        accounts.MapGet("/{userId:guid}", async (
                Guid userId, IWalletService wallet, CancellationToken ct) =>
            {
                var balance = await wallet.GetBalanceAsync(userId, ct);
                return balance is null
                    ? Results.NotFound()
                    : Results.Ok(new BalanceResponse(userId, balance.Value));
            })
            .WithName("GetBalance")
            .WithSummary("Current user balance")
            .Produces<BalanceResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        accounts.MapGet("/{userId:guid}/payouts", async (
                Guid userId, IWalletService wallet, CancellationToken ct) =>
            {
                var payouts = await wallet.GetPayoutsAsync(userId, ct);
                var response = payouts.Select(p => new PayoutResponse(p.CommissionId, p.Amount, p.CreatedAt));
                return Results.Ok(response);
            })
            .WithName("GetPayouts")
            .WithSummary("User payments history")
            .Produces<IEnumerable<PayoutResponse>>(StatusCodes.Status200OK);

        return app;
    }
}
