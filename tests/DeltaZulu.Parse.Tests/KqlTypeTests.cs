namespace DeltaZulu.Parse.Tests;

/// <summary>
/// Pins the KQL scalar type (see <see cref="KqlType"/>) each motif's output
/// is tagged with, and the two places that type can't be decided purely
/// from which parser matched: the ".." unwrap on a user-defined type (the
/// tag travels with the inner <c>FieldValue</c>, so it survives the
/// unwrap for free) and the "." splice of a structured motif's embedded
/// object (inferred per key from the actual JSON value).
/// </summary>
[TestClass]
public class KqlTypeTests
{
    [TestMethod]
    public void CustomType_DefaultsToDynamicWhenNotCollapsedToAScalar()
    {
        var ctx = Load("""
            type=@IPaddr:%ip:ipv4%
            rule=:an ip address %addr:@IPaddr%
            """);
        ctx.Parse("an ip address 10.0.0.1", out ParseResult result);

        Assert.IsTrue(result.Matched);
        Assert.IsTrue(result.TryGetKqlType("addr", out var type));
        Assert.AreEqual(KqlType.Dynamic, type, "a multi-field (here: named-field) custom type instantiation is an object");
    }

    [TestMethod]
    public void CustomType_DotDotUnwrap_InheritsInnerScalarType()
    {
        /* the outer @IPaddr instantiation is Dynamic by default, but the
         * ".."-named field unwraps it: "addr" ends up holding ipv4's own
         * value and type directly, not the type of the wrapping object */
        var ctx = Load("""
            type=@IPaddr:%..:ipv4%
            rule=:an ip address %addr:@IPaddr%
            """);
        ctx.Parse("an ip address 10.0.0.1", out ParseResult result);

        Assert.IsTrue(result.Matched);
        Assert.IsTrue(result.TryGetKqlType("addr", out var type));
        Assert.AreEqual(KqlType.String, type);
    }

    [TestMethod]
    public void CustomType_DotDotUnwrap_InheritsNonStringInnerType()
    {
        /* same unwrap, but through a format=number field, so the inherited
         * type is meaningfully different from the wrapping wildcard default */
        var ctx = Load("""
            type=@num:%..:number{"format":"number"}%
            rule=:count is %n:@num%
            """);
        ctx.Parse("count is 42", out ParseResult result);

        Assert.IsTrue(result.Matched);
        Assert.IsTrue(result.TryGetKqlType("n", out var type));
        Assert.AreEqual(KqlType.Long, type);
    }

    [TestMethod]
    public void DotSplice_InfersPerKeyTypesFromEmbeddedJsonObject()
    {
        var ctx = Load("""rule=:data %.:json%""");
        ctx.Parse("""data {"a": "x", "b": 1, "c": true, "d": {"nested": 1}}""", out ParseResult result);

        Assert.IsTrue(result.Matched);
        Assert.IsTrue(result.TryGetKqlType("a", out var a));
        Assert.AreEqual(KqlType.String, a);
        Assert.IsTrue(result.TryGetKqlType("b", out var b));
        Assert.AreEqual(KqlType.Long, b);
        Assert.IsTrue(result.TryGetKqlType("c", out var c));
        Assert.AreEqual(KqlType.Bool, c);
        Assert.IsTrue(result.TryGetKqlType("d", out var d));
        Assert.AreEqual(KqlType.Dynamic, d);
    }

    [TestMethod]
    public void MissingField_TryGetKqlTypeReturnsFalse()
    {
        var ctx = Load("rule=:hello %w:word%");
        ctx.Parse("hello world", out ParseResult result);

        Assert.IsFalse(result.TryGetKqlType("missing", out var type));
        Assert.AreEqual(KqlType.Unknown, type);
    }

    [TestMethod]
    [DataRow("rule=:%w:word%", "hello", "w", KqlType.String, DisplayName = "word -> String")]
    [DataRow("""rule=:n %num:number%""", "n 42", "num", KqlType.String, DisplayName = "number, default format -> String")]
    [DataRow("""rule=:n %{"name":"num", "type":"number", "format":"number"}%""", "n 42", "num", KqlType.Long, DisplayName = "number, format=number -> Long")]
    [DataRow("""rule=:f %{"name":"val", "type":"float", "format":"number"}%""", "f 15.9", "val", KqlType.Real, DisplayName = "float, format=number -> Real")]
    [DataRow("""rule=:h %{"name":"val", "type":"hexnumber", "format":"number"}% t""", "h 0x1234 t", "val", KqlType.Long, DisplayName = "hexnumber, format=number -> Long")]
    [DataRow("rule=:%ts:date-rfc3164% %h:word%", "Oct 29 09:47:08 myhost", "ts", KqlType.String, DisplayName = "date-rfc3164, default format -> String")]
    [DataRow("""rule=:%{"name":"ts", "type":"date-rfc3164", "format":"timestamp-unix"}% %h:word%""", "Oct 29 09:47:08 myhost", "ts", KqlType.Long, DisplayName = "date-rfc3164, format=timestamp-unix -> Long")]
    [DataRow("""rule=:count is %{"name":"n", "type":"repeat", "parser": {"type":"number"}, "while": {"type":"literal", "text":","} }%""", "count is 1,2,3", "n", KqlType.Dynamic, DisplayName = "repeat -> Dynamic")]
    [DataRow("""rule=:data %fields:json%""", """data {"a": 1}""", "fields", KqlType.Dynamic, DisplayName = "json -> Dynamic")]
    public void MotifOutput_HasExpectedKqlType(string rulebase, string message, string fieldName, KqlType expected)
    {
        var ctx = Load(rulebase);
        var r = ctx.Parse(message, out ParseResult result);

        Assert.AreEqual(0, r, $"message did not match: {message}");
        Assert.IsTrue(result.TryGetKqlType(fieldName, out var actual), $"field '{fieldName}' not present");
        Assert.AreEqual(expected, actual);
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
