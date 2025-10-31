using handle_backend.Services;
using handle_backend.Services.Firebase;
using handle_backend.Services.XML;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy => policy
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader());
});

builder.Services.AddSingleton<HandleXML>();
// Program.cs
builder.Services.AddSingleton<IFileWatcherService, FileWatcherService>();
builder.Services.AddSingleton<FirebaseService>();

var app = builder.Build();

using var scope = app.Services.CreateScope();
var watcher = scope.ServiceProvider.GetRequiredService<IFileWatcherService>();
await watcher.StartAsync();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}


app.UseHttpsRedirection();

app.UseCors("AllowAll");

app.UseAuthorization();

app.MapControllers();

app.Run();
