# Daily task planner — Assignment 5

**Team:** Yerkebulan Abenov, Nurbergen Assan, Amanalina Tumargul and Khalid Ahmad Ahmadi — all IT-2511.

**Team size:** 4 students. Two- and three-member alternatives are in `handoff/TEAM.md`.

**Repository:** [Abenovv/Assignment-5-of-ITFP](https://github.com/Abenovv/Assignment-5-of-ITFP)

This planner is for students who keep a task list but need to decide what to work on first. Assignment 5 uses one operation: take a list of tasks and make an ordered plan for a chosen day. Adding, editing and saving tasks can come later in the final project.

## Run the project

Prerequisite: the .NET 10 SDK. The SDK used for this submission is **10.0.401**. There are no third-party packages, database or other services to configure. Run these commands from the extracted folder containing `DailyPlanner.sln`, not from `Tests`:

```powershell
dotnet build DailyPlanner.sln --disable-build-servers --maxcpucount:1
dotnet run --project DailyPlanner.csproj --no-build
dotnet run --project Tests/DailyPlanner.Tests.csproj --no-build
```

The last command is the automated test runner. It compares actual and expected values, prints PASS or FAIL for each test, and exits with code 1 if any test fails. Use this command instead of `dotnet test`, since this project uses its own assertion runner.

The default run uses `samples/tasks.json`, the fixed date `2026-10-13`, both policies and minimum importance 3. It does not read the computer's clock. To supply arguments:

```powershell
dotnet run --project DailyPlanner.csproj -- samples/tasks.json 2026-10-13 both 3
```

Arguments, in order: input path, date, policy (`both`, `importance` or `short`), minimum importance (1–5). Omitted trailing arguments use the defaults above. Input paths are relative to the working directory.

## Input and rules

The input is a JSON array of task objects. Every field is required:

| Field | Type | Accepted values |
| --- | --- | --- |
| `id` | string | Nonblank and unique; surrounding spaces are trimmed; case-sensitive |
| `title` | string | Nonblank; surrounding spaces are trimmed |
| `date` | string | A real date written as `yyyy-MM-dd` |
| `importance` | integer | 1 to 5, inclusive; 5 is the highest |
| `minutes` | integer | 0 to 480, inclusive; 0 allows a quick reminder |
| `completed` | boolean | `true` or `false` |

Unknown field names are rejected so a spelling mistake is visible. Missing fields, null fields, wrong types and invalid values reject the whole input; no partial plan is produced. `[]` is a valid empty dataset. A missing file, blank file or JSON `null` is an error. Zero minutes and `false` are real values, so they are not treated as missing.

A task is selected when its date matches the requested day, it is unfinished, and its importance is at least the chosen minimum. Selection does not impose a daily time limit. The output lists every selected task and gives the total estimated minutes.

The two scoring rules are:

- **Importance first:** `importance * 10`.
- **Short tasks first:** `importance * 10 + max(0, 60 - minutes)`. This gives tasks shorter than an hour a bonus. It still considers importance, so it is not a strict sort by duration.

Higher scores come first. Equal scores are sorted by ID using ordinal string order. Positions start at 1. An empty plan has zero tasks and zero total minutes.

## Expected results, worked out before running

For `2026-10-13` and minimum importance 3:

| ID | Task | Importance | Minutes | Completed | Selection | Importance score | Short-task score |
| --- | --- | --- | --- | --- | --- | --- | --- |
| T1 | Review FP notes | 5 | 60 | false | Included | 50 | 50 |
| T2 | Finish C# exercises | 4 | 30 | false | Included | 40 | 70 |
| T3 | Plan tomorrow | 3 | 10 | false | Included at the threshold | 30 | 80 |
| T4 | Sort downloads | 2 | 15 | false | Below the threshold | — | — |
| T5 | Submit lab report | 5 | 15 | true | Already completed | — | — |
| T6 | Read next chapter | 4 | 20 | false | Dated October 14 | — | — |
| T7 | Practice delegates | 4 | 20 | false | Included | 40 | 80 |

T4, T5 and T6 contain valid data, but the selection rule excludes them. The separate failure samples contain invalid data and are rejected during validation.

Expected importance order: **T1, T2, T7, T3**. Expected short-task order: **T3, T7, T2, T1**. Both plans contain four tasks and total **120 minutes**. T3 moves from last to first when the scoring rule changes. T3 wins its tie with T7 by ID.

Expected default console output:

```text
Importance first | 2026-10-13
1. T1 | Review FP notes | score 50 | 60 min
2. T2 | Finish C# exercises | score 40 | 30 min
3. T7 | Practice delegates | score 40 | 20 min
4. T3 | Plan tomorrow | score 30 | 10 min
Selected: 4; total: 120 min

Short tasks first | 2026-10-13
1. T3 | Plan tomorrow | score 80 | 10 min
2. T7 | Practice delegates | score 80 | 20 min
3. T2 | Finish C# exercises | score 70 | 30 min
4. T1 | Review FP notes | score 50 | 60 min
Selected: 4; total: 120 min
```

## The three stages

`CreatePlan` connects the stages with ordinary function calls. The partial `TaskPipeline` class keeps its stages in separate files so team members can work on different methods:

```csharp
var selected = SelectTasks(tasks, date, minimumImportance);
var scored = ScoreTasks(selected, scorePolicy);
return BuildPlan(scored, date);
```

| Stage | Input → output | Rule and example |
| --- | --- | --- |
| `SelectTasks` | `IReadOnlyList<TaskItem>` plus date and minimum → `IReadOnlyList<TaskItem>` | Keep unfinished tasks on that date with enough importance. T3 stays because 3 meets the minimum; T4 is removed because 2 does not. |
| `ScoreTasks` | `IReadOnlyList<TaskItem>` plus `Func<TaskItem, int>` → `IReadOnlyList<ScoredTask>` | Call the supplied rule for each selected task. T3 gets 30 under importance first or `30 + 50 = 80` under short tasks first. |
| `BuildPlan` | `IReadOnlyList<ScoredTask>` plus date → `DailyPlan` | Sort, assign positions and total the minutes. Under short tasks first, T3 becomes position 1, and the complete plan totals 120 minutes. |

`ScoreTasks` is the higher-order function: it accepts `Func<TaskItem, int>` and calls `scorePolicy(task)`. `Program.Main` chooses the delegates and passes them into the same workflow. Neither production policy captures changing configuration.

The core accepts validated domain records. It does not read files, print, ask for input, use randomness or look up the current time. Records hold immutable values. Each stage returns a new read-only collection backed by its own array; `DailyPlan` also copies the collection it receives. Sorting and scoring do not change the caller's data. The same values and either supplied pure policy produce the same result. A custom delegate must also be pure to preserve this property.

## Empty and failure demonstrations

```powershell
dotnet run --project DailyPlanner.csproj --no-build -- samples/empty.json
dotnet run --project DailyPlanner.csproj --no-build -- samples/invalid.json
dotnet run --project DailyPlanner.csproj --no-build -- samples/missing-field.json
dotnet run --project DailyPlanner.csproj --no-build -- samples/malformed.json
dotnet run --project DailyPlanner.csproj --no-build -- samples/null.json
dotnet run --project DailyPlanner.csproj --no-build -- samples/not-found.json
```

The empty sample prints both policy headings and `Selected: 0; total: 0 min`, then exits with code 0. Each failure exits with code 1 and prints an error instead of a plan:

| Sample | Expected reason |
| --- | --- |
| `invalid.json` | Record 1 has negative minutes |
| `missing-field.json` | Record 1 is missing `completed` |
| `malformed.json` | `importance` is a string instead of an integer |
| `null.json` | Input is null rather than an array |
| `not-found.json` (intentionally absent) | The input file cannot be read |

An invalid command-line date, policy or minimum importance also exits with code 1.

## Requirement map

| Requirement | Implementation |
| --- | --- |
| Project topic, users, rules and dataset | This README and `samples/tasks.json` |
| Typed inputs and outputs | `Domain/Models.cs`: `TaskItem`, `ScoredTask`, `RankedTask`, `DailyPlan` |
| Parse and validate at the boundary | `Input/TaskParser.cs`: `RawTask`, `TaskParser.TryParse`; argument checks in `Program.Main` |
| Three useful composed stages | `Domain/TaskSelection.cs`, `TaskScoring.cs`, `PlanBuilder.cs`; `CreatePlan` in `TaskPipeline.cs` joins them |
| Higher-order function and two policies | `ScoreTasks` and `Domain/ScoringPolicies.cs`; selection in `Program.Main` |
| Pure calculations and separate I/O | `Domain/` contains calculations; `Program.cs` reads files and displays completed results |
| Automated assertions | `Tests/InputTests.cs`, `SelectionTests.cs`, `ScoringTests.cs`, `PlanTests.cs`; `Tests/Program.cs` runs assertions and exits nonzero on failure |
| Runnable submission | `DailyPlanner.sln`, both `.csproj` files, sources and sample files |

## Tests

The runner has 17 tests. The assertions cover:

| Test | Expected result |
| --- | --- |
| Normal workflow | IDs T1, T2, T7, T3; scores 50, 40, 40, 30; positions 1–4; 120 minutes |
| Empty dataset | Both policies return zero tasks and zero minutes |
| Importance boundary | Importance 3 is included; 2 is excluded; raising the minimum to 4 leaves T1, T2, T7 and 110 minutes |
| Zero and false | A zero-minute unfinished task is accepted and scores 90 under the short-task policy |
| Two policies | The same input gives both documented orders and scores |
| Selection stage | Only T1, T2, T3 and T7 survive, in input order |
| Scoring stage | A supplied delegate runs once for each selected task and its returned values are used |
| No mutation | Input task values, input order and intermediate collections stay unchanged |
| Defensive copies | Changing source lists cannot change results; returned collections reject mutation |
| Determinism | Repeated runs with each policy return equal task values and totals |
| Malformed input | Broken JSON, wrong field types and misspelled fields are rejected |
| Missing input | Null text, blank text, JSON null and null task records are rejected |
| Missing fields | Each of the six fields is rejected when absent or null |
| Invalid values | Blank IDs/titles, bad dates, out-of-range importance, negative minutes and duplicate IDs are rejected |
| Duration boundary | 480 minutes is accepted with no short-task bonus; 481 is rejected |
| Tie order | Equal scores are ordered by ID, even if input order differs |
| Invalid threshold | Minimum importance outside 1–5 throws a clear argument exception |

Latest verification, October 18, 2026: build succeeded with 0 warnings and 0 errors; **17/17 tests passed**. Both default policy outputs matched the expected results above. Ten additional command-line checks passed: empty input, the five failure samples, invalid date, invalid policy, invalid threshold, and the threshold-4 plan.

## Contribution and defense notes

The project continues Yerkebulan's existing Assignment 5 daily planner. The prepared work split is listed below; each member reviews, adapts, tests and defends their part. Preparation used AI assistance.

| Member | Logic to review and develop | Tests to own |
| --- | --- | --- |
| Yerkebulan Abenov, IT-2511 | Input validation, immutable models and CLI | `InputTests.cs` |
| Nurbergen Assan, IT-2511 | Task selection and importance threshold | `SelectionTests.cs` |
| Amanalina Tumargul, IT-2511 | Scoring policies and delegate-based scoring | `ScoringTests.cs` |
| Khalid Ahmad Ahmadi, IT-2511 | Ranking, plan construction and pipeline composition | `PlanTests.cs` |

Use case `4` in `handoff/ownership.json` and the `team-4` packages. `handoff/TEAM.md` also keeps the two- and three-person alternatives. The personal bundle's `START_HERE.md` gives the Git steps for both assignments.

For the defense, trace T3: its raw JSON is validated into a `TaskItem`; selection keeps it at importance 3; scoring produces a `ScoredTask` with 30 or 80; ranking creates a `RankedTask` at position 4 or 1 inside a `DailyPlan`. Point to `scorePolicy(task)` to show where the passed function runs.

For a small live change, run with minimum importance 4:

```powershell
dotnet run --project DailyPlanner.csproj --no-build -- samples/tasks.json 2026-10-13 both 4
dotnet run --project Tests/DailyPlanner.Tests.csproj --no-build
```

T3 is removed, leaving three tasks and 110 minutes. `ImportanceBoundary` already checks this change. For a code change, adjust a policy formula, work out its new scores first, update the relevant assertions and rebuild before running tests.

Keep this project for Assignments 6–8. Assignment 5 uses a console interface; a web interface and full task storage are outside this stage. Git hosting is required later for the final project, but does not replace the ZIP submitted to Moodle.
