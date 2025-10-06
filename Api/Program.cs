using EmbeddingPoC;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using OllamaSharp;
using Pgvector;
using Pgvector.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLogging();
builder.Services.AddDbContext<EmbeddingDbContext>(options =>
{
    var loggerFactory = LoggerFactory.Create(loggingBuilder =>
    {
        loggingBuilder.AddConsole();
        loggingBuilder.SetMinimumLevel(LogLevel.Information);
    });
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

    options.UseNpgsql(connectionString, c => c.UseVector())
        .UseLoggerFactory(loggerFactory)
        .EnableSensitiveDataLogging();
});
builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
{
    var modeId = builder.Configuration["Ollama:EmbeddingModel"];
    var baseUrl = builder.Configuration["Ollama:BaseUrl"];
    return new OllamaApiClient(baseUrl, modeId);
});

var app = builder.Build();

app.MapGet("/text/search", async (string query, EmbeddingDbContext context, IEmbeddingGenerator<string, Embedding<float>> embeddingService) =>
{
    int limit = 5;

    var embeddings = await embeddingService.GenerateAsync(query);
    var queryEmbedding = new Vector(embeddings.Vector);

    var similarTexts = await context.TextEmbeddings
        .OrderBy(x => x.Embedding.CosineDistance(queryEmbedding))
        .Select(x => new Text(x.Content))
        .Take(limit)
        .ToListAsync();

    return TypedResults.Ok(similarTexts);
});

app.MapGet("/text", async (EmbeddingDbContext context) =>
{
    int limit = 20;

    var similarTexts = await context.TextEmbeddings
        .OrderBy(x => x.Id)
        .Take(limit)
        .Select(x => new Text(x.Content))
        .ToListAsync();

    return TypedResults.Ok(similarTexts);
});

app.MapPost("/text", async (Text[] request, EmbeddingDbContext context, IEmbeddingGenerator<string, Embedding<float>> embeddingService) =>
{
    var embeddings = await embeddingService.GenerateAsync(request.Select(s => s.Content));

    var dbModels = embeddings.Select((embedding, index) => new TextEmbedding
    {
        Content = request[index].Content,
        Embedding = new Vector(embedding.Vector),
        CreatedAt = DateTime.UtcNow
    }).ToList();

    context.AddRange(dbModels);
    await context.SaveChangesAsync();

    return TypedResults.Created();
});

app.Run();

public record Text(string Content);