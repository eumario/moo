using System;
using System.Collections.Generic;
using System.Linq;
using static ForthDatum;
using static ForthProgramResult;

public static class Date
{
    public static ForthProgramResult Execute(Stack<ForthDatum> stack)
    {
        /*
        DATE ( -- i i i) 

        Returns the monthday, month, and year. ie: if it were February 6, 1992, date would return 6 2 1992 as three integers on the stack.
        */
        var now = DateTime.Now;

        stack.Push(new ForthDatum(now.Day));
        stack.Push(new ForthDatum(now.Month));
        stack.Push(new ForthDatum(now.Year));

        return default(ForthProgramResult);
    }
}