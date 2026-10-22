namespace Talleres.Api.Features.Workshops;

public static class WorkshopEndpoints
{
    public static RouteGroupBuilder MapWorkshopEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/workshops");

        group.MapGet("", (ReservationStore store) => Results.Ok(store.GetWorkshops()))
            .Produces<WorkshopResponse[]>(StatusCodes.Status200OK);

        group.MapGet("/{id:guid}", (Guid id, ReservationStore store) =>
        {
            var workshop = store.GetWorkshop(id);
            if (workshop is null)
            {
                return Results.Problem(
                    title: "Recurso no encontrado",
                    detail: "No se encontró el taller.",
                    statusCode: StatusCodes.Status404NotFound,
                    extensions: new Dictionary<string, object?>
                    {
                        ["code"] = "workshop_not_found"
                    });
            }

            return Results.Ok(workshop);
        })
        .WithName("GetWorkshop")
        .Produces<WorkshopResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }
}
