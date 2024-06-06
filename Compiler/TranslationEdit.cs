using System.Text.Json;

namespace LocalizeFromSource
{
    public record TranslationEdit(string? oldSource, string newSource, string? oldTarget, string? newTarget, Uri? link);
}
