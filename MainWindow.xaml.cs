using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Speech.Recognition;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Threading;


namespace RaidVoice_V2_WPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private SpeechRecognitionEngine recognizer;
        private List<string> grammarWords = new List<string>();
        private Grammar grammar;
        private bool isRecognizing = false;
        private bool isLoading = false;

        public MainWindow()
        {
            InitializeComponent();
            StartLoading();
            LoadGameTermsAsync();
        }

        public class ItemData
        {
            [JsonProperty("uid")]
            public string Uid { get; set; }

            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("tags")]
            public List<string> Tags { get; set; }

            [JsonProperty("shortName")]
            public string ShortName { get; set; }

            [JsonProperty("price")]
            public int Price { get; set; }

            [JsonProperty("basePrice")]
            public int BasePrice { get; set; }

            [JsonProperty("avg24hPrice")]
            public int Avg24hPrice { get; set; }

            [JsonProperty("avg7daysPrice")]
            public int Avg7daysPrice { get; set; }

            [JsonProperty("traderName")]
            public string TraderName { get; set; }

            [JsonProperty("traderPrice")]
            public int TraderPrice { get; set; }

            [JsonProperty("traderPriceCur")]
            public string TraderPriceCur { get; set; }

            [JsonProperty("updated")]
            public DateTime Updated { get; set; }

            [JsonProperty("slots")]
            public int Slots { get; set; }

            [JsonProperty("diff24h")]
            public double Diff24h { get; set; }

            [JsonProperty("diff7days")]
            public double Diff7days { get; set; }

            [JsonProperty("icon")]
            public string Icon { get; set; }

            [JsonProperty("link")]
            public string Link { get; set; }

            [JsonProperty("wikiLink")]
            public string WikiLink { get; set; }

            [JsonProperty("img")]
            public string Img { get; set; }

            [JsonProperty("imgBig")]
            public string ImgBig { get; set; }

            [JsonProperty("bsgId")]
            public string BsgId { get; set; }

            [JsonProperty("isFunctional")]
            public bool IsFunctional { get; set; }

            [JsonProperty("reference")]
            public string Reference { get; set; }
        }
        private void StartLoading()
        {
            Dispatcher.Invoke(() =>
            {
                isLoading = true;
                LoadingText.Visibility = Visibility.Visible;
            });
        }

        private void StopLoading()
        {
            Dispatcher.Invoke(() =>
            {
                isLoading = false;
                LoadingText.Visibility = Visibility.Collapsed;
            });
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if(grammar == null) {  return; }
            if(isLoading) return;
            if (e.Key == Key.CapsLock && !isRecognizing)
            {
                // Convert the hex color to the Color structure
                Color customColor = (Color)ColorConverter.ConvertFromString("#957e5a");

                // Create a SolidColorBrush with the specified Color
                SolidColorBrush brush = new SolidColorBrush(customColor);
                ListeningText.Foreground = brush;
                ListeningText.Content = "Listening...";
                isRecognizing = true;

                recognizer.SpeechRecognized += recognizer_SpeechRecognized;
                recognizer.RecognizeAsync(RecognizeMode.Multiple);
            }
        }

        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
            if (grammar == null) { return; }
            if (isLoading) return;
            if (e.Key == Key.CapsLock && isRecognizing && grammar != null)
            {
                ListeningText.Foreground = Brushes.White;
                ListeningText.Content = "Not listening...";

                Thread.Sleep(1500);
                isRecognizing = false;

                recognizer.RecognizeAsyncCancel();
                recognizer.SpeechRecognized -= recognizer_SpeechRecognized;
            }
        }


        private async void LoadGameTermsAsync()
        {
            try
            {
                await Task.Run(async () =>
                {
                    // Your asynchronous code to load game terms from the API...
                    await LoadGameTermsFromAPI();
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        // Example function to check fuzzy matching using Levenshtein distance
        private bool IsFuzzyMatch(string recognizedWord, string grammarWord, int threshold)
        {
            int n = recognizedWord.Length;
            int m = grammarWord.Length;

            int[,] dp = new int[n + 1, m + 1];

            for (int i = 0; i <= n; i++)
            {
                for (int j = 0; j <= m; j++)
                {
                    if (i == 0)
                        dp[i, j] = j;
                    else if (j == 0)
                        dp[i, j] = i;
                    else if (recognizedWord[i - 1] == grammarWord[j - 1])
                        dp[i, j] = dp[i - 1, j - 1];
                    else
                        dp[i, j] = 1 + Math.Min(dp[i, j - 1], Math.Min(dp[i - 1, j], dp[i - 1, j - 1]));
                }
            }

            return dp[n, m] <= threshold;
        }

        private async Task LoadGameTermsFromAPI()
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    HttpResponseMessage response = await client.GetAsync("http://23.92.30.86/items");
                    if (response.IsSuccessStatusCode)
                    {
                        string json = await response.Content.ReadAsStringAsync();
                        // Deserialize the JSON response to extract item names
                        List<string> itemNames = ExtractItemNamesFromJson(json);

                        // Load item names into Choices object for recognition
                        Choices gameTerms = new Choices();
                        gameTerms.Add(itemNames.ToArray()); // Add item names to Choices

                        GrammarBuilder grammarBuilder = new GrammarBuilder();
                        grammarBuilder.Append(gameTerms);

                        grammar = new Grammar(grammarBuilder);
                        InitializeSpeechRecognition();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error fetching data from API: " + ex.Message);
            }
        }

        private List<string> ExtractItemNamesFromJson(string json)
        {
            List<string> itemNames = new List<string>();

            try
            {
                // Deserialize the JSON string into a list of dynamic objects
                var items = JsonConvert.DeserializeObject<List<dynamic>>(json);

                // Extract "name" property from each object and add it to itemNames list
                foreach (var item in items)
                {
                    string itemName = item.name; // Assuming "name" is the property that holds the item name
                    itemNames.Add(itemName);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error extracting item names: " + ex.Message);
                // Handle the exception as needed
            }

            // Hide loading indicators (Loading1 and Loading2) if defined
            //if (Loading1 != null && Loading2 != null)
            //{
            //    Loading1.Visible = false;
            //    Loading2.Visible = false;
            //}

            grammarWords = itemNames;
            return itemNames;
        }

        private void InitializeSpeechRecognition()
        {
            recognizer = new SpeechRecognitionEngine();
            recognizer.SetInputToDefaultAudioDevice();
            recognizer.LoadGrammar(grammar);
            StopLoading();
        }

        private void recognizer_SpeechRecognized(object sender, SpeechRecognizedEventArgs e)
        {
            if (!isRecognizing)
                return;

            string resultText = e.Result.Text;
            System.Diagnostics.Debug.WriteLine("Recognized: " + resultText);

            foreach (string grammarWord in grammarWords)
            {
                if (IsFuzzyMatch(resultText, grammarWord, 1))
                {
                    RunGetItemData(grammarWord);
                }
            }
        }
        private async void RunGetItemData(string itemName)
        {
            try
            {
                await GetItemData(itemName);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        private async Task GetItemData(string itemName)
        {
            try
            {
                Console.WriteLine($"Item Name: {itemName}");
                string url = $"https://api.tarkov-market.app/api/v1/item?q={Uri.EscapeDataString(itemName)}&x-api-key=HngkuqFOXPLalgwf"; // Ensure proper encoding for item name in the URL
                
                Console.WriteLine($"URL: {url}"); // Log the URL for debugging

                using (HttpClient client = new HttpClient())
                {
                    HttpResponseMessage res = await client.GetAsync(url);

                    if (res.IsSuccessStatusCode)
                    {
                        string content = await res.Content.ReadAsStringAsync();
                        // Deserialize the JSON response to a list of items
                        var items = JsonConvert.DeserializeObject<List<ItemData>>(content);

                        Console.WriteLine($"Items Count: {items?.Count}"); // Log the count of items for debugging

                        if(items?.Count > 0)
                        {
                            var firstItem = items[0];
                            UpdateInterfaceWithData(firstItem, items[0].Price);
                        }
                    }
                    else
                    {
                        Console.WriteLine($"HTTP Error: {res.StatusCode} - {res.ReasonPhrase}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception occurred: {ex}");
            }
        }

        private void SetImageSource(string imagePath)
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri(imagePath, UriKind.RelativeOrAbsolute);
            image.EndInit();

            ItemImage.Source = image;
        }

        private void UpdateInterfaceWithData(ItemData data, int price)
        {
            Console.WriteLine(price);
            Dispatcher.Invoke(() =>
            {
                ItemTraderPrice.Content = "Trader Price: " + price.ToString();
            });
            try
            {
                Dispatcher.Invoke(() =>
                {
                    ItemName.Content = data.Name;
                    ItemFleaPrice.Content = "Flea price: " + data.Avg24hPrice;
                    PricePerSlot.Content = "Price per slot: " + (data.Avg24hPrice > price ? data.Avg24hPrice / data.Slots : price / data.Slots);
                    TraderName.Content = "Trader: " + data.TraderName;
                    SetImageSource(data.ImgBig);
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error updating UI: " + ex.Message);
            }
        }
    }
}
