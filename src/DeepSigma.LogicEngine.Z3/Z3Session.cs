using MZ3 = Microsoft.Z3;

namespace DeepSigma.LogicEngine.Z3;

/// <summary>
/// Owns a Z3 <c>Context</c> for a single solve and disposes it (and any solver/optimizer created
/// from it) deterministically — Z3 contexts are not thread-safe, so one per solve. Applies an
/// optional wall-clock timeout to whichever engine is used, and wires a
/// <see cref="CancellationToken"/> to Z3's interrupt so a long search can be cut short (it then
/// reports <c>UNKNOWN</c>). Both the plain <see cref="Solver"/> and the optimizing
/// <see cref="Optimize"/> are created lazily, so this single type backs every Z3 facade.
/// </summary>
internal sealed class Z3Session : IDisposable
{
    private readonly uint? _timeoutMs;
    private readonly CancellationTokenRegistration _cancelRegistration;
    private MZ3.Solver? _solver;
    private MZ3.Optimize? _optimize;

    public MZ3.Context Context { get; }

    public Z3Session(TimeSpan? timeout, CancellationToken cancellationToken)
    {
        Context = new MZ3.Context();
        _timeoutMs = timeout is { } t ? (uint)Math.Clamp(t.TotalMilliseconds, 0, uint.MaxValue) : null;
        _cancelRegistration = cancellationToken.CanBeCanceled
            ? cancellationToken.Register(static state => ((MZ3.Context)state!).Interrupt(), Context)
            : default;
    }

    /// <summary>The plain solver for this session (created on first use, with the timeout applied).</summary>
    public MZ3.Solver Solver
    {
        get
        {
            if (_solver is null)
            {
                _solver = Context.MkSolver();
                if (_timeoutMs is { } ms)
                {
                    _solver.Set("timeout", ms);
                }
            }
            return _solver;
        }
    }

    /// <summary>The optimizing solver (νZ) for MaxSAT (created on first use, with the timeout applied).</summary>
    public MZ3.Optimize Optimize
    {
        get
        {
            if (_optimize is null)
            {
                _optimize = Context.MkOptimize();
                if (_timeoutMs is { } ms)
                {
                    _optimize.Set("timeout", ms);
                }
            }
            return _optimize;
        }
    }

    public void Dispose()
    {
        _cancelRegistration.Dispose();
        _solver?.Dispose();
        _optimize?.Dispose();
        Context.Dispose();
    }
}
