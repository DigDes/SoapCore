using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading.Tasks;

namespace SoapCore.Tests.MessageInspectors.MessageInspector2
{
	public class MessageInspector2MockNoException : MessageInspector2Mock
	{
		protected override void ValidateMessage(ref Message message)
		{
		}
	}
}
