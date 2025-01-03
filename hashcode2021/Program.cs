﻿using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace hashcode2021
{
    class Program
    {
        static string[] inputFiles = {
            @"instances/I4000_S12000_C397.txt",
            @"instances/I7073_S9102_C1000.txt",
            @"instances/I10000_S35030_C1000.txt",
            @"instances/I8000_S95928_C1000.txt",
            @"instances/I500_S998_C1000.txt",
            @"instances/I1662_S10000_C1000.txt"
        };

        static void Main(string[] args)
        {
            DateTime startTime = DateTime.Now;

            foreach (string fileName in inputFiles)
            {
                Problem.EntireDuration = DateTime.Now; //aa
                Problem.EntireDuration = Problem.EntireDuration.AddMinutes(Problem.execDur);//aa
                DateTime solveStartTime = DateTime.Now;
                Solve(fileName);
                Console.WriteLine("Solve time: {0}", new TimeSpan(DateTime.Now.Ticks - solveStartTime.Ticks));
            }

            Console.WriteLine("Runtime: {0}", new TimeSpan(DateTime.Now.Ticks - startTime.Ticks));
        }

        static void Solve(string fileName)
        {
            Problem problem = Problem.LoadProblem(fileName);
            Console.WriteLine("*****************");
            Console.WriteLine("{0}, Duration: {1}, Intersections: {2}, Bonus Per Car: {3}, Streets: {4}, Cars: {5}",
                fileName, problem.Duration, problem.Intersections.Count, problem.BonusPerCar, problem.Streets.Count, problem.Cars.Count);
            Console.WriteLine("Score upper bound: {0}", problem.CalculateScoreUpperBound());

            // Remove streets that not car is using for the possible green lights
            int removedStreets = problem.RemoveUnusedStreets();
            Console.WriteLine("Removed streets: {0}", removedStreets);

            Solution solution = new Solution(problem.Intersections.Count);

            // Generate a dummy solution - each incoming street will get a green light for 1 cycle
            InitBasicSolution(problem, solution);

            // Run simulation and try to change the order of green lights to minimize blocking
            // D - use OptimizeGreenLightOrder4
            problem.OptimizeGreenLightOrder(solution, new HashSet<int>());
            
            // E - This works better
            //solution = OptimizeCycleDurationByNumberOfIncomingCars(problem, solution);

            // Add cycle time for the top blocked cars.
            // B - 64, C - 105
            // F - 16 With OptimizeGreenLightOrder3 is a bit higher
            solution = OptimizeCycleDuration(problem, solution, 50);

            // Remove streets where the only car that passes is a car that didn't finish from
            // the green light cycle
            solution = OptimizeCycleClearStreetsCarsDidntFinish(problem, solution);

            Console.WriteLine("Initial Solution Score: {0}", problem.RunSimulationLite(solution));

            //Hill-Climbing
            solution = OptimizeBruteForce(problem, solution, int.MaxValue);


            // Simulate solution
            int score = problem.RunSimulationLite(solution);
            Console.WriteLine("Score: {0}", score);

            // Generate output
            GenerateOutput(solution, fileName);
        }

        private static void FindOptimalParameter(Problem problem)
        {
            int bestScore = 0;
            for (int parameter = 1; parameter < 200; parameter++)
            {
                Solution solution = new Solution(problem.Intersections.Count);


                // Generate a dummy solution - each incoming street will get a green light for 1 cycle
                InitBasicSolution(problem, solution);


                // Run simulation and try to change the order of green lights to minimize blocking
                problem.OptimizeGreenLightOrder(solution, new HashSet<int>());


                solution = OptimizeCycleDuration(problem, solution, parameter);


                // Remove streets where the only car that passes is a car that didn't finish from
                // the green light cycle
                solution = OptimizeCycleClearStreetsCarsDidntFinish(problem, solution);


                // Simulate solution
                int simulationResultScore = problem.RunSimulationLite(solution);
                if (simulationResultScore > bestScore)
                {
                    Console.WriteLine("Found. Score: {0}, Parameter: {1}", simulationResultScore, parameter);
                    bestScore = simulationResultScore;
                }
            }
        }


        private static Solution OptimizeBruteForce(Problem problem, Solution solution, int maxPos)
        {
            int lastScore = problem.RunSimulationLite(solution);


            int maxGreenLigthCycle = solution.Intersections.Max(o => o.GreenLigths.Count);
            maxPos = Math.Min(maxPos, maxGreenLigthCycle);


            while (Problem.EntireDuration > DateTime.Now)//true)//aa
            {
                // For D - comment everything but this. Too slow.
                solution = OptimizeGreenLightOrderBruteForceSwap(problem, solution, maxPos);


                // Minimal improvement, very slow. Comment if time is limited.
                solution = OptimizeGreenLightOrderBruteForceMove(problem, solution, maxPos);


                // Tested only with 3 & 10. 
                // For E & F - 3 performed better
                // For B & C - 10
                for (int delta = 1; delta <= 3; delta++)
                {
                    solution = OptimizeGreenLightBruteForceDeltaDuration(problem, solution, maxPos, delta);
                    solution = OptimizeGreenLightBruteForceDeltaDuration(problem, solution, maxPos, -delta);
                }
                
                int score = problem.RunSimulationLite(solution);
                if (lastScore == score)
                    continue;//break;
                else
                {
                    Console.WriteLine("Done full loop, starting again");
                    lastScore = score;
                }
            }


            return solution;
        }


        private static Solution OptimizeGreenLightBruteForceDeltaDuration(Problem problem, Solution solution, int maxPos, int delta)
        {
            int bestScore = problem.RunSimulationLite(solution);


            for (int i = 0; i < solution.Intersections.Length; i++)
            {
                if (Problem.EntireDuration <= DateTime.Now) break; //aa
                int loopPos = Math.Min(maxPos, solution.Intersections[i].GreenLigths.Count);


                for (int pos = 0; pos < loopPos; pos++)
                {
                    if (Problem.EntireDuration <= DateTime.Now) break; //aa
                    Solution newSolution = (Solution)solution.Clone();
                    newSolution.Intersections[i].GreenLigths[pos].Duration += delta;
                    if (newSolution.Intersections[i].GreenLigths[pos].Duration < 0)
                        continue;


                    int newScore = problem.RunSimulationLite(newSolution);
                    if ((newScore > bestScore)||
                        ((newScore == bestScore)&&(delta < 0)))
                    {
                        solution = newSolution;
                        bestScore = newScore;
                        // Console.WriteLine("New best: {0} [Delta]", bestScore);
                    }
                    //if (Problem.EntireDuration <= DateTime.Now) break; //aa
                }
            }


            return solution;
        }


        private static Solution OptimizeGreenLightOrderBruteForceSwap(Problem problem, Solution solution, int maxPos)
        { 
            int bestScore = problem.RunSimulationLite(solution);


            for (int i = 0; i < solution.Intersections.Length; i++)
            {
                if (Problem.EntireDuration <= DateTime.Now) break; //aa
                int loopPos = Math.Min(maxPos, solution.Intersections[i].GreenLigths.Count);


                for (int pos2 = 1; pos2 < loopPos; pos2++)
                    for (int pos1 = 0; pos1 < pos2; pos1++)
                    {
                        if (Problem.EntireDuration <= DateTime.Now) break; //aa
                        Solution newSolution = (Solution)solution.Clone();
                        Utils.SwapItems(newSolution.Intersections[i].GreenLigths, pos1, pos2);


                        int newScore = problem.RunSimulationLite(newSolution);
                        if (newScore > bestScore)
                        {
                            solution = newSolution;
                            bestScore = newScore;
                            // Console.WriteLine("New best: {0} [Swap]", bestScore);
                        }
                        //if (Problem.EntireDuration <= DateTime.Now) break; //aa
                    }
            }


            return solution;
        }


        private static Solution OptimizeGreenLightOrderBruteForceMove(Problem problem, Solution solution, int maxPos)
        {
            int bestScore = problem.RunSimulationLite(solution);


            for (int i = 0; i < solution.Intersections.Length; i++)
            {
                if (Problem.EntireDuration <= DateTime.Now) break; //aa
                int loopPos = Math.Min(maxPos, solution.Intersections[i].GreenLigths.Count);


                for (int pos2 = 0; pos2 < loopPos; pos2++)
                    for (int pos1 = 0; pos1 < loopPos; pos1++)
                    {
                        if (Problem.EntireDuration <= DateTime.Now) break; //aa
                        if (pos1 == pos2)
                            continue;


                        // Skip - this is check by swap
                        if ((pos1 + 1 == pos2) || (pos1 - 1 == pos2))
                            continue;


                        Solution newSolution = (Solution)solution.Clone();
                        SolutionIntersection intersection = newSolution.Intersections[i];


                        // Move item
                        GreenLightCycle greenLightCycle = intersection.GreenLigths[pos1];
                        intersection.GreenLigths.RemoveAt(pos1);
                        intersection.GreenLigths.Insert(pos2, greenLightCycle);


                        int newScore = problem.RunSimulationLite(newSolution);
                        if (newScore > bestScore)
                        {
                            solution = newSolution;
                            bestScore = newScore;
                            // Console.WriteLine("New best: {0} [Move]", bestScore);
                        }
                        //if (Problem.EntireDuration <= DateTime.Now) break; //aa
                    }
            }


            return solution;
        }


        private static Solution OptimizeCycleClearStreetsCarsDidntFinish(Problem problem, Solution solution)
        {
            // Optimization - if a car didn't finish the drive & it's the only car on a street - remove 
            // that street from the green lights & stop the car
            SimulationResult result = problem.RunSimulation(solution);


            // Nothing to optimize here
            if (result.CarsNotFinished.Count == 0)
                return solution;


            Solution bestSolution = solution;
            int bestSolutionScore = result.Score;


            Solution newSolution = (Solution)solution.Clone();
            RemoveCycleStreetsUsedOnlyByCars(problem, newSolution, result.CarsNotFinished);
            int newResultScore = problem.RunSimulationLite(newSolution);
            if (newResultScore > bestSolutionScore)
            {
                bestSolution = newSolution;
                bestSolutionScore = newResultScore;
            }


            List<CarSimultionPosition> carsNotFinished = result.CarsNotFinished.OrderBy(o => o.TimeLeftOnDrive).ToList();


            while (true)
            {
                int bestSolutionExcludeCar = -1;
                for (int i = 0; i < carsNotFinished.Count; i++)
                {
                    CarSimultionPosition iCar = carsNotFinished[i];
                    carsNotFinished.RemoveAt(i);


                    newSolution = (Solution)solution.Clone();
                    RemoveCycleStreetsUsedOnlyByCars(problem, newSolution, carsNotFinished);
                    newResultScore = problem.RunSimulationLite(newSolution);
                    carsNotFinished.Insert(i, iCar);


                    if (newResultScore > bestSolutionScore)
                    {
                        bestSolution = newSolution;
                        bestSolutionScore = newResultScore;
                        bestSolutionExcludeCar = i;
                        break; 
                    }
                }
                if (bestSolutionExcludeCar == -1)
                    break; 


                carsNotFinished.RemoveAt(bestSolutionExcludeCar);
            }


            return bestSolution;
        }


        private static void RemoveCycleStreetsUsedOnlyByCars(Problem problem, Solution solution, List<CarSimultionPosition> cars)
        {
            Dictionary<int, int> incomingUsageCountByStreet = new Dictionary<int, int>();
            foreach (Street street in problem.Streets.Values)
                incomingUsageCountByStreet.Add(street.UniqueID, street.IncomingUsageCount);


            // Remove usage count of unwanted cars
            foreach (CarSimultionPosition car in cars)
                for (int s = 0; s < car.Car.Streets.Count - 1; s++)
                {
                    Street street = car.Car.Streets[s];
                    incomingUsageCountByStreet[street.UniqueID]--;
                }


            // Clear green lights of streets with no usage
            foreach (CarSimultionPosition car in cars)
                for (int s = 0; s < car.Car.Streets.Count - 1; s++)
                {
                    Street street = car.Car.Streets[s];
                    if (incomingUsageCountByStreet[street.UniqueID] == 0)
                    {
                        SolutionIntersection intersection = solution.Intersections[street.EndIntersection];
                        for (int g = 0; g < intersection.GreenLigths.Count; g++)
                        {
                            GreenLightCycle greenLightCycle = intersection.GreenLigths[g];
                            if (greenLightCycle.Street.UniqueID == street.UniqueID)
                            {
                                intersection.GreenLigths.RemoveAt(g);
                                g--;
                            }
                        }
                    }
                }
        }


        private static Solution OptimizeCycleDurationByNumberOfIncomingCars(Problem problem, Solution solution)
        {
            Solution bestSolution = solution;
            int bestScore = problem.RunSimulationLite(solution);


            for (int d = 1; d < 50; d++)
            {
                Solution newSolution = (Solution)solution.Clone();


                foreach (SolutionIntersection intersection in newSolution.Intersections)
                {
                    foreach (GreenLightCycle cycle in intersection.GreenLigths)
                        cycle.Duration = Math.Max(1, cycle.Street.IncomingUsageCount / d);
                }


                int resultScore = problem.RunSimulationLite(newSolution);
                if (resultScore > bestScore)
                {
                    bestSolution = newSolution;
                    bestScore = resultScore;
                }
            }


            return bestSolution;
        }


        private static Solution OptimizeCycleDuration(Problem problem, Solution solution, int addCycleDevider)
        {
            Solution bestSolution = null;
            int bestSolutionScore = -1;
            int timesSinceOptimized = 0;
            while (timesSinceOptimized < 10)//Problem.EntireDuration>DateTime.Now)// timesSinceOptimized < 10)//aa
            {
                timesSinceOptimized++;


                SimulationResult simulationResult = problem.RunSimulation(solution);


                if (simulationResult.Score > bestSolutionScore)
                {
                    bestSolution = (Solution)solution.Clone();
                    bestSolutionScore = simulationResult.Score;
                    timesSinceOptimized = 0;
                }
                //Console.WriteLine("Score: {0}, Max Blocked Traffic: {1}, Cars not finished: {2}, Best score: {3}", simulationResult.Score, simulationResult.GetMaxBlockedTraffic(), simulationResult.CarsNotFinished.Count, bestSolutionScore);


                List<SimulationResult.IntersectionResult> intersectionResults = simulationResult.IntersectionResults.OrderByDescending(o => o.MaxStreetBlockedTraffic).ToList();
                // Remove intersection without blocked cars
                for (int i = 0; i < intersectionResults.Count; i++)
                {
                    if (intersectionResults[i].MaxStreetBlockedTraffic == 0)
                    {
                        intersectionResults.RemoveAt(i);
                        i--;
                    }
                }


                // Nothing to optimize - break
                if (intersectionResults.Count == 0)
                    break; 


                for (int i = 0; i < intersectionResults.Count / addCycleDevider; i++)
                {
                    SimulationResult.IntersectionResult intersectionResult = intersectionResults[i];
                    SolutionIntersection intersection = solution.Intersections[intersectionResult.ID];
                    foreach (GreenLightCycle greenLightCycle in intersection.GreenLigths)
                        if (greenLightCycle.Street.Name.Equals(intersectionResult.MaxStreetName))
                            greenLightCycle.Duration++;
                }
            }


            return bestSolution;
        }


        private static Solution OptimizeCycleDuration2(Problem problem, Solution solution)
        {
            Dictionary<int, int> lastSimulationWaitingCarsByStreet = new Dictionary<int, int>();
            foreach (Street street in problem.Streets.Values)
                lastSimulationWaitingCarsByStreet.Add(street.UniqueID, -1);


            Solution bestSolution = null;
            int bestSolutionScore = -1;
            while (true)
            {
                SimulationResult simulationResult = problem.RunSimulation3(solution);

                if (simulationResult.Score > bestSolutionScore)
                {
                    bestSolution = (Solution)solution.Clone();
                    bestSolutionScore = simulationResult.Score;
                }
                else
                    // Stop if no improvement found
                    break;


                Console.WriteLine("Score: {0}, Max Blocked Traffic: {1}, Cars not finished: {2}, Best score: {3}", simulationResult.Score, simulationResult.GetMaxBlockedTraffic(), simulationResult.CarsNotFinished.Count, bestSolutionScore);


                List<SimulationResult.IntersectionResult> intersectionResults = simulationResult.IntersectionResults.OrderByDescending(o => o.MaxWaitOnGreenLight).ToList();
                // Remove intersection without blocked cars
                for (int i = 0; i < intersectionResults.Count; i++)
                {
                    if (intersectionResults[i].MaxWaitOnGreenLight == 0)
                    {
                        intersectionResults.RemoveAt(i);
                        i--;
                    }
                }

                // Nothing to optimize - break
                if (intersectionResults.Count == 0)
                    break;


                // Add cycle time for the top blocked cars.
                int lastBestScore = simulationResult.Score;
                for (int i = 0; i < intersectionResults.Count; i++)
                {
                    SimulationResult.IntersectionResult intersectionResult = intersectionResults[i];
                    SolutionIntersection intersection = solution.Intersections[intersectionResult.ID];
                    foreach (GreenLightCycle greenLightCycle in intersection.GreenLigths)
                        if (greenLightCycle.Street.UniqueID.Equals(intersectionResult.MaxWaitOnGreenLightStreetID))
                        {
                            // Don't repeat
                            if (lastSimulationWaitingCarsByStreet[greenLightCycle.Street.UniqueID] == intersectionResult.MaxWaitOnGreenLight)
                                break;

                            greenLightCycle.Duration++;
                            SimulationResult newResult = problem.RunSimulation3(solution);
                            if (newResult.Score <= lastBestScore)
                            {
                                greenLightCycle.Duration--;
                                lastSimulationWaitingCarsByStreet[greenLightCycle.Street.UniqueID] = intersectionResult.MaxWaitOnGreenLight;
                            }
                            else
                                lastSimulationWaitingCarsByStreet[greenLightCycle.Street.UniqueID] = -1;

                            break;
                        }
                }
            }

            return bestSolution;
        }

        private static void InitBasicSolution(Problem problem, Solution solution)
        {
            foreach (Intersection i in problem.Intersections)
            {
                solution.Intersections[i.ID].GreenLigths = new List<GreenLightCycle>();
                foreach (Street street in i.IncomingStreets)
                {
                    GreenLightCycle cycle = new GreenLightCycle(street);
                    cycle.Duration = 1;
                    solution.Intersections[i.ID].GreenLigths.Add(cycle);
                }
            }

        }

        private static void GenerateOutput(Solution solution, string fileName)
        {
            using (StreamWriter sw = new StreamWriter(fileName + ".out"))
            {
                sw.WriteLine(solution.CountIntersectionsWithGreenLights());

                foreach (SolutionIntersection i in solution.Intersections)
                {
                    // Count non-zero duration streets
                    int streetCount = 0;
                    foreach (GreenLightCycle c in i.GreenLigths)
                        if (c.Duration > 0)
                            streetCount++;

                    // Skip intersection - no streets
                    if (streetCount == 0)
                        continue;

                    sw.WriteLine(i.ID);
                    sw.WriteLine(streetCount);
                    foreach (GreenLightCycle c in i.GreenLigths)
                        if (c.Duration > 0)
                        {
                            sw.Write(c.Street.Name);
                            sw.Write(" ");
                            sw.WriteLine(c.Duration);
                        }
                }
            }
        }
    }
}
