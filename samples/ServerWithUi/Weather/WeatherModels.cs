using System.Runtime.Serialization;

namespace ServerWithUi.Weather
{
	// Order is set explicitly on every member and equals the declaration order, so DataContractSerializer
	// (which sorts by Order) and XmlSerializer (which uses declaration order) produce the same element order.

	/// <summary>
	/// The request of <c>GetReport</c>: which city, and from which day.
	/// </summary>
	[DataContract(Namespace = "http://soapcore.dev/samples/weather")]
	public class WeatherRequest
	{
		[DataMember(Order = 0)]
		public string CityCode { get; set; }

		[DataMember(Order = 1)]
		public DateTime? Date { get; set; }
	}

	/// <summary>
	/// A five day weather report: the city, the forecasts and some statistics.
	/// </summary>
	[DataContract(Namespace = "http://soapcore.dev/samples/weather")]
	public class WeatherReport
	{
		[DataMember(Order = 0)]
		public Guid Id { get; set; }

		[DataMember(Order = 1)]
		public DateTime GeneratedAt { get; set; }

		[DataMember(Order = 2)]
		public CityInfo City { get; set; }

		[DataMember(Order = 3)]
		public List<DailyForecast> Forecasts { get; set; }

		[DataMember(Order = 4)]
		public WeatherStatistics Statistics { get; set; }
	}

	/// <summary>
	/// A city of the demo catalog.
	/// </summary>
	[DataContract(Namespace = "http://soapcore.dev/samples/weather")]
	public class CityInfo
	{
		[DataMember(Order = 0)]
		public string Code { get; set; }

		[DataMember(Order = 1)]
		public string Name { get; set; }

		[DataMember(Order = 2)]
		public string Country { get; set; }
	}

	/// <summary>
	/// The forecast of one day.
	/// </summary>
	[DataContract(Namespace = "http://soapcore.dev/samples/weather")]
	public class DailyForecast
	{
		[DataMember(Order = 0)]
		public DateTime Date { get; set; }

		[DataMember(Order = 1)]
		public decimal TemperatureMin { get; set; }

		[DataMember(Order = 2)]
		public decimal TemperatureMax { get; set; }

		[DataMember(Order = 3)]
		public WeatherCondition Condition { get; set; }

		[DataMember(Order = 4)]
		public WindInfo Wind { get; set; }

		[DataMember(Order = 5)]
		public List<string> Alerts { get; set; }
	}

	/// <summary>
	/// The wind of a forecast: speed in km/h and compass direction.
	/// </summary>
	[DataContract(Namespace = "http://soapcore.dev/samples/weather")]
	public class WindInfo
	{
		[DataMember(Order = 0)]
		public double SpeedKmH { get; set; }

		[DataMember(Order = 1)]
		public string Direction { get; set; }
	}

	/// <summary>
	/// Numbers computed from the forecasts of a report.
	/// </summary>
	[DataContract(Namespace = "http://soapcore.dev/samples/weather")]
	public class WeatherStatistics
	{
		[DataMember(Order = 0)]
		public decimal AverageTemperature { get; set; }

		[DataMember(Order = 1)]
		public int TotalForecastDays { get; set; }
	}

	/// <summary>
	/// The data of <c>SubscribeToAlerts</c>: a city and the e-mail address that gets its alerts.
	/// </summary>
	[DataContract(Namespace = "http://soapcore.dev/samples/weather")]
	public class AlertSubscription
	{
		[DataMember(Order = 0)]
		public string CityCode { get; set; }

		[DataMember(Order = 1)]
		public string Email { get; set; }
	}

	/// <summary>
	/// Detail of the SOAP fault raised for an unknown city.
	/// </summary>
	[DataContract(Namespace = "http://soapcore.dev/samples/weather")]
	public class WeatherFault
	{
		[DataMember(Order = 0)]
		public string CityCode { get; set; }

		[DataMember(Order = 1)]
		public string Reason { get; set; }
	}

	/// <summary>
	/// The weather condition of a day.
	/// </summary>
	public enum WeatherCondition
	{
		Sunny,
		Cloudy,
		Rainy,
		Stormy,
		Snowy
	}
}
