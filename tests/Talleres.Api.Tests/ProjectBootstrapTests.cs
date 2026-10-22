using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;

namespace Talleres.Api.Tests;

public class ProjectBootstrapTests
{
    [Fact(DisplayName = "PROY-04: GET /health responde 200 y status ok")]
    public async Task Proy04_GetHealth_Devuelve200YStatusOk()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertMediaTypeStartsWith(response, "application/json");

        using var document = JsonDocument.Parse(body);
        Assert.Equal("ok", document.RootElement.GetProperty("status").GetString());
    }

    [Fact(DisplayName = "PROY-05: GET /openapi/v1.json en Development incluye la operación /health")]
    public async Task Proy05_GetOpenApiEnDevelopment_Devuelve200YContieneHealth()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(body);
        Assert.True(document.RootElement.TryGetProperty("paths", out var paths));
        Assert.True(paths.TryGetProperty("/health", out _));
    }

    [Fact(DisplayName = "PROY-06: GET /openapi/v1.json en Production responde 404 ProblemDetails")]
    public async Task Proy06_GetOpenApiEnProduction_Devuelve404ProblemDetailsSinExcepcion()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(Environments.Production);
        });
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");

        await AssertNotFoundProblemDetails(response);
    }

    [Fact(DisplayName = "PROY-06: GET /ruta-inexistente responde 404 ProblemDetails")]
    public async Task Proy06_GetRutaInexistenteEnDevelopment_Devuelve404ProblemDetailsSinExcepcion()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/ruta-inexistente");

        await AssertNotFoundProblemDetails(response);
    }

    private static void AssertMediaTypeStartsWith(HttpResponseMessage response, string mediaType)
    {
        var contentType = response.Content.Headers.ContentType?.ToString() ?? string.Empty;
        Assert.StartsWith(mediaType, contentType, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task AssertNotFoundProblemDetails(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        AssertMediaTypeStartsWith(response, "application/problem+json");

        var body = await response.Content.ReadAsStringAsync();
        AssertNoExcepcionExpuesta(body);

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        Assert.True(root.TryGetProperty("status", out var status));
        Assert.Equal(JsonValueKind.Number, status.ValueKind);
        Assert.Equal(404, status.GetInt32());

        Assert.True(root.TryGetProperty("title", out var titleElement));
        Assert.False(string.IsNullOrWhiteSpace(titleElement.GetString()));

        if (root.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String)
        {
            AssertNoExcepcionExpuesta(detail.GetString() ?? string.Empty);
        }
    }

    private static void AssertNoExcepcionExpuesta(string text)
    {
        Assert.DoesNotContain("exception", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stacktrace", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stack trace", text, StringComparison.OrdinalIgnoreCase);
    }
}
