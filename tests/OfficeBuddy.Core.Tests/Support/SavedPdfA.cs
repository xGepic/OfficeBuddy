using System.Xml.Linq;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Tokens;

namespace OfficeBuddy.Core.Tests.Support;

/// <summary>
/// What makes a saved Document claim to be PDF/A, read with PdfPig: the part and conformance level
/// in its XMP metadata, and the output intent naming the colour profile it was made for.
/// </summary>
public sealed record SavedPdfA(string? Part, string? Conformance, string? OutputIntent, bool HasOutputProfile)
{
    private static readonly XNamespace PdfAId = "http://www.aiim.org/pdfa/ns/id/";

    public static SavedPdfA Read(string path)
    {
        using var document = PdfDocument.Open(path);
        string? part = null, conformance = null;
        if (document.TryGetXmpMetadata(out var xmp))
        {
            var xml = xmp.GetXDocument();
            // XMP may write each property as an attribute or as an element.
            string? Property(string name) =>
                xml.Descendants().Select(element => element.Attribute(PdfAId + name)?.Value).FirstOrDefault(value => value is not null)
                ?? xml.Descendants(PdfAId + name).FirstOrDefault()?.Value;
            part = Property("part");
            conformance = Property("conformance");
        }

        string? outputIntent = null;
        var hasProfile = false;
        if (document.Structure.Catalog.CatalogDictionary.TryGet(NameToken.Create("OutputIntents"), out var intents)
            && Resolve(document, intents) is ArrayToken { Data: [var first, ..] }
            && Resolve(document, first) is DictionaryToken intent)
        {
            outputIntent = intent.TryGet(NameToken.S, out var subtype) && Resolve(document, subtype) is NameToken name ? name.Data : null;
            hasProfile = intent.TryGet(NameToken.Create("DestOutputProfile"), out var profile)
                && Resolve(document, profile) is StreamToken { Data.Length: > 0 };
        }

        return new SavedPdfA(part, conformance, outputIntent, hasProfile);
    }

    private static IToken Resolve(PdfDocument document, IToken token) =>
        token is IndirectReferenceToken reference ? document.Structure.GetObject(reference.Data).Data : token;
}
