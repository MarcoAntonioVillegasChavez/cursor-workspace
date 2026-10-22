using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Talleres.Api.Tests.Support;

namespace Talleres.Api.Tests;

public class KnownBugTests
{
    [Fact(DisplayName = "KnownBug: HEAD sobre recursos de lectura responde 405 en lugar de 200")]
    [Trait("Category", "KnownBug")]
    public async Task HeadDeRecursosDeLectura_Responde200ComoGet()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var paths = new[]
        {
            "/health",
            "/api/workshops",
            "/api/workshops/11111111-1111-1111-1111-111111111111",
        };
        var failures = new List<string>();

        foreach (var path in paths)
        {
            var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, path));
            if (response.StatusCode != HttpStatusCode.OK)
            {
                var body = await response.Content.ReadAsStringAsync();
                failures.Add($"{path} -> {(int)response.StatusCode} {response.StatusCode} {Trim(body)}");
            }
        }

        Assert.True(
            failures.Count == 0,
            "HEAD debería responder 200 en los recursos que ya responden GET. Resultados: "
            + string.Join(" | ", failures));
    }

    [Fact(DisplayName = "KnownBug: el README niega el catálogo que la API ya publica")]
    [Trait("Category", "KnownBug")]
    public async Task Readme_NoDebeDecirQueElCatalogoLlegaDespuesCuandoLaApiYaLoPublica()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var catalogo = await client.GetFromJsonAsync<JsonWorkshop[]>("/api/workshops");

        Assert.NotNull(catalogo);
        Assert.Equal("11111111-1111-1111-1111-111111111111", catalogo[0].Id);
        Assert.Equal("Taller de Cursor", catalogo[0].Title);

        var readme = await File.ReadAllTextAsync(Path.Combine(RepoPaths.Root(), "README.md"));
        var normalized = string.Join(
            ' ',
            readme.Replace("\r", string.Empty, StringComparison.Ordinal)
                .Split('\n')
                .Select(line => line.Trim()));
        const string stale =
            "El catálogo, la disponibilidad y las reservas llegan en incrementos siguientes.";

        Assert.DoesNotContain(stale, normalized);
    }

    private static string Trim(string value)
    {
        var compact = value.Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
        return compact.Length <= 180 ? compact : compact[..180];
    }

    private sealed record JsonWorkshop(string? Id, string? Title);
}
