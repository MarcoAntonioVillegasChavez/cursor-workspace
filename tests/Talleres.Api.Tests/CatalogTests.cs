using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Talleres.Api.Tests;

public class CatalogTests
{
    private const string InitialWorkshopId = "11111111-1111-1111-1111-111111111111";
    private const string MissingWorkshopId = "22222222-2222-2222-2222-222222222222";
    private const string InitialTitle = "Taller de Cursor";
    private const string WorkshopNotFoundCode = "workshop_not_found";
    private const string WorkshopNotFoundTitle = "Recurso no encontrado";
    private const string WorkshopNotFoundDetail = "No se encontró el taller.";

    private static readonly string[] WorkshopPropertyNames =
    [
        "activeReservations",
        "availableSeats",
        "capacity",
        "id",
        "title",
    ];

    [Fact(DisplayName = "CAT-01: GET /api/workshops responde 200 con el taller inicial")]
    public async Task Cat01_GetCatalogo_Devuelve200ConTallerInicial()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        using var document = await ReadOkJson(client, "/api/workshops");

        var workshop = RequireSingleWorkshop(document.RootElement);
        AssertInitialWorkshop(workshop);
    }

    [Fact(DisplayName = "CAT-02: GET /api/workshops/{id} existente responde 200 con los mismos valores")]
    public async Task Cat02_GetDetalleExistente_Devuelve200ConLosMismosValores()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        using var document = await ReadOkJson(client, $"/api/workshops/{InitialWorkshopId}");

        Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);
        AssertInitialWorkshop(document.RootElement);
    }

    [Fact(DisplayName = "CAT-03: un GUID válido inexistente responde 404 workshop_not_found")]
    public async Task Cat03_GetGuidInexistente_Devuelve404WorkshopNotFound()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/workshops/{MissingWorkshopId}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        AssertMediaTypeStartsWith(response, "application/problem+json");

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        Assert.Equal(JsonValueKind.Object, root.ValueKind);

        Assert.True(root.TryGetProperty("status", out var status), "El 404 no incluye status en la raíz.");
        Assert.Equal(JsonValueKind.Number, status.ValueKind);
        Assert.Equal(404, status.GetInt32());

        Assert.True(root.TryGetProperty("code", out var code), "El 404 no incluye code en la raíz.");
        Assert.Equal(JsonValueKind.String, code.ValueKind);
        Assert.Equal(WorkshopNotFoundCode, code.GetString());

        Assert.True(root.TryGetProperty("title", out var title), "El 404 no incluye title en la raíz.");
        Assert.Equal(JsonValueKind.String, title.ValueKind);
        Assert.Equal(WorkshopNotFoundTitle, title.GetString());

        Assert.True(root.TryGetProperty("detail", out var detail), "El 404 no incluye detail en la raíz.");
        Assert.Equal(JsonValueKind.String, detail.ValueKind);
        Assert.Equal(WorkshopNotFoundDetail, detail.GetString());
    }

    [Fact(DisplayName = "CAT-04: GET /api/workshops/no-es-guid responde 404")]
    public async Task Cat04_GetIdNoGuid_Devuelve404()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/workshops/no-es-guid");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact(DisplayName = "CAT-05: repetir catálogo y detalle conserva un taller y no crea una reserva")]
    public async Task Cat05_RepetirCatalogoYDetalle_ConservaValoresSinCrearReserva()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var firstCatalog = await ReadOkJson(client, "/api/workshops");
        using var firstDetail = await ReadOkJson(client, $"/api/workshops/{InitialWorkshopId}");
        using var secondCatalog = await ReadOkJson(client, "/api/workshops");
        using var secondDetail = await ReadOkJson(client, $"/api/workshops/{InitialWorkshopId}");

        AssertInitialWorkshop(RequireSingleWorkshop(firstCatalog.RootElement));
        AssertDetailObject(firstDetail.RootElement);
        AssertInitialWorkshop(RequireSingleWorkshop(secondCatalog.RootElement));
        AssertDetailObject(secondDetail.RootElement);
    }

    [Fact(DisplayName = "CAT-06: catálogo y detalle exponen exactamente los cinco campos del taller")]
    public async Task Cat06_CatalogoYDetalle_TienenExactamenteCincoCampos()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        using var catalog = await ReadOkJson(client, "/api/workshops");
        using var detail = await ReadOkJson(client, $"/api/workshops/{InitialWorkshopId}");

        AssertExactWorkshopProperties(RequireSingleWorkshop(catalog.RootElement));
        Assert.Equal(JsonValueKind.Object, detail.RootElement.ValueKind);
        AssertExactWorkshopProperties(detail.RootElement);
    }

    [Fact(DisplayName = "CAT-07: availableSeats es capacity menos activeReservations")]
    public async Task Cat07_CupoDisponible_CoincideConCapacidadMenosActivas()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        using var catalog = await ReadOkJson(client, "/api/workshops");
        using var detail = await ReadOkJson(client, $"/api/workshops/{InitialWorkshopId}");

        AssertAvailableSeatsMatchCapacityMinusActive(RequireSingleWorkshop(catalog.RootElement));
        Assert.Equal(JsonValueKind.Object, detail.RootElement.ValueKind);
        AssertAvailableSeatsMatchCapacityMinusActive(detail.RootElement);
    }

    [Fact(DisplayName = "CAT-08: OpenAPI en Development documenta el catálogo, el detalle y su 404")]
    public async Task Cat08_OpenApiEnDevelopment_DocumentaCatalogoDetalleY404()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(body);
        Assert.True(document.RootElement.TryGetProperty("paths", out var paths));
        Assert.Equal(JsonValueKind.Object, paths.ValueKind);
        Assert.True(paths.TryGetProperty("/api/workshops", out _));

        var pathNames = paths.EnumerateObject().Select(property => property.Name).ToArray();
        var detailPaths = pathNames.Where(IsWorkshopDetailPath).ToArray();

        Assert.True(
            detailPaths.Length > 0,
            "OpenAPI no documenta una ruta de detalle /api/workshops/{parametro}. Paths: "
            + string.Join(", ", pathNames));

        var documented = detailPaths.Contains("/api/workshops/{id}", StringComparer.Ordinal)
            ? ["/api/workshops/{id}"]
            : detailPaths;

        foreach (var detailPath in documented)
        {
            AssertGetDocumentsNotFound(paths, detailPath);
        }
    }

    private static async Task<JsonDocument> ReadOkJson(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertMediaTypeStartsWith(response, "application/json");

        return JsonDocument.Parse(body);
    }

    private static JsonElement RequireSingleWorkshop(JsonElement catalog)
    {
        Assert.Equal(JsonValueKind.Array, catalog.ValueKind);
        Assert.Equal(1, catalog.GetArrayLength());

        var workshop = catalog[0];
        Assert.Equal(JsonValueKind.Object, workshop.ValueKind);
        return workshop;
    }

    private static void AssertDetailObject(JsonElement workshop)
    {
        Assert.Equal(JsonValueKind.Object, workshop.ValueKind);
        AssertInitialWorkshop(workshop);
    }

    private static void AssertInitialWorkshop(JsonElement workshop)
    {
        Assert.Equal(InitialWorkshopId, workshop.GetProperty("id").GetString());
        Assert.Equal(InitialTitle, workshop.GetProperty("title").GetString());
        Assert.Equal(2, workshop.GetProperty("capacity").GetInt32());
        Assert.Equal(0, workshop.GetProperty("activeReservations").GetInt32());
        Assert.Equal(2, workshop.GetProperty("availableSeats").GetInt32());
    }

    private static void AssertExactWorkshopProperties(JsonElement workshop)
    {
        var actual = workshop.EnumerateObject()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(WorkshopPropertyNames, actual, StringComparer.Ordinal);

        foreach (var property in workshop.EnumerateObject())
        {
            Assert.NotEqual(JsonValueKind.Array, property.Value.ValueKind);
            Assert.NotEqual(JsonValueKind.Object, property.Value.ValueKind);
        }
    }

    private static void AssertAvailableSeatsMatchCapacityMinusActive(JsonElement workshop)
    {
        var capacity = workshop.GetProperty("capacity").GetInt32();
        var activeReservations = workshop.GetProperty("activeReservations").GetInt32();
        var availableSeats = workshop.GetProperty("availableSeats").GetInt32();

        Assert.Equal(capacity - activeReservations, availableSeats);
    }

    private static bool IsWorkshopDetailPath(string path)
    {
        const string prefix = "/api/workshops/";
        if (!path.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var parameter = path[prefix.Length..];
        if (parameter.Length < 3 || parameter[0] != '{' || parameter[^1] != '}')
        {
            return false;
        }

        var name = parameter[1..^1];
        return name.Length > 0
            && !name.Contains('/')
            && !name.Contains('{')
            && !name.Contains('}');
    }

    private static void AssertGetDocumentsNotFound(JsonElement paths, string detailPath)
    {
        var pathItem = paths.GetProperty(detailPath);
        Assert.True(
            pathItem.TryGetProperty("get", out var operation),
            $"La ruta {detailPath} no documenta la operación GET.");
        Assert.True(
            operation.TryGetProperty("responses", out var responses),
            $"GET {detailPath} no documenta responses.");
        Assert.True(
            responses.TryGetProperty("404", out _),
            $"GET {detailPath} no documenta la respuesta 404.");
    }

    private static void AssertMediaTypeStartsWith(HttpResponseMessage response, string mediaType)
    {
        var contentType = response.Content.Headers.ContentType?.ToString() ?? string.Empty;
        Assert.StartsWith(mediaType, contentType, StringComparison.OrdinalIgnoreCase);
    }
}
