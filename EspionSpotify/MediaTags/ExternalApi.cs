namespace EspionSpotify.MediaTags
{
    public static class ExternalApi
    {
        public static IExternalAPI Instance { get; set; } = new LastFMAPI();
    }
}
