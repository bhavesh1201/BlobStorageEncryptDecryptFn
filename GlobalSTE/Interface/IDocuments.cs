using GlobalSTE.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GlobalSTE.Interface
{
    public interface IDocuments
    {
        Task<ServerResponse<AttachmentDetailsModel>> UploadToBlob(SaveAttachmentModel model);
    

    }
}
