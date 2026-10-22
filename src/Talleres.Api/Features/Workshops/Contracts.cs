using System.Text.Json.Serialization;

namespace Talleres.Api.Features.Workshops;

public sealed record WorkshopResponse(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("capacity")] int Capacity,
    [property: JsonPropertyName("activeReservations")] int ActiveReservations,
    [property: JsonPropertyName("availableSeats")] int AvailableSeats);
