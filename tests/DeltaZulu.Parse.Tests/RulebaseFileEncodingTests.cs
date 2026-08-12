using System.Text;
using System.Text.Json.Nodes;

namespace DeltaZulu.Parse.Tests;

/// <summary>
/// Tests for how <see cref="RulebaseLoader"/> reads a rulebase file off disk:
/// newline convention (LF, CRLF, and lone CR) and byte-level encoding (UTF-8
/// with/without BOM, UTF-16/UTF-32 with BOM, and rejection of bytes that
/// aren't valid text in the detected encoding). These are file-level
/// concerns that <see cref="ParseContext.LoadSamplesFromString"/> never
/// exercises, since it hands the loader an already-decoded string.
/// </summary>
[TestClass]
public class RulebaseFileEncodingTests
{
    private string _root = "";

    [TestInitialize]
    public void CreateTempDirectory()
    {
        _root = Path.Combine(Path.GetTempPath(), "dznorm-enc-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [TestCleanup]
    public void DeleteTempDirectory()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            /* best effort */
        }
    }

    [TestMethod]
    public void LoneCrLineEndings_LoadAndMatch()
    {
        /* old Mac (CR-only) text files: the same silent-corruption risk as
         * CRLF (a real production rulebase carrying this in the wild is what
         * prompted this whole test class), but for the newline the CRLF fix
         * doesn't cover. Every "\n" below is a lone "\r" on disk. */
        var body = "version=2\rrule=:duration %field:duration% bytes\rrule=:duration %field:duration%\r";
        var path = Path.Combine(_root, "cr-only.rulebase");
        File.WriteAllBytes(path, Encoding.UTF8.GetBytes(body));

        var errors = new List<string>();
        var ctx = new ParseContext { ErrorCallback = errors.Add };
        Assert.AreEqual(0, ctx.LoadSamples(path), string.Join("; ", errors));

        Assert.AreEqual(0, ctx.Parse("duration 0:00:42 bytes", out JsonObject j));
        Assert.AreEqual("0:00:42", j["field"]!.GetValue<string>());
    }

    [TestMethod]
    public void LoneCrLineEndings_VersionHeaderStillEnforced()
    {
        var path = Path.Combine(_root, "cr-only-bad-header.rulebase");
        File.WriteAllBytes(path, Encoding.UTF8.GetBytes("not the header\rrule=:x %a:word%\r"));

        var errors = new List<string>();
        var ctx = new ParseContext { ErrorCallback = errors.Add };
        Assert.AreNotEqual(0, ctx.LoadSamples(path));
        Assert.Contains(e => e.Contains("must be version 2"), errors, string.Join("; ", errors));
    }

    [TestMethod]
    public void Utf8Bom_IsStrippedAndLoads()
    {
        var path = Path.Combine(_root, "utf8-bom.rulebase");
        var bytes = Encoding.UTF8.GetPreamble()
            .Concat(Encoding.UTF8.GetBytes("version=2\nrule=:city café %a:word%\n"))
            .ToArray();
        File.WriteAllBytes(path, bytes);

        var errors = new List<string>();
        var ctx = new ParseContext { ErrorCallback = errors.Add };
        Assert.AreEqual(0, ctx.LoadSamples(path), string.Join("; ", errors));

        Assert.AreEqual(0, ctx.Parse("city café x", out JsonObject j));
        Assert.AreEqual("x", j["a"]!.GetValue<string>());
    }

    [TestMethod]
    public void Utf16LeBom_IsDecodedAndLoads()
    {
        var path = Path.Combine(_root, "utf16le-bom.rulebase");
        var bytes = Encoding.Unicode.GetPreamble() // FF FE
            .Concat(Encoding.Unicode.GetBytes("version=2\nrule=:speed %a:word%\n"))
            .ToArray();
        File.WriteAllBytes(path, bytes);

        var errors = new List<string>();
        var ctx = new ParseContext { ErrorCallback = errors.Add };
        Assert.AreEqual(0, ctx.LoadSamples(path), string.Join("; ", errors));

        Assert.AreEqual(0, ctx.Parse("speed 42", out JsonObject j));
        Assert.AreEqual("42", j["a"]!.GetValue<string>());
    }

    [TestMethod]
    public void Utf16BeBom_IsDecodedAndLoads()
    {
        var path = Path.Combine(_root, "utf16be-bom.rulebase");
        var bytes = Encoding.BigEndianUnicode.GetPreamble() // FE FF
            .Concat(Encoding.BigEndianUnicode.GetBytes("version=2\nrule=:speed %a:word%\n"))
            .ToArray();
        File.WriteAllBytes(path, bytes);

        var errors = new List<string>();
        var ctx = new ParseContext { ErrorCallback = errors.Add };
        Assert.AreEqual(0, ctx.LoadSamples(path), string.Join("; ", errors));

        Assert.AreEqual(0, ctx.Parse("speed 42", out JsonObject j));
        Assert.AreEqual("42", j["a"]!.GetValue<string>());
    }

    [TestMethod]
    public void InvalidUtf8Bytes_FailsLoadWithClearDiagnostic()
    {
        /* a lone 0x81 is not a valid UTF-8 lead or continuation byte in any
         * position; File.ReadAllText would silently swap it for U+FFFD and
         * report success. That would compile fine and then just never match
         * any real message -- exactly the failure mode this check exists to
         * turn into a load-time error instead. */
        var path = Path.Combine(_root, "bad-utf8.rulebase");
        var prefix = Encoding.UTF8.GetBytes("version=2\nrule=:bad ");
        var badByte = new byte[] { 0x81 };
        var suffix = Encoding.UTF8.GetBytes(" %a:word%\n");
        File.WriteAllBytes(path, prefix.Concat(badByte).Concat(suffix).ToArray());

        var errors = new List<string>();
        var ctx = new ParseContext { ErrorCallback = errors.Add };
        Assert.AreNotEqual(0, ctx.LoadSamples(path));
        Assert.Contains(e => e.Contains("not valid UTF-8"), errors, string.Join("; ", errors));
    }

    [TestMethod]
    public void CrlfLineEndings_StillWorkAlongsideEncodingCheck()
    {
        /* regression guard: the encoding rewrite must not disturb the
         * existing CRLF handling. */
        var path = Path.Combine(_root, "crlf.rulebase");
        File.WriteAllBytes(path, Encoding.UTF8.GetBytes("version=2\r\nrule=:x %a:word%\r\n"));

        var errors = new List<string>();
        var ctx = new ParseContext { ErrorCallback = errors.Add };
        Assert.AreEqual(0, ctx.LoadSamples(path), string.Join("; ", errors));

        Assert.AreEqual(0, ctx.Parse("x hello", out JsonObject j));
        Assert.AreEqual("hello", j["a"]!.GetValue<string>());
    }
}
