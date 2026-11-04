using NutriLesiones.Api.Models;

namespace NutriLesiones.Api.Services;

/// <summary>
/// Método de Belén para convertir los requerimientos en intercambios (pasos de 0,5):
///  1. Cereales (altos en CHO, 30 g) = 1/3 del CHO requerido ÷ 30.
///  2. Vegetales y lácteos = límite inferior del rango DASH para esa energía; leguminosas = 1,5.
///  3. Carnes = (proteína requerida − la ya aportada) ÷ 11, hacia abajo; la mitad en "bajas en grasa" (hacia arriba) y el resto en "medias".
///  4. Grasas (altas, 12 g) = (grasa requerida − la ya aportada) ÷ 12.
///  5. Azúcares = 1 intercambio.
///  6. Frutas (medias en CHO, 20 g) = (CHO requerido − el ya aportado) ÷ 20. Si queda fuera del rango DASH de frutas,
///     se sube o baja el cereal en 0,5 y se repite desde el paso 2.
/// Además hay subgrupos con porciones FIJAS que siempre van (aguacate, cereales medios, frutas bajas en CHO) y los lácteos
/// se reparten mitad semidescremados / mitad descremados. Todo cuenta dentro de los totales antes de calcular carnes, grasas y frutas.
/// </summary>
public static class PorcionesService
{
    const double Paso = 0.5;
    const double Leguminosas = 1.5;
    const double AzucarPorciones = 1;
    const int MaxVueltas = 40;

    // Porciones fijas (editables; son parámetros, no vienen del libro): aguacate = 1/4 de unidad.
    static readonly (string grupo, string sub, double porciones)[] Fijos =
    {
        ("Grasas y frutos secos", "Medios en grasas", 1.0),
        ("Cereales", "Medios en CHO y bajos en grasas", 1.0),
        ("Cereales", "Medios en CHO y medios en grasas", 0.5),
        ("Frutas", "Bajas en CHO", 1.0),
    };

    static double Nearest(double x) => Math.Floor(x / Paso + 0.5) * Paso;
    static double Floor05(double x) => Math.Floor(x / Paso + 1e-9) * Paso;
    static double Ceil05(double x) => Math.Ceiling(x / Paso - 1e-9) * Paso;

    /// <summary>Rango DASH (mín, máx) de porciones para una energía: interpolado entre 2000 y 2600 kcal.</summary>
    static (int lo, int hi) RangoDash(double e, double l0, double h0, double l1, double h1)
    {
        double lo, hi;
        if (e <= 2000) { lo = l0 * e / 2000; hi = h0 * e / 2000; }
        else if (e >= 2600) { lo = l1; hi = h1; }
        else { var s = (e - 2000) / 600; lo = l0 + (l1 - l0) * s; hi = h0 + (h1 - h0) * s; }
        var loR = (int)Math.Floor(lo + 0.5);
        var hiR = (int)Math.Floor(hi + 0.5);
        return (loR, Math.Max(loR, hiR));
    }

    static GrupoIntercambio Buscar(List<GrupoIntercambio> gs, string grupo, string sub) =>
        gs.First(g => g.Grupo == grupo && g.Subgrupo == sub);

    public static List<GrupoIntercambio> Calcular(Requerimientos req)
    {
        var gs = GruposIntercambio.Crear();
        var fru = RangoDash(req.Kcal, 4, 5, 5, 6);
        var veg = RangoDash(req.Kcal, 4, 5, 5, 6);
        var lac = RangoDash(req.Kcal, 2, 3, 3, 3);

        var cer = Buscar(gs, "Cereales", "Altos en CHO y bajos en grasas");
        var vegetales = Buscar(gs, "Vegetales", "Bajos en CHO");
        var semi = Buscar(gs, "Lácteos", "Semidescremados");
        var desc = Buscar(gs, "Lácteos", "Descremados (bajos en grasas)");
        var legum = Buscar(gs, "Leguminosas", "Leguminosas");
        var bajas = Buscar(gs, "Carnes, pescados y huevos", "Bajos en grasas");
        var medias = Buscar(gs, "Carnes, pescados y huevos", "Medios en grasas");
        var grasas = Buscar(gs, "Grasas y frutos secos", "Altos en grasas");
        var azuc = Buscar(gs, GruposIntercambio.Azucares, GruposIntercambio.Azucares);
        var frutas = Buscar(gs, "Frutas", "Medias en CHO");

        double Total(Func<GrupoIntercambio, double> aporte) => gs.Sum(g => aporte(g) * g.Porciones);

        var cereal = Math.Max(0, Nearest(req.ChoG / 3 / 30));
        for (var vuelta = 0; vuelta < MaxVueltas; vuelta++)
        {
            foreach (var g in gs) g.Porciones = 0;
            foreach (var (grupo, sub, porc) in Fijos) Buscar(gs, grupo, sub).Porciones = porc;

            cer.Porciones = cereal;
            vegetales.Porciones = veg.lo;
            semi.Porciones = Nearest(lac.lo / 2.0);
            desc.Porciones = lac.lo - semi.Porciones;
            legum.Porciones = Leguminosas;
            azuc.Porciones = AzucarPorciones;

            var m = Math.Max(0, Floor05((req.ProtG - Total(g => g.Prot)) / 11));
            bajas.Porciones = Ceil05(m * 0.5);
            medias.Porciones = m - bajas.Porciones;

            grasas.Porciones = Math.Max(0, Nearest((req.GrasaG - Total(g => g.Grasa)) / 12));
            frutas.Porciones = Math.Max(0, Nearest((req.ChoG - Total(g => g.Cho)) / 20));

            if (frutas.Porciones > fru.hi) cereal += Paso;
            else if (frutas.Porciones < fru.lo) cereal = Math.Max(0, cereal - Paso);
            else break;
        }
        return gs;
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
}
