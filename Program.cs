using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Json;
using System.Collections.Generic;
using System.Linq;

namespace Wicher_mono
{
    class Program
    {
        private const string TempKey = "temp";
        private const string HumidityKey = "humidity";
        private const string PressureKey = "pressure";
        private const string TimestampKey = "timestamp";
        private const string WindSpeedKey = "windspeed";
        
        private const string WeatherPageUrl = "http://pogoda-gdansk.pl";

        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly Regex TempRe = new Regex("<strong id=\"PARAM_TA\">([^<]+)</strong>");
        private static readonly Regex HumidtyRe = new Regex("<strong id=\"PARAM_RH\">([^<]+)</strong>");
        private static readonly Regex PressureRe = new Regex("<strong id=\"PARAM_PR\">([^<]+)</strong>");
        private static readonly Regex WindSpeedRe = new Regex("<strong id=\"PARAM_WV\">([^<]+)</strong>");
        
        private static WeatherReading WeatherReadings = new WeatherReading();
        private static long LastCheckTimestamp = -1;

        /**
         * The address on which to listen. Must end with slash. Example: http://*:8081/
         */
        private static string serverAddress = Environment.GetEnvironmentVariable("server-address");

        static void UpdateReadings(Dictionary<string, string> readings)
        {
            foreach (var r in readings)
            {
                WeatherReadings.AddOrUpdate(r.Key, r.Value);
            }
        }

        static Dictionary<string, string> ExtractReadings(string pageContent, Dictionary<string, Regex> mapping)
        {
            return mapping.ToDictionary(m => m.Key, m => m.Value.Match(pageContent).Groups[1].Value);
        }

        static void WeatherCheckLoop()
        {
            while (true)
            {
                try
                {
                    string pageContent;
                    using (var hc = new HttpClient())
                    {
                        pageContent = hc.GetStringAsync(WeatherPageUrl).Result;
                    }

                    var readingsMapping = new Dictionary<string, Regex>()
                    {
                        { TempKey, TempRe },
                        { HumidityKey, HumidtyRe },
                        { PressureKey, PressureRe },
                        { WindSpeedKey, WindSpeedRe }
                    };
                    var readings = ExtractReadings(pageContent, readingsMapping);
                    UpdateReadings(readings);
                    Interlocked.Exchange(ref LastCheckTimestamp, DateTimeOffset.Now.ToUnixTimeMilliseconds());
                }
                catch (Exception ex)
                {
                    log.Error("Weather check loop", ex);
                }

                Thread.Sleep(TimeSpan.FromMinutes(1));
            }
        }

        static async void HttpListen()
        {
            using (var hl = new HttpListener())
            {
                hl.Prefixes.Add(serverAddress);
                hl.Start();
                while (true)
                {
                    var context = hl.GetContext();
                    var timestampCopy = Interlocked.Read(ref LastCheckTimestamp);
                    var responseJson = new JsonObject(new Dictionary<string, JsonValue>()
                    {
                        { "temp", WeatherReadings[TempKey] },
                        { "humidity", WeatherReadings[HumidityKey] },
                        { "pressure", WeatherReadings[PressureKey] },
                        { "windspeed", WeatherReadings[WindSpeedKey] },
                        { "timestamp", timestampCopy }
                    });
                    var responseString = responseJson.ToString();
                    byte[] responseBytes = Encoding.UTF8.GetBytes(responseString);
                    context.Response.ContentLength64 = responseBytes.Length;
                    context.Response.ContentType = "text/json; charset=UTF-8";
                    await context.Response.OutputStream.WriteAsync(responseBytes, 0, responseBytes.Length);
                }
            }
        }

        static void Main(string[] args)
        {
            try
            {
                var t = new Thread(() => WeatherCheckLoop());
                t.Start();
                HttpListen();
            }
            catch (Exception ex)
            {
                log.Fatal("Main", ex);
                throw;
            }
        }
    }
}
