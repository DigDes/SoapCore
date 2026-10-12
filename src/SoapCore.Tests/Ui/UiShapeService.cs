using System.Collections.Generic;

namespace SoapCore.Tests.Ui
{
	public class UiShapeService : IUiShapeService
	{
		public UiShapeModels.Reply Get(UiShapeModels.Request request)
		{
			return new UiShapeModels.Reply
			{
				Items = new List<UiShapeModels.Item>
				{
					new UiShapeModels.Item { Label = request?.Name, Amount = 1m },
				},
				Shape = new UiShapeModels.Derived { Name = request?.Name, Extra = request?.Count ?? 0 },
			};
		}
	}
}
