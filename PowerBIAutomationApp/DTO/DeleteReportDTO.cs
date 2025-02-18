using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PBIFunctionApp.DTOs
{
    internal class DeleteReportDTO
    {
        public required string WorkspaceId { get; set; }
        public required string ReportName { get; set; }
    }
}
