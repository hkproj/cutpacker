using System;
using Newtonsoft.Json;
using System.Linq;
using System.Collections.Generic;
using Google.OrTools.Algorithms;
using Google.OrTools.LinearSolver;
using System.IO;
using Newtonsoft.Json.Linq;

namespace CutPacker
{

    public class DataItem
    {

        public DataItem(string name, double weight)
        {
            Name = name;
            Weight = weight;
        }

        public string Name { get; set; }

        public double Weight { get; set; }

    }

    public class DataModel
    {

        public double BinCapacity { get; private set; }

        public IList<DataItem> Items { get; set; }

        public int MaxNumberOfBins
        {
            get
            {
                return Items.Count;
            }
        }

        public void AddItem(string name, int quantity, double weight)
        {
            for (var i = 1; i <= quantity; i++)
            {
                this.Items.Add(new DataItem($"{name}_{(i)}", weight));
            }
        }

        public DataModel(double bin_capacity)
        {
            BinCapacity = bin_capacity;
            this.Items = new List<DataItem>();
        }
    }


    class Program
    {


        static DataModel BuildModel()
        {
            var dataModel = new DataModel(6000);
            dataModel.AddItem("1S", /*32*/1, 6000);
            dataModel.AddItem("2S", 7, 3100);
            dataModel.AddItem("3S", /*7*/1, 3500);
            dataModel.AddItem("4S", /*4*/1, 1900);
            dataModel.AddItem("5S", /*7*/1, 5200);
            dataModel.AddItem("6S", 7, 900);
            dataModel.AddItem("7S", 3, 1700);
            dataModel.AddItem("8S", 7, 3200);
            dataModel.AddItem("9S", 7, 3200);
            dataModel.AddItem("10S", 7, 1700);
            dataModel.AddItem("11S", 7, 4200);
            dataModel.AddItem("12S", 6, 3300);

            return dataModel;
        }

        static DataModel ReadModelFromFile(string configuration_file, string data_file)
        {
            var data_content = File.ReadAllLines(data_file);
            var configuration_content = File.ReadAllText(configuration_file);

            try
            {
                dynamic configuration = JToken.Parse(configuration_content);
                double bin_capacity = configuration.bin_capacity;

                var model = new DataModel(bin_capacity);

                foreach(var line in data_content) {
                    if (String.IsNullOrEmpty(line.Trim())) {
                        continue;
                    }

                    var fields = line.Split(';', StringSplitOptions.RemoveEmptyEntries);
                    var name = fields[0];
                    var quantity = int.Parse(fields[1]);
                    var weight = double.Parse(fields[2]);

                    model.AddItem(name, quantity, weight);
                }

                return model;
            }
            catch (Exception)
            {
                return null;
            }
        }

        static void SolveAndPrint(DataModel data)
        {
            // Create the linear solver with the CBC backend.
            Solver solver = Solver.CreateSolver("BinPackingSolver", "CBC_MIXED_INTEGER_PROGRAMMING");

            // Variable x[i,j] indicates if the item i is packed into bin j.
            var x = new Variable[data.Items.Count, data.MaxNumberOfBins];
            for (var i = 0; i < data.Items.Count; i++)
            {
                for (var j = 0; j < data.MaxNumberOfBins; j++)
                {
                    x[i, j] = solver.MakeIntVar(0, 1, $"x_{i}_{j}");
                }
            }

            // Variable y[j] indicates if the bin j is being used
            var y = new Variable[data.MaxNumberOfBins];
            for (var j = 0; j < data.MaxNumberOfBins; j++)
            {
                y[j] = solver.MakeIntVar(0, 1, $"y_{j}");
            }

            // This constraint indicates that each item can only be present in one of the bins
            for (int i = 0; i < data.Items.Count; ++i)
            {
                var sum = new LinearExpr();
                for (int j = 0; j < data.MaxNumberOfBins; ++j)
                {
                    sum += x[i, j];
                }

                solver.Add(sum == 1.0);
            }

            // This constraint indicates that the sum of the items contained in each bin cannot exceed its capacity. That is:
            // SUM_i(weight[i] * x[i,j]) <= Capacity * y[j]
            // Which can be rewritten as:
            // 0 <= Capacity * y[j] - SUM_i(weight[i] * x[i,j])
            for (int j = 0; j < data.MaxNumberOfBins; j++)
            {
                var constaint = solver.MakeConstraint(0, double.PositiveInfinity, "");
                constaint.SetCoefficient(y[j], data.BinCapacity);
                for (var i = 0; i < data.Items.Count; i++)
                {
                    constaint.SetCoefficient(x[i, j], -data.Items[i].Weight);
                }
            }

            // The objective function is merely the sum of y[j], that is the number of bins used.
            var objective = solver.Objective();
            for (var j = 0; j < data.MaxNumberOfBins; j++)
            {
                objective.SetCoefficient(y[j], 1);
            }

            // The number of bins has to be minimized
            objective.SetMinimization();

            var resultStatus = solver.Solve();

            if (resultStatus == Solver.ResultStatus.OPTIMAL)
            {
                var separator = new string('=', 20);

                Console.WriteLine($"Number of bins used: {objective.Value()}\n{separator}");

                double totalWeight = 0;

                for (var j = 0; j < data.MaxNumberOfBins; j++)
                {
                    if (y[j].SolutionValue() == 1)
                    {
                        Console.WriteLine($"Bin {j}");
                        double binWeight = 0;

                        for (var i = 0; i < data.Items.Count; i++)
                        {
                            if (x[i, j].SolutionValue() == 1)
                            {
                                Console.WriteLine($"Item {data.Items[i].Name} - Weight {data.Items[i].Weight}");
                                binWeight += data.Items[i].Weight;
                            }
                        }

                        Console.WriteLine($"Packed bin weight: {binWeight}\n{separator}");
                        totalWeight += binWeight;
                    }
                }
                Console.WriteLine($"Total packed weight: {totalWeight}");
            }
            else
            {
                Console.WriteLine("The problem does not have an optimal solution");
            }
        }

        static int L1Bound(DataModel model) 
        {
            return (int)Math.Ceiling(model.Items.Select(m => m.Weight).Sum() / model.BinCapacity);
        }

        static void Main(string[] args)
        {
            var start_time = DateTime.UtcNow;
            Console.WriteLine($"Job started at: {start_time.ToLocalTime().ToString()}");

            var model = ReadModelFromFile(Path.GetFullPath(@"Data\data_config.json"), Path.GetFullPath(@"Data\input.csv"));

            Console.WriteLine($"L1 Bound: {L1Bound(model)}");

            //SolveAndPrint(model);

            var end_time = DateTime.UtcNow;
            var duration = (end_time - start_time);
            Console.WriteLine($"Job completed at: {end_time.ToLocalTime().ToString()}. Took {duration.Minutes}m, {duration.Seconds}s, {duration.Milliseconds}ms. Total {duration.TotalSeconds}s");
        }

    }
}
