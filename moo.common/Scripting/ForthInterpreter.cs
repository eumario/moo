using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

public class ForthInterpreter
{
    private static readonly ConcurrentBag<ForthProcess> processes = new ConcurrentBag<ForthProcess>();

    private readonly string program;
    private readonly List<ForthWord> words = new List<ForthWord>();
    private readonly List<string> programLocalVariableDeclarations = new List<string>();

    public ForthInterpreter(string program)
    {
        this.program = program;
    }

    private ForthParseResult Parse()
    {
        // Parse the program onto the stack
        var lines = program.Split(new string[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        Console.Out.WriteLine($"Parsed {lines.Length} lines");

        var regexWordParsing = new Regex(@"(?:(?<comment>\([^\)]*\))|(?:lvar\s+(?<lvar>\w+))|(?<word>\:\s*(?<wordName>[\w\-]+)\s*(?<wordBody>[^;]+)\;))");
        var regexDatumParsing = new Regex(@"(?:(?<comment>\([^\)]*\))|(?:""(?<string>[^""]*)"")|(?<float>\-?(?:\d+\.\d*|\d*\.\d+))|(?<int>\-?\d+)|(?<dbref>#\d+)|(?<prim>[\w\.\-\+\*\/%\?!><=@;:{}]+))", RegexOptions.Compiled);

        //int lineRatchet = 0;
        foreach (Match wordMatch in regexWordParsing.Matches(program))
        {
            /*while (lineRatchet < lines.Length) {
                if (lines[lineRatchet].IndexOf(wordMatch.Value) > -1)
                    break;
                lineRatchet++;
            }*/

            if (!string.IsNullOrWhiteSpace(wordMatch.Groups["comment"].Value))
                continue;

            if (!string.IsNullOrWhiteSpace(wordMatch.Groups["lvar"].Value))
            {
                programLocalVariableDeclarations.Add(wordMatch.Groups["lvar"].Value);
            }

            if (!string.IsNullOrWhiteSpace(wordMatch.Groups["word"].Value))
            {
                // Cut the word up and preserve line numbers to aid in debugging words
                var wordName = wordMatch.Groups["wordName"].Value;
                var wordBody = wordMatch.Groups["wordBody"].Value;

                var wordBodySplit = wordBody.Split(new string[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                var programLineNumbersAndDatum = new Dictionary<int, ForthDatum[]>();
                for (int i = 0; i < wordBodySplit.Length; i++)
                {
                    var lineData = new List<ForthDatum>();
                    var matches = regexDatumParsing.Matches(wordBodySplit[i]);
                    foreach (Match match in matches)
                    {
                        foreach (Group group in match.Groups.Skip(1).Where(g => g.Success))
                        {
                            switch (group.Name)
                            {
                                case "comment":
                                    {
                                        continue;
                                    }
                                case "string":
                                    {
                                        lineData.Add(new ForthDatum(group.Value, ForthDatum.DatumType.String));
                                        continue;
                                    }
                                case "float":
                                    {
                                        lineData.Add(new ForthDatum(float.Parse(group.Value), ForthDatum.DatumType.Float));
                                        continue;
                                    }
                                case "int":
                                    {
                                        lineData.Add(new ForthDatum(int.Parse(group.Value), ForthDatum.DatumType.Integer));
                                        continue;
                                    }
                                case "dbref":
                                    {
                                        lineData.Add(new ForthDatum(group.Value, ForthDatum.DatumType.DbRef));
                                        continue;
                                    }
                                case "prim":
                                    {
                                        if (ForthWord.GetPrimatives().Any(s => string.Compare(s, group.Value, true) == 0))
                                            lineData.Add(new ForthDatum(group.Value, ForthDatum.DatumType.Primitive));
                                        else // Could be a variable name
                                            lineData.Add(new ForthDatum(group.Value, ForthDatum.DatumType.Unknown));
                                        continue;
                                    }
                            }

                            return new ForthParseResult(false, $"Unable to parse line in word {wordName}: {match.Value}.  Full line: {wordBodySplit[i]}");
                        }

                    }

                    programLineNumbersAndDatum.Add(i + 1, lineData.ToArray());
                }
                var word = new ForthWord(wordName, programLineNumbersAndDatum);
                words.Add(word);
                continue;
            }
        }

        return new ForthParseResult(true, $"Parsed {programLocalVariableDeclarations.Count} program local variables and {words.Count} words");
    }

    public async Task<ForthProgramResult> SpawnAsync(
        Dbref scriptId,
        Player player,
        Dbref trigger,
        string command,
        object[] args,
        CancellationToken cancellationToken)
    {
        if (words.Count == 0)
        {
            var result = Parse();

            if (!result.isSuccessful)
                return new ForthProgramResult(ForthProgramResult.ForthProgramErrorResult.SYNTAX_ERROR, result.reason);
        }

        var process = new ForthProcess(scriptId, words, player);
        foreach (var lvar in programLocalVariableDeclarations)
            process.SetProgramLocalVariable(lvar, null);
        processes.Add(process);

        var programResult = await process.RunAsync(trigger, command, args, cancellationToken);
        return programResult;
    }
}