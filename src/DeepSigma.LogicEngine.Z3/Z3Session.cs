using MZ3 = Microsoft.Z3;

namespace DeepSigma.LogicEngine.Z3;

/// <summary>
/// Owns a Z3 <c>Context</c> + <c>Solver</c> for a single solve and disposes them
/// deterministically (Z3 contexts are not thread-safe, so one per solve). Applies an optional
/// wall-clock timeout and wires a <see cref="CancellationToken"/> to Z3's interrupt so a long
/// search can be cut short (it then reports <c>UNKNOWN</c>).
/// </summary>
internal sealed class Z3Session : IDisposable
{
    private readonly CancellationTokenRegistration _cancelRegistration;

    public MZ3.Context Context { get; }
    public MZ3.Solver Solver { get; }

    public Z3Session(TimeSpan? timeout, CancellationToken cancellationToken)
    {
        Context = new MZ3.Context();
        Solver = Context.MkSolver();

        if (timeout is { } t)
        {
            var ms = (uint)Math.Clamp(t.TotalMilliseconds, 0, uint.MaxValue);
            Solver.Set("timeout", ms);
        }

        _cancelRegistration = cancellationToken.CanBeCanceled
            ? cancellationToken.Register(static state => ((MZ3.Context)state!).Interrupt(), Context)
            : default;
    }

    public void Dispose()
    {
        _cancelRegistration.Dispose();
        Solver.Dispose();
        Context.Dispose();
    }
}
