using Nerves.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddNervesDefaults("nerves-directory");

builder.Services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapOpenApi();
app.MapNervesDefaults("nerves-directory");

app.Run();

/// <summary>Exposed so integration tests can host the service in-process.</summary>
public partial class Program;
