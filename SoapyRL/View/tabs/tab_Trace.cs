namespace SoapyRL.UI
{
    public static class tab_Trace
    {
        private static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

        public static int s_selectedTrace = 0;
        public static string[] s_comboTraces = new string[] { $"Trace 1", $"Trace 2", $"Trace 3", $"Trace 4", $"Trace 5", $"Trace 6" };
        public static Trace[] s_traces = new Trace[6];

        public enum traceViewStatus
        {
            active, clear, view
        }

        public struct Trace
        {
            public int average;

            public Trace()
            {
                plot = new SortedDictionary<float, float>();
                average = 1;
                viewStatus = traceViewStatus.clear;
            }

            public traceViewStatus viewStatus;
            public SortedDictionary<float, float> plot;
        }

        public static KeyValuePair<float, float> getClosestSampeledFrequency(int traceID, float Mhz)
        {
            lock (s_traces[traceID].plot)
                return s_traces[traceID].plot.MinBy(x => Math.Abs((long)x.Key - Mhz));
        }

        public static KeyValuePair<float, float> findMaxHoldRange(SortedDictionary<float, float> table, float start, float stop)
        {
            KeyValuePair<float, float> results = new KeyValuePair<float, float>(0, -1000);
            var range = table.ToList();
            foreach (KeyValuePair<float, float> sample in range)
                if (sample.Value > results.Value && sample.Key >= start && sample.Key <= stop)
                    results = sample;

            return results;
        }
    }
}