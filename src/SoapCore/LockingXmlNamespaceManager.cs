using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;

namespace SoapCore
{
	public class LockingXmlNamespaceManager : XmlNamespaceManager
	{
		private object _locker = new object();

		public LockingXmlNamespaceManager(XmlNameTable nameTable) : base(nameTable)
		{
		}

		public override void AddNamespace(string prefix, string uri)
		{
			lock (_locker)
			{
				base.AddNamespace(prefix, uri);
			}
		}

		public override IDictionary<string, string> GetNamespacesInScope(XmlNamespaceScope scope)
		{
			lock (_locker)
			{
				return base.GetNamespacesInScope(scope);
			}
		}

		public override bool HasNamespace(string prefix)
		{
			lock (_locker)
			{
				return base.HasNamespace(prefix);
			}
		}

		public override string LookupNamespace(string prefix)
		{
			lock (_locker)
			{
				return base.LookupNamespace(prefix);
			}
		}

		public override string LookupPrefix(string uri)
		{
			lock (_locker)
			{
				return base.LookupPrefix(uri);
			}
		}

		public override bool PopScope()
		{
			lock (_locker)
			{
				return base.PopScope();
			}
		}

		public override void PushScope()
		{
			lock (_locker)
			{
				base.PushScope();
			}
		}

		public override void RemoveNamespace(string prefix, string uri)
		{
			lock (_locker)
			{
				base.RemoveNamespace(prefix, uri);
			}
		}
	}
}
