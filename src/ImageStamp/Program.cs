using ImageStamp.Core.Imaging;
using ImageStamp.Core.Options;
using ImageStamp.Infrastructure.Persistence;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
//Swagger
builder.Services.AddSwaggerGen();

builder.Services.Configure<ImageProcessingOptions>(
    builder.Configuration.GetSection(ImageProcessingOptions.SectionName));

// The composition service owns the PNG encoder and the copy buffer it reuses across requests, so a
// single instance is kept for the lifetime of the process instead of rebuilding it every time.
builder.Services.AddSingleton<ImageCompositionService>();

builder.Services.AddScoped<CompositionRepository>();

WebApplication app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.MapOpenApi();
}

app.MapControllers();

app.MapGet("/health", async (CompositionRepository repository, CancellationToken cancellationToken) =>
{
    bool reachable = await repository.CanConnectAsync(cancellationToken);

    return reachable
        ? Results.Ok(new { status = "healthy" })
        : Results.Json(new { status = "degraded" }, statusCode: StatusCodes.Status503ServiceUnavailable);
});

app.Run();
