namespace WorkforceSync.HcmSource;

/// <summary>
/// A scripted workforce scenario for the mock HCM. Starts with a few hires
/// already in the system, then adds one more event every <see cref="StepSeconds"/>
/// (position change, comp change, termination, new hire) so a live dashboard
/// visibly updates over time. Deterministic — no randomness.
/// </summary>
public sealed class HcmScenario
{
    private readonly List<HcmEvent> _events = new();
    private readonly DateTime _start = DateTime.UtcNow;
    private int _step;
    private readonly object _gate = new();

    /// <summary>Seconds between scripted events.</summary>
    public int StepSeconds { get; } = 15;

    public HcmScenario()
    {
        // Seed: three hires already in the system at t=0.
        _events.Add(Hire("emp-1001", "Ada", "Lovelace", "ada@corp.example", "pos-1", "Software Engineer", "Engineering", 118000m));
        _events.Add(Hire("emp-1002", "Grace", "Hopper", "grace@corp.example", "pos-2", "Systems Analyst", "Engineering", 124000m));
        _events.Add(Hire("emp-1003", "Alan", "Turing", "alan@corp.example", "pos-3", "Data Scientist", "Analytics", 131000m));
    }

    /// <summary>
    /// The current set of events. Advances the scenario based on elapsed time
    /// before returning, so repeated calls grow the feed.
    /// </summary>
    public IReadOnlyList<HcmEvent> CurrentEvents
    {
        get
        {
            lock (_gate)
            {
                Advance();
                return _events.ToList();
            }
        }
    }

    private void Advance()
    {
        var elapsed = (int)(DateTime.UtcNow - _start).TotalSeconds;
        var target = Math.Min(elapsed / StepSeconds, MaxSteps);

        while (_step <= target)
        {
            AddStep(_step);
            _step++;
        }
    }

    private const int MaxSteps = 7;

    private void AddStep(int step)
    {
        var now = DateTime.UtcNow;
        switch (step)
        {
            case 1:
                _events.Add(new HcmEvent(
                    Id: "evt-2001", Type: "PositionChange", OccurredAtUtc: now,
                    EmployeeId: "emp-1001", PositionId: "pos-4",
                    JobTitle: "Senior Software Engineer", Department: "Engineering"));
                break;
            case 2:
                _events.Add(new HcmEvent(
                    Id: "evt-2002", Type: "CompensationChange", OccurredAtUtc: now,
                    EmployeeId: "emp-1002", BaseSalary: 132000m, Currency: "USD"));
                break;
            case 3:
                _events.Add(new HcmEvent(
                    Id: "evt-2003", Type: "Termination", OccurredAtUtc: now,
                    EmployeeId: "emp-1003", EndDate: now.Date));
                break;
            case 4:
                _events.Add(Hire("emp-1004", "Katherine", "Johnson", "katherine@corp.example", "pos-5", "Flight Engineer", "Analytics", 127000m, now));
                break;
            case 5:
                _events.Add(new HcmEvent(
                    Id: "evt-2005", Type: "PositionChange", OccurredAtUtc: now,
                    EmployeeId: "emp-1004", PositionId: "pos-6",
                    JobTitle: "Senior Flight Engineer", Department: "Analytics"));
                break;
            case 6:
                _events.Add(new HcmEvent(
                    Id: "evt-2006", Type: "CompensationChange", OccurredAtUtc: now,
                    EmployeeId: "emp-1001", BaseSalary: 129000m, Currency: "USD"));
                break;
            case 7:
                _events.Add(Hire("emp-1005", "Margaret", "Hamilton", "margaret@corp.example", "pos-7", "Software Architect", "Engineering", 142000m, now));
                break;
        }
    }

    private static HcmEvent Hire(
        string employeeId, string first, string last, string email,
        string positionId, string jobTitle, string department, decimal salary,
        DateTime? occurred = null)
    {
        var at = occurred ?? DateTime.UtcNow;
        return new HcmEvent(
            Id: $"evt-{employeeId}",
            Type: "Hire",
            OccurredAtUtc: at,
            EmployeeId: employeeId,
            FirstName: first,
            LastName: last,
            Email: email,
            PositionId: positionId,
            JobTitle: jobTitle,
            Department: department,
            StartDate: at.Date,
            BaseSalary: salary,
            Currency: "USD");
    }
}
