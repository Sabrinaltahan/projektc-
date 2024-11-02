using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.ML.Data;

namespace UserDepartmentPredictionApp
{
    public class ModelInput
    {
        [LoadColumn(0)]
        public string Description { get; set; }

        [LoadColumn(1)]
        public string Department { get; set; }
    }
}
