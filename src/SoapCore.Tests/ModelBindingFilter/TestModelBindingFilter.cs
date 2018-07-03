using System;
using System.Collections.Generic;

namespace SoapCore.Tests.ModelBindingFilter
{
	public class TestModelBindingFilter : IModelBindingFilter
	{
		public List<Type> ModelTypes { get; set; }

		public TestModelBindingFilter(List<Type> modelTypes)
		{
			ModelTypes = modelTypes;
		}

		public void OnModelBound(object model, IServiceProvider serviceProvider, out object result)
		{
			var complexModel = (ComplexModelInputForModelBindingFilter)model;
			complexModel.StringProperty += "MODIFIED BY TestModelBindingFilter";
			complexModel.IntProperty = complexModel.IntProperty * 2;
			result = true;
		}
	}
}
