using System;
using System.Collections.Generic;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace SoapCore.Tests.Ui
{
	// A contract for the operation details of the explorer: in, out and ref parameters, void, Task and Task<T> results, declared faults
	// and a message contract. The tests register the interface itself as the service type, so no implementation is needed.
	[ServiceContract(Namespace = "urn:ui-details")]
	public interface IUiDetailsService
	{
		[OperationContract]
		string Add(int left, int right);

		[OperationContract]
		bool TryParse(string text, out int value);

		[OperationContract]
		void Increment(ref int counter);

		[OperationContract]
		bool Split(string text, out List<UiShapeModels.Item> items, ref string separator);

		[OperationContract(IsOneWay = true)]
		void Notify(string message);

		// The operation name is "Touch": only the Async suffix of a Task method is dropped.
		[OperationContract]
		Task Touch(string key);

		// The operation name is "Load". The CancellationToken is not part of the SOAP message.
		[OperationContract]
		Task<UiShapeModels.Reply> LoadAsync(string key, CancellationToken cancellationToken);

		[OperationContract]
		ValueTask<int> Cheap();

		// SoapCore supplies the HttpContext when it calls the method, so it is not part of the SOAP message either.
		[OperationContract]
		string Context(HttpContext context, string text);

		// The same fault type declared twice is listed once.
		[OperationContract]
		[FaultContract(typeof(UiDetailsModels.Fault))]
		[FaultContract(typeof(UiDetailsModels.Fault))]
		[FaultContract(typeof(UiDetailsModels.OtherFault))]
		string Risky(string input);

		[OperationContract]
		UiDetailsModels.Response Contracted(UiDetailsModels.Request request);

		[OperationContract]
		List<UiShapeModels.Item> Items(int? limit, string[] tags, Guid id);
	}
}
