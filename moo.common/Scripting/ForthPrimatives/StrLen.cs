using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static Dbref;
using static ForthDatum;
using static ForthPrimativeResult;

public static class StrLen
{
    public static ForthPrimativeResult Execute(ForthPrimativeParameters parameters)
    {
        /*
        STRLEN ( s -- i ) 

        Returns the length of string s.
        */
        if (parameters.Stack.Count < 1)
            return new ForthPrimativeResult(ForthErrorResult.STACK_UNDERFLOW, "STRLEN requires one parameter");

        var n1 = parameters.Stack.Pop();
        if (n1.Type != DatumType.String)
            return new ForthPrimativeResult(ForthErrorResult.TYPE_MISMATCH, "STRLEN requires the top parameter on the stack to be a string");

        parameters.Stack.Push(new ForthDatum(n1.Value == null ? 0 : ((string)n1.Value).Length));

        return ForthPrimativeResult.SUCCESS;
    }
}