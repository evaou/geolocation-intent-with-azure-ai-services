using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Intent;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace IntentRecognition
{

    internal class Program
    {
        private static string speechKey = "";
        private static string speechRegion = "";
        private static string languageKey = "";
        private static string languageEndpoint = "";
        private static string cluProjectName = "";
        private static string cluDeploymentName = "";

        static void Main(string[] args)
        {
            Console.WriteLine("Demo time!");
            ReadConfigFile();
            Console.ReadLine();

            while (true)
            {
                string input = "";

                Console.WriteLine("Select the method for intent recognition (1/2/3):");
                Console.WriteLine(" 1 - Simple pattern matching");
                Console.WriteLine(" 2 - Custom entity pattern matching");
                Console.WriteLine(" 3 - Conversational language understanding\n");

                int method = int.Parse(Console.ReadLine());
                Console.WriteLine();

                Console.WriteLine("Type something...\n");
                input = Console.ReadLine();
                Console.WriteLine();

                switch (method)
                {
                    case 1:
                        SimpleIntentPatternMatchingWithMicrophoneAsync(input).Wait();
                        break;
                    case 2:
                        CustomIntentPatternMatchingWithMicrophoneAsync(input).Wait();
                        break;
                    case 3:
                        CLUIntentWithMicrophoneAsync(input).Wait();
                        break;
                }

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

            speechKey = config["SpeechKey"];
            speechRegion = config["SpeechRegion"];
            languageKey = config["LanguageKey"];
            languageEndpoint = config["LanguageEndpoint"];
            cluProjectName = config["CluProjectName"];
            cluDeploymentName = config["CluDeploymentName"];
        }
        private static async Task CLUIntentWithMicrophoneAsync(string input)
        {
            var speechConfig = SpeechConfig.FromSubscription(speechKey, speechRegion);

            using (var intentRecognizer = new IntentRecognizer(speechConfig))
            {
                var cluModel = new ConversationalLanguageUnderstandingModel(
                    languageKey,
                    languageEndpoint,
                    cluProjectName,
                    cluDeploymentName);
                var collection = new LanguageUnderstandingModelCollection();
                collection.Add(cluModel);
                intentRecognizer.ApplyLanguageModels(collection);

                IntentRecognitionResult recognitionResult;

                if (input == "")
                {
                    Console.WriteLine("Say something...\n");
                    Console.ReadLine();
                    recognitionResult = await intentRecognizer.RecognizeOnceAsync();
                }
                else
                {
                    recognitionResult = await intentRecognizer.RecognizeOnceAsync(input);
                }

                if (recognitionResult.Reason == ResultReason.RecognizedIntent)
                {
                    Console.WriteLine($"RECOGNIZED: Text = {recognitionResult.Text}");
                    Console.WriteLine($"    Intent Name: {recognitionResult.IntentId}.");
                    Console.WriteLine($"    Language Understanding JSON:\n");
                    string prettyJson = JToken.Parse(recognitionResult.Properties.GetProperty(PropertyId.LanguageUnderstandingServiceResponse_JsonResult)).ToString(Formatting.Indented);
                    Console.WriteLine(prettyJson);
                }
                else if (recognitionResult.Reason == ResultReason.RecognizedSpeech)
                {
                    Console.WriteLine($"RECOGNIZED: Text = {recognitionResult.Text}");
                    Console.WriteLine($"    Intent not recognized.");
                }
                else if (recognitionResult.Reason == ResultReason.NoMatch)
                {
                    Console.WriteLine($"NOMATCH: Speech could not be recognized.");
                }
                else if (recognitionResult.Reason == ResultReason.Canceled)
                {
                    var cancellation = CancellationDetails.FromResult(recognitionResult);
                    Console.WriteLine($"CANCELED: Reason = {cancellation.Reason}");

                    if (cancellation.Reason == CancellationReason.Error)
                    {
                        Console.WriteLine($"CANCELED: ErrorCode = {cancellation.ErrorCode}");
                        Console.WriteLine($"CANCELED: ErrorDetails = {cancellation.ErrorDetails}");
                        Console.WriteLine($"CANCELED: Did you update the subscription info?");
                    }
                }

                Console.ReadLine();
            }
        }


        private static async Task SimpleIntentPatternMatchingWithMicrophoneAsync(string input)
        {
            var config = SpeechConfig.FromSubscription(speechKey, speechRegion);

            using (var intentRecognizer = new IntentRecognizer(config))
            {
                intentRecognizer.AddIntent("{action} the door.", "OpenCloseDoor");
                intentRecognizer.AddIntent("{action} to floor {floorName}.", "ChangeFloors");

                var result = await intentRecognizer.RecognizeOnceAsync(input);

                switch (result.Reason)
                {
                    case ResultReason.RecognizedSpeech:
                        Console.WriteLine($"RECOGNIZED: Text = {result.Text}");
                        Console.WriteLine($"    Intent not recognized.");
                        break;
                    case ResultReason.RecognizedIntent:
                        Console.WriteLine($"RECOGNIZED: Text = {result.Text}");
                        Console.WriteLine($"       Intent Id = {result.IntentId}.");
                        var entities = result.Entities;
                        if (entities.TryGetValue("floorName", out string floorName))
                        {
                            Console.WriteLine($"       FloorName = {floorName}");
                        }

                        if (entities.TryGetValue("action", out string action))
                        {
                            Console.WriteLine($"       Action = {action}");
                        }

                        break;
                    case ResultReason.NoMatch:
                    {
                        Console.WriteLine($"NOMATCH: Speech could not be recognized.");
                        var noMatch = NoMatchDetails.FromResult(result);
                        switch (noMatch.Reason)
                        {
                            case NoMatchReason.NotRecognized:
                                Console.WriteLine($"NOMATCH: Speech was detected, but not recognized.");
                                break;
                            case NoMatchReason.InitialSilenceTimeout:
                                Console.WriteLine($"NOMATCH: The start of the audio stream contains only silence, and the service timed out waiting for speech.");
                                break;
                            case NoMatchReason.InitialBabbleTimeout:
                                Console.WriteLine($"NOMATCH: The start of the audio stream contains only noise, and the service timed out waiting for speech.");
                                break;
                            case NoMatchReason.KeywordNotRecognized:
                                Console.WriteLine($"NOMATCH: Keyword not recognized");
                                break;
                        }
                        break;
                    }
                    case ResultReason.Canceled:
                    {
                        var cancellation = CancellationDetails.FromResult(result);
                        Console.WriteLine($"CANCELED: Reason = {cancellation.Reason}");

                        if (cancellation.Reason == CancellationReason.Error)
                        {
                            Console.WriteLine($"CANCELED: ErrorCode = {cancellation.ErrorCode}");
                            Console.WriteLine($"CANCELED: ErrorDetails = {cancellation.ErrorDetails}");
                            Console.WriteLine($"CANCELED: Did you set the speech resource key and region values?");
                        }
                        break;
                    }
                    default:
                        break;
                }

                Console.ReadLine();
            }
        }

        private static async Task CustomIntentPatternMatchingWithMicrophoneAsync(string input)
        {
            var config = SpeechConfig.FromSubscription(speechKey, speechRegion);

            using (var recognizer = new IntentRecognizer(config))
            {
                // Creates a Pattern Matching model and adds specific intents from your model. The
                // Id is used to identify this model from others in the collection.
                var model = new PatternMatchingModel("YourPatternMatchingModelId");

                // Creates a pattern that uses groups of optional words. "[Go | Take me]" will match either "Go", "Take me", or "".
                var patternWithOptionalWords = "[Go | Take me] to [floor|level] {floorName}";

                // You can also have multiple entities of the same name in a single pattern by adding appending a unique identifier
                // to distinguish between the instances. For example:
                var patternWithTwoOfTheSameEntity = "Go to floor {floorName:1} [and then floor {floorName:2}]";
                // NOTE: Both floorName:1 and floorName:2 are tied to the same list of entries. The identifier can be a string
                //       and is separated from the entity name by a ':'

                // Adds some intents to look for specific patterns.
                model.Intents.Add(new PatternMatchingIntent("ChangeFloors", patternWithOptionalWords, patternWithTwoOfTheSameEntity));

                // Creates the "floorName" entity and set it to type list.
                // Adds acceptable values. NOTE the default entity type is Any
                model.Entities.Add(PatternMatchingEntity.CreateListEntity("floorName", EntityMatchMode.Strict, "ground floor", "lobby", "1st", "first", "one", "1", "2nd", "second", "two", "2"));

                var modelCollection = new LanguageUnderstandingModelCollection();
                modelCollection.Add(model);

                recognizer.ApplyLanguageModels(modelCollection);

                var result = await recognizer.RecognizeOnceAsync(input);

                if (result.Reason == ResultReason.RecognizedIntent)
                {
                    Console.WriteLine($"RECOGNIZED: Text = {result.Text}");
                    Console.WriteLine($"       Intent Name = {result.IntentId}.");

                    var entities = result.Entities;
                    switch (result.IntentId)
                    {
                        case "ChangeFloors":
                            if (entities.TryGetValue("floorName", out string floorName))
                            {
                                Console.WriteLine($"       FloorName = {floorName}");
                            }

                            if (entities.TryGetValue("floorName:1", out floorName))
                            {
                                Console.WriteLine($"     FloorName:1 = {floorName}");
                            }

                            if (entities.TryGetValue("floorName:2", out floorName))
                            {
                                Console.WriteLine($"     FloorName:2 = {floorName}");
                            }

                            break;

                        case "DoorControl":
                            if (entities.TryGetValue("action", out string action))
                            {
                                Console.WriteLine($"          Action = {action}");
                            }
                            break;
                    }
                }
                else if (result.Reason == ResultReason.RecognizedSpeech)
                {
                    Console.WriteLine($"RECOGNIZED: Text = {result.Text}");
                    Console.WriteLine($"    Intent not recognized.");
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

                Console.ReadLine();
            }
        }

    }
}
