using System.Globalization;
using System.Text;
using FinMind.Common.Exceptions;
using FinMind.Data;
using FinMind.DTO.IA;
using FinMind.Interfaces;
using FinMind.Models;
using FinMind.Models.Enitdades;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Transforms;

namespace FinMind.Services;

public class IAFinanzasService : IIAFinanzasService
{
    private readonly FinMindDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly IAOptions _options;
    private readonly MLContext _mlContext = new(seed: 12);
    private readonly SemaphoreSlim _modelSemaphore = new(1, 1);

    private ITransformer? _modelo;
    private string[] _etiquetasOrdenadas = Array.Empty<string>();
    private DateTime _fechaModeloUtc = DateTime.MinValue;

    public IAFinanzasService(
        FinMindDbContext context,
        IWebHostEnvironment environment,
        IOptions<IAOptions> options)
    {
        _context = context;
        _environment = environment;
        _options = options.Value;
    }

    public async Task<EntrenamientoModeloCategoriasResponseDto> EntrenarModeloCategoriasAsync(bool forzar = false, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            throw new BadRequestException("El módulo de IA está deshabilitado en configuración.");

        var datasetPath = ResolverRuta(_options.DatasetPath);
        var modelDir = ResolverRuta(_options.ModelOutputPath);
        var modelPath = Path.Combine(modelDir, _options.ModelFileName);

        if (!File.Exists(datasetPath))
            throw new BadRequestException($"No se ha encontrado el dataset de categorías en '{datasetPath}'.");

        Directory.CreateDirectory(modelDir);

        await _modelSemaphore.WaitAsync(cancellationToken);

        try
        {
            if (!forzar && File.Exists(modelPath))
            {
                CargarModelo(modelPath);

                return new EntrenamientoModeloCategoriasResponseDto
                {
                    ModeloDisponible = true,
                    ModeloEntrenadoEnEjecucion = false,
                    MacroAccuracy = 0,
                    MicroAccuracy = 0,
                    RegistrosEntrenamiento = ContarRegistrosDataset(datasetPath),
                    CategoriasDetectadas = _etiquetasOrdenadas.Length,
                    RutaDataset = datasetPath,
                    RutaModelo = modelPath,
                    Mensaje = "Modelo cargado desde disco.",
                    FechaModeloUtc = _fechaModeloUtc
                };
            }

            var data = _mlContext.Data.LoadFromTextFile<CategoriaTrainingInput>(
                path: datasetPath,
                hasHeader: true,
                separatorChar: ';',
                allowQuoting: true,
                trimWhitespace: true);

            var registros = _mlContext.Data.CreateEnumerable<CategoriaTrainingInput>(data, reuseRowObject: false).ToList();
            if (registros.Count == 0)
                throw new BadRequestException("El dataset no contiene registros para entrenar.");

            var categorias = registros
                .Select(r => r.Categoria)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (categorias.Length < 2)
                throw new BadRequestException("Se requieren al menos dos categorías distintas para entrenar el modelo.");

            var dataView = _mlContext.Data.LoadFromEnumerable(registros);
            var split = _mlContext.Data.TrainTestSplit(dataView, testFraction: 0.2);

            var pipeline = _mlContext.Transforms.Conversion.MapValueToKey(
                    outputColumnName: "Label",
                    inputColumnName: nameof(CategoriaTrainingInput.Categoria),
                    keyOrdinality: ValueToKeyMappingEstimator.KeyOrdinality.ByValue)
                .Append(_mlContext.Transforms.Text.FeaturizeText(
                    outputColumnName: "DescripcionFeaturizada",
                    inputColumnName: nameof(CategoriaTrainingInput.Descripcion)))
                .Append(_mlContext.Transforms.Concatenate(
                    "Features",
                    "DescripcionFeaturizada"))
                .Append(_mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy())
                .Append(_mlContext.Transforms.Conversion.MapKeyToValue(
                    outputColumnName: "PredictedLabel",
                    inputColumnName: "PredictedLabel"));

            var model = pipeline.Fit(split.TrainSet);
            var predictions = model.Transform(split.TestSet);
            var metrics = _mlContext.MulticlassClassification.Evaluate(predictions);

            using (var fs = File.Create(modelPath))
            {
                _mlContext.Model.Save(model, split.TrainSet.Schema, fs);
            }

            _modelo = model;
            _etiquetasOrdenadas = categorias;
            _fechaModeloUtc = DateTime.UtcNow;

            return new EntrenamientoModeloCategoriasResponseDto
            {
                ModeloDisponible = true,
                ModeloEntrenadoEnEjecucion = true,
                MacroAccuracy = Convert.ToDecimal(metrics.MacroAccuracy),
                MicroAccuracy = Convert.ToDecimal(metrics.MicroAccuracy),
                RegistrosEntrenamiento = registros.Count,
                CategoriasDetectadas = categorias.Length,
                RutaDataset = datasetPath,
                RutaModelo = modelPath,
                Mensaje = "Modelo entrenado correctamente.",
                FechaModeloUtc = _fechaModeloUtc
            };
        }
        finally
        {
            _modelSemaphore.Release();
        }
    }

    public async Task<SugerenciaCategoriaResponseDto> SugerirCategoriaAsync(SugerenciaCategoriaRequestDto request, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            throw new BadRequestException("El módulo de IA está deshabilitado en configuración.");

        if (string.IsNullOrWhiteSpace(request.Descripcion))
            throw new BadRequestException("La descripción es obligatoria para sugerir categoría.");

        if (request.Tipo is not 1 and not 2)
            throw new BadRequestException("El tipo debe ser 1 (Ingreso) o 2 (Gasto).");

        var categoriasCandidatas = await ObtenerCategoriasCandidatasAsync(request.UsuarioId, request.Tipo, cancellationToken);
        if (categoriasCandidatas.Count == 0)
            throw new BadRequestException("No hay categorías disponibles para sugerir con el tipo indicado.");

        var descripcionNormalizada = Normalizar(request.Descripcion);
        var coincidenciaExacta = categoriasCandidatas
            .FirstOrDefault(c => Normalizar(c.Nombre) == descripcionNormalizada);

        if (coincidenciaExacta != null)
        {
            var sugerenciaExacta = new CategoriaSugeridaDto
            {
                CategoriaId = coincidenciaExacta.Id,
                CategoriaNombre = coincidenciaExacta.Nombre,
                Confianza = 0.95m,
                Fuente = "regla-exacta"
            };

            return new SugerenciaCategoriaResponseDto
            {
                MejorSugerencia = sugerenciaExacta,
                Alternativas = new List<CategoriaSugeridaDto> { sugerenciaExacta },
                Confianza = sugerenciaExacta.Confianza,
                Fuente = sugerenciaExacta.Fuente,
                RequiereConfirmacion = false,
                UmbralAutoasignacion = _options.ConfidenceThreshold
            };
        }

        await GarantizarModeloDisponibleAsync(cancellationToken);

        var input = new CategoriaTrainingInput
        {
            Descripcion = request.Descripcion.Trim(),
            Importe = 0f,
            Tipo = request.Tipo,
            Categoria = string.Empty
        };

        CategoriaPrediction output;
        string[] etiquetas;
        ITransformer modeloActual;

        await _modelSemaphore.WaitAsync(cancellationToken);
        try
        {
            if (_modelo == null)
                throw new InvalidOperationException("No se pudo cargar el modelo para sugerir categorías.");

            modeloActual = _modelo;
            etiquetas = _etiquetasOrdenadas;
        }
        finally
        {
            _modelSemaphore.Release();
        }

        var engine = _mlContext.Model.CreatePredictionEngine<CategoriaTrainingInput, CategoriaPrediction>(modeloActual);
        output = engine.Predict(input);

        var scoreVector = output.Score ?? Array.Empty<float>();
        var probabilidades = scoreVector.Length > 0
            ? Softmax(scoreVector)
            : Array.Empty<decimal>();

        var topEtiquetas = new List<(string Etiqueta, decimal Confianza)>();

        if (probabilidades.Length > 0 && etiquetas.Length == probabilidades.Length)
        {
            topEtiquetas = etiquetas
                .Select((categoria, index) => (Etiqueta: categoria, Confianza: probabilidades[index]))
                .OrderByDescending(x => x.Confianza)
                .Take(3)
                .ToList();
        }
        else
        {
            var fallback = string.IsNullOrWhiteSpace(output.PredictedLabel)
                ? categoriasCandidatas[0].Nombre
                : output.PredictedLabel;

            topEtiquetas.Add((fallback, 1m));
        }

        var indiceCategorias = categoriasCandidatas
            .GroupBy(c => Normalizar(c.Nombre))
            .ToDictionary(g => g.Key, g => g.First());

        var alternativas = topEtiquetas
            .Select(item =>
            {
                var key = Normalizar(item.Etiqueta);
                var existe = indiceCategorias.TryGetValue(key, out var categoria);

                if (!existe)
                    return null;

                return new CategoriaSugeridaDto
                {
                    CategoriaId = categoria!.Id,
                    CategoriaNombre = categoria.Nombre,
                    Confianza = Math.Round(item.Confianza, 4),
                    Fuente = "modelo-global"
                };
            })
            .Where(a => a != null)
            .Select(a => a!)
            .ToList();

        var mejor = alternativas.FirstOrDefault();
        var confianza = mejor?.Confianza ?? 0m;

        return new SugerenciaCategoriaResponseDto
        {
            MejorSugerencia = mejor,
            Alternativas = alternativas,
            Confianza = confianza,
            Fuente = "modelo-global",
            RequiereConfirmacion = confianza < _options.ConfidenceThreshold || mejor?.CategoriaId == null,
            UmbralAutoasignacion = _options.ConfidenceThreshold
        };
    }

    private async Task GarantizarModeloDisponibleAsync(CancellationToken cancellationToken)
    {
        await _modelSemaphore.WaitAsync(cancellationToken);
        try
        {
            if (_modelo != null)
                return;
        }
        finally
        {
            _modelSemaphore.Release();
        }

        var modelPath = Path.Combine(ResolverRuta(_options.ModelOutputPath), _options.ModelFileName);
        if (File.Exists(modelPath))
        {
            await _modelSemaphore.WaitAsync(cancellationToken);
            try
            {
                if (_modelo == null)
                {
                    CargarModelo(modelPath);
                }
            }
            finally
            {
                _modelSemaphore.Release();
            }

            return;
        }

        await EntrenarModeloCategoriasAsync(forzar: true, cancellationToken);
    }

    private void CargarModelo(string modelPath)
    {
        DataViewSchema schema;
        using var stream = File.OpenRead(modelPath);
        _modelo = _mlContext.Model.Load(stream, out schema);
        _etiquetasOrdenadas = CargarCategoriasDesdeDataset().ToArray();
        _fechaModeloUtc = File.GetLastWriteTimeUtc(modelPath);
    }

    private List<string> CargarCategoriasDesdeDataset()
    {
        var datasetPath = ResolverRuta(_options.DatasetPath);
        if (!File.Exists(datasetPath))
            return new List<string>();

        var lineas = File.ReadAllLines(datasetPath, Encoding.UTF8)
            .Skip(1)
            .Where(l => !string.IsNullOrWhiteSpace(l));

        return lineas
            .Select(linea => linea.Split(';'))
            .Where(partes => partes.Length == 4)
            .Select(partes => partes[3].Trim())
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private int ContarRegistrosDataset(string datasetPath)
    {
        var totalLineas = File.ReadLines(datasetPath).Count();
        return totalLineas > 0 ? totalLineas - 1 : 0;
    }

    private string ResolverRuta(string relativeOrAbsolute)
    {
        if (Path.IsPathRooted(relativeOrAbsolute))
            return relativeOrAbsolute;

        return Path.Combine(_environment.ContentRootPath, relativeOrAbsolute);
    }

    private async Task<List<Categoria>> ObtenerCategoriasCandidatasAsync(Guid? usuarioId, int tipo, CancellationToken cancellationToken)
    {
        var tipoCategoria = tipo == 1 ? TipoCategoria.Ingreso : TipoCategoria.Gasto;

        var query = _context.Categorias
            .AsNoTracking()
            .Where(c => c.Tipo == tipoCategoria && !c.Archivada);

        if (usuarioId.HasValue)
        {
            query = query.Where(c => c.EsSistema || c.UsuarioId == usuarioId.Value);
        }
        else
        {
            query = query.Where(c => c.EsSistema);
        }

        return await query
            .OrderBy(c => c.Nombre)
            .ToListAsync(cancellationToken);
    }

    private static decimal[] Softmax(float[] values)
    {
        if (values.Length == 0)
            return Array.Empty<decimal>();

        var max = values.Max();
        var exp = values.Select(v => Math.Exp(v - max)).ToArray();
        var sum = exp.Sum();

        if (sum <= 0)
            return values.Select(_ => 0m).ToArray();

        return exp.Select(v => Convert.ToDecimal(v / sum)).ToArray();
    }

    private static string Normalizar(string valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return string.Empty;

        var normalizedString = valor.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();

        foreach (var ch in normalizedString)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(ch);
            }
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    private sealed class CategoriaTrainingInput
    {
        [LoadColumn(0)]
        public string Descripcion { get; set; } = string.Empty;

        [LoadColumn(1)]
        public float Importe { get; set; }

        [LoadColumn(2)]
        public float Tipo { get; set; }

        [LoadColumn(3)]
        public string Categoria { get; set; } = string.Empty;
    }

    private sealed class CategoriaPrediction
    {
        [ColumnName("PredictedLabel")]
        public string PredictedLabel { get; set; } = string.Empty;

        public float[] Score { get; set; } = Array.Empty<float>();
    }
}
