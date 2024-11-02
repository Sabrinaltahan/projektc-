using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UserDepartmentPredictionApp
{
    using Microsoft.ML.Data;

    public class ModelOutput
    {
        [ColumnName("PredictedLabel")]
        public string PredictedDepartment { get; set; }
    }
}
