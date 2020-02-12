using System.ServiceModel.Channels;

namespace SoapCore.Tests.Utilities
{
    public class CustomTextMessageEncoderFactory : MessageEncoderFactory
    {
        private readonly System.ServiceModel.Channels.MessageEncoder _encoder;
        private readonly MessageVersion _version;
        private readonly string _mediaType;
        private readonly string _charSet;

        internal CustomTextMessageEncoderFactory(string mediaType, string charSet,  MessageVersion version)
        {
            _version = version;
            _mediaType = mediaType;
            _charSet = charSet;
            _encoder = new CustomTextMessageEncoder(this);
        }

        public override System.ServiceModel.Channels.MessageEncoder Encoder
        {
            get
            {
                return _encoder;
            }
        }

        public override MessageVersion MessageVersion
        {
            get
            {
                return _version;
            }
        }

        internal string MediaType
        {
            get
            {
                return _mediaType;
            }
        }

        internal string CharSet
        {
            get
            {
                return _charSet;
            }
        }
    }
}
