using NutriLesiones.Api.Models;

namespace NutriLesiones.Api.Services;

/// <summary>Reparte las porciones diarias de cada subgrupo entre las 5 comidas (en pasos de 0.5).</summary>
public static class DistribuidorService
{
    public static double[] Distribuir(GrupoIntercambio g, CatalogoService catalogo)
    {
        var total = g.Porciones;
        var resultado = new double[5];
        if (total <= 0) return resultado;

        // Solo se asigna a las comidas donde el catálogo tiene alimentos de ese subgrupo.
        var pesos = new double[5];
        for (var m = 0; m < 5; m++)
            pesos[m] = catalogo.Opciones(g.Grupo, g.Subgrupo, CatalogoService.ComidaIds[m]).Count > 0 ? g.PesosComida[m] : 0;

        if (pesos.Sum() <= 0) // el reparto deseado cae en comidas sin opciones: se reparte parejo entre las que sí tienen
        {
            for (var m = 0; m < 5; m++)
                pesos[m] = catalogo.Opciones(g.Grupo, g.Subgrupo, CatalogoService.ComidaIds[m]).Count > 0 ? 1 : 0;
            if (pesos.Sum() <= 0) pesos[2] = 1; // sin opciones en ninguna comida: va al almuerzo
        }

        var unidades = (int)Math.Round(total / 0.5);   // media porción = 1 unidad
        var suma = pesos.Sum();
        var ideal = pesos.Select(p => unidades * p / suma).ToArray();
        var piso = ideal.Select(Math.Floor).ToArray();
        var faltan = unidades - (int)piso.Sum();

        // método del mayor resto: las unidades que sobran van a las comidas con mayor parte decimal
        var orden = Enumerable.Range(0, 5)
            .OrderByDescending(i => ideal[i] - piso[i]).ThenByDescending(i => pesos[i]).ToArray();
        for (var k = 0; k < faltan; k++) piso[orden[k % 5]]++;

        for (var m = 0; m < 5; m++) resultado[m] = piso[m] * 0.5;
        return resultado;
    }
}
