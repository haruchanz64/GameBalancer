using System;
using System.Collections.Generic;
using System.Globalization;

namespace GameBalancer.Math
{
    /// <summary>
    /// Zero-dependency recursive-descent math expression parser and evaluator.
    /// Supports <c>+</c>, <c>-</c>, <c>*</c>, <c>/</c>, right-associative <c>^</c>, unary signs,
    /// parentheses, named variables, constants (<c>pi</c>, <c>e</c>), and common math functions.
    /// </summary>
    public static class MathExpressionEvaluator
    {
        /// <summary>
        /// Parses and evaluates a math expression using the supplied variable map.
        /// Rejects division by near-zero values (&lt; 1e-12), trailing tokens, <c>NaN</c>, and infinity.
        /// </summary>
        /// <param name="expression">Formula string to evaluate.</param>
        /// <param name="variables">Optional dictionary of variable values; lookups fall back to case-insensitive matching.</param>
        /// <param name="result">Computed finite result when evaluation succeeds, or <c>0</c> on failure.</param>
        /// <param name="error">Human-readable parse or evaluation error message on failure, or <c>null</c> on success.</param>
        /// <returns><c>true</c> if the expression parsed and evaluated to a finite number; otherwise <c>false</c>.</returns>
        public static bool TryEvaluate(
            string expression,
            IReadOnlyDictionary<string, double> variables,
            out double result,
            out string error)
        {
            result = 0d;
            error = null;

            if (string.IsNullOrWhiteSpace(expression))
            {
                error = "Expression is empty.";
                return false;
            }

            try
            {
                var parser = new Parser(expression, variables);
                result = parser.ParseExpression();
                parser.SkipWhitespace();
                if (!parser.IsAtEnd)
                {
                    error = $"Unexpected character '{parser.Peek()}' at index {parser.Position}.";
                    return false;
                }

                if (double.IsNaN(result) || double.IsInfinity(result))
                {
                    error = "Expression evaluated to NaN or Infinity.";
                    return false;
                }

                return true;
            }
            catch (FormatException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Evaluates a math expression and returns <paramref name="fallback"/> if parsing or evaluation fails.
        /// </summary>
        /// <param name="expression">Formula string to evaluate.</param>
        /// <param name="variables">Optional dictionary of named variable values.</param>
        /// <param name="fallback">Default value returned when evaluation fails.</param>
        /// <returns>The evaluated finite result, or <paramref name="fallback"/> on error.</returns>
        public static double EvaluateOrDefault(
            string expression,
            IReadOnlyDictionary<string, double> variables,
            double fallback = 0d)
        {
            return TryEvaluate(expression, variables, out double value, out _) ? value : fallback;
        }

        private ref struct Parser
        {
            private readonly string _src;
            private readonly IReadOnlyDictionary<string, double> _vars;
            private int _pos;

            public int Position => _pos;
            public bool IsAtEnd => _pos >= _src.Length;

            public Parser(string source, IReadOnlyDictionary<string, double> variables)
            {
                _src = source;
                _vars = variables;
                _pos = 0;
            }

            public char Peek() => IsAtEnd ? '\0' : _src[_pos];

            public void SkipWhitespace()
            {
                while (_pos < _src.Length && char.IsWhiteSpace(_src[_pos]))
                {
                    _pos++;
                }
            }

            /// <summary>
            /// Parses additive expressions (<c>+</c> and <c>-</c>) left-to-right.
            /// </summary>
            public double ParseExpression()
            {
                double left = ParseTerm();
                while (true)
                {
                    SkipWhitespace();
                    if (Match('+'))
                    {
                        left += ParseTerm();
                    }
                    else if (Match('-'))
                    {
                        left -= ParseTerm();
                    }
                    else
                    {
                        break;
                    }
                }
                return left;
            }

            /// <summary>
            /// Parses multiplicative expressions (<c>*</c> and <c>/</c>) left-to-right, rejecting divisors smaller than 1e-12.
            /// </summary>
            private double ParseTerm()
            {
                double left = ParsePower();
                while (true)
                {
                    SkipWhitespace();
                    if (Match('*'))
                    {
                        left *= ParsePower();
                    }
                    else if (Match('/'))
                    {
                        double right = ParsePower();
                        if (System.Math.Abs(right) < 1e-12d)
                        {
                            throw new FormatException("Division by zero.");
                        }
                        left /= right;
                    }
                    else
                    {
                        break;
                    }
                }
                return left;
            }

            /// <summary>
            /// Parses exponentiation (<c>^</c>) right-associatively so <c>2 ^ 3 ^ 2</c> evaluates as <c>2 ^ (3 ^ 2)</c>.
            /// </summary>
            private double ParsePower()
            {
                double left = ParseUnary();
                SkipWhitespace();
                if (Match('^'))
                {
                    // Right-associative exponentiation
                    double right = ParsePower();
                    return System.Math.Pow(left, right);
                }
                return left;
            }

            /// <summary>
            /// Parses leading unary <c>+</c> and <c>-</c> operators before a primary expression.
            /// </summary>
            private double ParseUnary()
            {
                SkipWhitespace();
                if (Match('+')) return ParseUnary();
                if (Match('-')) return -ParseUnary();
                return ParsePrimary();
            }

            /// <summary>
            /// Parses a parenthesized sub-expression, numeric literal, constant, variable, or function call.
            /// </summary>
            private double ParsePrimary()
            {
                SkipWhitespace();
                if (IsAtEnd)
                {
                    throw new FormatException($"Unexpected end of expression at index {_pos}.");
                }

                if (Match('('))
                {
                    double value = ParseExpression();
                    SkipWhitespace();
                    if (!Match(')'))
                    {
                        throw new FormatException($"Missing closing ')' at index {_pos}.");
                    }
                    return value;
                }

                char c = Peek();
                if (char.IsDigit(c) || c == '.')
                {
                    return ParseNumber();
                }

                if (char.IsLetter(c) || c == '_')
                {
                    return ParseIdentifierOrFunction();
                }

                throw new FormatException($"Unexpected token '{c}' at index {_pos}.");
            }

            /// <summary>
            /// Parses an invariant-culture floating-point literal starting at the current cursor position.
            /// </summary>
            private double ParseNumber()
            {
                int start = _pos;
                bool hasDot = false;
                while (_pos < _src.Length)
                {
                    char c = _src[_pos];
                    if (char.IsDigit(c))
                    {
                        _pos++;
                    }
                    else if (c == '.' && !hasDot)
                    {
                        hasDot = true;
                        _pos++;
                    }
                    else
                    {
                        break;
                    }
                }

                string token = _src.Substring(start, _pos - start);
                if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                {
                    throw new FormatException($"Invalid number '{token}' at index {start}.");
                }

                return value;
            }

            /// <summary>
            /// Parses an identifier as either a built-in function call, a math constant (<c>pi</c>, <c>e</c>), or a named variable.
            /// </summary>
            private double ParseIdentifierOrFunction()
            {
                int start = _pos;
                while (_pos < _src.Length && (char.IsLetterOrDigit(_src[_pos]) || _src[_pos] == '_'))
                {
                    _pos++;
                }

                string ident = _src.Substring(start, _pos - start);
                SkipWhitespace();

                if (Match('('))
                {
                    var args = new List<double>(3);
                    SkipWhitespace();
                    if (!Match(')'))
                    {
                        while (true)
                        {
                            args.Add(ParseExpression());
                            SkipWhitespace();
                            if (Match(',')) continue;
                            if (Match(')')) break;
                            throw new FormatException($"Expected ',' or ')' in call to '{ident}' at index {_pos}.");
                        }
                    }

                    return InvokeFunction(ident, args);
                }

                if (string.Equals(ident, "pi", StringComparison.OrdinalIgnoreCase)) return System.Math.PI;
                if (string.Equals(ident, "e", StringComparison.OrdinalIgnoreCase)) return System.Math.E;

                if (_vars != null)
                {
                    if (_vars.TryGetValue(ident, out double direct))
                    {
                        return direct;
                    }

                    foreach (var kvp in _vars)
                    {
                        if (string.Equals(kvp.Key, ident, StringComparison.OrdinalIgnoreCase))
                        {
                            return kvp.Value;
                        }
                    }
                }

                throw new FormatException($"Unknown variable '{ident}' at index {start}.");
            }

            /// <summary>
            /// Dispatches built-in math function calls (<c>pow</c>, <c>sqrt</c>, <c>abs</c>, <c>floor</c>, <c>ceil</c>,
            /// <c>round</c>, <c>exp</c>, <c>ln</c>, <c>log</c>, <c>min</c>, <c>max</c>, <c>clamp</c>, <c>lerp</c>, <c>smoothstep</c>).
            /// </summary>
            private static double InvokeFunction(string name, List<double> args)
            {
                string lower = name.ToLowerInvariant();
                switch (lower)
                {
                    case "pow":
                        RequireArgs(name, args, 2);
                        return System.Math.Pow(args[0], args[1]);
                    case "sqrt":
                        RequireArgs(name, args, 1);
                        return System.Math.Sqrt(System.Math.Max(0d, args[0]));
                    case "abs":
                        RequireArgs(name, args, 1);
                        return System.Math.Abs(args[0]);
                    case "floor":
                        RequireArgs(name, args, 1);
                        return System.Math.Floor(args[0]);
                    case "ceil":
                        RequireArgs(name, args, 1);
                        return System.Math.Ceiling(args[0]);
                    case "round":
                        RequireArgs(name, args, 1);
                        return System.Math.Round(args[0], MidpointRounding.AwayFromZero);
                    case "exp":
                        RequireArgs(name, args, 1);
                        return System.Math.Exp(args[0]);
                    case "ln":
                        RequireArgs(name, args, 1);
                        return System.Math.Log(System.Math.Max(1e-9d, args[0]));
                    case "log":
                        if (args.Count == 1) return System.Math.Log10(System.Math.Max(1e-9d, args[0]));
                        RequireArgs(name, args, 2);
                        return System.Math.Log(System.Math.Max(1e-9d, args[0]), System.Math.Max(1e-9d, args[1]));
                    case "min":
                        RequireArgs(name, args, 2);
                        return System.Math.Min(args[0], args[1]);
                    case "max":
                        RequireArgs(name, args, 2);
                        return System.Math.Max(args[0], args[1]);
                    case "clamp":
                        RequireArgs(name, args, 3);
                        return System.Math.Clamp(args[0], args[1], args[2]);
                    case "lerp":
                        RequireArgs(name, args, 3);
                        return args[0] + ((args[1] - args[0]) * System.Math.Clamp(args[2], 0d, 1d));
                    case "smoothstep":
                    {
                        RequireArgs(name, args, 3);
                        double t = System.Math.Clamp((args[2] - args[0]) / System.Math.Max(1e-9d, args[1] - args[0]), 0d, 1d);
                        return t * t * (3d - (2d * t));
                    }
                    default:
                        throw new FormatException($"Unknown function '{name}'.");
                }
            }

            private static void RequireArgs(string name, List<double> args, int count)
            {
                if (args.Count != count)
                {
                    throw new FormatException($"Function '{name}' expects {count} argument(s), got {args.Count}.");
                }
            }

            private bool Match(char expected)
            {
                if (!IsAtEnd && _src[_pos] == expected)
                {
                    _pos++;
                    return true;
                }
                return false;
            }
        }
    }
}
