﻿using ComplementDrugSearch.Models;
using MathNet.Numerics.LinearAlgebra;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ComplementDrugSearch.Services
{
    /// <summary>
    /// Represents the hosted service corresponding to an application run.
    /// </summary>
    public class ApplicationRunService : BackgroundService
    {
        /// <summary>
        /// Represents the configuration.
        /// </summary>
        private readonly IConfiguration _configuration;

        /// <summary>
        /// Represents the logger.
        /// </summary>
        private readonly ILogger<ApplicationRunService> _logger;

        /// <summary>
        /// Represents the host application lifetime.
        /// </summary>
        private readonly IHostApplicationLifetime _hostApplicationLifetime;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="arguments">Represents the program arguments.</param>
        /// <param name="logger">Represents the logger.</param>
        public ApplicationRunService(IConfiguration configuration, ILogger<ApplicationRunService> logger, IHostApplicationLifetime hostApplicationLifetime)
        {
            _configuration = configuration;
            _logger = logger;
            _hostApplicationLifetime = hostApplicationLifetime;
        }

        /// <summary>
        /// Launches the application execution.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token corresponding to the task.</param>
        /// <returns>A runnable task.</returns>
        protected override Task ExecuteAsync(CancellationToken cancellationToken)
        {
            // Check if there is any request for displaying the help details.
            if (bool.TryParse(_configuration["Help"], out var displayHelp) && displayHelp)
            {
                // Log a message.
                _logger.LogInformation(string.Concat(
                    "\n\tWelcome to the ComplimentDrugSearch application!",
                    "\n\t",
                    "\n\t---",
                    "\n\t",
                    "\n\tThe following arguments can be provided:",
                    "\n\t--Help\tUse this parameter to display this help message.",
                    "\n\t--Interactions\tUse this parameter to specify the path to the file containing the protein-protein interactions. Each interaction should be on a new line, with its elements separated by tab characters. Each interaction should contain the source protein, the target protein, and the type (\"-1\" for a down-regulating interaction or equivalent, \"1\" for an up-regulating interaction or equivalent, or \"0\" otherwise).",
                    "\n\t--Drugs\tUse this parameter to specify the path to the file containing the possible drugs. Each drug should be on a new line, with its elements separated by tab characters. Each drug should contain the drug name, the corresponding drug target, and the type (\"-1\" for a drug that down-regulates its drug target, \"1\" for a drug that up-regulates its drug target, or \"0\" otherwise). Only the drugs with drug targets appearing in the interactions will be considered.",
                    "\n\t--DiseaseEssentialProteins\t(optional) Use this parameter to specify the path to the file containing the disease-essential proteins. Only proteins appearing in the interactions will be considered. Each protein should be on a new line. The parameter can be omitted if healthy-essential proteins are provided.",
                    "\n\t--HealthyEssentialProteins\t(optional) Use this parameter to specify the path to the file containing the disease-essential proteins. Only proteins appearing in the interactions will be considered. Each protein should be on a new line. The parameter can be omitted if disease-essential proteins are provided.",
                    "\n\t--Output\t(optional) Use this parameter to specify the path to the output file to be returned and created by the current run of the application. Writing permission is needed for the corresponding directory. If the file already exists, it will be overwritten! By default, the output file will be created in the same directory as the interactions file.",
                    "\n\t--Initial\tUse this parameter to specify the name of the initial drug or drug target, whose complement is needed. The initial drug must be in the list of drugs, and its corresponding drug target must appear in the interactions.",
                    "\n\t--MaximumPath\t(optional) Use this parameter to specify the length of the maximum path between the drug targets and the essential proteins. By default, it is equal to \"3\".",
                    "\n\t--NumberOfSolutions\t(optional) Use this parameter to specify the maximum number of solutions to be returned by the application. By default, it is equal to \"10\".",
                    "\n\tExamples of posible usage:",
                    "\n\t--Help \"True\"",
                    "\n\t--Interactions \"Path/To/FileContainingInteractions.extension\" --Drugs \"Path/To/FileContainingDrugs.extension\" --DiseaseEssentialProteins \"Path/To/FileContainingDiseaseEssentialProteins.extension\" --Initial \"InitialDrugName\"",
                    "\n\t--Interactions \"Path/To/FileContainingInteractions.extension\" --Drugs \"Path/To/FileContainingDrugs.extension\" --DiseaseEssentialProteins \"Path/To/FileContainingDiseaseEssentialProteins.extension\" --HealthyEssentialProteins \"Path/To/FileContainingHealthyEssentialProteins.extension\" --Initial \"InitialDrugName\" --Output \"Path/To/OutputFile.extension\" --MaximumPath \"3\" --NumberOfSolutions \"10\"",
                    "\n\t"));
                // Stop the application.
                _hostApplicationLifetime.StopApplication();
                // Return a successfully completed task.
                return Task.CompletedTask;
            }
            // Get the parameters from the configuration.
            var interactionsFilepath = _configuration["Interactions"];
            var drugsFilepath = _configuration["Drugs"];
            var diseaseEssentialProteinsFilepath = _configuration["DiseaseEssentialProteins"];
            var healthyEssentialProteinsFilepath = _configuration["HealthyEssentialProteins"];
            var outputFilepath = _configuration["Output"];
            var initialDrugString = _configuration["Initial"];
            var maximumPathString = _configuration["MaximumPath"];
            var numberOfSolutionsString = _configuration["NumberOfSolutions"];
            // Check if we have a file containing the interactions.
            if (string.IsNullOrEmpty(interactionsFilepath))
            {
                // Log an error.
                _logger.LogError("No file containing the interactions has been provided.");
                // Stop the application.
                _hostApplicationLifetime.StopApplication();
                // Return a successfully completed task.
                return Task.CompletedTask;
            }
            // Check if we have a file containing the drugs.
            if (string.IsNullOrEmpty(drugsFilepath))
            {
                // Log an error.
                _logger.LogError("No file containing the drugs has been provided.");
                // Stop the application.
                _hostApplicationLifetime.StopApplication();
                // Return a successfully completed task.
                return Task.CompletedTask;
            }
            // Check if we have a file containing the essential proteins.
            if (string.IsNullOrEmpty(diseaseEssentialProteinsFilepath) && string.IsNullOrEmpty(healthyEssentialProteinsFilepath))
            {
                // Log an error.
                _logger.LogError("No file containing the essential proteins has been provided.");
                // Stop the application.
                _hostApplicationLifetime.StopApplication();
                // Return a successfully completed task.
                return Task.CompletedTask;
            }
            // Get the current directory.
            var currentDirectory = AppDomain.CurrentDomain.BaseDirectory;
            // Check if the file containing the interactions exists.
            if (!File.Exists(interactionsFilepath))
            {
                // Log an error.
                _logger.LogError($"The file \"{interactionsFilepath}\" (containing the interactions) could not be found in the current directory \"{currentDirectory}\".");
                // Stop the application.
                _hostApplicationLifetime.StopApplication();
                // Return a successfully completed task.
                return Task.CompletedTask;
            }
            // Check if the file containing the drugs exists.
            if (!File.Exists(drugsFilepath))
            {
                // Log an error.
                _logger.LogError($"The file \"{drugsFilepath}\" (containing the drugs) could not be found in the current directory \"{currentDirectory}\".");
                // Stop the application.
                _hostApplicationLifetime.StopApplication();
                // Return a successfully completed task.
                return Task.CompletedTask;
            }
            // Check if the file containing the disease essential proteins exists.
            if (!string.IsNullOrEmpty(diseaseEssentialProteinsFilepath) && !File.Exists(diseaseEssentialProteinsFilepath))
            {
                // Log an error.
                _logger.LogError($"The file \"{diseaseEssentialProteinsFilepath}\" (containing the disease essential proteins) could not be found in the current directory \"{currentDirectory}\".");
                // Stop the application.
                _hostApplicationLifetime.StopApplication();
                // Return a successfully completed task.
                return Task.CompletedTask;
            }
            // Check if the file containing the healthy essential proteins exists.
            if (!string.IsNullOrEmpty(healthyEssentialProteinsFilepath) && !File.Exists(healthyEssentialProteinsFilepath))
            {
                // Log an error.
                _logger.LogError($"The file \"{healthyEssentialProteinsFilepath}\" (containing the healthy essential proteins) could not be found in the current directory \"{currentDirectory}\".");
                // Stop the application.
                _hostApplicationLifetime.StopApplication();
                // Return a successfully completed task.
                return Task.CompletedTask;
            }
            // Define the variables needed for the algorithm.
            var drugs = new List<Drug>();
            var proteins = new List<Protein>();
            var interactions = new List<Interaction>();
            // Try to read the interactions from the file.
            try
            {
                // Read all the rows in the file as tuples of (string, string, int).
                var rows = File.ReadAllLines(interactionsFilepath)
                    .Select(item => item.Split("\t"))
                    .Where(item => item.Length > 2)
                    .Where(item => !string.IsNullOrEmpty(item[0]) && !string.IsNullOrEmpty(item[1]) && int.TryParse(item[2], out var result));
                // Get all of the proteins.
                proteins = rows.Select(item => item[0])
                    .Concat(rows.Select(item => item[1]))
                    .Distinct()
                    .Select((item, index) => new Protein(index, item, false, false))
                    .ToList();
                // Get all of the interactions.
                interactions = rows
                    .Select(item => new Interaction(proteins.FirstOrDefault(item1 => item1.Name == item[0]), proteins.FirstOrDefault(item1 => item1.Name == item[1]), int.Parse(item[2])))
                    .Where(item => item.SourceProtein != null && item.TargetProtein != null)
                    .ToList();
            }
            catch
            {
                // Log an error.
                _logger.LogError($"An error occured while reading the file \"{interactionsFilepath}\" (containing the interactions).");
                // Stop the application.
                _hostApplicationLifetime.StopApplication();
                // Return a successfully completed task.
                return Task.CompletedTask;
            }
            // Try to read the drugs from the file.
            try
            {
                // Read all the rows in the file as tuples of (string, string).
                var rows = File.ReadAllLines(drugsFilepath)
                    .Select(item => item.Split("\t"))
                    .Where(item => item.Length > 2)
                    .Where(item => !string.IsNullOrEmpty(item[0]) && !string.IsNullOrEmpty(item[1]) && int.TryParse(item[2], out var result));
                // Get all of the drugs.
                drugs = rows
                    .Select(item => new Drug(item[0], proteins.FirstOrDefault(item1 => item1.Name == item[1]), int.Parse(item[2])))
                    .Where(item => item.Protein != null)
                    .ToList();
            }
            catch
            {
                // Log an error.
                _logger.LogError($"An error occured while reading the file \"{drugsFilepath}\" (containing the drugs).");
                // Stop the application.
                _hostApplicationLifetime.StopApplication();
                // Return a successfully completed task.
                return Task.CompletedTask;
            }
            // Check if there are any disease essential proteins to read.
            if (!string.IsNullOrEmpty(diseaseEssentialProteinsFilepath))
            {
                // Try to read the disease essential proteins from the file.
                try
                {
                    // Read all the rows in the file as a list of strings.
                    var rows = File.ReadAllLines(diseaseEssentialProteinsFilepath)
                        .Where(item => !string.IsNullOrEmpty(item))
                        .ToHashSet();
                    // Go over each of the corresponding proteins.
                    foreach (var protein in proteins.Where(item => rows.Contains(item.Name)))
                    {
                        // Mark it as disease essential.
                        protein.IsDiseaseEssential = true;
                    }
                }
                catch
                {
                    // Log an error.
                    _logger.LogError($"An error occured while reading the file \"{diseaseEssentialProteinsFilepath}\" (containing the disease essential proteins).");
                    // Stop the application.
                    _hostApplicationLifetime.StopApplication();
                    // Return a successfully completed task.
                    return Task.CompletedTask;
                }
            }
            // Check if there are any healthy essential proteins to read.
            if (!string.IsNullOrEmpty(healthyEssentialProteinsFilepath))
            {
                // Try to read the healthy essential proteins from the file.
                try
                {
                    // Read all the rows in the file as a list of strings.
                    var rows = File.ReadAllLines(healthyEssentialProteinsFilepath)
                        .Where(item => !string.IsNullOrEmpty(item))
                        .ToHashSet();
                    // Go over each of the corresponding proteins.
                    foreach (var protein in proteins.Where(item => rows.Contains(item.Name)))
                    {
                        // Mark it as healthy essential.
                        protein.IsHealthyEssential = true;
                    }
                }
                catch
                {
                    // Log an error.
                    _logger.LogError($"An error occured while reading the file \"{healthyEssentialProteinsFilepath}\" (containing the healthy essential proteins).");
                    // Stop the application.
                    _hostApplicationLifetime.StopApplication();
                    // Return a successfully completed task.
                    return Task.CompletedTask;
                }
            }
            // Check if there aren't any proteins.
            if (!proteins.Any())
            {
                // Log an error.
                _logger.LogError($"No proteins could be found with the provided data.");
                // Stop the application.
                _hostApplicationLifetime.StopApplication();
                // Return a successfully completed task.
                return Task.CompletedTask;
            }
            // Check if there aren't any interactions.
            if (!interactions.Any())
            {
                // Log an error.
                _logger.LogError($"No interactions could be found with the provided data.");
                // Stop the application.
                _hostApplicationLifetime.StopApplication();
                // Return a successfully completed task.
                return Task.CompletedTask;
            }
            // Check if there aren't any drugs.
            if (!drugs.Any())
            {
                // Log an error.
                _logger.LogError($"No drugs could be found with the provided data.");
                // Stop the application.
                _hostApplicationLifetime.StopApplication();
                // Return a successfully completed task.
                return Task.CompletedTask;
            }
            // Get the essential proteins.
            var essentialProteins = proteins.Where(item => item.IsDiseaseEssential || item.IsHealthyEssential);
            // Check if there aren't any essential proteins.
            if (!essentialProteins.Any())
            {
                // Log an error.
                _logger.LogError($"No essential proteins could be found with the provided data.");
                // Stop the application.
                _hostApplicationLifetime.StopApplication();
                // Return a successfully completed task.
                return Task.CompletedTask;
            }
            // Get the initial drug.
            var initialDrug = drugs.FirstOrDefault(item => item.Name == initialDrugString || item.Protein.Name == initialDrugString);
            // Check if there isn't any initial drug.
            if (initialDrug == null)
            {
                // Log an error.
                _logger.LogError($"The specified initial drug could not be found in the list of drugs or no interactions contain its corresponding drug target.");
                // Stop the application.
                _hostApplicationLifetime.StopApplication();
                // Return a successfully completed task.
                return Task.CompletedTask;
            }
            // Assign the default maximum path.
            maximumPathString = !string.IsNullOrEmpty(maximumPathString) ? maximumPathString : "3";
            // Try to parse the maximum path.
            if (!int.TryParse(maximumPathString, out var maximumPath))
            {
                // Log an error.
                _logger.LogError($"The maximum path could not be inferred from its argument \"{maximumPathString}\".");
                // Stop the application.
                _hostApplicationLifetime.StopApplication();
                // Return a successfully completed task.
                return Task.CompletedTask;
            }
            // Check if the maximum path is valid.
            if (maximumPath <= 0)
            {
                // Log an error.
                _logger.LogError($"The maximum path must be a positive integer.");
                // Stop the application.
                _hostApplicationLifetime.StopApplication();
                // Return a successfully completed task.
                return Task.CompletedTask;
            }
            // Assign the default number of returned solutions.
            numberOfSolutionsString = !string.IsNullOrEmpty(numberOfSolutionsString) ? numberOfSolutionsString : "10";
            // Try to parse the number of solutions.
            if (!int.TryParse(numberOfSolutionsString, out var numberOfSolutions))
            {
                // Log an error.
                _logger.LogError($"The number of solutions could not be inferred from its argument \"{numberOfSolutionsString}\".");
                // Stop the application.
                _hostApplicationLifetime.StopApplication();
                // Return a successfully completed task.
                return Task.CompletedTask;
            }
            // Check if the number of solutions is valid.
            if (numberOfSolutions <= 0)
            {
                // Log an error.
                _logger.LogError($"The number of solutions must be a positive integer.");
                // Stop the application.
                _hostApplicationLifetime.StopApplication();
                // Return a successfully completed task.
                return Task.CompletedTask;
            }
            // Check if there isn't any output filepath.
            if (string.IsNullOrEmpty(outputFilepath))
            {
                // Assign the default value.
                outputFilepath = Path.Combine(Path.GetFullPath(interactionsFilepath).Replace(Path.GetFileName(interactionsFilepath), string.Empty), $"{initialDrug.Name}_{initialDrug.Protein.Name}_{DateTime.Now.ToString("yyyyMMddHHmmss")}.json".Replace(" ", string.Empty));
            }
            // Log a message about the loaded data.
            _logger.LogInformation($"The data has been loaded successfully. There are {proteins.Count()} proteins (out of which {proteins.Count(item => item.IsDiseaseEssential)} disease essential and {proteins.Count(item => item.IsHealthyEssential)} healthy essential) and {interactions.Count()} interactions. The program will look for complement drugs around the initial drug \"{initialDrug.Name}\" (with the drug target \"{initialDrug.Protein.Name}\"), up to a maximum path length of \"{maximumPath}\" and save \"{numberOfSolutions}\" solution(s) to the output file \"{outputFilepath}\".");
            // Define a new stopwatch to measure the running time and start it.
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            // Log a message.
            _logger.LogInformation($"{DateTime.Now.ToString()}: Computing the corresponding matrices and matrix powers.");
            // Get the direction and adjacency matrix.
            var adjacencyMatrix = Matrix<double>.Build.Sparse(proteins.Count(), proteins.Count());
            var directionMatrix = Matrix<double>.Build.Sparse(proteins.Count(), proteins.Count());
            // Go over each interaction.
            foreach (var interaction in interactions)
            {
                // Set the corresponding entry in both matrices.
                adjacencyMatrix[interaction.SourceProtein.Index, interaction.TargetProtein.Index] = 1;
                directionMatrix[interaction.SourceProtein.Index, interaction.TargetProtein.Index] = interaction.Direction;
            }
            // Define the matrix lists.
            var adjacencyMatrixList = new List<Matrix<double>> { Matrix<double>.Build.SparseIdentity(proteins.Count(), proteins.Count()) };
            var directionMatrixList = new List<Matrix<double>> { Matrix<double>.Build.SparseIdentity(proteins.Count(), proteins.Count()) };
            // Go over all path values, up to the maximum path.
            for (int index = 1; index <= maximumPath; index++)
            {
                // Add the next matrix power.
                adjacencyMatrixList.Add(adjacencyMatrixList.Last().Multiply(adjacencyMatrix));
                directionMatrixList.Add(directionMatrixList.Last().Multiply(directionMatrix));
            }
            // Log a message.
            _logger.LogInformation($"{DateTime.Now.ToString()}: Computing the subgraph corresponding to the initial drug.");
            // Get all proteins which can be reached from the initial drug within maximum path.
            var subgraphProteins = adjacencyMatrixList
                .Select(item => item
                    .Row(initialDrug.Protein.Index)
                    .Select((value, index) => new { Item1 = value, Item2 = index })
                    .Where(item1 => item1.Item1 != 0)
                    .Select(item1 => item1.Item2))
                .SelectMany(item => item)
                .Distinct()
                .Select(item => proteins[item]);
            // Get the essential proteins in the subgraph.
            var subgraphEssentialProteins = subgraphProteins.Where(item => item.IsDiseaseEssential || item.IsHealthyEssential);
            // Check if there aren't any essential proteins in the subgraph.
            if (!subgraphEssentialProteins.Any())
            {
                // Log an error.
                _logger.LogError($"No essential proteins could be found within the subgraph corresponding to the initial drug.");
                // Stop the application.
                _hostApplicationLifetime.StopApplication();
                // Return a successfully completed task.
                return Task.CompletedTask;
            }
            // Log a message.
            _logger.LogInformation($"{DateTime.Now.ToString()}: Done! There are {subgraphProteins.Count()} proteins in the subgraph, out of which {subgraphEssentialProteins.Count()} are essential.");
            // Log a message.
            _logger.LogInformation($"{DateTime.Now.ToString()}: Computing the extended subgraph.");
            // Get all proteins which can reach the essential proteins in the subgraph within maximum path.
            var extendedSubgraphProteins = adjacencyMatrixList
                .Select(item => subgraphEssentialProteins
                    .Select(item1 => item
                        .Column(item1.Index)
                        .Select((value, index) => new { Item1 = value, Item2 = index })
                        .Where(item2 => item2.Item1 != 0)
                        .Select(item2 => item2.Item2))
                    .SelectMany(item1 => item1))
                .SelectMany(item => item)
                .Distinct()
                .Select(item => proteins[item]);
            // Get all the drugs with drug targets in the extended subgraph.
            var extendedSubgraphDrugs = drugs.Select(item => item.Protein).Intersect(extendedSubgraphProteins).Select(item => drugs.FirstOrDefault(item1 => item == item1.Protein));
            // Check if there aren't any drugs in the extended subgraph.
            if (!extendedSubgraphDrugs.Any())
            {
                // Log an error.
                _logger.LogError($"No drugs with drug targets within the extended subgraph could be found.");
                // Stop the application.
                _hostApplicationLifetime.StopApplication();
                // Return a successfully completed task.
                return Task.CompletedTask;
            }
            // Log a message.
            _logger.LogInformation($"{DateTime.Now.ToString()}: Done! There are {extendedSubgraphProteins.Count()} proteins and {extendedSubgraphDrugs.Count()} drugs in the extended subgraph.");
            // Log a message.
            _logger.LogInformation($"{DateTime.Now.ToString()}: Computing the direction from drugs to essential proteins within the extended subgraph.");
            // Define the dictionary with the essential protein data for each drug in the extended subgraph.
            var drugDictionary = new Dictionary<Drug, Dictionary<Protein, int>>();
            // Get the number of iterations.
            var currentIteration = 0;
            var totalIterations = extendedSubgraphDrugs.Count();
            // Go over each drug with target in the extended subgraph.
            foreach (var drug in extendedSubgraphDrugs)
            {
                // Check if the application is stopping.
                if (_hostApplicationLifetime.ApplicationStopping.IsCancellationRequested)
                {
                    // Break the loop.
                    break;
                }
                // Update the iteration.
                currentIteration += 1;
                // Log a message.
                _logger.LogInformation($"{DateTime.Now.ToString()}:\t{currentIteration}\t/\t{totalIterations}");
                // Define the drug dictionary value.
                drugDictionary[drug] = new Dictionary<Protein, int>();
                // Get all proteins which can be reached from the drug within maximum path.
                var drugSubgraphProteins = adjacencyMatrixList
                    .Select(item => item
                        .Row(drug.Protein.Index)
                        .Select((value, index) => new { Item1 = value, Item2 = index })
                        .Where(item1 => item1.Item1 != 0)
                        .Select(item1 => item1.Item2))
                    .SelectMany(item => item)
                    .Distinct()
                    .Select(item => proteins[item]);
                // Go over each of the essential proteins.
                foreach (var drugEssentialProtein in drugSubgraphProteins.Where(item => item.IsDiseaseEssential || item.IsHealthyEssential))
                {
                    // Get the lists of corresponding rows in each matrix list.
                    var adjacencyRowList = adjacencyMatrixList.Select(item => item[drug.Protein.Index, drugEssentialProtein.Index]).ToList();
                    var directionRowList = directionMatrixList.Select(item => item[drug.Protein.Index, drugEssentialProtein.Index]).ToList();
                    // Define the directions for the essential protein, for each path length.
                    var directions = new List<int>();
                    // Go over each possible path.
                    for (int index = 0; index < adjacencyRowList.Count(); index++)
                    {
                        // Check if there actually exists a path.
                        if (adjacencyRowList[index] != 0)
                        {
                            // Check if they have the same equivalent direction.
                            if (Math.Abs(directionRowList[index]) == adjacencyRowList[index])
                            {
                                // Assign the direction.
                                directions.Add((int) directionRowList[index] / (int) adjacencyRowList[index]);
                            }
                            else
                            {
                                // Assign the direction.
                                directions.Add(0);
                            }
                        }
                    }
                    // Check if there aren't any opposing directions.
                    if (directions.First() != 0 && directions.Distinct().Count() == 1)
                    {
                        // Assign the direction.
                        drugDictionary[drug][drugEssentialProtein] = directions.First() * drug.Direction;
                    }
                }
            }
            // Log a message.
            _logger.LogInformation($"{DateTime.Now.ToString()}: Computing the score of the drugs in the extended subgraph.");
            // Define the dictionary with the score of each drug.
            var drugScoreDictionary = new Dictionary<Drug, int>();
            // Get the set of keys in the initial drug dictionary.
            var initialDrugProteins = drugDictionary[initialDrug].Keys.ToList();
            // Go over each drug in the dictionary.
            foreach (var drug in drugDictionary.Keys.ToList())
            {
                // Define the score.
                var score = 0;
                // Get the set of keys in the extra drug dictionary.
                var extraDrugProteins = drugDictionary[drug].Keys.ToList();
                // Get the common elements of both dictionaries, and the ones specific to each.
                var commonProteins = initialDrugProteins.Intersect(extraDrugProteins);
                var initialOnlyProteins = initialDrugProteins.Except(extraDrugProteins);
                var extraOnlyProteins = extraDrugProteins.Except(initialDrugProteins);
                // Go over each common element that is disease essential.
                foreach (var protein in commonProteins.Where(item => item.IsDiseaseEssential))
                {
                    // Define the values to compare.
                    var values = (drugDictionary[initialDrug][protein], drugDictionary[drug][protein]);
                    // Compare the values.
                    switch (values)
                    {
                        case (-1, -1):
                            score += 0;
                            break;
                        case (-1, 0):
                            score += 0;
                            break;
                        case (-1, 1):
                            score += -1;
                            break;
                        case (0, -1):
                            score += 1;
                            break;
                        case (0, 0):
                            score += 0;
                            break;
                        case (0, 1):
                            score += -2;
                            break;
                        case (1, -1):
                            score += 1;
                            break;
                        case (1, 0):
                            score += 0;
                            break;
                        case (1, 1):
                            score += -2;
                            break;
                        default:
                            break;
                    }
                }
                // Go over each common element that is healthy essential.
                foreach (var protein in commonProteins.Where(item => item.IsHealthyEssential))
                {
                    // Define the values to compare.
                    var values = (drugDictionary[initialDrug][protein], drugDictionary[drug][protein]);
                    // Compare the values.
                    switch (values)
                    {
                        case (-1, -1):
                            score += -2;
                            break;
                        case (-1, 0):
                            score += 0;
                            break;
                        case (-1, 1):
                            score += 1;
                            break;
                        case (0, -1):
                            score += -2;
                            break;
                        case (0, 0):
                            score += 0;
                            break;
                        case (0, 1):
                            score += 1;
                            break;
                        case (1, -1):
                            score += -1;
                            break;
                        case (1, 0):
                            score += 0;
                            break;
                        case (1, 1):
                            score += 0;
                            break;
                        default:
                            break;
                    }
                }
                // Go over each initial only element that is disease essential.
                foreach (var protein in initialOnlyProteins.Where(item => item.IsDiseaseEssential))
                {
                    // Define the value to compare.
                    var value = drugDictionary[initialDrug][protein];
                    // Compare the value.
                    switch (value)
                    {
                        case -1:
                            score += 1;
                            break;
                        case 0:
                            score += 0;
                            break;
                        case 1:
                            score += -1;
                            break;
                        default:
                            break;
                    }
                }
                // Go over each initial only element that is healthy essential.
                foreach (var protein in initialOnlyProteins.Where(item => item.IsHealthyEssential))
                {
                    // Define the value to compare.
                    var value = drugDictionary[initialDrug][protein];
                    // Compare the value.
                    switch (value)
                    {
                        case -1:
                            score += -1;
                            break;
                        case 0:
                            score += 0;
                            break;
                        case 1:
                            score += 1;
                            break;
                        default:
                            break;
                    }
                }
                // Go over each extra only element that is disease essential.
                foreach (var protein in extraOnlyProteins.Where(item => item.IsDiseaseEssential))
                {
                    // Define the value to compare.
                    var value = drugDictionary[drug][protein];
                    // Compare the value.
                    switch (value)
                    {
                        case -1:
                            score += 1;
                            break;
                        case 0:
                            score += 0;
                            break;
                        case 1:
                            score += -1;
                            break;
                        default:
                            break;
                    }
                }
                // Go over each extra only element that is healthy essential.
                foreach (var protein in extraOnlyProteins.Where(item => item.IsHealthyEssential))
                {
                    // Define the value to compare.
                    var value = drugDictionary[drug][protein];
                    // Compare the value.
                    switch (value)
                    {
                        case -1:
                            score += -1;
                            break;
                        case 0:
                            score += 0;
                            break;
                        case 1:
                            score += 1;
                            break;
                        default:
                            break;
                    }
                }
                // Save the score of the drug.
                drugScoreDictionary[drug] = score;
            }
            // Get the drug solutions, sorted based on their score.
            var drugSolutions = drugScoreDictionary.OrderByDescending(item => item.Value).Take(numberOfSolutions).Select(item => item.Key);
            // Stop the measuring watch.
            stopwatch.Stop();
            // Define the output text to be written.
            var outputText = JsonSerializer.Serialize(new
            {
                Data = new
                {
                    Files = new
                    {
                        Interactions = Path.GetFullPath(interactionsFilepath),
                        DiseaseEssentialProteins = Path.GetFullPath(diseaseEssentialProteinsFilepath),
                        HealthyEssentialProteins = Path.GetFullPath(healthyEssentialProteinsFilepath),
                        Drugs = Path.GetFullPath(drugsFilepath)
                    },
                    Counts = new
                    {
                        ProteinCount = proteins.Count(),
                        InteractionCount = interactions.Count(),
                        DiseaseEssentialProteinCount = proteins.Count(item => item.IsDiseaseEssential),
                        HealthyEssentialProteinCount = proteins.Count(item => item.IsHealthyEssential),
                        DrugCount = drugs.Count()
                    },
                    TimeElapsed = stopwatch.Elapsed,
                },
                InitialDrug = new
                {
                    Drug = initialDrug.Name,
                    DrugTarget = initialDrug.Protein.Name,
                    DiseaseEssentialProteins = drugDictionary[initialDrug].Where(item => item.Key.IsDiseaseEssential).ToDictionary(item => item.Key.Name, item => item.Value),
                    HealthyEssentialProteins = drugDictionary[initialDrug].Where(item => item.Key.IsHealthyEssential).ToDictionary(item => item.Key.Name, item => item.Value)
                },
                SortedDrugs = drugSolutions.Select(item => new
                {
                    Drug = item.Name,
                    DrugTarget = item.Protein.Name,
                    Score = drugScoreDictionary[item],
                    DiseaseEssentialProteins = drugDictionary[item].Where(item1 => item1.Key.IsDiseaseEssential).ToDictionary(item1 => item1.Key.Name, item1 => item1.Value),
                    HealthyEssentialProteins = drugDictionary[item].Where(item1 => item1.Key.IsHealthyEssential).ToDictionary(item1 => item1.Key.Name, item1 => item1.Value)
                })
            }, new JsonSerializerOptions { WriteIndented = true });
            // Log a message.
            _logger.LogInformation($"{DateTime.Now.ToString()}: Writing the results in JSON format to file \"{outputFilepath}\".");
            // Try to write the results.
            try
            {
                // Write the text to the file.
                File.WriteAllText(outputFilepath, outputText);
            }
            catch (Exception ex)
            {
                // Log an error.
                _logger.LogError($"The error \"{ex.Message}\" occured while writing the results to the file \"{outputFilepath}\". The results will be displayed in the console instead.");
                // Log a message.
                _logger.LogInformation($"\n{outputText}");
                // Stop the application.
                _hostApplicationLifetime.StopApplication();
                // Return a successfully completed task.
                return Task.CompletedTask;
            }
            // Log a message.
            _logger.LogInformation($"{DateTime.Now.ToString()}: Application ended in {stopwatch.Elapsed}.");
            // Stop the application.
            _hostApplicationLifetime.StopApplication();
            // Return a successfully completed task.
            return Task.CompletedTask;
        }
    }
}
