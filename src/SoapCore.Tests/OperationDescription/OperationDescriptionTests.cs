using System.ServiceModel;
using SoapCore.Tests.OperationDescription;
using Xunit;

namespace SoapCore.Tests
{
	public class OperationDescriptionTests
	{
		[Fact]
		public void TestProperUnrappingOfGenericResponses()
		{
			ServiceDescription serviceDescription = new ServiceDescription(typeof(IServiceWithMessageContract));
			ContractDescription contractDescription = new ContractDescription(serviceDescription, typeof(IServiceWithMessageContract), new ServiceContractAttribute());

			System.Reflection.MethodInfo method = typeof(IServiceWithMessageContract).GetMethod(nameof(IServiceWithMessageContract.GetMyClass));

			OperationContractAttribute contractAttribute = new OperationContractAttribute();

			SoapCore.OperationDescription operationDescription = new SoapCore.OperationDescription(contractDescription, method, contractAttribute);

			Assert.True(operationDescription.IsMessageContractResponse);
		}

		[Fact]
		public void TestProperUnrappingOfNonGenericResponses()
		{
			ServiceDescription serviceDescription = new ServiceDescription(typeof(IServiceWithMessageContract));
			ContractDescription contractDescription = new ContractDescription(serviceDescription, typeof(IServiceWithMessageContract), new ServiceContractAttribute());

			System.Reflection.MethodInfo method = typeof(IServiceWithMessageContract).GetMethod(nameof(IServiceWithMessageContract.GetMyOtherClass));

			OperationContractAttribute contractAttribute = new OperationContractAttribute();

			SoapCore.OperationDescription operationDescription = new SoapCore.OperationDescription(contractDescription, method, contractAttribute);

			Assert.True(operationDescription.IsMessageContractResponse);
		}

		[Fact]
		public void TestProperUnrappingOfNonMessageContractResponses()
		{
			ServiceDescription serviceDescription = new ServiceDescription(typeof(IServiceWithMessageContract));
			ContractDescription contractDescription = new ContractDescription(serviceDescription, typeof(IServiceWithMessageContract), new ServiceContractAttribute());

			System.Reflection.MethodInfo method = typeof(IServiceWithMessageContract).GetMethod(nameof(IServiceWithMessageContract.GetMyStringClass));

			OperationContractAttribute contractAttribute = new OperationContractAttribute();

			SoapCore.OperationDescription operationDescription = new SoapCore.OperationDescription(contractDescription, method, contractAttribute);

			Assert.False(operationDescription.IsMessageContractResponse);
		}
	}
}
