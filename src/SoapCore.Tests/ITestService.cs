using System.Collections.Generic;
using System.ServiceModel;
using System.Threading.Tasks;
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
	}
}
