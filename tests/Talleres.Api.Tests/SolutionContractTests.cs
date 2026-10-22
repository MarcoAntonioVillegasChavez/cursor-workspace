using System.Text.Json;
using System.Xml.Linq;

namespace Talleres.Api.Tests;

public class SolutionContractTests
{
    [Fact(DisplayName = "PROY-01: la solución incluye la API y el proyecto de pruebas enlazado")]
    public void Proy01_Solucion_IncluyeApiYPruebas()
    {
        var root = RepoRoot();
        var solution = File.ReadAllText(Path.Combine(root, "CursoCursor.slnx"));

        Assert.Contains("src/Talleres.Api/Talleres.Api.csproj", solution, StringComparison.Ordinal);
        Assert.Contains("tests/Talleres.Api.Tests/Talleres.Api.Tests.csproj", solution, StringComparison.Ordinal);

        var api = XDocument.Load(Path.Combine(root, "src/Talleres.Api/Talleres.Api.csproj"));
        var tests = XDocument.Load(Path.Combine(root, "tests/Talleres.Api.Tests/Talleres.Api.Tests.csproj"));

        Assert.StartsWith("<Project Sdk=\"Microsoft.NET.Sdk.Web\">", File.ReadAllText(Path.Combine(root, "src/Talleres.Api/Talleres.Api.csproj")), StringComparison.Ordinal);
        Assert.StartsWith("<Project Sdk=\"Microsoft.NET.Sdk\">", File.ReadAllText(Path.Combine(root, "tests/Talleres.Api.Tests/Talleres.Api.Tests.csproj")), StringComparison.Ordinal);
        Assert.Equal("net10.0", SingleValue(api, "TargetFramework"));
        Assert.Equal("net10.0", SingleValue(tests, "TargetFramework"));
        Assert.Equal("enable", SingleValue(api, "Nullable"));
        Assert.Equal("enable", SingleValue(tests, "Nullable"));
        Assert.Equal("enable", SingleValue(api, "ImplicitUsings"));
        Assert.Equal("enable", SingleValue(tests, "ImplicitUsings"));
        Assert.Equal("10.0.13", PackageVersion(api, "Microsoft.AspNetCore.OpenApi"));
        Assert.Equal("10.0.13", PackageVersion(tests, "Microsoft.AspNetCore.Mvc.Testing"));
        Assert.Equal("17.14.1", PackageVersion(tests, "Microsoft.NET.Test.Sdk"));
        Assert.Equal("2.9.3", PackageVersion(tests, "xunit"));
        Assert.Equal("3.1.4", PackageVersion(tests, "xunit.runner.visualstudio"));
        Assert.Equal("6.0.4", PackageVersion(tests, "coverlet.collector"));
        Assert.Contains(tests.Descendants("Using"), element => element.Attribute("Include")?.Value == "Xunit");

        var reference = tests.Descendants("ProjectReference").Single().Attribute("Include")?.Value ?? string.Empty;
        Assert.EndsWith(
            "src/Talleres.Api/Talleres.Api.csproj",
            reference.Replace('\\', '/'),
            StringComparison.Ordinal);
    }

    [Fact(DisplayName = "PROY-07: el perfil http escucha en localhost:5080 sin abrir el navegador")]
    public void Proy07_PerfilHttp_EstaFijadoEnLaunchSettings()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            RepoRoot(),
            "src/Talleres.Api/Properties/launchSettings.json")));
        var profiles = document.RootElement.GetProperty("profiles");
        var profile = Assert.Single(profiles.EnumerateObject());
        Assert.Equal("http", profile.Name);
        var http = profile.Value;
        Assert.Equal("Project", http.GetProperty("commandName").GetString());
        Assert.Equal("http://localhost:5080", http.GetProperty("applicationUrl").GetString());
        Assert.False(http.GetProperty("launchBrowser").GetBoolean());
        Assert.Equal(
            "Development",
            http.GetProperty("environmentVariables").GetProperty("ASPNETCORE_ENVIRONMENT").GetString());
    }

    [Fact(DisplayName = "PROY-08: el SDK está fijado y los artefactos locales quedan ignorados")]
    public void Proy08_SdkYSecretos_SiguenElContrato()
    {
        var root = RepoRoot();
        using var sdk = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "global.json")));
        var sdkNode = sdk.RootElement.GetProperty("sdk");

        Assert.Equal("10.0.100", sdkNode.GetProperty("version").GetString());
        Assert.Equal("latestFeature", sdkNode.GetProperty("rollForward").GetString());
        Assert.False(sdkNode.GetProperty("allowPrerelease").GetBoolean());

        var ignore = File.ReadAllText(Path.Combine(root, ".gitignore"));
        Assert.Contains("bin/", ignore, StringComparison.Ordinal);
        Assert.Contains("obj/", ignore, StringComparison.Ordinal);
        Assert.Contains("TestResults/", ignore, StringComparison.Ordinal);
        Assert.Contains(".env", ignore, StringComparison.Ordinal);

        foreach (var relative in new[] { "src/Talleres.Api/appsettings.json", "src/Talleres.Api/appsettings.Development.json" })
        {
            var text = File.ReadAllText(Path.Combine(root, relative));
            Assert.DoesNotContain("password", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("secret", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("connectionstring", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("apikey", text, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string SingleValue(XDocument document, string elementName)
    {
        return document.Descendants(elementName).Single().Value;
    }

    private static string PackageVersion(XDocument document, string packageId)
    {
        var package = document.Descendants("PackageReference")
            .Single(element => element.Attribute("Include")?.Value == packageId);
        return package.Attribute("Version")?.Value ?? string.Empty;
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "CursoCursor.slnx")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("No se encontró la raíz del repositorio desde la salida de las pruebas.");
    }
}
