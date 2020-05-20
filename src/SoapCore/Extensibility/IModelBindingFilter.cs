using System;
using System.Collections.Generic;

namespace SoapCore.Extensibility
{
	public interface IModelBindingFilter
	{
		List<Type> ModelTypes { get; set; }
		void OnModelBound(object model, IServiceProvider serviceProvider, out object output);
	}
}
