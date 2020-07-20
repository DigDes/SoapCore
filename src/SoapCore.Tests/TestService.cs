using System;
using System.Collections.Generic;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SoapCore.Tests.Model;

namespace SoapCore.Tests
{
	public class TestService : ITestService
	{
		private ThreadLocal<string> _pingResultValue = new ThreadLocal<string>() { Value = string.Empty };

		public string Ping(string s)
		{
			return s;
		}

		public string EmptyArgs()
		{
			return "EmptyArgs";
		}

		public string SingleInteger(int i)
		{
			return i.ToString();
		}

		public Task<string> AsyncMethod()
		{
			return Task.FromResult<string>("hello, async");
		}

		public bool IsNull(double? d)
		{
			return !d.HasValue;
		}

		public void ThrowException()
		{
			throw new Exception();
		}

		public async Task ThrowExceptionAsync()
		{
			await Task.Run(() => throw new Exception());
		}

		public void ThrowExceptionWithMessage(string message)
		{
			throw new Exception(message);
		}

		public void ThrowDetailedFault(string detailMessage)
		{
			throw new FaultException<FaultDetail>(new FaultDetail { ExceptionProperty = detailMessage }, new FaultReason("test"), new FaultCode("test"), "test");
		}

		public string Overload(double d)
		{
			return "Overload(double)";
		}

		public string Overload(string s)
		{
			return "Overload(string)";
		}

		public bool OperationName() => true;

		public void OutParam(out string message)
		{
			message = "hello, world";
		}

		public void OutComplexParam(out ComplexModelInput test)
		{
			test = new ComplexModelInput();
			test.StringProperty = "test message";
			test.IntProperty = 10;
			test.ListProperty = new List<string> { "test", "list", "of", "strings" };
		}

		public ComplexModelInput ComplexParam(ComplexModelInput test)
		{
			return test;
		}

		public ComplexModelInputForModelBindingFilter ComplexParamWithModelBindingFilter(ComplexModelInputForModelBindingFilter test)
		{
			return test;
		}

		public void RefParam(ref string message)
		{
			message = "hello, world";
		}

		[ServiceFilter(typeof(ActionFilter.TestActionFilter))]
		public ComplexModelInput ComplexParamWithActionFilter(ComplexModelInput test)
		{
			return test;
		}

		public void SetPingResult(string value)
		{
			_pingResultValue.Value = value;
		}

		public string PingWithServiceOperationTuning()
		{
			return _pingResultValue.Value;
		}

		public ComplexModelInput[] ArrayOfComplexItems(ComplexModelInput[] items)
		{
			return items;
		}

		public List<ComplexModelInput> ListOfComplexItems(List<ComplexModelInput> items)
		{
			return items;
		}

		public Dictionary<string, string> ListOfDictionaryItems(Dictionary<string, string> items)
		{
			return items;
		}

		public ComplexInheritanceModelInputBase GetComplexInheritanceModel(ComplexInheritanceModelInputBase input)
		{
			switch (input)
			{
				case ComplexInheritanceModelInputB _:
					{
						return new ComplexInheritanceModelInputB();
					}

				case ComplexInheritanceModelInputA _:
					{
						return new ComplexInheritanceModelInputA();
					}

				default:
					{
						throw new NotImplementedException();
					}
			}
		}

		public ComplexModelInput ComplexModelInputFromServiceKnownType(object value)
		{
			if (value is null)
			{
				throw new ArgumentNullException(nameof(value));
			}

			if (value is ComplexModelInput input)
			{
				return input;
			}

			throw new Exception($"Invalid object type `{value.GetType()}`.");
		}

		public object ObjectFromServiceKnownType(ComplexModelInput value)
		{
			if (value is null)
			{
				throw new ArgumentNullException(nameof(value));
			}

			return value;
		}
	}
}
