using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Talleres.Api.Features.Workshops;
using Talleres.Api.Tests.Support;

namespace Talleres.Api.Tests;

public class CatalogEdgeCaseTests
{
    private const string InitialWorkshopId = "11111111-1111-1111-1111-111111111111";

    [Theory(DisplayName = "CAT-EDGE-01: formatos aceptados por la restricción guid resuelven el mismo taller")]
    [InlineData("/api/workshops/11111111-1111-1111-1111-111111111111")]
    [InlineData("/API/WORKSHOPS/11111111-1111-1111-1111-111111111111")]
    [InlineData("/api/workshops/11111111111111111111111111111111")]
    [InlineData("/api/workshops/%7B11111111-1111-1111-1111-111111111111%7D")]
    [InlineData("/api/workshops/%2011111111-1111-1111-1111-111111111111")]
    public async Task FormatosDeGuid_ResuelvenElTallerSemilla(string path)
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var document = await ReadOkJson(client, path);

        Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);
        Assert.Equal(InitialWorkshopId, document.RootElement.GetProperty("id").GetString());
        Assert.Equal(2, document.RootElement.GetProperty("availableSeats").GetInt32());
    }

    [Fact(DisplayName = "CAT-EDGE-01b: el GUID en mayúsculas devuelve el identificador canónico")]
    public async Task GuidEnMayusculas_DevuelveElIdentificadorCanonico()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var document = await ReadOkJson(
            client,
            "/api/workshops/11111111-1111-1111-1111-111111111111".ToUpperInvariant());

        Assert.Equal(InitialWorkshopId, document.RootElement.GetProperty("id").GetString());
    }

    [Theory(DisplayName = "CAT-EDGE-02: un GUID válido que no es el taller semilla responde workshop_not_found")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("ffffffff-ffff-ffff-ffff-ffffffffffff")]
    [InlineData("22222222222222222222222222222222")]
    public async Task GuidValidoInexistente_Devuelve404DeNegocio(string rawId)
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/workshops/{rawId}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        AssertMediaTypeStartsWith(response, "application/problem+json");
        AssertNoExcepcionExpuesta(body);

        using var document = JsonDocument.Parse(body);
        Assert.Equal(404, document.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("workshop_not_found", document.RootElement.GetProperty("code").GetString());
        Assert.Equal("Recurso no encontrado", document.RootElement.GetProperty("title").GetString());
        Assert.Equal("No se encontró el taller.", document.RootElement.GetProperty("detail").GetString());
    }

    [Theory(DisplayName = "CAT-EDGE-03: una ruta que no es un taller no responde 500")]
        [InlineData("/api/workshops/no-es-guid")]
        [InlineData("/api/workshops/%20")]
        [InlineData("/api/workshops/11111111-1111-1111-1111-11111111111")]
    [InlineData("/api/workshops/11111111-1111-1111-1111-111111111111/extra")]
    [InlineData("/api/workshops/%ZZ")]
    [InlineData("/api//workshops")]
    public async Task RutaInvalida_NoResponde500(string path)
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();

        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        AssertMediaTypeStartsWith(response, "application/problem+json");
        AssertNoExcepcionExpuesta(body);
        Assert.False(string.IsNullOrWhiteSpace(JsonDocument.Parse(body).RootElement.GetProperty("title").GetString()));
    }

    [Fact(DisplayName = "CAT-EDGE-04: una ruta muy larga no tumba el host")]
    public async Task RutaMuyLarga_NoResponde500()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var path = "/api/workshops/" + new string('a', 4000);

        var response = await client.GetAsync(path);

        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact(DisplayName = "CAT-EDGE-05: consultar con otra cultura y con query string no altera el catálogo")]
    public async Task CulturaYQueryString_NoAlteranElCatalogo()
    {
        var originalCulture = Thread.CurrentThread.CurrentCulture;
        var originalUiCulture = Thread.CurrentThread.CurrentUICulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("tr-TR");
            Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo("tr-TR");

            await using var factory = new WebApplicationFactory<Program>();
            using var client = factory.CreateClient();
            using var first = await ReadOkJson(client, "/api/workshops?page=1&vacio=");
            using var second = await ReadOkJson(client, "/API/Workshops/");

            AssertInitialWorkshop(RequireSingleWorkshop(first.RootElement));
            AssertInitialWorkshop(RequireSingleWorkshop(second.RootElement));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = originalCulture;
            Thread.CurrentThread.CurrentUICulture = originalUiCulture;
        }
    }

    [Fact(DisplayName = "CAT-EDGE-06: el catálogo HTTP refleja el cupo calculado y no lo comparte con otro host")]
    public async Task CupoHttp_ReflejaLasReservasDelMismoHost()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        await using var otherFactory = new WebApplicationFactory<Program>();
        using var otherClient = otherFactory.CreateClient();

        var store = factory.Services.GetRequiredService<ReservationStore>();
        var otherStore = otherFactory.Services.GetRequiredService<ReservationStore>();
        Assert.NotSame(store, otherStore);

        ReservationStoreSeeder.Add(store, ReservationStore.SeedWorkshopId, isActive: false, count: 2);
        ReservationStoreSeeder.Add(store, Guid.Parse("33333333-3333-3333-3333-333333333333"), isActive: true);
        ReservationStoreSeeder.Add(store, ReservationStore.SeedWorkshopId, isActive: true);

        using var catalogo = await ReadOkJson(client, "/api/workshops");
        using var detalle = await ReadOkJson(client, $"/api/workshops/{InitialWorkshopId}");
        using var otroCatalogo = await ReadOkJson(otherClient, "/api/workshops");

        AssertCupo(RequireSingleWorkshop(catalogo.RootElement), active: 1, available: 1);
        Assert.Equal(JsonValueKind.Object, detalle.RootElement.ValueKind);
        AssertCupo(detalle.RootElement, active: 1, available: 1);
        AssertCupo(RequireSingleWorkshop(otroCatalogo.RootElement), active: 0, available: 2);
    }

    [Fact(DisplayName = "CAT-EDGE-07: el JSON del taller no contiene datos de participantes")]
    public async Task JsonDelTaller_NoContieneDatosDeParticipantes()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var catalogo = await client.GetStringAsync("/api/workshops");
        var detalle = await client.GetStringAsync($"/api/workshops/{InitialWorkshopId}");

        foreach (var body in new[] { catalogo, detalle })
        {
            Assert.DoesNotContain("@", body, StringComparison.Ordinal);
            Assert.DoesNotContain("email", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("correo", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("nombre", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("participant", body, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact(DisplayName = "CAT-EDGE-08: varias consultas simultáneas siguen viendo un taller con cupo 2")]
    public async Task ConsultasSimultaneas_ConservanElCatalogoInicial()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 24).Select(_ => client.GetAsync("/api/workshops")));

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        foreach (var response in responses)
        {
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            AssertInitialWorkshop(RequireSingleWorkshop(document.RootElement));
            response.Dispose();
        }
    }

    private static void AssertCupo(JsonElement workshop, int active, int available)
    {
        Assert.Equal(2, workshop.GetProperty("capacity").GetInt32());
        Assert.Equal(active, workshop.GetProperty("activeReservations").GetInt32());
        Assert.Equal(available, workshop.GetProperty("availableSeats").GetInt32());
        Assert.Equal(
            workshop.GetProperty("capacity").GetInt32() - workshop.GetProperty("activeReservations").GetInt32(),
            workshop.GetProperty("availableSeats").GetInt32());
    }

    private static void AssertInitialWorkshop(JsonElement workshop)
    {
        Assert.Equal(InitialWorkshopId, workshop.GetProperty("id").GetString());
        Assert.Equal("Taller de Cursor", workshop.GetProperty("title").GetString());
        AssertCupo(workshop, active: 0, available: 2);
    }

    private static JsonElement RequireSingleWorkshop(JsonElement catalog)
    {
        Assert.Equal(JsonValueKind.Array, catalog.ValueKind);
        Assert.Equal(1, catalog.GetArrayLength());
        return catalog[0];
    }

    private static async Task<JsonDocument> ReadOkJson(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertMediaTypeStartsWith(response, "application/json");
        return JsonDocument.Parse(body);
    }

    private static void AssertMediaTypeStartsWith(HttpResponseMessage response, string mediaType)
    {
        var contentType = response.Content.Headers.ContentType?.ToString() ?? string.Empty;
        Assert.StartsWith(mediaType, contentType, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertNoExcepcionExpuesta(string text)
    {
        Assert.DoesNotContain("NullReferenceException", text, StringComparison.Ordinal);
        Assert.DoesNotContain("stacktrace", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stack trace", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(".cs:line", text, StringComparison.OrdinalIgnoreCase);
    }
}
