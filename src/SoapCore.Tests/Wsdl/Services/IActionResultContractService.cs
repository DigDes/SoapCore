using System.ServiceModel;
using Microsoft.AspNetCore.Mvc;
using SoapCore.Tests.Model;

namespace SoapCore.Tests.Wsdl.Services
{
	[ServiceContract]
	public interface IActionResultContractService
	{
		[OperationContract]
		IActionResult IActionResultTest();

		[OperationContract]
		ActionResult ActionResultTest();

		[OperationContract]
		ActionResult<string> GenericActionResultTest();

		[OperationContract]
		ActionResult<ComplexModelInput> ComplexGenericActionResultTest();
	}

	public class ActionResultContractService : IActionResultContractService
	{
		public ActionResult ActionResultTest()
		{
			throw new System.NotImplementedException();
		}

		public ActionResult<ComplexModelInput> ComplexGenericActionResultTest()
		{
			throw new System.NotImplementedException();
		}

		public ActionResult<string> GenericActionResultTest()
		{
			throw new System.NotImplementedException();
		}

		public IActionResult IActionResultTest()
		{
			throw new System.NotImplementedException();
		}
	}
}
