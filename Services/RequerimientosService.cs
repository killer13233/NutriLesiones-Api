using NutriLesiones.Api.Models;

namespace NutriLesiones.Api.Services;

public static class RequerimientosService
{
    public static readonly string[] Fases = { "inflamatoria", "proliferativa", "remodelacion" };

    /// <summary>Acepta variantes de escritura ("inflmatoria", "remodalacion", "Remodelación"...).</summary>
    public static string? ParsearFase(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var n = Texto.Norm(texto);
        if (n.StartsWith("inflam") || n.StartsWith("inflm")) return "inflamatoria";
        if (n.StartsWith("prolif")) return "proliferativa";
        if (n.StartsWith("remod")) return "remodelacion";
        return null;
    }

    /// <summary>
    /// kcal = peso × kcal/kg de la fase; cada macro = kcal × % / (4 para CHO y proteína, 9 para grasa).
    /// </summary>
    public static Requerimientos Calcular(double peso, string fase)
    {
        var (kcalKg, prot, cho, grasa) = fase switch
        {
            "inflamatoria" or "proliferativa" => (30, 20, 50, 30),
            "remodelacion" => (40, 20, 55, 25),
            _ => throw new ArgumentException("Fase no reconocida: " + fase)
        };
        var kcal = peso * kcalKg;
        return new Requerimientos(kcal, kcal * cho / 100 / 4, kcal * prot / 100 / 4, kcal * grasa / 100 / 9,
            kcalKg, prot, cho, grasa);
    }
}
