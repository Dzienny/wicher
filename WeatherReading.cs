using System;
using System.Collections.Concurrent;

namespace Wicher_mono
{
    public class WeatherReading
    {
        /**
         * The text that is used on the webpage when the value for a given measurement is not available.
         */
        public const string ReadingUnavailable = "-";

        private ConcurrentDictionary<string, ReadingItem> WeatherReadings = new ConcurrentDictionary<string, ReadingItem>();

        public string this[string key] 
        {
            get => WeatherReadings[key].Value;
        }

        public void AddOrUpdate(string key, string value)
        {
            if (value == ReadingUnavailable &&
                WeatherReadings.ContainsKey(key) &&
                WeatherReadings[key].Timestamp.Subtract(TimeSpan.FromMinutes(15)) <= DateTime.UtcNow)
            {
                return;
            }
            var readingItem = new ReadingItem(value, DateTime.UtcNow);
            WeatherReadings.AddOrUpdate(key, readingItem, (_, __) => readingItem);
        }

        private class ReadingItem
        {
            public string Value;
            public DateTime Timestamp;

            public ReadingItem(string value, DateTime timestamp)
            {
                Value = value;
                Timestamp = timestamp;
            }
        }
    }
}
