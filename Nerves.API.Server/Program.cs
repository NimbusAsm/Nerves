using Microsoft.OpenApi.Models;
using Nerves.API.Server;
using Nerves.API.Server.Services;
using System.Reflection;

var configs = new ConfigManager()
    .SetLocation("configs")
    ;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();

#if !Debug
builder.Services.AddSwaggerGen(
    options =>
    {
        var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename), true);
        //生成多个文档显示
        typeof(ApiVersions).GetEnumNames().ToList().ForEach(version =>
        {
            //添加文档介绍
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

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(
        options =>
        {
            /*
             options.SwaggerEndpoint($"/swagger/V1/swagger.json",$"版本选择:V1");
            */
            //如果只有一个版本也要和上方保持一致
            typeof(ApiVersions).GetEnumNames().ToList().ForEach(version =>
            {
                //切换版本操作
                //参数一是使用的哪个json文件,参数二就是个名字
                options.SwaggerEndpoint($"/swagger/{version}/swagger.json", version);
            });
        }
    );
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
