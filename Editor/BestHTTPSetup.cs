namespace Foxscore.EasyLogin
{
    internal static class BestHTTPSetup
    {
        private static object _lock = new();
        private static bool _isSetup = false;

        public static void Setup()
        {
            lock (_lock)
            {
                if (_isSetup) return;
                BestHTTP.HTTPManager.Setup();
                _isSetup = true;
            }
        }
    }
}