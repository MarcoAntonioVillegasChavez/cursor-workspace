using System.Reflection;
using Talleres.Api.Features.Workshops;

namespace Talleres.Api.Tests.Support;

internal static class ReservationStoreSeeder
{
    public static void Add(ReservationStore store, Guid workshopId, bool isActive, int count = 1)
    {
        ArgumentNullException.ThrowIfNull(store);
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        var reservations = typeof(ReservationStore)
            .GetField("_reservations", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(store)
            ?? throw new InvalidOperationException("No se encontró la lista interna de reservas.");

        var reservationType = typeof(ReservationStore).GetNestedType("Reservation", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("No se encontró el tipo interno de reserva.");
        var add = reservations.GetType().GetMethod("Add")
            ?? throw new InvalidOperationException("La lista interna no expone Add.");

        for (var index = 0; index < count; index++)
        {
            var reservation = Activator.CreateInstance(reservationType, workshopId, isActive)
                ?? throw new InvalidOperationException("No se pudo crear una reserva de prueba.");
            add.Invoke(reservations, [reservation]);
        }
    }
}
