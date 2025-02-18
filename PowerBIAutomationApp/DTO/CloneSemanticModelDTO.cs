using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PBIFunctionApp.DTO
{
    internal class CloneSemanticModelDTO
    {
        public required string modelName { get; set; }
        public required string targetWorkspaceId { get; set; }
    }
}
