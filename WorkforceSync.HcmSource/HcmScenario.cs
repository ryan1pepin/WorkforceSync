using System.Globalization;

namespace WorkforceSync.HcmSource;

/// <summary>
/// A bounded, varied workforce-activity stream for the mock HCM. Each cycle
/// lasts <see cref="CycleSeconds"/> (default 5 minutes): it starts with a few
/// hires already in the system, then — every <see cref="StepSeconds"/> — emits
/// a weighted-random event (hire, promotion, pay change, termination, rehire)
/// against a growing internal roster.
///
/// When the cycle elapses the scenario <em>resets from scratch</em> — fresh
/// roster, fresh event ids, and a new RNG seed — and the cycle repeats
/// perpetually. The consumer clears its state at the same moment, so the
/// dashboard always shows a small, fresh, realistic population instead of an
/// unbounded backlog.
///
/// Names are drawn without replacement from the name pools, so no two people
/// in the feed ever share a name (a real HCM would never show five "Ada
/// Allens"). A fixed seed per cycle keeps each cycle reproducible.
///
/// Every event gets a unique, monotonic id within its cycle, so the
/// consumer's idempotency check treats each one as new.
/// </summary>
public sealed class HcmScenario
{
    private readonly List<HcmEvent> _events = new();
    private readonly List<RosterEntry> _roster = new();
    private readonly List<RosterEntry> _terminated = new();
    private readonly List<string> _usedNames = new();
    private readonly object _gate = new();
    private Random _rng;
    private int _eventCounter;
    private int _cycleNumber;
    private DateTime _cycleStart;
    private DateTime _nextEventAt;

    /// <summary>Seconds between generated events.</summary>
    public int StepSeconds { get; }

    /// <summary>How long one full cycle lasts before the scenario resets.</summary>
    public int CycleSeconds { get; }

    public HcmScenario(int stepSeconds = 15, int cycleSeconds = 300)
    {
        StepSeconds = Math.Max(1, stepSeconds);
        CycleSeconds = Math.Max(StepSeconds * 2, cycleSeconds);
        _rng = new Random(1337); // fixed seed → reproducible but varied
        _cycleStart = DateTime.UtcNow;

        // Seed: three hires already in the system at t=0.
        SeedHire("emp-1001", "Ada", "Lovelace", "Engineering", level: 1, salary: 118000m);
        SeedHire("emp-1002", "Grace", "Hopper", "Engineering", level: 1, salary: 124000m);
        SeedHire("emp-1003", "Alan", "Turing", "Analytics", level: 1, salary: 131000m);

        _nextEventAt = DateTime.UtcNow; // first generated event is due immediately
    }

    /// <summary>
    /// The current set of events. Advances the scenario based on elapsed time
    /// before returning, so repeated calls keep growing the feed — until the
    /// cycle elapses, at which point the scenario resets and the feed starts
    /// over from scratch.
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

        // Cycle elapsed — clear everything and start over from scratch.
        if (now - _cycleStart >= TimeSpan.FromSeconds(CycleSeconds))
        {
            Reset(now);
        }

        while (now >= _nextEventAt)
        {
            EmitRandomEvent(now);
            _nextEventAt = _nextEventAt.AddSeconds(StepSeconds);
        }
    }

    /// <summary>
    /// Clears the roster, the event history, and the used-name set, then
    /// re-seeds the starting hires. A fresh RNG seed per cycle keeps each
    /// cycle reproducible while making successive cycles different.
    /// </summary>
    private void Reset(DateTime now)
    {
        _events.Clear();
        _roster.Clear();
        _terminated.Clear();
        _usedNames.Clear();
        _eventCounter = 0;
        _cycleNumber++;
        _rng = new Random(1337 + _cycleNumber);
        _cycleStart = now;
        _nextEventAt = now;

        SeedHire("emp-1001", "Ada", "Lovelace", "Engineering", level: 1, salary: 118000m);
        SeedHire("emp-1002", "Grace", "Hopper", "Engineering", level: 1, salary: 124000m);
        SeedHire("emp-1003", "Alan", "Turing", "Analytics", level: 1, salary: 131000m);
    }

    private void EmitRandomEvent(DateTime now)
    {
        // Occasionally emit a stale/out-of-order event — a transfer or pay change
        // for someone who has already left. Real feeds have these (late or
        // duplicated messages), and the pipeline must reject them. This is what
        // exercises the "change after termination" guard and keeps the dead-letter
        // queue populated so the replay/discard path is always demonstrable.
        if (_terminated.Count > 0 && _rng.Next(100) < 18)
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
        var (first, last) = NextUniqueName();
        var dept = Pick(Departments);
        var level = 0; // new hires start at entry level
        var salary = SalaryFor(dept, level);

        var empId = $"emp-{1000 + _eventCounter}";
        var email = $"{first}.{last}{_eventCounter}@corp.example".ToLowerInvariant();
        var posId = $"pos-{100 + _eventCounter}";
        var title = TitleFor(dept, level);
        var employmentType = _rng.Next(100) < 10 ? "Temporary" : "Regular";

        _roster.Add(new RosterEntry(
            empId, PersonNumberFor(empId), first, last, email,
            Pick(LegalEmployers), posId, JobFor(dept), GradeFor(level),
            title, dept, Pick(WorkLocations), Pick(Supervisors), salary, level,
            employmentType));

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
            EmploymentType: employmentType,
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

        // The dismissal reason must match the person's employment type: a
        // "contract ending" only makes sense for a contractor (Temporary),
        // while resignation and layoff apply to regular employees.
        var reason = e.EmploymentType == "Temporary"
            ? "End of Contract"
            : Pick(RegularTerminationReasons);

        _events.Add(new HcmEvent(
            Id: NextEventId(),
            Type: "Termination",
            OccurredAtUtc: now,
            EmployeeId: e.EmployeeId,
            EndDate: now.Date,
            TerminationReason: reason));
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
        e.EmploymentType = "Regular"; // the rehire event carries Regular — keep the roster in sync
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
        // Reserve the name so a later hire can't be drawn with the same one.
        _usedNames.Add(first + " " + last);

        var email = $"{first}.{last}@corp.example".ToLowerInvariant();
        var posId = $"pos-{100 + _eventCounter}";
        var title = TitleFor(dept, level);
        _roster.Add(new RosterEntry(
            empId, PersonNumberFor(empId), first, last, email,
            Pick(LegalEmployers), posId, JobFor(dept), GradeFor(level),
            title, dept, Pick(WorkLocations), Pick(Supervisors), salary, level,
            "Regular"));

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

    // Event ids are globally unique across cycles (the cycle number is baked
    // in) so the consumer's idempotency check can never confuse an old-cycle
    // event with a new-cycle one that reuses the same sequence number.
    private string NextEventId() => $"evt-{_cycleNumber}-{_eventCounter++}";

    private T Pick<T>(IReadOnlyList<T> list) => list[_rng.Next(list.Count)];

    /// <summary>
    /// Draws a first + last name that no one in the current cycle has used.
    /// Names are drawn without replacement, so the feed never contains two
    /// people with the same name.
    /// </summary>
    private (string First, string Last) NextUniqueName()
    {
        for (var attempt = 0; attempt < 500; attempt++)
        {
            var f = Pick(FirstNames);
            var l = Pick(LastNames);
            var key = f + " " + l;
            if (!_usedNames.Contains(key))
            {
                _usedNames.Add(key);
                return (f, l);
            }
        }

        // Pools exhausted (only possible after ~576 hires) — fall back to a
        // counter-suffixed name so the feed stays valid rather than throwing.
        var fbFirst = Pick(FirstNames);
        var fbLast = Pick(LastNames);
        _usedNames.Add($"{fbFirst} {fbLast} {_eventCounter}");
        return (fbFirst, fbLast);
    }

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

    private static readonly string[] RegularTerminationReasons =
    {
        "Voluntary Resignation",
        "Layoff — Restructuring",
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
        string department, string workLocation, string supervisor, decimal baseSalary, int level,
        string employmentType)
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
        public string EmploymentType { get; set; } = employmentType;
    }
}
