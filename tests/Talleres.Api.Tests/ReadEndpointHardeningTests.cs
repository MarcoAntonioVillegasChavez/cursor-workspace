using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Talleres.Api.Features.Workshops;

namespace Talleres.Api.Tests;

public class ReadEndpointHardeningTests
{
    [Theory(DisplayName = "PROY-EDGE-01: un método no permitido responde 405 ProblemDetails sin traza")]
    [InlineData("POST", "/health")]
    [InlineData("POST", "/api/workshops")]
    [InlineData("PUT", "/api/workshops/11111111-1111-1111-1111-111111111111")]
    [InlineData("DELETE", "/api/workshops/11111111-1111-1111-1111-111111111111")]
    public async Task MetodoNoPermitido_Devuelve405SinTraza(string method, string path)
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var request = new HttpRequestMessage(new HttpMethod(method), path)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json"),
        };
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        AssertMediaTypeStartsWith(response, "application/problem+json");
        AssertNoExcepcionExpuesta(body);

        using var document = JsonDocument.Parse(body);
        Assert.Equal(405, document.RootElement.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("title").GetString()));
        Assert.Contains("GET", ReadAllow(response));
    }

    [Theory(DisplayName = "PROY-EDGE-02: fuera de Development no se publica OpenAPI y el catálogo sigue disponible")]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task FueraDeDevelopment_OcultaOpenApiYConservaElCatalogo(string environment)
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
        });
        using var client = factory.CreateClient();

        var openApi = await client.GetAsync("/openapi/v1.json");
        var openApiBody = await openApi.Content.ReadAsStringAsync();
        var health = await client.GetAsync("/health");
        var catalogo = await client.GetAsync("/api/workshops");

        Assert.Equal(HttpStatusCode.NotFound, openApi.StatusCode);
        AssertMediaTypeStartsWith(openApi, "application/problem+json");
        AssertNoExcepcionExpuesta(openApiBody);
        Assert.Equal(404, JsonDocument.Parse(openApiBody).RootElement.GetProperty("status").GetInt32());

        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        using var healthDocument = JsonDocument.Parse(await health.Content.ReadAsStringAsync());
        Assert.Equal("ok", healthDocument.RootElement.GetProperty("status").GetString());
        Assert.Single(healthDocument.RootElement.EnumerateObject());

        Assert.Equal(HttpStatusCode.OK, catalogo.StatusCode);
        using var catalogDocument = JsonDocument.Parse(await catalogo.Content.ReadAsStringAsync());
        Assert.Equal(1, catalogDocument.RootElement.GetArrayLength());
    }

    [Fact(DisplayName = "PROY-EDGE-03: OpenAPI describe exactamente los cinco campos del taller y ninguna persona")]
    public async Task OpenApi_DescribeSoloLosCamposDelTaller()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));

        var schema = document.RootElement
            .GetProperty("components")
            .GetProperty("schemas")
            .GetProperty("WorkshopResponse");
        var required = schema.GetProperty("required").EnumerateArray().Select(item => item.GetString()!).ToArray();
        var properties = schema.GetProperty("properties").EnumerateObject().Select(property => property.Name).ToArray();
        string[] expected = ["id", "title", "capacity", "activeReservations", "availableSeats"];

        Assert.Equal(expected, required);
        Assert.Equal(expected, properties);
        Assert.Equal("uuid", schema.GetProperty("properties").GetProperty("id").GetProperty("format").GetString());

        var serialized = document.RootElement.GetRawText();
        Assert.DoesNotContain("email", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("correo", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("participant", serialized, StringComparison.OrdinalIgnoreCase);
    }

    [Theory(DisplayName = "PROY-EDGE-04: un fallo interno responde 500 ProblemDetails sin la excepción")]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task FalloInterno_NoExponeLaExcepcion(string environment)
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
        });
        using var client = factory.CreateClient();
        var store = factory.Services.GetRequiredService<ReservationStore>();
        typeof(ReservationStore)
            .GetField("_workshop", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(store, null);

        var response = await client.GetAsync("/api/workshops");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        AssertMediaTypeStartsWith(response, "application/problem+json");
        AssertNoExcepcionExpuesta(body);
        Assert.DoesNotContain("NullReference", body, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(body);
        Assert.Equal(500, document.RootElement.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("title").GetString()));
    }

    [Fact(DisplayName = "PROY-EDGE-05: la validación de futuros DTO queda registrada en el host")]
    public void Validacion_QuedaRegistradaEnElHost()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var _ = factory.CreateClient();
        var optionsType = Type.GetType(
            "Microsoft.Extensions.Validation.ValidationOptions, Microsoft.Extensions.Validation",
            throwOnError: true)!;
        var options = factory.Services.GetService(typeof(Microsoft.Extensions.Options.IOptions<>).MakeGenericType(optionsType));

        Assert.NotNull(options);
    }

    [Fact(DisplayName = "PROY-EDGE-06: pedir XML sigue recibiendo el catálogo JSON")]
    public async Task AcceptXml_RecibeElCatalogoJson()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/workshops");
        request.Headers.TryAddWithoutValidation("Accept", "application/xml");

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertMediaTypeStartsWith(response, "application/json");
        using var document = JsonDocument.Parse(body);
        Assert.Equal(JsonValueKind.Array, document.RootElement.ValueKind);
        Assert.Equal(1, document.RootElement.GetArrayLength());
    }

    private static IReadOnlyCollection<string> ReadAllow(HttpResponseMessage response)
    {
        if (response.Content.Headers.Allow.Count > 0)
        {
            return response.Content.Headers.Allow.ToArray();
        }

        return response.Headers.TryGetValues("Allow", out var values)
            ? values.ToArray()
            : [];
    }

    private static void AssertMediaTypeStartsWith(HttpResponseMessage response, string mediaType)
    {
        var contentType = response.Content.Headers.ContentType?.ToString() ?? string.Empty;
        Assert.StartsWith(mediaType, contentType, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertNoExcepcionExpuesta(string text)
    {
        Assert.DoesNotContain("exception", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stacktrace", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stack trace", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(".cs:line", text, StringComparison.OrdinalIgnoreCase);
    }
}
