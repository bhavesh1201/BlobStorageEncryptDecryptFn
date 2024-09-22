using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GlobalSTE.Model
{
    public class SaveAttachmentModel
    {
        public string FileName { get; set; }
        public IFormFile AttachmentFile { get; set; }
    }
}
