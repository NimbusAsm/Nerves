using Nerves.ApiServer;
using Nerves.ApiServer.Utils.Extensions;

Instances.Init();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AllowAllOrigins();

// Add services to the container.
builder.Services.AddControllers();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();

builder.Services.ConfigureSwagger();

var app = builder.Build();

app.ConfigureSwaggerHttpPipeLine();

app.UseHttpsRedirection();

app.AllowAllOrigins();

app.UseAuthorization();

app.MapControllers();

app.Run();
