using DeltaZulu.Parse;

namespace DeltaZulu.Normalize.Tests;

/// <summary>
/// Pins <see cref="RecordNormalizer.Normalize"/>'s core contract: every
/// <see cref="NormalizedField.Value"/> is the native CLR type its
/// <see cref="NormalizedField.Type"/> promises, with no residual
/// <c>System.Text.Json</c> types leaking through except inside a
/// <see cref="KqlType.Dynamic"/> field's plain object graph.
/// </summary>
[TestClass]
public class RecordNormalizerTests
{
    [TestMethod]
    public void DateIso_FormatDatetime_YieldsBoxedDateTimeOffset()
    {
        var ctx = Load("""rule=:day %{"name":"d", "type":"date-iso", "format":"datetime"}%""");
        ctx.Parse("day 2024-01-15", out ParseResult result);

        var record = RecordNormalizer.Normalize(result);

        Assert.IsTrue(record.TryGetField("d", out var field));
        Assert.AreEqual(KqlType.DateTime, field.Type);
        Assert.AreEqual(new DateTimeOffset(2024, 1, 15, 0, 0, 0, TimeSpan.Zero), field.Value);
        Assert.IsInstanceOfType<DateTimeOffset>(field.Value);
    }

    [TestMethod]
    public void Duration_FormatTimespan_YieldsBoxedTimeSpan()
    {
        var ctx = Load("""rule=:duration %{"name":"d", "type":"duration", "format":"timespan"}% bytes""");
        ctx.Parse("duration 37:59:42 bytes", out ParseResult result);

        var record = RecordNormalizer.Normalize(result);

        Assert.IsTrue(record.TryGetField("d", out var field));
        Assert.AreEqual(KqlType.Timespan, field.Type);
        Assert.AreEqual(new TimeSpan(37, 59, 42), field.Value);
        Assert.IsInstanceOfType<TimeSpan>(field.Value);
    }

    [TestMethod]
    public void NumberFormatNumber_YieldsBoxedLong()
    {
        var ctx = Load("""rule=:n %{"name":"num", "type":"number", "format":"number"}%""");
        ctx.Parse("n 42", out ParseResult result);

        var record = RecordNormalizer.Normalize(result);

        Assert.IsTrue(record.TryGetField("num", out var field));
        Assert.AreEqual(KqlType.Long, field.Type);
        Assert.AreEqual(42L, field.Value);
        Assert.IsInstanceOfType<long>(field.Value);
    }

    [TestMethod]
    public void PlainStringField_PrefersZeroCopyRawText()
    {
        var ctx = Load("rule=:hello %w:word%");
        const string message = "hello world";
        ctx.Parse(message, out ParseResult result);

        var record = RecordNormalizer.Normalize(result);

        Assert.IsTrue(record.TryGetField("w", out var field));
        Assert.AreEqual(KqlType.String, field.Type);
        Assert.AreEqual("world", field.Value);
        Assert.IsInstanceOfType<string>(field.Value);
    }

    [TestMethod]
    public void DynamicField_ConvertsToPlainDictionaryGraph_NotJsonNode()
    {
        var ctx = Load("""rule=:data %fields:json%""");
        ctx.Parse("""data {"a": "x", "b": 1, "c": true, "d": [1, 2, 3]}""", out ParseResult result);

        var record = RecordNormalizer.Normalize(result);

        Assert.IsTrue(record.TryGetField("fields", out var field));
        Assert.AreEqual(KqlType.Dynamic, field.Type);
        var dict = Assert.IsInstanceOfType<Dictionary<string, object?>>(field.Value);
        Assert.AreEqual("x", dict["a"]);
        Assert.AreEqual(1L, dict["b"]);
        Assert.AreEqual(true, dict["c"]);
        var list = Assert.IsInstanceOfType<List<object?>>(dict["d"]);
        CollectionAssert.AreEqual(new object?[] { 1L, 2L, 3L }, list);
    }

    [TestMethod]
    public void FailedMatch_CarriesOriginalmsgAsStringAndMatchedFalse()
    {
        var ctx = Load("rule=:hello %w:word%");
        ctx.Parse("goodbye world", out ParseResult result);

        var record = RecordNormalizer.Normalize(result);

        Assert.IsFalse(record.Matched);
        Assert.IsTrue(record.TryGetField("originalmsg", out var orig));
        Assert.AreEqual(KqlType.String, orig.Type);
        Assert.AreEqual("goodbye world", orig.Value);
    }

    [TestMethod]
    public void Enumeration_PreservesParseResultFieldOrder()
    {
        var ctx = Load("rule=:hello %first:word% %second:word%");
        ctx.Parse("hello foo bar", out ParseResult result);

        var record = RecordNormalizer.Normalize(result);

        Assert.AreEqual(result.Count, record.Count);
        for (var i = 0; i < result.Count; ++i)
        {
            Assert.AreEqual(result.GetName(i), record[i].Name);
        }

        CollectionAssert.AreEqual(
            record.Select(f => f.Name).ToList(),
            record.ToList().Select(f => f.Name).ToList());
    }

    [TestMethod]
    public void MissingField_TryGetFieldReturnsFalse()
    {
        var ctx = Load("rule=:hello %w:word%");
        ctx.Parse("hello world", out ParseResult result);

        var record = RecordNormalizer.Normalize(result);

        Assert.IsFalse(record.TryGetField("missing", out var field));
        Assert.AreEqual(default, field);
    }

    private static ParseContext Load(string rulebase, ParseOptions options = ParseOptions.None)
    {
        var ctx = new ParseContext { Options = options };
        var errors = new List<string>();
        ctx.ErrorCallback = errors.Add;
        Assert.AreEqual(0, ctx.LoadSamplesFromString(rulebase), $"rulebase failed to load: {string.Join("; ", errors)}");
        return ctx;
    }
}
