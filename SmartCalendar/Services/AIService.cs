// DOSYA: AIService.cs
// (Mevcut dosyanın içeriğini bununla değiştirin)

using SmartCalendar.Models;
using System.Text.Json; // <-- YENİ

namespace SmartCalendar.Services
{
    public class AIService
    {
        private readonly HttpClient _httpClient;

        public AIService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        // Fixed method signature and body to resolve CS1992 and CS0103
        public async Task<JsonElement> GetEventRecommendationAsync(List<EventDto> userEvents)
        {
            var request = new
            {
                history = userEvents.Select(e => new
                {
                    day = e.Day,
                    time = e.Time,
                    title = e.Title,
                    tags = e.Tags,
                    location = e.Location
                })
            };

            // Ensure 'response' is declared in the correct context
            var response = await _httpClient.PostAsJsonAsync("http://localhost:5001/api/recommend-events", request);

            if (!response.IsSuccessStatusCode)
            {
                // Return a JSON object indicating failure
                return JsonSerializer.SerializeToElement(new { success = false, message = "AI önerisi alınamadı." });
            }

            // Read and return the JSON response
            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            return json;
        }
    }
    }
