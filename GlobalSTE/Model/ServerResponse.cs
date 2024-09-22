using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GlobalSTE.Model
{
    public class ServerResponse<T>
    {
    
        public bool Success { get; set; }

       
        public ErrorModel Error { get; set; }

       
        public T Payload { get; set; }
    }
}
