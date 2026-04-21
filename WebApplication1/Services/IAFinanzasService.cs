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

    private static readonly HashSet<string> TokensIgnorados = new(StringComparer.OrdinalIgnoreCase)
    {
        "y", "de", "del", "la", "el", "los", "las", "al", "en", "the", "other", "otros"
    };

    private static readonly HashSet<string> TokensOperacion = new(StringComparer.OrdinalIgnoreCase)
    {
        "compra", "ticket", "factura", "movimiento", "cargo", "pago", "tarjeta",
        "abono", "ingreso", "entrada", "transferencia", "recibida", "servicio", "online",
        "comercio", "local", "proveedor", "habitual"
    };

    private static readonly KeywordRule[] KeywordRules =
    {
        // Priorizamos "supermercado" para evitar que acabe en categorías amplias
        // como "comida y bebida" cuando ambas existen.
        new("mercadona", new[] { "supermercado" }, 0.95m),
        new("carrefour", new[] { "supermercado" }, 0.95m),
        new("lidl", new[] { "supermercado" }, 0.95m),
        new("dia", new[] { "supermercado" }, 0.90m),
        new("eroski", new[] { "supermercado" }, 0.95m),
        new("alcampo", new[] { "supermercado" }, 0.95m),

        new("repsol", new[] { "combustible", "coche", "transporte" }, 0.95m),
        new("cepsa", new[] { "combustible", "coche", "transporte" }, 0.95m),
        new("gasolina", new[] { "combustible", "coche", "transporte" }, 0.95m),
        new("gasoil", new[] { "combustible", "coche", "transporte" }, 0.95m),
        new("combustible", new[] { "combustible", "coche", "transporte" }, 0.90m),

        new("uber", new[] { "taxi", "transporte" }, 0.95m),
        new("cabify", new[] { "taxi", "transporte" }, 0.95m),
        new("taxi", new[] { "taxi", "transporte" }, 0.95m),

        new("nomina", new[] { "salario", "ingres", "nomina", "prestacion", "pension" }, 0.95m),
        new("salario", new[] { "salario", "ingres", "nomina", "prestacion", "pension" }, 0.95m),
        new("payroll", new[] { "salario", "ingres", "nomina" }, 0.95m),

        new("alquiler", new[] { "alquiler", "hipoteca", "arrend" }, 0.90m),
        new("renta", new[] { "alquiler", "hipoteca", "arrend" }, 0.85m),

        // Priorizamos "suscripciones" para plataformas recurrentes.
        new("netflix", new[] { "suscripcion", "suscripciones", "subscripcion", "subscripciones" }, 0.95m),
        new("spotify", new[] { "suscripcion", "suscripciones", "subscripcion", "subscripciones" }, 0.95m),
        new("disney", new[] { "suscripcion", "suscripciones", "subscripcion", "subscripciones" }, 0.95m),
        new("hbo", new[] { "suscripcion", "suscripciones", "subscripcion", "subscripciones" }, 0.95m)
    };

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

            var registrosBase = _mlContext.Data.CreateEnumerable<CategoriaTrainingInput>(data, reuseRowObject: false).ToList();
            if (registrosBase.Count == 0)
                throw new BadRequestException("El dataset no contiene registros para entrenar.");

            var categoriasActivas = await ObtenerCategoriasActivasParaEntrenamientoAsync(cancellationToken);
            var registrosEntrenamiento = ExpandirRegistrosEntrenamiento(registrosBase, categoriasActivas);

            var categorias = registrosEntrenamiento
                .Select(r => r.Categoria)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (categorias.Length < 2)
                throw new BadRequestException("Se requieren al menos dos categorías distintas para entrenar el modelo.");

            var dataView = _mlContext.Data.LoadFromEnumerable(registrosEntrenamiento);
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
            var schemaSalida = model.GetOutputSchema(split.TrainSet.Schema);
            _etiquetasOrdenadas = CargarEtiquetasDesdeSchema(schemaSalida);
            if (_etiquetasOrdenadas.Length == 0)
                _etiquetasOrdenadas = categorias;

            _fechaModeloUtc = DateTime.UtcNow;

            return new EntrenamientoModeloCategoriasResponseDto
            {
                ModeloDisponible = true,
                ModeloEntrenadoEnEjecucion = true,
                MacroAccuracy = Convert.ToDecimal(metrics.MacroAccuracy),
                MicroAccuracy = Convert.ToDecimal(metrics.MicroAccuracy),
                RegistrosEntrenamiento = registrosEntrenamiento.Count,
                CategoriasDetectadas = _etiquetasOrdenadas.Length,
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

        var sugerenciaPorReglas = ObtenerSugerenciaPorKeywords(descripcionNormalizada, categoriasCandidatas);
        if (sugerenciaPorReglas != null)
            return sugerenciaPorReglas;

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

        var indiceCategorias = categoriasCandidatas
            .GroupBy(c => Normalizar(c.Nombre))
            .ToDictionary(g => g.Key, g => g.First());

        var scoreVector = output.Score ?? Array.Empty<float>();
        var probabilidades = scoreVector.Length > 0
            ? Softmax(scoreVector)
            : Array.Empty<decimal>();

        var alternativas = ConstruirAlternativasModelo(probabilidades, etiquetas, indiceCategorias);

        if (alternativas.Count == 0)
        {
            var etiquetaPredicha = string.IsNullOrWhiteSpace(output.PredictedLabel)
                ? categoriasCandidatas[0].Nombre
                : output.PredictedLabel;

            var keyPredicha = Normalizar(etiquetaPredicha);
            if (indiceCategorias.TryGetValue(keyPredicha, out var categoriaPredicha))
            {
                alternativas.Add(new CategoriaSugeridaDto
                {
                    CategoriaId = categoriaPredicha.Id,
                    CategoriaNombre = categoriaPredicha.Nombre,
                    Confianza = 0.45m,
                    Fuente = "modelo-global"
                });
            }
        }

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

    private List<CategoriaTrainingInput> ExpandirRegistrosEntrenamiento(
        List<CategoriaTrainingInput> registrosBase,
        List<CategoriaSemillaEntrenamiento> categoriasActivas)
    {
        var expandidos = new List<CategoriaTrainingInput>(registrosBase.Count * 4);
        var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var etiquetaCanonicaPorTipoYCategoria = registrosBase
            .Where(r => !string.IsNullOrWhiteSpace(r.Categoria))
            .GroupBy(
                r => $"{(r.Tipo <= 1.5f ? 1 : 2)}|{Normalizar(r.Categoria)}",
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Categoria, StringComparer.OrdinalIgnoreCase);

        foreach (var registro in registrosBase)
        {
            AgregarVariante(registro, registro.Descripcion);

            var descripcionLimpia = LimpiarDescripcionEntrenamiento(registro.Descripcion, registro.Categoria);
            AgregarVariante(registro, descripcionLimpia);

            var descripcionClave = ExtraerTokensClave(descripcionLimpia);
            AgregarVariante(registro, descripcionClave);
        }

        foreach (var categoria in categoriasActivas)
        {
            if (string.IsNullOrWhiteSpace(categoria.Nombre))
                continue;

            var tipo = categoria.Tipo == TipoCategoria.Ingreso ? 1 : 2;
            var tipoFloat = tipo == 1 ? 1f : 2f;
            var key = $"{tipo}|{Normalizar(categoria.Nombre)}";
            var etiqueta = etiquetaCanonicaPorTipoYCategoria.TryGetValue(key, out var existente)
                ? existente
                : categoria.Nombre.Trim();

            var registroSemilla = new CategoriaTrainingInput
            {
                Categoria = etiqueta,
                Tipo = tipoFloat,
                Importe = tipo == 1 ? 1250f : 55f
            };

            foreach (var descripcionSemilla in GenerarDescripcionesSemilla(etiqueta, tipo))
            {
                AgregarVariante(registroSemilla, descripcionSemilla);
            }
        }

        return expandidos;

        void AgregarVariante(CategoriaTrainingInput baseRegistro, string? descripcionVariante)
        {
            if (string.IsNullOrWhiteSpace(descripcionVariante))
                return;

            var descripcion = descripcionVariante.Trim();
            if (descripcion.Length < 3)
                return;

            var uniqueKey = $"{baseRegistro.Tipo}|{Normalizar(baseRegistro.Categoria)}|{descripcion}";
            if (!vistos.Add(uniqueKey))
                return;

            expandidos.Add(new CategoriaTrainingInput
            {
                Descripcion = descripcion,
                Importe = baseRegistro.Importe,
                Tipo = baseRegistro.Tipo,
                Categoria = baseRegistro.Categoria
            });
        }
    }

    private async Task<List<CategoriaSemillaEntrenamiento>> ObtenerCategoriasActivasParaEntrenamientoAsync(CancellationToken cancellationToken)
    {
        var categorias = await _context.Categorias
            .AsNoTracking()
            .Where(c => !c.Archivada)
            .Select(c => new CategoriaSemillaEntrenamiento(c.Nombre, c.Tipo))
            .ToListAsync(cancellationToken);

        return categorias
            .Where(c => !string.IsNullOrWhiteSpace(c.Nombre))
            .GroupBy(c => $"{(int)c.Tipo}|{Normalizar(c.Nombre)}", StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
    }

    private static IEnumerable<string> GenerarDescripcionesSemilla(string categoria, int tipo)
    {
        if (string.IsNullOrWhiteSpace(categoria))
            yield break;

        var categoriaLimpia = categoria.Trim();

        if (tipo == 1)
        {
            yield return $"ingreso {categoriaLimpia}";
            yield return $"abono {categoriaLimpia}";
            yield return $"transferencia recibida {categoriaLimpia}";
            yield return $"cobro {categoriaLimpia}";
            yield return $"{categoriaLimpia} mensual";
            yield return $"pago recibido {categoriaLimpia}";
            yield break;
        }

        yield return $"compra {categoriaLimpia}";
        yield return $"pago tarjeta {categoriaLimpia}";
        yield return $"ticket {categoriaLimpia}";
        yield return $"factura {categoriaLimpia}";
        yield return $"cargo {categoriaLimpia}";
        yield return $"movimiento {categoriaLimpia}";
        yield return $"{categoriaLimpia} mensual";
        yield return $"servicio {categoriaLimpia}";
    }

    private static string LimpiarDescripcionEntrenamiento(string descripcion, string categoria)
    {
        var tokens = TokenizarConOrden(Normalizar(descripcion));
        if (tokens.Count == 0)
            return string.Empty;

        var categoriaTokens = TokenizarConOrden(Normalizar(categoria))
            .Where(t => !TokensIgnorados.Contains(t))
            .ToList();

        if (categoriaTokens.Count > 0 && tokens.Count >= categoriaTokens.Count)
        {
            var segmentoFinal = tokens.Skip(tokens.Count - categoriaTokens.Count).ToList();
            if (segmentoFinal.SequenceEqual(categoriaTokens))
            {
                tokens.RemoveRange(tokens.Count - categoriaTokens.Count, categoriaTokens.Count);
            }
        }

        var tokensLimpios = tokens
            .Where(t => !TokensOperacion.Contains(t))
            .ToList();

        if (tokensLimpios.Count == 0)
            tokensLimpios = tokens;

        return string.Join(' ', tokensLimpios);
    }

    private static string ExtraerTokensClave(string descripcionNormalizada)
    {
        var salida = new List<string>();
        var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var token in TokenizarConOrden(descripcionNormalizada))
        {
            if (token.Length < 3 || TokensIgnorados.Contains(token) || TokensOperacion.Contains(token))
                continue;

            if (vistos.Add(token))
            {
                salida.Add(token);
                if (salida.Count == 3)
                    break;
            }
        }

        return string.Join(' ', salida);
    }

    private static List<CategoriaSugeridaDto> ConstruirAlternativasModelo(
        decimal[] probabilidades,
        string[] etiquetas,
        Dictionary<string, Categoria> indiceCategorias)
    {
        if (probabilidades.Length == 0 || etiquetas.Length != probabilidades.Length)
            return new List<CategoriaSugeridaDto>();

        var probabilidadPorCategoria = new Dictionary<Guid, (Categoria Categoria, decimal ProbRaw)>();

        for (var i = 0; i < etiquetas.Length; i++)
        {
            var key = Normalizar(etiquetas[i]);
            if (!indiceCategorias.TryGetValue(key, out var categoria))
                continue;

            if (probabilidadPorCategoria.TryGetValue(categoria.Id, out var actual))
            {
                probabilidadPorCategoria[categoria.Id] = (actual.Categoria, actual.ProbRaw + probabilidades[i]);
            }
            else
            {
                probabilidadPorCategoria[categoria.Id] = (categoria, probabilidades[i]);
            }
        }

        if (probabilidadPorCategoria.Count == 0)
            return new List<CategoriaSugeridaDto>();

        var sumaCandidatas = probabilidadPorCategoria.Values.Sum(x => x.ProbRaw);
        if (sumaCandidatas <= 0)
            sumaCandidatas = 1m;

        return probabilidadPorCategoria
            .Values
            .Select(x =>
            {
                var probNormalizada = x.ProbRaw / sumaCandidatas;
                var confianza = CalibrarConfianzaModelo(x.ProbRaw, probNormalizada);

                return new CategoriaSugeridaDto
                {
                    CategoriaId = x.Categoria.Id,
                    CategoriaNombre = x.Categoria.Nombre,
                    Confianza = confianza,
                    Fuente = "modelo-global"
                };
            })
            .OrderByDescending(x => x.Confianza)
            .Take(3)
            .ToList();
    }

    private SugerenciaCategoriaResponseDto? ObtenerSugerenciaPorKeywords(
        string descripcionNormalizada,
        List<Categoria> categoriasCandidatas)
    {
        var tokensDescripcion = Tokenizar(descripcionNormalizada);
        if (tokensDescripcion.Count == 0)
            return null;

        var categoriasPorId = categoriasCandidatas.ToDictionary(c => c.Id);
        var nombresNormalizados = categoriasCandidatas.ToDictionary(c => c.Id, c => Normalizar(c.Nombre));
        var puntuacionPorCategoria = new Dictionary<Guid, decimal>();

        void Acumular(Guid categoriaId, decimal puntuacion)
        {
            if (!puntuacionPorCategoria.TryAdd(categoriaId, puntuacion))
            {
                puntuacionPorCategoria[categoriaId] += puntuacion;
            }
        }

        foreach (var categoria in categoriasCandidatas)
        {
            var nombreNormalizado = nombresNormalizados[categoria.Id];
            if (string.IsNullOrWhiteSpace(nombreNormalizado))
                continue;

            if (descripcionNormalizada.Contains(nombreNormalizado, StringComparison.Ordinal))
            {
                Acumular(categoria.Id, 0.90m);
            }

            foreach (var tokenCategoria in Tokenizar(nombreNormalizado))
            {
                if (tokenCategoria.Length < 4 || TokensIgnorados.Contains(tokenCategoria))
                    continue;

                if (tokensDescripcion.Contains(tokenCategoria))
                {
                    Acumular(categoria.Id, 0.22m);
                }
            }
        }

        foreach (var rule in KeywordRules)
        {
            if (!tokensDescripcion.Contains(rule.Keyword))
                continue;

            foreach (var categoria in categoriasCandidatas)
            {
                var nombreNormalizado = nombresNormalizados[categoria.Id];
                if (rule.CategoryHints.Any(h => nombreNormalizado.Contains(h, StringComparison.Ordinal)))
                {
                    Acumular(categoria.Id, rule.Weight);
                }
            }
        }

        if (puntuacionPorCategoria.Count == 0)
            return null;

        var ranking = puntuacionPorCategoria
            .OrderByDescending(x => x.Value)
            .Take(3)
            .ToList();

        var mejorPuntuacion = ranking[0].Value;
        if (mejorPuntuacion < 0.65m)
            return null;

        var alternativas = ranking
            .Select(item => new CategoriaSugeridaDto
            {
                CategoriaId = item.Key,
                CategoriaNombre = categoriasPorId[item.Key].Nombre,
                Confianza = CalcularConfianzaRegla(item.Value),
                Fuente = "regla-keywords"
            })
            .ToList();

        var mejor = alternativas[0];

        return new SugerenciaCategoriaResponseDto
        {
            MejorSugerencia = mejor,
            Alternativas = alternativas,
            Confianza = mejor.Confianza,
            Fuente = "regla-keywords",
            RequiereConfirmacion = mejor.Confianza < _options.ConfidenceThreshold,
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
        _etiquetasOrdenadas = CargarEtiquetasDesdeSchema(schema);
        if (_etiquetasOrdenadas.Length == 0)
            _etiquetasOrdenadas = CargarCategoriasDesdeDataset().ToArray();

        _fechaModeloUtc = File.GetLastWriteTimeUtc(modelPath);
    }

    private static string[] CargarEtiquetasDesdeSchema(DataViewSchema schema)
    {
        var scoreColumnFound = false;
        var scoreColumn = default(DataViewSchema.Column);

        for (var i = 0; i < schema.Count; i++)
        {
            var current = schema[i];
            if (!string.Equals(current.Name, "Score", StringComparison.Ordinal))
                continue;

            scoreColumn = current;
            scoreColumnFound = true;
            break;
        }

        if (!scoreColumnFound)
            return Array.Empty<string>();

        var annotations = scoreColumn.Annotations;
        var hasSlotNames = false;

        for (var i = 0; i < annotations.Schema.Count; i++)
        {
            if (string.Equals(annotations.Schema[i].Name, "SlotNames", StringComparison.Ordinal))
            {
                hasSlotNames = true;
                break;
            }
        }

        if (!hasSlotNames)
            return Array.Empty<string>();

        VBuffer<ReadOnlyMemory<char>> slotNames = default;
        annotations.GetValue("SlotNames", ref slotNames);

        return slotNames
            .DenseValues()
            .Select(v => v.ToString())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToArray();
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

    private static decimal CalcularConfianzaRegla(decimal score)
    {
        var confianza = 0.45m + (score * 0.35m);
        if (confianza > 0.99m)
            confianza = 0.99m;

        return Math.Round(confianza, 4);
    }

    private static decimal CalibrarConfianzaModelo(decimal rawProbability, decimal normalizedProbability)
    {
        var confianza = (rawProbability * 0.10m) + (normalizedProbability * 0.90m);
        return Math.Round(Math.Clamp(confianza, 0m, 0.99m), 4);
    }

    private static List<string> TokenizarConOrden(string valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return new List<string>();

        var sb = new StringBuilder(valor.Length);
        foreach (var ch in valor)
        {
            sb.Append(char.IsLetterOrDigit(ch) ? ch : ' ');
        }

        return sb
            .ToString()
            .Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .ToList();
    }

    private static HashSet<string> Tokenizar(string valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var sb = new StringBuilder(valor.Length);
        foreach (var ch in valor)
        {
            sb.Append(char.IsLetterOrDigit(ch) ? ch : ' ');
        }

        return sb
            .ToString()
            .Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
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

    private sealed record CategoriaSemillaEntrenamiento(string Nombre, TipoCategoria Tipo);

    private sealed record KeywordRule(string Keyword, string[] CategoryHints, decimal Weight);
}
