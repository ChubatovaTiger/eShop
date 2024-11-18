﻿using eShop.Catalog.API.Services;
using Microsoft.Extensions.AI;
using OpenAI;

public static class Extensions
{
    public static void AddApplicationServices(this IHostApplicationBuilder builder)
    {
        builder.AddNpgsqlDbContext<CatalogContext>("catalogdb", configureDbContextOptions: dbContextOptionsBuilder =>
        {
            dbContextOptionsBuilder.UseNpgsql(builder =>
            {
                builder.UseVector();
            });
        });

        // REVIEW: This is done for development ease but shouldn't be here in production
        builder.Services.AddMigration<CatalogContext, CatalogContextSeed>();

        // Add the integration services that consume the DbContext
        builder.Services.AddTransient<IIntegrationEventLogService, IntegrationEventLogService<CatalogContext>>();

        builder.Services.AddTransient<ICatalogIntegrationEventService, CatalogIntegrationEventService>();

        builder.AddRabbitMqEventBus("eventbus")
               .AddSubscription<OrderStatusChangedToAwaitingValidationIntegrationEvent, OrderStatusChangedToAwaitingValidationIntegrationEventHandler>()
               .AddSubscription<OrderStatusChangedToPaidIntegrationEvent, OrderStatusChangedToPaidIntegrationEventHandler>();

        builder.Services.AddOptions<CatalogOptions>()
            .BindConfiguration(nameof(CatalogOptions));

        if (builder.Configuration["OllamaEnabled"] is string ollamaEnabled && bool.Parse(ollamaEnabled))
        {
            builder.AddKeyedOllamaSharpEmbeddingGenerator("embedding");
            builder.Services.AddEmbeddingGenerator<string, Embedding<float>>(b => b
                .UseOpenTelemetry()
                .UseLogging()
                // Use the OllamaSharp embedding generator
                .Use(b.Services.GetRequiredKeyedService<IEmbeddingGenerator<string, Embedding<float>>>("embedding")));
        }
        else if (!string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("openai")))
        {
            builder.AddOpenAIClientFromConfiguration("openai");
            builder.Services.AddEmbeddingGenerator<string, Embedding<float>>(b => b
                .UseOpenTelemetry()
                .UseLogging()
                .Use(b.Services.GetRequiredService<OpenAIClient>().AsEmbeddingGenerator(builder.Configuration["AI:OpenAI:EmbeddingModel"]!)));
        }

        builder.Services.AddScoped<ICatalogAI, CatalogAI>();
    }
}
