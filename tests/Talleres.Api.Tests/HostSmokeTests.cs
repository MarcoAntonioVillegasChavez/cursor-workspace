using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace CursoCursor.Api.Tests;

public class HostSmokeTests
{
    [Fact(DisplayName = "E5: el host de pruebas construye un cliente")]
    public void ElHostDePruebasConstruyeUnCliente()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        Assert.NotNull(client);
    }
}
