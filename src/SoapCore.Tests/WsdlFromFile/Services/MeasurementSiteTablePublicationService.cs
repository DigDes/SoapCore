using System;
using System.Threading.Tasks;

namespace SoapCore.Tests.WsdlFromFile.Services
{
    /// <summary>
    /// Pull service for snapshopPull interface(MeasurementSiteTablePublicationService)
    /// </summary>
    public class MeasurementSiteTablePublicationService : snapshotPullInterface
    {
        public MeasurementSiteTablePublicationService()
        {
        }

        public pullSnapshotDataResponse pullSnapshotData(pullSnapshotDataRequest request)
        {
            var response = new pullSnapshotDataResponse();
            try
            {
               //todo
            }
            catch (System.Exception)
            {
				throw;
			}

            return response;
        }

        public Task<pullSnapshotDataResponse> pullSnapshotDataAsync(pullSnapshotDataRequest request)
		{
			throw new NotImplementedException();
		}
	}
}
