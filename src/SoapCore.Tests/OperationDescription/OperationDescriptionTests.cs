using System.Linq;
using System.ServiceModel;
using SoapCore.ServiceModel;
using SoapCore.Tests.OperationDescription;
using Xunit;

namespace SoapCore.Tests
{
	public class OperationDescriptionTests
	{
		[Fact]
		public void TestProperUnrappingOfGenericResponses()
		{
			ServiceDescription serviceDescription = new ServiceDescription(typeof(IServiceWithMessageContract), false);
			ContractDescription contractDescription = new ContractDescription(serviceDescription, typeof(IServiceWithMessageContract), new ServiceContractAttribute(), false);

			System.Reflection.MethodInfo method = typeof(IServiceWithMessageContract).GetMethod(nameof(IServiceWithMessageContract.GetMyClass));

			OperationContractAttribute contractAttribute = new OperationContractAttribute();

			ServiceModel.OperationDescription operationDescription = new ServiceModel.OperationDescription(contractDescription, method, contractAttribute, false);

			Assert.True(operationDescription.IsMessageContractResponse);
		}

		[Fact]
		public void TestSupportXmlRootForParameterName()
		{
			ServiceDescription serviceDescription = new ServiceDescription(typeof(IServiceWithMessageContract), false);
			ContractDescription contractDescription = new ContractDescription(serviceDescription, typeof(IServiceWithMessageContract), new ServiceContractAttribute(), false);

			System.Reflection.MethodInfo method = typeof(IServiceWithMessageContract).GetMethod(nameof(IServiceWithMessageContract.GetClassWithXmlRoot));

			OperationContractAttribute contractAttribute = new OperationContractAttribute();

			ServiceModel.OperationDescription operationDescription = new ServiceModel.OperationDescription(contractDescription, method, contractAttribute, false);

			Assert.Equal("test", operationDescription.AllParameters.FirstOrDefault()?.Name);
		}

		[Fact]
		public void TestSupportXmlRootForParameterNameWithEmptyStringAsRootElementNameUsesParameterInfoToExtractAName()
		{
			ServiceDescription serviceDescription = new ServiceDescription(typeof(IServiceWithMessageContractAndEmptyXmlRoot), false);
			ContractDescription contractDescription = new ContractDescription(serviceDescription, typeof(IServiceWithMessageContractAndEmptyXmlRoot), new ServiceContractAttribute(), false);

			System.Reflection.MethodInfo method = typeof(IServiceWithMessageContractAndEmptyXmlRoot).GetMethod(nameof(IServiceWithMessageContractAndEmptyXmlRoot.GetClassWithEmptyXmlRoot));

			OperationContractAttribute contractAttribute = new OperationContractAttribute();

			ServiceModel.OperationDescription operationDescription = new ServiceModel.OperationDescription(contractDescription, method, contractAttribute, false);

			Assert.Equal("classWithXmlRoot", operationDescription.AllParameters.FirstOrDefault()?.Name);
		}

		[Fact]
		public void TestProperUnrappingOfNonGenericResponses()
		{
			ServiceDescription serviceDescription = new ServiceDescription(typeof(IServiceWithMessageContract), false);
			ContractDescription contractDescription = new ContractDescription(serviceDescription, typeof(IServiceWithMessageContract), new ServiceContractAttribute(), false);

			System.Reflection.MethodInfo method = typeof(IServiceWithMessageContract).GetMethod(nameof(IServiceWithMessageContract.GetMyOtherClass));

			OperationContractAttribute contractAttribute = new OperationContractAttribute();

			ServiceModel.OperationDescription operationDescription = new ServiceModel.OperationDescription(contractDescription, method, contractAttribute, false);

			Assert.True(operationDescription.IsMessageContractResponse);
		}

		[Fact]
		public void TestProperUnrappingOfNonMessageContractResponses()
		{
			ServiceDescription serviceDescription = new ServiceDescription(typeof(IServiceWithMessageContract), false);
			ContractDescription contractDescription = new ContractDescription(serviceDescription, typeof(IServiceWithMessageContract), new ServiceContractAttribute(), false);

			System.Reflection.MethodInfo method = typeof(IServiceWithMessageContract).GetMethod(nameof(IServiceWithMessageContract.GetMyStringClass));

			OperationContractAttribute contractAttribute = new OperationContractAttribute();

			ServiceModel.OperationDescription operationDescription = new ServiceModel.OperationDescription(contractDescription, method, contractAttribute, false);

			Assert.False(operationDescription.IsMessageContractResponse);
		}

		[Fact]
		public void TestProperUnwrappingOfSoapFaults()
		{
			ServiceDescription serviceDescription = new ServiceDescription(typeof(IServiceWithMessageContract), false);
			ContractDescription contractDescription = new ContractDescription(serviceDescription, typeof(IServiceWithMessageContract), new ServiceContractAttribute(), false);

			System.Reflection.MethodInfo method = typeof(IServiceWithMessageContract).GetMethod(nameof(IServiceWithMessageContract.ThrowTypedFault));

			OperationContractAttribute contractAttribute = new OperationContractAttribute();

			ServiceModel.OperationDescription operationDescription = new ServiceModel.OperationDescription(contractDescription, method, contractAttribute, false);

			var faultInfo = Assert.Single(operationDescription.Faults);
			Assert.Equal("TypedSoapFault", faultInfo.Name);

			var properties = faultInfo.GetProperties().Where(prop => prop.CustomAttributes.All(attr => attr.AttributeType.Name != "IgnoreDataMemberAttribute"));
			var faultProperty = Assert.Single(properties);
			Assert.Equal("MyIncludedProperty", faultProperty.Name);
		}

		[Fact]
		public void TestProperNamingOfAsyncMethods()
		{
			ServiceDescription serviceDescription = new ServiceDescription(typeof(IServiceWithMessageContract), false);
			ContractDescription contractDescription = new ContractDescription(serviceDescription, typeof(IServiceWithMessageContract), new ServiceContractAttribute(), false);

			System.Reflection.MethodInfo method = typeof(IServiceWithMessageContract).GetMethod(nameof(IServiceWithMessageContract.GetMyAsyncClassAsync));

			OperationContractAttribute contractAttribute = new OperationContractAttribute();

			ServiceModel.OperationDescription operationDescription = new ServiceModel.OperationDescription(contractDescription, method, contractAttribute, false);

			Assert.True(operationDescription.IsMessageContractResponse);
			Assert.Equal("GetMyAsyncClass", operationDescription.Name);
		}

		[Fact]
		public void TestGeneratedSoapActionName()
		{
			ServiceDescription serviceDescription = new ServiceDescription(typeof(IServiceWithMessageContract), true);
			ContractDescription contractDescription = new ContractDescription(serviceDescription, typeof(IServiceWithMessageContract), new ServiceContractAttribute(), true);

			System.Reflection.MethodInfo method = typeof(IServiceWithMessageContract).GetMethod(nameof(IServiceWithMessageContract.GetMyOtherClass));

			OperationContractAttribute contractAttribute = new OperationContractAttribute();

			ServiceModel.OperationDescription operationDescription1 = new ServiceModel.OperationDescription(contractDescription, method, contractAttribute, true);

			Assert.Equal("http://tempuri.org/GetMyOtherClass", operationDescription1.SoapAction);

			ServiceModel.OperationDescription operationDescription2 = new ServiceModel.OperationDescription(contractDescription, method, contractAttribute, false);

			Assert.Equal("http://tempuri.org/IServiceWithMessageContract/GetMyOtherClass", operationDescription2.SoapAction);
		}
	}
}
