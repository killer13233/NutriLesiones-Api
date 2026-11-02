using NutriLesiones.Api.Models;

namespace NutriLesiones.Api.Services;

/// <summary>
/// Algoritmo greedy dinámico: en cada paso toma el nutriente más atrasado (menor % de adecuación)
/// y le suma media porción al subgrupo que deje los 4 nutrientes más cerca de 100 %,
/// sin que ninguno pase de 110 %. Termina cuando los 4 llegan a 90 % o más.
/// </summary>
public static class PorcionesService
{
    const double Paso = 0.5;
    const double Inferior = 90;
    const double Superior = 110;
    const double ToleranciaEmpate = 1.10;
    const int MaxIteraciones = 300;

    public static List<GrupoIntercambio> Calcular(Requerimientos req)
    {
        var grupos = GruposIntercambio.Crear();
        AsignarBase(grupos, req.Kcal);

        for (var i = 0; i < MaxIteraciones; i++)
        {
            var ad = Adecuaciones(grupos, req);
            if (ad.All(a => a >= Inferior)) break;

            var atrasado = Array.IndexOf(ad, ad.Min());
            var mejor = MejorGrupo(grupos, req, atrasado);
            if (mejor == null) break; // ningún subgrupo puede subir sin pasarse de 110 %
            mejor.Porciones += Paso;
        }
        return grupos;
    }

    /// <summary>Porciones base: frutas, vegetales y lácteos fijos según calorías; leguminosas mínimo 1.</summary>
    static void AsignarBase(List<GrupoIntercambio> grupos, double kcal)
    {
        var (frutas, vegetales, lacteos) = kcal <= 2000 ? (4.0, 4.0, 2.0) : (5.0, 5.0, 3.0); // 2600+ kcal: provisional
        foreach (var g in grupos)
        {
            g.Porciones = (g.Grupo, g.Subgrupo) switch
            {
                ("Frutas", "Medias en CHO") => frutas,
                ("Vegetales", _) => vegetales,
                ("Lácteos", "Semidescremados") => lacteos,
                ("Leguminosas", _) => 1,
                _ => 0
            };
        }
    }

    /// <summary>[CHO %, Proteína %, Grasa %, Kcal %] del total acumulado frente al requerimiento.</summary>
    public static double[] Adecuaciones(IEnumerable<GrupoIntercambio> grupos, Requerimientos r)
    {
        double cho = 0, prot = 0, grasa = 0, kcal = 0;
        foreach (var g in grupos)
        {
            cho += g.Cho * g.Porciones; prot += g.Prot * g.Porciones;
            grasa += g.Grasa * g.Porciones; kcal += g.Kcal * g.Porciones;
        }
        return new[] { cho * 100 / r.ChoG, prot * 100 / r.ProtG, grasa * 100 / r.GrasaG, kcal * 100 / r.Kcal };
    }

    static GrupoIntercambio? MejorGrupo(List<GrupoIntercambio> grupos, Requerimientos r, int nutriente)
    {
        double cho = grupos.Sum(g => g.Cho * g.Porciones), prot = grupos.Sum(g => g.Prot * g.Porciones);
        double grasa = grupos.Sum(g => g.Grasa * g.Porciones), kcal = grupos.Sum(g => g.Kcal * g.Porciones);

        var candidatos = new List<(GrupoIntercambio g, double puntaje)>();
        var mejorPuntaje = double.MaxValue;

        foreach (var g in grupos)
        {
            if (!g.Ajustable || g.Porciones + Paso > g.Max + 1e-9) continue;
            if (g.Aporte(nutriente) <= 0) continue;

            var aCho = (cho + g.Cho * Paso) * 100 / r.ChoG;
            var aProt = (prot + g.Prot * Paso) * 100 / r.ProtG;
            var aGrasa = (grasa + g.Grasa * Paso) * 100 / r.GrasaG;
            var aKcal = (kcal + g.Kcal * Paso) * 100 / r.Kcal;
            if (aCho > Superior || aProt > Superior || aGrasa > Superior || aKcal > Superior) continue;

            var puntaje = Math.Pow(100 - aCho, 2) + Math.Pow(100 - aProt, 2)
                        + Math.Pow(100 - aGrasa, 2) + Math.Pow(100 - aKcal, 2);
            candidatos.Add((g, puntaje));
            if (puntaje < mejorPuntaje) mejorPuntaje = puntaje;
        }

        // Solo cuentan los subgrupos que de verdad aportan al nutriente atrasado (al menos la mitad que el que más aporta);
        // si no, un subgrupo con aporte mínimo (ej. aguacate para carbohidratos) se colaba por ir con 0 porciones.
        if (candidatos.Count > 0)
        {
            var maxAporte = candidatos.Max(c => c.g.Aporte(nutriente));
            candidatos = candidatos.Where(c => c.g.Aporte(nutriente) >= 0.5 * maxAporte).ToList();
            mejorPuntaje = candidatos.Min(c => c.puntaje);
        }

        // Entre los casi empatados con el mejor: primero los preferentes (los opcionales solo si no hay preferente
        // cerca) y, entre esos, el que lleva menos porciones (así se reparten las carnes, etc.)
        var cercanos = candidatos.Where(c => c.puntaje <= mejorPuntaje * ToleranciaEmpate + 1).Select(c => c.g).ToList();
        var preferentes = cercanos.Where(g => g.Prioridad == Prioridad.Preferente).ToList();
        var lista = preferentes.Count > 0 ? preferentes : cercanos;
        GrupoIntercambio? elegido = null;
        foreach (var g in lista)
            if (elegido == null || g.Porciones < elegido.Porciones) elegido = g;
        return elegido;
    }
}
