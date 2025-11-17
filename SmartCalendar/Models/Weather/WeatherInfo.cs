namespace SmartCalendar.Models.Weather
{
    public class WeatherInfo
    {
        public string City { get; set; }
        public string Description { get; set; }
        public double Temperature { get; set; }
        public double WindSpeed { get; set; }
        public int Humidity { get; set; }
        public DateTime Date { get; set; }
    }

}
