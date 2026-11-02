using System.Text.Json;
using NutriLesiones.Api.Models;

namespace NutriLesiones.Api.Services;

public sealed record ReglaIncompatible(string A, string B);

/// <summary>Carga el catálogo de alimentos y las reglas de compatibilidad desde Data/*.json.</summary>
public sealed class CatalogoService
{
    public static readonly string[] ComidaIds = { "Desayuno", "MM", "Almuerzo", "MT", "Cena" };
    public static readonly string[] ComidaNombres = { "Desayuno", "Media mañana", "Almuerzo", "Media tarde", "Cena" };
    public static readonly string[] ComidaHorarios = { "07:00–08:00", "10:00–10:30", "12:30–13:30", "16:00–16:30", "19:00–20:00" };

    public IReadOnlyList<Alimento> Alimentos { get; }
    public IReadOnlyList<ReglaIncompatible> Reglas { get; }

    public CatalogoService(IWebHostEnvironment env)
    {
        var opciones = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var dir = Path.Combine(AppContext.BaseDirectory, "Data");

        Alimentos = JsonSerializer.Deserialize<List<Alimento>>(
            File.ReadAllText(Path.Combine(dir, "alimentos.json")), opciones) ?? new();

        var reglas = new List<ReglaIncompatible>();
        var rutaReglas = Path.Combine(dir, "reglas.json");
        if (File.Exists(rutaReglas))
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(rutaReglas));
            if (doc.RootElement.TryGetProperty("incompatibles", out var lista))
                foreach (var r in lista.EnumerateArray())
                    reglas.Add(new ReglaIncompatible(
                        Texto.Norm(r.GetProperty("a").GetString() ?? ""),
                        Texto.Norm(r.GetProperty("b").GetString() ?? "")));
        }
        Reglas = reglas;

        // Si falta la foto de un alimento, basta guardar wwwroot/imagenes/<id>.jpg (o .png) y el programa la usa sola.
        var carpetaFotos = Path.Combine(env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot"), "imagenes");
        foreach (var a in Alimentos.Where(x => x.Foto == null || x.FotoReferencia != null))
            foreach (var ext in new[] { ".jpg", ".jpeg", ".png" })
                if (File.Exists(Path.Combine(carpetaFotos, a.Id + ext))) { a.Foto = "imagenes/" + a.Id + ext; a.FotoReferencia = null; break; }
    }

    public List<Alimento> Opciones(string grupo, string subgrupo, string comidaId) =>
        Alimentos.Where(a => a.Grupo == grupo && a.Subgrupo == subgrupo && a.Comidas.Contains(comidaId)).ToList();
}
