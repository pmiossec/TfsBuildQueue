using System;
using System.Configuration;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.Build.Client;

namespace TFSBuildQueue
{
    class Program
    {
        static void Main(string[] args)
        {
            int BuildCount = 0;
            string TFS_URL = args.Length == 1 ? args[0] : ConfigurationManager.AppSettings["TFS_URL"]; ;
            Console.WriteLine("\nTFS Build Queue");
            Console.WriteLine("===============\n");
            Console.WriteLine("Connecting to: " + TFS_URL + " and querying build controllers...");
            TfsTeamProjectCollection tfs = TfsTeamProjectCollectionFactory.GetTeamProjectCollection(new Uri(TFS_URL));
            IBuildServer bs = tfs.GetService<IBuildServer>();
            IQueuedBuildSpec qbSpec = bs.CreateBuildQueueSpec("*", "*");
            IQueuedBuildQueryResult qbResults = bs.QueryQueuedBuilds(qbSpec);


            // Define DataTable for storage and manipulation of currently queued builds.
            DataTable QBTable = new DataTable();
            QBTable.Columns.Add("BuildMachine");
            QBTable.Columns.Add("Project");
            QBTable.Columns.Add("BuildDefinition");
            QBTable.Columns.Add("BuildStatus");
            QBTable.Columns.Add("Priority");
            QBTable.Columns.Add("Date");
            QBTable.Columns.Add("ElapsedTime");
            QBTable.Columns.Add("User");


            // Query TFS For Queued builds and write each build to QBTable
            foreach (IQueuedBuild qb in qbResults.QueuedBuilds)
            {
                if(qb.Build != null)
                    qb.Build.RefreshAllDetails();
                string RequestedBy = qb.RequestedBy.PadRight(18);
                if (qb.RequestedBy != qb.RequestedFor)
                {
                    RequestedBy = String.Concat(qb.RequestedBy, " (for ", qb.RequestedFor, ")").PadRight(18);
                }
                DateTime CurrentTime = DateTime.Now;
                TimeSpan ElapsedTime = CurrentTime.Subtract(qb.QueueTime);
                string ElapsedTimeString = ElapsedTime.ToString();
                String TFSET = ElapsedTimeString;
                String TFS_TEAMPROJECT;
                String BuildAgentStr;
                String BuildMachineStr;
                if (qb.Status.ToString() != "InProgress")
                {
                    TFS_TEAMPROJECT = "-------";
                    BuildAgentStr = "N/A";
                }
                else
                {
                    TFS_TEAMPROJECT = qb.Build.TeamProject;
                    BuildAgentStr = GetBuildAgent(qb.Build);
                }
                BuildMachineStr = qb.BuildController.Name + " (" + BuildAgentStr.ToUpper() + ")";

                QBTable.Rows.Add(
                BuildMachineStr.PadRight(46),
                TFS_TEAMPROJECT.PadRight(17),
                qb.BuildDefinition.Name.PadRight(28),
                qb.Status.ToString().PadRight(11),
                qb.Priority.ToString().PadRight(9),
                qb.QueueTime.ToString().PadRight(23),
                TFSET.PadRight(17),
                RequestedBy.PadRight(19)
                );
                BuildCount++;
            }

            // Sorts QBTable on Build controller then by date
            var qbSorted = QBTable.Select("", "BuildMachine ASC, Date ASC");

            // Writes the headers 
            WriteHeaders();

            foreach (DataRow dataRow in qbSorted)
            {
                WriteReportLine(
                    dataRow[0].ToString(),
                    dataRow[1].ToString(),
                    dataRow[2].ToString(),
                    dataRow[3].ToString(),
                    dataRow[4].ToString(),
                    dataRow[5].ToString(),
                    dataRow[6].ToString(),
                    dataRow[7].ToString());
            }


            Console.WriteLine("\n\nTotal Builds Queued: " + BuildCount + "\n\n");
        }

        static void WriteHeaders()
        {
            Console.WriteLine("\n\n");
            Console.WriteLine("Build Controller (Agent)".PadRight(46) + " " +
                              "Project".PadRight(17) + " " +
                              "Build Definition".PadRight(28) + " " +
                              "Status".PadRight(11) + " " +
                              "Priority".PadRight(9) + " " +
                              "Date & Time Started".PadRight(23) + " " +
                              "Elapsed Time".PadRight(17) + " " +
                              "User".PadRight(19));
            Console.WriteLine("============================================".PadRight(46) + " " +
                              "=======".PadRight(17) + " " +
                              "================".PadRight(28) + " " +
                              "=======".PadRight(11) + " " +
                              "========".PadRight(9) + " " +
                              "=====================".PadRight(23) + " " +
                              "================".PadRight(17) + " " +
                              "============".PadRight(19));
        }

        static void WriteReportLine(string tfsBuildController, string tfsProject, string tfsBuildDefinition, string TFSBuildStatus, string TFSBuildPriority, string TFSBuildDateTime, string ElapsedTime, string TFSBuildUser)
        {
            Console.WriteLine("{0} {1} {2} {3} {4} {5} {6} {7}", tfsBuildController, tfsProject, tfsBuildDefinition, TFSBuildStatus, TFSBuildPriority, TFSBuildDateTime, ElapsedTime, TFSBuildUser);
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
