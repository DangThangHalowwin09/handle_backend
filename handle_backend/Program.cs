using handle_backend.Services;

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
var app = builder.Build();

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

// The ENC0118 diagnostic is informational and relates to Edit and Continue (Hot Reload) in Visual Studio.
// It means changes to top-level statements (like those in Program.cs) require an application restart to take effect.
// No code change is required to fix the error itself. To resolve the issue during development, simply restart the application after making changes to this file.
