using System.ComponentModel;
using System.ServiceModel;

namespace ServerWithUi.Weather
{
	/// <summary>
	/// A small weather contract that exercises what the explorer can generate from a WSDL:
	/// complex types, lists, enums, dates, a nullable, a one-way operation and a SOAP fault.
	/// The known city codes are BOG, MDE, NYC and LON.
	/// </summary>
	/// <remarks>
	/// The explorer shows the summaries below as descriptions. They come from the XML documentation file of this project
	/// (<c>options.IncludeXmlComments(...)</c> in Program.cs). Two operations use <c>[Description]</c> instead, which needs no XML file.
	/// </remarks>
	[ServiceContract(Namespace = "http://soapcore.dev/samples/weather")]
	public interface IWeatherService
	{
		/// <summary>
		/// Builds a five day weather report for a city: the city, the forecasts and some statistics.
		/// </summary>
		/// <param name="request">The city code and, optionally, the first day of the report (today when it is empty).</param>
		/// <returns>The report. An unknown city code raises a <see cref="WeatherFault"/>.</returns>
		[OperationContract]
		WeatherReport GetReport(WeatherRequest request);

		/// <summary>
		/// Lists the daily forecasts of a city, from a date on. An unknown city gives an empty list.
		/// </summary>
		/// <param name="cityCode">The code of the city, for example BOG.</param>
		/// <param name="from">The first day.</param>
		/// <param name="days">How many days, from 1 to 14.</param>
		/// <returns>One forecast per day.</returns>
		[OperationContract]
		List<DailyForecast> GetForecast(string cityCode, DateTime from, int days);

		/// <summary>
		/// Gets the weather condition of a city today (<see cref="WeatherCondition.Cloudy"/> for an unknown city).
		/// </summary>
		/// <param name="cityCode">The code of the city, for example MDE.</param>
		/// <returns>The condition.</returns>
		[OperationContract]
		WeatherCondition GetCurrentCondition(string cityCode);

		[Description("Tells whether the city code is one of the cities of the demo catalog.")]
		[OperationContract]
		bool CityExists([Description("The code of the city, for example NYC.")] string cityCode);

		[Description("Subscribes an e-mail address to the weather alerts of a city. One-way: the answer is an HTTP 202 with no body.")]
		[OperationContract(IsOneWay = true)]
		void SubscribeToAlerts(AlertSubscription subscription);

		/// <summary>
		/// Gets the forecast of one day. An unknown city raises a <see cref="WeatherFault"/>, which the explorer shows as a SOAP fault.
		/// </summary>
		/// <param name="cityCode">The code of the city, for example LON.</param>
		/// <param name="date">The day.</param>
		/// <returns>The forecast.</returns>
		[OperationContract]
		[FaultContract(typeof(WeatherFault))]
		DailyForecast GetDailyForecast(string cityCode, DateTime date);
	}
}
