using System.ServiceModel;
using SoapCore.Tests.Model;

namespace SoapCore.Tests.NativeAuthenticationAndAuthorization
{
	[ServiceContract]
	public interface ITestService
	{
		[OperationContract]
		string JwtAuthenticationAndAuthorizationIActionResultUnprotected(ComplexModelInput payload);

		[OperationContract]
		string JwtAuthenticationAndAuthorizationIActionResultJustAuthenticated(ComplexModelInput payload);

		[OperationContract]
		string JwtAuthenticationAndAuthorizationIActionResultUsingPolicy(ComplexModelInput payload);

		[OperationContract]
		string JwtAuthenticationAndAuthorizationIActionResult(ComplexModelInput payload);

		[OperationContract]
		string JwtAuthenticationAndAuthorizationActionResult(ComplexModelInput payload);

		[OperationContract]
		string JwtAuthenticationAndAuthorizationGenericActionResult(ComplexModelInput payload);

		[OperationContract]
		ComplexModelInput JwtAuthenticationAndAuthorizationComplexGenericActionResult(ComplexModelInput payload);
	}
}
