using System;
using System.Configuration;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Text.RegularExpressions;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.Build.Client;

namespace TFSBuildQueue
{
    class Program
    {
        static void Main(string[] args)
        {
            int buildCount = 0;
            string tfsUrl = args.Length == 1 ? args[0] : ConfigurationManager.AppSettings["TFS_URL"]; ;
            Console.WriteLine("TFS Build Queue: " + tfsUrl);
            TfsTeamProjectCollection tfs = TfsTeamProjectCollectionFactory.GetTeamProjectCollection(new Uri(tfsUrl));
            IBuildServer bs = tfs.GetService<IBuildServer>();
            IQueuedBuildSpec qbSpec = bs.CreateBuildQueueSpec("*", "*");
            IQueuedBuildQueryResult qbResults = bs.QueryQueuedBuilds(qbSpec);

            // Define DataTable for storage and manipulation of currently queued builds.
            DataTable QBTable = new DataTable();
            QBTable.Columns.Add("Project");
            QBTable.Columns.Add("BuildDefinition");
            //QBTable.Columns.Add("Priority");
            QBTable.Columns.Add("Date");
            QBTable.Columns.Add("ElapsedTime");
            QBTable.Columns.Add("User");
            QBTable.Columns.Add("BuildStatus");
            QBTable.Columns.Add("BuildMachine");

            DateTime currentTime = DateTime.Now;
            // Query TFS For Queued builds and write each build to QBTable
            foreach (IQueuedBuild qb in qbResults.QueuedBuilds)
            {
                if(qb.Build != null)
                    qb.Build.RefreshAllDetails();
                string RequestedBy = qb.RequestedBy;
                if (qb.RequestedBy != qb.RequestedFor)
                {
                    //RequestedBy = String.Concat(qb.RequestedBy, " (for ", qb.RequestedFor, ")");
                    RequestedBy = qb.RequestedFor;
                }
                TimeSpan elapsedTime = currentTime.Subtract(qb.QueueTime);
                elapsedTime = new TimeSpan(elapsedTime.Days, elapsedTime.Hours, elapsedTime.Minutes, elapsedTime.Seconds);
                string elapsedTimeString = elapsedTime.ToString();
                String tfsTeamproject;
                String buildAgentStr;
                if (qb.Status != QueueStatus.InProgress)
                {
                    tfsTeamproject = "-------";
                    buildAgentStr = "N/A";
                }
                else
                {
                    tfsTeamproject = qb.Build.TeamProject;
                    buildAgentStr = GetBuildAgent(qb.Build);
                }
                var buildMachineStr = qb.BuildController.Name + " (" + buildAgentStr + ")";

                QBTable.Rows.Add(
                    tfsTeamproject,
                    qb.BuildDefinition.Name,
                    //qb.Priority.ToString(),
                    qb.QueueTime.Date == currentTime.Date ? qb.QueueTime.ToLongTimeString() : qb.QueueTime.ToString(),
                    elapsedTimeString,
                    RequestedBy,
                    qb.Status.ToString(),
                    buildMachineStr
                );
                buildCount++;
            }

            // Sorts QBTable on Build controller then by date
            var qbSorted = QBTable.Select("", "BuildMachine ASC, Date ASC");

            var datas = ConvertToString(qbSorted);

            if (buildCount != 0)
            {
                // Writes the headers
                List<int> wishedColSize = new List<int>();
                for (int i = 0; i < _columns.Count(); i++)
                {
                    wishedColSize.Add(datas.Select(e => e[i].Length).Max());

                }
                WriteData(datas, wishedColSize);
            }

            Console.WriteLine("\nTotal Builds Queued: " + buildCount);
        }

        private static IEnumerable<List<string>> ConvertToString(DataRow[] qbSorted)
        {
            return qbSorted.Select(dataRow => new List<string>
            {
                dataRow[0].ToString(),
                dataRow[1].ToString(),
                dataRow[2].ToString(),
                dataRow[3].ToString(),
                dataRow[4].ToString(),
                dataRow[5].ToString(),
                dataRow[6].ToString(),
                //dataRow[7].ToString(),
            });
        }

        static int WriteColumHeader(string text, int wishedSize)
        {
            var colSize = Math.Max(wishedSize, text.Length);
            Console.Write(text.PadRight(colSize) + " ");
            return colSize;
        }

        static List<string> _columns = new List<string>
            {
                "Project",
                "Build Definition",
                //"Priority",
                "Start",
                "Duration",
                "User",
                "Status",
                "Build Controller (Agent)"
            };

        static void WriteData(IEnumerable<List<string>> datas, List<int> wishedColSize)
        {
            Console.WriteLine(string.Empty);
            var colsSize = new List<int>();
            int i = 0;
            foreach (var col in _columns)
            {
                colsSize.Add(WriteColumHeader(col, wishedColSize[i]));
                i++;
            }
            Console.WriteLine(string.Empty);
            foreach (var colSize in colsSize)
            {
                WriteColumHeader(string.Empty.PadRight(colSize, '='), colSize);
            }
            Console.WriteLine(string.Empty);
            foreach (var row in datas)
            {
                for (int iCol = 0; iCol < _columns.Count(); iCol++)
                {
                    var size = colsSize[iCol];
                    WriteColumHeader(row[iCol].PadRight(size), size);
                }
                Console.WriteLine(string.Empty);
            }
        }

        public static string GetBuildAgent(IBuildDetail build)  //IQueuedBuild.Build
        {
            foreach (var child in build.Information.Nodes)
            {
                string agentName = ShowChild(child, 1);
                if (!string.IsNullOrEmpty(agentName))
                {
                    return agentName;
                }
            }
            return string.Empty;
        }

        static string ShowChild(IBuildInformationNode node, int level)
        {
            string levelStr = new string(' ', level * 4);
            foreach (var field in node.Fields)
            {
                if (field.Key == "ReservedAgentName")
                {
                    return field.Value;
                }
            }

            foreach (var child in node.Children.Nodes)
            {
                string agentName = ShowChild(child, level + 1);
                if (!string.IsNullOrEmpty(agentName))
                {
                    return agentName;
                }
            }
            return string.Empty;

        }

    }

}
