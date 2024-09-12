using APIContagem.Data;
using APIContagem.Tracing;
using DbUp;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Sinks.Grafana.Loki;
using Serilog.Enrichers.Span;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// Documentacao do OpenTelemetry:
// https://opentelemetry.io/docs/instrumentation/net/getting-started/

// Integracao do OpenTelemetry com Jaeger e tambem Grafana Tempo em .NET:
// https://github.com/open-telemetry/opentelemetry-dotnet/tree/e330e57b04fa3e51fe5d63b52bfff891fb5b7961/docs/trace/getting-started-jaeger#collect-and-visualize-traces-using-jaeger


Console.WriteLine("Executando Migrations com DbUp...");

// Aguarda 10 segundos para se assegurar de que
// a instancia do SQL Server esteja ativa 
Thread.Sleep(12_000);

var upgrader = DeployChanges.To.SqlDatabase(builder.Configuration.GetConnectionString("BaseMaster"))
    .WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly())
    .LogToConsole()
    .Build();
var result = upgrader.PerformUpgrade();

if (result.Successful)
{
    Console.WriteLine("Migrations do DbUp executadas com sucesso!");
}
else
{
    Environment.ExitCode = 3;
    Console.WriteLine($"Falha na execucao das Migrations do DbUp: {result.Error.Message}");
    return;
}

builder.Services.AddScoped<ContagemRepository>();

builder.Services.AddSerilog(new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.GrafanaLoki(
        builder.Configuration["Loki:Uri"]!,
        new List<LokiLabel>()
        {
            new()
            {
                Key = "service_name",
                Value = OpenTelemetryExtensions.ServiceName
            },
            new()
            {
                Key = "using_database",
                Value = "true"
            }
        })
    .Enrich.WithSpan(new SpanOptions() { IncludeOperationName = true, IncludeTags = true })
    .CreateLogger());

builder.Services.AddDbContext<ContagemContext>(options =>
{
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("BaseContagem"));
});

builder.Services.AddOpenTelemetry()
    .WithTracing((traceBuilder) =>
    {
        traceBuilder
            .AddSource(OpenTelemetryExtensions.ServiceName)
            .SetResourceBuilder(
                ResourceBuilder.CreateDefault()
                    .AddService(serviceName: OpenTelemetryExtensions.ServiceName,
                        serviceVersion: OpenTelemetryExtensions.ServiceVersion))
            .AddAspNetCoreInstrumentation()
            .AddSqlClientInstrumentation()
            .AddOtlpExporter()
            .AddConsoleExporter();
    });

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthorization();
app.UseSerilogRequestLogging();

app.MapControllers();

app.Run();