using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static ForthDatum;
using static ForthProgramResult;

public class ForthProcess
{
    public enum MultitaskingMode
    {
        Preempt = 0,
        Foreground = 1,
        Background = 2
    }

    public enum ProcessState
    {
        Initializing,
        Parsing,
        Running,
        WaitingForInput,
        Paused,
        Preempted,
        Complete
    }

    private static int nextPid = 1;
    private readonly static object nextPidLock = new Object();

    private readonly int processId;
    private MultitaskingMode mode;
    public ProcessState State;
    private IEnumerable<ForthWord> words;
    private readonly Stack<ForthDatum> stack = new Stack<ForthDatum>();
    // Variables that came from a caller program
    private readonly Dictionary<string, ForthVariable> outerScopeVariables = new Dictionary<string, ForthVariable>();
    // Variables local to my process context, which I could pass on to programs I call and all my words can see ($DEF, LVAR)
    private readonly ConcurrentDictionary<string, ForthVariable> programLocalVariables = new ConcurrentDictionary<string, ForthVariable>();

    private readonly Server server;
    private readonly Dbref scriptId;
    private readonly PlayerConnection connection;
    private readonly String scopeId;
    private readonly String outerScopeId;
    private bool hasRan;

    public Server Server => server;
    public MultitaskingMode Mode => mode;

    public ForthProcess(
        Server server,
        Dbref scriptId,
        PlayerConnection connection,
        string outerScopeId = null,
        Dictionary<string, ForthVariable> outerScopeVariables = null)
    {
        this.processId = GetNextPid();
        this.State = ProcessState.Initializing;
        this.mode = MultitaskingMode.Foreground;
        this.server = server;
        this.scriptId = scriptId;
        this.connection = connection;
        this.scopeId = Guid.NewGuid().ToString();

        if (outerScopeId != null)
        {
            this.outerScopeId = outerScopeId;

            if (outerScopeVariables != null)
            {
                foreach (var kvp in outerScopeVariables)
                    this.outerScopeVariables.Add(kvp.Key, kvp.Value);
            }
        }
    }

    public static int GetNextPid()
    {
        lock (nextPidLock)
        {
            var pid = Interlocked.Increment(ref nextPid);
            if (pid < Int32.MaxValue)
                return pid;
            Interlocked.Exchange(ref nextPid, 1);
            return 1;
        }
    }

    public ConcurrentDictionary<string, ForthVariable> GetProgramLocalVariables()
    {
        return this.programLocalVariables;
    }

    public void SetProgramLocalVariable(string name, ForthVariable value)
    {
        if (!programLocalVariables.TryAdd(name, value))
        {
            programLocalVariables[name] = value;
        }
    }

    public bool HasWord(string wordName) => this.words.Any(w => string.Compare(w.name, wordName, true) == 0);

    public async Task<ForthProgramResult> RunWordAsync(string wordName, Dbref trigger, string command, Dbref? lastListItem, CancellationToken cancellationToken)
    {
        return await this.words.Single(w => string.Compare(w.name, wordName, true) == 0).RunAsync(this, stack, connection, trigger, command, lastListItem, cancellationToken);
    }

    public async Task NotifyAsync(Dbref target, string message)
    {
        await server.NotifyAsync(target, message);
    }

    public async Task NotifyRoomAsync(Dbref target, string message, List<Dbref> exclude = null)
    {
        await server.NotifyRoomAsync(target, message, exclude);
    }

    public async Task<ForthProgramResult> RunAsync(
        IEnumerable<ForthWord> words,
        Dbref trigger,
        string command,
        object[] args,
        CancellationToken cancellationToken)
    {
        if (hasRan)
        {
            return new ForthProgramResult(ForthProgramErrorResult.INTERNAL_ERROR, $"Execution scope {scopeId} tried to run twice.");
        }
        hasRan = true;

        this.words = words;

        // Execute the last word.
        if (args != null && args.Length > 0 && args[0] != null)
        {
            if (args[0].GetType() == typeof(string))
                stack.Push(new ForthDatum((string)args[0]));
        }

        this.State = ProcessState.Running;
        var result = await words.Last().RunAsync(this, stack, connection, trigger, command, null, cancellationToken);
        this.State = ProcessState.Complete;
        return result;
    }
}