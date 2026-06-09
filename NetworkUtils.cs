namespace StyleOS
{
    public static class NetworkUtils
    {
        public static HttpClient GetBypassedHttpClient()
        {
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
            };
            var client = new HttpClient(handler);
            return client;
        }
    }
}
