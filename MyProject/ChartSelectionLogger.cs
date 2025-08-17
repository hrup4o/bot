using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Indicators;
using MarketSignals.Core.Hybrid;
using MarketSignals.Core.Metrics;
using MarketSignals.Core.RawData;

namespace Logging
{
    /// <summary>
    /// Renderer интерфейс за визуализация на маркери (начало/край) върху графика.
    /// UI-агностичен – имплементацията зависи от конкретния графичен компонент.
    /// </summary>
    public interface ISelectionRenderer
    {
        /// <summary>Рисува начален маркер при индекс и време (UTC).</summary>
        /// <param name="index">Бар индекс.</param>
        /// <param name="timestampUtc">Време (UTC).</param>
        void DrawStart(int index, DateTime timestampUtc);

        /// <summary>Рисува краен маркер при индекс и време (UTC).</summary>
        /// <param name="index">Бар индекс.</param>
        /// <param name="timestampUtc">Време (UTC).</param>
        void DrawEnd(int index, DateTime timestampUtc);

        /// <summary>Премахва всички маркери на дадения индекс.</summary>
        /// <param name="index">Бар индекс.</param>
        void RemoveAt(int index);

        /// <summary>Премахва всички маркери от графиката.</summary>
        void ClearAll();
    }

    /// <summary>
    /// Модул за управление на селекция по индекси (клик с мишка) и логване в CSV.
    /// - UI-агностичен: изисква външен renderer (по желание) и feature-builder делегат.
    /// - CSV: фиксиран хедър, инвариантна култура, апенд на редове със запазен ред на колоните.
    /// - Независим от конкретна платформа/бот. Поддържа автоматично извличане на метрики от Indicators.IndicatorEngine.
    /// </summary>
    public sealed class ChartSelectionLogger
    {
        /// <summary>
        /// Делегат за построяване на фийчър ред за даден бар индекс.
        /// Ключовете са имената на колоните, стойностите – числови фийчъри.
        /// </summary>
        public delegate IDictionary<string, float> FeatureBuilder(int index);

        /// <summary>Политика за сериализация на липсващи стойности.</summary>
        public enum MissingValuePolicy { Zero, NaN }

        private readonly string _logDirectory;
        private string _fileNameTemplate;
        private string _symbol = "SYMBOL";
        private string _timeframe = "TF";
        private bool _useInvariantCulture = true;
        private MissingValuePolicy _missingPolicy = MissingValuePolicy.NaN;
        private bool _writeSchema = true;

        private int _startIndex = -1;
        private readonly HashSet<int> _markerIndexes = new HashSet<int>();
        private ISelectionRenderer _renderer;
        private FeatureBuilder _builder;
        private string[] _headerOrder;

        // Опционална интеграция с IndicatorEngine
        private IndicatorSeriesBundle _indSeries;
        private IndicatorParams _indParams;

        // Нови интеграции: достъп до барове и енджини за метрики
        private Func<int, OhlcvBar> _getBar;
        private OhlcvMetricsEngine _ohlcvEngine;
        private HeikenAshiMetricsEngine _haEngine;
        private HybridRawFeatureEngine _hybridEngine;
        private OptimalTCNSpikeEngine _spikeEngine;
        private readonly List<(string name, Func<int, IDictionary<string, float>> provider)> _extraProviders = new List<(string, Func<int, IDictionary<string, float>>)>(4);

        // >>> ADD: bars buffer за extras и конфиг
        private IReadOnlyList<OhlcvBar> _barsRef;
        private Indicators.IndicatorExtrasConfig _extrasCfg;

        /// <summary>
        /// Създава нов логер за селекция и CSV запис.
        /// </summary>
        /// <param name="logDirectory">Целева директория за CSV файлове.</param>
        /// <param name="fileNameTemplate">
        /// Шаблон за име на файл. Поддържани плейсхолдъри: {symbol}, {timeframe}, {start}, {end}, {date}.
        /// Пример: "Log_{symbol}_{timeframe}_{start}_{end}_{date}.csv"
        /// </param>
        /// <exception cref="ArgumentException">Ако директорията е празна/невалидна.</exception>
        public ChartSelectionLogger(string logDirectory, string fileNameTemplate = "Log_{symbol}_{timeframe}_{start}_{end}_{date}.csv")
        {
            if (string.IsNullOrWhiteSpace(logDirectory))
                throw new ArgumentException("logDirectory is required", nameof(logDirectory));

            _logDirectory = logDirectory;
            _fileNameTemplate = fileNameTemplate;
            Directory.CreateDirectory(_logDirectory);
        }

        /// <summary>Задава renderer за визуална индикация (по избор).</summary>
        public ChartSelectionLogger SetRenderer(ISelectionRenderer renderer)
        {
            _renderer = renderer;
            return this;
        }

        /// <summary>Задава feature-builder делегат. Задължително преди логване/клик, ако искаш допълнителни фийчъри.</summary>
        public ChartSelectionLogger SetFeatureBuilder(FeatureBuilder builder)
        {
            _builder = builder ?? throw new ArgumentNullException(nameof(builder));
            return this;
        }

        /// <summary>
        /// Регистрира външен доставчик на фийчъри, който ще бъде извикан за всеки бар от диапазона.
        /// </summary>
        public ChartSelectionLogger RegisterFeatureProvider(string groupName, Func<int, IDictionary<string, float>> provider)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            _extraProviders.Add((groupName ?? "custom", provider));
            return this;
        }

        /// <summary>
        /// Задава достъп до базовите барове по индекс (необходим за OHLC/HA/Hybrid/Spike енджини).
        /// </summary>
        public ChartSelectionLogger SetOhlcvBarAccessor(Func<int, OhlcvBar> accessor)
        {
            _getBar = accessor ?? throw new ArgumentNullException(nameof(accessor));
            return this;
        }

        /// <summary>Свързва OHLC метрик енджин.</summary>
        public ChartSelectionLogger SetOhlcvEngine(OhlcvMetricsEngine engine)
        {
            _ohlcvEngine = engine ?? throw new ArgumentNullException(nameof(engine));
            return this;
        }

        /// <summary>Свързва HeikenAshi метрик енджин.</summary>
        public ChartSelectionLogger SetHeikenAshiEngine(HeikenAshiMetricsEngine engine)
        {
            _haEngine = engine ?? throw new ArgumentNullException(nameof(engine));
            return this;
        }

        /// <summary>Свързва Hybrid raw feature енджин.</summary>
        public ChartSelectionLogger SetHybridEngine(HybridRawFeatureEngine engine)
        {
            _hybridEngine = engine ?? throw new ArgumentNullException(nameof(engine));
            return this;
        }

        /// <summary>Свързва Spike-focused TCN енджин.</summary>
        public ChartSelectionLogger SetSpikeEngine(OptimalTCNSpikeEngine engine)
        {
            _spikeEngine = engine ?? throw new ArgumentNullException(nameof(engine));
            return this;
        }

        /// <summary>
        /// Фиксира подредба на колоните в CSV. Ако не е зададена, се използва азбучна подредба на ключовете.
        /// </summary>
        public ChartSelectionLogger SetHeaderOrder(IEnumerable<string> keysInOrder)
        {
            _headerOrder = keysInOrder?.ToArray();
            return this;
        }

        /// <summary>Задава метаданни за файл/шаблон: символ и таймфрейм.</summary>
        public ChartSelectionLogger SetMetadata(string symbol, string timeframe)
        {
            if (!string.IsNullOrWhiteSpace(symbol)) _symbol = symbol;
            if (!string.IsNullOrWhiteSpace(timeframe)) _timeframe = timeframe;
            return this;
        }

        /// <summary>Променя шаблона за име на файл (по избор).</summary>
        public ChartSelectionLogger SetFileNameTemplate(string template)
        {
            if (!string.IsNullOrWhiteSpace(template))
                _fileNameTemplate = template;
            return this;
        }

        /// <summary>Задава използване на инвариантна култура при форматиране на числа.</summary>
        public ChartSelectionLogger SetCultureInvariant(bool enabled = true)
        {
            _useInvariantCulture = enabled;
            return this;
        }

        /// <summary>Указва как да се сериализират липсващите стойности (Zero или NaN; по подразбиране NaN).</summary>
        public ChartSelectionLogger SetMissingValuePolicy(MissingValuePolicy policy)
        {
            _missingPolicy = policy;
            return this;
        }

        /// <summary>Включва/изключва автоматичен export на schema JSON до CSV файла.</summary>
        public ChartSelectionLogger SetSchemaExport(bool enabled = true)
        {
            _writeSchema = enabled;
            return this;
        }

        /// <summary>
        /// Свързва логера с IndicatorEngine: подай външните индикаторни серии и параметри.
        /// Ако е зададен и FeatureBuilder, двата набора от фийчъри се обединяват при логване.
        /// </summary>
        public ChartSelectionLogger SetIndicatorEngine(IndicatorSeriesBundle series, IndicatorParams parameters)
        {
            _indSeries = series ?? throw new ArgumentNullException(nameof(series));
            _indParams = parameters ?? throw new ArgumentNullException(nameof(parameters));
            return this;
        }

        /// <summary>Задава референция към целия буфер от барове (нужен за extras изчисления).</summary>
        public ChartSelectionLogger SetBarsBuffer(IReadOnlyList<OhlcvBar> barsBuffer)
        {
            _barsRef = barsBuffer ?? throw new ArgumentNullException(nameof(barsBuffer));
            return this;
        }

        /// <summary>Активира вътрешните extras от IndicatorEngine (VIDYA, VHF, SuperTrend, KVO, ADL, VWAP) с подаден конфиг.</summary>
        public ChartSelectionLogger SetIndicatorExtras(Indicators.IndicatorExtrasConfig extrasConfig)
        {
            _extrasCfg = extrasConfig ?? throw new ArgumentNullException(nameof(extrasConfig));
            return this;
        }

        /// <summary>Нулира текущата селекция и изчиства всички маркери.</summary>
        public void ResetSelection()
        {
            _startIndex = -1;
            _renderer?.ClearAll();
            _markerIndexes.Clear();
        }

        /// <summary>
        /// Обработва клик по бар индекс. Първи клик маркира начало, втори – край и логва диапазона.
        /// Повторен клик на вече маркиран индекс премахва маркерите там.
        /// </summary>
        public void OnMouseDown(int barIndex, DateTime timestampUtc)
        {
            if (barIndex < 0) return;

            if (_markerIndexes.Contains(barIndex))
            {
                _renderer?.RemoveAt(barIndex);
                _markerIndexes.Remove(barIndex);
                if (_startIndex == barIndex)
                    _startIndex = -1;
                return;
            }

            if (_startIndex < 0)
            {
                _startIndex = barIndex;
                _renderer?.DrawStart(barIndex, timestampUtc);
                _markerIndexes.Add(barIndex);
            }
            else
            {
                int endIndex = barIndex;
                _renderer?.DrawEnd(endIndex, timestampUtc);
                _markerIndexes.Add(endIndex);
                LogRange(Math.Min(_startIndex, endIndex), Math.Max(_startIndex, endIndex));
                _startIndex = -1;
            }
        }

        /// <summary>
        /// Логва редове за диапазон [startIndex, endIndex] в CSV файл. Комбинира IndicatorEngine метрики и (по избор) FeatureBuilder.
        /// Създава или апендва към файл, с фиксиран хедър и инвариантна култура. При нов файл изнася и schema JSON.
        /// </summary>
        public string LogRange(int startIndex, int endIndex, string fileTag = null)
        {
            if (endIndex < startIndex)
            {
                int t = startIndex;
                startIndex = endIndex;
                endIndex = t;
            }
            if (startIndex < 0 || endIndex < 0) return null;

            var rows = new List<IDictionary<string, float>>(Math.Max(0, endIndex - startIndex + 1));
            for (int i = startIndex; i <= endIndex; i++)
            {
                var combined = new Dictionary<string, float>(512, StringComparer.Ordinal);

                OhlcvMetrics ohlc = null;
                HeikenAshiMetrics ha = null;
                OhlcvBar bar = null;

                if (_getBar != null)
                {
                    bar = _getBar(i);
                    if (bar != null && _ohlcvEngine != null)
                    {
                        ohlc = _ohlcvEngine.ComputeNext(bar);
                        FlattenObject(ohlc, combined);
                    }
                    if (bar != null && _haEngine != null)
                    {
                        ha = _haEngine.ComputeNext(bar);
                        FlattenObject(ha, combined);
                    }
                    if (bar != null && _spikeEngine != null)
                    {
                        var spike = _spikeEngine.ComputeNext(bar);
                        FlattenObject(spike, combined);
                    }
                    if (bar != null && _hybridEngine != null)
                    {
                        var hybrid = _hybridEngine.ComputeNext(bar, ohlc, ha);
                        if (hybrid is IDictionary<string, double> dd)
                        {
                            foreach (var kv in dd) combined[kv.Key] = (float)kv.Value;
                        }
                        else if (hybrid is IDictionary<string, float> df)
                        {
                            foreach (var kv in df) combined[kv.Key] = kv.Value;
                        }
                        else if (hybrid != null)
                        {
                            FlattenObject(hybrid, combined);
                        }
                    }

                    // >>> ADD: IndicatorEngine extras (изчисляват се от барове + стандартни серии)
                    if (_extrasCfg != null && _barsRef != null)
                    {
                        var extras = Indicators.IndicatorEngine.ComputeExtrasFromBarsAsDict(i, _barsRef, _extrasCfg, _indSeries);
                        if (extras != null)
                        {
                            foreach (var kv in extras)
                                combined[kv.Key] = (float)kv.Value;
                        }
                    }
                }

                if (_indSeries != null && _indParams != null)
                {
                    var indRow = BuildIndicatorFeatures(i);
                    foreach (var kv in indRow) combined[kv.Key] = kv.Value;
                }

                if (_builder != null)
                {
                    var row = _builder(i);
                    if (row != null)
                    {
                        foreach (var kv in row) combined[kv.Key] = kv.Value;
                    }
                }

                if (_extraProviders.Count > 0)
                {
                    foreach (var (name, provider) in _extraProviders)
                    {
                        var ext = provider(i);
                        if (ext == null) continue;
                        foreach (var kv in ext) combined[kv.Key] = kv.Value;
                    }
                }

                if (combined.Count > 0)
                    rows.Add(combined);
            }
            if (rows.Count == 0) return null;

            string fileName = BuildFileName(startIndex, endIndex, fileTag);
            string path = Path.Combine(_logDirectory, fileName);

            if (!File.Exists(path))
            {
                WriteCsv(path, rows);
                if (_writeSchema)
                {
                    var header = DetermineHeader(rows[0]);
                    WriteSchema(path, header);
                }
            }
            else
            {
                AppendCsv(path, rows);
            }

            return path;
        }

        private string BuildFileName(int startIndex, int endIndex, string tag)
        {
            string dateStr = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            string name = _fileNameTemplate
                .Replace("{symbol}", _symbol)
                .Replace("{timeframe}", _timeframe)
                .Replace("{start}", startIndex.ToString())
                .Replace("{end}", endIndex.ToString())
                .Replace("{date}", dateStr);

            if (!string.IsNullOrWhiteSpace(tag))
            {
                string ext = Path.GetExtension(name);
                string stem = Path.GetFileNameWithoutExtension(name);
                name = $"{stem}_{tag}{ext}";
            }
            return name;
        }

        private void WriteCsv(string path, IList<IDictionary<string, float>> rows)
        {
            var header = DetermineHeader(rows[0]);
            using (var sw = new StreamWriter(path, false))
            {
                sw.WriteLine(string.Join(",", header));
                for (int r = 0; r < rows.Count; r++)
                    WriteRow(sw, header, rows[r]);
            }
        }

        private void AppendCsv(string path, IList<IDictionary<string, float>> rows)
        {
            string firstLine;
            using (var sr = new StreamReader(path))
                firstLine = sr.ReadLine();

            string[] header;
            if (string.IsNullOrWhiteSpace(firstLine))
            {
                header = DetermineHeader(rows[0]);
                using (var sw = new StreamWriter(path, false))
                    sw.WriteLine(string.Join(",", header));
                if (_writeSchema) WriteSchema(path, header);
            }
            else
            {
                header = firstLine.Split(',');
            }

            using (var sw = new StreamWriter(path, true))
            {
                for (int r = 0; r < rows.Count; r++)
                    WriteRow(sw, header, rows[r]);
            }
        }
        private string[] DetermineHeader(IDictionary<string, float> firstRow)
        {
            if (_headerOrder != null && _headerOrder.Length > 0)
                return _headerOrder;

            return firstRow.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray();
        }

        /// <summary>Записва schema JSON, съдържаща подредбата на колоните и базови метаданни.</summary>
        private void WriteSchema(string csvPath, string[] header)
        {
            try
            {
                var schema = new
                {
                    File = Path.GetFileName(csvPath),
                    CreatedUtc = DateTime.UtcNow,
                    Symbol = _symbol,
                    Timeframe = _timeframe,
                    CultureInvariant = _useInvariantCulture,
                    MissingPolicy = _missingPolicy.ToString(),
                    FileNameTemplate = _fileNameTemplate,
                    Engines = new
                    {
                        OHLC = _ohlcvEngine != null,
                        HeikenAshi = _haEngine != null,
                        Spike = _spikeEngine != null,
                        Hybrid = _hybridEngine != null,
                        Indicators = _indSeries != null,
                        Extras = _extrasCfg != null
                    },
                    FeatureProviders = _extraProviders.Select(p => p.name).ToArray(),
                    Columns = header
                };
                string schemaPath = csvPath + ".schema.json";
                var opts = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(schemaPath, JsonSerializer.Serialize(schema, opts));
            }
            catch { /* silent */ }
        }

        private void WriteRow(StreamWriter sw, string[] header, IDictionary<string, float> row)
        {
            var provider = _useInvariantCulture ? CultureInfo.InvariantCulture : CultureInfo.CurrentCulture;
            string[] vals = new string[header.Length];
            for (int i = 0; i < header.Length; i++)
            {
                float v;
                if (!row.TryGetValue(header[i], out v))
                    v = _missingPolicy == MissingValuePolicy.NaN ? float.NaN : 0f;
                vals[i] = v.ToString("G9", provider);
            }
            sw.WriteLine(string.Join(",", vals));
        }

        /// <summary>
        /// Строи пълен фийчър-ред от всички метрики на Indicators.IndicatorEngine за даден индекс.
        /// Включва основни стойности, производни, кросове, спредове и локални диапазони.
        /// </summary>
        private IDictionary<string, float> BuildIndicatorFeatures(int index)
        {
            var d = new Dictionary<string, float>(256, StringComparer.Ordinal);
            if (_indSeries == null || _indParams == null) return d;

            var snap = IndicatorEngine.ComputeSnapshot(index, _indSeries, _indParams);

            var t = typeof(IndicatorSnapshot);
            var fields = t.GetFields(BindingFlags.Public | BindingFlags.Instance);

            foreach (var f in fields)
            {
                var val = f.GetValue(snap);
                if (val == null) continue;

                if (f.FieldType == typeof(Derivative))
                {
                    var df = (Derivative)val;
                    d[$"{f.Name}_Delta"] = (float)df.Delta;
                    d[$"{f.Name}_Slope"] = (float)df.Slope;
                    d[$"{f.Name}_AngleDeg"] = (float)df.AngleDeg;
                    d[$"{f.Name}_Acceleration"] = (float)df.Acceleration;
                }
                else if (val is double dv)
                {
                    d[f.Name] = (float)dv;
                }
                else if (val is int iv)
                {
                    d[f.Name] = iv;
                }
            }

            return d;
        }

        /// <summary>
        /// Универсално flatten-ване на DTO: публични свойства/полета към ключ-стойност числа.
        /// Поддържа double/float/int/long/bool/enum и Nullable<>
        /// </summary>
        private static void FlattenObject(object dto, IDictionary<string, float> target)
        {
            if (dto == null) return;

            var t = dto.GetType();
            foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!p.CanRead) continue;
                var v = p.GetValue(dto);
                AddNumericValue(p.Name, v, target);
            }
            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var v = f.GetValue(dto);
                AddNumericValue(f.Name, v, target);
            }
        }

        private static void AddNumericValue(string name, object v, IDictionary<string, float> target)
        {
            if (v == null) return;
            var type = Nullable.GetUnderlyingType(v.GetType()) ?? v.GetType();
            if (type == typeof(double)) { target[name] = (float)(double)v; return; }
            if (type == typeof(float)) { target[name] = (float)v; return; }
            if (type == typeof(int)) { target[name] = (int)v; return; }
            if (type == typeof(long)) { target[name] = (float)(long)v; return; }
            if (type == typeof(bool)) { target[name] = (bool)v ? 1f : 0f; return; }
            if (type.IsEnum) { target[name] = Convert.ToSingle((int)v, CultureInfo.InvariantCulture); return; }
            // Ignore non-numeric
        }
    }
}
