using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

public static class ForthPreprocessor
{
    private static readonly Random random = new Random(Environment.TickCount);

    //   private static readonly Regex preTokensRegex = new Regex(@"\s*(?<token>(?:\$(?!\s)|PUBLIC)[^\$]+)(?!\$)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    //private static readonly Regex preTokensRegex = new Regex(@"(?<token>(?:lvar|public|\$(?:abort|author|def|define|echo|else|endif|ifdef|ifndef|include|libdef|lib-version|note|pubdef|endif|undef|version))(?:(?: +|\$lib\/|[\w\d\-\/\<\@\>\.\?\#\*\)\'\{\}])[\w\d\-\/\<\@\>\.\?\#\*\(\)\'\=\{\}_]+|\""[\w\d\-\/\<\@\>\.\?\#\*\(\)\'\$ \{\}_]*\"")*)+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex commentOnDefineLineRegex = new Regex(@"\([^\)]*\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex abortRegex = new Regex(@"^\s*(?:\$abort\s+(?<value>.*))$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex authorRegex = new Regex(@"^\s*(?:\$author\s+(?<value>.*))$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex defRegex = new Regex(@"^\s*\$def\s+(?<defName>[^\s]{1,50})(?:\s*$|\s+(?<defValue>[^\r\n]+)$)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    // private static readonly Regex defineRegex = new Regex(@"^\s*\$define\s+(?<defName>[^\s]{1,20})(?:\s*\$enddef$|\s+(?<defValue>[^\r\n]+)\s+\$enddef$)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex defineOpenRegex = new Regex(@"^\s*\$define\s+(?<defName>[^\s]{1,50})", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex defineCompleteRegex = new Regex(@"^\s*\$define\s+(?<defName>[^\s]{1,50})(?:\s*\$enddef$|\s+(?<defValue>(?:.|\r|\n)+)\s*\$enddef$)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex echoRegex = new Regex(@"^(?:\$echo\s+\""(?<value>[^\""]*)\"")", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ifdefRegex = new Regex(@"^\s*\$if(?<negate>n)?def\s+(?<defName>[^\s\=\<]{1,50})(?:\s*$|\s*(=|<)\s*(?<defValue>[^\r\n]{1,50})$)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex endifRegex = new Regex(@"^(\s*\$endif\s*)+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex includeRegex = new Regex(@"^\s*(?:\$include\s+(?:\$(?<libname>.+)|(?<dbref>\#\d+))?)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex libDefRegex = new Regex(@"(?:\$libdef\s+(?<function>[^\s\/]+))", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex libVersionRegex = new Regex(@"^\s*(?:\$lib-version\s+(?<value>\d+(?:.\d*)?))$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex noteRegex = new Regex(@"^\s*(?:\$note\s+(?<value>.*))$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex pubdefRegex = new Regex(@"^(?:\$pubdef\s+(?:(?<clear>\:)|(?<defName>[^:\/\s]+)(?:\s+(?<defValue>.+))?))$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex publicRegex = new Regex(@"(?<!^\()((?:PUBLIC\s+)(?<functionName>[^\s]{1,50}))+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex undefRegex = new Regex(@"^\s*\$undef\s+(?<defName>[^\s]{1,50})\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex versionRegex = new Regex(@"^\s*(?:\$version\s+(?<value>\d+(?:.\d*)?))$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex quotedStringPreRegex = new Regex(@"\""[^\r\n]*?(?<!\\)\""(?=\s)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static async Task<ForthPreprocessingResult> Preprocess(PlayerConnection connection, Script script, string program, CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        var defines = new Dictionary<string, string>() {
            { "PR_MODE", "0" },
            { "FG_MODE", "1" },
            { "BG_MODE", "2" },
            { "PREEMPT", "0 setmode" },
            { "FOREGROUND", "1 setmode" },
            { "BACKGROUND", "2 setmode" },
            { "EVENT_WAIT", "0 array_make event_waitfor" },
            { "NOTIFY_EXCEPT", "1 swap notify_exclude" },
            { "INSTRING", "tolower swap tolower swap instr" },
            { "RINSTRING", "tolower swap tolower swap rinstr" },
            { "}LIST", "} array_make" },
            { "}DICT", "} 2 / array_make_dict" },
            { "}TELL", "} array_make me @ 1 array_make array_notify" },
            { "}JOIN", "} array_make \"\" array_join" },
            { "}CAT", "} array_make array_interpret" },
            { "SETDESC", "\"_/de\" swap 0 addprop" },
            { "SETSUCC", "\"_/sc\" swap 0 addprop" },
            { "SETFAIL", "\"_/fl\" swap 0 addprop" },
            { "SETDROP", "\"_/dr\" swap 0 addprop" },
            { "SETOSUCC", "\"_/osc\" swap 0 addprop" },
            { "SETOFAIL", "\"_/ofl\" swap 0 addprop" },
            { "SETODROP", "\"_/odr\" swap 0 addprop" },
            { "SORTTYPE_CASEINSENS", "1" },
            { "SORTTYPE_DESCENDING", "2" },
            { "SORTTYPE_SHUFFLE", "4" },
            { "SORTTYPE_NOCASE_ASCEND", "1" },
            { "SORTTYPE_CASE_DESCEND", "2" },
            { "SORTTYPE_NOCASE_DESCEND", "3" },
            { "ARRAY_INTERSECT", "2 array_nintersect" },
            { "STRIP", "striplead striptail" }
        };
        var controlFlow = new Stack<ControlFlowMarker>();
        var verbosity = 0;

        var publicFunctionNames = new List<String>(20);

        var x = -1;
        var inMultiLineComment = false;

        var lines = program.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            x++;

            if (!inMultiLineComment && (line.TrimStart().StartsWith("$")
             || line.TrimStart().StartsWith("PUBLIC ", StringComparison.OrdinalIgnoreCase)))
            {
                // $define
                {
                    var defineMatch = defineCompleteRegex.Match(line);
                    var defineOpenMatch = defineOpenRegex.Match(line);

                    if (defineOpenMatch.Success && !defineMatch.Success)
                    {
                        // This is broken up across multiple lines.
                        var multiline = line;
                        var i2 = i;
                        do
                        {
                            i2++;
                            multiline += lines[i2];
                            defineMatch = defineCompleteRegex.Match(multiline);
                        } while (!defineCompleteRegex.IsMatch(multiline));
                        i = i2;
                    }

                    if (defineMatch.Success)
                    {
                        var key = defineMatch.Groups["defName"].Value.ToUpperInvariant();

                        if (!defineMatch.Groups["defValue"].Success)
                        {
                            if (!defines.ContainsKey(key))
                                defines.Add(key, null);
                        }
                        else
                        {
                            if (defines.ContainsKey(key))
                                defines[key] = defineMatch.Groups["defValue"].Value;
                            else
                                defines.Add(key, defineMatch.Groups["defValue"].Value);
                        }

                        continue;
                    }
                }

                var tokenHandled = false;

                //var tokens = preTokensRegex.Matches(line);

                // We have to treat lines as tokens, because someone could type $endif $endif on one line
                //foreach (System.Text.RegularExpressions.Match match in tokens)
                {
                again:
                    var token = line;//match.Groups["token"].Value;

                    // Comments on $/public def lines
                    {
                        var match = commentOnDefineLineRegex.Match(token);
                        if (match.Success)
                        {
                            tokenHandled = true;
                            line = line.Remove(match.Captures[0].Index, match.Captures[0].Length).Trim();
                            if (line.Length == 0)
                                continue;
                            goto again;
                        }
                    }

                    // PUBLIC
                    {
                        var publicMatch = publicRegex.Match(token);
                        if (publicMatch.Success)
                        {
                            publicFunctionNames.Add(publicMatch.Groups["functionName"].Value);
                            tokenHandled = true;
                            line = line.Remove(publicMatch.Captures[0].Index, publicMatch.Captures[0].Length).Trim();
                            if (line.Length == 0)
                                continue;
                            goto again;
                        }
                    }

                    // $abort
                    {
                        var abortMatch = abortRegex.Match(token);
                        if (abortMatch.Success)
                            return new ForthPreprocessingResult(abortMatch.Groups["value"].Value);
                    }

                    // $author (CONTINUE SINCE IT GOES UNTIL THE END OF THE LINE)
                    {
                        var match = authorRegex.Match(token);
                        if (match.Success)
                        {
                            script.SetPropertyPathValue("_author", Property.PropertyType.String, match.Groups["value"].Value);
                            tokenHandled = true;
                            line = line.Remove(match.Captures[0].Index, match.Captures[0].Length).Trim();
                            if (line.Length == 0)
                                continue;
                            goto again;
                        }
                    }

                    // $pubdef (CONTINUE SINCE IT GOES UNTIL THE END OF THE LINE)
                    {
                        var pubdefMatch = pubdefRegex.Match(token);
                        if (pubdefMatch.Success)
                        {
                            if (pubdefMatch.Groups["clear"].Success)
                            {
                                // Clears the _defs/ propdir on the program.
                                script.ClearPropertyPath($"_defs/");
                                tokenHandled = true;
                                continue;
                            }

                            var key = pubdefMatch.Groups["defName"].Value.ToUpperInvariant();

                            if (!pubdefMatch.Groups["defValue"].Success)
                            {
                                // Clears the _defs/<defname> prop on the prog.
                                script.ClearPropertyPath($"_defs/{key}");
                                tokenHandled = true;
                                continue;
                            }

                            var value = pubdefMatch.Groups["defValue"].Value;

                            if (key.StartsWith('\\'))
                            {
                                // Sets _defs/<defname> if not already set.
                                if ((await script.GetPropertyPathValueAsync($"_defs/{key}", cancellationToken)).Equals(default(Property)))
                                {
                                    script.SetPropertyPathValue($"_defs/{key}", Property.PropertyType.String, value);
                                }

                                tokenHandled = true;
                                continue;
                            }

                            // Sets _defs/<defname> prop to <rest_of_line>.
                            script.SetPropertyPathValue($"_defs/{key}", Property.PropertyType.String, value);
                            tokenHandled = true;
                            continue;
                        }
                    }

                    // $def
                    {
                        var defMatch = defRegex.Match(token);
                        if (defMatch.Success)
                        {
                            var key = defMatch.Groups["defName"].Value.ToUpperInvariant();

                            if (!defMatch.Groups["defValue"].Success)
                            {
                                if (!defines.ContainsKey(key))
                                    defines.Add(key, null);
                            }
                            else
                            {
                                if (defines.ContainsKey(key))
                                    defines[key] = defMatch.Groups["defValue"].Value;
                                else
                                    defines.Add(key, defMatch.Groups["defValue"].Value);
                            }

                            tokenHandled = true;
                            continue;
                        }
                    }

                    // $ifdef
                    {
                        var ifdefMatch = ifdefRegex.Match(token);
                        if (ifdefMatch.Success)
                        {
                            // I could be an 'if' inside a skipped branch.
                            if (controlFlow.Count > 0)
                            {
                                var controlCurrent = controlFlow.Peek();
                                if (controlCurrent.Element == ControlFlowElement.InIfAndSkip
                                 || controlCurrent.Element == ControlFlowElement.InElseAndSkip
                                 || controlCurrent.Element == ControlFlowElement.SkippedBranch
                                 || controlCurrent.Element == ControlFlowElement.SkipToAfterNextUntilOrRepeat)
                                {
                                    if (verbosity >= 2 && verbosity <= 3 && connection != null)
                                        await connection.sendOutput($"SKIPPED LINE: {line}");
                                    controlFlow.Push(new ControlFlowMarker(ControlFlowElement.SkippedBranch, x));
                                    tokenHandled = true;
                                    continue;
                                }
                            }

                            var isTrue = ifdefMatch.Groups["defValue"].Success
                            ? (defines.ContainsKey(ifdefMatch.Groups["defName"].Value.ToUpperInvariant()) && defines[ifdefMatch.Groups["defName"].Value.ToUpperInvariant()].Equals(ifdefMatch.Groups["defValue"].Value))
                            : (defines.ContainsKey(ifdefMatch.Groups["defName"].Value.ToUpperInvariant()));

                            var negate = ifdefMatch.Groups["negate"].Success;

                            if (isTrue ^ negate)
                                controlFlow.Push(new ControlFlowMarker(ControlFlowElement.InIfAndContinue, x));
                            else
                                controlFlow.Push(new ControlFlowMarker(ControlFlowElement.InIfAndSkip, x));
                            tokenHandled = true;
                            continue;
                        }
                    }

                    // $else
                    if (string.Compare("$else", token.Trim(), true) == 0)
                    {
                        // I could be an 'else' inside a skipped branch.
                        if (controlFlow.Count > 0)
                        {
                            var controlCurrent = controlFlow.Peek();
                            if (controlCurrent.Element == ControlFlowElement.SkippedBranch
                             || controlCurrent.Element == ControlFlowElement.SkipToAfterNextUntilOrRepeat)
                            {
                                if (verbosity >= 2 && verbosity <= 3 && connection != null)
                                    await connection.sendOutput($"SKIPPED LINE: {line}");
                                tokenHandled = true;
                                continue;
                            }
                        }

                        if (controlFlow.Count == 0)
                            return new ForthPreprocessingResult("$else encountered without preceding $ifdef/$ifndef");

                        var currentControl = controlFlow.Pop();
                        if (currentControl.Element == ControlFlowElement.InIfAndContinue)
                            controlFlow.Push(new ControlFlowMarker(ControlFlowElement.InElseAndSkip, x));
                        else
                            controlFlow.Push(new ControlFlowMarker(ControlFlowElement.InElseAndContinue, x));

                        tokenHandled = true;
                        continue;
                    }

                    // $endif
                    {
                        var match = endifRegex.Match(token);
                        if (match.Success)
                        {
                            // I could be an 'else' inside a skipped branch.
                            if (controlFlow.Count > 0)
                            {
                                var controlCurrent = controlFlow.Peek();
                                if (controlCurrent.Element == ControlFlowElement.SkippedBranch
                                 || controlCurrent.Element == ControlFlowElement.SkipToAfterNextUntilOrRepeat)
                                {
                                    if (verbosity >= 2 && verbosity <= 3 && connection != null)
                                        await connection.sendOutput($"SKIPPED LINE: {line}");
                                    // A skipped if will push a SkippedBranch, so we should pop it.
                                    controlFlow.Pop();
                                    continue;
                                }
                            }

                            if (controlFlow.Count == 0)
                                return new ForthPreprocessingResult("$endif encountered without preceding $ifdef/$ifndef");
                            controlFlow.Pop();

                            tokenHandled = true;
                            line = line.Remove(match.Captures[0].Index, match.Captures[0].Length).Trim();
                            if (line.Length == 0)
                                continue;
                            goto again;
                        }
                    }

                    if (controlFlow.Count > 0)
                    {
                        var controlCurrent = controlFlow.Peek();
                        if (controlCurrent.Element == ControlFlowElement.InIfAndSkip
                         || controlCurrent.Element == ControlFlowElement.InElseAndSkip
                         || controlCurrent.Element == ControlFlowElement.SkippedBranch
                         || controlCurrent.Element == ControlFlowElement.SkipToAfterNextUntilOrRepeat)
                        {
                            // Debug, print stack
                            if (verbosity >= 2 && verbosity <= 3 && connection != null)
                                await connection.sendOutput($"SKIPPED LINE: {line}");
                            tokenHandled = true;
                            continue;
                        }
                    }

                    // $echo
                    {
                        var echoMatch = echoRegex.Match(token);
                        if (echoMatch.Success)
                        {
                            if (connection != null)
                                await connection.sendOutput(echoMatch.Groups["value"].Value);
                            tokenHandled = true;
                            continue;
                        }
                    }

                    // $include
                    if (token.TrimStart().StartsWith("$include", StringComparison.OrdinalIgnoreCase))
                    {
                        var includeMatch = includeRegex.Match(token);
                        if (includeMatch.Success)
                        {
                            Dbref targetDbref;

                            if (includeMatch.Groups["libname"].Success)
                            {
                                // Find library
                                var aetherLookup = await ThingRepository.GetAsync<Room>(Dbref.AETHER, cancellationToken);
                                if (!aetherLookup.isSuccess)
                                    return new ForthPreprocessingResult($"Unable to load {Dbref.AETHER}: {aetherLookup.reason}");

                                var libname = includeMatch.Groups["libname"].Value;

                                var prop = await aetherLookup.value.GetPropertyPathValueAsync($"_reg/{libname}", cancellationToken);
                                if (prop.Equals(default(Property)))
                                    return new ForthPreprocessingResult($"Unable to load library {libname}");

                                targetDbref = (Dbref)prop.Value;
                            }
                            else
                            {
                                // Parse Dbref
                                if (!Dbref.TryParse(includeMatch.Groups["dbref"].Value, out Dbref parsedDbref))
                                    return new ForthPreprocessingResult($"UNABLE TO LOAD LIBRARY DBREF {includeMatch.Groups["dbref"].Value}");
                                if (!parsedDbref.IsValid())
                                    return new ForthPreprocessingResult($"UNABLE TO LOAD LIBRARY DBREF {includeMatch.Groups["dbref"].Value} (invalid resolution to {parsedDbref})");

                                targetDbref = parsedDbref;
                            }

                            var targetLookup = await ThingRepository.GetAsync(targetDbref, cancellationToken);
                            if (!targetLookup.isSuccess)
                                return new ForthPreprocessingResult($"Unable to load library: {targetLookup.reason}");

                            var target = targetLookup.value;
                            var defsProperty = await target.GetPropertyPathValueAsync("_defs/", cancellationToken);
                            var defsDirectory = (PropertyDirectory)defsProperty.Value;

                            if (defsDirectory == null)
                                continue;

                            foreach (var kvp in defsDirectory)
                            {
                                if (defines.ContainsKey(kvp.Key))
                                    defines[kvp.Key] = kvp.Value.value.ToString();
                                else
                                    defines.Add(kvp.Key, kvp.Value.value.ToString());
                            }

                            tokenHandled = true;
                            continue;
                        }
                    }

                    // $libdef
                    if (token.Contains("$libdef", StringComparison.OrdinalIgnoreCase))
                    {
                        var libDefMatch = libDefRegex.Match(token);
                        if (libDefMatch.Success)
                        {
                            var function = libDefMatch.Groups["function"].Value;
                            script.SetPropertyPathValue($"_defs/{function}", Property.PropertyType.String, $"{script.id} \"{function}\" call");
                            tokenHandled = true;
                            continue;
                        }
                    }

                    // $lib-version
                    {
                        var libVersionMatch = libVersionRegex.Match(token);
                        if (libVersionMatch.Success)
                        {
                            if (float.TryParse(libVersionMatch.Groups["value"].Value, out float version))
                                script.SetPropertyPathValue("_lib-version", version);
                            tokenHandled = true;
                            continue;
                        }
                    }

                    // $note
                    {
                        var noteMatch = noteRegex.Match(token);
                        if (noteMatch.Success)
                        {
                            script.SetPropertyPathValue("_note", Property.PropertyType.String, noteMatch.Groups["value"].Value);
                            tokenHandled = true;
                            continue;
                        }
                    }

                    // $undef
                    {
                        var undefMatch = undefRegex.Match(token);
                        if (undefMatch.Success)
                        {
                            if (defines.ContainsKey(undefMatch.Groups["defName"].Value.ToUpperInvariant()))
                                defines.Remove(undefMatch.Groups["defName"].Value.ToUpperInvariant());
                            tokenHandled = true;
                            continue;
                        }
                    }

                    // $version
                    {
                        var versionMatch = versionRegex.Match(token);
                        if (versionMatch.Success)
                        {
                            if (float.TryParse(versionMatch.Groups["value"].Value, out float version))
                                script.SetPropertyPathValue("_version", version);
                            tokenHandled = true;
                            continue;
                        }
                    }

                    if (connection != null)
                        await connection.sendOutput($"UNHANDLED PREPROCESSOR TOKEN: {token}");
                }

                if (tokenHandled)
                    continue;

                if (connection != null)
                    await connection.sendOutput($"UNHANDLED PREPROCESSOR LINE: {line}");
            }
            else
            {
                var line2 = line;

                // If we open a multi-line comment, then continue until we close it.
                if (inMultiLineComment && line2.IndexOf(')') != -1)
                {
                    inMultiLineComment = false;
                    line2 = line2.Substring(line2.IndexOf(')'));
                }
                else if (inMultiLineComment || line2.TrimStart().StartsWith('(') && line2.IndexOf(')') == -1)
                {
                    inMultiLineComment = true;
                    continue;
                }

                // We want to ensure any replaces do NOT happen in quoted strings
                var holdingPen = new Dictionary<string, string>();
                while (quotedStringPreRegex.IsMatch(line2))
                {
                    var match = quotedStringPreRegex.Match(line2);
                    var key = RandomString(match.Length);
                    holdingPen.Add(key, match.Value);
                    line2 = line2.Remove(match.Index, match.Length).Insert(match.Index, key);
                }

                // Strip comments
                if (line2.IndexOf('(') > -1)
                    line2 = Regex.Replace(line2, @"^\([^\)]*\)|\([^\r\n]*$|\([^\)]*\)", "");

                foreach (var define in defines.Where(d => d.Value != null))
                    line2 = Regex.Replace(line2, @"(?<=\s|^)" + Regex.Escape(define.Key) + @"(?=\s|$)", define.Value, RegexOptions.IgnoreCase);

                foreach (var hold in holdingPen)
                    line2 = line2.Replace(hold.Key, hold.Value);

                if (verbosity > 0 && verbosity <= 3 && line.CompareTo(line2) != 0 && connection != null)
                    await connection.sendOutput($"XFORM \"{line}\" into \"{line2}\"");
                else if (verbosity == 4 && connection != null)
                    await connection.sendOutput($"PRE: {line2}");

                sb.AppendLine(line2);
            }
        }

        return new ForthPreprocessingResult(sb.ToString(), publicFunctionNames, null);
    }

    public static string RandomString(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*_+=-";
        return new string(Enumerable.Repeat(chars, length)
          .Select(s => s[random.Next(s.Length)]).ToArray());
    }
}