using System.Text.Json.Nodes;

namespace DeltaZulu.Parse.Parsers;

/// <summary>Date and time motifs.</summary>
internal static class DateTimeParsers
{
    private const int KernelTimestampLength = 14;

    public static int ConstructRfc3164(ParseContext ctx, JsonObject config, out object? pdata)
            => ConstructDate(ctx, config, "date-rfc3164", out pdata);

    public static int ConstructRfc5424(ParseContext ctx, JsonObject config, out object? pdata)
            => ConstructDate(ctx, config, "date-rfc5424", out pdata);

    /// <summary>"string" (default) or "datetime" — a native <see cref="DateTimeOffset"/>
    /// (UTC midnight of that date), not a numeric encoding of one.</summary>
    public static int ConstructIsoDate(ParseContext ctx, JsonObject config, out object? pdata)
            => ConstructNativeFormat(ctx, config, "date-iso", "datetime", out pdata);

    /// <summary>"string" (default) or "timespan" — a native <see cref="TimeSpan"/>,
    /// not a numeric encoding of one.</summary>
    public static int ConstructDuration(ParseContext ctx, JsonObject config, out object? pdata)
            => ConstructNativeFormat(ctx, config, "duration", "timespan", out pdata);

    public static int ConstructKernelTimestamp(ParseContext ctx, JsonObject config, out object? pdata)
            => ConstructNativeFormat(ctx, config, "kernel-timestamp", "timespan", out pdata);

    public static int ConstructTime12hr(ParseContext ctx, JsonObject config, out object? pdata)
            => ConstructNativeFormat(ctx, config, "time-12hr", "timespan", out pdata);

    public static int ConstructTime24hr(ParseContext ctx, JsonObject config, out object? pdata)
            => ConstructNativeFormat(ctx, config, "time-24hr", "timespan", out pdata);

    /// <summary>A duration: (H)H+:MM:SS, where hours may exceed 23.</summary>
    public static int ParseDuration(Npb npb, ref int offs, object? pdata, string? parserName,
        out int parsed, bool wantValue, ref JsonNode? value)
    {
        parsed = 0;
        var s = npb.Str;
        var i = offs;

        /* hour is a bit tricky: 1 or 2 digits */
        if (!TextRules.IsDigit(npb.At(i)))
        {
            return ErrorCodes.WrongParser;
        }

        var hour = s[i] - '0';
        ++i;
        if (TextRules.IsDigit(npb.At(i)))
        {
            hour = (hour * 10) + (s[i] - '0');
            ++i;
        }

        if (npb.At(i) == ':')
        {
            ++i;
        }
        else
        {
            return ErrorCodes.WrongParser;
        }

        if (i + 5 > npb.StrLen)
        {
            return ErrorCodes.WrongParser;
        }

        if (s[i] < '0' || s[i] > '5')
        {
            return ErrorCodes.WrongParser;
        }

        if (!TextRules.IsDigit(s[i + 1]))
        {
            return ErrorCodes.WrongParser;
        }

        if (s[i + 2] != ':')
        {
            return ErrorCodes.WrongParser;
        }

        if (s[i + 3] < '0' || s[i + 3] > '5')
        {
            return ErrorCodes.WrongParser;
        }

        if (!TextRules.IsDigit(s[i + 4]))
        {
            return ErrorCodes.WrongParser;
        }

        parsed = i + 5 - offs;
        if (wantValue)
        {
            var data = (NativeFormatData)pdata!;
            if (!data.Native)
            {
                value = JsonValue.Create(npb.Str.Substring(offs, parsed));
            }
            else
            {
                var minute = ((s[i] - '0') * 10) + (s[i + 1] - '0');
                var second = ((s[i + 3] - '0') * 10) + (s[i + 4] - '0');
                value = JsonValue.Create(new TimeSpan(hour, minute, second));
            }
        }

        return 0;
    }

    /// <summary>An ISO date: exactly YYYY-MM-DD.</summary>
    public static int ParseIsoDate(Npb npb, ref int offs, object? pdata, string? parserName,
        out int parsed, bool wantValue, ref JsonNode? value)
    {
        parsed = 0;
        var s = npb.Str;
        var i = offs;

        if (offs + 10 > npb.StrLen)
        {
            return ErrorCodes.WrongParser;
        }

        /* year */
        if (!TextRules.IsDigit(s[i]) || !TextRules.IsDigit(s[i + 1])
            || !TextRules.IsDigit(s[i + 2]) || !TextRules.IsDigit(s[i + 3]))
        {
            return ErrorCodes.WrongParser;
        }

        if (s[i + 4] != '-')
        {
            return ErrorCodes.WrongParser;
        }
        /* month */
        if (s[i + 5] == '0')
        {
            if (s[i + 6] < '1' || s[i + 6] > '9')
            {
                return ErrorCodes.WrongParser;
            }
        }
        else if (s[i + 5] == '1')
        {
            if (s[i + 6] < '0' || s[i + 6] > '2')
            {
                return ErrorCodes.WrongParser;
            }
        }
        else
        {
            return ErrorCodes.WrongParser;
        }
        if (s[i + 7] != '-')
        {
            return ErrorCodes.WrongParser;
        }
        /* day */
        if (s[i + 8] == '0')
        {
            if (s[i + 9] < '1' || s[i + 9] > '9')
            {
                return ErrorCodes.WrongParser;
            }
        }
        else if (s[i + 8] == '1' || s[i + 8] == '2')
        {
            if (!TextRules.IsDigit(s[i + 9]))
            {
                return ErrorCodes.WrongParser;
            }
        }
        else if (s[i + 8] == '3')
        {
            if (s[i + 9] != '0' && s[i + 9] != '1')
            {
                return ErrorCodes.WrongParser;
            }
        }
        else
        {
            return ErrorCodes.WrongParser;
        }

        parsed = 10;
        if (wantValue)
        {
            var data = (NativeFormatData)pdata!;
            if (!data.Native)
            {
                value = JsonValue.Create(npb.Str.Substring(offs, parsed));
            }
            else
            {
                var year = ((s[i] - '0') * 1000) + ((s[i + 1] - '0') * 100) + ((s[i + 2] - '0') * 10) + (s[i + 3] - '0');
                var month = ((s[i + 5] - '0') * 10) + (s[i + 6] - '0');
                var day = ((s[i + 8] - '0') * 10) + (s[i + 9] - '0');
                /* UTC midnight of that date; year is clamped the same way
                 * SyslogTimeToUnix's 1970..2100 check clamps the epoch-long
                 * motifs (date-rfc3164/date-rfc5424), so an out-of-range
                 * year here similarly falls back to the Unix epoch instant
                 * rather than throwing */
                value = JsonValue.Create(year is >= 1970 and <= 2100
                    ? new DateTimeOffset(year, month, day, 0, 0, 0, TimeSpan.Zero)
                    : DateTimeOffset.UnixEpoch);
            }
        }

        return 0;
    }

    /// <summary>A Linux kernel timestamp: "[12345.123456]" (5-12 digit seconds, 6 digit fraction).</summary>
    public static int ParseKernelTimestamp(Npb npb, ref int offs, object? pdata, string? parserName,
        out int parsed, bool wantValue, ref JsonNode? value)
    {
        parsed = 0;
        var s = npb.Str;
        var i = offs;

        if (npb.At(i) != '[' || i + KernelTimestampLength > npb.StrLen
            || !TextRules.IsDigit(s[i + 1]) || !TextRules.IsDigit(s[i + 2]) || !TextRules.IsDigit(s[i + 3])
            || !TextRules.IsDigit(s[i + 4]) || !TextRules.IsDigit(s[i + 5]))
        {
            return ErrorCodes.WrongParser;
        }
        i += 6;
        for (var j = 0; j < 7 && i < npb.StrLen && TextRules.IsDigit(s[i]); ++j)
        {
            ++i; /* just scan */
        }

        if (i >= npb.StrLen || s[i] != '.')
        {
            return ErrorCodes.WrongParser;
        }

        ++i; /* skip '.' */

        if (i + 7 > npb.StrLen
            || !TextRules.IsDigit(s[i]) || !TextRules.IsDigit(s[i + 1]) || !TextRules.IsDigit(s[i + 2])
            || !TextRules.IsDigit(s[i + 3]) || !TextRules.IsDigit(s[i + 4]) || !TextRules.IsDigit(s[i + 5])
            || s[i + 6] != ']')
        {
            return ErrorCodes.WrongParser;
        }
        i += 7;

        parsed = i - offs;
        if (wantValue)
        {
            var data = (NativeFormatData)pdata!;
            if (!data.Native)
            {
                value = JsonValue.Create(npb.Str.Substring(offs, parsed));
            }
            else
            {
                /* strip the enclosing '[' ']'; the fixed 6-digit fraction
                 * validated above means this is always a well-formed
                 * "seconds.ffffff" literal */
                var text = npb.Str.Substring(offs + 1, parsed - 2);
                var seconds = double.Parse(text, System.Globalization.CultureInfo.InvariantCulture);
                value = JsonValue.Create(TimeSpan.FromSeconds(seconds));
            }
        }

        return 0;
    }

    /// <summary>An RFC3164 date ("Oct 29 09:47:08", with common deviations accepted).</summary>
    public static int ParseRfc3164(Npb npb, ref int offs, object? pdata, string? parserName,
        out int parsed, bool wantValue, ref JsonNode? value)
    {
        parsed = 0;
        var data = (DateData)pdata!;
        var s = npb.Str;
        var i = offs;
        var len = npb.StrLen - offs;
        var orglen = len;

        if (len < 3)
        {
            return ErrorCodes.WrongParser;
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
            return ErrorCodes.WrongParser;
        }

        i += 3;
        len -= 3;

        if (len == 0 || s[i++] != ' ')
        {
            return ErrorCodes.WrongParser;
        }

        --len;

        /* accept a slightly malformed timestamp with one-digit days */
        if (npb.At(i) == ' ')
        {
            --len;
            ++i;
        }

        var day = HParseInt(s, ref i, ref len);
        if (day < 1 || day > 31)
        {
            return ErrorCodes.WrongParser;
        }

        if (len == 0 || s[i++] != ' ')
        {
            return ErrorCodes.WrongParser;
        }

        --len;

        var hour = HParseInt(s, ref i, ref len);
        if (hour > 1970 && hour < 2100)
        {
            /* assume it actually is a year (e.g. some Cisco formats);
             * re-query the hour, this time it must be valid */
            if (len == 0 || s[i++] != ' ')
            {
                return ErrorCodes.WrongParser;
            }

            --len;
            hour = HParseInt(s, ref i, ref len);
        }
        if (hour < 0 || hour > 23)
        {
            return ErrorCodes.WrongParser;
        }

        if (len == 0 || s[i++] != ':')
        {
            return ErrorCodes.WrongParser;
        }

        --len;
        var minute = HParseInt(s, ref i, ref len);
        if (minute < 0 || minute > 59)
        {
            return ErrorCodes.WrongParser;
        }

        if (len == 0 || s[i++] != ':')
        {
            return ErrorCodes.WrongParser;
        }

        --len;
        var second = HParseInt(s, ref i, ref len);
        if (second < 0 || second > 60)
        {
            return ErrorCodes.WrongParser;
        }

        /* an extra ":" after the date occurs frequently enough (Cisco) to permit it */
        if (len > 0 && s[i] == ':')
        {
            ++i;
            --len;
        }

        parsed = orglen - len;
        if (wantValue)
        {
            if (data.FmtMode == FormatMode.AsString)
            {
                value = JsonValue.Create(npb.Str.Substring(offs, parsed));
            }
            else
            {
                /* RFC3164 has no year: assume the current (UTC) year */
                var year = DateTime.UtcNow.Year;
                var timestamp = SyslogTimeToUnix(year, month, day, hour, minute, second, 0, 0, '+');
                if (data.FmtMode == FormatMode.AsTimestampUnixMs)
                {
                    timestamp *= 1000; /* no more precise info available */
                }

                value = JsonValue.Create(timestamp);
            }
        }
        return 0;
    }

    /// <summary>A TIMESTAMP as specified in RFC5424 (subset of RFC3339).</summary>
    public static int ParseRfc5424(Npb npb, ref int offs, object? pdata, string? parserName,
        out int parsed, bool wantValue, ref JsonNode? value)
    {
        parsed = 0;
        var data = (DateData)pdata!;
        var s = npb.Str;
        var i = offs;
        var len = npb.StrLen - offs;
        var orglen = len;

        var year = HParseInt(s, ref i, ref len);

        /* we accept slightly malformed timestamps, e.g. 2003-9-1T1:0:0 */
        if (len == 0 || s[i++] != '-')
        {
            return ErrorCodes.WrongParser;
        }

        --len;
        var month = HParseInt(s, ref i, ref len);
        if (month < 1 || month > 12)
        {
            return ErrorCodes.WrongParser;
        }

        if (len == 0 || s[i++] != '-')
        {
            return ErrorCodes.WrongParser;
        }

        --len;
        var day = HParseInt(s, ref i, ref len);
        if (day < 1 || day > 31)
        {
            return ErrorCodes.WrongParser;
        }

        if (len == 0 || s[i++] != 'T')
        {
            return ErrorCodes.WrongParser;
        }

        --len;

        var hour = HParseInt(s, ref i, ref len);
        if (hour < 0 || hour > 23)
        {
            return ErrorCodes.WrongParser;
        }

        if (len == 0 || s[i++] != ':')
        {
            return ErrorCodes.WrongParser;
        }

        --len;
        var minute = HParseInt(s, ref i, ref len);
        if (minute < 0 || minute > 59)
        {
            return ErrorCodes.WrongParser;
        }

        if (len == 0 || s[i++] != ':')
        {
            return ErrorCodes.WrongParser;
        }

        --len;
        var second = HParseInt(s, ref i, ref len);
        if (second < 0 || second > 60)
        {
            return ErrorCodes.WrongParser;
        }

        int secfrac;
        int secfracPrecision;
        if (len > 0 && s[i] == '.')
        {
            --len;
            var start = ++i;
            secfrac = HParseInt(s, ref i, ref len);
            secfracPrecision = i - start;
        }
        else
        {
            secfracPrecision = 0;
            secfrac = 0;
        }

        /* the timezone is required */
        if (len == 0)
        {
            return ErrorCodes.WrongParser;
        }

        int offsetHour;
        int offsetMinute;
        char offsetMode;
        if (s[i] == 'Z')
        {
            offsetHour = 0;
            offsetMinute = 0;
            offsetMode = '+';
            --len;
            i++; /* eat Z */
        }
        else if (s[i] == '+' || s[i] == '-')
        {
            offsetMode = s[i];
            --len;
            i++;
            offsetHour = HParseInt(s, ref i, ref len);
            if (offsetHour < 0 || offsetHour > 23)
            {
                return ErrorCodes.WrongParser;
            }

            if (len == 0 || s[i++] != ':')
            {
                return ErrorCodes.WrongParser;
            }

            --len;
            offsetMinute = HParseInt(s, ref i, ref len);
            if (offsetMinute < 0 || offsetMinute > 59)
            {
                return ErrorCodes.WrongParser;
            }
        }
        else
        {
            return ErrorCodes.WrongParser;
        }

        if (len > 0 && s[i] != ' ') /* if not followed by space, it cannot be a "good" time */
        {
            return ErrorCodes.WrongParser;
        }

        parsed = orglen - len;

        if (wantValue)
        {
            if (data.FmtMode == FormatMode.AsString)
            {
                value = JsonValue.Create(npb.Str.Substring(offs, parsed));
            }
            else
            {
                var timestamp = SyslogTimeToUnix(year, month, day, hour, minute, second,
                    offsetHour, offsetMinute, offsetMode);
                if (data.FmtMode == FormatMode.AsTimestampUnixMs)
                {
                    timestamp *= 1000;
                    var div = 1;
                    if (secfracPrecision == 1)
                    {
                        secfrac *= 100;
                    }
                    else if (secfracPrecision == 2)
                    {
                        secfrac *= 10;
                    }
                    else if (secfracPrecision > 3)
                    {
                        for (var k = 0; k < secfracPrecision - 3; ++k)
                        {
                            div *= 10;
                        }
                    }

                    timestamp += secfrac / div;
                }
                value = JsonValue.Create(timestamp);
            }
        }
        return 0;
    }

    /// <summary>A timestamp in 12hr format (exactly HH:MM:SS).</summary>
    public static int ParseTime12hr(Npb npb, ref int offs, object? pdata, string? parserName,
        out int parsed, bool wantValue, ref JsonNode? value)
    {
        parsed = 0;
        var s = npb.Str;
        var i = offs;

        if (offs + 8 > npb.StrLen)
        {
            return ErrorCodes.WrongParser;
        }

        /* hour */
        if (s[i] == '0')
        {
            if (!TextRules.IsDigit(s[i + 1]))
            {
                return ErrorCodes.WrongParser;
            }
        }
        else if (s[i] == '1')
        {
            if (s[i + 1] < '0' || s[i + 1] > '2')
            {
                return ErrorCodes.WrongParser;
            }
        }
        else
        {
            return ErrorCodes.WrongParser;
        }
        if (!CheckMmSs(s, i))
        {
            return ErrorCodes.WrongParser;
        }

        parsed = 8;
        if (wantValue)
        {
            value = HhMmSsValue(s, offs, i, (NativeFormatData)pdata!);
        }

        return 0;
    }

    /// <summary>A timestamp in 24hr format (exactly HH:MM:SS).</summary>
    public static int ParseTime24hr(Npb npb, ref int offs, object? pdata, string? parserName,
        out int parsed, bool wantValue, ref JsonNode? value)
    {
        parsed = 0;
        var s = npb.Str;
        var i = offs;

        if (offs + 8 > npb.StrLen)
        {
            return ErrorCodes.WrongParser;
        }

        /* hour */
        if (s[i] == '0' || s[i] == '1')
        {
            if (!TextRules.IsDigit(s[i + 1]))
            {
                return ErrorCodes.WrongParser;
            }
        }
        else if (s[i] == '2')
        {
            if (s[i + 1] < '0' || s[i + 1] > '3')
            {
                return ErrorCodes.WrongParser;
            }
        }
        else
        {
            return ErrorCodes.WrongParser;
        }
        if (!CheckMmSs(s, i))
        {
            return ErrorCodes.WrongParser;
        }

        parsed = 8;
        if (wantValue)
        {
            value = HhMmSsValue(s, offs, i, (NativeFormatData)pdata!);
        }

        return 0;
    }

    /// <summary>
    /// Materialize an "HH:MM:SS" match (time-24hr/time-12hr) either as the
    /// raw string or as a native <see cref="TimeSpan"/>, per <paramref name="data"/>.
    /// <paramref name="i"/> is the hour's starting index (checked by
    /// <see cref="CheckMmSs"/> to have a valid ":MM:SS" tail at i+2..i+7).
    /// </summary>
    private static JsonNode? HhMmSsValue(string s, int offs, int i, NativeFormatData data)
    {
        if (!data.Native)
        {
            return JsonValue.Create(s.Substring(offs, 8));
        }

        var hour = ((s[i] - '0') * 10) + (s[i + 1] - '0');
        var minute = ((s[i + 3] - '0') * 10) + (s[i + 4] - '0');
        var second = ((s[i + 6] - '0') * 10) + (s[i + 7] - '0');
        return JsonValue.Create(new TimeSpan(hour, minute, second));
    }

    /// <summary>
    /// Convert a wall-clock time to a Unix timestamp without involving the
    /// local time zone (port of syslogTime2time_t). Out-of-range years yield
    /// 0; out-of-range days simply roll over, like the C code.
    /// </summary>
    internal static long SyslogTimeToUnix(int year, int month, int day,
        int hour, int minute, int second, int offsetHour, int offsetMinute, char offsetMode)
    {
        if (year < 1970 || year > 2100)
        {
            return 0;
        }

        ReadOnlySpan<int> monthInDays = [0, 31, 59, 90, 120, 151, 181, 212, 243, 273, 304, 334];
        long days = monthInDays[month - 1];
        if (IsLeap(year) && month > 2)
        {
            days++;
        }

        for (var y = 1970; y < year; ++y)
        {
            days += IsLeap(y) ? 366 : 365;
        }

        days += day - 1;

        var t = (days * 86400) + (hour * 3600L) + (minute * 60L) + second;
        var utcOffset = (offsetHour * 3600) + (offsetMinute * 60);
        if (offsetMode == '+')
        {
            utcOffset = -utcOffset; /* timestamp is ahead, "go back" to UTC */
        }

        return t + utcOffset;

        /* the C library's leap rule, valid for 1970..2100 */
        static bool IsLeap(int y) => (y % 100 != 0 && y % 4 == 0) || y == 2000;
    }

    /// <summary>Validate the ":MM:SS" tail shared by the time parsers.</summary>
    private static bool CheckMmSs(string s, int i)
        => s[i + 2] == ':'
           && s[i + 3] >= '0' && s[i + 3] <= '5' && TextRules.IsDigit(s[i + 4])
           && s[i + 5] == ':'
           && s[i + 6] >= '0' && s[i + 6] <= '5' && TextRules.IsDigit(s[i + 7]);

    private static int ConstructDate(ParseContext ctx, JsonObject config, string parserName, out object? pdata)
    {
        var data = new DateData();
        foreach ((var key, var val) in config)
        {
            if (key == "format")
            {
                var fmtmode = JsonText.GetLenientString(val) ?? string.Empty;
                if (fmtmode == "timestamp-unix")
                {
                    data.FmtMode = FormatMode.AsTimestampUnix;
                }
                else if (fmtmode == "timestamp-unix-ms")
                {
                    data.FmtMode = FormatMode.AsTimestampUnixMs;
                }
                else if (fmtmode == "string")
                {
                    data.FmtMode = FormatMode.AsString;
                }
                else
                {
                    ctx.Error($"invalid value for {parserName}:format {fmtmode}");
                }
            }
            else if (!NumberParsers.IsDashName(key, val))
            {
                ctx.Error($"invalid param for {parserName} {key}");
            }
        }
        pdata = data;
        return 0;
    }

    /// <summary>
    /// "string" (default) or <paramref name="nativeKeyword"/> ("datetime" for
    /// date-iso, "timespan" for the duration/time-of-day motifs) — shared by
    /// every motif whose only choice is "the matched text" vs. "a native
    /// .NET value of the corresponding KQL type," with no intermediate
    /// numeric encoding (see <see cref="KqlType"/>/docs/COMPARISON.md: the
    /// goal is a value a downstream KQL/MessagePack consumer can use as-is,
    /// with no cast/conversion step of its own).
    /// </summary>
    private static int ConstructNativeFormat(ParseContext ctx, JsonObject config, string parserName,
        string nativeKeyword, out object? pdata)
    {
        var data = new NativeFormatData();
        foreach ((var key, var val) in config)
        {
            if (key == "format")
            {
                var fmtmode = JsonText.GetLenientString(val) ?? string.Empty;
                if (fmtmode == nativeKeyword)
                {
                    data.Native = true;
                }
                else if (fmtmode == "string")
                {
                    data.Native = false;
                }
                else
                {
                    ctx.Error($"invalid value for {parserName}:format {fmtmode}");
                }
            }
            else if (!NumberParsers.IsDashName(key, val))
            {
                ctx.Error($"invalid param for {parserName} {key}");
            }
        }
        pdata = data;
        return 0;
    }

    /// <summary>Parse a decimal integer, advancing index and remaining length (port of hParseInt).</summary>
    private static int HParseInt(string s, ref int i, ref int len)
    {
        var v = 0;
        while (len > 0 && TextRules.IsDigit(s[i]))
        {
            v = unchecked((v * 10) + s[i] - '0');
            ++i;
            --len;
        }
        return v;
    }

    internal sealed class DateData
    {
        public FormatMode FmtMode = FormatMode.AsString;
    }

    internal sealed class NativeFormatData
    {
        public bool Native;
    }
}
