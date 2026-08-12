using System.Globalization;
using DeltaZulu.Parse.Parsers;

namespace DeltaZulu.Parse;

/// <summary>
/// Which syslog transport framing (if any) <see cref="SyslogDecoder.TryDecode"/>
/// recognized around a message body.
/// </summary>
public enum SyslogFraming
{
    /// <summary>No recognized syslog envelope; the input is used unchanged as the message body.</summary>
    None,

    /// <summary>RFC 3164 ("BSD syslog"): optional "&lt;PRI&gt;", a month-name timestamp, hostname, and "tag[pid]:".</summary>
    Rfc3164,

    /// <summary>RFC 5424 ("the syslog protocol"): "&lt;PRI&gt;1 " followed by an ISO-8601 timestamp, hostname, app-name, procid, msgid, and structured data.</summary>
    Rfc5424,
}

/// <summary>
/// <para>
/// The result of decoding a raw log line's syslog envelope via
/// <see cref="SyslogDecoder.TryDecode"/>: the transport-level fields RFC
/// 3164 or RFC 5424 framing carries around an actual message body, plus
/// that body itself (<see cref="Msg"/>).
/// </para>
/// <para>
/// This has no upstream liblognorm equivalent: liblognorm normalizes an
/// already-extracted message string and has no concept of syslog framing
/// at all — see docs/COMPARISON.md and
/// docs/adr/0006-syslog-envelope-predecoder.md for why this exists and
/// where the line is drawn between this and the rulebase/PDAG engine.
/// </para>
/// </summary>
public readonly struct SyslogEnvelope
{
    internal SyslogEnvelope(SyslogFraming framing, int? facility, int? severity,
        DateTimeOffset? timestamp, string? host, string? appName, string? procId,
        string? msgId, string? structuredData, string msg)
    {
        Framing = framing;
        Facility = facility;
        Severity = severity;
        Timestamp = timestamp;
        Host = host;
        AppName = appName;
        ProcId = procId;
        MsgId = msgId;
        StructuredData = structuredData;
        Msg = msg;
    }

    /// <summary>The application/process name: RFC 3164's "tag", or RFC 5424's "APP-NAME".</summary>
    public string? AppName { get; }

    /// <summary>The PRI value's facility (0-23), when a "&lt;PRI&gt;" prefix was present; null otherwise (most files written by a local syslogd have already stripped it).</summary>
    public int? Facility { get; }

    /// <summary>
    /// Which framing matched. <see cref="SyslogFraming.None"/> means the
    /// input wasn't syslog-framed at all: every other property is null and
    /// <see cref="Msg"/> is the original input, unchanged.
    /// </summary>
    public SyslogFraming Framing { get; }

    /// <summary>The sending host/hostname/IP, or null if the field was RFC 5424's NILVALUE ("-").</summary>
    public string? Host { get; }

    /// <summary>
    /// The message body — what a rulebase should be matched against. For
    /// RFC 3164 framing this preserves whatever separator actually followed
    /// the header in the input (normally a single space after "tag:"),
    /// rather than trimming it, because real-world rulebases (e.g. Sagan's)
    /// are written assuming that leading space is still there, matching
    /// what rsyslog's own <c>$msg</c> property contains.
    /// </summary>
    public string Msg { get; }

    /// <summary>RFC 5424's MSGID, or null if absent/NILVALUE. Always null for RFC 3164 framing.</summary>
    public string? MsgId { get; }

    /// <summary>The process ID: RFC 3164's optional "tag[pid]", or RFC 5424's PROCID.</summary>
    public string? ProcId { get; }

    /// <summary>The PRI value's severity (0-7), when a "&lt;PRI&gt;" prefix was present; null otherwise.</summary>
    public int? Severity { get; }

    /// <summary>
    /// RFC 5424's raw STRUCTURED-DATA text (one or more "[...]" elements,
    /// unparsed beyond bracket/quote balancing), or null if absent/NILVALUE.
    /// Always null for RFC 3164 framing, which has no structured-data
    /// concept.
    /// </summary>
    public string? StructuredData { get; }

    /// <summary>
    /// The envelope timestamp, when present. RFC 3164's wire format has no
    /// year or time zone; both are filled in (current UTC year, UTC offset)
    /// the same way this port's "date-rfc3164" motif does
    /// (<see cref="DateTimeParsers.ParseRfc3164"/>), so the two agree.
    /// </summary>
    public DateTimeOffset? Timestamp { get; }
}

/// <summary>
/// Decodes the RFC 3164 ("BSD syslog") or RFC 5424 ("the syslog protocol")
/// envelope framing a raw log line may carry around its actual message
/// body — a preprocessing step a caller opts into explicitly before
/// <see cref="ParseContext.Parse(string, out System.Text.Json.Nodes.JsonObject)"/>,
/// never run implicitly by the rulebase/PDAG engine itself. See
/// docs/adr/0006-syslog-envelope-predecoder.md.
/// </summary>
public static class SyslogDecoder
{
    private static readonly char[] Rfc3164Terminators = [' ', ':', '['];

    /// <summary>
    /// Attempt to decode <paramref name="line"/>'s syslog envelope. Returns
    /// false when nothing recognizable as RFC 3164 or RFC 5424 framing was
    /// found, in which case <paramref name="envelope"/>.Framing is
    /// <see cref="SyslogFraming.None"/> and its Msg is <paramref name="line"/>
    /// unchanged — safe to use either way without checking the return value
    /// first.
    /// </summary>
    public static bool TryDecode(string line, out SyslogEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(line);

        var len = line.Length;
        var i = 0;
        if (!TryParsePri(line, ref i, len, out var facility, out var severity))
        {
            envelope = NoFraming(line);
            return false;
        }

        if (facility.HasValue && i + 1 < len && line[i] == '1' && line[i + 1] == ' '
            && TryDecodeRfc5424(line, i + 2, len, facility, severity, out envelope))
        {
            return true;
        }

        if (TryDecodeRfc3164(line, i, len, facility, severity, out envelope))
        {
            return true;
        }

        envelope = NoFraming(line);
        return false;
    }

    private static SyslogEnvelope NoFraming(string line)
        => new(SyslogFraming.None, null, null, null, null, null, null, null, null, line);

    private static int ReadDigits(string s, ref int i, int len, int maxDigits)
    {
        var start = i;
        while (i < len && i - start < maxDigits && TextRules.IsDigit(s[i]))
        {
            i++;
        }

        if (i == start)
        {
            return -1;
        }

        var val = 0;
        for (var k = start; k < i; k++)
        {
            val = (val * 10) + (s[k] - '0');
        }

        return val;
    }

    /// <summary>Reads a space-terminated RFC 5424 token; "-" reads as null (NILVALUE). Returns false if there was no token to read at all (a required field is missing).</summary>
    private static bool ReadNilableToken(string s, ref int i, int len, out string? token)
    {
        var start = i;
        while (i < len && s[i] != ' ')
        {
            i++;
        }

        if (i == start)
        {
            token = null;
            return false;
        }

        var raw = s[start..i];
        token = raw == "-" ? null : raw;
        return true;
    }

    private static bool TryDecodeRfc3164(string s, int i, int len, int? facility, int? severity, out SyslogEnvelope envelope)
    {
        envelope = default;
        if (!TryParseRfc3164Timestamp(s, ref i, len, out var timestamp))
        {
            return false;
        }

        if (i >= len || s[i] != ' ')
        {
            return false;
        }

        i++;
        var hostStart = i;
        while (i < len && s[i] != ' ')
        {
            i++;
        }

        if (i == hostStart || i >= len)
        {
            return false; /* empty hostname, or nothing follows it */
        }

        var host = s[hostStart..i];
        i++; /* the space after HOSTNAME */

        var tagStart = i;
        var j = i;
        while (j < len && Array.IndexOf(Rfc3164Terminators, s[j]) < 0)
        {
            j++;
        }

        string? appName = null;
        string? procId = null;
        if (j < len && s[j] == '[')
        {
            var pidStart = j + 1;
            var k = pidStart;
            while (k < len && TextRules.IsDigit(s[k]))
            {
                k++;
            }

            if (k > pidStart && k < len && s[k] == ']')
            {
                appName = s[tagStart..j];
                procId = s[pidStart..k];
                j = k + 1; /* now positioned right after ']', expecting ':' next */
            }
            else
            {
                /* "[" wasn't a well-formed "[pid]" — fall back to treating the
                 * whole tag region up to ':' (or a space, if there's no
                 * colon) as an opaque, un-split tag. */
                j = tagStart;
                while (j < len && s[j] != ' ' && s[j] != ':')
                {
                    j++;
                }
            }
        }

        string msg;
        if (j < len && s[j] == ':')
        {
            appName ??= s[tagStart..j];
            msg = s[(j + 1)..]; /* preserve whatever follows verbatim, incl. the usual single leading space */
        }
        else
        {
            /* tag-less message: a common, accepted deviation. Nothing after
             * HOSTNAME's separating space was consumed as a tag. */
            appName = null;
            procId = null;
            msg = s[tagStart..];
        }

        envelope = new SyslogEnvelope(SyslogFraming.Rfc3164, facility, severity, timestamp,
            host, appName, procId, null, null, msg);
        return true;
    }

    private static bool TryDecodeRfc5424(string s, int i, int len, int? facility, int? severity, out SyslogEnvelope envelope)
    {
        envelope = default;

        if (!ReadNilableToken(s, ref i, len, out var tsTok) || !ExpectSpace(s, ref i, len))
        {
            return false;
        }

        DateTimeOffset? timestamp = null;
        if (tsTok != null)
        {
            if (!DateTimeOffset.TryParse(tsTok, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedTs))
            {
                return false;
            }

            timestamp = parsedTs;
        }

        if (!ReadNilableToken(s, ref i, len, out var host) || !ExpectSpace(s, ref i, len)
            || !ReadNilableToken(s, ref i, len, out var appName) || !ExpectSpace(s, ref i, len)
            || !ReadNilableToken(s, ref i, len, out var procId) || !ExpectSpace(s, ref i, len)
            || !ReadNilableToken(s, ref i, len, out var msgId) || !ExpectSpace(s, ref i, len))
        {
            return false;
        }

        if (!TryReadStructuredData(s, ref i, len, out var structuredData))
        {
            return false;
        }

        string msg;
        if (i == len)
        {
            msg = string.Empty;
        }
        else if (s[i] == ' ')
        {
            msg = s[(i + 1)..];
        }
        else
        {
            return false; /* trailing garbage that's neither a separator nor end of line */
        }

        envelope = new SyslogEnvelope(SyslogFraming.Rfc5424, facility, severity, timestamp,
            host, appName, procId, msgId, structuredData, msg);
        return true;

        static bool ExpectSpace(string s, ref int i, int len)
        {
            if (i >= len || s[i] != ' ')
            {
                return false;
            }

            i++;
            return true;
        }
    }

    /// <summary>
    /// STRUCTURED-DATA: NILVALUE "-", or one or more "[SD-ID (SP
    /// PARAM=&quot;VALUE&quot;)*]" elements. Only balances brackets/quotes
    /// (respecting backslash-escaping inside a quoted PARAM-VALUE) rather
    /// than splitting out individual SD-ID/params — good enough for a
    /// pre-decoder that hands the raw block on, not a structured-data API.
    /// </summary>
    private static bool TryReadStructuredData(string s, ref int i, int len, out string? structuredData)
    {
        structuredData = null;
        if (i < len && s[i] == '-')
        {
            i++;
            return true;
        }

        if (i >= len || s[i] != '[')
        {
            return false;
        }

        var start = i;
        while (i < len && s[i] == '[')
        {
            i++; /* '[' */
            while (i < len && s[i] != ' ' && s[i] != ']')
            {
                i++; /* SD-ID */
            }

            while (i < len && s[i] == ' ')
            {
                i++; /* SP before PARAM-NAME */
                while (i < len && s[i] != '=' && s[i] != ']')
                {
                    i++; /* PARAM-NAME */
                }

                if (i >= len || s[i] != '=')
                {
                    continue;
                }

                i++; /* '=' */
                if (i >= len || s[i] != '"')
                {
                    continue;
                }

                i++; /* opening '"' */
                while (i < len && s[i] != '"')
                {
                    i += s[i] == '\\' && i + 1 < len ? 2 : 1;
                }

                if (i < len)
                {
                    i++; /* closing '"' */
                }
            }

            if (i >= len || s[i] != ']')
            {
                return false;
            }

            i++; /* ']' */
        }

        structuredData = s[start..i];
        return true;
    }

    private static bool TryParsePri(string s, ref int i, int len, out int? facility, out int? severity)
    {
        facility = null;
        severity = null;
        if (i >= len || s[i] != '<')
        {
            return true; /* no PRI at all is not a failure -- most log files on disk have already lost it */
        }

        var start = i + 1;
        var j = start;
        while (j < len && j - start < 3 && TextRules.IsDigit(s[j]))
        {
            j++;
        }

        if (j == start || j >= len || s[j] != '>')
        {
            return false; /* looked like PRI but wasn't well-formed */
        }

        var pri = int.Parse(s.AsSpan(start, j - start), CultureInfo.InvariantCulture);
        if (pri > 191)
        {
            return false;
        }

        facility = pri / 8;
        severity = pri % 8;
        i = j + 1;
        return true;
    }

    /// <summary>An RFC 3164 date ("Mmm d hh:mm:ss", accepting the common "Mmm  d" single-digit-day double-space form), independent of <c>DateTimeParsers.ParseRfc3164</c>'s <c>Npb</c>-bound calling convention but agreeing with it on the actual date math via the shared <see cref="DateTimeParsers.SyslogTimeToUnix"/> helper.</summary>
    private static bool TryParseRfc3164Timestamp(string s, ref int i, int len, out DateTimeOffset timestamp)
    {
        timestamp = default;
        var start = i;
        if (len - i < 3)
        {
            return false;
        }

        var month = (char.ToUpperInvariant(s[i]), char.ToUpperInvariant(s[i + 1]), char.ToUpperInvariant(s[i + 2])) switch {
            ('J', 'A', 'N') => 1,
            ('F', 'E', 'B') => 2,
            ('M', 'A', 'R') => 3,
            ('A', 'P', 'R') => 4,
            ('M', 'A', 'Y') => 5,
            ('J', 'U', 'N') => 6,
            ('J', 'U', 'L') => 7,
            ('A', 'U', 'G') => 8,
            ('S', 'E', 'P') => 9,
            ('O', 'C', 'T') => 10,
            ('N', 'O', 'V') => 11,
            ('D', 'E', 'C') => 12,
            _ => 0,
        };
        if (month == 0)
        {
            i = start;
            return false;
        }

        i += 3;
        if (i >= len || s[i] != ' ')
        {
            i = start;
            return false;
        }

        i++;
        if (i < len && s[i] == ' ')
        {
            i++; /* single-digit-day padding: "Jun  2" */
        }

        var day = ReadDigits(s, ref i, len, 2);
        if (day is < 1 or > 31)
        {
            i = start;
            return false;
        }

        if (i >= len || s[i] != ' ')
        {
            i = start;
            return false;
        }

        i++;
        var hour = ReadDigits(s, ref i, len, 2);
        if (hour is < 0 or > 23)
        {
            i = start;
            return false;
        }

        if (i >= len || s[i] != ':')
        {
            i = start;
            return false;
        }

        i++;
        var minute = ReadDigits(s, ref i, len, 2);
        if (minute is < 0 or > 59)
        {
            i = start;
            return false;
        }

        if (i >= len || s[i] != ':')
        {
            i = start;
            return false;
        }

        i++;
        var second = ReadDigits(s, ref i, len, 2);
        if (second is < 0 or > 60)
        {
            i = start;
            return false;
        }

        var year = DateTime.UtcNow.Year;
        var unix = DateTimeParsers.SyslogTimeToUnix(year, month, day, hour, minute, second, 0, 0, '+');
        timestamp = DateTimeOffset.FromUnixTimeSeconds(unix);
        return true;
    }
}
