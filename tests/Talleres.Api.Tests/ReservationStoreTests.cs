using Talleres.Api.Features.Workshops;
using Talleres.Api.Tests.Support;

namespace Talleres.Api.Tests;

public class ReservationStoreTests
{
    private static readonly Guid OtroTallerId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact(DisplayName = "STORE-01: un almacén nuevo publica el taller semilla con cupo completo")]
    public void AlmacenNuevo_PublicaElTallerSemillaConCupoCompleto()
    {
        var store = new ReservationStore();

        var catalogo = store.GetWorkshops();
        var detalle = store.GetWorkshop(ReservationStore.SeedWorkshopId);

        Assert.Equal(ReservationStore.SeedWorkshopId, Guid.Parse("11111111-1111-1111-1111-111111111111"));
        Assert.Single(catalogo);
        AssertInitial(catalogo[0]);
        Assert.NotNull(detalle);
        AssertSameWorkshop(catalogo[0], detalle);
    }

    [Theory(DisplayName = "STORE-02: un identificador vacío, ajeno o extremo no devuelve taller")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("22222222-2222-2222-2222-222222222222")]
    [InlineData("ffffffff-ffff-ffff-ffff-ffffffffffff")]
    public void IdentificadorDesconocido_DevuelveNull(string rawId)
    {
        var store = new ReservationStore();

        var detalle = store.GetWorkshop(Guid.Parse(rawId));

        Assert.Null(detalle);
        AssertInitial(Assert.Single(store.GetWorkshops()));
    }

    [Fact(DisplayName = "STORE-03: dos almacenes no comparten reservas")]
    public void DosAlmacenes_NoCompartenReservas()
    {
        var primero = new ReservationStore();
        var segundo = new ReservationStore();
        ReservationStoreSeeder.Add(primero, ReservationStore.SeedWorkshopId, isActive: true);

        Assert.Equal(1, primero.GetWorkshop(ReservationStore.SeedWorkshopId)!.ActiveReservations);
        Assert.Equal(1, primero.GetWorkshop(ReservationStore.SeedWorkshopId)!.AvailableSeats);
        AssertInitial(Assert.Single(segundo.GetWorkshops()));
    }

    [Fact(DisplayName = "STORE-04: mutar la instantánea devuelta no cambia la siguiente lectura")]
    public void MutarLaInstantanea_NoCambiaLaSiguienteLectura()
    {
        var store = new ReservationStore();
        var primera = store.GetWorkshops();
        var segunda = store.GetWorkshops();

        Assert.NotSame(primera, segunda);
        Assert.NotSame(primera[0], segunda[0]);

        primera[0] = new WorkshopResponse(Guid.Empty, "mutado", 99, 7, -1);

        AssertInitial(store.GetWorkshop(ReservationStore.SeedWorkshopId)!);
        AssertInitial(Assert.Single(store.GetWorkshops()));
    }

    [Fact(DisplayName = "STORE-05: solo las reservas activas del taller semilla consumen cupo")]
    public void Cupo_IgnoraInactivasYLasDeOtroTaller()
    {
        var store = new ReservationStore();
        ReservationStoreSeeder.Add(store, ReservationStore.SeedWorkshopId, isActive: false, count: 4);
        ReservationStoreSeeder.Add(store, OtroTallerId, isActive: true, count: 3);

        var antes = store.GetWorkshop(ReservationStore.SeedWorkshopId)!;
        AssertInitial(antes);

        ReservationStoreSeeder.Add(store, ReservationStore.SeedWorkshopId, isActive: true, count: 2);
        var lleno = store.GetWorkshop(ReservationStore.SeedWorkshopId)!;
        var catalogo = Assert.Single(store.GetWorkshops());

        Assert.Equal(2, lleno.ActiveReservations);
        Assert.Equal(0, lleno.AvailableSeats);
        AssertSameWorkshop(catalogo, lleno);
    }

    [Theory(DisplayName = "STORE-06: los lugares disponibles son la capacidad menos las reservas activas")]
    [InlineData(0, 2)]
    [InlineData(1, 1)]
    [InlineData(2, 0)]
    [InlineData(3, -1)]
    public void LugaresDisponibles_SiguenLaFormulaDelContrato(int activas, int esperadas)
    {
        var store = new ReservationStore();
        ReservationStoreSeeder.Add(store, ReservationStore.SeedWorkshopId, isActive: true, count: activas);

        var detalle = store.GetWorkshop(ReservationStore.SeedWorkshopId)!;
        var catalogo = Assert.Single(store.GetWorkshops());

        Assert.Equal(2, detalle.Capacity);
        Assert.Equal(activas, detalle.ActiveReservations);
        Assert.Equal(esperadas, detalle.AvailableSeats);
        Assert.Equal(detalle.Capacity - detalle.ActiveReservations, detalle.AvailableSeats);
        AssertSameWorkshop(catalogo, detalle);
    }

    [Fact(DisplayName = "STORE-07: lecturas simultáneas del catálogo conservan el mismo cupo")]
    public async Task LecturasSimultaneas_ConservanElMismoCupo()
    {
        var store = new ReservationStore();
        ReservationStoreSeeder.Add(store, ReservationStore.SeedWorkshopId, isActive: true);

        var lecturas = await Task.WhenAll(
            Enumerable.Range(0, 64).Select(_ => Task.Run(() => store.GetWorkshops()[0])));

        Assert.All(lecturas, taller =>
        {
            Assert.Equal(ReservationStore.SeedWorkshopId, taller.Id);
            Assert.Equal(1, taller.ActiveReservations);
            Assert.Equal(1, taller.AvailableSeats);
        });
    }

    private static void AssertInitial(WorkshopResponse taller)
    {
        Assert.Equal(ReservationStore.SeedWorkshopId, taller.Id);
        Assert.Equal("Taller de Cursor", taller.Title);
        Assert.Equal(2, taller.Capacity);
        Assert.Equal(0, taller.ActiveReservations);
        Assert.Equal(2, taller.AvailableSeats);
    }

    private static void AssertSameWorkshop(WorkshopResponse left, WorkshopResponse right)
    {
        Assert.Equal(left.Id, right.Id);
        Assert.Equal(left.Title, right.Title);
        Assert.Equal(left.Capacity, right.Capacity);
        Assert.Equal(left.ActiveReservations, right.ActiveReservations);
        Assert.Equal(left.AvailableSeats, right.AvailableSeats);
    }
}
