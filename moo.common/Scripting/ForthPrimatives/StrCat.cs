using System;
using System.Collections.Generic;
using System.Linq;
using static ForthDatum;
using static ForthPrimativeResult;

public static class StrCat
{
    public static ForthPrimativeResult Execute(ForthPrimativeParameters parameters)
    {
        /*
        STRCAT ( s1 s2 -- s ) 

        Concatenates two strings s1 and s2 and pushes the result s = s1s2 onto the stack.
        */
        if (parameters.Stack.Count < 2)
            return new ForthPrimativeResult(ForthErrorResult.STACK_UNDERFLOW, "STRCAT requires two parameters");

        var n2 = parameters.Stack.Pop();
        if (n2.Type != DatumType.String)
            return new ForthPrimativeResult(ForthErrorResult.TYPE_MISMATCH, "STRCAT requires the second-to-top parameter on the stack to be a string");

        var n1 = parameters.Stack.Pop();
        if (n1.Type != DatumType.String)
            return new ForthPrimativeResult(ForthErrorResult.TYPE_MISMATCH, "STRCAT requires the top parameter on the stack to be a string");

        parameters.Stack.Push(new ForthDatum((string)n1.Value + (string)n2.Value));
        return ForthPrimativeResult.SUCCESS;
    }
}