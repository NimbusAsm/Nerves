using System.Reflection;
using Microsoft.OpenApi.Models;

namespace Nerves.ApiServer.Utils.Extensions;

public static class SwaggerExtension
{
    public static IServiceCollection ConfigureSwagger(this IServiceCollection services)
    {
#if DEBUG
        services.AddSwaggerGen(
            options =>
            {
                var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";

                options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename), true);

                typeof(ApiVersions).GetEnumNames().ToList().ForEach(version =>
                {
                    options.SwaggerDoc(version, new OpenApiInfo
                    {
                        Title = $"Nerves",
                        Version = version,
                        Description = $"Version: {version}"
                    });
                });
            }
        );
#endif

        return services;
    }

    public static IApplicationBuilder ConfigureSwaggerHttpPipeLine(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();

            app.UseSwaggerUI(
                options =>
                {
                    typeof(ApiVersions).GetEnumNames().ToList().ForEach(version =>
                    {
                        options.SwaggerEndpoint($"/swagger/{version}/swagger.json", version);
                    });
                }
            );
        }

        return app;
    }
}