using System;
using System.Globalization;
using Tracker.Domain;
using Tracker.Input;

namespace Tracker
{
    class Program
    {
        private static Result<string> ReadInput(string path)
        {
            try { return Result<string>.Ok(File.ReadAllText(path)); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
            {
                return Result<string>.Fail($"Cannot read input file: {error.Message}");
            }
        }

        static int Main(string[] args)
        {
            if (args.Length > 4)
            {
                Console.Error.WriteLine("Usage: DailyPlanner [file] [yyyy-MM-dd] [both|importance|short] [minimum importance]");
                return 1;
            }

            string path = args.Length > 0 ? args[0] : "samples/tasks.json";
            string dateText = args.Length > 1 ? args[1] : "2026-10-13";
            string policyName = args.Length > 2 ? args[2] : "both";
            string minimumText = args.Length > 3 ? args[3] : "3";

            if (!DateOnly.TryParseExact(dateText, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out DateOnly date))
            {
                Console.Error.WriteLine("Date must be a real date in yyyy-MM-dd format.");
                return 1;
            }

            if (!int.TryParse(minimumText, NumberStyles.None, CultureInfo.InvariantCulture, out int minimum)
                || minimum < 1 || minimum > 5)
            {
                Console.Error.WriteLine("Minimum importance must be an integer from 1 to 5.");
                return 1;
            }

            // Policy selection belongs to the application, outside the pure stages.
            var policies = new List<(string Name, Func<TaskItem, int> Score)>();
            switch (policyName)
            {
                case "both":
                    policies.Add(("Importance first", ScoringPolicies.ImportanceFirst));
                    policies.Add(("Short tasks first", ScoringPolicies.ShortTasksFirst));
                    break;
                case "importance":
                    policies.Add(("Importance first", ScoringPolicies.ImportanceFirst));
                    break;
                case "short":
                    policies.Add(("Short tasks first", ScoringPolicies.ShortTasksFirst));
                    break;
                default:
                    Console.Error.WriteLine("Policy must be both, importance, or short.");
                    return 1;
            }

            var plans = ReadInput(path)
                .Bind(TaskParser.Parse)
                .Map(tasks => policies.Select(policy =>
                    (policy.Name, Plan: TaskPipeline.CreatePlan(tasks, date, minimum, policy.Score))).ToArray());

            if (plans is Result<(string Name, DailyPlan Plan)[]>.Failure failure)
            {
                Console.Error.WriteLine(failure.Error);
                return 1;
            }
            if (plans is not Result<(string Name, DailyPlan Plan)[]>.Success success)
                throw new InvalidOperationException("Unknown planning result.");

            foreach (var policy in success.Value)
            {
                DailyPlan plan = policy.Plan;
                Console.WriteLine($"{policy.Name} | {plan.Date:yyyy-MM-dd}");
                foreach (RankedTask item in plan.Tasks)
                {
                    Console.WriteLine($"{item.Position}. {item.Task.Id} | {item.Task.Title} | score {item.Score} | {item.Task.Minutes} min");
                }
                Console.WriteLine($"Selected: {plan.Tasks.Count}; total: {plan.TotalMinutes} min");
                Console.WriteLine();
            }
            return 0;
        }
    }
}
