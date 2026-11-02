# NutriLesiones.Api (.NET 8)

Backend del sistema: calcula los requerimientos (kcal, CHO, proteína, grasa) según peso y fase de la lesión,
reparte las porciones por grupo de alimentos (90–110 % de adecuación), las divide en las 5 comidas del día
y arma el menú con alimentos reales y sus fotos del libro de la USFQ.

## Ejecutar en local (Windows, PowerShell)
```powershell
# 1) Instalar el SDK de .NET 8 (solo la primera vez)
winget install Microsoft.DotNet.SDK.8

# 2) Descomprimir y entrar a la carpeta
Expand-Archive NutriLesiones.Api.zip -DestinationPath .
cd NutriLesiones.Api

# 3) Levantar el backend
dotnet run --urls http://localhost:5099
```
- Página de prueba: http://localhost:5099
- Probar la API: `Invoke-RestMethod -Method Post -Uri http://localhost:5099/api/plan -ContentType "application/json" -Body '{"peso":65,"fase":"inflamatoria"}' | ConvertTo-Json -Depth 10`
- Revisar que responde: `Invoke-RestMethod http://localhost:5099/api/salud`

Si solo tienes .NET 10 instalado, cambia la versión antes de correr (no probado con 10, pero el código no usa nada específico de 8):
```powershell
(Get-Content NutriLesiones.Api.csproj) -replace 'net8.0','net10.0' | Set-Content NutriLesiones.Api.csproj
(Get-Content Dockerfile) -replace ':8.0',':10.0' | Set-Content Dockerfile
```

Con Docker (opcional):
```powershell
docker build -t nutrilesiones-api .
docker run -p 8080:8080 nutrilesiones-api
```

## Endpoints
| Método | Ruta | Para qué |
|---|---|---|
| GET | `/` | Página de prueba (formulario + menú con fotos) |
| GET | `/api/salud` | Revisar que el servidor responde |
| GET | `/api/catalogos` | Fases, lecciones y comidas (para los combos de Angular) |
| GET | `/api/alimentos?grupo=cereales&comida=Desayuno` | Catálogo de alimentos (filtros opcionales) |
| POST | `/api/plan` | **Plan completo del día** |
| GET | `/imagenes/<archivo>.jpg` | Fotos del libro |

### POST /api/plan
```json
{ "peso": 65, "fase": "inflamatoria", "leccion": "desgarros", "semilla": 7 }
```
- `fase`: `inflamatoria`, `proliferativa` o `remodelacion`.
- `semilla` es opcional: **sin semilla cada llamada devuelve un menú distinto** (botón "Generar otro menú"); con la misma semilla devuelve el mismo menú.
- Respuesta de ejemplo completa: `docs/ejemplo-respuesta.json`.
- Cada alimento del menú trae `alternativas` (los **intercambios**): otros alimentos del mismo subgrupo, permitidos en esa comida, que se pueden usar en su lugar con las mismas porciones.
- `advertencias` avisa si algo no se pudo cumplir (adecuación fuera de 90–110 %, alimentos sin foto, choques con las reglas de compatibilidad).
- La foto de cada alimento viene en `foto` (ruta relativa). En Angular: `apiUrl + '/' + alimento.foto`. Si `foto` es `null`, ese alimento no tiene foto en el libro.

## Qué archivo tocar para cada cambio
| Cambio | Archivo |
|---|---|
| Agregar/quitar alimentos, medidas, comidas permitidas o fotos | `Data/alimentos.json` y `wwwroot/imagenes/` |
| Reglas de compatibilidad (ej. leche con huevo) | `Data/reglas.json` |
| Valores por porción de cada subgrupo, topes (leguminosas máx. 2) y reparto por comida | `Models/Modelos.cs` |
| Porciones base de frutas/vegetales/lácteos según calorías | `Services/PorcionesService.cs` (método `AsignarBase`) |
| kcal/kg y % de macros por fase | `Services/RequerimientosService.cs` |
| Horarios de las comidas | `Services/CatalogoService.cs` |

## Publicar gratis (Render, con Docker)
1. Sube esta carpeta a un repositorio de GitHub.
2. En Render: **New → Web Service →** conecta el repo → Runtime **Docker** → plan **Free**.
3. Health Check Path: `/api/salud`.
4. (Opcional) Variable de entorno `CORS_ORIGINS` = `https://tu-front.vercel.app` para que solo tu Angular pueda llamar a la API. Sin ella acepta cualquier origen.
5. Render te da un link `https://nombre.onrender.com`. El plan gratis se "duerme" tras un rato sin uso y la primera llamada tarda unos segundos en despertar.

## Prioridad de los subgrupos (decisión del nutricionista)
- **Preferentes:** los 8 de siempre, más lácteos semidescremados (llevan las porciones base de lácteos) y grasas medias (aguacate).
- **Opcionales** (el algoritmo los usa solo si le sirven más que los preferentes): cereales medios en CHO (dos subgrupos), frutas bajas en CHO y lácteos descremados.
- **Fuera por ahora** (pendiente de la tutora): quesos, lácteos enteros, lácteos descremados altos en CHO y grasas altas en grasas y bajas en CHO.
- Se cambia en `Models/Modelos.cs` (campo `Prioridad` de cada subgrupo).
- Los valores por porción de los cereales medios salen del libro (p. 10). En el Excel del nutricionista esos dos subgrupos tienen los valores cruzados (20/2/6/145 corresponde a "medios en grasas" y 20/3/1/105 a "bajos en grasas").

## Fotos que faltan
El libro **no trae foto** de 12 alimentos. Para 5 usé la foto de un alimento parecido del libro, marcada en la API como `fotoReferencia` (y como aviso en `advertencias`); los otros 7 quedan sin foto (`foto: null`).

Para poner la foto real: guarda el archivo con el nombre indicado en `wwwroot/imagenes/` y listo (el programa la detecta sola, reemplaza la de referencia y no hay que tocar el JSON).

**Con foto de referencia (reemplazar cuando tengan la real):**

| Alimento | Archivo de la foto real | Foto de referencia usada |
|---|---|---|
| Choclo en grano | `choclo-en-grano.jpg` | Maíz, dulce, enlatado. |
| Sardinas en aceite, enlatadas | `sardinas-en-aceite-enlatadas.jpg` | Sardina enlatada, en salsa de tomate. |
| Res, bistec, crudo | `res-bistec-crudo.jpg` | Res, bife de chorizo, crudo. |
| Res, carne magra, cruda | `res-carne-magra-cruda.jpg` | Res, carne semimagra, cruda. |
| Pescado, atún en agua o aceite, enlatado | `pescado-atun-en-agua-o-aceite-enlatado.jpg` | Pescado, albacora (atún blanco), cocido. |

**Sin ninguna foto (hay que tomarlas):**

| Alimento | Archivo |
|---|---|
| Plátano maduro o maqueño | `platano-maduro-o-maqueno.jpg` |
| Pan integral, tipo pita | `pan-integral-tipo-pita.jpg` |
| Harina de cebada, máchica | `harina-de-cebada-machica.jpg` |
| Pescado, bagre | `pescado-bagre.jpg` |
| Aceite de oliva | `aceite-de-oliva.jpg` |
| Aceite de canola | `aceite-de-canola.jpg` |
| Aceite de girasol | `aceite-de-girasol.jpg` |

## Confirmado por el nutricionista
- Leguminosas: 1 a 2 porciones **por día** (el código ya lo hace: 1 de base, máximo 2).
- Lesión, edad, altura, sexo y posición **no** afectan el cálculo; solo cuentan peso y fase (por eso `leccion` en `/api/plan` es opcional y solo se devuelve tal cual).
- Leche/yogur con huevo sí se permite.

## Pendientes
- Más de 2600 kcal: hoy usa las mismas porciones base que a 2600. Una opción es interpolar entre 2600 y 3100 kcal (DASH: 6 vegetales, 6 frutas, 3–4 lácteos a 3100); debe aprobarla la tutora. Los valores de 2600 también hay que verificarlos.
- Pechuga de pollo: peso 40 g sin confirmar si es crudo o cocido (el catálogo lo marca con una nota).
- Reglas de compatibilidad: `Data/reglas.json` está vacío porque leche/yogur con huevo sí se permite.
