using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Net;
using System.Text;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Intent;

namespace GeolocationIntent
{
    public class Program
    {
        private static string azureMapsKey = "";
        private static string speechKey = "";
        private static string speechRegion = "";
        private static string languageKey = "";
        private static string languageRegion = "";
        private static string cluProjectName = "";
        private static string cluDeploymentName = "";

        private static string origin = "";
        private static string destination = "";
        private static string intent = "";
        private static Coordinate originLonLat = new Coordinate();
        private static Coordinate destinationLonLat = new Coordinate();
        private static DateTime currentDatetime = DateTime.Now; 
        private static DateTime recommendedArrivalDatetime;
        private static DateTime arrivalDatetime;
        private static DateTime departureDatetime;
        private static string geojsonIoLink = "";

        static async Task Main(string[] args)
        {
            while (true)
            {
                Console.WriteLine("Demo time!");
                ReadConfigFile();
                Console.ReadLine();

                string input = "";
                Console.WriteLine("Type something...\n");
                input = Console.ReadLine();
                Console.WriteLine();

                Console.WriteLine("=== 1. Recognize Geolocation Intent ===\n");
                GeoIntentPatternMatchingWithMicrophoneAsync(input).Wait();
                Console.WriteLine();
                Console.WriteLine($"Destination: {destination}");
                Console.WriteLine($"Origin: {origin}");
                Console.ReadLine();

                Console.WriteLine("=== 2. Get Location Coordinate ===\n");
                destinationLonLat = await AzureMapsGeocodingAsync(destination);
                Console.WriteLine($"Destination Coordinate: {destinationLonLat.Longitude}, {destinationLonLat.Latitude}");
                originLonLat = await AzureMapsGeocodingAsync(origin);
                Console.WriteLine($"Origin Coordinate: {originLonLat.Longitude}, {originLonLat.Latitude}");
                Console.ReadLine();

                Console.WriteLine("=== 3. Get Destination Weather Forecast ===\n");
                Console.WriteLine($"Current Datetime: {currentDatetime}");
                Console.ReadLine();
                recommendedArrivalDatetime = await AzureMapsWeatherAsync(destinationLonLat); 
                Console.WriteLine($"Recommended Arrival Datetime: {recommendedArrivalDatetime}");
                Console.ReadLine();

                Console.WriteLine("=== 4. Get Route from Origin to Destination at Recommended Arrival Datetime ===\n");
                await AzureMapsRouteAsync(originLonLat, destinationLonLat, recommendedArrivalDatetime);
                Console.WriteLine($"Route Departure DateTime: {departureDatetime}");
                Console.WriteLine($"Route Arrival DateTime: {arrivalDatetime}");
                Console.ReadLine();

                Console.WriteLine("=== 5. Provide Intelligent Suggestion ===\n");
                Console.WriteLine($"Destination ({destination}) shows that you would like to go {intent}. To arrive at the {intent} spot on a sunny day, here come the recommended departure time ({departureDatetime}) and driving route shown below.");
                Console.ReadLine();

                Console.WriteLine("=== 6. Open Driving Route ===");
                Console.ReadLine();

                Process.Start(new ProcessStartInfo
                {
                    FileName = "msedge.exe",
                    Arguments = geojsonIoLink,
                    UseShellExecute = true
                });
                Console.ReadLine();

                Console.Clear();
            }
        }

        private static void ReadConfigFile()
        {
            string rootPath = Directory.GetCurrentDirectory();
            string configFilePath = Path.Combine(rootPath, "..", "..", "..", "appsettings.json");

            IConfiguration config = new ConfigurationBuilder()
                .AddJsonFile(configFilePath, optional: true, reloadOnChange: true)
                .Build();

            azureMapsKey = config["AzureMapsKey"];
            speechKey = config["SpeechKey"];
            speechRegion = config["SpeechRegion"];
            languageKey = config["LanguageKey"];
            languageRegion = config["LanguageRegion"];
            cluProjectName = config["CluProjectName"];
            cluDeploymentName = config["CluDeploymentName"];
        }

        private static async Task<Coordinate> AzureMapsGeocodingAsync(string inputQuery)
        {
            string apiUrl = $"https://atlas.microsoft.com/geocode?api-version=2023-06-01&query={inputQuery}&subscription-key={azureMapsKey}";
            Coordinate result = new();

            using (HttpClient client = new HttpClient())
            {
                HttpResponseMessage response = await client.GetAsync(apiUrl);

                string jsonResponse;

                if (response.IsSuccessStatusCode)
                {
                    jsonResponse = await response.Content.ReadAsStringAsync();
                    dynamic featureCollection = JsonConvert.DeserializeObject<FeatureCollection>(jsonResponse);
                    var feature = featureCollection.Features[0];

                    result.Longitude = feature.Geometry.Coordinates[0];
                    result.Latitude = feature.Geometry.Coordinates[1];

                    return result;
                }
            }

            return result;
        }

        private static async Task<DateTime> AzureMapsWeatherAsync(Coordinate locationLonLat, int duration = 240, int weatherCode = 3)
        {
            string apiUrl = $"https://atlas.microsoft.com/weather/forecast/hourly/json?api-version=1.1&query={locationLonLat}&duration={duration}&subscription-key={azureMapsKey}";

            DateTime result = new();

            using (HttpClient client = new HttpClient())
            {
                HttpResponseMessage response = await client.GetAsync(apiUrl);

                string jsonResponse;

                if (response.IsSuccessStatusCode)
                {
                    jsonResponse = await response.Content.ReadAsStringAsync();

                    dynamic forecastCollection = JsonConvert.DeserializeObject<ForecastCollection>(jsonResponse);

                    // check weather forcasts for an hour later
                    for (var i = 1; i < forecastCollection.Forecasts.Length; i++)
                    {
                        var forecast = forecastCollection.Forecasts[i];

                        Console.WriteLine($"Date: {forecast.Date}, Phrase: {forecast.IconPhrase}, Code: {forecast.IconCode}");

                        if (forecast.IconCode <= weatherCode)
                        {
                            Console.WriteLine();
                            return forecast.Date;
                        }
                    }
                }
            }

            return result;
        }

        private static async Task AzureMapsRouteAsync(Coordinate originLocation, Coordinate destinationLocation, DateTime arriveAt)
        {
            string apiUrl = $"https://atlas.microsoft.com/route/directions?api-version=2023-10-01-preview&subscription-key={azureMapsKey}";
            string jsonBody = $@"{{
              ""type"": ""FeatureCollection"",
              ""features"": [
                {{
                  ""type"": ""Feature"",
                  ""geometry"": {{
                    ""coordinates"": [
                      {originLocation.Longitude},
                      {originLocation.Latitude}
                    ],
                    ""type"": ""Point""
                  }},
                  ""properties"": {{
                    ""pointIndex"": 0,
                    ""pointType"": ""waypoint""
                  }}
                }},
                {{
                  ""type"": ""Feature"",
                  ""geometry"": {{
                    ""coordinates"": [
                      {destinationLocation.Longitude},
                      {destinationLocation.Latitude}
                    ],
                    ""type"": ""Point""
                  }},
                  ""properties"": {{
                    ""pointIndex"": 1,
                    ""pointType"": ""waypoint""
                  }}
                }}
              ],
              ""optimizeRoute"": ""fastestWithTraffic"",
              ""routeOutputOptions"": [
                ""routePath""
              ],
              ""maxRouteCount"": 2,
              ""arriveAt"": ""{arriveAt:yyyy-MM-ddTHH:mm:ssK}"",
              ""travelMode"": ""driving""
            }}";
            
            using (HttpClient client = new HttpClient())
            {
                var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/geo+json");

                try
                {
                    HttpResponseMessage response = await client.PostAsync(apiUrl, content);
                    string jsonResponse;

                    if (response.IsSuccessStatusCode)
                    {
                        jsonResponse = await response.Content.ReadAsStringAsync();

                        dynamic featureCollection = JsonConvert.DeserializeObject<FeatureCollection2>(jsonResponse);
                        var feature = featureCollection.Features[2];
                        departureDatetime = feature.Properties.DepartureTime;
                        arrivalDatetime = feature.Properties.ArrivalTime;

                        dynamic geoJson = JsonConvert.DeserializeObject(jsonResponse);
                        string geoJsonString = JsonConvert.SerializeObject(geoJson);
                        string encodedGeoJson = WebUtility.UrlEncode(geoJsonString);
                        geojsonIoLink = $"https://geojson.io/#data=data:application/json,{encodedGeoJson}";
                    }
                }

                catch (HttpRequestException e)
                {
                    Console.WriteLine($"An error occurred: {e.Message}");
                }
            }

        }

        private static async Task GeoIntentPatternMatchingWithMicrophoneAsync(string input)
        {
            var config = SpeechConfig.FromSubscription(speechKey, speechRegion);

            origin = "";
            destination = "";

            using (var recognizer = new IntentRecognizer(config))
            {
                var model = new PatternMatchingModel("GeoIntentPatternMatchingModelId");

                var hikingPatternWithPrefix = "[{subject}] go to destination {hikingPrefix} {hikingKeyword} from {hikingOrigin}";
                var hikingPatternWithSuffix = "[{subject}] go to destination {hikingKeyword} {hikingSuffix} from {hikingOrigin}";

                var eatingPatternWithPrefix = "[{subject}] go to destination {eatingPrefix} {eatingKeyword}";
                var eatingPatternWithSuffix = "[{subject}] go to destination {eatingKeyword} {eatingSuffix}";

                var shoppingPatternWithPrefix = "[{subject}] go to destination {shoppingPrefix} {shoppingKeyword}";
                var shoppingPatternWithSuffix = "[{subject}] go to destination {shoppingKeyword} {shoppingSuffix}";

                model.Intents.Add(new PatternMatchingIntent("Hiking", hikingPatternWithPrefix, hikingPatternWithSuffix));
                model.Intents.Add(new PatternMatchingIntent("Eating", eatingPatternWithPrefix, eatingPatternWithSuffix));
                model.Intents.Add(new PatternMatchingIntent("Shopping", shoppingPatternWithPrefix, shoppingPatternWithSuffix));

                model.Entities.Add(PatternMatchingEntity.CreateListEntity("hikingKeyword", EntityMatchMode.Strict, "trail", "trailhead", "mountain", "mt.", "park"));
                model.Entities.Add(PatternMatchingEntity.CreateListEntity("eatingKeyword", EntityMatchMode.Strict, "restaurant"));
                model.Entities.Add(PatternMatchingEntity.CreateListEntity("shoppingKeyword", EntityMatchMode.Strict, "mall"));

                var modelCollection = new LanguageUnderstandingModelCollection();
                modelCollection.Add(model);

                recognizer.ApplyLanguageModels(modelCollection);
                IntentRecognitionResult result;

                if (input == "")
                {
                    Console.WriteLine("Say something...\n");
                    Console.ReadLine();
                    result = await recognizer.RecognizeOnceAsync();
                }
                else
                { 
                    result = await recognizer.RecognizeOnceAsync(input);
                }

                if (result.Reason == ResultReason.RecognizedIntent)
                {
                    Console.WriteLine($"Input Text: {result.Text}\n");
                    Console.WriteLine($"Recognized Intent: {result.IntentId}\n");
                    intent = result.IntentId;

                    var entities = result.Entities;
                    switch (result.IntentId)
                    {
                        case "Hiking":

                            if (entities.TryGetValue("hikingPrefix", out string hikingPrefix))
                            {
                                Console.WriteLine($"Hiking Prefix: {hikingPrefix}");
                                destination = destination + hikingPrefix + " ";
                            }

                            if (entities.TryGetValue("hikingKeyword", out string hikingKeyword))
                            {
                                Console.WriteLine($"Hiking Keyword: {hikingKeyword}");
                                destination += hikingKeyword;
                            }

                            if (entities.TryGetValue("hikingSuffix", out string hikingSuffix))
                            {
                                Console.WriteLine($"Hiking Suffix: {hikingSuffix}");
                                destination = destination + " " + hikingSuffix;
                            }

                            if (entities.TryGetValue("hikingOrigin", out string hikingOrigin))
                            {
                                origin += hikingOrigin;
                            }
                            break;

                        case "Eating":
                            if (entities.TryGetValue("eatingPrefix", out string eatingPrefix))
                            {
                                Console.WriteLine($"Eating Prefix: {eatingPrefix}");
                            }

                            if (entities.TryGetValue("eatingKeyword", out string eatingKeyword))
                            {
                                Console.WriteLine($"Eating Keyword: {eatingKeyword}");
                            }

                            if (entities.TryGetValue("eatingSuffix", out string eatingSuffix))
                            {
                                Console.WriteLine($"Eating Suffix: {eatingSuffix}");
                            }
                            break;

                        case "Shopping":
                            if (entities.TryGetValue("shoppingPrefix", out string shoppingPrefix))
                            {
                                Console.WriteLine($"Shopping Prefix: {shoppingPrefix}");
                            }

                            if (entities.TryGetValue("shoppingKeyword", out string shoppingKeyword))
                            {
                                Console.WriteLine($"Shopping Keyword: {shoppingKeyword}");
                            }

                            if (entities.TryGetValue("shoppingSuffix", out string shoppingSuffix))
                            {
                                Console.WriteLine($"Shopping Suffix: {shoppingSuffix}");
                            }
                            break;
                    }
                }
                else if (result.Reason == ResultReason.RecognizedSpeech)
                {
                    Console.WriteLine($"Input Text = {result.Text}\n");
                    Console.WriteLine($"Intent not recognized.");
                }
                else if (result.Reason == ResultReason.NoMatch)
                {
                    Console.WriteLine($"NOMATCH: Speech could not be recognized.");
                }
                else if (result.Reason == ResultReason.Canceled)
                {
                    var cancellation = CancellationDetails.FromResult(result);
                    Console.WriteLine($"CANCELED: Reason = {cancellation.Reason}");

                    if (cancellation.Reason == CancellationReason.Error)
                    {
                        Console.WriteLine($"CANCELED: ErrorCode = {cancellation.ErrorCode}");
                        Console.WriteLine($"CANCELED: ErrorDetails = {cancellation.ErrorDetails}");
                        Console.WriteLine($"CANCELED: Did you set the speech resource key and region values?");
                    }
                }
            }
        }

        public class FeatureCollection
        {
            public Feature[] Features { get; set; }
        }

        public class Feature
        {
            public Geometry Geometry { get; set; }
        }

        public class Geometry
        {
            public double[] Coordinates { get; set; }
        }

        public class Coordinate
        {
            public double Longitude { get; set; }
            public double Latitude { get; set; }

            public Coordinate(double longitude = 0, double latitude = 0)
            {
                Longitude = longitude;
                Latitude = latitude;
            }

            public override string ToString()
            {
                return $"{Latitude}, {Longitude}";
            }
        }
        public class FeatureCollection2
        {
            public Feature2[] Features { get; set; }
        }

        public class Feature2
        {
            public Properties Properties { get; set; }
        }

        public class Properties 
        {
            public DateTime DepartureTime { get; set; }
            public DateTime ArrivalTime { get; set; }
        }

        public class ForecastCollection
        {
            public Forecast[] Forecasts { get; set; }
        }
        public class Forecast 
        {
            public int IconCode { get; set; }
            public string IconPhrase { get; set; }
            public DateTime Date { get; set; }
        }

        public static async Task<string> ShortenUrl(string longUrl)
        {
            using (HttpClient client = new HttpClient())
            {
                string tinyUrl = $"http://tinyurl.com/api-create.php?url={longUrl}";
                string shortUrl = await client.GetStringAsync(tinyUrl);
                return shortUrl;
            }
        }

    }
}
