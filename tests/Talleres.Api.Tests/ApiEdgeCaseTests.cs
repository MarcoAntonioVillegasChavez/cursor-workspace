using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Talleres.Api.Tests;

public class ApiEdgeCaseTests
{
    private const string SeedId = "11111111-1111-1111-1111-111111111111";
    private const string SeedTitle = "Taller de Cursor";

    [Theory(DisplayName = "EDGE-01: la ruta y el GUID en cualquier mayúscula devuelven el id canónico")]
    [InlineData("/api/workshops")]
    [InlineData("/API/WORKSHOPS")]
    [InlineData("/Api/Workshops")]
    public async Task Edge01_RutaEnCualquierMayuscula_DevuelveElCatalogoInicial(string path)
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var document = await ReadOk(client, path);
        AssertInitialWorkshop(RequireSingle(document.RootElement));
    }

    [Theory(DisplayName = "EDGE-02: los formatos D, N, B y P del GUID semilla devuelven el mismo detalle")]
    [InlineData("/api/workshops/11111111-1111-1111-1111-111111111111")]
    [InlineData("/API/WORKSHOPS/11111111-1111-1111-1111-111111111111")]
    [InlineData("/api/workshops/11111111111111111111111111111111")]
    [InlineData("/api/workshops/%7B11111111-1111-1111-1111-111111111111%7D")]
    [InlineData("/api/workshops/(11111111-1111-1111-1111-111111111111)")]
    public async Task Edge02_FormatosDeGuidDelTallerInicial_DevuelvenElDetalleCanonico(string path)
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain("@", body, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Title\"", body, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Capacity\"", body, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(body);
        Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);
        AssertInitialWorkshop(document.RootElement);
    }

    [Theory(DisplayName = "EDGE-03: un GUID válido que no es el taller semilla responde 404 de negocio")]
    [InlineData("/api/workshops/00000000-0000-0000-0000-000000000000")]
    [InlineData("/api/workshops/FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF")]
    [InlineData("/api/workshops/22222222-2222-2222-2222-222222222222")]
    [InlineData("/api/workshops/22222222222222222222222222222222")]
    [InlineData("/api/workshops/%7B22222222-2222-2222-2222-222222222222%7D")]
    public async Task Edge03_GuidValidoInexistente_Devuelve404DeNegocio(string path)
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(path);
        await AssertWorkshopNotFound(response);

        using var catalog = await ReadOk(client, "/api/workshops");
        AssertInitialWorkshop(RequireSingle(catalog.RootElement));
    }

    [Theory(DisplayName = "EDGE-04: una ruta inválida responde 4xx, no 500, y no altera el catálogo")]
    [InlineData("/api/workshops/no-es-guid")]
    [InlineData("/api/workshops/0")]
    [InlineData("/api/workshops/null")]
    [InlineData("/api/workshops/undefined")]
    [InlineData("/api/workshops/11111111-1111-1111-1111-11111111111")]
    [InlineData("/api/workshops/11111111-1111-1111-1111-1111111111111")]
    [InlineData("/api/workshops/gggggggg-gggg-gggg-gggg-gggggggggggg")]
    [InlineData("/api/workshops/11111111-1111-1111-1111-111111111111/participantes")]
    [InlineData("/api/workshops/%20")]
    [InlineData("/api/workshops/taller")]
    [InlineData("/health/extra")]
    [InlineData("/ruta-inexistente")]
    [InlineData("/api//workshops")]
    public async Task Edge04_RutaInvalida_Responde4xxSinExcepcionYNoAlteraElCatalogo(string path)
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();

        Assert.InRange((int)response.StatusCode, 400, 499);
        AssertNoExceptionLeak(body);

        using var catalog = await ReadOk(client, "/api/workshops");
        AssertInitialWorkshop(RequireSingle(catalog.RootElement));
    }

    [Theory(DisplayName = "EDGE-05: una consulta desconocida no filtra talleres ni cambia el cupo")]
    [InlineData("/api/workshops?title=otro&id=22222222-2222-2222-2222-222222222222&page=-1&email=a@b.com")]
    [InlineData("/api/workshops?capacity=0")]
    [InlineData("/api/workshops/11111111-1111-1111-1111-111111111111?expand=participants&email=a@b.com")]
    public async Task Edge05_ConsultaDesconocida_NoFiltraNiAltera(string path)
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("@", body, StringComparison.Ordinal);
        Assert.DoesNotContain("participant", body, StringComparison.OrdinalIgnoreCase);

        using var document = JsonDocument.Parse(body);
        if (path.Contains("expand", StringComparison.Ordinal))
        {
            Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);
            AssertInitialWorkshop(document.RootElement);
        }
        else
        {
            AssertInitialWorkshop(RequireSingle(document.RootElement));
        }
    }

    [Theory(DisplayName = "EDGE-06: un método distinto de GET no modifica el catálogo")]
    [InlineData("POST", "/api/workshops")]
    [InlineData("PUT", "/api/workshops")]
    [InlineData("DELETE", "/api/workshops")]
    [InlineData("PATCH", "/api/workshops")]
    [InlineData("POST", "/api/workshops/11111111-1111-1111-1111-111111111111")]
    [InlineData("DELETE", "/api/workshops/11111111-1111-1111-1111-111111111111")]
    [InlineData("PUT", "/health")]
    [InlineData("PATCH", "/health")]
    public async Task Edge06_MetodoNoAdmitido_NoModificaElCatalogo(string method, string path)
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var request = new HttpRequestMessage(new HttpMethod(method), path)
        {
            Content = new StringContent(
                "{\"title\":\"Intruso\",\"email\":\"persona@example.com\",\"name\":\"Ada\"}",
                Encoding.UTF8,
                "application/json")
        };
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        Assert.Contains("GET", ReadAllow(response), StringComparer.OrdinalIgnoreCase);
        AssertNoExceptionLeak(body);
        Assert.DoesNotContain("persona@example.com", body, StringComparison.OrdinalIgnoreCase);

        using var catalog = await ReadOk(client, "/api/workshops");
        AssertInitialWorkshop(RequireSingle(catalog.RootElement));

        var health = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }

    [Theory(DisplayName = "EDGE-07: quien acepta JSON recibe ProblemDetails en una ruta inexistente")]
    [InlineData(null)]
    [InlineData("application/json")]
    [InlineData("application/problem+json")]
    [InlineData("*/*")]
    public async Task Edge07_RutaInexistente_CuandoAceptaJson_DevuelveProblemDetails(string? accept)
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        if (accept is not null)
        {
            client.DefaultRequestHeaders.Accept.ParseAdd(accept);
        }

        var response = await client.GetAsync("/ruta-inexistente");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        AssertNoExceptionLeak(body);

        using var document = JsonDocument.Parse(body);
        Assert.Equal(404, document.RootElement.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("title").GetString()));
    }

    [Fact(DisplayName = "EDGE-08: un cliente que solo acepta HTML no recibe 500 ni una traza")]
    public async Task Edge08_RutaInexistente_SoloHtml_NoDevuelve500NiTraza()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Accept.ParseAdd("text/html");

        var response = await client.GetAsync("/ruta-inexistente");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        AssertNoExceptionLeak(body);
        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact(DisplayName = "EDGE-09: producción sigue sirviendo salud y catálogo, y oculta OpenAPI")]
    public async Task Edge09_Produccion_SirveLaApiYOcultaOpenApi()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(Environments.Production);
        });
        using var client = factory.CreateClient();

        await AssertHealth(client);
        using var catalog = await ReadOk(client, "/api/workshops");
        AssertInitialWorkshop(RequireSingle(catalog.RootElement));
        using var detail = await ReadOk(client, $"/api/workshops/{SeedId}");
        AssertInitialWorkshop(detail.RootElement);

        var openApi = await client.GetAsync("/openapi/v1.json");
        var body = await openApi.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.NotFound, openApi.StatusCode);
        Assert.Equal("application/problem+json", openApi.Content.Headers.ContentType?.MediaType);
        AssertNoExceptionLeak(body);
        using var document = JsonDocument.Parse(body);
        Assert.Equal(404, document.RootElement.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("title").GetString()));
    }

    [Fact(DisplayName = "EDGE-10: un entorno que no es Development tampoco publica OpenAPI")]
    public async Task Edge10_Staging_OcultaOpenApiYConservaElCatalogo()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(Environments.Staging);
        });
        using var client = factory.CreateClient();

        var openApi = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.NotFound, openApi.StatusCode);
        Assert.Equal("application/problem+json", openApi.Content.Headers.ContentType?.MediaType);

        using var catalog = await ReadOk(client, "/api/workshops");
        AssertInitialWorkshop(RequireSingle(catalog.RootElement));
    }

    [Fact(DisplayName = "EDGE-11: salud responde exactamente status ok en Development y Production")]
    public async Task Edge11_Salud_TieneSoloElCampoStatus()
    {
        using var development = new WebApplicationFactory<Program>();
        using var developmentClient = development.CreateClient();
        await AssertHealth(developmentClient);

        using var production = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(Environments.Production);
        });
        using var productionClient = production.CreateClient();
        await AssertHealth(productionClient);
    }

    [Fact(DisplayName = "EDGE-12: con cultura fr-FR el cupo sigue siendo un entero JSON")]
    public async Task Edge12_CulturaFrFr_NoCambiaLosNumerosJson()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<IStartupFilter, FrenchCultureStartupFilter>();
            });
        });
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/workshops");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("fr-FR", response.Headers.GetValues("X-Test-Culture").Single());
        Assert.DoesNotContain("2,0", body, StringComparison.Ordinal);
        Assert.DoesNotContain("2.0", body, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(body);
        var workshop = RequireSingle(document.RootElement);
        Assert.Equal(JsonValueKind.Number, workshop.GetProperty("capacity").ValueKind);
        Assert.Equal(2, workshop.GetProperty("capacity").GetInt32());
        Assert.Equal(0, workshop.GetProperty("activeReservations").GetInt32());
        Assert.Equal(2, workshop.GetProperty("availableSeats").GetInt32());
    }

    [Fact(DisplayName = "EDGE-13: lecturas HTTP simultáneas del mismo host conservan el taller inicial", Timeout = 20000)]
    public async Task Edge13_LecturasSimultaneas_ConservanElTallerInicial()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var failures = new System.Collections.Concurrent.ConcurrentBag<string>();

        await Parallel.ForEachAsync(Enumerable.Range(0, 24), async (index, token) =>
        {
            try
            {
                var catalog = await client.GetAsync("/api/workshops", token);
                var detail = await client.GetAsync($"/api/workshops/{SeedId}", token);
                var missing = await client.GetAsync("/api/workshops/22222222-2222-2222-2222-222222222222", token);

                if (catalog.StatusCode != HttpStatusCode.OK || detail.StatusCode != HttpStatusCode.OK)
                {
                    failures.Add($"#{index} catálogo {(int)catalog.StatusCode} detalle {(int)detail.StatusCode}");
                    return;
                }

                if (missing.StatusCode != HttpStatusCode.NotFound)
                {
                    failures.Add($"#{index} inexistente {(int)missing.StatusCode}");
                }

                using var catalogDocument = JsonDocument.Parse(await catalog.Content.ReadAsStringAsync(token));
                using var detailDocument = JsonDocument.Parse(await detail.Content.ReadAsStringAsync(token));
                var catalogWorkshop = RequireSingle(catalogDocument.RootElement);
                if (catalogWorkshop.GetProperty("availableSeats").GetInt32() != 2
                    || detailDocument.RootElement.GetProperty("activeReservations").GetInt32() != 0
                    || detailDocument.RootElement.GetProperty("id").GetString() != SeedId)
                {
                    failures.Add($"#{index} el cupo o el id cambiaron");
                }
            }
            catch (Exception exception)
            {
                failures.Add($"#{index} {exception.GetType().Name}: {exception.Message}");
            }
        });

        Assert.True(failures.IsEmpty, string.Join(" | ", failures));
    }

    [Fact(DisplayName = "EDGE-14: la barra final o un segmento extra no provocan 500 ni crean un taller")]
    public async Task Edge14_BarraFinalYSegmentoExtra_NoCorrompenElCatalogo()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var trailing = await client.GetAsync("/api/workshops/");
        var trailingBody = await trailing.Content.ReadAsStringAsync();
        Assert.NotEqual(HttpStatusCode.InternalServerError, trailing.StatusCode);
        AssertNoExceptionLeak(trailingBody);
        if (trailing.StatusCode == HttpStatusCode.OK)
        {
            using var trailingDocument = JsonDocument.Parse(trailingBody);
            AssertInitialWorkshop(RequireSingle(trailingDocument.RootElement));
        }
        else
        {
            Assert.Equal(HttpStatusCode.NotFound, trailing.StatusCode);
        }

        var extra = await client.GetAsync($"/api/workshops/{SeedId}/reservas");
        Assert.Equal(HttpStatusCode.NotFound, extra.StatusCode);
        AssertNoExceptionLeak(await extra.Content.ReadAsStringAsync());

        using var catalog = await ReadOk(client, "/api/workshops");
        AssertInitialWorkshop(RequireSingle(catalog.RootElement));
    }

    [Fact(DisplayName = "EDGE-15: una petición cancelada no impide la lectura siguiente")]
    public async Task Edge15_PeticionCancelada_NoImpideLaLecturaSiguiente()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        using var source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetAsync("/api/workshops", source.Token));

        using var catalog = await ReadOk(client, "/api/workshops");
        AssertInitialWorkshop(RequireSingle(catalog.RootElement));
    }

    [Fact(DisplayName = "EDGE-16: pedir texto plano sigue devolviendo el catálogo JSON")]
    public async Task Edge16_AcceptTextoPlano_SigueDevolviendoJson()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.ParseAdd("text/plain");

        using var catalog = await ReadOk(client, "/api/workshops");
        AssertInitialWorkshop(RequireSingle(catalog.RootElement));
    }

    [Fact(DisplayName = "EDGE-17: el 404 de negocio no revela el taller ni una excepción")]
    public async Task Edge17_NotFoundDeNegocio_NoRevelaElTallerNiUnaExcepcion()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Accept.ParseAdd("text/html");

        var response = await client.GetAsync("/api/workshops/AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA");
        var body = await AssertWorkshopNotFound(response);
        Assert.DoesNotContain(SeedTitle, body, StringComparison.Ordinal);
        Assert.DoesNotContain(SeedId, body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "EDGE-18: OpenAPI en Development no documenta reservas y sí el catálogo")]
    public async Task Edge18_OpenApi_NoDocumentaReservas()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(body);
        var paths = document.RootElement.GetProperty("paths");
        Assert.True(paths.TryGetProperty("/health", out _));
        Assert.True(paths.TryGetProperty("/api/workshops", out _));

        var names = paths.EnumerateObject().Select(property => property.Name).ToArray();
        Assert.Contains(names, name => name.StartsWith("/api/workshops/", StringComparison.Ordinal));
        Assert.DoesNotContain(names, name =>
            name.Contains("reservation", StringComparison.OrdinalIgnoreCase)
            || name.Contains("reserva", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task AssertHealth(HttpClient client)
    {
        var response = await client.GetAsync("/health");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        using var document = JsonDocument.Parse(body);
        var names = document.RootElement.EnumerateObject().Select(property => property.Name).ToArray();
        Assert.Equal(["status"], names);
        Assert.Equal(JsonValueKind.String, document.RootElement.GetProperty("status").ValueKind);
        Assert.Equal("ok", document.RootElement.GetProperty("status").GetString());
    }

    private static async Task<string> AssertWorkshopNotFound(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        AssertNoExceptionLeak(body);

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        Assert.Equal(404, root.GetProperty("status").GetInt32());
        Assert.Equal("workshop_not_found", root.GetProperty("code").GetString());
        Assert.Equal("Recurso no encontrado", root.GetProperty("title").GetString());
        Assert.Equal("No se encontró el taller.", root.GetProperty("detail").GetString());
        return body;
    }

    private static async Task<JsonDocument> ReadOk(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        return JsonDocument.Parse(body);
    }

    private static JsonElement RequireSingle(JsonElement catalog)
    {
        Assert.Equal(JsonValueKind.Array, catalog.ValueKind);
        Assert.Equal(1, catalog.GetArrayLength());
        return catalog[0];
    }

    private static void AssertInitialWorkshop(JsonElement workshop)
    {
        Assert.Equal(SeedId, workshop.GetProperty("id").GetString());
        Assert.Equal(SeedTitle, workshop.GetProperty("title").GetString());
        Assert.Equal(2, workshop.GetProperty("capacity").GetInt32());
        Assert.Equal(0, workshop.GetProperty("activeReservations").GetInt32());
        Assert.Equal(2, workshop.GetProperty("availableSeats").GetInt32());
        Assert.Equal(
            workshop.GetProperty("capacity").GetInt32() - workshop.GetProperty("activeReservations").GetInt32(),
            workshop.GetProperty("availableSeats").GetInt32());
    }

    private static IEnumerable<string> ReadAllow(HttpResponseMessage response)
    {
        if (response.Content.Headers.Allow.Count > 0)
        {
            return response.Content.Headers.Allow;
        }

        if (response.Headers.TryGetValues("Allow", out var values))
        {
            return values;
        }

        return [];
    }

    private static void AssertNoExceptionLeak(string text)
    {
        Assert.DoesNotContain("exception", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stacktrace", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stack trace", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("at Talleres.Api", text, StringComparison.Ordinal);
    }

    private sealed class FrenchCultureStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            return app =>
            {
                app.Use(async (context, nextMiddleware) =>
                {
                    var previous = CultureInfo.CurrentCulture;
                    var previousUi = CultureInfo.CurrentUICulture;
                    var culture = CultureInfo.GetCultureInfo("fr-FR");
                    CultureInfo.CurrentCulture = culture;
                    CultureInfo.CurrentUICulture = culture;
                    context.Response.OnStarting(() =>
                    {
                        context.Response.Headers["X-Test-Culture"] = CultureInfo.CurrentCulture.Name;
                        return Task.CompletedTask;
                    });

                    try
                    {
                        await nextMiddleware(context);
                    }
                    finally
                    {
                        CultureInfo.CurrentCulture = previous;
                        CultureInfo.CurrentUICulture = previousUi;
                    }
                });
                next(app);
            };
        }
    }
}
