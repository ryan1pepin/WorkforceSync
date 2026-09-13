using System.Globalization;

namespace WorkforceSync.HcmSource;

/// <summary>
/// An endless, varied workforce-activity stream for the mock HCM. Starts with a
/// few hires already in the system, then — every <see cref="StepSeconds"/> —
/// emits a weighted-random event (hire, promotion, pay change, termination)
/// against a growing internal roster.
///
/// It is <em>not</em> a fixed script: the roster evolves (people get promoted,
/// paid more, leave, and new people join), so the feed keeps producing fresh,
/// different events forever. A fixed RNG seed keeps the sequence reproducible
/// (the demo is repeatable) without it being a simple loop of the same actions.
///
/// Every event gets a unique, monotonic id so the consumer's idempotency check
/// treats each one as new.
/// </summary>
public sealed class HcmScenario
{
    private readonly List<HcmEvent> _events = new();
    private readonly List<RosterEntry> _roster = new();
    private readonly List<RosterEntry> _terminated = new();
    private readonly Random _rng;
    private readonly object _gate = new();
    private int _eventCounter;
    private DateTime _nextEventAt;

    /// <summary>Seconds between generated events.</summary>
    public int StepSeconds { get; }

    public HcmScenario(int stepSeconds = 15)
    {
        StepSeconds = Math.Max(1, stepSeconds);
        _rng = new Random(1337); // fixed seed → reproducible but varied

        // Seed: three hires already in the system at t=0.
        SeedHire("emp-1001", "Ada", "Lovelace", "Engineering", level: 1, salary: 118000m);
        SeedHire("emp-1002", "Grace", "Hopper", "Engineering", level: 1, salary: 124000m);
        SeedHire("emp-1003", "Alan", "Turing", "Analytics", level: 1, salary: 131000m);

        _nextEventAt = DateTime.UtcNow; // first generated event is due immediately
    }

    /// <summary>
    /// The current set of events. Advances the scenario based on elapsed time
    /// before returning, so repeated calls keep growing the feed.
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
        var now = DateTime.UtcNow;
        while (now >= _nextEventAt)
        {
            EmitRandomEvent(now);
            _nextEventAt = _nextEventAt.AddSeconds(StepSeconds);
        }
    }

    private void EmitRandomEvent(DateTime now)
    {
        // Occasionally emit a stale/out-of-order event — a transfer or pay change
        // for someone who has already left. Real feeds have these (late or
        // duplicated messages), and the pipeline must reject them. This is what
        // exercises the "change after termination" guard.
        if (_terminated.Count > 0 && _rng.Next(100) < 8)
        {
            EmitStaleChangeForTerminated(now);
            return;
        }

        // Occasionally rehire someone who left — a real HCM "Rehire" event that
        // reactivates the person with a fresh assignment.
        if (_terminated.Count > 0 && _rng.Next(100) < 6)
        {
            EmitRehire(now);
            return;
        }

        // Weighted pick. Termination is only offered when there's someone to
        // terminate; otherwise its weight is redistributed.
        var roll = _rng.Next(100);
        var canTerminate = _roster.Count > 0;

        if (roll < 30)
        {
            EmitHire(now);
        }
        else if (roll < 55)
        {
            EmitPromotion(now);
        }
        else if (roll < 80)
        {
            EmitPayChange(now);
        }
        else if (canTerminate)
        {
            EmitTermination(now);
        }
        else
        {
            EmitHire(now); // no one to terminate — fall back to a hire
        }
    }

    /// <summary>
    /// A stale event: a transfer or pay change targeting a terminated employee.
    /// The consumer rejects it (a new role for a terminated person is a rehire,
    /// which must arrive as a Hire with pay).
    /// </summary>
    private void EmitStaleChangeForTerminated(DateTime now)
    {
        var e = Pick(_terminated);
        if (_rng.Next(2) == 0)
        {
            // Stale transfer.
            var newTitle = TitleFor(e.Department, Math.Min(e.Level + 1, MaxLevel));
            _events.Add(new HcmEvent(
                Id: NextEventId(),
                Type: "Transfer",
                OccurredAtUtc: now,
                EmployeeId: e.EmployeeId,
                PositionId: $"pos-{100 + _eventCounter}",
                Job: JobFor(e.Department),
                Grade: GradeFor(Math.Min(e.Level + 1, MaxLevel)),
                JobTitle: newTitle,
                Department: e.Department));
        }
        else
        {
            // Stale pay change.
            var newSalary = Math.Round(e.BaseSalary * (1m + Pct(0.04m, 0.10m)), 0, MidpointRounding.AwayFromZero);
            _events.Add(new HcmEvent(
                Id: NextEventId(),
                Type: "PayChange",
                OccurredAtUtc: now,
                EmployeeId: e.EmployeeId,
                BaseSalary: newSalary,
                Currency: "USD"));
        }
    }

    private void EmitHire(DateTime now)
    {
        var first = Pick(FirstNames);
        var last = Pick(LastNames);
        var dept = Pick(Departments);
        var level = 0; // new hires start at entry level
        var salary = SalaryFor(dept, level);

        var empId = $"emp-{1000 + _eventCounter}";
        var email = $"{first}.{last}{_eventCounter}@corp.example".ToLowerInvariant();
        var posId = $"pos-{100 + _eventCounter}";
        var title = TitleFor(dept, level);

        _roster.Add(new RosterEntry(
            empId, PersonNumberFor(empId), first, last, email,
            Pick(LegalEmployers), posId, JobFor(dept), GradeFor(level),
            title, dept, Pick(WorkLocations), Pick(Supervisors), salary, level));

        _events.Add(new HcmEvent(
            Id: NextEventId(),
            Type: "Hire",
            OccurredAtUtc: now,
            EmployeeId: empId,
            PersonNumber: PersonNumberFor(empId),
            FirstName: first,
            LastName: last,
            Email: email,
            LegalEmployer: Pick(LegalEmployers),
            PositionId: posId,
            Job: JobFor(dept),
            Grade: GradeFor(level),
            JobTitle: title,
            Department: dept,
            WorkLocation: Pick(WorkLocations),
            Supervisor: Pick(Supervisors),
            EmploymentType: _rng.Next(100) < 10 ? "Temporary" : "Regular",
            PayBasis: "Annual",
            StartDate: now.Date,
            BaseSalary: salary,
            Currency: "USD"));
    }

    private void EmitPromotion(DateTime now)
    {
        var e = Pick(_roster);
        if (e.Level >= MaxLevel)
        {
            // Already at the top — a promotion becomes a pay bump instead.
            EmitPayChange(now);
            return;
        }

        e.Level++;
        e.JobTitle = TitleFor(e.Department, e.Level);
        e.PositionId = $"pos-{100 + _eventCounter}";
        e.BaseSalary = Math.Round(e.BaseSalary * (1m + Pct(0.10m, 0.18m)), 0, MidpointRounding.AwayFromZero);

        _events.Add(new HcmEvent(
            Id: NextEventId(),
            Type: "Promotion",
            OccurredAtUtc: now,
            EmployeeId: e.EmployeeId,
            PositionId: e.PositionId,
            Job: JobFor(e.Department),
            Grade: GradeFor(e.Level),
            JobTitle: e.JobTitle,
            Department: e.Department,
            WorkLocation: e.WorkLocation,
            Supervisor: e.Supervisor,
            BaseSalary: e.BaseSalary,
            Currency: "USD"));
    }

    private void EmitPayChange(DateTime now)
    {
        var e = Pick(_roster);
        e.BaseSalary = Math.Round(e.BaseSalary * (1m + Pct(0.04m, 0.10m)), 0, MidpointRounding.AwayFromZero);

        _events.Add(new HcmEvent(
            Id: NextEventId(),
            Type: "PayChange",
            OccurredAtUtc: now,
            EmployeeId: e.EmployeeId,
            BaseSalary: e.BaseSalary,
            Currency: "USD"));
    }

    private void EmitTermination(DateTime now)
    {
        var e = Pick(_roster);
        _roster.Remove(e);
        _terminated.Add(e);

        _events.Add(new HcmEvent(
            Id: NextEventId(),
            Type: "Termination",
            OccurredAtUtc: now,
            EmployeeId: e.EmployeeId,
            EndDate: now.Date,
            TerminationReason: Pick(TerminationReasons)));
    }

    /// <summary>
    /// A rehire: a previously terminated employee comes back with a fresh
    /// assignment (entry level, new position, current salary band).
    /// </summary>
    private void EmitRehire(DateTime now)
    {
        var e = Pick(_terminated);
        _terminated.Remove(e);

        e.Level = 0; // rehire at entry level
        e.Grade = GradeFor(e.Level);
        e.JobTitle = TitleFor(e.Department, e.Level);
        e.PositionId = $"pos-{100 + _eventCounter}";
        e.BaseSalary = SalaryFor(e.Department, e.Level);
        _roster.Add(e);

        _events.Add(new HcmEvent(
            Id: NextEventId(),
            Type: "Rehire",
            OccurredAtUtc: now,
            EmployeeId: e.EmployeeId,
            PersonNumber: e.PersonNumber,
            FirstName: e.FirstName,
            LastName: e.LastName,
            Email: e.Email,
            LegalEmployer: e.LegalEmployer,
            PositionId: e.PositionId,
            Job: e.Job,
            Grade: e.Grade,
            JobTitle: e.JobTitle,
            Department: e.Department,
            WorkLocation: e.WorkLocation,
            Supervisor: e.Supervisor,
            EmploymentType: "Regular",
            PayBasis: "Annual",
            StartDate: now.Date,
            BaseSalary: e.BaseSalary,
            Currency: "USD"));
    }

    private void SeedHire(string empId, string first, string last, string dept, int level, decimal salary)
    {
        var email = $"{first}.{last}@corp.example".ToLowerInvariant();
        var posId = $"pos-{100 + _eventCounter}";
        var title = TitleFor(dept, level);
        _roster.Add(new RosterEntry(
            empId, PersonNumberFor(empId), first, last, email,
            Pick(LegalEmployers), posId, JobFor(dept), GradeFor(level),
            title, dept, Pick(WorkLocations), Pick(Supervisors), salary, level));

        _events.Add(new HcmEvent(
            Id: NextEventId(),
            Type: "Hire",
            OccurredAtUtc: DateTime.UtcNow,
            EmployeeId: empId,
            PersonNumber: PersonNumberFor(empId),
            FirstName: first,
            LastName: last,
            Email: email,
            LegalEmployer: Pick(LegalEmployers),
            PositionId: posId,
            Job: JobFor(dept),
            Grade: GradeFor(level),
            JobTitle: title,
            Department: dept,
            WorkLocation: Pick(WorkLocations),
            Supervisor: Pick(Supervisors),
            EmploymentType: "Regular",
            PayBasis: "Annual",
            StartDate: DateTime.UtcNow.Date,
            BaseSalary: salary,
            Currency: "USD"));
    }

    private string NextEventId() => $"evt-{_eventCounter++}";

    private T Pick<T>(IReadOnlyList<T> list) => list[_rng.Next(list.Count)];

    private decimal Pct(decimal min, decimal max) =>
        (decimal)_rng.NextDouble() * (max - min) + min;

    private decimal SalaryFor(string dept, int level)
    {
        // Entry band per department, scaled up by level.
        var baseBand = dept switch
        {
            "Engineering" => (95000m, 120000m),
            "Analytics" => (90000m, 115000m),
            "Operations" => (85000m, 110000m),
            "Finance" => (90000m, 115000m),
            "Product" => (95000m, 120000m),
            _ => (85000m, 110000m),
        };
        var lo = baseBand.Item1 * (1m + 0.22m * level);
        var hi = baseBand.Item2 * (1m + 0.22m * level);
        return Math.Round((decimal)_rng.NextDouble() * (hi - lo) + lo, 0, MidpointRounding.AwayFromZero);
    }

    private const int MaxLevel = 3;

    /// <summary>The HCM person number for an employee id (e.g. emp-1001 → 0001001).</summary>
    private static string PersonNumberFor(string empId)
    {
        var digits = empId.Replace("emp-", "");
        return digits.PadLeft(7, '0');
    }

    /// <summary>The HCM job (role family) for a department.</summary>
    private static string JobFor(string dept) => dept switch
    {
        "Engineering" => "Software Engineering",
        "Analytics" => "Data & Analytics",
        "Operations" => "Operations",
        "Finance" => "Finance",
        "Product" => "Product Management",
        _ => "General",
    };

    /// <summary>The pay grade for a level (G-1 entry … G-4 principal).</summary>
    private static string GradeFor(int level) => $"G-{level + 1}";

    private static string TitleFor(string dept, int level) => dept switch
    {
        "Engineering" => level switch
        {
            0 => "Software Engineer",
            1 => "Senior Software Engineer",
            2 => "Staff Software Engineer",
            _ => "Principal Engineer",
        },
        "Analytics" => level switch
        {
            0 => "Data Analyst",
            1 => "Data Scientist",
            2 => "Senior Data Scientist",
            _ => "Analytics Lead",
        },
        "Operations" => level switch
        {
            0 => "Operations Analyst",
            1 => "Operations Manager",
            2 => "Senior Operations Manager",
            _ => "Director of Operations",
        },
        "Finance" => level switch
        {
            0 => "Financial Analyst",
            1 => "Senior Financial Analyst",
            2 => "Finance Manager",
            _ => "Finance Director",
        },
        "Product" => level switch
        {
            0 => "Product Analyst",
            1 => "Product Manager",
            2 => "Senior Product Manager",
            _ => "Group Product Manager",
        },
        _ => level switch
        {
            0 => "Associate",
            1 => "Senior Associate",
            2 => "Manager",
            _ => "Director",
        },
    };

    private static readonly string[] Departments =
    {
        "Engineering", "Analytics", "Operations", "Finance", "Product",
    };

    private static readonly string[] LegalEmployers =
    {
        "Apex Industries LLC",
        "Apex Industries (UK) Ltd",
    };

    private static readonly string[] WorkLocations =
    {
        "Atlanta, GA",
        "Raleigh, NC",
        "Austin, TX",
        "Remote — US",
        "London, UK",
    };

    private static readonly string[] Supervisors =
    {
        "Dana Whitfield",
        "Marcus Chen",
        "Priya Raman",
        "Tom Okafor",
        "Elena Vasquez",
    };

    private static readonly string[] TerminationReasons =
    {
        "Voluntary Resignation",
        "Layoff — Restructuring",
        "End of Contract",
    };

    private static readonly string[] FirstNames =
    {
        "Katherine", "Margaret", "Clara", "Rosalind", "Hedy", "Barbara",
        "Dorothy", "Evelyn", "Grace", "Ada", "Lin", "Frances",
        "Jean", "Adele", "Radia", "Kathi", "Shafi", "Vint",
        "Guido", "Bjarne", "Anders", "Erik", "Tan", "Leslie",
    };

    private static readonly string[] LastNames =
    {
        "Johnson", "Hamilton", "Babbage", "Noether", "Lamarr", "Liskov",
        "Hertz", "Shaw", "Hopper", "Byron", "He", "Allen",
        "Ito", "Goldstine", "Perlman", "Fisher", "Goldwasser", "Cerf",
        "Rossum", "Stroustrup", "Hejlsberg", "Eriksson", "Lamport", "Lam",
    };

    /// <summary>A person currently active in the mock organization.</summary>
    private sealed class RosterEntry(
        string employeeId, string personNumber, string firstName, string lastName, string email,
        string legalEmployer, string positionId, string job, string grade, string jobTitle,
        string department, string workLocation, string supervisor, decimal baseSalary, int level)
    {
        public string EmployeeId { get; } = employeeId;
        public string PersonNumber { get; } = personNumber;
        public string FirstName { get; } = firstName;
        public string LastName { get; } = lastName;
        public string Email { get; } = email;
        public string LegalEmployer { get; } = legalEmployer;
        public string PositionId { get; set; } = positionId;
        public string Job { get; } = job;
        public string Grade { get; set; } = grade;
        public string JobTitle { get; set; } = jobTitle;
        public string Department { get; } = department;
        public string WorkLocation { get; } = workLocation;
        public string Supervisor { get; } = supervisor;
        public decimal BaseSalary { get; set; } = baseSalary;
        public int Level { get; set; } = level;
    }
}
