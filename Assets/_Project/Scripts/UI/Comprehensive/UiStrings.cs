using System.Collections.Generic;

namespace ARWalking.UI
{
    public static class UiStrings
    {
        static readonly Dictionary<string, string> English = new Dictionary<string, string>
        {
            { "app.title", "Memory Walk" },
            { "app.subtitle", "Every walk keeps a story" },
            { "nav.map", "Map" },
            { "nav.garden", "Garden" },
            { "nav.walk", "Walk" },
            { "nav.journal", "Journal" },
            { "nav.book", "Book" },
            { "action.continue", "Enable & continue" },
            { "action.startWalk", "Start a walk" },
            { "action.finishWalk", "Finish walk" },
            { "action.saveJourney", "Save to journal" },
            { "action.recenter", "Recenter" },
            { "action.hatch", "Welcome this spirit" },
            { "action.photo", "Take photo" },
            { "action.savePhoto", "Save photo" },
            { "action.retake", "Retake" },
            { "action.close", "Close" },
            { "permission.location", "Location while walking" },
            { "permission.camera", "Camera for AR memories" },
            { "permission.activity", "Activity for step progress" },
            { "status.prototype", "Prototype data" },
            { "status.synced", "All memories are available offline" },
            { "screen.map", "Good morning, Explorer" },
            { "screen.garden", "Seedling Garden" },
            { "screen.collection", "Spirit Book" },
            { "screen.journal", "Journey Journal" },
            { "screen.ar", "Explore together" },
            { "empty.title", "Nothing here yet" },
            { "empty.body", "Take a gentle walk and new memories will appear." }
        };

        public static string Get(string key)
        {
            return English.TryGetValue(key, out var value) ? value : key;
        }
    }
}
