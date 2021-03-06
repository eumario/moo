using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static ThingRepository;

public class NameBuiltIn : IRunnable
{
    public Tuple<bool, string> CanProcess(PlayerConnection connection, CommandResult command)
    {
        var verb = command.getVerb().ToLowerInvariant();
        if (verb == "@name" && command.hasDirectObject())
            return new Tuple<bool, string>(true, verb);
        return new Tuple<bool, string>(false, null);
    }

    public async Task<VerbResult> Process(PlayerConnection connection, CommandResult command, CancellationToken cancellationToken)
    {
        var str = command.getNonVerbPhrase();
        if (str == null || string.IsNullOrWhiteSpace(str))
            return new VerbResult(false, "@NAME <object>=<name> [<password>]\r\n\r\nSets the name field of <object> to <name>. A null <name> is illegal. You must supply <password> if renaming a player. Wizards can rename any player but still must include the password.");

        var parts = str.Split("=");
        if (parts.Length < 2)
            return new VerbResult(false, "@NAME <object>=<name> [<password>]\r\n\r\nSets the name field of <object> to <name>. A null <name> is illegal. You must supply <password> if renaming a player. Wizards can rename any player but still must include the password.");

        var name = parts[0].Trim();
        var predicate = parts[1].Trim();

        var targetDbref = await connection.FindThingForThisPlayerAsync(name, cancellationToken);
        if (targetDbref.Equals(Dbref.NOT_FOUND))
            return new VerbResult(false, $"Can't find '{name}' here");
        if (targetDbref.Equals(Dbref.AMBIGUOUS))
            return new VerbResult(false, $"Which one?");

        var targetLookup = await ThingRepository.GetAsync<Thing>(targetDbref, cancellationToken);
        if (!targetLookup.isSuccess)
        {
            await connection.sendOutput($"You can't seem to find that.  {targetLookup.reason}");
            return new VerbResult(false, "Target not found");
        }

        var target = targetLookup.value;
        var predicateParts = predicate.LastIndexOf(' ') == -1 ? new[] { predicate } : new string[] { predicate.Substring(0, predicate.LastIndexOf(' ')), predicate.Substring(predicate.LastIndexOf(' ') + 1) };
        if (predicateParts.Length < 1 || predicateParts.Length >2)
            return new VerbResult(false, "@NAME <object>=<name> [<password>]\r\n\r\nSets the name field of <object> to <name>. A null <name> is illegal. You must supply <password> if renaming a player. Wizards can rename any player but still must include the password.");

        // TODO: Validate password

        target.name = predicateParts[0];
        //await ThingRepository.FlushToDatabaseAsync(target, cancellationToken);

        return new VerbResult(true, $"Object {target.UnparseObject()} updated");
    }
}