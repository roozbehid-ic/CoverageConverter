using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Xsl;
using Microsoft.VisualStudio.Coverage.Analysis;


namespace CoverageDiff
{
    class Program
    {
        static void Main(string[] args)
        {

            if ((args.Length > 4) || (args.Length < 3))
            {
                Console.WriteLine("CoverageTool [action] file1 file2 [options]");
                Console.WriteLine("actions:");
                Console.WriteLine("             diff     compare coverage files");
                Console.WriteLine("             merge    merge coverage files");
                Console.WriteLine("options:");
                Console.WriteLine("-ignoremodulenamecheck : by default module names should match to considered for diff checking.");
                return;
            }
            bool ignoremodulenamecheck = false;
            if ( (args.Length==4) && (args[3] == "-ignoremodulenamecheck") )
                ignoremodulenamecheck = true;

            if (args[0] == "merge")
            {
                CoverageDS dataSet_1 = new CoverageDS();
                dataSet_1.ImportXml(args[1]);

                CoverageDS dataSet_2 = new CoverageDS();
                dataSet_2.ImportXml(args[2]);

                CoverageDS dataSet_3 = CoverageDS.Join(dataSet_1, dataSet_2);
                string fname = "merge_" + Path.GetFileName(args[1]) + "_" + Path.GetFileName(args[2]) + ".xml";
                dataSet_3.ExportXml(fname);

                Console.WriteLine($"Merge of two coverage files created as {fname}");

                return;

            }

            CoverageDS dataSet1 = new CoverageDS();
            dataSet1.ImportXml(args[1]);

            CoverageDS dataSet2 = new CoverageDS();
            dataSet2.ImportXml(args[2]);

            List<string> validModules = new List<string>();

            foreach (CoverageDSPriv.ModuleRow module in dataSet1.Module)
            {
                DataRow[] rows_module = dataSet2.Module.Select($"ModuleName = '{module.ModuleName.Replace("'", "''")}'");
                if (rows_module.Length > 0)
                    validModules.Add(module.ModuleName);
            }

            Console.WriteLine("Following modules are only checked : ");
            foreach (var module in validModules)
            {
                Console.WriteLine(module);
            }

            Console.WriteLine("\n\n");

            bool printMissing = false;
            string printMissingMessage = $"Following Modules from {args[1]} are not compared :\n";
            foreach (CoverageDSPriv.ModuleRow module in dataSet1.Module)
            {
                if (!validModules.Contains(module.ModuleName))
                {
                    printMissingMessage = printMissingMessage + module.ModuleName + "\n";
                    printMissing = true;
                }
            }
            if (printMissing)
                Console.WriteLine(printMissingMessage+ "\n\n");

            printMissingMessage = $"Following Modules from {args[2]} are not compared :\n";
            foreach (CoverageDSPriv.ModuleRow module in dataSet2.Module)
            {
                if (!validModules.Contains(module.ModuleName))
                {
                    printMissingMessage = printMissingMessage + module.ModuleName + "\n";
                    printMissing = true;
                }
            }
            if (printMissing)
                Console.WriteLine(printMissingMessage + "\n\n");




            foreach (CoverageDSPriv.MethodRow method in dataSet1.Method)
            {
                if (method.MethodName.StartsWith("."))
                    continue;

                String title = ":" + method.MethodName;
                DataRow[] rows = dataSet2.Method.Select($"MethodName = '{method.MethodName.Replace("'", "''")}'");

                try
                {
                    DataRow[] rows_class = dataSet1.Class.Select($"ClassKeyName = '{method.ClassKeyName.Replace("'", "''")}'");
                    DataRow[] rows_namespaces = dataSet1.NamespaceTable.Select($"NamespaceKeyName = '{((CoverageDSPriv.ClassRow)rows_class[0]).NamespaceKeyName.Replace("'", "''")}'");
                    if ((!ignoremodulenamecheck) && (!validModules.Contains(((CoverageDSPriv.NamespaceTableRow)rows_namespaces[0]).ModuleName) ))
                        continue;

                    DataRow[] rows_lines = dataSet1.Lines.Select($"MethodKeyName = '{method.MethodKeyName.Replace("'", "''")}'");
                    DataRow[] rows_sources = dataSet1.SourceFileNames.Select($"SourceFileID = '{((CoverageDSPriv.LinesRow)rows_lines[0]).SourceFileID}'");
                    title = Path.GetFileName(((CoverageDSPriv.SourceFileNamesRow)rows_sources[0]).SourceFileName) + ":" + method.MethodName;
                }
                catch {

                }

                
                if (rows.Length > 1)
                {
                    //Console.WriteLine(method.MethodName);
                }
                else if (rows.Length == 0)
                {
                    Console.WriteLine($"=======1======== {title} =========2====");
                    Console.WriteLine($"Only here | Not used here");

                }
                else //if = 1
                {
                    CoverageDSPriv.MethodRow row = (CoverageDSPriv.MethodRow)rows[0];
                    if ((row.LinesCovered != method.LinesCovered) || (row.LinesNotCovered != method.LinesNotCovered) || (row.LinesPartiallyCovered != method.LinesPartiallyCovered))
                    {
                        Console.WriteLine($"=======1======== {title} =========2====");
                        Console.WriteLine($"Lines covered : {method.LinesCovered}  |  {row.LinesCovered}");
                        Console.WriteLine($"Lines not covered : {method.LinesNotCovered}  |  {row.LinesNotCovered}");
                        Console.WriteLine($"Lines partially covered : {method.LinesPartiallyCovered}  |  {row.LinesPartiallyCovered}");
                        Console.WriteLine($"Blocks covered : {method.BlocksCovered}  |  {row.BlocksCovered}");
                        Console.WriteLine($"Blocks not covered : {method.BlocksNotCovered}  |  {row.BlocksNotCovered}");

                        DataRow[] dataset1_lines = dataSet1.Lines.Select($"MethodKeyName = '{method.MethodKeyName.Replace("'", "''")}' and Coverage > 0","LnStart ASC");
                        DataRow[] dataset2_lines = dataSet2.Lines.Select($"MethodKeyName = '{row.MethodKeyName.Replace("'", "''")}' and Coverage > 0", "LnStart ASC");

                        string dataset1_lines_string="", dataset2_lines_string="";
                        uint prevCounter1 = 0;
                        uint prevCounter2 = 0;

                        foreach (var roww in dataset1_lines)
                        {
                            CoverageDSPriv.LinesRow covRow = (CoverageDSPriv.LinesRow)roww;

                            if (covRow.LnStart - 1 == prevCounter1)
                            {
                                prevCounter1 = covRow.LnEnd;
                                if (!dataset1_lines_string.EndsWith("-"))
                                    dataset1_lines_string = dataset1_lines_string + "-";
                                continue;
                            }
                            else
                            {
                                prevCounter1 = covRow.LnEnd;
                                if (!dataset1_lines_string.EndsWith("-") && dataset1_lines_string.Length > 0)
                                    dataset1_lines_string = dataset1_lines_string + ",";
                                dataset1_lines_string = dataset1_lines_string + covRow.LnStart;
                            }

                        }

                        foreach (var roww in dataset2_lines)
                        {
                            CoverageDSPriv.LinesRow covRow = (CoverageDSPriv.LinesRow)roww;

                            if (covRow.LnStart - 1 == prevCounter2)
                            {
                                prevCounter2 = covRow.LnEnd;
                                if (!dataset2_lines_string.EndsWith("-"))
                                    dataset2_lines_string = dataset2_lines_string + "-";
                                continue;
                            }
                            else
                            {
                                prevCounter2 = covRow.LnEnd;
                                if (!dataset2_lines_string.EndsWith("-") && dataset2_lines_string.Length > 0)
                                    dataset2_lines_string = dataset2_lines_string + ",";
                                dataset2_lines_string = dataset2_lines_string + covRow.LnStart;
                            }

                        }



                        Console.WriteLine($"Lines difference : {dataset1_lines_string}  vs  {dataset2_lines_string}");
                        Console.WriteLine();
                    }

                }
                
            }


            //now only check for things not in 1
            foreach (CoverageDSPriv.MethodRow method in dataSet2.Method)
            {
                if (method.MethodName.StartsWith("."))
                    continue;

                DataRow[] rows = dataSet2.Method.Select($"MethodName = '{method.MethodName.Replace("'", "''")}'");
                String title = ":" + method.MethodName;
                try
                {
                    DataRow[] rows_class = dataSet2.Class.Select($"ClassKeyName = '{method.ClassKeyName.Replace("'", "''")}'");
                    DataRow[] rows_namespaces = dataSet2.NamespaceTable.Select($"NamespaceKeyName = '{((CoverageDSPriv.ClassRow)rows_class[0]).NamespaceKeyName.Replace("'", "''")}'");

                    if ((!ignoremodulenamecheck) && (!validModules.Contains(((CoverageDSPriv.NamespaceTableRow)rows_namespaces[0]).ModuleName)))
                        continue;


                    DataRow[] rows_lines = dataSet2.Lines.Select($"MethodKeyName = '{method.MethodKeyName.Replace("'", "''")}'");
                    DataRow[] rows_sources = dataSet2.SourceFileNames.Select($"SourceFileID = '{((CoverageDSPriv.LinesRow)rows_lines[0]).SourceFileID}'");
                    title = Path.GetFileName(((CoverageDSPriv.SourceFileNamesRow)rows_sources[0]).SourceFileName) + ":" + method.MethodName;
                }
                catch
                {

                }

                


                if (rows.Length > 1)
                {
                    //Console.WriteLine(method.MethodName);
                }
                else if (rows.Length == 0)
                {
                    Console.WriteLine($"=======1======== {title} =========2====");
                    Console.WriteLine($"Not used here | Only here");

                }
            }



        }
    }
}
