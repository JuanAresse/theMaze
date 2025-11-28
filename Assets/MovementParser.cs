using System;
using System.Collections.Generic;
using UnityEngine;

/*
GameObject: MovementParser (utility static class, no GameObject)
Descripción: Parsea scripts de texto en acciones ejecutables por Character (Move, Shoot, Repeat, Wait).
*/

public static class MovementParser
{
    // Parse: convierte un script en lista de acciones para un Character.
    // Parámetros: script - cadena con comandos; character - instancia objetivo.
    public static List<Action> Parse(string script, Character character)
    {
        var tokens = Tokenize(script);
        int index = 0;
        return ParseSequence(tokens, ref index, character);
    }

    // Tokenize: convierte el script en tokens manejando paréntesis y llaves.
    private static List<string> Tokenize(string script)
    {
        var s = (script ?? "").Replace("\r", "").Replace("\n", "");
        var tokens = new List<string>();
        int i = 0;
        while (i < s.Length)
        {
            char c = s[i];

            if (char.IsWhiteSpace(c)) { i++; continue; }

            if (c == ';') { i++; continue; }
            if (c == '}') { tokens.Add("}"); i++; continue; }

            if (char.IsLetterOrDigit(c) || c == '_')
            {
                int start = i;
                while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] == '_')) i++;
                string ident = s.Substring(start, i - start);

                if (i < s.Length && s[i] == '(')
                {
                    int parenDepth = 0;
                    int argStart = i;
                    while (i < s.Length)
                    {
                        if (s[i] == '(') parenDepth++;
                        else if (s[i] == ')')
                        {
                            parenDepth--;
                            if (parenDepth == 0) { i++; break; }
                        }
                        i++;
                    }
                    string inside = s.Substring(argStart + 1, Math.Max(0, i - argStart - 2 + (parenDepth == 0 ? 0 : 1)));
                    string funcToken = ident + "(" + inside + ")";
                    if (i < s.Length && s[i] == '{')
                    {
                        funcToken += "{";
                        i++;
                    }
                    tokens.Add(funcToken);
                }
                else if (i < s.Length && s[i] == '{')
                {
                    tokens.Add(ident + "{");
                    i++;
                }
                else
                {
                    tokens.Add(ident);
                }

                continue;
            }

            if (c == '{') { tokens.Add("{"); i++; continue; }

            i++;
        }

        return tokens;
    }

    // ParseSequence: parsea una secuencia de tokens en acciones (soporta Repeat).
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

            if (t.StartsWith("Repeat", StringComparison.OrdinalIgnoreCase) && t.EndsWith("{"))
            {
                var innerRepeat = t;
                int count = 1;
                try
                {
                    int open = innerRepeat.IndexOf('(');
                    int close = innerRepeat.IndexOf(')');
                    if (open >= 0 && close > open)
                    {
                        var num = innerRepeat.Substring(open + 1, close - open - 1);
                        int.TryParse(num, out count);
                    }
                }
                catch { count = 1; }

                i++;
                var inner = ParseSequence(tokens, ref i, character);
                for (int k = 0; k < count; k++) result.AddRange(inner);
                continue;
            }

            var action = TokenToAction(t, character);
            if (action != null) result.Add(action);

            i++;
        }
        return result;
    }

    // TokenToAction: convierte un token en Action conocida (Move, Wait, Shoot...).
    private static Action TokenToAction(string token, Character character)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        string norm = token.Trim();

        switch (norm)
        {
            case "MoveUp()":
            case "MoveUp": return character.MoveUp;
            case "MoveDown()":
            case "MoveDown": return character.MoveDown;
            case "MoveLeft()":
            case "MoveLeft": return character.MoveLeft;
            case "MoveRight()":
            case "MoveRight": return character.MoveRight;
            case "Wait()":
            case "Wait": return () => { };
            case "Radar":
            case "Radar()":
                Debug.LogWarning("Token 'Radar' debe usarse dentro de Shoot(...). Ignorando.");
                return null;
            case "Shoot()":
            case "Shoot":
                Debug.LogWarning("Shoot() sin argumentos ya no está permitido. Usa Shoot(...Radar...)");
                return null;
        }

        int p = norm.IndexOf('(');
        if (p > 0 && norm.EndsWith(")"))
        {
            var name = norm.Substring(0, p);
            var args = norm.Substring(p + 1, norm.Length - p - 2);

            if (string.Equals(name, "Shoot", StringComparison.OrdinalIgnoreCase))
            {
                return BuildShootWithArgsAction(args, character);
            }

            Debug.LogWarning($"Función desconocida con args: {name}(...). Token: {token}");
            return null;
        }

        Debug.LogWarning("Comando desconocido: " + token);
        return null;
    }

    // BuildShootWithArgsAction: construye la acción de Shoot(...) interpretando modificaciones y Radar.
    private static Action BuildShootWithArgsAction(string args, Character character)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(args))
        {
            var raw = args.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var r in raw) parts.Add(r.Trim());
        }

        int radarIndex = parts.FindIndex(p => string.Equals(p, "Radar", StringComparison.OrdinalIgnoreCase) || string.Equals(p, "Radar()", StringComparison.OrdinalIgnoreCase));
        if (radarIndex < 0)
        {
            Debug.LogWarning("Shoot(...) requiere 'Radar' en los argumentos. Ignorando Shoot(...): " + args);
            return null;
        }

        var modifications = parts.GetRange(0, radarIndex);

        return () =>
        {
            if (character == null || character.manager == null)
            {
                Debug.LogWarning("Shoot(...): Character o manager nulo al ejecutar acción.");
                return;
            }

            var manager = character.manager;
            var radar = manager.GetLastKnownRadarFor(character);
            UnityEngine.Vector2Int pos = radar.pos;
            var facing = radar.facing;

            Debug.Log($"[Shoot] {character.name} - radar inicial pos={pos}, facing={facing}, mods=[{string.Join(";", modifications)}]");

            Character.Facing RotateLeft(Character.Facing f)
            {
                switch (f)
                {
                    case Character.Facing.North: return Character.Facing.West;
                    case Character.Facing.West: return Character.Facing.South;
                    case Character.Facing.South: return Character.Facing.East;
                    case Character.Facing.East: return Character.Facing.North;
                    default: return f;
                }
            }
            Character.Facing RotateRight(Character.Facing f)
            {
                switch (f)
                {
                    case Character.Facing.North: return Character.Facing.East;
                    case Character.Facing.East: return Character.Facing.South;
                    case Character.Facing.South: return Character.Facing.West;
                    case Character.Facing.West: return Character.Facing.North;
                    default: return f;
                }
            }
            UnityEngine.Vector2Int DeltaForward(Character.Facing f)
            {
                switch (f)
                {
                    case Character.Facing.North: return UnityEngine.Vector2Int.up;
                    case Character.Facing.East: return UnityEngine.Vector2Int.right;
                    case Character.Facing.South: return UnityEngine.Vector2Int.down;
                    case Character.Facing.West: return UnityEngine.Vector2Int.left;
                    default: return UnityEngine.Vector2Int.zero;
                }
            }
            UnityEngine.Vector2Int DeltaRight(Character.Facing f)
            {
                switch (f)
                {
                    case Character.Facing.North: return UnityEngine.Vector2Int.right;
                    case Character.Facing.East: return UnityEngine.Vector2Int.down;
                    case Character.Facing.South: return UnityEngine.Vector2Int.left;
                    case Character.Facing.West: return UnityEngine.Vector2Int.up;
                    default: return UnityEngine.Vector2Int.zero;
                }
            }
            UnityEngine.Vector2Int DeltaLeft(Character.Facing f) => -DeltaRight(f);
            UnityEngine.Vector2Int DeltaBack(Character.Facing f) => -DeltaForward(f);

            foreach (var rawMod in modifications)
            {
                var mod = rawMod;
                if (mod.EndsWith("()")) mod = mod.Substring(0, mod.Length - 2);
                mod = mod.Trim();

                if (string.Equals(mod, "TurnLeft", StringComparison.OrdinalIgnoreCase))
                {
                    facing = RotateLeft(facing);
                }
                else if (string.Equals(mod, "TurnRight", StringComparison.OrdinalIgnoreCase))
                {
                    facing = RotateRight(facing);
                }
                else if (string.Equals(mod, "MoveUp", StringComparison.OrdinalIgnoreCase))
                {
                    pos += DeltaForward(facing);
                }
                else if (string.Equals(mod, "MoveDown", StringComparison.OrdinalIgnoreCase))
                {
                    pos += DeltaBack(facing);
                }
                else if (string.Equals(mod, "MoveLeft", StringComparison.OrdinalIgnoreCase))
                {
                    pos += DeltaLeft(facing);
                }
                else if (string.Equals(mod, "MoveRight", StringComparison.OrdinalIgnoreCase))
                {
                    pos += DeltaRight(facing);
                }
                else
                {
                    Debug.LogWarning($"Token de modificación desconocido en Shoot(...): {rawMod}");
                }
            }

            Debug.Log($"[Shoot] {character.name} - objetivo calculado pos={pos}, facingAplicado={facing}");
            character.ShootAt(pos);
        };
    }
}
