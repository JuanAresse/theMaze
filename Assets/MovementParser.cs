using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

// Parser de comandos de Character: MoveUp(), MoveDown(), MoveLeft(), MoveRight(), Shoot(), Wait()
// Soporta Repeat(n){ ... } anidado
public static class MovementParser
{
    public static List<Action> Parse(string script, Character character)
    {
        var tokens = Tokenize(script);
        int index = 0;
        return ParseSequence(tokens, ref index, character);
    }

    private static List<string> Tokenize(string script)
    {
        string s = script.Replace("\r", "").Replace("\n", "").Trim();
        var tokens = new List<string>();
        var pattern = @"Repeat\(\d+\)\{|\}|\w+\(\)|;"; // reconoce Repeat(n){ , } , Func()
        var matches = Regex.Matches(s, pattern);
        foreach (Match m in matches)
        {
            if (m.Value == ";") continue;
            tokens.Add(m.Value);
        }
        return tokens;
    }

    private static List<Action> ParseSequence(List<string> tokens, ref int i, Character character)
    {
        var result = new List<Action>();
        while (i < tokens.Count)
        {
            var t = tokens[i];

            if (t == "}")
            {
                i++;
                break;
            }

            if (t.StartsWith("Repeat"))
            {
                var m = Regex.Match(t, @"Repeat\((\d+)\)\{");
                int count = 1;
                if (m.Success) int.TryParse(m.Groups[1].Value, out count);
                i++; // avanzar después de Repeat(...)
                var inner = ParseSequence(tokens, ref i, character); // consume hasta matching '}'
                for (int k = 0; k < count; k++) result.AddRange(inner);
                continue;
            }

            var action = TokenToAction(t, character);
            if (action != null) result.Add(action);

            i++;
        }
        return result;
    }

    private static Action TokenToAction(string token, Character character)
    {
        switch (token)
        {
            case "MoveUp()": return character.MoveUp;
            case "MoveDown()": return character.MoveDown;
            case "MoveLeft()": return character.MoveLeft;
            case "MoveRight()": return character.MoveRight;
            case "Shoot()": return character.Shoot;
            case "Wait()": return () => { };
            default:
                Debug.LogWarning("Comando desconocido: " + token);
                return null;
        }
    }
}
