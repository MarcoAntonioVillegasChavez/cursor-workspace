namespace Talleres.Api.Features.Workshops;

public sealed class ReservationStore
{
    public static readonly Guid SeedWorkshopId = new("11111111-1111-1111-1111-111111111111");

    private readonly object _gate = new();
    private readonly Workshop _workshop;
    private readonly List<Reservation> _reservations;

    public ReservationStore()
    {
        _workshop = new Workshop(SeedWorkshopId, "Taller de Cursor", 2);
        _reservations = [];
    }

    public WorkshopResponse[] GetWorkshops()
    {
        lock (_gate)
        {
            return [CreateSnapshot()];
        }
    }

    public WorkshopResponse? GetWorkshop(Guid id)
    {
        lock (_gate)
        {
            if (id != _workshop.Id)
            {
                return null;
            }

            return CreateSnapshot();
        }
    }

    private WorkshopResponse CreateSnapshot()
    {
        var activeReservations = _reservations.Count(reservation =>
            reservation.WorkshopId == _workshop.Id && reservation.IsActive);
        var availableSeats = _workshop.Capacity - activeReservations;

        return new WorkshopResponse(
            _workshop.Id,
            _workshop.Title,
            _workshop.Capacity,
            activeReservations,
            availableSeats);
    }

    private sealed record Workshop(Guid Id, string Title, int Capacity);

    private sealed record Reservation(Guid WorkshopId, bool IsActive);
}
