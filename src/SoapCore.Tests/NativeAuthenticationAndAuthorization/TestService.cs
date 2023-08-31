using System.ServiceModel;
using SoapCore.Tests.Model;

namespace SoapCore.Tests.NativeAuthenticationAndAuthorization
{
	[ServiceContract]
	public abstract class TestService : ITestService
	{
		[OperationContract]
		public abstract string JwtAuthenticationAndAuthorizationIActionResultUnprotected(ComplexModelInput payload);

		[OperationContract]
		public abstract string JwtAuthenticationAndAuthorizationIActionResultJustAuthenticated(ComplexModelInput payload);

		[OperationContract]
		public abstract string JwtAuthenticationAndAuthorizationIActionResultUsingPolicy(ComplexModelInput payload);

		[OperationContract]
		public abstract string JwtAuthenticationAndAuthorizationIActionResult(ComplexModelInput payload);

		[OperationContract]
		public abstract string JwtAuthenticationAndAuthorizationActionResult(ComplexModelInput payload);

		[OperationContract]
		public abstract string JwtAuthenticationAndAuthorizationGenericActionResult(ComplexModelInput payload);

		[OperationContract]
		public abstract ComplexModelInput JwtAuthenticationAndAuthorizationComplexGenericActionResult(ComplexModelInput payload);
	}
}
