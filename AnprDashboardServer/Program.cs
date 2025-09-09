using Microsoft.OpenApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using AnprDashboardServer.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowClient", policy =>
    {
        policy.WithOrigins("http://localhost:5286")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});
builder.Services.AddSignalR();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "ANPR API", Version = "v1" });
    c.OperationFilter<SwaggerFileOperationFilter>();
});
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=detections.db"));


var app = builder.Build();
app.UseStaticFiles();
app.UseRouting();
app.UseCors("AllowClient");

// Configure Swagger middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "ANPR API V1");
    });
}

//app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.MapHub<DetectionHub>("/detectionHub");

var controllerTypes = typeof(Program).Assembly.GetTypes()
    .Where(t => typeof(ControllerBase).IsAssignableFrom(t));
Console.WriteLine("Controllers loaded: " + string.Join(", ", controllerTypes.Select(t => t.Name)));

app.Run();

