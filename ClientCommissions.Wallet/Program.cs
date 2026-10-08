using ClientCommissions.Wallet.Application;
using ClientCommissions.Wallet.Endpoints;
using ClientCommissions.Wallet.Infrastructure;
using ClientCommissions.Wallet.Workers;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using Prometheus;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, config) => config
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

builder.Services.AddScoped<IWalletService, WalletService>();
builder.Services.AddDbContext<WalletDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.Configure<KafkaOptions>(builder.Configuration.GetSection("Kafka"));
builder.Services.AddSingleton<IProducer<string, string>>(sp =>
{
    var kafka = sp.GetRequiredService<IOptions<KafkaOptions>>().Value;
    return new ProducerBuilder<string, string>(new ProducerConfig
    {
        BootstrapServers = kafka.BootstrapServers,
        Acks = Acks.All,
        EnableIdempotence = true,
    }).Build();
});
builder.Services.AddHostedService<PayoutConsumerWorker>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("DefaultConnection")!);

var app = builder.Build();

app.MapHealthChecks("/health");

app.UseSwagger();
app.UseSwaggerUI();

app.UseSerilogRequestLogging();
app.UseHttpMetrics();

app.MapWalletEndpoints();
app.MapMetrics();

app.Run();
