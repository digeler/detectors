#load "../Framework/References/_frameworkRef.csx"
using System;
using System.Linq;
using System.Data;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Diagnostics.DataProviders;
using Diagnostics.ModelsAndUtils;
using Diagnostics.ModelsAndUtils.Attributes;
using Diagnostics.ModelsAndUtils.Models;
using Diagnostics.ModelsAndUtils.Models.ResponseExtensions;
using Diagnostics.ModelsAndUtils.ScriptUtilities;
using Newtonsoft.Json;
private static string GetQuery(OperationContext<AzureKubernetesService> cxt)
{

    //quota issues
    //change the timestamp before publish
    return
      $@"union cluster('Aks').database('AKSprod').FrontEndContextActivity
    | where {Utilities.TimeFilterQuery(cxt.StartTime, cxt.EndTime, "PreciseTimeStamp")}
    | where resourceName contains '{cxt.Resource.Name}' 
    | where msg contains 'quota'
    | where level contains 'error'
    | project TIMESTAMP,msg,resourceName
    | limit 30
    ";
}


private static string Getvesion(OperationContext<AzureKubernetesService> cxt)
{
    //change the timestamp before publish

    //cluster version
    return
      $@"union cluster('Aks').database('AKSprod').AsyncContextActivity
    | where {Utilities.TimeFilterQuery(cxt.StartTime, cxt.EndTime, "PreciseTimeStamp")}
    | where resourceName contains '{cxt.Resource.Name}' 
    | where msg contains 'Unversioned ManagedCluster body'    
    | project TIMESTAMP,msg,resourceName
    | limit 10
    ";
}


private static string Clusterdep(OperationContext<AzureKubernetesService> cxt)
{
    //change the timestamp before publish

    //cluster deployment
    return
      $@"union cluster('Aks').database('AKSprod').FrontEndContextActivity
    | where {Utilities.TimeFilterQuery(cxt.StartTime, cxt.EndTime, "PreciseTimeStamp")}
    | where subscriptionID contains '{cxt.Resource.SubscriptionId}' 
    | where level contains 'error'    
    | project TIMESTAMP,msg,targetURI
    | limit 40
    ";
}



private static string Shoebox(OperationContext<AzureKubernetesService> cxt)
{
    //change the timestamp before publish

    //cluster shoebox
    return
      $@"cluster('Armprod').database('ARMProd').ShoeboxEntries
    | where {Utilities.TimeFilterQuery(cxt.StartTime, cxt.EndTime, "PreciseTimeStamp")}
    | where  resourceId contains '{cxt.Resource.SubscriptionId}'   
    | where resultType contains 'Failure'
    | where level contains 'error'    
    | project TIMESTAMP,properties,operationName
    | limit 40   
    ";
}

private static string GetUnderlaySummary(OperationContext<AzureKubernetesService> cxt)
{
        return
        $@"cluster('Aks').database('AKSprod').BlackboxMonitoringActivity
            | where {Utilities.TimeFilterQuery(cxt.StartTime, cxt.EndTime, "PreciseTimeStamp")}
            | where subscriptionID =~ '{cxt.Resource.SubscriptionId}'
            | where resourceGroupName =~ '{cxt.Resource.ResourceGroup}'
            | where clusterName =~ '{cxt.Resource.Name}'
            | where level contains 'error'
            | project TIMESTAMP,fqdn,msg
            | limit 100
            ";
}



[AzureKubernetesServiceFilter]
[Definition(Id = "Cluster Issues", Category= "Summary", Name = "Cluster All issues", Author = "Digeler", Description = "Multiple Tables To Troubelshoot Issues With The Cluster")]
public async static Task<Response> Run(DataProviders dp, OperationContext<AzureKubernetesService> cxt, Response res)
{
    
            DataTable quota = await dp.Kusto.ExecuteClusterQuery(GetQuery(cxt));
            
            List<DataSummary> quotalist = new List<DataSummary>();
              
          

    foreach (DataRow dr in quota.Rows)
    {
        string reason = dr["TIMESTAMP"].ToString();
        string msg = dr["msg"].ToString();
        //string level = dr["level"].ToString();
        if (!string.IsNullOrEmpty(msg))
        {
            DataSummary reasonSummary = new DataSummary(reason, msg);
           quotalist.Add(reasonSummary);
        }
       
    }
     Dictionary<string, string> insightDetails = new Dictionary<string, string>();
   insightDetails.Add("Description", 
            $@"<markdown>
                 We identified errors within the given time range on the `{cxt.Resource.Name}` issues with quota detected
            </markdown>");

            insightDetails.Add("Recommended Action", 
            $@"<markdown>
                please check : https://docs.microsoft.com/en-us/azure/azure-resource-manager/resource-manager-quota-errors
               </markdown>");             
           
           if (quotalist.Capacity>0){
         res.AddDataSummary(quotalist, "Quota Issues");
         res.AddInsight(InsightStatus.Critical, "Errors with quota found", insightDetails);
           }
           else{
                res.AddDataSummary(quotalist, "No Issues Detected");
         res.AddInsight(InsightStatus.Info, "No Errors with quota found", insightDetails);
           }

 DataTable vers = await dp.Kusto.ExecuteClusterQuery(Getvesion(cxt));
            List<DataSummary> version = new List<DataSummary>();
   foreach (DataRow dr in vers.Rows)
    {
        string reason = dr["TIMESTAMP"].ToString();
        string msg = dr["msg"].ToString();
        //string level = dr["level"].ToString();
        if (!string.IsNullOrEmpty(reason))
        {
            DataSummary reasonSummary = new DataSummary(reason, msg);
           version.Add(reasonSummary);
        }
    }
    Dictionary<string, string> insightDetails1 = new Dictionary<string, string>();
   insightDetails1.Add("Description", 
            $@"<markdown>
                 verify if cluster version  `{cxt.Resource.Name}` is supported
            </markdown>");

            insightDetails1.Add("Recommended Action", 
            $@"<markdown>
                please check : https://docs.microsoft.com/en-us/azure/aks/supported-kubernetes-versions
               </markdown>");             
           
           if(version.Capacity>0){
         res.AddDataSummary(version, "Cluster Version");
         res.AddInsight(InsightStatus.Info, "cluster version insights", insightDetails1);
           }
           else{
               res.AddDataSummary(version, "No Version Content Found");
         res.AddInsight(InsightStatus.Info, "No Cluster VersionInsights Found", insightDetails1);
           }

        DataTable clusterdep = await dp.Kusto.ExecuteClusterQuery(Clusterdep(cxt));
            List<DataSummary> dep = new List<DataSummary>();
   foreach (DataRow dr in clusterdep.Rows)
    {
        string reason = dr["TIMESTAMP"].ToString();
        string msg = dr["msg"].ToString();
        //string level = dr["level"].ToString();
        if (!string.IsNullOrEmpty(reason))
        {
            DataSummary reasonSummary = new DataSummary(reason, msg);
           dep.Add(reasonSummary);
        }
    }

     Dictionary<string, string> insightDetails2 = new Dictionary<string, string>();
   insightDetails2.Add("Description", 
            $@"<markdown>
                 verify deployment of   `{cxt.Resource.Name}` for errors
            </markdown>");

            insightDetails2.Add("Recommended Action", 
            $@"<markdown>
                please check : the wiki for known issues
               </markdown>");             
           
           if(dep.Capacity>0){
         res.AddDataSummary(dep, "Cluster Deployment Issues");   
         res.AddInsight(InsightStatus.Info, "Cluster Deployment Insights", insightDetails2);
           }
           else{
                res.AddDataSummary(dep, "Cluster Deployment Issues");   
         res.AddInsight(InsightStatus.Info, "No Cluster Deployment Have Been Found", insightDetails2);
           }
              
          

 DataTable shoe = await dp.Kusto.ExecuteClusterQuery(Shoebox(cxt));
            List<DataSummary> sbox = new List<DataSummary>();
            
                   
   foreach (DataRow dr in shoe.Rows)
    {
        string reason = dr["TIMESTAMP"].ToString();
        string msg = dr["properties"].ToString();
        //string level = dr["level"].ToString();
        if (!string.IsNullOrEmpty(reason))
        {
            DataSummary reasonSummary = new DataSummary(reason, msg);
            
          sbox.Add(reasonSummary);
        }
    }
         res.AddDataSummary(sbox, "ShoeboxEntries");   
        
         res.Dataset.Add(new DiagnosticData()
    {
        Table = await dp.Kusto.ExecuteClusterQuery(GetUnderlaySummary(cxt)),
        RenderingProperties = new Rendering(RenderingType.Table){
            Title = "Cluster Underlay errors",
            Description = "Shows errors about underlay "
        }
    });

    

    return res;
}

