using System.Globalization;
using System.Resources;
using System.Windows.Markup;

namespace Pastille;

internal static class Localization
{
    internal static readonly ResourceManager Resources = new("Pastille.Strings", typeof(Localization).Assembly);
    internal static readonly string[] SupportedLanguages = ["en", "fr", "es", "de", "it"];

    internal static CultureInfo SelectCulture(CultureInfo windowsCulture) =>
        SupportedLanguages.Contains(windowsCulture.TwoLetterISOLanguageName)
            ? windowsCulture
            : CultureInfo.GetCultureInfo("en");

    internal static void Initialize() => ApplyCulture(SelectCulture(CultureInfo.CurrentUICulture));

    internal static void ApplyCulture(CultureInfo culture)
    {
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    internal static string Text(string key) =>
        Resources.GetString(key, CultureInfo.CurrentUICulture)
        ?? throw new MissingManifestResourceException($"Missing Pastille resource: {key}");

    internal static string Format(string key, params object[] arguments) =>
        string.Format(CultureInfo.CurrentCulture, Text(key), arguments);

    internal static string FormatPan(double value) => value == 0 ? Text("Center") :
        (value > 0 ? CultureInfo.CurrentCulture.NumberFormat.PositiveSign : "") +
        value.ToString("P0", CultureInfo.CurrentCulture);
}

[MarkupExtensionReturnType(typeof(string))]
public sealed class LocExtension(string key) : MarkupExtension
{
    public override object ProvideValue(IServiceProvider serviceProvider) => Localization.Text(key);
}
