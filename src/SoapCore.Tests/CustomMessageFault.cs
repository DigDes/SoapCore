using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace SoapCore.Tests
{
	internal class CustomMessageFault : MessageFault
	{
		private readonly string _reason;
		private readonly string _actor;

		public CustomMessageFault(string reason, string actor)
		{
			_reason = reason;
			_actor = actor;
		}

		public override FaultCode Code => new FaultCode("Client");

		public override bool HasDetail => false;

		public override FaultReason Reason => new FaultReason(_reason);

		public override string Actor => _actor;

		protected override void OnWriteDetailContents(XmlDictionaryWriter writer)
		{
			throw new NotSupportedException();
		}
	}
}
