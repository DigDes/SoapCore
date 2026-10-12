using System.ServiceModel;

namespace ServerWithUi.Weather
{
	/// <summary>
	/// Deterministic in-memory implementation of <see cref="IWeatherService"/>: the numbers for a given city and
	/// date are always the same, so the explorer shows repeatable results. The report id and timestamp are generated per call.
	/// </summary>
	public sealed class WeatherService : IWeatherService
	{
		private const int MaxForecastDays = 14;

		private static readonly string[] WindDirections = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };

		private static readonly CityInfo[] KnownCities =
		{
			new CityInfo { Code = "BOG", Name = "Bogota", Country = "CO" },
			new CityInfo { Code = "MDE", Name = "Medellin", Country = "CO" },
			new CityInfo { Code = "NYC", Name = "New York", Country = "US" },
			new CityInfo { Code = "LON", Name = "London", Country = "GB" }
		};

		private readonly ILogger<WeatherService> _logger;

		public WeatherService(ILogger<WeatherService> logger)
		{
			_logger = logger;
		}

		public WeatherReport GetReport(WeatherRequest request)
		{
			var city = RequireCity(request?.CityCode);
			var firstDay = (request?.Date ?? DateTime.UtcNow).Date;
			var forecasts = CreateForecasts(city, firstDay, 5);

			return new WeatherReport
			{
				Id = Guid.NewGuid(),
				GeneratedAt = DateTime.UtcNow,
				City = new CityInfo { Code = city.Code, Name = city.Name, Country = city.Country },
				Forecasts = forecasts,
				Statistics = new WeatherStatistics
				{
					AverageTemperature = Math.Round(forecasts.Average(f => (f.TemperatureMin + f.TemperatureMax) / 2), 1),
					TotalForecastDays = forecasts.Count
				}
			};
		}

		public List<DailyForecast> GetForecast(string cityCode, DateTime from, int days)
		{
			// Lenient on purpose: an unknown city gives an empty list. GetReport and GetDailyForecast raise a fault instead.
			var city = FindCity(cityCode);
			if (city == null)
			{
				return new List<DailyForecast>();
			}

			return CreateForecasts(city, from.Date, Math.Clamp(days, 1, MaxForecastDays));
		}

		public WeatherCondition GetCurrentCondition(string cityCode)
		{
			var city = FindCity(cityCode);
			return city == null ? WeatherCondition.Cloudy : CreateForecast(city, DateTime.UtcNow.Date).Condition;
		}

		public bool CityExists(string cityCode)
		{
			return FindCity(cityCode) != null;
		}

		public void SubscribeToAlerts(AlertSubscription subscription)
		{
			// One-way: the caller gets an HTTP 202 and no body. Request values are not logged, they are untrusted input.
			var city = FindCity(subscription?.CityCode);
			_logger.LogInformation("Alert subscription received for {City}.", city?.Name ?? "an unknown city");
		}

		public DailyForecast GetDailyForecast(string cityCode, DateTime date)
		{
			return CreateForecast(RequireCity(cityCode), date.Date);
		}

		private static CityInfo FindCity(string cityCode)
		{
			if (string.IsNullOrWhiteSpace(cityCode))
			{
				return null;
			}

			var code = cityCode.Trim();
			foreach (var city in KnownCities)
			{
				if (string.Equals(city.Code, code, StringComparison.OrdinalIgnoreCase))
				{
					return city;
				}
			}

			return null;
		}

		private static CityInfo RequireCity(string cityCode)
		{
			var city = FindCity(cityCode);
			if (city != null)
			{
				return city;
			}

			// FaultException<T> becomes a SOAP Fault whose detail is the serialized WeatherFault.
			throw new FaultException<WeatherFault>(
				new WeatherFault { CityCode = cityCode, Reason = "Unknown city code." },
				new FaultReason("City '" + cityCode + "' is not supported."),
				new FaultCode("Client"));
		}

		private static List<DailyForecast> CreateForecasts(CityInfo city, DateTime firstDay, int days)
		{
			var forecasts = new List<DailyForecast>(days);
			for (var i = 0; i < days; i++)
			{
				forecasts.Add(CreateForecast(city, firstDay.AddDays(i)));
			}

			return forecasts;
		}

		private static DailyForecast CreateForecast(CityInfo city, DateTime date)
		{
			var seed = date.Year + date.DayOfYear;
			foreach (var c in city.Code)
			{
				seed += c;
			}

			var condition = (WeatherCondition)(seed % 5);
			var min = 8 + (seed % 10);

			var alerts = new List<string>();
			switch (condition)
			{
				case WeatherCondition.Rainy:
					alerts.Add("Light rain");
					break;
				case WeatherCondition.Stormy:
					alerts.Add("Thunderstorm warning");
					alerts.Add("Strong winds");
					break;
				case WeatherCondition.Snowy:
					alerts.Add("Snow expected");
					break;
			}

			return new DailyForecast
			{
				Date = date,
				TemperatureMin = min,
				TemperatureMax = min + 6 + (seed % 5),
				Condition = condition,
				Wind = new WindInfo
				{
					SpeedKmH = 5 + ((seed % 7) * 4.5),
					Direction = WindDirections[seed % WindDirections.Length]
				},
				Alerts = alerts
			};
		}
	}
}
