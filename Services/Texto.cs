using System.Text;

namespace NutriLesiones.Api.Services;

public static class Texto
{
    static readonly Dictionary<char, char> Mapa = new()
    {
        ['á'] = 'a', ['é'] = 'e', ['í'] = 'i', ['ó'] = 'o', ['ú'] = 'u', ['ü'] = 'u', ['ñ'] = 'n'
    };

    /// <summary>Minúsculas y sin tildes (para comparar textos).</summary>
    public static string Norm(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s.ToLowerInvariant()) sb.Append(Mapa.TryGetValue(ch, out var m) ? m : ch);
        return sb.ToString();
    }

    /// <summary>Primera palabra del nombre ("Arroz blanco..." -> "arroz"); sirve para no repetir el mismo alimento en una comida.</summary>
    public static string Familia(string nombre)
    {
        var n = Norm(nombre);
        var i = n.IndexOfAny(new[] { ',', ' ' });
        var primera = i < 0 ? n : n[..i];
        return primera is "sambo" or "zapallo" or "zuquini" ? "calabaza" : primera; // evita 2 calabazas en la misma comida
    }
}
