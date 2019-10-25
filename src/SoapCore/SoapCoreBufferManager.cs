using System.Buffers;
using System.ServiceModel.Channels;

namespace SoapCore
{
	internal class SoapCoreBufferManager : BufferManager
	{
		public override void Clear()
		{
		}

		public override void ReturnBuffer(byte[] buffer)
		{
			ArrayPool<byte>.Shared.Return(buffer);
		}

		public override byte[] TakeBuffer(int bufferSize)
		{
			return ArrayPool<byte>.Shared.Rent(bufferSize);
		}
	}
}
