using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PBIFunctionApp.DTO
{
    internal class CloneReportDTO
    {
        public string? name { get; set; }
        public string? targetWorkspaceId { get; set; }
        public string? targetModelId { get; set; }
    }
}
