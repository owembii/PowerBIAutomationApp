using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PowerBIAutomationApp.DTO
{
    public class UpdateParametersDTO
    {
        public required string Name { get; set; }
        public required object NewValue { get; set; }
    }
}
