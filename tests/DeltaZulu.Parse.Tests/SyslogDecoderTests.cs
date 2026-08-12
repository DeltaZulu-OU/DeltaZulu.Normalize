using System.Text.Json.Nodes;

namespace DeltaZulu.Parse.Tests;

/// <summary>
/// Tests for <see cref="SyslogDecoder"/> — the RFC 3164/5424 envelope
/// pre-decoder added by docs/adr/0006-syslog-envelope-predecoder.md. This
/// is deliberately independent of the PDAG/rulebase engine: it runs before
/// <see cref="ParseContext.Parse(string, out JsonObject)"/>, never inside it.
/// </summary>
[TestClass]
public class SyslogDecoderTests
{
    [TestMethod]
    public void Rfc3164_NoPriTag_DecodesAndPreservesLeadingSpaceInMsg()
    {
        /* the exact log line that prompted this feature: Sagan's Snort rule
         * requires a leading space before "[1:...", matching what rsyslog's
         * own $msg would contain once "snort: " is stripped. */
        var ok = SyslogDecoder.TryDecode(
            "Jun  2 00:41:47 demo snort: [1:19559:5] INDICATOR-SCAN SSH brute force login attempt",
            out var env);

        Assert.IsTrue(ok);
        Assert.AreEqual(SyslogFraming.Rfc3164, env.Framing);
        Assert.AreEqual("demo", env.Host);
        Assert.AreEqual("snort", env.AppName);
        Assert.IsNull(env.ProcId);
        Assert.IsNull(env.Facility);
        Assert.IsNull(env.Severity);
        Assert.AreEqual(" [1:19559:5] INDICATOR-SCAN SSH brute force login attempt", env.Msg);
        Assert.AreEqual(6, env.Timestamp!.Value.Month);
        Assert.AreEqual(2, env.Timestamp!.Value.Day);
        Assert.AreEqual(new TimeSpan(0, 41, 47), env.Timestamp!.Value.TimeOfDay);
    }

    [TestMethod]
    public void Rfc3164_WithPriAndPid_DecodesFacilitySeverityAndProcId()
    {
        var ok = SyslogDecoder.TryDecode("<34>Oct 11 22:14:15 mymachine su[1234]: 'su root' failed for lonvick on /dev/pts/8", out var env);

        Assert.IsTrue(ok);
        Assert.AreEqual(SyslogFraming.Rfc3164, env.Framing);
        Assert.AreEqual(4, env.Facility);
        Assert.AreEqual(2, env.Severity);
        Assert.AreEqual("mymachine", env.Host);
        Assert.AreEqual("su", env.AppName);
        Assert.AreEqual("1234", env.ProcId);
        Assert.AreEqual(" 'su root' failed for lonvick on /dev/pts/8", env.Msg);
    }

    [TestMethod]
    public void Rfc3164_TaglessMessage_HasNoAppNameButStillDecodes()
    {
        var ok = SyslogDecoder.TryDecode("Aug 12 09:00:00 host just a plain message with no tag", out var env);

        Assert.IsTrue(ok);
        Assert.AreEqual(SyslogFraming.Rfc3164, env.Framing);
        Assert.AreEqual("host", env.Host);
        Assert.IsNull(env.AppName);
        Assert.IsNull(env.ProcId);
        Assert.AreEqual("just a plain message with no tag", env.Msg);
    }

    [TestMethod]
    public void Rfc5424_CanonicalExample_DecodesAllFields()
    {
        /* RFC 5424 §6.5 example 1, verbatim (including the UTF-8 BOM the
         * spec itself puts at the start of MSG). */
        var ok = SyslogDecoder.TryDecode(
            "<165>1 2003-10-11T22:14:15.003Z mymachine.example.com evntslog - ID47 " +
            "[exampleSDID@32473 iut=\"3\" eventSource=\"Application\" eventID=\"1011\"] " +
            "﻿An application event log entry",
            out var env);

        Assert.IsTrue(ok);
        Assert.AreEqual(SyslogFraming.Rfc5424, env.Framing);
        Assert.AreEqual(20, env.Facility);
        Assert.AreEqual(5, env.Severity);
        Assert.AreEqual("mymachine.example.com", env.Host);
        Assert.AreEqual("evntslog", env.AppName);
        Assert.IsNull(env.ProcId);
        Assert.AreEqual("ID47", env.MsgId);
        Assert.AreEqual("[exampleSDID@32473 iut=\"3\" eventSource=\"Application\" eventID=\"1011\"]", env.StructuredData);
        Assert.AreEqual("﻿An application event log entry", env.Msg);
        Assert.AreEqual(new DateTimeOffset(2003, 10, 11, 22, 14, 15, 3, TimeSpan.Zero), env.Timestamp);
    }

    [TestMethod]
    public void Rfc5424_NoStructuredDataOrMsgId_NilFieldsAreNull()
    {
        /* RFC 5424 §6.5 example 2: PROCID present, MSGID and STRUCTURED-DATA
         * both NILVALUE. */
        var ok = SyslogDecoder.TryDecode(
            "<165>1 2003-08-24T05:14:15.000003-07:00 192.0.2.1 myproc 8710 - - %% It's time to make the do-nuts.",
            out var env);

        Assert.IsTrue(ok);
        Assert.AreEqual(SyslogFraming.Rfc5424, env.Framing);
        Assert.AreEqual("192.0.2.1", env.Host);
        Assert.AreEqual("myproc", env.AppName);
        Assert.AreEqual("8710", env.ProcId);
        Assert.IsNull(env.MsgId);
        Assert.IsNull(env.StructuredData);
        Assert.AreEqual("%% It's time to make the do-nuts.", env.Msg);
    }

    [TestMethod]
    public void Rfc5424_MultipleStructuredDataElements_CapturedAsOneRawBlock()
    {
        var ok = SyslogDecoder.TryDecode(
            "<165>1 2003-10-11T22:14:15.003Z host app - - [a@1 x=\"1\"][b@2 y=\"2\"] the message",
            out var env);

        Assert.IsTrue(ok);
        Assert.AreEqual("[a@1 x=\"1\"][b@2 y=\"2\"]", env.StructuredData);
        Assert.AreEqual("the message", env.Msg);
    }

    [TestMethod]
    public void Rfc5424_EmptyMessageBody_YieldsEmptyMsg()
    {
        var ok = SyslogDecoder.TryDecode("<165>1 2003-10-11T22:14:15.003Z host app - - -", out var env);

        Assert.IsTrue(ok);
        Assert.AreEqual(string.Empty, env.Msg);
    }

    [TestMethod]
    public void NonSyslogLine_DecodesAsNoneWithMsgUnchanged()
    {
        /* the Citrix Netscaler log from the same investigation: it looks
         * date-like ("16:04:31 GMT ...") but isn't RFC3164/5424 framing at
         * all -- a caller must be able to pass it through unaffected. */
        const string line = "16:04:31 GMT server1 PPE-1 : AAA LOGIN_FAILED 71011157 :  User bob - Client_ip 12.12.12.12";

        var ok = SyslogDecoder.TryDecode(line, out var env);

        Assert.IsFalse(ok);
        Assert.AreEqual(SyslogFraming.None, env.Framing);
        Assert.AreEqual(line, env.Msg);
        Assert.IsNull(env.Host);
        Assert.IsNull(env.Timestamp);
    }

    [TestMethod]
    public void EmptyLine_DecodesAsNone()
    {
        var ok = SyslogDecoder.TryDecode(string.Empty, out var env);

        Assert.IsFalse(ok);
        Assert.AreEqual(SyslogFraming.None, env.Framing);
        Assert.AreEqual(string.Empty, env.Msg);
    }

    [TestMethod]
    public void MalformedPri_FallsBackToNoneWithOriginalLineIntact()
    {
        /* "<" followed by something that isn't a well-formed 1-3-digit PRI:
         * the whole line must come back unchanged, not partially consumed. */
        const string line = "<not-a-pri>Jun 2 00:41:47 host tag: msg";

        var ok = SyslogDecoder.TryDecode(line, out var env);

        Assert.IsFalse(ok);
        Assert.AreEqual(SyslogFraming.None, env.Framing);
        Assert.AreEqual(line, env.Msg);
    }

    [TestMethod]
    public void DecodedMessageBody_FeedsStraightIntoARulebase()
    {
        /* end-to-end: decode, then hand Msg to the exact kind of rule this
         * feature exists for (leading-space-dependent, Sagan-style). */
        const string rb = "rule=: [%generator_id:number%:%sig_id:number%:%rev:number%] %sig_name:rest%";
        var ctx = new ParseContext();
        Assert.AreEqual(0, ctx.LoadSamplesFromString(rb));

        var ok = SyslogDecoder.TryDecode(
            "Jun  2 00:41:47 demo snort: [1:19559:5] INDICATOR-SCAN SSH brute force login attempt",
            out var env);
        Assert.IsTrue(ok);

        Assert.AreEqual(0, ctx.Parse(env.Msg, out JsonObject j));
        Assert.AreEqual("19559", j["sig_id"]!.GetValue<string>());
    }
}
