namespace ARWalking.UI
{
    public readonly struct WebViewMapMarker
    {
        public readonly string id;
        public readonly string label;
        public readonly GeoPoint location;

        public WebViewMapMarker(string id, string label, GeoPoint location)
        {
            this.id = id;
            this.label = label;
            this.location = location;
        }
    }
}
