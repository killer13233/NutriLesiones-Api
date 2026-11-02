namespace NutriLesiones.Api.Models;

public sealed record PlanRequest(double Peso, string? Fase, string? Leccion, int? Semilla);

public sealed record MacrosDto(double Kcal, double Cho, double Proteina, double Grasa);

public sealed record RequerimientosDto(double Kcal, double Carbohidratos, double Proteina, double Grasa,
    int KcalPorKg, int PctProteina, int PctCarbohidratos, int PctGrasa);

public sealed record AdecuacionDto(double Carbohidratos, double Proteina, double Grasa, double Kcal, bool EnRango90a110);

public sealed record GrupoPorcionDto(string Grupo, string Subgrupo, double PorcionesTotal, double[] PorComida);

public sealed record AlternativaDto(string Id, string Nombre, string MedidaPorPorcion, string PesoPorPorcionG, string? Foto, string? FotoReferencia);

/// <summary>Un alimento del menú. "Alternativas" son los intercambios: alimentos del mismo subgrupo,
/// permitidos en esa comida, que se pueden usar en su lugar con la misma cantidad de porciones.</summary>
public sealed record AlimentoMenuDto(string Id, string Nombre, string Grupo, string Subgrupo,
    double Porciones, string MedidaPorPorcion, string PesoPorPorcionG, string? Foto, string? FotoReferencia, string? Nota,
    List<AlternativaDto> Alternativas);

public sealed record ComidaDto(string Id, string Nombre, string Horario, List<AlimentoMenuDto> Alimentos, MacrosDto Aporte);

public sealed record PlanResponse(
    string Fase, string? Leccion, double Peso, int Semilla,
    RequerimientosDto Requerimientos,
    List<GrupoPorcionDto> Porciones,
    AdecuacionDto Adecuacion,
    List<ComidaDto> Comidas,
    MacrosDto TotalDia,
    List<string> Advertencias);
