using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PBIFunctionApp.DTOs
{
    public class SemanticModelListResponse
    {
        public List<SemanticModel> Value { get; set; } = new();
    }

    public class SemanticModel
    {
        public string Id { get; set; }
    }
}
