using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static ForthDatum;
using static ForthPrimativeResult;
using static Property;

public static class IsExit
{
    public static async Task<ForthPrimativeResult> ExecuteAsync(ForthPrimativeParameters parameters)
    {
        /*
        EXIT? ( d -- i ) 

        Returns 1 if object d is an exit object, 0 if otherwise.
        */
        if (parameters.Stack.Count < 1)
            return new ForthPrimativeResult(ForthErrorResult.STACK_UNDERFLOW, "EXIT? requires one parameter");

        var sTarget = parameters.Stack.Pop();
        if (sTarget.Type != DatumType.DbRef)
            return new ForthPrimativeResult(ForthErrorResult.TYPE_MISMATCH, "EXIT? requires the top parameter on the stack to be a dbref");

        if (sTarget.UnwrapDbref() == Dbref.HOME)
        {
            parameters.Stack.Push(new ForthDatum(1));
            return ForthPrimativeResult.SUCCESS;
        }

        if (sTarget.UnwrapDbref().ToInt32() < 0)
        {
            parameters.Stack.Push(new ForthDatum(0));
            return ForthPrimativeResult.SUCCESS;
        }

        var targetResult = await ThingRepository.GetAsync<Thing>(sTarget.UnwrapDbref(), parameters.CancellationToken);
        if (!targetResult.isSuccess)
        {
            parameters.Stack.Push(new ForthDatum(0));
            return ForthPrimativeResult.SUCCESS;
        }

        if (targetResult.value.type != (int)Dbref.DbrefObjectType.Exit)
            parameters.Stack.Push(new ForthDatum(0));
        else
            parameters.Stack.Push(new ForthDatum(1));
        return ForthPrimativeResult.SUCCESS;
    }
}