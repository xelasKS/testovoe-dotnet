using ClientCommissions.Commissions.Application;
using ClientCommissions.Commissions.Domain.Calculations;
using ClientCommissions.Commissions.Endpoints;
using ClientCommissions.Commissions.Infrastructure;
using ClientCommissions.Commissions.Workers;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Prometheus;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, config) => config
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

builder.Services.AddSingleton<ICommissionScheme, LinearScheme>();
builder.Services.AddSingleton<ICommissionScheme, FibonacciScheme>();
builder.Services.AddSingleton<ICommissionSchemeResolver, CommissionSchemeResolver>();
builder.Services.AddSingleton<ICommissionEngine, CommissionEngine>();

var usersBaseUrl = builder.Configuration["Services:Users"]
                   ?? throw new InvalidOperationException("Services:Users is not configured");
builder.Services.AddUsersHierarchyClient(usersBaseUrl);

builder.Services.AddScoped<IEventService, EventService>();
builder.Services.AddScoped<IEventQueryService, EventQueryService>();
builder.Services.AddScoped<ISchemaService, SchemaService>();

builder.Services.Configure<KafkaOptions>(builder.Configuration.GetSection("Kafka"));
builder.Services.AddSingleton<IProducer<string, string>>(sp =>
{
    var kafka = sp.GetRequiredService<IOptions<KafkaOptions>>().Value;
    var config = new ProducerConfig
    {
        BootstrapServers = kafka.BootstrapServers,
        Acks = Acks.All,
        EnableIdempotence = true,
    };
    return new ProducerBuilder<string, string>(config).Build();
});

builder.Services.AddHostedService<CommissionCalculationWorker>();
builder.Services.AddHostedService<PayoutSchedulerWorker>();
builder.Services.AddHostedService<OutboxDispatcherWorker>();

builder.Services.AddDbContext<ApplicationContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

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

app.MapEventEndpoints();
app.MapAdminEndpoints();
app.MapMetrics();

app.Run();
