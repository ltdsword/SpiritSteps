using System.Collections.Generic;

namespace ARWalking.UI
{
    public static class UiStrings
    {
        static readonly Dictionary<string, string> English = new Dictionary<string, string>
        {
            { "app.title", "Pawprints" },
            { "app.subtitle", "Walk, grow, remember" },
            { "nav.map", "Map" },
            { "nav.companions", "Companions" },
            { "nav.shop", "Shop" },
            { "nav.journey", "Journey" },
            { "status.local", "Saved only on this phone" }
        };

        public static string Get(string key) => English.TryGetValue(key, out var value) ? value : key;
    }
}
