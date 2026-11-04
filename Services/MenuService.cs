using NutriLesiones.Api.Models;

namespace NutriLesiones.Api.Services;

/// <summary>
/// Arma el menú del día: para cada comida elige alimentos reales del catálogo (con foto del libro)
/// que sean del subgrupo correcto, permitidos en esa comida y variados entre comidas y entre llamadas.
/// </summary>
public sealed class MenuService
{
    // Orden en que se muestran los subgrupos dentro de una comida
    const string GrupoCarnes = "Carnes, pescados y huevos";
    static readonly string[] OrdenGrupos = { "Lácteos", "Cereales", "Carnes, pescados y huevos", "Leguminosas", "Vegetales", "Frutas", "Grasas y frutos secos", GruposIntercambio.Azucares };

    readonly CatalogoService _catalogo;
    public MenuService(CatalogoService catalogo) => _catalogo = catalogo;

    public PlanResponse CrearPlan(double peso, string fase, string? leccion, int semilla)
    {
        var req = RequerimientosService.Calcular(peso, fase);
        var grupos = PorcionesService.Calcular(req);
        var ad = PorcionesService.Adecuaciones(grupos, req);
        var enRango = ad.All(a => a >= 90 && a <= 110);

        var reparto = grupos.ToDictionary(g => g, g => DistribuidorService.Distribuir(g, _catalogo));
        var rng = new Random(semilla);
        var usados = new Dictionary<string, int>(); // cuántas veces se usó cada alimento en el día
        var usadasFamilias = new Dictionary<string, int>(); // cuántas veces se usó cada fuente proteica (pollo, res, pescado, huevo...)
        var advertencias = new List<string>();
        var comidas = new List<ComidaDto>();

        for (var m = 0; m < 5; m++)
        {
            var items = new List<(Alimento a, double porc, GrupoIntercambio g)>();
            var gruposComida = grupos
                .Where(g => reparto[g][m] > 0)
                .OrderBy(g => Array.IndexOf(OrdenGrupos, g.Grupo)).ThenBy(g => g.Subgrupo);

            var listaGrupos = gruposComida.ToList();
            // una sola fuente proteica por comida (no pollo + res en el mismo plato)
            var familiaProteica = ElegirFamiliaProteica(
                listaGrupos.Where(g => g.Grupo == GrupoCarnes).ToList(), m, usadasFamilias, rng);

            foreach (var g in listaGrupos)
            {
                var porciones = reparto[g][m];
                var pool = _catalogo.Opciones(g.Grupo, g.Subgrupo, CatalogoService.ComidaIds[m]);
                if (g.Grupo == GrupoCarnes && familiaProteica != null)
                {
                    var deLaFamilia = pool.Where(a => Texto.Familia(a.Nombre) == familiaProteica).ToList();
                    if (deLaFamilia.Count > 0) pool = deLaFamilia;
                }
                if (pool.Count == 0) continue;

                // cuántas porciones por alimento antes de agregar otro alimento al plato
                var porAlimento = g.Grupo switch
                {
                    "Vegetales" => 1.0,                 // varias verduras, una por porción
                    "Cereales" => 2.5,
                    "Leguminosas" or "Lácteos" => 2.0,
                    GrupoCarnes => 3.0,                 // una sola carne por subgrupo
                    _ => 1.5
                };
                var unidades = (int)Math.Round(porciones / 0.5);
                var n = Math.Min(Math.Min(pool.Count, unidades), Math.Max(1, (int)Math.Ceiling(porciones / porAlimento)));
                var elegidos = new List<Alimento>();
                for (var k = 0; k < n; k++)
                {
                    var siguiente = Elegir(pool, elegidos, items.Select(i => i.a), usados, rng);
                    if (siguiente == null) break;
                    elegidos.Add(siguiente);
                    usados[siguiente.Id] = usados.GetValueOrDefault(siguiente.Id) + 1;
                }

                // reparte las porciones entre los alimentos elegidos (los primeros reciben la unidad extra)
                var baseU = unidades / elegidos.Count;
                var extra = unidades % elegidos.Count;
                for (var k = 0; k < elegidos.Count; k++)
                    items.Add((elegidos[k], (baseU + (k < extra ? 1 : 0)) * 0.5, g));
            }

            // avisa si quedó en la misma comida algo que la regla de compatibilidad no recomienda
            var platoComida = items.Select(i => i.a).ToList();
            for (var x = 0; x < platoComida.Count; x++)
                for (var y = x + 1; y < platoComida.Count; y++)
                    if (ReglaChoca(platoComida[x], platoComida[y]))
                        advertencias.Add($"{CatalogoService.ComidaNombres[m]}: {platoComida[x].Nombre} con {platoComida[y].Nombre} (regla de compatibilidad provisional; no había otra opción en esa comida).");

            var dtoItems = items.Select(i => new AlimentoMenuDto(
                i.a.Id, i.a.Nombre, i.a.Grupo, i.a.Subgrupo, i.porc, i.a.MedidaCasera, i.a.PesoG, i.a.Foto, i.a.FotoReferencia, i.a.Nota,
                Alternativas(i.a, items.Select(x => x.a).ToList(), m))).ToList();
            var aporte = new MacrosDto(
                Math.Round(items.Sum(i => i.g.Kcal * i.porc), 1), Math.Round(items.Sum(i => i.g.Cho * i.porc), 1),
                Math.Round(items.Sum(i => i.g.Prot * i.porc), 1), Math.Round(items.Sum(i => i.g.Grasa * i.porc), 1));
            comidas.Add(new ComidaDto(CatalogoService.ComidaIds[m], CatalogoService.ComidaNombres[m],
                CatalogoService.ComidaHorarios[m], dtoItems, aporte));
        }

        if (!enRango) advertencias.Add("No se logró dejar los 4 nutrientes entre 90 % y 110 % con las reglas actuales.");
        var sinFoto = comidas.SelectMany(c => c.Alimentos).Where(a => a.Foto == null).Select(a => a.Nombre).Distinct().ToList();
        if (sinFoto.Count > 0) advertencias.Add("Alimentos sin foto en el libro: " + string.Join(", ", sinFoto) + ".");
        var conReferencia = comidas.SelectMany(c => c.Alimentos).Where(a => a.FotoReferencia != null).Select(a => a.Nombre).Distinct().ToList();
        if (conReferencia.Count > 0) advertencias.Add("Foto de referencia (no es el mismo alimento): " + string.Join(", ", conReferencia) + ".");

        var total = new MacrosDto(
            Math.Round(comidas.Sum(c => c.Aporte.Kcal), 1), Math.Round(comidas.Sum(c => c.Aporte.Cho), 1),
            Math.Round(comidas.Sum(c => c.Aporte.Proteina), 1), Math.Round(comidas.Sum(c => c.Aporte.Grasa), 1));

        return new PlanResponse(
            fase, leccion, peso, semilla,
            new RequerimientosDto(Math.Round(req.Kcal, 1), Math.Round(req.ChoG, 1), Math.Round(req.ProtG, 1),
                Math.Round(req.GrasaG, 1), req.KcalPorKg, req.ProtPct, req.ChoPct, req.GrasaPct),
            grupos.Select(g => new GrupoPorcionDto(g.Grupo, g.Subgrupo, g.Porciones, reparto[g])).ToList(),
            new AdecuacionDto(Math.Round(ad[0], 1), Math.Round(ad[1], 1), Math.Round(ad[2], 1), Math.Round(ad[3], 1), enRango),
            comidas, total, advertencias);
    }

    /// <summary>
    /// Elige la fuente proteica de la comida (pollo, res, pescado, huevo...) entre las que tienen opciones
    /// en TODOS los subgrupos de carnes que tocan en esa comida; prefiere las menos usadas en el día.
    /// </summary>
    string? ElegirFamiliaProteica(List<GrupoIntercambio> gruposCarne, int comida, Dictionary<string, int> usadas, Random rng)
    {
        if (gruposCarne.Count == 0) return null;
        HashSet<string>? comunes = null;
        foreach (var g in gruposCarne)
        {
            var familias = _catalogo.Opciones(g.Grupo, g.Subgrupo, CatalogoService.ComidaIds[comida])
                .Select(a => Texto.Familia(a.Nombre)).ToHashSet();
            if (familias.Count == 0) continue;
            if (comunes == null) comunes = familias;
            else { var cruce = comunes.Intersect(familias).ToHashSet(); if (cruce.Count > 0) comunes = cruce; }
        }
        if (comunes == null || comunes.Count == 0) return null;
        var elegida = comunes.OrderBy(f => usadas.GetValueOrDefault(f)).ThenBy(_ => rng.Next()).First();
        usadas[elegida] = usadas.GetValueOrDefault(elegida) + 1;
        return elegida;
    }

    /// <summary>
    /// Elige un alimento del pool: prefiere los menos usados en el día, al azar entre los empatados,
    /// y evita (si hay otra opción) repetir la misma "familia" (arroz+arroz, pescado+pescado) o juntar
    /// alimentos incompatibles en la misma comida.
    /// </summary>
    Alimento? Elegir(List<Alimento> pool, List<Alimento> yaDelGrupo, IEnumerable<Alimento> yaEnComida,
        Dictionary<string, int> usados, Random rng)
    {
        var enComida = yaEnComida.Concat(yaDelGrupo).ToList();
        var disponibles = pool.Where(a => !yaDelGrupo.Contains(a))
            .OrderBy(a => a.Foto == null ? 2 : a.FotoReferencia != null ? 1 : 0) // primero foto propia, luego de referencia, al final sin foto
            .ThenBy(a => usados.GetValueOrDefault(a.Id))          // luego los menos usados en el día
            .ThenBy(_ => rng.Next()).ToList();                    // y al azar entre los empatados
        if (disponibles.Count == 0) return null;
        return disponibles.FirstOrDefault(a => !Conflicto(a, enComida)) ?? disponibles[0];
    }

    /// <summary>
    /// Intercambios de un alimento: otros del mismo subgrupo, permitidos en esa comida, que no estén ya en el plato
    /// y que no choquen con lo demás de la comida. Primero los que tienen foto y los de la misma fuente.
    /// </summary>
    List<AlternativaDto> Alternativas(Alimento elegido, List<Alimento> plato, int comida)
    {
        var resto = plato.Where(x => x.Id != elegido.Id).ToList();
        var elegidoYaChoca = Conflicto(elegido, resto); // si el plato ya tiene el choque, no se le quita el intercambio por eso
        return _catalogo.Opciones(elegido.Grupo, elegido.Subgrupo, CatalogoService.ComidaIds[comida])
            .Where(a => a.Id != elegido.Id && !plato.Any(x => x.Id == a.Id) && (elegidoYaChoca || !Conflicto(a, resto)))
            .OrderBy(a => a.Foto == null ? 2 : a.FotoReferencia != null ? 1 : 0)
            .ThenBy(a => Texto.Familia(a.Nombre) == Texto.Familia(elegido.Nombre) ? 0 : 1)
            .ThenBy(a => a.Nombre)
            .Take(8)
            .Select(a => new AlternativaDto(a.Id, a.Nombre, a.MedidaCasera, a.PesoG, a.Foto, a.FotoReferencia))
            .ToList();
    }

    bool ReglaChoca(Alimento a, Alimento b)
    {
        var na = Texto.Norm(a.Nombre); var nb = Texto.Norm(b.Nombre);
        foreach (var r in _catalogo.Reglas)
            if ((na.Contains(r.A) && nb.Contains(r.B)) || (na.Contains(r.B) && nb.Contains(r.A)))
                return true;
        return false;
    }

    bool Conflicto(Alimento a, List<Alimento> enComida)
    {
        var fam = Texto.Familia(a.Nombre);
        foreach (var otro in enComida)
        {
            var ambosCarnes = a.Grupo == GrupoCarnes && otro.Grupo == GrupoCarnes; // en carnes la misma fuente SÍ se permite
            if (!ambosCarnes && Texto.Familia(otro.Nombre) == fam) return true;
            if (ReglaChoca(a, otro)) return true;
        }
        return false;
    }
}
