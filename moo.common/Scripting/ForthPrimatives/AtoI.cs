using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static Dbref;
using static ForthDatum;
using static ForthPrimativeResult;

public static class AtoI
{
    public static ForthPrimativeResult Execute(ForthPrimativeParameters parameters)
    {
        /*
        ATOI ( s -- i ) 

        Turns string s into integer i. If s is not a string, then 0 is pushed onto the stack.
        */
        if (parameters.Stack.Count < 1)
            return new ForthPrimativeResult(ForthErrorResult.STACK_UNDERFLOW, "ATOI requires one parameter");

        var n1 = parameters.Stack.Pop();
        if (n1.Type != DatumType.String)
            parameters.Stack.Push(new ForthDatum(0));
        else
        {
            int i;
            if (int.TryParse((string)n1.Value, out i))
                parameters.Stack.Push(new ForthDatum(i));
            else
                parameters.Stack.Push(new ForthDatum(0));
        }

        return ForthPrimativeResult.SUCCESS;
    }
}