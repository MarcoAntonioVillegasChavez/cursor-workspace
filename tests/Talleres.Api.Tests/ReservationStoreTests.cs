using System.Collections;
using System.Reflection;
using Talleres.Api.Features.Workshops;

namespace Talleres.Api.Tests;

public class ReservationStoreTests
{
    private static readonly Guid SeedId = ReservationStore.SeedWorkshopId;
    private const string SeedTitle = "Taller de Cursor";
    private const int SeedCapacity = 2;

    [Fact(DisplayName = "STORE-01: un almacén nuevo expone un taller con cupo completo y sin reservas")]
    public void Store01_AlmacenNuevo_ExponeElTallerInicial()
    {
        var store = new ReservationStore();

        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), SeedId);

        var workshops = store.GetWorkshops();
        var detail = store.GetWorkshop(SeedId);

        Assert.Single(workshops);
        Assert.NotNull(detail);
        Assert.Equal(workshops[0], detail);
        AssertSnapshot(detail, activeReservations: 0, availableSeats: SeedCapacity);
        Assert.Equal(typeof(WorkshopResponse[]), workshops.GetType());
    }

    [Fact(DisplayName = "STORE-02: un GUID vacío, máximo o aleatorio no corresponde a ningún taller")]
    public void Store02_GuidDesconocido_DevuelveNull()
    {
        var store = new ReservationStore();

        Assert.Null(store.GetWorkshop(Guid.Empty));
        Assert.Null(store.GetWorkshop(Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff")));
        Assert.Null(store.GetWorkshop(Guid.NewGuid()));
        Assert.NotNull(store.GetWorkshop(Guid.Parse(SeedId.ToString("D").ToUpperInvariant())));
    }

    [Fact(DisplayName = "STORE-03: cada lectura es una instantánea nueva y no cambia el cupo")]
    public void Store03_LecturasRepetidas_SonInstantaneasIndependientes()
    {
        var store = new ReservationStore();

        var first = store.GetWorkshops();
        var second = store.GetWorkshops();
        var firstDetail = store.GetWorkshop(SeedId);
        var secondDetail = store.GetWorkshop(SeedId);

        Assert.NotSame(first, second);
        Assert.NotSame(first[0], second[0]);
        Assert.NotSame(firstDetail, secondDetail);
        Assert.Equal(first[0], second[0]);
        Assert.Equal(firstDetail, secondDetail);

        first[0] = first[0] with
        {
            Title = "Taller alterado",
            Capacity = 99,
            ActiveReservations = 98,
            AvailableSeats = 1
        };

        var afterMutation = store.GetWorkshop(SeedId);
        Assert.NotNull(afterMutation);
        AssertSnapshot(afterMutation, activeReservations: 0, availableSeats: SeedCapacity);

        for (var i = 0; i < 20; i++)
        {
            Assert.Equal(0, store.GetWorkshops()[0].ActiveReservations);
            Assert.Equal(SeedCapacity, store.GetWorkshop(SeedId)!.AvailableSeats);
        }
    }

    [Fact(DisplayName = "STORE-04: una reserva activa reduce en uno los lugares disponibles")]
    public void Store04_ReservaActiva_ReduceElCupo()
    {
        var store = new ReservationStore();

        ReservationStoreAccess.AddReservation(store, SeedId, isActive: true);

        AssertSeats(store, activeReservations: 1, availableSeats: 1);
        Assert.Single(store.GetWorkshops());
        AssertSerializedWorkshopOmitsPersonalData(store);
    }

    [Fact(DisplayName = "STORE-05: las reservas inactivas no ocupan lugares")]
    public void Store05_ReservasInactivas_NoOcupanCupo()
    {
        var store = new ReservationStore();

        ReservationStoreAccess.AddReservation(store, SeedId, isActive: false);
        ReservationStoreAccess.AddReservation(store, SeedId, isActive: false);
        ReservationStoreAccess.AddReservation(store, SeedId, isActive: false);

        AssertSeats(store, activeReservations: 0, availableSeats: SeedCapacity);
    }

    [Fact(DisplayName = "STORE-06: una reserva activa de otro taller no ocupa cupo de este")]
    public void Store06_ReservaDeOtroTaller_NoOcupaCupo()
    {
        var store = new ReservationStore();

        ReservationStoreAccess.AddReservation(store, Guid.NewGuid(), isActive: true);
        ReservationStoreAccess.AddReservation(store, Guid.Empty, isActive: true);

        AssertSeats(store, activeReservations: 0, availableSeats: SeedCapacity);
    }

    [Fact(DisplayName = "STORE-07: al alcanzar la capacidad el cupo disponible queda en cero")]
    public void Store07_CapacidadExacta_DejaCeroLugares()
    {
        var store = new ReservationStore();

        ReservationStoreAccess.AddReservation(store, SeedId, isActive: true);
        ReservationStoreAccess.AddReservation(store, SeedId, isActive: true);

        AssertSeats(store, activeReservations: SeedCapacity, availableSeats: 0);
        Assert.True(store.GetWorkshop(SeedId)!.AvailableSeats >= 0);
    }

    [Fact(DisplayName = "STORE-08: solo cuentan las reservas activas del taller consultado")]
    public void Store08_MezclaDeReservas_CuentaSoloLasActivasDelTaller()
    {
        var store = new ReservationStore();

        ReservationStoreAccess.AddReservation(store, SeedId, isActive: true);
        ReservationStoreAccess.AddReservation(store, SeedId, isActive: false);
        ReservationStoreAccess.AddReservation(store, Guid.NewGuid(), isActive: true);
        ReservationStoreAccess.AddReservation(store, SeedId, isActive: true);
        ReservationStoreAccess.AddReservation(store, SeedId, isActive: false);

        AssertSeats(store, activeReservations: 2, availableSeats: 0);
        AssertSerializedWorkshopOmitsPersonalData(store);
    }

    [Fact(DisplayName = "STORE-09: dos almacenes no comparten las reservas mutables")]
    public void Store09_DosInstancias_NoCompartenReservas()
    {
        var first = new ReservationStore();
        var second = new ReservationStore();

        ReservationStoreAccess.AddReservation(first, SeedId, isActive: true);

        AssertSeats(first, activeReservations: 1, availableSeats: 1);
        AssertSeats(second, activeReservations: 0, availableSeats: SeedCapacity);
        Assert.Equal(SeedId, second.GetWorkshops()[0].Id);
    }

    [Fact(DisplayName = "STORE-10: lecturas concurrentes no se bloquean ni divergen", Timeout = 15000)]
    public async Task Store10_LecturasConcurrentes_SonCoherentes()
    {
        var store = new ReservationStore();
        ReservationStoreAccess.AddReservation(store, SeedId, isActive: true);
        ReservationStoreAccess.AddReservation(store, SeedId, isActive: false);

        var tasks = Enumerable.Range(0, 100).Select(_ => Task.Run(() =>
        {
            var list = store.GetWorkshops();
            var detail = store.GetWorkshop(SeedId);
            Assert.NotNull(detail);
            Assert.Single(list);
            Assert.Equal(list[0], detail);
            AssertSnapshot(detail, activeReservations: 1, availableSeats: 1);
            Assert.Null(store.GetWorkshop(Guid.Empty));
        })).ToArray();

        await Task.WhenAll(tasks);
    }

    [Fact(DisplayName = "STORE-11: la superficie pública no expone colecciones ni datos de participantes")]
    public void Store11_SuperficiePublica_NoExponeParticipantesNiColecciones()
    {
        var properties = typeof(ReservationStore).GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
        Assert.DoesNotContain(
            properties,
            property => property.PropertyType != typeof(string)
                && typeof(System.Collections.IEnumerable).IsAssignableFrom(property.PropertyType));

        var names = typeof(ReservationStore)
            .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Select(member => member.Name);

        Assert.DoesNotContain(names, name =>
            name.Contains("email", StringComparison.OrdinalIgnoreCase)
            || name.Contains("correo", StringComparison.OrdinalIgnoreCase)
            || name.Contains("participant", StringComparison.OrdinalIgnoreCase)
            || name.Contains("nombre", StringComparison.OrdinalIgnoreCase));
    }

    private static void AssertSeats(ReservationStore store, int activeReservations, int availableSeats)
    {
        var workshops = store.GetWorkshops();
        var detail = store.GetWorkshop(SeedId);

        Assert.Single(workshops);
        Assert.NotNull(detail);
        Assert.Equal(workshops[0], detail);
        AssertSnapshot(detail, activeReservations, availableSeats);
    }

    private static void AssertSnapshot(WorkshopResponse workshop, int activeReservations, int availableSeats)
    {
        Assert.Equal(SeedId, workshop.Id);
        Assert.Equal(SeedTitle, workshop.Title);
        Assert.Equal(SeedCapacity, workshop.Capacity);
        Assert.Equal(activeReservations, workshop.ActiveReservations);
        Assert.Equal(availableSeats, workshop.AvailableSeats);
        Assert.Equal(workshop.Capacity - workshop.ActiveReservations, workshop.AvailableSeats);
        Assert.InRange(workshop.ActiveReservations, 0, workshop.Capacity);
    }

    private static void AssertSerializedWorkshopOmitsPersonalData(ReservationStore store)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(store.GetWorkshop(SeedId));
        Assert.DoesNotContain("email", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("correo", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@", json, StringComparison.Ordinal);
        Assert.DoesNotContain("participant", json, StringComparison.OrdinalIgnoreCase);
    }
}

internal static class ReservationStoreAccess
{
    // No hay un método público para reservar: la spec de catálogo deja las escrituras
    // para una etapa posterior, pero prohíbe dejar el cupo codificado como constante.
    public static void AddReservation(ReservationStore store, Guid workshopId, bool isActive)
    {
        var field = typeof(ReservationStore).GetField("_reservations", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.True(field is not null, "El almacén ya no guarda las reservas en el campo de instancia _reservations.");

        var list = field.GetValue(store) as IList;
        Assert.True(list is not null, "La colección de reservas no está materializada en una lista de instancia.");

        var reservationType = typeof(ReservationStore).GetNestedType("Reservation", BindingFlags.NonPublic);
        Assert.True(reservationType is not null, "No se encontró el registro interno Reservation.");

        var reservation = Activator.CreateInstance(
            reservationType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [workshopId, isActive],
            culture: null);
        Assert.True(reservation is not null, "No se pudo crear una reserva interna para la prueba.");

        list.Add(reservation);
    }
}
