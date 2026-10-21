using System.Text;

namespace HousecarlCore;

/// <summary>Display interpretation of UTF-8 text exposed through an inline Windows-1252 reader.
/// This is not encoding detection: some byte sequences have valid meanings in both encodings.
/// Callers retain the original write token and expose the alternate interpretation separately; display names
/// and translated-text predicates prefer this interpretation when present. Never apply it to
/// arbitrary string fields such as EditorID or asset paths, or feed its result to the writer.</summary>
public static class RecordNameDisplay
{
    static readonly Encoding Windows1252 = CodePagesEncodingProvider.Instance.GetEncoding(
        1252, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback)
        ?? throw new InvalidOperationException("Windows-1252 encoding is unavailable.");
    static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);

    /// <summary>Return a distinct UTF-8 interpretation only when both conversions are strict and
    /// the Windows-1252 conversion is reversible. Already-Unicode text outside that codepage,
    /// ASCII, and invalid UTF-8 have no alternate interpretation. No replacement or best-fit decoding.</summary>
    public static string? Utf8Interpretation(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        try
        {
            var bytes = Windows1252.GetBytes(value);
            if (!string.Equals(Windows1252.GetString(bytes), value, StringComparison.Ordinal)) return null;
            var decoded = StrictUtf8.GetString(bytes);
            return string.Equals(decoded, value, StringComparison.Ordinal) ? null : decoded;
        }
        catch (EncoderFallbackException) { return null; }
        catch (DecoderFallbackException) { return null; }
    }
}
