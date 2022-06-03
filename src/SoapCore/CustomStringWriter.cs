using System.IO;
using System.Text;

namespace SoapCore
{    
    public class CustomStringWriter : StringWriter
    {
        private readonly Encoding _encoding;

		public CustomStringWriter(Encoding encoding)
		{
			_encoding = encoding;
		}

        public override Encoding Encoding
        {
            get
            {
                return _encoding;
            }
        }        
    }
}