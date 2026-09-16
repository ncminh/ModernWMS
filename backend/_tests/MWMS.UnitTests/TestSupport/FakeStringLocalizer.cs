using Microsoft.Extensions.Localization;

namespace ModernWMS.UnitTests.TestSupport
{
    /// <summary>
    /// Returns the lookup key as-is instead of resolving it against the real .resx files.
    /// Tests that need to assert on the actual localized message text should load the
    /// real resx-backed localizer instead of this fake.
    /// </summary>
    public sealed class FakeStringLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] =>
            new(name, string.Format(name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) =>
            Enumerable.Empty<LocalizedString>();
    }
}
