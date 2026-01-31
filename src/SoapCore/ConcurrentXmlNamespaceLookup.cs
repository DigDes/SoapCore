using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Xml;

namespace SoapCore;

public class ConcurrentXmlNamespaceLookup
{
	private readonly ConcurrentDictionary<string, string> _uriToPrefix = new ();
	private readonly ConcurrentDictionary<string, string> _prefixToUri = new ();

	public IDictionary<string, string> GetNamespacesInScope(XmlNamespaceScope scope) => ToXmlNamespaceManager().GetNamespacesInScope(scope);
	public string LookupPrefix(string uri) => _uriToPrefix.TryGetValue(uri, out var r) ? r : null;
	public string LookupNamespace(string prefix) => _prefixToUri.TryGetValue(prefix, out var r) ? r : null;
	public void AddNamespace(string prefix, string uri)
	{
		_uriToPrefix.AddOrUpdate(uri, _ => prefix, (_, _) => prefix);
		_prefixToUri.AddOrUpdate(prefix, _ => uri, (_, _) => uri);
	}

	public XmlNamespaceManager ToXmlNamespaceManager()
	{
		var r = new XmlNamespaceManager(new NameTable());
		foreach (var e in _prefixToUri)
		{
			r.AddNamespace(e.Key, e.Value);
		}

		return r;
	}
}
