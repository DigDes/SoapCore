using System.ServiceModel;
using SoapCore.Meta;
using SoapCore.ServiceModel;
using SoapCore.Tests.Wsdl.Services;
using Xunit;

namespace SoapCore.Tests.Wsdl
{
	public class DefaultWsdlOperationNameGeneratorTests
	{
		private readonly ServiceDescription _serviceDescription;
		private readonly ServiceModel.OperationDescription _operation;
		private readonly DefaultWsdlOperationNameGenerator _generator;

		public DefaultWsdlOperationNameGeneratorTests()
		{
			_serviceDescription = new ServiceDescription(typeof(IEnumService), false);
			var contractDescription = new ContractDescription(_serviceDescription, typeof(IEnumService), new ServiceContractAttribute(), false);
			var method = typeof(IEnumService).GetMethod(nameof(IEnumService.Method));
			var contractAttribute = new OperationContractAttribute();
			_operation = new ServiceModel.OperationDescription(contractDescription, method, contractAttribute, false);

			_generator = new DefaultWsdlOperationNameGenerator();
		}

		[Fact]
		public void TestGenerateWsdlInputMessageName()
		{
			var result = _generator.GenerateWsdlInputMessageName(_operation, _serviceDescription);
			Assert.Equal("IEnumService_Method_InputMessage", result);
		}

		[Fact]
		public void TestGenerateWsdlOutputMessageName()
		{
			var result = _generator.GenerateWsdlOutputMessageName(_operation, _serviceDescription);
			Assert.Equal("IEnumService_Method_OutputMessage", result);
		}
	}
}
