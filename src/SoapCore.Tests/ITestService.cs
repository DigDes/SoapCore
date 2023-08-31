using System.Collections.Generic;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SoapCore.Tests.Model;

namespace SoapCore.Tests
{
	[ServiceContract]
	public interface ITestService
	{
		[OperationContract]
		string Ping(string s);

		[OperationContract]
		string EmptyArgs();

		[OperationContract]
		string SingleInteger(int i);

		[OperationContract]
		Task<string> AsyncMethod();

		[OperationContract]
		bool IsNull(double? d);

		[OperationContract(Name = "OperationNameTest")]
		bool OperationName();

		[OperationContract]
		string Overload(string s);

		[OperationContract(Name = "OverloadDouble")]
		string Overload(double d);

		[OperationContract]
		void OutParam(out string message);

		[OperationContract]
		void OutComplexParam(out ComplexModelInput test);

		[OperationContract]
		ComplexModelInput ComplexParam(ComplexModelInput test);

		[OperationContract]
		ComplexModelInputForModelBindingFilter ComplexParamWithModelBindingFilter(ComplexModelInputForModelBindingFilter test);

		[OperationContract]
		void RefParam(ref string message);

		[OperationContract]
		void ThrowException();

		[OperationContract(Name = "ThrowExceptionAsync")]
		Task ThrowExceptionAsync();

		[OperationContract]
		void ThrowExceptionWithMessage(string message);

		[OperationContract]
		[FaultContract(typeof(FaultDetail))]
		void ThrowDetailedFault(string detailMessage);

		[OperationContract]
		[ServiceFilter(typeof(ActionFilter.TestActionFilter))]
		ComplexModelInput ComplexParamWithActionFilter(ComplexModelInput test);

		[OperationContract]
		string PingWithServiceOperationTuning();

		[OperationContract]
		ComplexModelInput[] ArrayOfComplexItems(ComplexModelInput[] items);

		[OperationContract]
		List<ComplexModelInput> ListOfComplexItems(List<ComplexModelInput> items);

		[OperationContract]
		Dictionary<string, string> ListOfDictionaryItems(Dictionary<string, string> items);

		[OperationContract]
		ComplexInheritanceModelInputBase GetComplexInheritanceModel(ComplexInheritanceModelInputBase input);

		[ServiceKnownType(typeof(ComplexModelInput))]
		[OperationContract]
		ComplexModelInput ComplexModelInputFromServiceKnownType(object value);

		[ServiceKnownType(typeof(ComplexModelInput))]
		[OperationContract]
		object ObjectFromServiceKnownType(ComplexModelInput value);

		[OperationContract]
		string EmpryBody(EmptyMembers members);

		[ServiceKnownType("GetKnownTypes", typeof(TestServiceKnownTypesProvider))]
		[OperationContract]
		IComplexTreeModelInput GetComplexModelInputFromKnownTypeProvider(ComplexModelInput value);

		[OperationContract]
		XmlElement ReturnXmlElement();

		[OperationContract]
		XmlElement XmlElementInput(XmlElement input);

		/// <summary>
		/// Return type is different than the one bellow due to customizations. Use SoapCore.Tests.NativeAuthenticationAndAuthorization.IActionResultContractService to access these endpoints.
		/// </summary>
		/// <param name="payload">Payload</param>
		/// <returns>The service should return a different type from this one. It will be of type object, more specifically string.</returns>
		[OperationContract]
		IActionResult JwtAuthenticationAndAuthorizationIActionResultUnprotected(ComplexModelInput payload);

		/// <summary>
		/// Return type is different than the one bellow due to customizations. Use SoapCore.Tests.NativeAuthenticationAndAuthorization.IActionResultContractService to access these endpoints.
		/// </summary>
		/// <param name="payload">Payload</param>
		/// <returns>The service should return a different type from this one. It will be of type object, more specifically string.</returns>
		[Authorize]
		[OperationContract]
		IActionResult JwtAuthenticationAndAuthorizationIActionResultJustAuthenticated(ComplexModelInput payload);

		/// <summary>
		/// Return type is different than the one bellow due to customizations. Use SoapCore.Tests.NativeAuthenticationAndAuthorization.IActionResultContractService to access these endpoints.
		/// </summary>
		/// <param name="payload">Payload</param>
		/// <returns>The service should return a different type from this one. It will be of type object, more specifically string.</returns>
		[Authorize(Policy = "something")]
		[OperationContract]
		IActionResult JwtAuthenticationAndAuthorizationIActionResultUsingPolicy(ComplexModelInput payload);

		/// <summary>
		/// Return type is different than the one bellow due to customizations. Use SoapCore.Tests.NativeAuthenticationAndAuthorization.IActionResultContractService to access these endpoints.
		/// </summary>
		/// <param name="payload">Payload</param>
		/// <returns>The service should return a different type from this one. It will be of type object, more specifically string.</returns>
		[Authorize(Roles = "role1")]
		[OperationContract]
		IActionResult JwtAuthenticationAndAuthorizationIActionResult(ComplexModelInput payload);

		/// <summary>
		/// Return type is different than the one bellow due to customizations. Use SoapCore.Tests.NativeAuthenticationAndAuthorization.IActionResultContractService to access these endpoints.
		/// </summary>
		/// <param name="payload">Payload</param>
		/// <returns>The service should return a different type from this one. It will be of type object, more specifically string.</returns>
		[Authorize(Roles = "role1")]
		[OperationContract]
		ActionResult JwtAuthenticationAndAuthorizationActionResult(ComplexModelInput payload);

		/// <summary>
		/// Return type is different than the one bellow due to customizations. Use SoapCore.Tests.NativeAuthenticationAndAuthorization.IActionResultContractService to access these endpoints.
		/// </summary>
		/// <param name="payload">Payload</param>
		/// <returns>The service should return a different type from this one. It will be of type string.</returns>
		[Authorize(Roles = "role1")]
		[OperationContract]
		ActionResult<string> JwtAuthenticationAndAuthorizationGenericActionResult(ComplexModelInput payload);

		/// <summary>
		/// Return type is different than the one bellow due to customizations. Use SoapCore.Tests.NativeAuthenticationAndAuthorization.IActionResultContractService to access these endpoints.
		/// </summary>
		/// <param name="payload">Payload</param>
		/// <returns>The service should return a different type from this one. It will be of type ComplexModelInput.</returns>
		[Authorize(Roles = "role1")]
		[OperationContract]
		ActionResult<ComplexModelInput> JwtAuthenticationAndAuthorizationComplexGenericActionResult(ComplexModelInput payload);
	}
}
