#load "../Framework/References/_frameworkRef.csx"
using System;
using System.Linq;
using System.Data;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Drawing;
using System.Xml.Linq;
using Diagnostics.DataProviders;
using Diagnostics.ModelsAndUtils;
using Diagnostics.ModelsAndUtils.Attributes;
using Diagnostics.ModelsAndUtils.Models;
using Diagnostics.ModelsAndUtils.Models.ResponseExtensions;
using Diagnostics.ModelsAndUtils.ScriptUtilities;
using Newtonsoft.Json;
private static string Pvcissues(OperationContext<AzureKubernetesService> cxt)
{
    return
       $@"cluster('Armprod').database('ARMProd').ShoeboxEntries
	    | where {Utilities.TimeFilterQuery(cxt.StartTime, cxt.EndTime, "PreciseTimeStamp")}
        | where resourceId contains '{cxt.Resource.SubscriptionId}'  and operationName contains 'MICROSOFT.COMPUTE/DISKS/WRITE'   
        | where  properties contains 'error'       
        | project PreciseTimeStamp,resourceId,resultType,properties
        | limit 1000
        ";

}


[AzureKubernetesServiceFilter]
[Definition(Id = "pvc", Name = "checkpvc", Author = "digeler", Description = "check pvc issues in kubernetes")]
public async static Task<Response> Run(DataProviders dp, OperationContext<AzureKubernetesService> cxt, Response res)
{
    // res.Dataset.Add(new DiagnosticData()
    // {
    //     Table = await dp.Kusto.ExecuteClusterQuery(Pvcissues(cxt)),
    //     RenderingProperties = new Rendering(RenderingType.Table){
    //         Title = "Pvc Issues Encountered", 
    //         Description = "Shows pvc issues"
    //     }
    // });
           
    DataTable pvc = await dp.Kusto.ExecuteClusterQuery(Pvcissues(cxt));
    List<DataSummary> pvcissues = new List<DataSummary>();
    foreach (DataRow dr in pvc.Rows)
    {
        string reason = dr["properties"].ToString();
        string time = dr["PreciseTimeStamp"].ToString();
    if (!string.IsNullOrEmpty(reason))
    {
        DataSummary reasonSummary = new DataSummary(reason,time);
        pvcissues.Add(reasonSummary);
    }

    }
  Dictionary<string, string> insightDetails = new Dictionary<string, string>();
   insightDetails.Add("Description", 
            $@"<markdown>
                 We identified errors within the given time range on the `{cxt.Resource.Name}` issues with pvcs detected
            </markdown>");

            insightDetails.Add("Recommended Action", 
            $@"<markdown>
                please check : https://github.com/kubernetes/cloud-provider-azure/blob/master/docs/azuredisk-issues.md
               </markdown>");            
            insightDetails.Add("Customer Ready Content", 
            $@"<markdown>
                Please review the following pvcs and issues <br/>  
               </markdown>");

           

    res.AddDataSummary(pvcissues,"PVCISSUES");    
    res.AddInsight(InsightStatus.Critical, "Errors with Pvc identified", insightDetails);
    
   
    return res;
}

