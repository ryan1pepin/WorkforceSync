using WorkforceSync.HcmSource;

namespace WorkforceSync.Core.Tests;

/// <summary>
/// Regression tests for the mock-HCM scenario's bounded-cycle behavior:
/// the feed must never contain two people with the same name, and it must
/// reset to a fresh cycle (dropping its event count) once the cycle elapses.
/// </summary>
public class HcmScenarioTests
{
    [Fact]
    public void Hires_NeverReuseAName_WithinACycle()
    {
        // A short, fast cycle so we get a good number of hires quickly.
        var scenario = new HcmScenario(stepSeconds: 1, cycleSeconds: 30);

        // Pull the feed a few times to let it grow (each call advances time).
        var events = scenario.CurrentEvents;
        for (var i = 0; i < 3; i++)
        {
            Thread.Sleep(1200);
            events = scenario.CurrentEvents;
        }

        var hireNames = events
            .Where(e => e.Type == "Hire" && e.FirstName is not null && e.LastName is not null)
            .Select(e => e.FirstName + " " + e.LastName)
            .ToList();

        // There must be at least a handful of hires to make the check meaningful.
        Assert.True(hireNames.Count >= 3, $"expected >=3 hires, got {hireNames.Count}");

        // No two hires may share a name (the "five Ada Allens" bug).
        var duplicates = hireNames
            .GroupBy(n => n)
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key} x{g.Count()}")
            .ToList();

        Assert.Empty(duplicates);
    }

    [Fact]
    public void Cycle_Resets_AndStaysBounded()
    {
        // Tiny cycle (2s) so several resets happen within the test window.
        var scenario = new HcmScenario(stepSeconds: 1, cycleSeconds: 2);

        // One cycle can hold at most (cycleSeconds / stepSeconds) generated
        // events plus the 3 seed hires. Sample the count over several cycles
        // and assert it never grows past that bound — i.e. the feed resets
        // instead of accumulating without limit.
        var bound = (2 / 1) + 3 + 1; // +1 slack for a boundary event
        var maxSeen = 0;
        for (var i = 0; i < 12; i++)
        {
            maxSeen = Math.Max(maxSeen, scenario.CurrentEvents.Count);
            Thread.Sleep(500);
        }

        Assert.True(maxSeen <= bound,
            $"feed should stay bounded by one cycle (<= {bound}), but reached {maxSeen}");
    }

    [Fact]
    public void EventIds_AreUniqueAcrossCycles()
    {
        var scenario = new HcmScenario(stepSeconds: 1, cycleSeconds: 2);

        var cycleOne = scenario.CurrentEvents.Select(e => e.Id).ToHashSet();
        Thread.Sleep(3500);
        var cycleTwo = scenario.CurrentEvents.Select(e => e.Id).ToHashSet();

        // The two cycles must not share any event id — otherwise the consumer's
        // idempotency check would skip a new-cycle event as a redelivery.
        var overlap = cycleOne.Intersect(cycleTwo).ToList();
        Assert.Empty(overlap);
    }

    [Fact]
    public void ContractEndings_OnlyHappenToContractors()
    {
        // A "contract ending" dismissal must only ever be attached to a
        // contractor (hired as Temporary). Regular employees leave by
        // resignation or layoff — never "End of Contract".
        var scenario = new HcmScenario(stepSeconds: 1, cycleSeconds: 60);

        var events = scenario.CurrentEvents;
        for (var i = 0; i < 8; i++)
        {
            Thread.Sleep(1200);
            events = scenario.CurrentEvents;
        }

        // Latest employment type per employee: a rehire event overrides the
        // original hire (the feed always carries the current type).
        var employmentTypeByEmployee = new Dictionary<string, string>();
        foreach (var e in events.Where(e => e.Type is "Hire" or "Rehire" && e.EmploymentType is not null))
        {
            employmentTypeByEmployee[e.EmployeeId] = e.EmploymentType!;
        }

        var contractEndings = events
            .Where(e => e.Type == "Termination" && e.TerminationReason == "End of Contract")
            .ToList();

        foreach (var t in contractEndings)
        {
            Assert.True(
                employmentTypeByEmployee.TryGetValue(t.EmployeeId, out var type) && type == "Temporary",
                $"employee {t.EmployeeId} was dismissed with 'End of Contract' but was not hired as a contractor");
        }
    }
}
