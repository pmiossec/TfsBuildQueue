using System;
using System.Configuration;
using System.Collections.Generic;
using System.Linq;
using System.Data;
using System.Threading;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.Build.Client;

namespace TFSBuildQueue
{
    class Program
    {
        static void Main(string[] args)
        {
            string tfsUrl  = null;
            bool loop = false;
            if (args.Length > 0)
            {
                if (args.Length > 2)
                {
                    DisplayHelp();
                }
                loop = args[args.Length - 1] == "--loop";
                if (args[0].StartsWith("http"))
                    tfsUrl = args[0];
                
            }
            if(tfsUrl == null)
                tfsUrl = ConfigurationManager.AppSettings["TFS_URL"];
            if (tfsUrl == null)
                DisplayHelp();
            Console.WriteLine("TFS Build Queue: " + tfsUrl);
            TfsTeamProjectCollection tfs = TfsTeamProjectCollectionFactory.GetTeamProjectCollection(new Uri(tfsUrl));
            IBuildServer bs = tfs.GetService<IBuildServer>();
            IQueuedBuildSpec qbSpec = bs.CreateBuildQueueSpec("*", "*");

            var previousBuildCount = -1;
            var cursorPosition = Console.CursorTop;
            var foundBuilds = false;
            do
            {
                if (loop && foundBuilds)
                {
                    Console.SetCursorPosition(0, cursorPosition);
                    ClearScreen(previousBuildCount+2);
                    Console.SetCursorPosition(0, cursorPosition);
                }
                var buildsCount = DisplayBuilds(bs, qbSpec);

                if (loop)
                {
                    if (buildsCount == 0 && previousBuildCount == buildsCount)
                    {
                        Console.Write(".");
                        Thread.Sleep(30000);
                    }
                    else
                    {
                        Console.WriteLine("\nTotal Builds Queued: " + buildsCount);

                        DisplayFinishedBuilds(bs);

                        Thread.Sleep(5000);
                    }
                    previousBuildCount = Console.CursorTop - cursorPosition;
                    foundBuilds = (buildsCount != 0);

                }
                else
                {
                    Console.WriteLine("\nTotal Builds Queued: " + buildsCount);
                }
            } while (loop);
        }

        private static void ClearScreen(int nbLines)
        {
            var emptyLine = new string(' ', LineSize);
            for (int i = 0; i < nbLines; i++)
            {
                Console.WriteLine(emptyLine);
            }
        }

        private static void DisplayFinishedBuilds(IBuildServer bs)
        {
            var previousColor = Console.ForegroundColor;
            try
            {
                var buildSpec = bs.CreateBuildDetailSpec("*", "*");
                buildSpec.InformationTypes = null;
                buildSpec.MinFinishTime = DateTime.Now.AddMinutes(-5);
                var buildDetails = bs.QueryBuilds(buildSpec).Builds.ToList();
                if (buildDetails.Any())
                {
                    Console.WriteLine("Build just finished:");
                }
                foreach (var buildFinished in buildDetails)
                {
                    switch (buildFinished.Status)
                    {
                        case BuildStatus.Failed:
                            Console.ForegroundColor = ConsoleColor.Red;
                            break;
                        case BuildStatus.Stopped:
                        case BuildStatus.PartiallySucceeded:
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            break;
                        case BuildStatus.Succeeded:
                            Console.ForegroundColor = ConsoleColor.Green;
                            break;
                    }
                    Console.WriteLine(buildFinished.TeamProject + " " + buildFinished.BuildDefinition.Name + " => " +
                                      buildFinished.Status);
                }
            }
            catch (Exception)
            {
                Console.WriteLine("Server not reachable :(");
            }
            Console.ForegroundColor = previousColor;

        }

        private static void DisplayHelp()
        {
            Console.WriteLine("usage: TFSBuildQueue.exe [http://tfs.server.url/tfs] [--loop]");
            Environment.Exit(1);
        }

        private static int DisplayBuilds(IBuildServer bs, IQueuedBuildSpec qbSpec)
        {
            try
            {
                int buildCount = 0;
                IQueuedBuildQueryResult qbResults = bs.QueryQueuedBuilds(qbSpec);

                // Define DataTable for storage and manipulation of currently queued builds.
                DataTable queuedBuildTable = new DataTable();
                queuedBuildTable.Columns.Add("Project");
                queuedBuildTable.Columns.Add("BuildDefinition");
                //QBTable.Columns.Add("Priority");
                queuedBuildTable.Columns.Add("Date");
                queuedBuildTable.Columns.Add("ElapsedTime");
                queuedBuildTable.Columns.Add("User");
                queuedBuildTable.Columns.Add("BuildStatus");
                queuedBuildTable.Columns.Add("BuildMachine");

                DateTime currentTime = DateTime.Now;
                // Query TFS For Queued builds and write each build to QBTable
                foreach (var qb in qbResults.QueuedBuilds)
                {
                    if (qb.Build != null)
                        qb.Build.RefreshAllDetails();
                    string requestedBy = qb.RequestedBy;
                    if (qb.RequestedBy != qb.RequestedFor)
                    {
                        //RequestedBy = String.Concat(qb.RequestedBy, " (for ", qb.RequestedFor, ")");
                        requestedBy = qb.RequestedFor;
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

                    queuedBuildTable.Rows.Add(
                        tfsTeamproject,
                        qb.BuildDefinition.Name,
                        //qb.Priority.ToString(),
                        qb.QueueTime.Date == currentTime.Date ? qb.QueueTime.ToLongTimeString() : qb.QueueTime.ToString(),
                        elapsedTimeString,
                        requestedBy,
                        qb.Status.ToString(),
                        buildMachineStr
                        );
                    buildCount++;
                }

                // Sorts QBTable on Build controller then by date
                var qbSorted = queuedBuildTable.Select("", "BuildMachine ASC, Date ASC");

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
                return buildCount;
            }
            catch (Exception)
            {
                Console.WriteLine("Server not reachable :(");
                return 0;
            }
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

        private static int LineSize = 0;

        static void WriteData(IEnumerable<List<string>> datas, List<int> wishedColSize)
        {
            ClearScreen(1);
            var colsSize = new List<int>();
            int i = 0;
            foreach (var col in _columns)
            {
                colsSize.Add(WriteColumHeader(col, wishedColSize[i]));
                i++;
            }
            ClearScreen(1);
            foreach (var colSize in colsSize)
            {
                WriteColumHeader(string.Empty.PadRight(colSize, '='), colSize);
            }
            ClearScreen(1);
            foreach (var row in datas)
            {
                for (int iCol = 0; iCol < _columns.Count(); iCol++)
                {
                    var size = colsSize[iCol];
                    WriteColumHeader(row[iCol].PadRight(size), size);
                }
                ClearScreen(1);
            }
            LineSize = colsSize.Aggregate((x, y) => x + y);
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
