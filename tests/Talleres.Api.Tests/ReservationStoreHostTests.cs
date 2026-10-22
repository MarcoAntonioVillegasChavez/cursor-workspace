using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Talleres.Api.Features.Workshops;

namespace Talleres.Api.Tests;

public class ReservationStoreHostTests
{
    private const string SeedId = "11111111-1111-1111-1111-111111111111";

    [Fact(DisplayName = "STORE-12: el almacén es singleton dentro del host y la HTTP ve el mismo cupo")]
    public async Task Store12_SingletonDelHost_LaHttpObservaElMismoCupo()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var store = factory.Services.GetRequiredService<ReservationStore>();

        using (var scopeA = factory.Services.CreateScope())
        using (var scopeB = factory.Services.CreateScope())
        {
            Assert.Same(
                scopeA.ServiceProvider.GetRequiredService<ReservationStore>(),
                scopeB.ServiceProvider.GetRequiredService<ReservationStore>());
            Assert.Same(store, scopeA.ServiceProvider.GetRequiredService<ReservationStore>());
        }

        await AssertHttpSeats(client, activeReservations: 0, availableSeats: 2);

        ReservationStoreAccess.AddReservation(store, ReservationStore.SeedWorkshopId, isActive: true);
        Assert.Equal(1, store.GetWorkshops()[0].ActiveReservations);
        await AssertHttpSeats(client, activeReservations: 1, availableSeats: 1);

        ReservationStoreAccess.AddReservation(store, ReservationStore.SeedWorkshopId, isActive: false);
        ReservationStoreAccess.AddReservation(store, Guid.NewGuid(), isActive: true);
        await AssertHttpSeats(client, activeReservations: 1, availableSeats: 1);

        ReservationStoreAccess.AddReservation(store, ReservationStore.SeedWorkshopId, isActive: true);
        await AssertHttpSeats(client, activeReservations: 2, availableSeats: 0);
    }

    [Fact(DisplayName = "STORE-13: dos hosts no comparten el almacén ni el cupo")]
    public async Task Store13_DosHosts_NoCompartenElAlmacen()
    {
        using var firstFactory = new WebApplicationFactory<Program>();
        using var secondFactory = new WebApplicationFactory<Program>();
        using var firstClient = firstFactory.CreateClient();
        using var secondClient = secondFactory.CreateClient();

        var firstStore = firstFactory.Services.GetRequiredService<ReservationStore>();
        var secondStore = secondFactory.Services.GetRequiredService<ReservationStore>();
        Assert.NotSame(firstStore, secondStore);

        ReservationStoreAccess.AddReservation(firstStore, ReservationStore.SeedWorkshopId, isActive: true);

        await AssertHttpSeats(firstClient, activeReservations: 1, availableSeats: 1);
        await AssertHttpSeats(secondClient, activeReservations: 0, availableSeats: 2);
        Assert.Equal(0, secondStore.GetWorkshop(ReservationStore.SeedWorkshopId)!.ActiveReservations);
    }

    private static async Task AssertHttpSeats(HttpClient client, int activeReservations, int availableSeats)
    {
        using var catalog = await ReadOk(client, "/api/workshops");
        using var detail = await ReadOk(client, $"/api/workshops/{SeedId}");

        Assert.Equal(JsonValueKind.Array, catalog.RootElement.ValueKind);
        Assert.Equal(1, catalog.RootElement.GetArrayLength());
        AssertSeats(catalog.RootElement[0], activeReservations, availableSeats);
        Assert.Equal(JsonValueKind.Object, detail.RootElement.ValueKind);
        AssertSeats(detail.RootElement, activeReservations, availableSeats);
    }

    private static void AssertSeats(JsonElement workshop, int activeReservations, int availableSeats)
    {
        Assert.Equal(SeedId, workshop.GetProperty("id").GetString());
        Assert.Equal("Taller de Cursor", workshop.GetProperty("title").GetString());
        Assert.Equal(2, workshop.GetProperty("capacity").GetInt32());
        Assert.Equal(activeReservations, workshop.GetProperty("activeReservations").GetInt32());
        Assert.Equal(availableSeats, workshop.GetProperty("availableSeats").GetInt32());
        Assert.Equal(
            workshop.GetProperty("capacity").GetInt32() - workshop.GetProperty("activeReservations").GetInt32(),
            workshop.GetProperty("availableSeats").GetInt32());
    }

    private static async Task<JsonDocument> ReadOk(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        return JsonDocument.Parse(body);
    }
}
